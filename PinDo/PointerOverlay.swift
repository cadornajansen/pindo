import AppKit
import SwiftUI

/// Figma Polygon 1: rounded triangle, apex up. Path taken from the design's SVG (content box x 3.97...17.07, y 0...13.54).
struct PointerShape: Shape {
    func path(in rect: CGRect) -> Path {
        let sx = rect.width / 13.10, sy = rect.height / 13.54
        func p(_ x: CGFloat, _ y: CGFloat) -> CGPoint { CGPoint(x: rect.minX + (x - 3.97) * sx, y: rect.minY + y * sy) }
        var path = Path()
        path.move(to: p(8.71515, 1.14386))
        path.addCurve(to: p(12.3301, 1.14386), control1: p(9.43755, -0.381287), control2: p(11.6077, -0.381285))
        path.addLine(to: p(16.8503, 10.687))
        path.addCurve(to: p(15.0428, 13.5431), control1: p(17.4788, 12.0138), control2: p(16.5111, 13.5431))
        path.addLine(to: p(6.00243, 13.5431))
        path.addCurve(to: p(4.19494, 10.687), control1: p(4.53422, 13.5431), control2: p(3.56644, 12.0138))
        path.closeSubpath()
        return path
    }
}

@Observable
final class PointerState {
    var visible = false
    var tip = CGPoint.zero          // window-local, top-left origin
    var rect: CGRect?               // window-local AX bounds
    var label = ""
    var trail: [CGPoint] = []       // Figma "TRAIL EFFECT": shrinking copies along the path just travelled
    var flip = false                // label on the left when the target is near the right edge
}

/// Click-through overlay that points at validated targets. It never takes focus, is excluded from screen
/// capture (no feedback into the screenshots it helps interpret) and only draws on the target's display.
@MainActor
final class PointerOverlay {
    private var window: NSWindow?
    private let state = PointerState()
    private var hideWork: DispatchWorkItem?

    /// `target` is in global coordinates (top-left origin); it's converted here, once, for AppKit.
    func show(_ target: GroundedTarget) {
        hideWork?.cancel()
        let primaryHeight = NSScreen.screens.first?.frame.maxY ?? 0
        let point = CoordinateTransform.appKit(target.point, primaryHeight: primaryHeight)
        guard let screen = NSScreen.screens.first(where: { $0.frame.contains(point) }) else { return } // never on a wrong display
        let window = self.window ?? makeWindow()
        if window.frame != screen.frame { window.setFrame(screen.frame, display: false) }
        // AppKit (y up) → window-local SwiftUI (y down).
        func local(_ p: CGPoint) -> CGPoint { CGPoint(x: p.x - screen.frame.minX, y: screen.frame.maxY - p.y) }
        let tip = local(point)
        let rect = target.rect.map { r -> CGRect in
            let a = CoordinateTransform.appKit(r, primaryHeight: primaryHeight)
            return CGRect(origin: local(CGPoint(x: a.minX, y: a.maxY)), size: a.size)
        }
        let reduceMotion = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
        let from = state.visible ? state.tip : CGPoint(x: tip.x + 120, y: tip.y + 90)
        let apply = {
            self.state.tip = tip
            self.state.rect = rect
            self.state.label = target.instruction
            self.state.flip = tip.x > screen.frame.width - 320
            self.state.visible = true
        }
        if reduceMotion {
            state.trail = []
            apply()
        } else {
            state.trail = (1...5).map { i in
                let t = CGFloat(i) * 0.12
                return CGPoint(x: tip.x + (from.x - tip.x) * t, y: tip.y + (from.y - tip.y) * t)
            }
            withAnimation(.spring(response: 0.45, dampingFraction: 0.78)) { apply() }
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.6) { withAnimation(.easeOut(duration: 0.35)) { self.state.trail = [] } }
        }
        window.orderFrontRegardless()
    }

    func hide() {
        guard state.visible else { return }
        withAnimation(.easeOut(duration: 0.15)) { state.visible = false; state.trail = [] }
        let work = DispatchWorkItem { [weak self] in self?.window?.orderOut(nil) }
        hideWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2, execute: work)
    }

    private func makeWindow() -> NSWindow {
        let w = NSWindow(contentRect: .zero, styleMask: .borderless, backing: .buffered, defer: false)
        w.isOpaque = false
        w.backgroundColor = .clear
        w.hasShadow = false
        w.ignoresMouseEvents = true               // click-through
        w.level = .screenSaver                    // above menus, so menu bar targets stay visible
        w.collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
        w.sharingType = .none                     // not in screenshots
        w.contentView = NSHostingView(rootView: PointerView(state: state))
        window = w
        return w
    }
}

private struct PointerView: View {
    let state: PointerState

    // Figma sizes × ~1.3 for the desktop: Polygon 1 is 27.5 × 28 in the mockup.
    private let pointerSize = CGSize(width: 34, height: 35)
    private let trailScales: [CGFloat] = [0.83, 0.71, 0.49, 0.31, 0.2] // Polygons 5-8

    var body: some View {
        ZStack(alignment: .topLeading) {
            if state.visible {
                highlight
                ForEach(Array(state.trail.enumerated()), id: \.offset) { i, p in
                    pointer.scaleEffect(trailScales[min(i, trailScales.count - 1)]).opacity(0.9 - Double(i) * 0.15)
                        .position(center(forTip: p))
                }
                pointer
                    .overlay(alignment: state.flip ? .trailing : .leading) {
                        label.fixedSize().offset(x: state.flip ? -(pointerSize.width + 6) : pointerSize.width + 6, y: 14)
                    }
                    .position(center(forTip: state.tip))
                    .transition(.opacity)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    /// Dashed box around AX bounds (Rectangle 8), dashed circle for vision points (Ellipse 3).
    @ViewBuilder private var highlight: some View {
        let dash = StrokeStyle(lineWidth: 2, dash: [5, 4])
        if let r = state.rect {
            RoundedRectangle(cornerRadius: 6).stroke(Figma.pointer, style: dash)
                .frame(width: r.width + 10, height: r.height + 10)
                .position(x: r.midX, y: r.midY)
                .shadow(color: Figma.pointer.opacity(0.6), radius: 6)
        } else {
            Circle().stroke(Figma.pointer, style: dash).frame(width: 53, height: 53)
                .position(state.tip)
                .shadow(color: Figma.pointer.opacity(0.6), radius: 6)
        }
    }

    /// Apex rotated to point up-left like a cursor (up-right when flipped), so the tip sits on the target.
    private var pointer: some View {
        PointerShape().fill(Figma.pointer)
            .frame(width: pointerSize.width, height: pointerSize.height)
            .rotationEffect(.degrees(state.flip ? 45 : -45))
            .shadow(color: .black.opacity(0.25), radius: 2, y: 4) // drop shadow from the SVG filter
    }

    /// The apex is half the height above the shape's center; rotated ±45° it sits 0.354 h up and 0.354 h to the side.
    private func center(forTip p: CGPoint) -> CGPoint {
        let d = pointerSize.height * 0.354
        return CGPoint(x: p.x + (state.flip ? -d : d), y: p.y + d)
    }

    /// "Click on this" (SF Pro Semibold, white, soft text shadow) on glass so it reads over light apps too.
    private var label: some View {
        Text(state.label)
            .font(.system(size: 14, weight: .semibold)).tracking(-0.83)
            .foregroundStyle(.primary) // adaptive on glass: readable over light and dark apps
            .padding(.horizontal, 12).padding(.vertical, 7)
            .background(GlassBackground(shape: Capsule()))
    }
}
