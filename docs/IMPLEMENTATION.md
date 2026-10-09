# Implementation notes

## Components and dependencies

| Project | Responsibility | References |
|---|---|---|
| `LocalTutor.Desktop` | WPF command palette, native shortcut, foreground context, mock tutor, Ollama boundary | Core |
| `LocalTutor.Core` | Serializable tutoring models and shared contracts | No application projects |
| `LocalTutor.Tools` | Typed, validated local utility base class | Core |
| `LocalTutor.Tests` | Focused tests for shared contracts and pure tool logic | Core, Tools |

Desktop targets `net10.0-windows`; Core, Tools, and Tests target `net10.0` because their shared logic does not require Windows. Add a Desktop → Tools reference only when the desktop needs a real utility. No database, web server, or additional framework is required.

## Current execution flow

1. Application startup captures the original foreground window handle before showing the assistant. WPF then creates the assistant and registers a native hotkey after its own window handle exists.
2. On each `Ctrl + Space`, the handler again captures the foreground window handle **before** showing and activating the assistant. It keeps the last external window if the assistant itself is foreground. A handle (`HWND`) identifies a native Windows window; future UI Automation must inspect that original window.
3. Input submission constructs a `TutorRequest` and calls `ILocalTutorService` asynchronously. The bootstrap implementation is visibly a mock and supplies no real captured UI elements.
4. The response is rendered in the assistant. No model, highlighting, or computer action occurs.
5. Escape hides only when the hotkey is registered. Closing exits; native registration and the message hook are cleaned up. If registration fails, the window remains reachable and reports the collision.

Ollama availability is a separate asynchronous status check. Failure must leave the mock assistant usable. Cancellation tokens let an operation stop when its caller no longer needs it; the UI thread must not block waiting for HTTP work.

### C# and WPF concepts used here

- A `record` holds a named data shape and provides value equality. These DTOs (data transfer objects) can be serialized to JSON without depending on WPF.
- An `interface` specifies what a service must provide. `App` supplies the mock implementation to the window's constructor; this is dependency injection without a container library.
- `Task<T>` represents an operation that produces a `T`. `await` lets the UI process input while an asynchronous operation waits. `string?` permits a missing string, such as no target ID.
- `TInput` and `TOutput` are generic type parameters: each tool chooses concrete input/output types. The interface's `in TInput` allows a tool accepting a broader input type to serve callers supplying a narrower subtype.
- XAML declares the controls; `x:Name` lets C# access a control. `IsDefault="True"` routes Enter to the submit button.
- `nint` holds a native-sized window handle. `DllImport` calls Windows functions from C#. `HwndSource.AddHook` receives `WM_HOTKEY` messages, and `Dispose` unregisters the shortcut when the window exits.

## Tutoring public API

Namespace: **`LocalTutor.Core`**.

| Contract | Members and meaning |
|---|---|
| `ScreenRectangle` | `Left`, `Top`, `Width`, `Height`, in physical screen pixels; negative left/top are valid on multi-monitor desktops |
| `UiElementSnapshot` | `Id`, `Name`, `ControlType`, `Bounds`, optional `IsEnabled`; IDs are stable within one snapshot, not across application changes |
| `TutorRequest` | `Instruction`, `ActiveApplicationName`, `Elements` |
| `TutorResponse` | Nullable `TargetElementId`, `Instruction`, `Success`, optional `Error` |
| `ILocalTutorService` | `Task<TutorResponse> GetNextStepAsync(TutorRequest request, CancellationToken cancellationToken = default)` |

A future response target must resolve to an element from the same captured snapshot before any highlight appears. The future highlight layer must convert physical pixels to WPF device-independent units using the appropriate monitor DPI. Never treat model-generated coordinates as validated screen geometry.

## Member 2: tool public API

Contracts are in **`LocalTutor.Core.Tools`**:

```csharp
public abstract record ToolInput
{
    public abstract IReadOnlyList<string> Validate();
}

public sealed record ToolResult<TOutput>(bool Success, TOutput? Value, string? Error = null);

public interface ILocalTool<in TInput, TOutput> where TInput : ToolInput
{
    string Id { get; }
    string Description { get; }
    Task<ToolResult<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}
```

Implementation base: **`LocalTutor.Tools.LocalTool<TInput, TOutput>`**, constrained to `TInput : ToolInput`. Derive from it, define `Id` and `Description`, and implement `ExecuteValidatedAsync(TInput input, CancellationToken cancellationToken)`. Its public `ExecuteAsync` checks cancellation and validation before reaching your implementation. Validation returns all input errors; a nonempty list prevents execution.

Use a small typed input record and output record for each approved utility. Test invalid input, success, and cancellation in a tool-specific test file. A future caller must explicitly allow approved tool IDs and obtain appropriate confirmation before execution; the generic contract alone is not an authorization system. Do not add a string-based shell-command tool or a plugin discovery framework.

## Ollama boundary

`src/LocalTutor.Desktop/Services/OllamaSettings.cs` owns the source defaults: `http://localhost:11434` and `qwen3:1.7b`. Rebuild after editing them. No settings file, environment loader, automatic install, or model download is wired up.

`IOllamaClient` currently exposes the availability boundary only. Its HTTP implementation checks `GET /api/version` asynchronously; this is not model inference and does not prove a model is installed. Future inference can extend this boundary after benchmarking without coupling Core or Tools to HTTP or WPF.

## Small checkpoints

1. **Bootstrap (current):** solution, mock assistant, shortcut, shared contracts, availability plumbing, docs, and repository.
2. **Next milestone only:** benchmark local `qwen3:1.7b` and collect/filter one bounded UI Automation snapshot. Agree on output validation before building the end-to-end tutor.

Technical risks to measure: unavailable accessibility controls, changing/stale UI snapshots, mixed-DPI monitors, hotkey collisions, foreground activation restrictions, and local-model latency/target accuracy. No benchmark or broad application support is currently established.

Verification commands and manual checks are in [README](../README.md). Restore and build passed with no warnings or errors; all 3 automated tests passed. A Windows 11 build `26200` interactive smoke check passed 22 assertions at 125% display scaling, covering launch/focus, input validation, Submit/Enter, Hide/Escape, the actual global shortcut, collision handling, graceful exit, and immediate hotkey re-registration after exit. The assistant remained usable without Ollama. Screenshot review found no clipping. Windows 10 and mixed-DPI behavior remain untested; product UI Automation, inference, and highlighting remain unimplemented.

References: [Windows UI Automation overview](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-overview), [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey), [Ollama API](https://github.com/ollama/ollama/blob/main/docs/api.md).

## Bootstrap file inventory

All 30 repository files below were created for this bootstrap. No existing project files were modified or deleted. Temporary generated `Class1.cs` files in Core/Tools and `UnitTest1.cs` were discarded before the initial commit. Build outputs and IDE caches are excluded by `.gitignore`.

| File | Purpose |
|---|---|
| `.editorconfig` | Consistent formatting conventions |
| `.gitattributes` | Repository line-ending conventions |
| `.gitignore` | Exclude builds, IDE caches, temporary files, and secrets |
| `global.json` | Select a stable .NET 10 SDK |
| `LocalTutor.slnx` | Group the four projects |
| `README.md` | Setup, verification, limitations, and onboarding entry point |
| `docs/PRD.md` | Audience, product scope, and acceptance targets |
| `docs/IMPLEMENTATION.md` | Architecture, contracts, flow, and this inventory |
| `docs/HACKATHON_COMPLIANCE.md` | Disclosures, submission checklist, unresolved rules |
| `docs/TEAM_TASKS.md` | Ownership, branches, contributor workflow |
| `src/LocalTutor.Core/LocalTutor.Core.csproj` | Shared .NET library project |
| `src/LocalTutor.Core/TutorContracts.cs` | Screen bounds, UI snapshots, tutor request/response/service |
| `src/LocalTutor.Core/Tools/ToolContracts.cs` | Typed utility input/result/interface |
| `src/LocalTutor.Tools/LocalTutor.Tools.csproj` | Utility library and Core reference |
| `src/LocalTutor.Tools/LocalTool.cs` | Validate and check cancellation before utility execution |
| `src/LocalTutor.Desktop/LocalTutor.Desktop.csproj` | Windows WPF executable and Core reference |
| `src/LocalTutor.Desktop/App.xaml` | WPF application resources and lifetime settings |
| `src/LocalTutor.Desktop/App.xaml.cs` | Startup foreground capture, dependency wiring, HTTP lifetime |
| `src/LocalTutor.Desktop/AssemblyInfo.cs` | WPF resource-theme assembly metadata |
| `src/LocalTutor.Desktop/MainWindow.xaml` | Compact assistant controls and dark styling |
| `src/LocalTutor.Desktop/MainWindow.xaml.cs` | Submit, focus, status, hide, close, and process context |
| `src/LocalTutor.Desktop/Interop/NativeMethods.cs` | Typed native Windows API declarations |
| `src/LocalTutor.Desktop/Interop/GlobalHotkey.cs` | Register/receive/unregister shortcut and capture foreground HWND |
| `src/LocalTutor.Desktop/Services/IOllamaClient.cs` | Async local-runtime availability boundary |
| `src/LocalTutor.Desktop/Services/OllamaSettings.cs` | Central source defaults for local URL/model |
| `src/LocalTutor.Desktop/Services/OllamaAvailabilityClient.cs` | Nonblocking version-endpoint availability check |
| `src/LocalTutor.Desktop/Services/MockTutorService.cs` | Explicitly labeled mock submission response |
| `tests/LocalTutor.Tests/LocalTutor.Tests.csproj` | Test dependencies and Core/Tools references |
| `tests/LocalTutor.Tests/TutorContractsTests.cs` | Shared tutoring-contract verification |
| `tests/LocalTutor.Tests/LocalToolTests.cs` | Tool validation and cancellation verification |
