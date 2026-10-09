import Foundation

/// How Pindo handles a request, chosen from its wording so users never pick an internal mode.
/// English and Taglish. Tested in tests/IntentPolicyTests.swift.
nonisolated enum Interaction: Equatable, Sendable {
    case guide        // show where: point at controls, the user clicks (GuideSession)
    case act          // do it: Pindo acts, confirming consequential steps (Agent); also answers questions
    case teach        // a step-by-step lesson from the skills library (TutorSession)
    case clarify      // unclear: ask "Show me, or do it for you?"
}

nonisolated enum IntentPolicy {
    private static let teachPhrases = ["teach me", "walk me through", "step by step", "step-by-step", "tutorial", "lesson",
                                       "turuan mo", "turuan mo ako", "ituro mo"]
    private static let guidePhrases = ["how do i", "how can i", "how to", "how would i", "where do i", "where is", "where's",
                                       "where can i", "which button", "what button", "show me", "find the", "point to",
                                       "paano", "saan", "nasaan", "asan"]
    private static let actVerbs: Set<String> = [
        "convert", "type", "write", "make", "create", "open", "close", "add", "insert", "rename", "move", "organize",
        "organise", "format", "bold", "italicize", "underline", "center", "select", "delete", "remove", "send", "save",
        "set", "change", "put", "draft", "compose", "summarize", "summarise", "translate", "copy", "paste", "fill",
        "sort", "export", "launch", "start", "go", "search", "reply", "forward", "compress", "zip", "merge", "resize",
        "gawin", "gumawa", "isulat", "buksan", "ilipat", "palitan", "i-convert", "ayusin",
    ]
    private static let questionStarts = ["what", "who", "why", "when", "which", "is", "are", "does", "do", "can", "explain",
                                         "summarize", "tell me", "ano", "sino", "bakit", "kailan"]
    private static let politePrefixes = ["please", "can you", "could you", "would you", "pls"]

    /// A plain question (no action to take): answered without touching the computer, and the only kind of
    /// request that may go to optional cloud answers.
    static func isQuestion(_ request: String) -> Bool {
        let words = verbFirst(" " + request.lowercased() + " ")
        guard decide(request) == .act, let first = words.first, !actVerbs.contains(first), !first.hasPrefix("paki") else { return false }
        return questionStarts.contains { words.starts(with: $0.split(separator: " ").map(String.init)) }
    }

    /// Words with polite openers ("can you", "please") removed, so the first word is the verb.
    private static func verbFirst(_ text: String) -> [String] {
        var words = text.replacingOccurrences(of: "?", with: " ").replacingOccurrences(of: ",", with: " ")
            .split(whereSeparator: \.isWhitespace).map(String.init)
        var changed = true
        while changed {
            changed = false
            for p in politePrefixes {
                let pw = p.split(separator: " ").map(String.init)
                if words.starts(with: pw) { words.removeFirst(pw.count); changed = true }
            }
        }
        return words
    }

    static func decide(_ request: String) -> Interaction {
        let text = " " + request.lowercased().trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "?", with: " ").replacingOccurrences(of: ",", with: " ") + " "
        func has(_ phrases: [String]) -> Bool { phrases.contains { text.contains(" " + $0 + " ") } }

        if has(teachPhrases) { return .teach }
        if has(guidePhrases) { return .guide }

        let words = verbFirst(text)
        // "help me <verb> …" is a request to do it when the verb is an action; "help me with this" is unclear.
        if words.starts(with: ["help", "me"]) {
            let rest = Array(words.dropFirst(2))
            if let verb = rest.first, actVerbs.contains(verb) { return .act }
            return .clarify
        }
        guard let first = words.first else { return .clarify }
        // Tagalog "paki-<verb>" / "gawin mo" are already polite commands: do it.
        if first.hasPrefix("paki") || words.starts(with: ["gawin", "mo"]) { return .act }
        if actVerbs.contains(first) { return .act }
        if questionStarts.contains(where: { words.starts(with: $0.split(separator: " ").map(String.init)) }) { return .act }
        return .clarify
    }
}
