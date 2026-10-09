// swiftc -swift-version 6 PinDo/Cloud.swift tests/CloudTests.swift -o build/cloud-tests && build/cloud-tests
// Response validation and spoken-text rules for the optional cloud providers (no network).
import Foundation

@main
enum CloudTests {
    static func main() {
        var failures = 0
        func check(_ ok: Bool, _ name: String) { if !ok { failures += 1; print("FAIL \(name)") } }
        func throwsError(_ body: () throws -> Any) -> Bool { (try? body()) == nil }
        let json = { (s: String) in Data(s.utf8) }

        // AssemblyAI
        check((try? Cloud.field("upload_url", in: json(#"{"upload_url":"https://cdn/x"}"#))) == "https://cdn/x", "upload_url")
        check(throwsError { try Cloud.field("upload_url", in: json(#"{"error":"bad"}"#)) }, "missing upload_url")
        check(throwsError { try Cloud.field("id", in: json("<html>")) }, "non-JSON id")
        let done = try? Cloud.transcriptState(json(#"{"status":"completed","text":"open excel"}"#))
        check(done?.status == "completed" && done?.text == "open excel", "completed transcript")
        let failed = try? Cloud.transcriptState(json(#"{"status":"error","error":"no audio"}"#))
        check(failed?.status == "error" && failed?.error == "no audio", "errored transcript")
        check(throwsError { try Cloud.transcriptState(json(#"{"text":"x"}"#)) }, "transcript without status")

        // OpenRouter
        check((try? Cloud.openRouterText(json(#"{"choices":[{"message":{"role":"assistant","content":" Tokyo. "}}]}"#))) == "Tokyo.", "openrouter content")
        check(throwsError { try Cloud.openRouterText(json(#"{"choices":[]}"#)) }, "openrouter no choices")
        check(throwsError { try Cloud.openRouterText(json(#"{"choices":[{"message":{"content":"  "}}]}"#)) }, "openrouter blank")
        check(throwsError { try Cloud.openRouterText(json(#"{"error":{"message":"bad key"}}"#)) }, "openrouter error body")

        // Spoken text: final reply only, never progress lines or prompts; bounded length.
        check(Cloud.spokenText("▸ Opening Safari\nThe capital of Japan is Tokyo.") == "The capital of Japan is Tokyo.", "skip progress")
        check(Cloud.spokenText("▸ Pressing “Bold”\n⚠︎ Press “Delete”?") == nil, "nothing to say")
        check(Cloud.spokenText("") == nil, "empty")
        let long = String(repeating: "This is a sentence. ", count: 60)
        let spoken = Cloud.spokenText(long) ?? ""
        check(spoken.count <= 600 && spoken.hasSuffix("."), "long answer cut at a sentence")

        print(failures == 0 ? "Cloud provider tests passed." : "\(failures) failure(s)")
        exit(failures == 0 ? 0 : 1)
    }
}
