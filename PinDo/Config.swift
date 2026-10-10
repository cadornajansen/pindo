import Foundation

/// Every non-secret setting, in one place. Values live in UserDefaults (domain com.pindopro.PinDo, editable with
/// `defaults write` or the Settings window); API keys never do — they live in the Keychain (see Keychain in Cloud.swift).
/// Malformed values fall back to the defaults below instead of being used. Tested in tests/ConfigTests.swift.
///
/// Precedence: Settings / `defaults` value if valid → built-in default. No environment variables are read.
nonisolated enum Config {
    static let defaultLocalModel = "maternion/mai-ui:8b"   // MAI-UI 8B (qwen3vl architecture, Q4_K_M) via Ollama
    static let defaultCloudModel = "openai/gpt-6-luna"     // GPT Luna via OpenRouter; used only when switched on
    static let defaultEndpoint = URL(string: "http://127.0.0.1:11434/api/")!
    static let defaultTimeout: TimeInterval = 60

    enum Key {
        static let localModel = "model", endpoint = "ollamaEndpoint", timeout = "inferenceTimeout"
        static let cloudEnabled = "cloudModelEnabled", cloudModel = "cloudModel", groundingDebug = "groundingDebug"
    }

    private static var store: UserDefaults { .standard }

    /// The local model Ollama runs for every request (Guide, Do, Teach, questions).
    static var localModel: String { modelID(store.string(forKey: Key.localModel)) ?? defaultLocalModel }

    /// Ollama's API base. Loopback only, so "local" inference can never be pointed at another machine.
    static var endpoint: URL { loopbackURL(store.string(forKey: Key.endpoint)) ?? defaultEndpoint }

    /// Per-request timeout for model calls, 5–300 s.
    static var inferenceTimeout: TimeInterval {
        let value = store.double(forKey: Key.timeout)
        return (5...300).contains(value) ? value : defaultTimeout
    }

    /// The cloud model standing in for the local one, or nil. Off unless the user switches it on in Settings;
    /// a failed local request never switches it on.
    static var cloudModel: String? {
        guard store.bool(forKey: Key.cloudEnabled) else { return nil }
        return modelID(store.string(forKey: Key.cloudModel)) ?? defaultCloudModel
    }

    static var groundingDebug: Bool { store.bool(forKey: Key.groundingDebug) }

    /// Problems with stored values (logged at launch; the defaults are used instead). Never includes secrets.
    static func problems() -> [String] {
        var found: [String] = []
        if let raw = store.string(forKey: Key.localModel), modelID(raw) == nil { found.append("model “\(raw)” is not a valid model name") }
        if let raw = store.string(forKey: Key.endpoint), loopbackURL(raw) == nil { found.append("ollamaEndpoint must be an http URL on this Mac (127.0.0.1, localhost or ::1)") }
        if store.object(forKey: Key.timeout) != nil, !(5...300).contains(store.double(forKey: Key.timeout)) { found.append("inferenceTimeout must be 5–300 seconds") }
        if let raw = store.string(forKey: Key.cloudModel), modelID(raw) == nil { found.append("cloudModel “\(raw)” is not a valid model name") }
        return found
    }

    // MARK: Validation (pure, tested)

    /// Ollama / OpenRouter model names: letters, digits and . _ : / - only, 1–100 characters.
    static func modelID(_ raw: String?) -> String? {
        guard let raw = raw?.trimmingCharacters(in: .whitespaces), (1...100).contains(raw.count),
              raw.allSatisfy({ $0.isASCII && ($0.isLetter || $0.isNumber || "._:/-".contains($0)) }) else { return nil }
        return raw
    }

    static func loopbackURL(_ raw: String?) -> URL? {
        guard let raw, let url = URL(string: raw), url.scheme == "http" || url.scheme == "https",
              let host = url.host()?.lowercased(), ["127.0.0.1", "localhost", "::1", "[::1]"].contains(host) else { return nil }
        return url.absoluteString.hasSuffix("/") ? url : url.appending(path: "")
    }
}
