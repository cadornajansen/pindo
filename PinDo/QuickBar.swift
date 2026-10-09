import AppKit
import SwiftUI

/// Owns the quick-bar panel. The panel is built once at launch and only shown/hidden
/// afterwards, so it appears instantly.
final class QuickBar {
    private let model = QuickBarModel()
    private lazy var panel = QuickBarPanel(rootView: QuickBarView(model: model, onClose: { [weak self] in self?.hide() }),
                                           onResignKey: { [weak self] in self?.hide() })
    private var isShown = false

    init() { _ = panel }

    func toggle() { isShown ? hide() : show() }

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
    var focusTick = 0
    private var task: Task<Void, Never>?

    func send() {
        let prompt = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !prompt.isEmpty, !isBusy else { return }
        text = ""
        answer = ""
        isBusy = true
        task = Task {
            do {
                try await Ollama.chat(prompt) { self.answer += $0 }
            } catch is CancellationError {
            } catch {
                if !Task.isCancelled {
                    answer = "Couldn't reach the local model. Is Ollama running?\n(\(error.localizedDescription))"
                }
            }
            isBusy = false
        }
    }

    func cancel() {
        task?.cancel()
        task = nil
        isBusy = false
    }
}

struct QuickBarView: View {
    @Bindable var model: QuickBarModel
    let onClose: () -> Void
    @FocusState private var focused: Bool

    private let accent = Color(red: 0.33, green: 0.56, blue: 1.0)
    private var canSend: Bool { !model.text.trimmingCharacters(in: .whitespaces).isEmpty && !model.isBusy }

    var body: some View {
        VStack(spacing: 10) {
            Spacer(minLength: 0)
            if !model.answer.isEmpty {
                ViewThatFits(in: .vertical) {
                    answerText
                    ScrollView { answerText }
                }
                .frame(maxHeight: 320)
                .background(card)
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
            bar
        }
        .padding(20) // room for the shadow inside the transparent panel
        .animation(.spring(response: 0.3, dampingFraction: 0.85), value: model.answer.isEmpty)
        .onChange(of: model.focusTick) { focused = true }
    }

    private var bar: some View {
        HStack(spacing: 14) {
            Image(systemName: "hand.point.up.left.fill")
                .font(.system(size: 22, weight: .semibold))
                .foregroundStyle(accent)
                .symbolEffect(.pulse, isActive: model.isBusy)
            TextField("What can I help you with?", text: $model.text)
                .textFieldStyle(.plain)
                .font(.system(size: 18))
                .focused($focused)
                .onSubmit(model.send)
                .onExitCommand(perform: onClose)
            Button(action: model.send) {
                Image(systemName: "arrow.up")
                    .font(.system(size: 16, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 36, height: 36)
                    .background(canSend ? accent : Color.secondary.opacity(0.35),
                                in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            }
            .buttonStyle(.plain)
            .disabled(!canSend)
            .animation(.easeOut(duration: 0.15), value: canSend)
        }
        .padding(.horizontal, 18)
        .frame(height: 62)
        .background(card)
    }

    private var answerText: some View {
        Text(model.answer)
            .font(.system(size: 15))
            .lineSpacing(3)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(18)
    }

    private var card: some View {
        RoundedRectangle(cornerRadius: 18, style: .continuous)
            .fill(.regularMaterial)
            .overlay(RoundedRectangle(cornerRadius: 18, style: .continuous).strokeBorder(.white.opacity(0.1)))
            .shadow(color: .black.opacity(0.35), radius: 18, y: 8)
    }
}

/// Local model over Ollama's HTTP API (127.0.0.1, so no ATS exception needed).
enum Ollama {
    // ponytail: model name via `defaults write com.pindopro.PinDo model <name>`; real settings UI in M7.
    static var model: String { UserDefaults.standard.string(forKey: "model") ?? "maternion/mai-ui:2b" }
    private static let url = URL(string: "http://127.0.0.1:11434/api/chat")!
    private static let system = "You are PinDo, a friendly Mac assistant. Answer in 1–3 short sentences unless asked for more."

    /// Loads the model and keeps it resident (keep_alive -1), so the first real question isn't a cold start.
    static func warm() async throws {
        _ = try await URLSession.shared.data(for: request(["model": model, "messages": [], "keep_alive": -1]))
    }

    static func chat(_ prompt: String, onToken: (String) -> Void) async throws {
        let body: [String: Any] = [
            "model": model, "stream": true, "keep_alive": -1,
            "messages": [["role": "system", "content": system], ["role": "user", "content": prompt]],
        ]
        let (bytes, response) = try await URLSession.shared.bytes(for: request(body))
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw URLError(.badServerResponse) }
        for try await line in bytes.lines {
            try Task.checkCancellation()
            if let chunk = try? JSONDecoder().decode(Chunk.self, from: Data(line.utf8)), let text = chunk.message?.content {
                onToken(text)
            }
        }
    }

    private struct Chunk: Decodable {
        struct Message: Decodable { let content: String }
        let message: Message?
    }

    private static func request(_ body: [String: Any]) throws -> URLRequest {
        var req = URLRequest(url: url)
        req.httpMethod = "POST"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        req.httpBody = try JSONSerialization.data(withJSONObject: body)
        return req
    }
}
