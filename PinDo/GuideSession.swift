import AppKit
import os

/// Guide mode: point at the next control, let the user act, look again, repeat until the goal is reached.
/// Pindo never clicks or types here. Every async result carries the session generation, so cancelled or
/// superseded requests (and app switches) can't put up a late pointer.
@Observable
final class GuideSession {
    private(set) var active = false        // guiding (working or waiting for the user)
    private(set) var working = false       // collecting + asking the model
    private(set) var step = 0
    private(set) var lastTimings: Grounder.Timings?

    let overlay = PointerOverlay()
    /// Called with the target's AppKit point so the quick bar can move out of the way.
    var onTarget: ((CGPoint) -> Void)?

    private static let maxSteps = 10
    private var goal = ""
    private var procedure: String?     // matched lesson from the skills library, if any
    private var webEditor = false      // the lesson is for a web app (Canva…): its UI is drawn, so look at the screen
    private var history: [String] = []
    private var lastLabel: String?
    private var targetPID: pid_t?
    private var generation = 0
    private var report: ((String) -> Void)?
    private var monitor: Any?
    private var appWatcher: NSObjectProtocol?

    func start(_ goal: String, report: @escaping (String) -> Void) {
        stop()
        self.goal = goal
        self.report = report
        history = []
        step = 0
        lastLabel = nil
        let skill = Skills.match(task: goal)
        procedure = skill.map(Skills.procedure)
        webEditor = skill?.surface == "browser"
        active = true
        appWatcher = NSWorkspace.shared.notificationCenter.addObserver(forName: NSWorkspace.didActivateApplicationNotification, object: nil, queue: .main) { [weak self] note in
            let app = note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication
            MainActor.assumeIsolated { self?.appActivated(app?.processIdentifier) }
        }
        advance()
    }

    /// "Check again": look at the screen now (also resumes after switching apps).
    func checkAgain() {
        guard active, !working else { return }
        endWait()
        advance()
    }

    func stop() {
        generation += 1
        active = false
        working = false
        endWait()
        overlay.hide()
        if let appWatcher { NSWorkspace.shared.notificationCenter.removeObserver(appWatcher) }
        appWatcher = nil
    }

    private func finish(_ message: String) {
        report?(message)
        stop()
    }

    /// `vision`: include the screenshot even if Accessibility looks rich enough.
    private func advance(vision: Bool = false) {
        generation += 1
        let token = generation
        working = true
        overlay.hide()
        let goal = self.goal, history = self.history, procedure = self.procedure, useVision = vision || webEditor
        Task {
            defer { if token == self.generation { self.working = false } }
            guard let ctx = await Task.detached(operation: { GroundingCollector.collect(task: goal) }).value else {
                report?("Bring the app you need help with to the front, then press Check again.")
                return
            }
            do {
                let (result, timings) = try await Grounder.ground(goal: goal, history: history, context: ctx,
                                                                  procedure: procedure, vision: useVision, pointOnly: webEditor)
                // Stale: cancelled, superseded, or the user moved to another app while the model was thinking.
                guard token == generation, NSWorkspace.shared.frontmostApplication?.processIdentifier == ctx.pid else { return }
                lastTimings = timings
                groundLog.info("""
                    grounded in collect \(timings.collect, format: .fixed(precision: 0))ms, capture \(timings.capture, format: .fixed(precision: 0))ms, \
                    text \(timings.textInference, format: .fixed(precision: 0))ms, vision \(timings.visionInference, format: .fixed(precision: 0))ms
                    """)
                switch result {
                case .message(let text, let final):
                    final ? finish(text) : report?(text)
                case .target(let target):
                    if target.label == lastLabel, !useVision {
                        // Same target after the user acted: Accessibility may not show what changed (a ribbon tab that
                        // opened, a canvas). Look at the screen once before saying nothing happened.
                        return advance(vision: true)
                    } else if target.label == lastLabel {
                        report?("That step doesn't seem to have changed anything yet. Try “\(target.label)” again, or press Check again.")
                    } else {
                        step += 1
                        report?(target.instruction)
                    }
                    guard step <= Self.maxSteps else { return finish("Stopped after \(Self.maxSteps) steps. Tell me what's left.") }
                    lastLabel = target.label
                    targetPID = target.pid
                    overlay.show(target)
                    onTarget?(CoordinateTransform.appKit(target.point, primaryHeight: NSScreen.screens.first?.frame.maxY ?? 0))
                    waitForUser(token: token, label: target.label)
                }
            } catch GroundingCapture.Failure.permission {
                finish("Pindo needs Screen Recording permission to see this window. Allow it in System Settings → Privacy & Security, then try again.")
            } catch let error as CloudError {
                guard token == generation else { return }
                finish(error.localizedDescription)
            } catch {
                guard token == generation else { return }
                finish("Couldn't reach the local model. Is Ollama running? (\(error.localizedDescription))")
            }
        }
    }

    /// The user's own click or key press in the target app ends this step; then Pindo looks again.
    private func waitForUser(token: Int, label: String) {
        endWait()
        monitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseUp, .keyDown]) { [weak self] _ in
            MainActor.assumeIsolated {
                // Only the user's actions in the guided app count (a click in another app is not progress).
                guard let self, token == self.generation, self.active, !self.working,
                      NSWorkspace.shared.frontmostApplication?.processIdentifier == self.targetPID else { return }
                self.endWait()
                self.overlay.hide()
                self.history.append("step \(self.step): pointed at “\(label)”, then the user acted")
                Task {
                    try? await Task.sleep(for: .milliseconds(700)) // let the UI settle (menus open, dialogs appear)
                    guard token == self.generation, self.active else { return }
                    self.advance()
                }
            }
        }
    }

    private func endWait() {
        if let monitor { NSEvent.removeMonitor(monitor) }
        monitor = nil
    }

    private func appActivated(_ pid: pid_t?) {
        guard active, let pid, pid != ProcessInfo.processInfo.processIdentifier, pid != targetPID else { return }
        generation += 1 // drop anything in flight
        working = false
        endWait()
        overlay.hide()
        report?("Paused because you switched apps. Go back and press Check again to continue.")
    }
}
