using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using Microsoft.Win32;
using Pointly.App.Presentation;
using Pointly.App.Tools;
using Pointly.App.Tutor;

namespace Pointly.App;

public partial class MainWindow
{
    private string? _workspace;
    private readonly List<string> _selectedFiles = [];
    private readonly List<string> _selectedFolders = [];
    private ToolReviewWindow? _reviewWindow;
    private string? _lastToolResult;

    private void OnSelectFiles()
    {
        var menu = new ContextMenu();
        void Add(string title, Action action)
        {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Add("Add files…", () =>
        {
            var dialog = new OpenFileDialog { Multiselect = true, Title = "Select files for Pindo" };
            if (dialog.ShowDialog() != true) return;
            _workspace ??= Path.GetDirectoryName(dialog.FileNames[0]);
            foreach (string file in dialog.FileNames)
                if (!_selectedFiles.Contains(file, StringComparer.OrdinalIgnoreCase)) _selectedFiles.Add(file);
            UpdateToolSelection();
        });
        Add("Choose workspace…", () =>
        {
            var dialog = new OpenFolderDialog { Title = "Choose a workspace containing your inputs and outputs" };
            if (dialog.ShowDialog() != true) return;
            _workspace = dialog.FolderName;
            _selectedFiles.Clear(); _selectedFolders.Clear();
            UpdateToolSelection();
        });
        Add("Add folder…", () =>
        {
            var dialog = new OpenFolderDialog { Title = "Select a folder inside the workspace" };
            if (dialog.ShowDialog() != true) return;
            _workspace ??= Path.GetDirectoryName(dialog.FolderName);
            if (!_selectedFolders.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase)) _selectedFolders.Add(dialog.FolderName);
            UpdateToolSelection();
        });
        Add("Clear selected files", () => { _selectedFiles.Clear(); _selectedFolders.Clear(); UpdateToolSelection(); });
        if (_lastToolResult is { } result)
            Add("Last tool result…", () => new ToolReviewWindow("Last tool result", result, false).ShowDialog());
        menu.IsOpen = true;
    }

    private void UpdateToolSelection() => _presenter.SetSelection(string.Join(" · ",
        _selectedFiles.Select(Path.GetFileName).Concat(_selectedFolders.Select(path => Path.GetFileName(path) + "/"))
        .Prepend(_workspace is null ? "No workspace" : "Workspace: " + Path.GetFileName(_workspace))));

    private object ToolCatalogForPlanner() => LocalToolDispatcher.Catalog;
    private object ToolSelectionForPlanner() => new { workspace = _workspace, files = _selectedFiles.ToArray(), folders = _selectedFolders.ToArray() };

    private async Task PresentToolProposalAsync(ToolProposal proposal, CancellationToken parentToken)
    {
        if (_workspace is null)
        {
            _goal.AwaitingClarification = true;
            _presenter.ShowInstruction("Use + to choose a workspace or add files, then tell me to continue.");
            return;
        }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        operation.CancelAfter(TimeSpan.FromMinutes(5));
        _goalWork = operation;
        long identity = _goal.Identity;
        try
        {
            string[] urls = _goal.Conversation.Where(text => !text.StartsWith("Pindo:", StringComparison.Ordinal))
                .SelectMany(text => Regex.Matches(text, @"https://[^\s<>""']+").Select(match => match.Value.TrimEnd('.', ',', ')'))).Distinct().ToArray();
            var selection = new ToolSelection(_workspace, _selectedFiles.ToArray(), _selectedFolders.ToArray(), urls);
            _presenter.SetBusy(true);
            _presenter.SetState("Preparing a preview…");
            using ToolReview review = await new LocalToolDispatcher(ToolDependencies.Load()).PrepareAsync(proposal, selection, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (!_goal.IsCurrent(identity)) return;
            _presenter.SetBusy(false);
            _presenter.ShowInstruction("Review the operation and choose Run or Cancel.");
            _reviewWindow = new ToolReviewWindow(review.Title, review.Details, true);
            using CancellationTokenRegistration registration = operation.Token.Register(() => Dispatcher.BeginInvoke(() => _reviewWindow?.Close()));
            bool approved = _reviewWindow.ShowDialog() == true;
            _reviewWindow = null;
            operation.Token.ThrowIfCancellationRequested();
            if (!approved) { _presenter.ShowInstruction("Operation cancelled. No execution was requested."); return; }
            _presenter.SetBusy(true);
            _presenter.SetState("Running local tool…");
            _lastToolResult = "Execution started. If cancelled, check these output locations for completed files.\n\n" + review.Details;
            object result = await review.RunAsync(operation.Token);
            _lastToolResult = ToolReview.Format(result);
            if (!_goal.IsCurrent(identity)) return;
            _goal.Reset();
            _presenter.SetBusy(false);
            _presenter.ShowInstruction("Operation finished. See the results for output paths and any partial failures.");
            _reviewWindow = new ToolReviewWindow("Operation result", _lastToolResult, false);
            _reviewWindow.ShowDialog();
            _reviewWindow = null;
        }
        catch (OperationCanceledException)
        {
            if (_goal.IsCurrent(identity)) _presenter.ShowInstruction("Operation stopped. Use + → Last tool result to check output locations.");
        }
        catch (Exception ex)
        {
            Log($"LocalToolFailed Type={ex.GetType().Name}");
            if (_goal.IsCurrent(identity))
            {
                _goal.AwaitingClarification = true;
                _presenter.ShowInstruction(ex is InvalidOperationException or ArgumentException ? ex.Message : "The local operation failed. Check the selected files and try again.");
            }
        }
        finally
        {
            _reviewWindow = null;
            if (ReferenceEquals(_goalWork, operation)) { _goalWork = null; _presenter.SetBusy(false); }
        }
    }
}
