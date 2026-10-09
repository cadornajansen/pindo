import CoreGraphics
import Foundation

// Pure, testable pieces of GUI grounding (tests/GroundingTests.swift): coordinate mapping and model-output
// validation. No AppKit, no I/O.
//
// Coordinate systems:
//  - "global": points, origin at the top-left of the primary display, y down. AX positions and
//    CGWindowList bounds use it; other displays can have negative x/y.
//  - "AppKit": points, origin at the bottom-left of the primary display, y up (NSScreen, NSWindow).
//  - "image": pixels of the exact image sent to the model.
//  - "normalized": Qwen3-VL's point_2d, 0...1000 on each axis relative to the image (measured, see README).

/// Where a screenshot came from. Kept by the host, never by the model, and used to map answers back.
nonisolated struct CaptureGeometry: Equatable, Sendable, Codable {
    /// Global frame (points) of the region the image shows: the captured window, or a crop of it.
    var sourceFrame: CGRect
    /// Pixel size of the image the model saw.
    var imageSize: CGSize
    /// Part of the image that holds `sourceFrame`: the whole image, unless it was padded or letterboxed.
    var contentRect: CGRect
    /// Backing scale of the display (for debugging; the mapping itself only uses ratios).
    var displayScale: CGFloat

    init(sourceFrame: CGRect, imageSize: CGSize, contentRect: CGRect? = nil, displayScale: CGFloat = 2) {
        self.sourceFrame = sourceFrame
        self.imageSize = imageSize
        self.contentRect = contentRect ?? CGRect(origin: .zero, size: imageSize)
        self.displayScale = displayScale
    }
}

nonisolated enum CoordinateTransform {
    /// Qwen3-VL point (0...1000 per axis) → image pixels. nil if out of range or not finite.
    static func imagePoint(normalized p: CGPoint, imageSize: CGSize) -> CGPoint? {
        guard p.x.isFinite, p.y.isFinite, (0...1000).contains(p.x), (0...1000).contains(p.y) else { return nil }
        return CGPoint(x: p.x / 1000 * imageSize.width, y: p.y / 1000 * imageSize.height)
    }

    /// Image pixels → global point. nil if the point is in padding, outside the captured content.
    static func globalPoint(imagePoint p: CGPoint, geometry g: CaptureGeometry) -> CGPoint? {
        let c = g.contentRect
        guard c.width > 0, c.height > 0, p.x >= c.minX, p.x <= c.maxX, p.y >= c.minY, p.y <= c.maxY else { return nil }
        let u = (p.x - c.minX) / c.width, v = (p.y - c.minY) / c.height
        return CGPoint(x: g.sourceFrame.minX + u * g.sourceFrame.width, y: g.sourceFrame.minY + v * g.sourceFrame.height)
    }

    /// Model answer → global point, in one step.
    static func globalPoint(normalized p: CGPoint, geometry g: CaptureGeometry) -> CGPoint? {
        imagePoint(normalized: p, imageSize: g.imageSize).flatMap { globalPoint(imagePoint: $0, geometry: g) }
    }

    /// Global rect → image pixels (to draw AX bounds on the debug screenshot).
    static func imageRect(globalRect r: CGRect, geometry g: CaptureGeometry) -> CGRect {
        let sx = g.contentRect.width / g.sourceFrame.width, sy = g.contentRect.height / g.sourceFrame.height
        return CGRect(x: g.contentRect.minX + (r.minX - g.sourceFrame.minX) * sx, y: g.contentRect.minY + (r.minY - g.sourceFrame.minY) * sy,
                      width: r.width * sx, height: r.height * sy)
    }

    /// Global → AppKit. Only the primary display's height is needed: both systems share the primary display's
    /// x axis and its left edge, so this holds for displays left of, above or below it (negative origins).
    static func appKit(_ p: CGPoint, primaryHeight: CGFloat) -> CGPoint { CGPoint(x: p.x, y: primaryHeight - p.y) }
    static func appKit(_ r: CGRect, primaryHeight: CGFloat) -> CGRect {
        CGRect(x: r.minX, y: primaryHeight - r.maxY, width: r.width, height: r.height)
    }

    /// Size for the model: keeps the aspect ratio, never upscales, long edge at most `maxLongEdge` pixels.
    static func fittedSize(_ s: CGSize, maxLongEdge: CGFloat) -> CGSize {
        let scale = min(1, maxLongEdge / max(s.width, s.height))
        return CGSize(width: (s.width * scale).rounded(), height: (s.height * scale).rounded())
    }

    /// A target found on an old screenshot is only shown if its window hasn't moved or resized since.
    static func isStale(captured: CGRect, current: CGRect?, tolerance: CGFloat = 2) -> Bool {
        guard let current else { return true }
        return abs(captured.minX - current.minX) > tolerance || abs(captured.minY - current.minY) > tolerance
            || abs(captured.width - current.width) > tolerance || abs(captured.height - current.height) > tolerance
    }
}

/// What the grounding model may answer. Anything else is rejected, never reinterpreted.
nonisolated enum GroundingOutcome: Equatable, Sendable {
    case element(id: String, instruction: String)        // an accessibility element from the snapshot
    case visual(point: CGPoint, instruction: String)      // normalized 0...1000 on the attached image
    case clarification(String)
    case noTarget(String)
    case done(String)
}

nonisolated enum GroundingRejection: Error, Equatable, Sendable {
    case invalidJSON, unknownKind(String), missingField(String), unknownElement(String), noImageForPoint, pointOutOfRange
}

nonisolated enum GroundingParser {
    /// Parses one model reply. `knownIDs` are the element IDs of this snapshot; points need an attached image.
    static func parse(_ text: String, knownIDs: Set<String>, imageAttached: Bool) -> Result<GroundingOutcome, GroundingRejection> {
        guard let data = text.data(using: .utf8),
              let obj = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any],
              let kind = obj["kind"] as? String else { return .failure(.invalidJSON) }
        func string(_ key: String) -> String? {
            (obj[key] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines).nilIfEmpty
        }
        switch kind {
        case "accessibility_target":
            guard let id = string("element_id") else { return .failure(.missingField("element_id")) }
            guard knownIDs.contains(id) else { return .failure(.unknownElement(id)) }
            return .success(.element(id: id, instruction: string("instruction") ?? ""))
        case "visual_target":
            guard imageAttached else { return .failure(.noImageForPoint) }
            guard let xy = obj["point_2d"] as? [Any], xy.count == 2,
                  let x = (xy[0] as? NSNumber)?.doubleValue, let y = (xy[1] as? NSNumber)?.doubleValue else {
                return .failure(.missingField("point_2d"))
            }
            guard x.isFinite, y.isFinite, (0...1000).contains(x), (0...1000).contains(y) else { return .failure(.pointOutOfRange) }
            return .success(.visual(point: CGPoint(x: x, y: y), instruction: string("instruction") ?? ""))
        case "clarification":
            return string("question").map { .success(.clarification($0)) } ?? .failure(.missingField("question"))
        case "no_target":
            return .success(.noTarget(string("reason") ?? "I can't find that control on screen."))
        case "done":
            return .success(.done(string("message") ?? "Done."))
        default:
            return .failure(.unknownKind(kind))
        }
    }
}

extension String {
    nonisolated var nilIfEmpty: String? { isEmpty ? nil : self }
}
