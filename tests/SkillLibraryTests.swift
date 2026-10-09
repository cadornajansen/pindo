import Foundation

@main
struct SkillLibraryTests {
    static func main() throws {
        let library = try SkillLibrary.decode(Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1])))
        for skill in library.skills {
            for intent in skill.intents {
                precondition(library.candidates(applicationID: skill.application_id, instruction: intent).contains { $0.id == skill.id }, "Unmatched intent: \(skill.id): \(intent)")
            }
            for step in skill.steps {
                precondition(!TeachingProgress.canAdvance(step: step, status: "not_yet", evidence: "A click occurred", userConfirmed: false))
                precondition(!TeachingProgress.canAdvance(step: step, status: "observed", evidence: "  ", userConfirmed: false))
                precondition(TeachingProgress.canAdvance(step: step, status: "observed", evidence: "Visible result", userConfirmed: false) == (step.verification == "visual"))
                precondition(TeachingProgress.canAdvance(step: step, status: "uncertain", evidence: "", userConfirmed: true) == (step.verification == "user_confirmation"))
            }
        }
        precondition(library.candidates(applicationID: "unknown.app", instruction: "insert image").isEmpty)
        let profile = ApplicationProfile(id: "test.browser", name: "Example", aliases: ["Example"], bundle_ids: [], domains: ["example.com"], surface: "browser", terminology: ["page"])
        precondition(profile.matches(name: "Safari", bundleID: nil, host: "www.example.com"))
        precondition(!profile.matches(name: "Safari", bundleID: nil, host: "example.com.evil.test"))
        precondition(!profile.matches(name: "Example", bundleID: nil, host: nil))
        print("Swift matching, application identity, and completion gates passed for \(library.skills.count) skills.")
    }
}
