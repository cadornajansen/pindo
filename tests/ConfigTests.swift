// swiftc -swift-version 6 PinDo/Config.swift tests/ConfigTests.swift -o build/config-tests && build/config-tests
// Settings validation: bad values must never be used (e.g. a "local" endpoint on another machine).
import Foundation

@main
enum ConfigTests {
    static func main() {
        var failures = 0
        func check(_ ok: Bool, _ name: String) { if !ok { failures += 1; print("FAIL \(name)") } }

        check(Config.modelID("maternion/mai-ui:8b") == "maternion/mai-ui:8b", "MAI-UI id")
        check(Config.modelID("openai/gpt-6-luna") == "openai/gpt-6-luna", "GPT Luna id")
        check(Config.modelID("") == nil, "empty model")
        check(Config.modelID("model; rm -rf /") == nil, "model with spaces and symbols")
        check(Config.modelID(String(repeating: "a", count: 101)) == nil, "overlong model")
        check(Config.modelID("modèle") == nil, "non-ASCII model")

        check(Config.loopbackURL("http://127.0.0.1:11434/api/") != nil, "loopback v4")
        check(Config.loopbackURL("http://localhost:11434/api/") != nil, "localhost")
        check(Config.loopbackURL("http://[::1]:11434/api/") != nil, "loopback v6")
        check(Config.loopbackURL("http://192.168.1.20:11434/api/") == nil, "LAN host is not local")
        check(Config.loopbackURL("http://localhost.evil.test:11434/") == nil, "lookalike host")
        check(Config.loopbackURL("file:///etc/passwd") == nil, "non-http scheme")
        check(Config.loopbackURL(nil) == nil, "missing")

        check(Config.defaultLocalModel == "maternion/mai-ui:8b", "local default is MAI-UI 8B")
        check(Config.defaultEndpoint.host() == "127.0.0.1", "default endpoint is loopback")

        print(failures == 0 ? "Config tests passed." : "\(failures) failure(s)")
        exit(failures == 0 ? 0 : 1)
    }
}
