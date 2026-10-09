import AppKit
import Observation

@Observable
final class TutorSession {
    var library: SkillLibrary?
    var skill: TeachingSkill?
    var choices: [TeachingSkill] = []
    var applicationID = ""
    var inputAnswers = ""
    var instruction = "Choose Teach, bring your app forward, and describe the task."
    var evidence = ""
    var paused = true
    var active = false
    var checking = false
    var prepared = false
    var completed = false
    var stepIndex = 0
    var timing = ""
    private var request = ""
    private var target: TutorTarget?
    private var generation = 0
    private var observationRevision = 0
    private var fingerprint: String?
    private var debounce: Task<Void, Never>?
    private var job: Task<Void, Never>?
    private var pending = false
    private let watcher = TutorWatcher()

    var step: SkillStep? { guard let skill, skill.steps.indices.contains(stepIndex) else { return nil }; return skill.steps[stepIndex] }

    init() {
        do { library = try SkillLibrary.load() }
        catch { instruction = error.localizedDescription }
    }

    func start(_ prompt: String) {
        stop()
        request = prompt
        active = true
        do {
            let target = try TutorCapture.target()
            self.target = target
            guard let library else { throw SkillLibrary.LibraryError.invalid("Skills are unavailable. Rebuild the application resource.") }
            let detected = library.applications.filter { $0.matches(name: target.appName, bundleID: target.bundleID, host: target.host) }
            let websites = detected.filter { $0.surface == "browser" }
            let profiles = websites.isEmpty ? detected : websites
            applicationID = profiles.count == 1 ? profiles[0].id : ""
            instruction = applicationID.isEmpty ? "Confirm which application or website is open using the application menu." : "Choose the task you want to learn."
            selectApplication()
        } catch { instruction = error.localizedDescription }
    }

    func selectApplication() {
        guard let library, !applicationID.isEmpty else { return }
        let matches = library.candidates(applicationID: applicationID, instruction: request)
        choices = matches.isEmpty ? library.skills.filter { $0.application_id == applicationID } : matches
        if matches.count == 1 { choose(matches[0]) }
    }

    func choose(_ selected: TeachingSkill) {
        pause()
        skill = selected
        stepIndex = 0
        prepared = false
        completed = false
        inputAnswers = ""
        evidence = ""
        instruction = "Review the requirements and supply the requested details, then begin."
    }

    func showChoices() {
        pause()
        skill = nil
        choices = library?.skills.filter { $0.application_id == applicationID } ?? []
        instruction = "Choose the task you want to learn."
    }

    func begin() {
        guard let skill, skill.inputs.isEmpty || !inputAnswers.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            instruction = "Supply the requested details before beginning."
            return
        }
        prepared = true
        instruction = step?.objective ?? ""
        resume()
    }

    func pause() {
        paused = true
        generation += 1
        debounce?.cancel()
        job?.cancel()
        watcher.stop()
        pending = false
    }

    func stop() {
        pause()
        active = false
        skill = nil
        choices = []
        target = nil
        prepared = false
        completed = false
        fingerprint = nil
        timing = ""
    }

    func resume() {
        guard active, prepared, !completed, let skill else { return }
        do {
            let current = try TutorCapture.target()
            guard let original = target, current.pid == original.pid else {
                throw SkillLibrary.LibraryError.invalid("Return to the application where this lesson started. Start a new lesson to change apps.")
            }
            if skill.surface == "browser", let host = current.host,
               library?.applications.first(where: { $0.id == skill.application_id })?.matches(name: current.appName, bundleID: current.bundleID, host: host) != true {
                throw SkillLibrary.LibraryError.invalid("Return to the selected website before resuming.")
            }
            target = current // Explicit resume acknowledges a modal or a newly selected window.
            fingerprint = nil
            paused = false
            watcher.start(pid: current.pid) { [weak self] in self?.activity() }
            activity()
        } catch { instruction = error.localizedDescription; pause() }
    }

    func activity() {
        guard active, !paused else { return }
        observationRevision += 1
        if (try? TutorCapture.target()) != target {
            instruction = "The application, tab, or window changed. Return to the intended window and resume."
            pause()
            return
        }
        debounce?.cancel()
        debounce = Task { [weak self] in
            do { try await Task.sleep(for: .milliseconds(750)); try Task.checkCancellation() }
            catch { return }
            self?.observe()
        }
    }

    private func observe() {
        guard !paused, let skill, let step, let target else { return }
        guard job == nil else { pending = true; return }
        let token = generation
        let index = stepIndex
        let revision = observationRevision
        checking = true
        job = Task { [weak self] in
            guard let self else { return }
            defer {
                self.checking = false
                self.job = nil
                if self.pending && !self.paused { self.pending = false; self.activity() }
            }
            do {
                let started = Date()
                let observation = try await TutorCapture.capture(target: target, task: skill.title)
                try Task.checkCancellation()
                guard token == self.generation, self.fingerprint != observation.fingerprint else { return }
                let (decision, metrics) = try await TutorClient.evaluate(skill: skill, step: step, request: self.request, inputs: self.inputAnswers, observation: observation)
                try Task.checkCancellation()
                guard TeachingProgress.isCurrent(session: token, currentSession: self.generation, step: index, currentStep: self.stepIndex,
                    observation: revision, currentObservation: self.observationRevision, paused: self.paused, sameTarget: (try? TutorCapture.target()) == target) else {
                    if token == self.generation && !self.paused { self.pending = true }
                    return
                }
                self.fingerprint = observation.fingerprint
                self.timing = "Observation to response: \(Int(Date().timeIntervalSince(started) * 1000)) ms; input tokens: \(metrics.inputTokens.map(String.init) ?? "unavailable"); output tokens: \(metrics.outputTokens.map(String.init) ?? "unavailable")."
                self.evidence = decision.evidence
                self.instruction = decision.instruction
                if decision.status == "uncertain" || decision.status == "needs_input" { self.pause(); return }
                if TeachingProgress.canAdvance(step: step, status: decision.status, evidence: decision.evidence, userConfirmed: false) { self.advance() }
            } catch is CancellationError {
            } catch {
                guard token == self.generation else { return }
                self.instruction = error.localizedDescription
                self.pause()
            }
        }
    }

    func confirm() {
        guard active, prepared, !completed, let step, step.verification == "user_confirmation" else { return }
        generation += 1
        job?.cancel()
        advance()
    }

    private func advance() {
        guard let skill else { return }
        if stepIndex + 1 < skill.steps.count {
            stepIndex += 1
            instruction = skill.steps[stepIndex].objective
            evidence = ""
            // A fresh activity/observation is required for the next step.
        } else {
            completed = true
            instruction = "Lesson complete. Review the result in your application."
            pause()
        }
    }
}
