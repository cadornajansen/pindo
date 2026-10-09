// Coordinate mapping and grounding-output validation tests.
//   swiftc -swift-version 6 PinDo/GroundingGeometry.swift tests/GroundingTests.swift -o build/grounding-tests && build/grounding-tests
import CoreGraphics
import Foundation

@main
enum GroundingTests {
    static func main() {
        var failures = 0
        func check(_ ok: Bool, _ name: String, _ detail: @autoclosure () -> String = "") {
            if !ok { failures += 1; print("FAIL \(name) \(detail())") }
        }
        func near(_ a: CGPoint?, _ b: CGPoint, _ tol: CGFloat = 0.51) -> Bool {
            guard let a else { return false }
            return abs(a.x - b.x) <= tol && abs(a.y - b.y) <= tol
        }
        typealias T = CoordinateTransform

        // Full-screen image: a 1512×982 pt display captured at 1280×831 px.
        do {
            let g = CaptureGeometry(sourceFrame: CGRect(x: 0, y: 0, width: 1512, height: 982), imageSize: CGSize(width: 1280, height: 831))
            check(near(T.globalPoint(normalized: CGPoint(x: 500, y: 500), geometry: g), CGPoint(x: 756, y: 491)), "full screen center")
            check(near(T.globalPoint(normalized: CGPoint(x: 0, y: 0), geometry: g), .zero), "full screen origin")
            check(near(T.globalPoint(normalized: CGPoint(x: 1000, y: 1000), geometry: g), CGPoint(x: 1512, y: 982)), "full screen far corner")
        }

        // Window-only image: an 800×600 pt window at (200, 100), image 1280×960 px. Must map into the window, never the screen.
        do {
            let g = CaptureGeometry(sourceFrame: CGRect(x: 200, y: 100, width: 800, height: 600), imageSize: CGSize(width: 1280, height: 960))
            check(near(T.globalPoint(normalized: CGPoint(x: 250, y: 500), geometry: g), CGPoint(x: 400, y: 400)), "window-only mapping")
            let pixel = T.imagePoint(normalized: CGPoint(x: 250, y: 500), imageSize: g.imageSize)
            check(near(pixel, CGPoint(x: 320, y: 480)), "normalized to image pixels")
        }

        // Tall aspect ratio, no upscaling.
        do {
            check(T.fittedSize(CGSize(width: 700, height: 1100), maxLongEdge: 1280) == CGSize(width: 700, height: 1100), "no upscale")
            let fit = T.fittedSize(CGSize(width: 1400, height: 2200), maxLongEdge: 1280)
            check(fit == CGSize(width: 815, height: 1280), "tall fit keeps aspect", "\(fit)")
            let g = CaptureGeometry(sourceFrame: CGRect(x: 10, y: 20, width: 700, height: 1100), imageSize: fit)
            check(near(T.globalPoint(normalized: CGPoint(x: 243, y: 841), geometry: g), CGPoint(x: 180.1, y: 945.1), 0.6), "tall mapping")
        }

        // 2× Retina: image pixels are twice the window's points; mapping uses ratios, so the scale must not leak in.
        do {
            let g = CaptureGeometry(sourceFrame: CGRect(x: 100, y: 50, width: 640, height: 400), imageSize: CGSize(width: 1280, height: 800), displayScale: 2)
            check(near(T.globalPoint(imagePoint: CGPoint(x: 640, y: 400), geometry: g), CGPoint(x: 420, y: 250)), "retina image pixel to points")
            let back = T.imageRect(globalRect: CGRect(x: 420, y: 250, width: 10, height: 10), geometry: g)
            check(back == CGRect(x: 640, y: 400, width: 20, height: 20), "retina global rect to image", "\(back)")
        }

        // External display left of the primary (negative x) and above it (negative y); AppKit conversion.
        do {
            let primaryHeight: CGFloat = 982
            let left = CaptureGeometry(sourceFrame: CGRect(x: -1920, y: -100, width: 1000, height: 800), imageSize: CGSize(width: 1000, height: 800))
            let pLeft = T.globalPoint(normalized: CGPoint(x: 100, y: 500), geometry: left)
            check(near(pLeft, CGPoint(x: -1820, y: 300)), "left display global", "\(String(describing: pLeft))")
            check(T.appKit(CGPoint(x: -1820, y: 300), primaryHeight: primaryHeight) == CGPoint(x: -1820, y: 682), "left display appkit")
            let above = CaptureGeometry(sourceFrame: CGRect(x: 0, y: -1080, width: 1920, height: 1080), imageSize: CGSize(width: 1280, height: 720))
            let pAbove = T.globalPoint(normalized: CGPoint(x: 500, y: 500), geometry: above)
            check(near(pAbove, CGPoint(x: 960, y: -540)), "above display global")
            check(T.appKit(CGPoint(x: 960, y: -540), primaryHeight: primaryHeight) == CGPoint(x: 960, y: 1522), "above display appkit (y > primary)")
            let r = T.appKit(CGRect(x: 10, y: 20, width: 100, height: 30), primaryHeight: primaryHeight)
            check(r == CGRect(x: 10, y: 932, width: 100, height: 30), "rect appkit flips by maxY", "\(r)")
        }

        // Cropped image: only a 200×100 pt region of a window was sent (coarse-to-fine).
        do {
            let crop = CGRect(x: 300 + 50, y: 200 + 40, width: 200, height: 100) // global frame of the crop
            let g = CaptureGeometry(sourceFrame: crop, imageSize: CGSize(width: 800, height: 400))
            check(near(T.globalPoint(normalized: CGPoint(x: 500, y: 500), geometry: g), CGPoint(x: 450, y: 290)), "crop maps inside the crop, not the window")
        }

        // Letterboxed image: 1280×1280 canvas, content 1280×800 starting at y = 240. Padding must be rejected.
        do {
            let g = CaptureGeometry(sourceFrame: CGRect(x: 0, y: 0, width: 1280, height: 800), imageSize: CGSize(width: 1280, height: 1280),
                                    contentRect: CGRect(x: 0, y: 240, width: 1280, height: 800))
            check(T.globalPoint(normalized: CGPoint(x: 500, y: 50), geometry: g) == nil, "point in top padding rejected")
            check(near(T.globalPoint(normalized: CGPoint(x: 500, y: 500), geometry: g), CGPoint(x: 640, y: 400)), "letterbox content center")
            // Pillarbox: padding on the sides instead (a tall window on a square canvas).
            let side = CaptureGeometry(sourceFrame: CGRect(x: 50, y: 60, width: 600, height: 960), imageSize: CGSize(width: 960, height: 960),
                                       contentRect: CGRect(x: 180, y: 0, width: 600, height: 960))
            check(T.globalPoint(normalized: CGPoint(x: 100, y: 500), geometry: side) == nil, "point in side padding rejected")
            check(near(T.globalPoint(normalized: CGPoint(x: 250, y: 500), geometry: side), CGPoint(x: 110, y: 540)), "pillarbox maps from content edge")
        }

        // Invalid points and stale windows.
        do {
            check(T.imagePoint(normalized: CGPoint(x: 1001, y: 10), imageSize: CGSize(width: 10, height: 10)) == nil, "x > 1000 rejected")
            check(T.imagePoint(normalized: CGPoint(x: -1, y: 10), imageSize: CGSize(width: 10, height: 10)) == nil, "negative rejected")
            check(T.imagePoint(normalized: CGPoint(x: CGFloat.nan, y: 10), imageSize: CGSize(width: 10, height: 10)) == nil, "NaN rejected")
            let captured = CGRect(x: 100, y: 100, width: 800, height: 600)
            check(!T.isStale(captured: captured, current: captured.offsetBy(dx: 1, dy: 0)), "1 pt jitter is not stale")
            check(T.isStale(captured: captured, current: captured.offsetBy(dx: 40, dy: 0)), "moved window is stale")
            check(T.isStale(captured: captured, current: CGRect(x: 100, y: 100, width: 900, height: 600)), "resized window is stale")
            check(T.isStale(captured: captured, current: nil), "closed window is stale")
        }

        // Round trip: an AX rect drawn on the screenshot and pointed at by the model lands back on the same spot.
        do {
            let g = CaptureGeometry(sourceFrame: CGRect(x: -500, y: 30, width: 1000, height: 700), imageSize: T.fittedSize(CGSize(width: 2000, height: 1400), maxLongEdge: 1280))
            let ax = CGRect(x: -100, y: 200, width: 60, height: 24)
            let img = T.imageRect(globalRect: ax, geometry: g)
            let normalized = CGPoint(x: img.midX / g.imageSize.width * 1000, y: img.midY / g.imageSize.height * 1000)
            check(near(T.globalPoint(normalized: normalized, geometry: g), CGPoint(x: ax.midX, y: ax.midY)), "round trip")
        }

        // Model output validation: never reinterpret, reject instead.
        do {
            let ids: Set = ["ax_1", "ax_2"]
            func parse(_ s: String, image: Bool = true) -> Result<GroundingOutcome, GroundingRejection> { GroundingParser.parse(s, knownIDs: ids, imageAttached: image) }
            check(parse(#"{"kind":"accessibility_target","element_id":"ax_2","instruction":"Click Insert."}"#) == .success(.element(id: "ax_2", instruction: "Click Insert.")), "valid element")
            check(parse(#"{"kind":"accessibility_target","element_id":"ax_9"}"#) == .failure(.unknownElement("ax_9")), "unknown element rejected")
            check(parse(#"{"kind":"visual_target","point_2d":[420,180],"instruction":"Click Export."}"#) == .success(.visual(point: CGPoint(x: 420, y: 180), instruction: "Click Export.")), "valid point")
            check(parse(#"{"kind":"visual_target","point_2d":[420,180]}"#, image: false) == .failure(.noImageForPoint), "point without image rejected")
            check(parse(#"{"kind":"visual_target","point_2d":[1420,180]}"#) == .failure(.pointOutOfRange), "point out of range rejected")
            check(parse(#"{"kind":"visual_target","point_2d":[420]}"#) == .failure(.missingField("point_2d")), "short point rejected")
            check(parse(#"{"kind":"clarification"}"#) == .failure(.missingField("question")), "empty clarification rejected")
            check(parse(#"{"kind":"click","x":1}"#) == .failure(.unknownKind("click")), "unknown kind rejected")
            check(parse("Click the Insert tab") == .failure(.invalidJSON), "prose rejected")
            check(parse(#"{"kind":"done","message":"The folder exists."}"#) == .success(.done("The folder exists.")), "done")
        }

        print(failures == 0 ? "Grounding geometry and parser tests passed." : "\(failures) failure(s)")
        exit(failures == 0 ? 0 : 1)
    }
}
