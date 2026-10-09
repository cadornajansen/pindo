import AppKit
import SwiftUI

@main
struct PinDoApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    var body: some Scene {
        MenuBarExtra("PinDo Pro", systemImage: "hand.point.up.left.fill") {
            Button("Open Quick Bar (Fn Space)") { delegate.quickBar.show() }
            Button("Accessibility Settings…") {
                NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
            }
            Divider()
            Button("Quit PinDo") { NSApp.terminate(nil) }.keyboardShortcut("q")
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    let quickBar = QuickBar()
    private lazy var hotKey = FnSpaceHotKey { [quickBar] in quickBar.toggle() }

    func applicationDidFinishLaunching(_ notification: Notification) {
        AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": true] as CFDictionary)
        startHotKey()
        Task { try? await Ollama.warm() }
    }

    // The tap can't be created until Accessibility is granted; keep retrying so it
    // starts working the moment the user flips the switch, no relaunch needed.
    private func startHotKey() {
        if !hotKey.start() {
            DispatchQueue.main.asyncAfter(deadline: .now() + 2) { self.startHotKey() }
        }
    }
}

/// Fn + Space via a CGEventTap. Fn is a modifier flag (.maskSecondaryFn) that
/// RegisterEventHotKey can't express, and the tap lets us swallow the Space so
/// it never reaches the frontmost app.
final class FnSpaceHotKey {
    private let onTrigger: () -> Void
    private var tap: CFMachPort?
    private var swallowNextSpaceUp = false
    private static let spaceKeyCode: Int64 = 49

    init(onTrigger: @escaping () -> Void) { self.onTrigger = onTrigger }

    func start() -> Bool {
        guard tap == nil else { return true }
        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue | 1 << CGEventType.keyUp.rawValue)
        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap, place: .headInsertEventTap, options: .defaultTap,
            eventsOfInterest: mask,
            callback: { _, type, event, refcon in
                let me = Unmanaged<FnSpaceHotKey>.fromOpaque(refcon!).takeUnretainedValue()
                let keyCode = event.getIntegerValueField(.keyboardEventKeycode)
                let isRepeat = event.getIntegerValueField(.keyboardEventAutorepeat) != 0
                let flags = event.flags.rawValue
                // The run-loop source is on the main run loop, so we're on the main thread.
                let swallow = MainActor.assumeIsolated {
                    me.shouldSwallow(type, keyCode: keyCode, flags: CGEventFlags(rawValue: flags), isRepeat: isRepeat)
                }
                return swallow ? nil : Unmanaged.passUnretained(event)
            },
            userInfo: Unmanaged.passUnretained(self).toOpaque()
        ) else { return false }
        self.tap = tap
        CFRunLoopAddSource(CFRunLoopGetMain(), CFMachPortCreateRunLoopSource(nil, tap, 0), .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        return true
    }

    private func shouldSwallow(_ type: CGEventType, keyCode: Int64, flags: CGEventFlags, isRepeat: Bool) -> Bool {
        // macOS disables taps that stall or on secure input; turn it straight back on.
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            return false
        }
        guard keyCode == Self.spaceKeyCode else { return false }
        if type == .keyDown, flags.contains(.maskSecondaryFn) {
            if !isRepeat { onTrigger() }
            swallowNextSpaceUp = true
            return true
        }
        if type == .keyUp, swallowNextSpaceUp {
            swallowNextSpaceUp = false
            return true
        }
        return false
    }
}
