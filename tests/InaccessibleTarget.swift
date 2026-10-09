// Test D target: a window whose buttons are drawn pixels, invisible to Accessibility, so Guide mode must use vision.
//   swift tests/InaccessibleTarget.swift
// Prints the "Export" button's true global rect (top-left origin, points) for checking where Pindo points.
import AppKit

final class DrawnButtons: NSView {
    static let export = CGRect(x: 640, y: 70, width: 150, height: 44) // view coordinates (flipped)
    static let decoys: [(CGRect, String)] = [(CGRect(x: 40, y: 70, width: 120, height: 40), "Share"),
                                              (CGRect(x: 180, y: 70, width: 120, height: 40), "Print"),
                                              (CGRect(x: 40, y: 140, width: 120, height: 40), "Help")]
    override var isFlipped: Bool { true }
    override func isAccessibilityElement() -> Bool { false }
    override func accessibilityChildren() -> [Any]? { [] }

    override func draw(_ dirtyRect: NSRect) {
        NSColor(white: 0.97, alpha: 1).setFill(); bounds.fill()
        for (r, t) in Self.decoys + [(Self.export, "Export")] {
            NSColor.systemIndigo.setFill(); NSBezierPath(roundedRect: r, xRadius: 8, yRadius: 8).fill()
            let s = NSAttributedString(string: t, attributes: [.font: NSFont.boldSystemFont(ofSize: 17), .foregroundColor: NSColor.white])
            s.draw(at: CGPoint(x: r.midX - s.size().width / 2, y: r.midY - s.size().height / 2))
        }
    }
}

let app = NSApplication.shared
app.setActivationPolicy(.regular)
let window = NSWindow(contentRect: NSRect(x: 220, y: 260, width: 860, height: 520), styleMask: [.titled], backing: .buffered, defer: false)
window.title = "Pindo Grounding Target"
window.contentView = DrawnButtons()
window.makeKeyAndOrderFront(nil)
app.activate(ignoringOtherApps: true)
DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
    // View rect → screen (AppKit, y up) → global (top-left origin).
    let view = window.contentView!
    let inWindow = view.convert(DrawnButtons.export, to: nil)
    let onScreen = window.convertToScreen(inWindow)
    let primaryHeight = NSScreen.screens[0].frame.maxY
    let global = CGRect(x: onScreen.minX, y: primaryHeight - onScreen.maxY, width: onScreen.width, height: onScreen.height)
    print("EXPORT_GLOBAL_RECT \(global.minX) \(global.minY) \(global.width) \(global.height)")
    fflush(stdout)
}
app.run()
