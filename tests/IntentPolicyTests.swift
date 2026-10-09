// swiftc -swift-version 6 PinDo/IntentPolicy.swift tests/IntentPolicyTests.swift -o build/intent-tests && build/intent-tests
import Foundation

@main
enum IntentPolicyTests {
    static func main() {
        let cases: [(String, Interaction)] = [
            // The brief's examples
            ("Convert this PDF to a Word document", .act),
            ("How do I create a pivot table?", .guide),
            ("Where do I change this setting?", .guide),
            ("Help me organize these files", .act),
            // Guide: show where
            ("where is the export button", .guide),
            ("Paano gumawa ng chart sa Excel?", .guide),
            ("saan yung insert image", .guide),
            ("show me how to add a slide", .guide),
            ("How can I insert an image into this slide?", .guide),
            // Act: do it, or answer
            ("make all the text bold", .act),
            ("type 'Hello' in the document", .act),
            ("please open Excel", .act),
            ("can you summarize this email", .act),
            ("pakigawa ng new folder", .act),
            ("what is the capital of France?", .act),
            ("explain this error", .act),
            ("rename it to Notes", .act),
            // Teach: lessons
            ("teach me pivot tables", .teach),
            ("walk me through making a chart", .teach),
            ("turuan mo ako mag excel", .teach),
            // Unclear: ask
            ("help me with this", .clarify),
            ("pivot table", .clarify),
            ("", .clarify),
        ]
        var failures = 0
        // Only plain questions may go to optional cloud answers.
        for (request, expected) in [("what is the capital of Japan?", true), ("who wrote Noli Me Tangere", true),
                                    ("make all the text bold", false), ("how do I make a chart?", false), ("pivot table", false),
                                    ("can you open Excel", false), ("please what is 2+2", true), ("pakigawa ng folder", false),
                                    ("is the file saved?", true)]
        where IntentPolicy.isQuestion(request) != expected {
            failures += 1
            print("FAIL isQuestion(\(request.debugDescription)): expected \(expected)")
        }
        for (request, expected) in cases where IntentPolicy.decide(request) != expected {
            failures += 1
            print("FAIL \(request.debugDescription): got \(IntentPolicy.decide(request)), expected \(expected)")
        }
        print(failures == 0 ? "Intent policy tests passed (\(cases.count) cases)." : "\(failures) failure(s)")
        exit(failures == 0 ? 0 : 1)
    }
}
