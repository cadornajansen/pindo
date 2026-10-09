import AppKit
import ApplicationServices
import ScreenCaptureKit
import CryptoKit

struct TutorTarget: Equatable {
    let pid: pid_t
    let windowID: CGWindowID
    let appName: String
    let bundleID: String?
    let host: String?
    let browserTitle: String?
}

struct TutorObservation {
    let text: String
    let image: Data
    let fingerprint: String
}

enum TutorCapture {
    static func target() throws -> TutorTarget {
        guard let app = NSWorkspace.shared.frontmostApplication,
              app.processIdentifier != ProcessInfo.processInfo.processIdentifier,
              let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]],
              let window = windows.first(where: {
                  ($0[kCGWindowOwnerPID as String] as? pid_t) == app.processIdentifier && ($0[kCGWindowLayer as String] as? Int) == 0
              }), let id = window[kCGWindowNumber as String] as? CGWindowID else {
            throw CaptureError.unavailable("Bring the application window to the front, then resume.")
        }
        let browserIDs = ["com.apple.Safari", "com.google.Chrome", "com.microsoft.edgemac", "org.mozilla.firefox", "company.thebrowser.Browser"]
        return TutorTarget(pid: app.processIdentifier, windowID: id, appName: app.localizedName ?? "Application",
                           bundleID: app.bundleIdentifier, host: browserHost(pid: app.processIdentifier),
                           browserTitle: browserIDs.contains(app.bundleIdentifier ?? "") ? window[kCGWindowName as String] as? String : nil)
    }

    private static func browserHost(pid: pid_t) -> String? {
        // Read the focused window's document URL, never an arbitrary link in its page.
        let app = AXUIElementCreateApplication(pid)
        AXUIElementSetMessagingTimeout(app, 0.15)
        var window: CFTypeRef?
        guard AXUIElementCopyAttributeValue(app, kAXFocusedWindowAttribute as CFString, &window) == .success,
              let window, CFGetTypeID(window) == AXUIElementGetTypeID() else { return nil }
        var document: CFTypeRef?
        let element = unsafeDowncast(window, to: AXUIElement.self)
        guard AXUIElementCopyAttributeValue(element, kAXDocumentAttribute as CFString, &document) == .success else { return nil }
        if let value = document as? String { return URL(string: value)?.host?.lowercased() }
        return (document as? URL)?.host?.lowercased()
    }

    static func capture(target: TutorTarget, task: String) async throws -> TutorObservation {
        guard AXIsProcessTrusted() else { throw CaptureError.unavailable("Enable Accessibility for PinDo in System Settings, then resume.") }
        guard CGPreflightScreenCaptureAccess() else {
            CGRequestScreenCaptureAccess()
            throw CaptureError.unavailable("Enable Screen Recording for PinDo in System Settings, then resume. A restart may be required.")
        }
        guard try self.target() == target else { throw CaptureError.unavailable("The application, tab, or window changed. Return to the teaching window and resume.") }
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        guard let window = content.windows.first(where: { $0.windowID == target.windowID && $0.owningApplication?.processID == target.pid }) else {
            throw CaptureError.unavailable("The teaching window is no longer available.")
        }
        let configuration = SCStreamConfiguration()
        let scale = min(1, 1280 / max(window.frame.width, window.frame.height))
        configuration.width = max(1, Int(window.frame.width * scale))
        configuration.height = max(1, Int(window.frame.height * scale))
        configuration.showsCursor = false
        let image = try await SCScreenshotManager.captureImage(contentFilter: SCContentFilter(desktopIndependentWindow: window), configuration: configuration)
        try Task.checkCancellation()
        guard try self.target() == target else { throw CaptureError.unavailable("The target changed during observation. Resume in the intended window.") }
        let bitmap = NSBitmapImageRep(cgImage: image)
        guard let data = bitmap.representation(using: .jpeg, properties: [.compressionFactor: 0.65]) else {
            throw CaptureError.unavailable("Could not encode the teaching window.")
        }
        let snapshot = AX.snapshot(pid: target.pid, appName: target.appName, task: task)
        let controls = snapshot.candidates.filter { !$0.id.hasPrefix("m") }.map(\.line).joined(separator: "\n")
        let menus = snapshot.candidates.filter { $0.id.hasPrefix("m") }.map(\.line).joined(separator: "\n")
        let text = String("Window controls (presence is not proof of completion):\n\(controls)\nMenu capabilities (may be CLOSED; not visibility evidence):\n\(menus)".prefix(12_000))
        var digest = SHA256()
        digest.update(data: data)
        digest.update(data: Data(text.utf8))
        return TutorObservation(text: text, image: data, fingerprint: digest.finalize().map { String(format: "%02x", $0) }.joined())
    }

    enum CaptureError: LocalizedError {
        case unavailable(String)
        var errorDescription: String? { switch self { case .unavailable(let message): message } }
    }
}

/// Activity is only a wake-up signal. No key text, clipboard, or screenshots are retained.
final class TutorWatcher {
    private var monitor: Any?
    private var observer: AXObserver?
    private var applicationObserver: NSObjectProtocol?
    private var callback: (@MainActor @Sendable () -> Void)?

    func start(pid: pid_t, changed: @escaping @MainActor @Sendable () -> Void) {
        stop()
        callback = changed
        monitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseUp, .rightMouseUp, .keyUp, .scrollWheel]) { _ in
            MainActor.assumeIsolated { changed() }
        }
        applicationObserver = NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main) { _ in
            MainActor.assumeIsolated { changed() }
        }
        var created: AXObserver?
        let result = AXObserverCreate(pid, { _, _, _, context in
            guard let context else { return }
            MainActor.assumeIsolated {
                Unmanaged<TutorWatcher>.fromOpaque(context).takeUnretainedValue().callback?()
            }
        }, &created)
        if result == .success, let created {
            observer = created
            let app = AXUIElementCreateApplication(pid)
            for notification in [kAXFocusedUIElementChangedNotification, kAXFocusedWindowChangedNotification, kAXValueChangedNotification, kAXSelectedChildrenChangedNotification] {
                AXObserverAddNotification(created, app, notification as CFString, Unmanaged.passUnretained(self).toOpaque())
            }
            CFRunLoopAddSource(CFRunLoopGetMain(), AXObserverGetRunLoopSource(created), .commonModes)
        }
    }

    func stop() {
        if let monitor { NSEvent.removeMonitor(monitor) }
        if let applicationObserver { NSWorkspace.shared.notificationCenter.removeObserver(applicationObserver) }
        if let observer { CFRunLoopRemoveSource(CFRunLoopGetMain(), AXObserverGetRunLoopSource(observer), .commonModes) }
        monitor = nil
        applicationObserver = nil
        observer = nil
        callback = nil
    }
}
