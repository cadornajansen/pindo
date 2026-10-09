import AppKit
import SwiftUI

/// Owns the quick-bar panel. The panel is built once at launch and only shown/hidden
/// afterwards, so it appears instantly.
final class QuickBar {
    private let model = QuickBarModel()
    private lazy var panel = QuickBarPanel(rootView: QuickBarView(model: model, onClose: { [weak self] in self?.hide() }),
                                           onResignKey: { [weak self] in if self?.model.isBusy == false && self?.model.tutor.active == false && self?.model.guide.active == false { self?.hide() } })
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

    #if DEBUG
    /// Debug: renders the panel's views (layout and text; system glass needs the window server) to a PNG.
    func snapshot(to url: URL) {
        guard let view = panel.contentView, let rep = view.bitmapImageRepForCachingDisplay(in: view.bounds) else { return }
        view.cacheDisplay(in: view.bounds, to: rep)
        try? rep.representation(using: .png, properties: [:])?.write(to: url)
    }
    #endif
    func cancel() { model.cancel() }

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
    var teachMode: Bool { mode == .teach && tutor.active }
    let tutor = TutorSession()
    let guide = GuideSession()
    private var task: Task<Void, Never>?
    private var approval: CheckedContinuation<Bool, Never>?

    /// What the panel shows, derived from the real backend state (never simulated).
    enum Phase: Equatable { case composing, working, approval, choosing }
    var phase: Phase {
        if awaitingApproval { return .approval }
        if isBusy || guide.working { return .working }
        if pendingChoice != nil { return .choosing }
        return .composing
    }

    /// Latest real progress for the slim working row.
    var status: String {
        if guide.working { return "Looking at the screen…" }
        if let step = answer.split(separator: "\n").last(where: { $0.hasPrefix("▸") }) { return String(step.dropFirst(2)) }
        return "Thinking…"
    }

    func send() {
        if awaitingApproval { return resolveApproval(true) } // ↩ = allow
        let prompt = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !prompt.isEmpty, phase == .composing else { return } // no duplicate submissions
        text = ""
        switch IntentPolicy.decide(prompt) {
        case .guide: run(prompt, as: .guide)
        case .act: run(prompt, as: .act)
        case .teach: run(prompt, as: .teach)
        case .clarify:
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
        switch mode {
        case .teach:
            tutor.start(prompt)
        case .guide:
            guide.start(prompt) { [weak self] in self?.answer = $0 } // the card shows the current step only
        case .act:
            isBusy = true
            task = Task {
                do {
                    try await Agent.run(task: prompt, report: { self.say($0) }, confirm: { await self.askApproval($0) })
                } catch is CancellationError {
                } catch {
                    if !Task.isCancelled {
                        say(error is Ollama.ModelError ? error.localizedDescription
                            : "Couldn't reach the local model. Is Ollama running?\n(\(error.localizedDescription))")
                    }
                }
                isBusy = false
            }
        }
    }

    func cancel() {
        tutor.stop()
        guide.stop()
        task?.cancel()
        task = nil
        resolveApproval(false)
        pendingChoice = nil
        isBusy = false
    }

    func dismissResult() { answer = "" }

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
                Color.clear.glassEffect(.regular, in: shape)
            } else {
                shape.fill(.regularMaterial)
            }
        }
        .overlay(shape.strokeBorder(LinearGradient(colors: [.white.opacity(0.35), .white.opacity(0.06)],
                                                   startPoint: .top, endPoint: .bottom), lineWidth: 0.75))
        .shadow(color: .black.opacity(0.14), radius: 12, y: 5)
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
                if model.phase == .working {
                    workingRow.transition(workingTransition)
                } else if model.phase == .composing {
                    composer.transition(composerTransition)
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
            Image(systemName: "mic") // ponytail: voice input (plan M2) isn't built; shown inactive
                .font(.system(size: 14, weight: .medium))
                .foregroundStyle(.tertiary)
                .frame(width: 26, height: 26)
                .help("Voice is coming soon")
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
            Button(action: model.cancel) {
                Image(systemName: "xmark")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(.secondary)
                    .frame(width: 22, height: 22)
                    .background(Circle().fill(Color.primary.opacity(0.08)))
            }
            .buttonStyle(.plain)
            .keyboardShortcut(.cancelAction)
            .help("Stop (Esc)")
        }
        .padding(.leading, 14).padding(.trailing, 8)
        .frame(width: 380, height: 38)
        .background(GlassBackground(shape: RoundedRectangle(cornerRadius: 12, style: .continuous)))
    }

    // MARK: Cards (result, approval, guiding, choice)

    private var cardContent: AnyView? {
        switch model.phase {
        case .working:
            return nil // never a second card next to the thinking row
        case .choosing:
            return AnyView(card(text: "Do you want me to do it, or show you where to click?") {
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
            return AnyView(card(text: model.answer) {
                Button("Done") { model.dismissResult() }.buttonStyle(QuietButtonStyle())
            })
        }
    }

    private func card(text: String, step: Int = 0, @ViewBuilder actions: () -> some View) -> some View {
        VStack(alignment: .leading, spacing: 10) {
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
