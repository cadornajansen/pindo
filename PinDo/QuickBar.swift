import AppKit
import SwiftUI

/// Owns the quick-bar panel. The panel is built once at launch and only shown/hidden
/// afterwards, so it appears instantly.
final class QuickBar {
    private let model = QuickBarModel()
    private lazy var panel = QuickBarPanel(rootView: QuickBarView(model: model, onClose: { [weak self] in self?.hide() }),
                                           onResignKey: { [weak self] in if self?.model.isIdle == true { self?.hide() } })
    private var isShown = false

    init() {
        _ = panel
        // While guiding, keep the bar from covering the control it points at.
        model.guide.onTarget = { [weak self] point in
            guard let self, self.isShown, self.panel.frame.insetBy(dx: -40, dy: -40).contains(point),
                  let screen = NSScreen.screens.first(where: { $0.frame.contains(point) }) else { return }
            let y = point.y > screen.visibleFrame.midY ? screen.visibleFrame.minY + 12 : screen.visibleFrame.maxY - self.panel.frame.height - 12
            self.panel.animator().setFrameOrigin(NSPoint(x: self.panel.frame.minX, y: y))
        }
    }

    func checkAgain() { model.guide.checkAgain() }
    func toggleVoice() { show(); model.toggleVoice() }

    #if DEBUG
    /// Debug: renders the panel's views (layout and text; system glass needs the window server) to a PNG.
    func snapshot(to url: URL) {
        guard let view = panel.contentView, let rep = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return }
        view.cacheDisplay(in: view.bounds, to: rep)
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
    }
    #endif
    func cancel() { model.cancel() }
    #if DEBUG
    /// Debug: runs an audio file through the same transcription path as a recording.
    func transcribe(file: URL) {
        show()
        guard let audio = try? Data(contentsOf: file) else { return }
        model.transcribe(audio)
    }
    #endif

    func toggle() { isShown ? hide() : show() }

    func submit(_ text: String, mode: QuickBarModel.Mode? = nil) {
        show()
        if let mode { model.run(text, as: mode) } else { model.text = text; model.send() }
    }

    func show() {
        guard !isShown else { return }
        isShown = true
        let origin = Self.anchorOrigin(panelSize: panel.frame.size)
        let rise: CGFloat = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion ? 0 : 8 // fade in while moving up
        panel.alphaValue = 0
        panel.setFrameOrigin(NSPoint(x: origin.x, y: origin.y - rise))
        panel.makeKeyAndOrderFront(nil)
        NSAnimationContext.runAnimationGroup { ctx in
            ctx.duration = 0.18
            ctx.timingFunction = CAMediaTimingFunction(name: .easeOut)
            panel.animator().alphaValue = 1
            panel.animator().setFrameOrigin(origin)
        }
        model.focusTick += 1
    }

    func hide() {
        guard isShown else { return }
        isShown = false
        model.cancel()
        let fall: CGFloat = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion ? 0 : 8 // fade out while moving down
        NSAnimationContext.runAnimationGroup({ ctx in
            ctx.duration = 0.14
            ctx.timingFunction = CAMediaTimingFunction(name: .easeIn)
            panel.animator().alphaValue = 0
            panel.animator().setFrameOrigin(NSPoint(x: panel.frame.minX, y: panel.frame.minY - fall))
        }, completionHandler: { [panel] in
            // A quick re-open during the fade-out must not be hidden by this callback.
            MainActor.assumeIsolated { if panel.alphaValue == 0 { panel.orderOut(nil) } }
        })
    }

    /// Bottom-centre of the frontmost app's window (Cocoa coordinates), so the bar feels
    /// attached to what you're working on. Falls back to the screen under the mouse.
    private static func anchorOrigin(panelSize: NSSize) -> NSPoint {
        let margin: CGFloat = 12
        var anchor: NSRect?
        if let pid = NSWorkspace.shared.frontmostApplication?.processIdentifier,
           let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]],
           let info = windows.first(where: { ($0[kCGWindowOwnerPID as String] as? pid_t) == pid && ($0[kCGWindowLayer as String] as? Int) == 0 }),
           let boundsDict = info[kCGWindowBounds as String] as? NSDictionary,
           let cg = CGRect(dictionaryRepresentation: boundsDict),
           cg.width > 300, cg.height > 200 {
            // CG window bounds are top-left origin on the primary display; Cocoa is bottom-left.
            let primaryHeight = NSScreen.screens.first?.frame.maxY ?? 0
            anchor = NSRect(x: cg.minX, y: primaryHeight - cg.maxY, width: cg.width, height: cg.height)
        }
        let mouse = NSEvent.mouseLocation
        let screen = NSScreen.screens.first { $0.frame.contains(anchor.map { NSPoint(x: $0.midX, y: $0.midY) } ?? mouse) }
            ?? NSScreen.main ?? NSScreen.screens[0]
        let visible = screen.visibleFrame
        let base = anchor ?? NSRect(x: visible.minX, y: visible.minY + 80, width: visible.width, height: visible.height)
        let x = min(max(base.midX - panelSize.width / 2, visible.minX), visible.maxX - panelSize.width)
        let y = min(max(base.minY + margin, visible.minY), visible.maxY - panelSize.height)
        return NSPoint(x: x, y: y)
    }
}

/// Borderless, non-activating panel: it takes keystrokes while the app underneath stays
/// frontmost (that's the app PinDo will look at and point into).
final class QuickBarPanel: NSPanel {
    private let onResignKey: () -> Void

    init(rootView: some View, onResignKey: @escaping () -> Void) {
        self.onResignKey = onResignKey
        // Fixed, mostly transparent canvas: the bar sits at the bottom and the answer grows
        // upward inside it, so we never resize the window mid-animation. Clicks on the
        // transparent area fall through to the app below.
        super.init(contentRect: NSRect(x: 0, y: 0, width: 680, height: 460),
                   styleMask: [.nonactivatingPanel, .borderless, .fullSizeContentView],
                   backing: .buffered, defer: false)
        isFloatingPanel = true
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .transient]
        backgroundColor = .clear
        isOpaque = false
        hasShadow = false // SwiftUI draws the shadow; window shadows lag behind content changes
        hidesOnDeactivate = false
        let host = NSHostingView(rootView: rootView)
        host.sizingOptions = [] // keep the panel's size fixed; it's positioned once per show
        contentView = host
    }

    override var canBecomeKey: Bool { true }

    override func resignKey() {
        super.resignKey()
        onResignKey() // clicking anywhere else dismisses
    }
}

@Observable
final class QuickBarModel {
    var text = ""
    var answer = ""
    var isBusy = false
    var awaitingApproval = false
    var focusTick = 0
    /// Internal only: chosen per request by IntentPolicy (or by the user when it asks). Never shown as a switch.
    enum Mode: String, CaseIterable { case guide = "Guide", act = "Do", teach = "Teach" }
    private(set) var mode: Mode = .act
    /// A request the policy couldn't place: the UI asks "Show me, or do it for you?".
    private(set) var pendingChoice: String?
    /// Voice: recording, waiting for the transcript, or reading the answer aloud.
    private(set) var listening = false
    private(set) var transcribing = false
    private(set) var speaking = false
    /// The transcript of the last spoken request, shown with its result.
    private(set) var heard: String?
    var teachMode: Bool { mode == .teach && tutor.active }
    let tutor = TutorSession()
    let guide = GuideSession()
    private var task: Task<Void, Never>?
    private var approval: CheckedContinuation<Bool, Never>?
    private let recorder = Recorder()
    private let speaker = Speaker()
    /// Bumped by every cancel/new request. Async results carrying an older value are dropped, so a cancelled or
    /// superseded request can never change the UI or start more work.
    private var generation = 0

    /// What the panel shows, derived from the real backend state (never simulated).
    enum Phase: Equatable { case composing, listening, transcribing, working, approval, choosing }
    var phase: Phase {
        if awaitingApproval { return .approval }
        if listening { return .listening }
        if transcribing { return .transcribing }
        if isBusy || guide.working { return .working }
        if pendingChoice != nil { return .choosing }
        return .composing
    }

    /// Nothing in flight, so clicking elsewhere may dismiss the bar.
    var isIdle: Bool { phase == .composing && !speaking && !tutor.active && !guide.active }

    /// Latest real progress for the slim working row.
    var status: String {
        if transcribing { return "Transcribing with AssemblyAI (cloud)…" }
        if guide.working { return "Looking at the screen…" }
        if let step = answer.split(separator: "\n").last(where: { $0.hasPrefix("▸") }) { return String(step.dropFirst(2)) }
        if let heard { return "“\(heard)”" }
        return "Thinking…"
    }

    func send() {
        if awaitingApproval { return resolveApproval(true) } // ↩ = allow
        let prompt = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !prompt.isEmpty, phase == .composing else { return } // no duplicate submissions
        text = ""
        heard = nil
        switch IntentPolicy.decide(prompt) {
        case .guide: run(prompt, as: .guide)
        case .act: run(prompt, as: .act)
        case .teach: run(prompt, as: .teach)
        case .clarify:
            cancel()
            answer = ""
            pendingChoice = prompt
        }
    }

    /// The user's answer to "Show me, or do it for you?".
    func choose(_ mode: Mode) {
        guard let prompt = pendingChoice else { return }
        pendingChoice = nil
        run(prompt, as: mode)
    }

    func run(_ prompt: String, as mode: Mode) {
        cancel()
        self.mode = mode
        answer = ""
        let id = generation
        switch mode {
        case .teach:
            tutor.start(prompt)
        case .guide:
            guide.start(prompt) { [weak self] in if self?.generation == id { self?.answer = $0 } } // the card shows the current step only
        case .act:
            isBusy = true
            task = Task {
                do {
                    try await Agent.run(task: prompt,
                                        report: { line in if self.generation == id { self.say(line) } },
                                        confirm: { question in self.generation == id ? await self.askApproval(question) : false })
                } catch {
                    guard generation == id, !Task.isCancelled else { return }
                    await recover(from: error, prompt: prompt, id: id)
                }
                guard generation == id else { return }
                isBusy = false
                speakAnswer(id)
            }
        }
    }

    /// Local model failures get a truthful message. If the user turned on cloud answers, a plain question
    /// (text only, no screen content) may be answered by OpenRouter instead, and the card says so.
    private func recover(from error: Error, prompt: String, id: Int) async {
        let localDown = error is URLError || error is Ollama.ModelError
        guard localDown, Provider.openRouter.isEnabled, IntentPolicy.isQuestion(prompt) else { return say(Self.describe(error)) }
        guard let key = Provider.openRouter.key else {
            return say(Self.describe(error) + "\nCloud answers are on, but no OpenRouter key is saved in Settings.")
        }
        say("☁︎ The local model is unavailable, so OpenRouter (cloud) answered. Only your question was sent.")
        do {
            let reply = try await Cloud.answer(prompt, key: key)
            if generation == id { say(reply) }
        } catch {
            if generation == id, !Task.isCancelled { say(error.localizedDescription) }
        }
    }

    static func describe(_ error: Error) -> String {
        switch error {
        case let error as Ollama.ModelError: error.localizedDescription
        case is DecodingError: "The local model gave an answer Pindo couldn't read, so nothing more was done. Try again or rephrase."
        case let error as URLError where error.code == .timedOut: "The local model took too long to answer. Try again."
        case is URLError: "Couldn't reach the local model. Is Ollama running? (ollama serve)"
        default: error.localizedDescription
        }
    }

    func cancel() {
        generation += 1
        let wasWorking = isBusy || awaitingApproval || transcribing
        tutor.stop()
        guide.stop()
        task?.cancel()
        task = nil
        resolveApproval(false)
        pendingChoice = nil
        isBusy = false
        if listening { recorder.discard() }
        listening = false
        transcribing = false
        stopSpeaking()
        if wasWorking, !answer.isEmpty { say("Stopped.") }
    }

    func dismissResult() {
        stopSpeaking()
        answer = ""
        heard = nil
    }

    // MARK: Voice (optional, cloud)

    /// Mic button: start recording, or finish it. Never records unless the user turned voice input on.
    func toggleVoice() {
        if listening { return finishListening() }
        guard phase == .composing else { return }
        guard Provider.assemblyAI.isEnabled else { return note("Voice input is off. Turn it on in PinDo Settings (menu bar icon ▸ Settings…).") }
        guard Keychain.has(Provider.assemblyAI.rawValue) else { return note("Add an AssemblyAI API key in PinDo Settings to use voice input.") }
        cancel()
        let id = generation
        Task {
            guard await Recorder.permission() else {
                return note("Pindo needs microphone access. Allow it in System Settings → Privacy & Security → Microphone.")
            }
            guard generation == id else { return }
            do {
                try recorder.start()
                answer = ""
                heard = nil
                listening = true
            } catch {
                return note(error.localizedDescription)
            }
            try? await Task.sleep(for: .seconds(60)) // bounded: a forgotten recording stops itself
            if generation == id, listening { finishListening() }
        }
    }

    func finishListening() {
        guard listening else { return }
        listening = false
        guard let (audio, seconds) = recorder.stop(), seconds >= 0.5 else {
            return note("That recording was too short. Click the mic, speak, then click Done.")
        }
        transcribe(audio)
    }

    /// Sends the recording to AssemblyAI and submits the transcript like a typed request.
    func transcribe(_ audio: Data) {
        guard let key = Provider.assemblyAI.key else { return note("Add an AssemblyAI API key in PinDo Settings to use voice input.") }
        let id = generation
        transcribing = true
        task = Task {
            do {
                let started = Date()
                let transcript = try await Cloud.transcribe(audio, key: key).trimmingCharacters(in: .whitespacesAndNewlines)
                agentLog.info("voice: transcribed \(transcript.count) chars in \(Date().timeIntervalSince(started), format: .fixed(precision: 1))s")
                guard generation == id else { return }
                transcribing = false
                guard !transcript.isEmpty else { return note("I didn't catch anything. Try again a little closer to the mic.") }
                text = transcript
                send()
                heard = transcript
            } catch {
                guard generation == id, !Task.isCancelled else { return }
                agentLog.info("voice: transcription failed")
                transcribing = false
                note(error.localizedDescription)
            }
        }
    }

    /// Reads the final answer aloud when voice output is on. Speaking never triggers any computer action.
    private func speakAnswer(_ id: Int) {
        guard Provider.elevenLabs.isEnabled, let text = Cloud.spokenText(answer) else { return }
        guard let key = Provider.elevenLabs.key else { return say("Voice output is on, but no ElevenLabs key is saved in Settings.") }
        speaking = true
        task = Task {
            do {
                let audio = try await Cloud.speech(text, key: key)
                guard generation == id, speaking else { return }
                agentLog.info("speech: playing \(audio.count) bytes")
                try await speaker.play(audio)
                agentLog.info("speech: finished")
            } catch {
                agentLog.info("speech: \(Task.isCancelled ? "stopped" : "failed", privacy: .public)")
                if generation == id, speaking, !Task.isCancelled { say(error.localizedDescription) }
            }
            if generation == id { speaking = false }
        }
    }

    func stopSpeaking() {
        guard speaking else { return }
        speaking = false
        speaker.stop()
        task?.cancel()
    }

    // MARK: -

    private func note(_ message: String) {
        answer = message
    }

    private func say(_ line: String) {
        answer = answer.isEmpty ? line : answer + "\n" + line
    }

    private func askApproval(_ question: String) async -> Bool {
        say("⚠︎ \(question)")
        awaitingApproval = true
        return await withCheckedContinuation { approval = $0 }
    }

    private func resolveApproval(_ allowed: Bool) {
        awaitingApproval = false
        approval?.resume(returning: allowed)
        approval = nil
    }
}

/// Colors from the Figma file that the pointer overlay still uses.
enum Figma {
    static let stepNumber = Color(red: 243 / 255, green: 1, blue: 70 / 255)     // #F3FF46
    static let pointer = Color(red: 227 / 255, green: 1, blue: 69 / 255)       // #E3FF45 (Polygon 1)
}

/// Native glass: Liquid Glass on macOS 26, a regular material before. A hairline edge and a light shadow
/// separate it from the desktop. Text on it uses system label colors, so it stays legible on light and dark.
struct GlassBackground<S: InsettableShape>: View {
    let shape: S

    var body: some View {
        Group {
            if #available(macOS 26.0, *) {
                backing.glassEffect(.regular, in: shape)
            } else {
                backing.background(shape.fill(.regularMaterial))
            }
        }
        .overlay(shape.strokeBorder(LinearGradient(colors: [.white.opacity(0.35), .white.opacity(0.06)],
                                                   startPoint: .top, endPoint: .bottom), lineWidth: 0.75))
        .shadow(color: .black.opacity(0.14), radius: 12, y: 5)
    }

    /// The bar's own appearance, read outside the glass. Liquid Glass re-resolves dynamic colors for what's behind
    /// it, so a dynamic backing turned white over white pages while the text (outside the glass) stayed white.
    @Environment(\.colorScheme) private var scheme

    /// A fixed backing in the bar's appearance keeps text contrast on any background.
    private var backing: some View {
        shape.fill(scheme == .dark ? Color(white: 0.12, opacity: 0.82) : Color(white: 0.98, opacity: 0.85))
    }
}

private struct ContentHeight: PreferenceKey {
    static let defaultValue: CGFloat = 0
    static func reduce(value: inout CGFloat, nextValue: () -> CGFloat) { value = max(value, nextValue()) }
}

/// Small borderless control with hover and press feedback.
private struct QuietButtonStyle: ButtonStyle {
    var prominent = false
    @State private var hovering = false

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 12.5, weight: .semibold))
            .foregroundStyle(prominent ? Color.white : Color.primary)
            .padding(.horizontal, 11).padding(.vertical, 5)
            .background(Capsule().fill(prominent ? Color.accentColor : Color.primary.opacity(hovering ? 0.14 : 0.08)))
            .scaleEffect(configuration.isPressed ? 0.96 : 1)
            .opacity(configuration.isPressed ? 0.85 : 1)
            .onHover { hovering = $0 }
            .animation(.easeOut(duration: 0.12), value: hovering)
    }
}

struct QuickBarView: View {
    @Bindable var model: QuickBarModel
    let onClose: () -> Void
    @FocusState private var focused: Bool
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var teachHeight: CGFloat = 120
    @State private var pulse = false
    @AppStorage("voiceInput") private var voiceInput = false

    private static let radius: CGFloat = 14
    private var maxWidth: CGFloat { 640 }
    /// Grows with what's typed (not animated per keystroke), within 460…640 pt.
    private var composerWidth: CGFloat { min(maxWidth, max(460, 250 + CGFloat(model.text.count) * 7.5)) }

    var body: some View {
        VStack(spacing: 8) {
            Spacer(minLength: 0)
            if model.teachMode {
                // Sized to the lesson's content (scrolls past 260 pt) instead of always stretching to the maximum.
                ScrollView {
                    TutorView(session: model.tutor)
                        .background(GeometryReader { g in Color.clear.preference(key: ContentHeight.self, value: g.size.height) })
                }
                .frame(height: min(260, teachHeight))
                .onPreferenceChange(ContentHeight.self) { teachHeight = $0 }
                .frame(width: composerWidth)
                    .background(GlassBackground(shape: RoundedRectangle(cornerRadius: Self.radius, style: .continuous)))
                    .transition(cardTransition)
            }
            if let card = cardContent {
                card.transition(cardTransition)
            }
            ZStack {
                switch model.phase {
                case .working, .transcribing: workingRow.transition(workingTransition)
                case .listening: listeningRow.transition(workingTransition)
                case .composing: composer.transition(composerTransition)
                case .approval, .choosing: EmptyView()
                }
            }
            .frame(height: 48)
        }
        .padding(.horizontal, 20).padding(.vertical, 18) // room for the shadow inside the transparent panel
        .frame(width: 680, height: 460, alignment: .bottom) // the panel never resizes; content anchors to its bottom
        .animation(motion, value: model.phase)
        .animation(motion, value: model.answer.isEmpty)
        .animation(motion, value: model.guide.active)
        .onChange(of: model.focusTick) { focused = true }
        .onChange(of: model.phase) { if model.phase == .composing { focused = true } }
    }

    // MARK: Motion

    private var motion: Animation { reduceMotion ? .easeInOut(duration: 0.15) : .spring(response: 0.32, dampingFraction: 0.92) }
    /// Composer: in = fade while rising, out = fade while sinking.
    private var composerTransition: AnyTransition {
        reduceMotion ? .opacity : .asymmetric(insertion: .opacity.combined(with: .offset(y: 10)),
                                              removal: .opacity.combined(with: .offset(y: 10)))
    }
    /// Thinking row grows out of the space the composer leaves.
    private var workingTransition: AnyTransition {
        reduceMotion ? .opacity : .asymmetric(insertion: .opacity.combined(with: .scale(scale: 0.97, anchor: .bottom)), removal: .opacity)
    }
    private var cardTransition: AnyTransition { reduceMotion ? .opacity : .opacity.combined(with: .offset(y: 6)) }

    // MARK: Composer (idle)

    private var composer: some View {
        HStack(spacing: 10) {
            TextField("", text: $model.text, prompt: Text("What can I help you with?").foregroundStyle(.secondary))
                .textFieldStyle(.plain)
                .font(.system(size: 15))
                .focused($focused)
                .onSubmit(model.send)
                .onExitCommand(perform: onClose)
            Text(modelName)
                .font(.system(size: 11, weight: .medium))
                .foregroundStyle(.tertiary)
                .help("Local model: \(Ollama.model)")
            Button(action: model.toggleVoice) {
                Image(systemName: "mic")
                    .font(.system(size: 14, weight: .medium))
                    .foregroundStyle(voiceInput ? .secondary : .tertiary)
                    .frame(width: 26, height: 26)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .help(voiceInput ? "Speak a request (sent to AssemblyAI, cloud)" : "Voice input is off. Turn it on in Settings.")
            Button(action: model.send) {
                Image(systemName: "arrow.up")
                    .font(.system(size: 13, weight: .bold))
                    .foregroundStyle(canSend ? Color.white : Color.secondary)
                    .frame(width: 28, height: 28)
                    .background(Circle().fill(canSend ? Color.accentColor : Color.primary.opacity(0.08)))
            }
            .buttonStyle(.plain)
            .disabled(!canSend)
            .help("Send (↩)")
        }
        .padding(.leading, 16).padding(.trailing, 10)
        .frame(width: composerWidth, height: 48)
        .background(GlassBackground(shape: RoundedRectangle(cornerRadius: Self.radius, style: .continuous)))
    }

    private var canSend: Bool { !model.text.trimmingCharacters(in: .whitespaces).isEmpty }
    private var modelName: String { Ollama.model == "qwen3-vl:8b" ? "Qwen3-VL 8B" : Ollama.model }

    // MARK: Working (single slim row)

    private var workingRow: some View {
        HStack(spacing: 10) {
            ProgressView().controlSize(.small)
            Text(model.status)
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.primary)
                .lineLimit(1).truncationMode(.tail)
                .contentTransition(.opacity)
                .animation(.easeOut(duration: 0.2), value: model.status)
            Spacer(minLength: 6)
            cancelButton(help: "Stop (Esc)")
        }
        .padding(.leading, 14).padding(.trailing, 8)
        .frame(width: 380, height: 38)
        .background(GlassBackground(shape: RoundedRectangle(cornerRadius: 12, style: .continuous)))
    }

    /// Recording: a red dot (pulsing unless Reduce Motion is on), Done to transcribe, Esc to discard.
    private var listeningRow: some View {
        HStack(spacing: 10) {
            Circle().fill(Color.red).frame(width: 8, height: 8)
                .opacity(pulse && !reduceMotion ? 0.3 : 1)
                .animation(reduceMotion ? nil : .easeInOut(duration: 0.7).repeatForever(autoreverses: true), value: pulse)
                .onAppear { pulse = true }
                .onDisappear { pulse = false }
            Text("Listening…")
                .font(.system(size: 13, weight: .medium))
                .foregroundStyle(.primary)
            Spacer(minLength: 6)
            Button("Done") { model.finishListening() }
                .buttonStyle(QuietButtonStyle(prominent: true))
                .keyboardShortcut(.defaultAction)
                .help("Stop recording and transcribe (↩)")
            cancelButton(help: "Discard the recording (Esc)")
        }
        .padding(.leading, 14).padding(.trailing, 8)
        .frame(width: 380, height: 38)
        .background(GlassBackground(shape: RoundedRectangle(cornerRadius: 12, style: .continuous)))
    }

    private func cancelButton(help: String) -> some View {
        Button(action: model.cancel) {
            Image(systemName: "xmark")
                .font(.system(size: 10, weight: .bold))
                .foregroundStyle(.secondary)
                .frame(width: 22, height: 22)
                .background(Circle().fill(Color.primary.opacity(0.08)))
        }
        .buttonStyle(.plain)
        .keyboardShortcut(.cancelAction)
        .help(help)
    }

    // MARK: Cards (result, approval, guiding, choice)

    private var cardContent: AnyView? {
        switch model.phase {
        case .working, .transcribing, .listening:
            return nil // never a second card next to the thinking row
        case .choosing:
            return AnyView(card(text: "Do you want me to do it, or show you where to click?", heard: model.heard) {
                Button("Show me") { model.choose(.guide) }.buttonStyle(QuietButtonStyle())
                Button("Do it for me") { model.choose(.act) }.buttonStyle(QuietButtonStyle(prominent: true)).keyboardShortcut(.defaultAction)
                Button("Cancel") { model.cancel() }.buttonStyle(QuietButtonStyle()).keyboardShortcut(.cancelAction)
            })
        case .approval:
            return AnyView(card(text: model.answer) {
                Button("Allow") { model.send() }.buttonStyle(QuietButtonStyle(prominent: true)).keyboardShortcut(.defaultAction)
                Button("Stop") { model.cancel() }.buttonStyle(QuietButtonStyle()).keyboardShortcut(.cancelAction)
            })
        case .composing:
            guard !model.answer.isEmpty, !model.teachMode else { return nil }
            if model.guide.active {
                return AnyView(card(text: model.answer, step: model.guide.step) {
                    Button("Check again") { model.guide.checkAgain() }.buttonStyle(QuietButtonStyle())
                    Button("Stop") { model.cancel() }.buttonStyle(QuietButtonStyle())
                })
            }
            return AnyView(card(text: model.answer, heard: model.heard) {
                if model.speaking {
                    Button("Stop speaking") { model.stopSpeaking() }.buttonStyle(QuietButtonStyle()).help("Voice by ElevenLabs (cloud)")
                }
                Button("Done") { model.dismissResult() }.buttonStyle(QuietButtonStyle())
            })
        }
    }

    private func card(text: String, step: Int = 0, heard: String? = nil, @ViewBuilder actions: () -> some View) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            if let heard {
                Label("“\(heard)”", systemImage: "waveform")
                    .font(.system(size: 12))
                    .foregroundStyle(.secondary)
                    .lineLimit(2)
            }
            if step > 0 {
                Text("STEP \(step)").font(.system(size: 10.5, weight: .bold)).tracking(0.6).foregroundStyle(.secondary)
            }
            // Hug the text; only long results scroll (a max-height frame would always stretch to its maximum).
            if text.count > 420 {
                ScrollView { cardText(text) }.frame(height: 200)
            } else {
                cardText(text).fixedSize(horizontal: false, vertical: true)
            }
            HStack(spacing: 6) { actions() }
        }
        .padding(14)
        .frame(width: composerWidth, alignment: .leading) // same width as the composer, so they read as one unit
        .background(GlassBackground(shape: RoundedRectangle(cornerRadius: Self.radius, style: .continuous)))
    }

    private func cardText(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 13.5))
            .foregroundStyle(.primary)
            .lineSpacing(2.5)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}
