import Foundation

nonisolated struct SkillTarget: Codable, Sendable {
    let role: String
    let semantic_name: String
}

nonisolated struct SkillStep: Codable, Sendable, Identifiable {
    let id: String
    let objective: String
    let target: SkillTarget
    let expected_result: [String]
    let why: String
    let verification: String
}

nonisolated struct SkillConcept: Codable, Sendable {
    let name: String
    let explanation: String
}

nonisolated struct SkillInput: Codable, Sendable, Identifiable {
    let name: String
    let question: String
    var id: String { name }
}

nonisolated struct SkillRecovery: Codable, Sendable {
    let condition: String
    let guidance: String
}

nonisolated struct SkillValidation: Codable, Sendable {
    let status: String
    let documented_versions: [String]
    let tested_versions: [String]
    let notes: String
}

nonisolated struct TeachingSkill: Codable, Sendable, Identifiable {
    let schema_version: Int
    let id: String
    let application_id: String
    let application: String
    let platform: String
    let surface: String
    let title: String
    let difficulty: String
    let intents: [String]
    let prerequisites: [String]
    let requirements: [String]
    let inputs: [SkillInput]
    let concepts: [SkillConcept]
    let match_groups: [[String]]
    let steps: [SkillStep]
    let recovery: [SkillRecovery]
    let success_criteria: [String]
    let constraints: [String]
    let validation: SkillValidation

    func matches(_ instruction: String) -> Bool {
        let normalized = " " + Self.normalize(instruction) + " "
        return !match_groups.isEmpty && match_groups.allSatisfy { group in
            !group.isEmpty && group.contains { normalized.contains(" " + Self.normalize($0) + " ") }
        }
    }

    private static func normalize(_ text: String) -> String {
        text.lowercased().components(separatedBy: CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyz0123456789").inverted)
            .filter { !$0.isEmpty }.joined(separator: " ")
    }
}

nonisolated struct ApplicationProfile: Codable, Sendable, Identifiable {
    let id: String
    let name: String
    let aliases: [String]
    let bundle_ids: [String]
    let domains: [String]
    let surface: String
    let terminology: [String]

    func matches(name: String, bundleID: String?, host: String?) -> Bool {
        if surface == "browser" {
            guard let host else { return false }
            return domains.contains { host.lowercased() == $0 || host.lowercased().hasSuffix("." + $0) }
        }
        return bundleID.map(bundle_ids.contains) == true || aliases.contains { $0.caseInsensitiveCompare(name) == .orderedSame }
    }
}

nonisolated struct SkillLibrary: Decodable, Sendable {
    let schema_version: Int
    let applications: [ApplicationProfile]
    let skills: [TeachingSkill]

    static func load() throws -> SkillLibrary {
        guard let url = Bundle.main.url(forResource: "ApplicationSkills", withExtension: "json") else {
            throw LibraryError.invalid("The skills resource is missing. Rebuild the app with its resources.")
        }
        return try decode(Data(contentsOf: url))
    }

    static func decode(_ data: Data) throws -> SkillLibrary {
        let library = try JSONDecoder().decode(Self.self, from: data)
        guard library.schema_version == 2, !library.skills.isEmpty,
              Set(library.skills.map(\.id)).count == library.skills.count,
              Set(library.applications.map(\.id)).count == library.applications.count,
              library.skills.allSatisfy({ skill in
                  skill.schema_version == 2 && skill.platform == "macos" && !skill.steps.isEmpty
                  && !skill.match_groups.isEmpty && skill.match_groups.allSatisfy { !$0.isEmpty }
                  && Set(skill.steps.map(\.id)).count == skill.steps.count
                  && skill.steps.allSatisfy { !$0.expected_result.isEmpty && ["visual", "user_confirmation"].contains($0.verification) }
                  && library.applications.contains { $0.id == skill.application_id && $0.surface == skill.surface }
              }) else { throw LibraryError.invalid("The skills resource is invalid or unsupported.") }
        return library
    }

    func candidates(applicationID: String, instruction: String) -> [TeachingSkill] {
        skills.filter { $0.application_id == applicationID && $0.matches(instruction) }.sorted { $0.id < $1.id }
    }

    enum LibraryError: LocalizedError {
        case invalid(String)
        var errorDescription: String? { switch self { case .invalid(let message): message } }
    }
}

/// The host owns progression. A model response cannot skip steps or perform actions.
nonisolated enum TeachingProgress {
    static func isCurrent(session: Int, currentSession: Int, step: Int, currentStep: Int,
                          observation: Int, currentObservation: Int, paused: Bool, sameTarget: Bool) -> Bool {
        session == currentSession && step == currentStep && observation == currentObservation && !paused && sameTarget
    }

    static func canAdvance(step: SkillStep, status: String, evidence: String, userConfirmed: Bool) -> Bool {
        if step.verification == "user_confirmation" { return userConfirmed }
        return status == "observed" && !evidence.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}
