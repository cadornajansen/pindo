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
    func cancel() { model.cancel() }

    func toggle() { isShown ? hide() : show() }

    func submit(_ text: String, mode: QuickBarModel.Mode? = nil) {
        show()
        if let mode { model.mode = mode }
        model.text = text
        model.send()
    }

    func show() {
        guard !isShown else { return }
        isShown = true
        let origin = Self.anchorOrigin(panelSize: panel.frame.size)
        panel.alphaValue = 0
        panel.setFrameOrigin(NSPoint(x: origin.x, y: origin.y - 10))
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
        NSAnimationContext.runAnimationGroup({ ctx in
            ctx.duration = 0.12
            panel.animator().alphaValue = 0
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
        contentView = NSHostingView(rootView: rootView)
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
    enum Mode: String, CaseIterable { case guide = "Guide", act = "Do", teach = "Teach" }
    var mode: Mode = .guide // Guide: Pindo points, you click. Do: Pindo acts (with confirmations). Teach: lessons.
    var teachMode: Bool { mode == .teach }
    let tutor = TutorSession()
    let guide = GuideSession()
    private var task: Task<Void, Never>?
    private var approval: CheckedContinuation<Bool, Never>?

    func send() {
        if awaitingApproval { return resolveApproval(true) } // ↩ = allow
        let prompt = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !prompt.isEmpty, !isBusy else { return }
        text = ""
        answer = ""
        if teachMode { guide.stop(); tutor.start(prompt); return }
        tutor.stop()
        if mode == .guide { guide.start(prompt) { [weak self] in self?.say($0) }; return }
        guide.stop()
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

    func cancel() {
        tutor.stop()
        guide.stop()
        task?.cancel()
        task = nil
        resolveApproval(false)
        isBusy = false
    }

    private func say(_ line: String) {
        answer = answer.isEmpty ? line : answer + "\n" + line
    }

    private func askApproval(_ question: String) async -> Bool {
        say("⚠︎ \(question) Press ↩ to allow, or Esc to stop.")
        awaitingApproval = true
        return await withCheckedContinuation { approval = $0 }
    }

    private func resolveApproval(_ allowed: Bool) {
        awaitingApproval = false
        approval?.resume(returning: allowed)
        approval = nil
    }
}

enum Figma {
    static let fill = Color(red: 217 / 255, green: 217 / 255, blue: 217 / 255).opacity(0.2)
    static let thinkingBorder = Color(red: 0, green: 140 / 255, blue: 1)        // #008CFF
    static let stepNumber = Color(red: 243 / 255, green: 1, blue: 70 / 255)     // #F3FF46
    static let barSize = CGSize(width: 557, height: 145)                       // 242 × 63
    static let radius: CGFloat = 32                                            // 14
    static let pointer = Color(red: 227 / 255, green: 1, blue: 69 / 255)       // #E3FF45 (Polygon 1)
}

/// Glassmorphism per the Figma frame: 20% #D9D9D9 over a blur, a light edge, and a soft shadow.
/// macOS 26 renders the blur with Liquid Glass; older systems use a material.
struct GlassBackground<S: InsettableShape>: View {
    let shape: S
    var glow: Color? = nil // Thinking state: 1 px #008CFF edge, glowing

    var body: some View {
        ZStack {
            if #available(macOS 26.0, *) {
                Color.clear.glassEffect(.regular, in: shape)
            } else {
                shape.fill(.ultraThinMaterial)
            }
            shape.fill(Figma.fill)
            shape.strokeBorder(LinearGradient(colors: [.white.opacity(0.6), .white.opacity(0.08)],
                                              startPoint: .topLeading, endPoint: .bottomTrailing), lineWidth: 1)
            if let glow { shape.strokeBorder(glow, lineWidth: 2) }
        }
        .shadow(color: glow?.opacity(0.6) ?? .black.opacity(0.2), radius: glow == nil ? 18 : 14, y: glow == nil ? 8 : 0)
    }
}

struct QuickBarView: View {
    @Bindable var model: QuickBarModel
    let onClose: () -> Void
    @FocusState private var focused: Bool

    private let accent = Color(red: 0.33, green: 0.56, blue: 1.0)
    private var canSend: Bool { model.awaitingApproval || (!model.text.trimmingCharacters(in: .whitespaces).isEmpty && !model.isBusy) }

    var body: some View {
        VStack(spacing: 10) {
            Spacer(minLength: 0)
            if model.teachMode {
                ScrollView { TutorView(session: model.tutor) }
                    .frame(maxHeight: 300)
                    .background(card)
            }
            if !model.answer.isEmpty {
                VStack(alignment: .leading, spacing: 0) {
                    ViewThatFits(in: .vertical) {
                        answerText
                        ScrollView { answerText }
                    }
                    .frame(maxHeight: 320)
                    if model.guide.active && !model.guide.working {
                        Button("Check again") { model.guide.checkAgain() }
                            .buttonStyle(.plain)
                            .font(.system(size: 13, weight: .semibold)).foregroundStyle(.white)
                            .padding(.horizontal, 14).padding(.vertical, 6)
                            .background(GlassBackground(shape: Capsule()))
                            .padding([.horizontal, .bottom], 16)
                    }
                }
                .background(card)
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
            bar
        }
        .padding(.bottom, 72).padding(20) // room for the step pill and shadow inside the transparent panel
        .animation(.spring(response: 0.3, dampingFraction: 0.85), value: model.answer.isEmpty)
        .onChange(of: model.focusTick) { focused = true }
    }

    // Figma PINDO › Frame 1 (Group 12 idle bar, Group 11 "Thinking...", Frame 2 step pill).
    // The mockup is drawn at ~0.43×; everything here is the Figma value × 2.3.

    private var thinking: Bool { (model.isBusy && !model.awaitingApproval) || model.guide.working }

    private var bar: some View {
        VStack(alignment: .leading, spacing: 0) {
            Group {
                if thinking {
                    HStack(spacing: 12) {                                          // gap 5
                        Image("FigmaSpinner").resizable().frame(width: 28, height: 28)
                        title(Text("Thinking..."))
                    }
                } else {
                    TextField("", text: $model.text, prompt: title(Text("What can I help you with?")))
                        .textFieldStyle(.plain)
                        .font(.system(size: 23, weight: .semibold, design: .rounded))
                        .tracking(-0.92)
                        .foregroundStyle(.white)
                        .focused($focused)
                        .onSubmit(model.send)
                        .onExitCommand(perform: onClose)
                }
            }
            .frame(height: 30)
            Spacer(minLength: 0)
            HStack(alignment: .bottom, spacing: 16) {                              // gap 7
                HStack(spacing: 7) {                                               // gap 3
                    Image("FigmaSliders").resizable().frame(width: 21, height: 21)  // 9
                    Text(modelName).font(.system(size: 14, weight: .semibold)).tracking(-0.83)
                        .foregroundStyle(.white).opacity(0.9)
                }
                .padding(.bottom, 9)
                // Not in the design: Teach mode (PR #1) needs a switch; kept small next to the model chip.
                // Not in the design: Guide (default), Do and Teach need a switch; kept small next to the model chip.
                Picker("", selection: $model.mode) {
                    ForEach(QuickBarModel.Mode.allCases, id: \.self) { Text($0.rawValue) }
                }
                .pickerStyle(.segmented).labelsHidden().controlSize(.mini).fixedSize()
                .disabled(model.isBusy || model.guide.active)
                .onChange(of: model.mode) { model.tutor.stop(); model.guide.stop() }
                .padding(.bottom, 6)
                Spacer()
                Image("FigmaMic").resizable().frame(width: 16, height: 23)          // 7 × 10
                    .opacity(0.5) // ponytail: voice (plan M2) isn't built; shown per design, inactive
                    .help("Voice is coming soon")
                    .padding(.bottom, 9)
                primaryButton
            }
        }
        .padding(EdgeInsets(top: 41, leading: 28, bottom: 12, trailing: 21))
        .frame(width: Figma.barSize.width, height: Figma.barSize.height)
        .background(glass)
        .overlay(alignment: .bottom) { stepPill.offset(y: 41 + 30) }              // pill 18 below the bar
    }

    private func title(_ text: Text) -> Text {
        text.font(.system(size: 23, weight: .semibold, design: .rounded)).tracking(-0.92)  // SF Pro Rounded 10
            .foregroundColor(.white.opacity(0.9))
    }

    private var modelName: String { Ollama.model == "qwen3-vl:8b" ? "Qwen 3-VL 8B" : Ollama.model }

    /// "Step N" pill under the bar while PinDo works (one per action). PinDo doesn't plan the total
    /// up front, so the design's "of 3" is left out.
    @ViewBuilder private var stepPill: some View {
        let steps = model.guide.active ? model.guide.step : model.answer.split(separator: "\n").filter { $0.hasPrefix("▸") }.count
        if thinking || model.guide.active, steps > 0 {
            HStack(spacing: 12) {
                Image("FigmaClipboard").resizable().frame(width: 16, height: 16)    // 7
                (Text("Step ") + Text("\(steps)").foregroundColor(Figma.stepNumber))
                    .font(.system(size: 14, weight: .semibold)).tracking(-0.83).foregroundStyle(.white)
            }
            .padding(EdgeInsets(top: 7, leading: 44, bottom: 7, trailing: 46))     // py 3, pl 19, pr 20
            .background(GlassBackground(shape: Capsule()))
            .transition(.opacity)
        }
    }

    private var glass: some View {
        GlassBackground(shape: RoundedRectangle(cornerRadius: Figma.radius, style: .continuous),
                        glow: thinking ? Figma.thinkingBorder : nil)
            .animation(.easeInOut(duration: 0.3), value: thinking)
    }

    /// White 18 px circle with the four-bar waveform (sends); Stop ■ while working, ✓ to approve.
    private var primaryButton: some View {
        let active = canSend || model.isBusy || model.guide.active
        return Button {
            if thinking || model.guide.active { model.cancel() } else { model.send() }
        } label: {
            Group {
                if model.awaitingApproval {
                    Image(systemName: "checkmark").font(.system(size: 17, weight: .bold))
                } else if thinking || model.guide.active {
                    Image(systemName: "stop.fill").font(.system(size: 14, weight: .bold))
                } else {
                    HStack(alignment: .center, spacing: 2.1) {                     // bars 1.32 wide, 2.23 apart
                        let heights: [CGFloat] = [11.5, 18.4, 25.3, 11.5]                // 5, 8, 11, 5
                        ForEach(heights.indices, id: \.self) { Capsule().frame(width: 3, height: heights[$0]) }
                    }
                }
            }
            .foregroundStyle(.black)
            .frame(width: 41, height: 41)                                          // 18
            .background(Circle().fill(.white.opacity(active ? 1 : 0.6)))
        }
        .buttonStyle(.plain)
        .disabled(!active)
        .help(thinking ? "Stop" : model.awaitingApproval ? "Allow" : "Send")
    }

    private var answerText: some View {
        Text(model.answer)
            .font(.system(size: 15, weight: .medium))
            .foregroundStyle(.white)
            .shadow(color: .black.opacity(0.2), radius: 1.4, y: 1)
            .lineSpacing(3)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(18)
    }

    private var card: some View {
        GlassBackground(shape: RoundedRectangle(cornerRadius: 24, style: .continuous))
    }
}
