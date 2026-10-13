using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Win32;
using Pindo.App.Input;
using Pindo.App.Planning;
using Pindo.App.Presentation;
using Pindo.App.Shell;
using Pindo.App.Tools;

namespace Pindo.App;

internal sealed class UtilityController : IDisposable
{
    private readonly ChatWindow _chat = new();
    private readonly OpenRouterPlanner _planner;
    private readonly UtilitySession _session = new();
    private readonly ModifierHotkey _hotkey;
    private readonly TrayShell _tray;
    private readonly List<string> _files = [];
    private readonly List<string> _folders = [];
    private string? _workspace;
    private string? _lastResult;
    private long _lastResultIdentity;
    private CancellationTokenSource? _operation;
    private ToolReviewWindow? _review;
    private bool _disposed;

    public UtilityController(OpenRouterPlanner? planner = null)
    {
        _planner = planner ?? new OpenRouterPlanner();
        new WindowInteropHelper(_chat).EnsureHandle();
        _hotkey = new ModifierHotkey(() => _chat.Dispatcher.BeginInvoke(Toggle));
        _tray = new TrayShell(Toggle, () => Application.Current.Shutdown());
        _chat.QuestionSubmitted += Submit;
        _chat.DismissRequested += Dismiss;
        _chat.SelectionRequested += Select;
        try { _hotkey.Start(); }
        catch (Exception) { _tray.Report("The Ctrl+Windows shortcut is unavailable. Open Pindo from its tray icon."); }
    }

    private void Toggle()
    {
        if (_disposed) return;
        if (_chat.IsVisible) Dismiss();
        else _chat.Present(DesktopGeometry.At(DesktopGeometry.Cursor()), focusInput: true);
    }

    private void Cancel()
    {
        _session.Reset();
        _operation?.Cancel();
        _operation = null;
        _review?.Close();
        _review = null;
        _chat.SetBusy(false);
        _chat.SetState("");
    }

    private void Dismiss()
    {
        Cancel();
        _chat.SetInstruction("");
        _chat.Hide();
    }

    public void Suspend() { _hotkey.Reset(); Dismiss(); }

    private async void Submit(string question)
    {
        if (_disposed || _operation is not null) return;
        if (Configuration.Read("PINDO_PREVIEW") == "true")
        {
            _chat.SetInstruction("Preview only. No AI request was sent.");
            return;
        }
        long identity = _session.Begin(question);
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _operation = operation;
        _chat.SetBusy(true);
        _chat.SetState("Preparing your request…");
        try
        {
            ToolSelection selection = SnapshotSelection();
            UtilityResponse response = await _planner.PlanAsync(_session.Conversation.ToArray(), selection, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (!_session.IsCurrent(identity)) return;
            _chat.SetInstruction(response.Message);
            if (response.Kind == "clarification") _session.Clarify(response.Message);
            else if (response.Tool is { } proposal)
            {
                if (_workspace is null)
                {
                    _session.Reset();
                    _chat.SetInstruction("Use + to select your files and workspace, then send the request again.");
                    return;
                }
                operation.CancelAfter(TimeSpan.FromMinutes(5));
                await ReviewAsync(proposal, selection, identity, operation.Token);
            }
            else _session.Reset();
        }
        catch (OperationCanceledException)
        {
            if (_session.IsCurrent(identity))
            {
                _session.Reset();
                _chat.SetInstruction("Request stopped or timed out. You can send it again. Last tool result lists any started outputs.");
            }
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(identity))
            {
                _session.Reset();
                _chat.SetInstruction(ex is InvalidOperationException or ArgumentException or HttpRequestException
                    ? ex.Message : "The operation failed. Check your selected files and try again.");
            }
        }
        finally
        {
            if (ReferenceEquals(_operation, operation))
            {
                _operation = null;
                _chat.SetBusy(false);
                _chat.SetState("");
            }
        }
    }

    private ToolSelection SnapshotSelection()
    {
        string[] urls = _session.Conversation.Where(text => !text.StartsWith("Pindo:", StringComparison.Ordinal))
            .SelectMany(text => Regex.Matches(text, "https://[^\\s<>\"']+")
                .Select(match => match.Value.TrimEnd('.', ',', ')'))).Distinct().ToArray();
        return new(_workspace ?? "", _files.ToArray(), _folders.ToArray(), urls);
    }

    private async Task ReviewAsync(ToolProposal proposal, ToolSelection selection, long identity, CancellationToken token)
    {
        using ToolReview review = await new LocalToolDispatcher(ToolDependencies.Load()).PrepareAsync(proposal, selection, token);
        token.ThrowIfCancellationRequested();
        if (!_session.IsCurrent(identity)) return;
        _chat.SetState("Review the operation before running it.");
        bool approved = ShowReview(new ToolReviewWindow(review.Title, review.Details, true), token);
        token.ThrowIfCancellationRequested();
        if (!_session.IsCurrent(identity)) return;
        if (!approved)
        {
            _session.Reset();
            _chat.SetInstruction("Cancelled. No execution was requested.");
            return;
        }
        _chat.SetState("Running local utility…");
        _lastResultIdentity = identity;
        _lastResult = "Execution started. If interrupted, check these output locations for completed files.\n\n" + review.Details;
        object result = await review.RunAsync(token);
        // Preserve the result of started work even if its UI request was dismissed.
        string resultText = ToolReview.Format(result);
        if (_lastResultIdentity == identity) _lastResult = resultText;
        token.ThrowIfCancellationRequested();
        if (!_session.IsCurrent(identity)) return;
        _chat.SetInstruction("Operation finished. Review the output paths and any partial failures.");
        ShowReview(new ToolReviewWindow("Operation result", resultText, false), token);
        if (_session.IsCurrent(identity)) _session.Reset();
    }

    private bool ShowReview(ToolReviewWindow window, CancellationToken token)
    {
        _review = window;
        using CancellationTokenRegistration registration = token.Register(() => _chat.Dispatcher.BeginInvoke(() =>
        {
            if (ReferenceEquals(_review, window)) window.Close();
        }));
        try { return window.ShowDialog() == true; }
        finally { if (ReferenceEquals(_review, window)) _review = null; }
    }

    private void Select()
    {
        if (_operation is not null) return;
        var menu = new ContextMenu();
        void Add(string title, Action action)
        {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Add("Add files…", AddFiles);
        Add("Choose workspace…", ChooseWorkspace);
        Add("Add folder…", AddFolder);
        Add("Clear selected files", () => { _files.Clear(); _folders.Clear(); SelectionChanged(); });
        if (_lastResult is { } result)
            Add("Last tool result…", () => new ToolReviewWindow("Last tool result", result, false).ShowDialog());
        menu.IsOpen = true;
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Select files for Pindo" };
        if (dialog.ShowDialog() != true) return;
        _workspace ??= Path.GetDirectoryName(dialog.FileNames[0]);
        foreach (string file in dialog.FileNames)
            if (!_files.Contains(file, StringComparer.OrdinalIgnoreCase)) _files.Add(file);
        SelectionChanged();
    }

    private void ChooseWorkspace()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the workspace for your inputs and outputs" };
        if (dialog.ShowDialog() != true) return;
        _workspace = dialog.FolderName;
        _files.Clear(); _folders.Clear();
        SelectionChanged();
    }

    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select a folder inside the workspace" };
        if (dialog.ShowDialog() != true) return;
        _workspace ??= Path.GetDirectoryName(dialog.FolderName);
        if (!_folders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase)) _folders.Add(dialog.FolderName);
        SelectionChanged();
    }

    private void SelectionChanged()
    {
        Cancel();
        _chat.SetInstruction("Selection updated. Describe the operation you want to run.");
        _chat.SetSelection(string.Join(" · ", _files.Select(Path.GetFileName)
            .Concat(_folders.Select(path => Path.GetFileName(path) + "/"))
            .Prepend(_workspace is null ? "No workspace" : "Workspace: " + Path.GetFileName(_workspace))));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkey.Dispose();
        Cancel();
        _chat.Exit();
        _tray.Dispose();
        _planner.Dispose();
    }
}
