import AppKit
import SwiftUI

@main
struct PinDoApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate

    var body: some Scene {
        MenuBarExtra("PinDo Pro", systemImage: "hand.point.up.left.fill") {
            Button("Open Quick Bar (Fn Space)") { delegate.quickBar.show() }
            SettingsLink { Text("Settings…") }.keyboardShortcut(",")
            Button("Accessibility Settings…") {
                NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!)
            }
            Divider()
            Button("Quit PinDo") { NSApp.terminate(nil) }.keyboardShortcut("q")
        }
        Settings { SettingsView() }
    }
}

/// Local model status and the optional cloud services. Everything cloud is off until the user turns it on.
struct SettingsView: View {
    @AppStorage("voiceInput") private var voiceInput = false
    @AppStorage("voiceOutput") private var voiceOutput = false
    @AppStorage("cloudReasoning") private var cloudReasoning = false
    @AppStorage("cloudModelEnabled") private var cloudModelEnabled = false
    @State private var localStatus = "Checking…"

    var body: some View {
        Form {
            Section {
                LabeledContent("\(Ollama.displayName) (Ollama)", value: localStatus)
            } header: { Text("Local model") } footer: {
                Text("Handles every request. Screenshots and screen content never leave this Mac.").foregroundStyle(.secondary)
            }
            Section {
                Toggle("Voice input (AssemblyAI)", isOn: $voiceInput)
                KeyField(provider: .assemblyAI)
                Toggle("Speak answers (ElevenLabs)", isOn: $voiceOutput)
                KeyField(provider: .elevenLabs)
                Toggle("Cloud answers when the local model is down (OpenRouter)", isOn: $cloudReasoning)
                KeyField(provider: .openRouter)
                Toggle("Use cloud model for guidance (OpenRouter, sends screenshots)", isOn: $cloudModelEnabled)
            } header: { Text("Cloud (optional)") } footer: {
                Text("Voice input sends your recording to AssemblyAI. Spoken answers send the answer text to ElevenLabs. "
                     + "Cloud answers send only your typed question to OpenRouter, never screen content. Keys are stored in your Keychain.")
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
        .frame(width: 520)
        .task { localStatus = await Ollama.status() }
        .onAppear { NSApp.activate() } // menu-bar app: bring the window in front of the current app
    }
}

private struct KeyField: View {
    let provider: Provider
    @State private var draft = ""
    @State private var saved = false

    var body: some View {
        HStack {
            SecureField("\(provider.name) key", text: $draft, prompt: Text(saved ? "Saved in Keychain" : "Not set"))
            Button("Save") {
                saved = Keychain.save(draft.trimmingCharacters(in: .whitespacesAndNewlines), for: provider.rawValue)
                draft = ""
            }
            .disabled(draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            if saved {
                Button("Remove") { Keychain.delete(provider.rawValue); saved = false }
            }
        }
        .onAppear { saved = provider.hasKey }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    let quickBar = QuickBar()
    private lazy var hotKey = FnSpaceHotKey { [quickBar] in quickBar.toggle() }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // One Pindo at a time: a second copy would register a second Fn+Space tap and a second quick bar.
        if let other = NSRunningApplication.runningApplications(withBundleIdentifier: Bundle.main.bundleIdentifier ?? "")
            .first(where: { $0 != .current }) {
            other.activate()
            NSApp.terminate(nil)
            return
        }
        for problem in Config.problems() { agentLog.error("settings: \(problem, privacy: .public); using the default") }
        AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": true] as CFDictionary)
        startHotKey()
        Task { try? await Ollama.warm() }
        #if DEBUG
        // Test hook for scripted checks (debug builds only):
        // a distributed notification named com.pindopro.PinDo.debug.run with the task as its object.
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.run"), object: nil, queue: .main) { [quickBar] note in
            let task = note.object as? String
            MainActor.assumeIsolated {
                guard let task else { return }
                // "guide: …" / "do: …" / "teach: …" force a mode; otherwise the intent policy decides.
                let mode = QuickBarModel.Mode.allCases.first { task.lowercased().hasPrefix($0.rawValue.lowercased() + ":") }
                quickBar.submit(mode.map { String(task.dropFirst($0.rawValue.count + 1)).trimmingCharacters(in: .whitespaces) } ?? task, mode: mode)
            }
        }
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.check"), object: nil, queue: .main) { [quickBar] _ in
            MainActor.assumeIsolated { quickBar.checkAgain() }
        }
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.stop"), object: nil, queue: .main) { [quickBar] _ in
            MainActor.assumeIsolated { quickBar.cancel() }
        }
        // Renders Settings off-screen to $TMPDIR/pindo-settings.png.
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.settings"), object: nil, queue: .main) { _ in
            MainActor.assumeIsolated {
                let window = NSWindow(contentRect: NSRect(x: -4000, y: -4000, width: 520, height: 520), styleMask: [.titled], backing: .buffered, defer: false)
                window.contentView = NSHostingView(rootView: SettingsView())
                window.orderFrontRegardless()
                DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) {
                    if let view = window.contentView, let rep = view.bitmapImageRepForCachingDisplay(in: view.bounds) {
                        view.cacheDisplay(in: view.bounds, to: rep)
                        try? rep.representation(using: .png, properties: [:])?.write(to: FileManager.default.temporaryDirectory.appending(path: "pindo-settings.png"))
                    }
                    window.orderOut(nil)
                }
            }
        }
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.mic"), object: nil, queue: .main) { [quickBar] _ in
            MainActor.assumeIsolated { quickBar.toggleVoice() }
        }
        // Object: path of an audio file, sent through the same transcription path as a recording.
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.transcribe"), object: nil, queue: .main) { [quickBar] note in
            let path = note.object as? String
            MainActor.assumeIsolated { if let path { quickBar.transcribe(file: URL(filePath: path)) } }
        }
        DistributedNotificationCenter.default().addObserver(forName: .init("com.pindopro.PinDo.debug.snapshot"), object: nil, queue: .main) { [quickBar] note in
            let name = (note.object as? String) ?? "panel"
            MainActor.assumeIsolated { quickBar.snapshot(to: FileManager.default.temporaryDirectory.appending(path: "pindo-\(name).png")) }
        }
        #endif
    }

    func applicationWillTerminate(_ notification: Notification) {
        quickBar.cancel() // stops model requests, recordings and speech
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
