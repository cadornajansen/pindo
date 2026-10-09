import SwiftUI

struct TutorView: View {
    @Bindable var session: TutorSession

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(session.instruction).font(.system(size: 15)).textSelection(.enabled)
            if session.active {
                if session.skill == nil {
                    Picker("Application", selection: $session.applicationID) {
                        Text("Choose application").tag("")
                        ForEach(session.library?.applications ?? []) { Text($0.name).tag($0.id) }
                    }
                    .onChange(of: session.applicationID) { session.selectApplication() }
                    ForEach(session.choices) { skill in
                        Button(skill.title) { session.choose(skill) }
                    }
                } else if let skill = session.skill {
                    Text("\(skill.application) · \(skill.title)").font(.caption).foregroundStyle(.secondary)
                    Button("Choose another task") { session.showChoices() }.font(.caption)
                    if !session.prepared {
                        Text("Documentation reviewed; hands-on validation pending.").font(.caption)
                        ForEach(skill.prerequisites + skill.requirements, id: \.self) { Text("• " + $0).font(.caption) }
                        ForEach(skill.inputs) { input in Text(input.question).font(.caption) }
                        if !skill.inputs.isEmpty {
                            TextField("Your details", text: $session.inputAnswers, axis: .vertical).textFieldStyle(.roundedBorder)
                        }
                        Button("Requirements met — begin") { session.begin() }
                    } else if let step = session.step {
                        Text("Step \(session.stepIndex + 1) of \(skill.steps.count)").font(.caption)
                        if session.paused && !session.completed { Text(step.objective).font(.caption) }
                        if session.paused && !session.completed {
                            TextField("Add or correct task details before resuming", text: $session.inputAnswers, axis: .vertical)
                                .textFieldStyle(.roundedBorder)
                        }
                        DisclosureGroup("Why this step?") {
                            Text(step.why).font(.caption)
                            ForEach(skill.concepts, id: \.name) { Text($0.name + ": " + $0.explanation).font(.caption) }
                        }
                        DisclosureGroup("What to check") {
                            ForEach(step.expected_result, id: \.self) { Text($0).font(.caption) }
                            if !session.evidence.isEmpty { Text("Observed: " + session.evidence).font(.caption) }
                        }
                        if !session.timing.isEmpty {
                            DisclosureGroup("Response timing") { Text(session.timing).font(.caption) }
                        }
                        HStack {
                            if !session.completed {
                                Button(session.paused ? "Resume watching" : "Pause") {
                                    if session.paused { session.resume() } else { session.pause() }
                                }
                                if step.verification == "user_confirmation" {
                                    Button("I checked this result") { session.confirm() }
                                }
                                if session.checking { ProgressView().controlSize(.small) }
                            }
                            Button("Stop lesson") { session.stop() }
                        }
                        Text(session.paused ? "Watching paused" : "Watching this window locally; you perform the actions.")
                            .font(.caption).foregroundStyle(.secondary)
                    }
                }
            }
        }.padding(18).frame(maxWidth: .infinity, alignment: .leading)
    }
}
