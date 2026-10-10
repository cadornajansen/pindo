import AVFoundation
import Foundation
import Security

/// Optional cloud services. Every one is off by default; Pindo works fully on the local model without them.
/// Keys live in the Keychain only (never in defaults, source, logs or the bundle). Tested in tests/CloudTests.swift.
nonisolated enum Provider: String, CaseIterable, Sendable {
    case assemblyAI = "assemblyai", elevenLabs = "elevenlabs", openRouter = "openrouter"

    var name: String {
        switch self { case .assemblyAI: "AssemblyAI"; case .elevenLabs: "ElevenLabs"; case .openRouter: "OpenRouter" }
    }
    /// The UserDefaults switch for the feature this provider powers (voice input, spoken answers, cloud answers).
    var enabledKey: String {
        switch self { case .assemblyAI: "voiceInput"; case .elevenLabs: "voiceOutput"; case .openRouter: "cloudReasoning" }
    }
    var isEnabled: Bool { UserDefaults.standard.bool(forKey: enabledKey) }
    /// A key file is read before the Keychain: an ad-hoc rebuild looks like a new app to the Keychain, which then asks
    /// for the login password on every rebuild (and froze Pindo while the prompt was hidden).
    /// File: ~/Library/Application Support/PinDo/<assemblyai|elevenlabs|openrouter>.key, readable only by you (chmod 600).
    var key: String? { fileKey ?? Keychain.read(rawValue) }

    var fileKey: String? {
        let url = FileManager.default.homeDirectoryForCurrentUser.appending(path: "Library/Application Support/PinDo/\(rawValue).key")
        guard let attributes = try? FileManager.default.attributesOfItem(atPath: url.path),
              let mode = (attributes[.posixPermissions] as? NSNumber)?.intValue, mode & 0o077 == 0, // not readable by others
              let text = try? String(contentsOf: url, encoding: .utf8) else { return nil }
        return text.trimmingCharacters(in: .whitespacesAndNewlines).nonEmptyKey
    }
}

nonisolated enum Keychain {
    private static let service = "com.pindopro.PinDo"
    private static func query(_ account: String) -> [String: Any] {
        [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service, kSecAttrAccount as String: account]
    }

    static func read(_ account: String) -> String? {
        var query = query(account)
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        var out: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &out) == errSecSuccess, let data = out as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    /// Whether a key is saved, without reading the secret itself.
    static func has(_ account: String) -> Bool {
        SecItemCopyMatching(query(account) as CFDictionary, nil) == errSecSuccess
    }

    @discardableResult
    static func save(_ value: String, for account: String) -> Bool {
        delete(account)
        var query = query(account)
        query[kSecValueData as String] = Data(value.utf8)
        return SecItemAdd(query as CFDictionary, nil) == errSecSuccess
    }

    static func delete(_ account: String) { SecItemDelete(query(account) as CFDictionary) }
}

nonisolated struct CloudError: LocalizedError {
    let errorDescription: String?
    init(_ message: String) { errorDescription = message }
}

nonisolated enum Cloud {
    // MARK: AssemblyAI (speech to text): upload, submit, poll. https://www.assemblyai.com/docs/api-reference

    static func transcribe(_ audio: Data, key: String) async throws -> String {
        var upload = request("https://api.assemblyai.com/v2/upload", timeout: 30)
        upload.setValue(key, forHTTPHeaderField: "authorization")
        upload.setValue("application/octet-stream", forHTTPHeaderField: "content-type")
        upload.httpBody = audio
        let audioURL = try field("upload_url", in: try await send(upload, .assemblyAI))

        var submit = request("https://api.assemblyai.com/v2/transcript", timeout: 15)
        submit.setValue(key, forHTTPHeaderField: "authorization")
        submit.setValue("application/json", forHTTPHeaderField: "content-type")
        submit.httpBody = try JSONSerialization.data(withJSONObject: ["audio_url": audioURL]) // default models + language detection
        let id = try field("id", in: try await send(submit, .assemblyAI))
        guard id.allSatisfy({ $0.isLetter || $0.isNumber || $0 == "-" || $0 == "_" }) else { throw malformed(.assemblyAI) }

        let deadline = Date().addingTimeInterval(60)
        while Date() < deadline {
            try await Task.sleep(for: .milliseconds(700))
            var poll = request("https://api.assemblyai.com/v2/transcript/\(id)", method: "GET", timeout: 15)
            poll.setValue(key, forHTTPHeaderField: "authorization")
            let state = try transcriptState(try await send(poll, .assemblyAI))
            switch state.status {
            case "completed": return state.text ?? ""
            case "error": throw CloudError("AssemblyAI couldn't transcribe that (\(state.error ?? "unknown error")).")
            default: continue // queued, processing
            }
        }
        throw CloudError("AssemblyAI took too long to transcribe. Try a shorter recording.")
    }

    static func transcriptState(_ data: Data) throws -> (status: String, text: String?, error: String?) {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let status = object["status"] as? String else { throw malformed(.assemblyAI) }
        return (status, object["text"] as? String, object["error"] as? String)
    }

    // MARK: ElevenLabs (text to speech). https://elevenlabs.io/docs/api-reference/text-to-speech/convert

    static func speech(_ text: String, key: String) async throws -> Data {
        // `defaults write com.pindopro.PinDo elevenLabsVoice <id>` / elevenLabsModel override the documented defaults.
        let configured = UserDefaults.standard.string(forKey: "elevenLabsVoice") ?? ""
        let voice = !configured.isEmpty && configured.allSatisfy({ $0.isLetter || $0.isNumber }) ? configured : "JBFqnCBsd6RMkjVDRZzb"
        let model = UserDefaults.standard.string(forKey: "elevenLabsModel") ?? "eleven_multilingual_v2"
        var req = request("https://api.elevenlabs.io/v1/text-to-speech/\(voice)?output_format=mp3_44100_128", timeout: 20)
        req.setValue(key, forHTTPHeaderField: "xi-api-key")
        req.setValue("application/json", forHTTPHeaderField: "content-type")
        req.setValue("audio/mpeg", forHTTPHeaderField: "accept")
        req.httpBody = try JSONSerialization.data(withJSONObject: ["text": text, "model_id": model])
        let audio = try await send(req, .elevenLabs)
        guard audio.count > 256 else { throw CloudError("ElevenLabs returned no audio.") }
        return audio
    }

    /// What to read aloud: the final reply only (no "▸ step" progress or "⚠︎" prompts), capped so a long
    /// answer can't run up a long, slow synthesis.
    static func spokenText(_ answer: String, limit: Int = 600) -> String? {
        let reply = answer.split(separator: "\n")
            .filter { !$0.hasPrefix("▸") && !$0.hasPrefix("⚠︎") && !$0.hasPrefix("☁︎") }
            .joined(separator: " ").trimmingCharacters(in: .whitespacesAndNewlines)
        guard !reply.isEmpty else { return nil }
        guard reply.count > limit else { return reply }
        let cut = reply.prefix(limit)
        return String(cut[...(cut.lastIndex(where: { ".!?".contains($0) }) ?? cut.index(before: cut.endIndex))])
    }

    // MARK: OpenRouter (optional text answers). Only the user's typed question is sent, never screen content.

    static func answer(_ question: String, key: String) async throws -> String {
        let model = UserDefaults.standard.string(forKey: "openRouterModel") ?? "openrouter/auto"
        var req = request("https://openrouter.ai/api/v1/chat/completions", timeout: 30)
        req.setValue("Bearer \(key)", forHTTPHeaderField: "Authorization")
        req.setValue("application/json", forHTTPHeaderField: "content-type")
        req.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model, "max_tokens": 400,
            "messages": [
                ["role": "system", "content": "You are Pindo, a concise assistant. Answer in a few sentences. You cannot see or control the user's computer."],
                ["role": "user", "content": question],
            ],
        ])
        return try openRouterText(try await send(req, .openRouter))
    }

    /// Settings → "Use cloud model for guidance": the OpenRouter model that replaces the local one for Guide and Do,
    /// or nil when it's off. It receives the same prompts and, for Guide, the window screenshot.
    static var cloudModel: String? {
        guard UserDefaults.standard.bool(forKey: "cloudModelEnabled") else { return nil }
        return UserDefaults.standard.string(forKey: "cloudModel") ?? "openai/gpt-6-luna"
    }

    /// One JSON reply from the cloud model, as text (like Ollama's `response`). Pindo's parsers validate it.
    static func generate(system: String, user: String, image: Data?, model: String) async throws -> String {
        // Off the main thread: a Keychain prompt for a rebuilt app would otherwise freeze Pindo until answered.
        guard let key = await Task.detached(operation: { Provider.openRouter.key }).value else {
            throw CloudError("The cloud model is on, but no OpenRouter key is saved in Settings.")
        }
        var content: [[String: Any]] = [["type": "text", "text": user + "\nReply with exactly one JSON object and nothing else."]]
        if let image { content.append(["type": "image_url", "image_url": ["url": "data:image/jpeg;base64," + image.base64EncodedString()]]) }
        var req = request("https://openrouter.ai/api/v1/chat/completions", timeout: 45)
        req.setValue("Bearer \(key)", forHTTPHeaderField: "Authorization")
        req.setValue("application/json", forHTTPHeaderField: "content-type")
        req.httpBody = try JSONSerialization.data(withJSONObject: [
            "model": model, "max_tokens": 400, "temperature": 0, "response_format": ["type": "json_object"],
            "messages": [["role": "system", "content": system], ["role": "user", "content": content]],
        ])
        let text = try openRouterText(try await send(req, .openRouter))
        // Some models wrap JSON in a code fence even in JSON mode.
        return text.replacingOccurrences(of: "```json", with: "").replacingOccurrences(of: "```", with: "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    static func openRouterText(_ data: Data) throws -> String {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let choices = object["choices"] as? [[String: Any]],
              let message = choices.first?["message"] as? [String: Any],
              let content = (message["content"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines),
              !content.isEmpty else { throw malformed(.openRouter) }
        return content
    }

    // MARK: Shared HTTP

    static func field(_ name: String, in data: Data) throws -> String {
        guard let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let value = object[name] as? String, !value.isEmpty else { throw CloudError("Unexpected response from the cloud service.") }
        return value
    }

    private static func malformed(_ provider: Provider) -> CloudError { CloudError("\(provider.name) sent a response Pindo couldn't read.") }

    private static func request(_ url: String, method: String = "POST", timeout: TimeInterval) -> URLRequest {
        var req = URLRequest(url: URL(string: url)!)
        req.httpMethod = method
        req.timeoutInterval = timeout
        return req
    }

    /// Maps transport and HTTP failures to short, truthful messages. Never includes keys or bodies.
    private static func send(_ req: URLRequest, _ provider: Provider) async throws -> Data {
        let data: Data, response: URLResponse
        do {
            (data, response) = try await URLSession.shared.data(for: req)
        } catch let error as URLError where error.code == .timedOut {
            throw CloudError("\(provider.name) didn't respond in time.")
        } catch let error as URLError where error.code != .cancelled {
            throw CloudError("Couldn't reach \(provider.name). Check your internet connection.")
        }
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        switch status {
        case 200..<300: return data
        case 401, 403: throw CloudError("\(provider.name) rejected the API key. Update it in PinDo Settings.")
        case 402: throw CloudError("\(provider.name) says the account is out of credit.")
        case 429: throw CloudError("\(provider.name) is rate limiting requests. Try again in a moment.")
        default: throw CloudError("\(provider.name) returned an error (HTTP \(status)).")
        }
    }
}

/// Push-to-talk recording: only between the user's start and stop. The file is deleted as soon as it's read.
@MainActor
final class Recorder {
    private var recorder: AVAudioRecorder?

    static func permission() async -> Bool {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: return true
        case .notDetermined: return await AVCaptureDevice.requestAccess(for: .audio)
        default: return false
        }
    }

    func start() throws {
        let url = FileManager.default.temporaryDirectory.appending(path: "pindo-voice-\(UUID().uuidString).m4a")
        let settings: [String: Any] = [AVFormatIDKey: kAudioFormatMPEG4AAC, AVSampleRateKey: 16_000, AVNumberOfChannelsKey: 1,
                                       AVEncoderAudioQualityKey: AVAudioQuality.medium.rawValue]
        let recorder = try AVAudioRecorder(url: url, settings: settings)
        guard recorder.record() else { throw CloudError("Couldn't start recording. Check that a microphone is connected.") }
        self.recorder = recorder
    }

    /// Stops and returns the audio and its length; nil if nothing was recorded.
    func stop() -> (audio: Data, seconds: TimeInterval)? {
        guard let recorder else { return nil }
        let seconds = recorder.currentTime
        recorder.stop()
        self.recorder = nil
        defer { try? FileManager.default.removeItem(at: recorder.url) }
        return (try? Data(contentsOf: recorder.url)).map { ($0, seconds) }
    }

    func discard() {
        recorder?.stop()
        recorder?.deleteRecording()
        recorder = nil
    }
}

/// Plays one answer at a time; starting a new one or stopping cuts the current one off immediately.
@MainActor
final class Speaker {
    private var player: AVAudioPlayer?

    /// Returns when playback ends; cancelling the calling task stops it.
    func play(_ audio: Data) async throws {
        stop()
        let player = try AVAudioPlayer(data: audio)
        self.player = player
        defer { if self.player === player { stop() } }
        guard player.play() else { throw CloudError("Couldn't play the spoken answer.") }
        while player.isPlaying { try await Task.sleep(for: .milliseconds(150)) }
    }

    func stop() {
        player?.stop()
        player = nil
    }
}

private extension String {
    nonisolated var nonEmptyKey: String? { isEmpty ? nil : self }
}
