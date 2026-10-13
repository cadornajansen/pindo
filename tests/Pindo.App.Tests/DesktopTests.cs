using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pindo.App.Planning;
using Pindo.App.Presentation;

namespace Pindo.App.Tests;

public sealed class DesktopTests
{
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();

    private sealed class NativeInputFactAttribute : FactAttribute
    {
        public NativeInputFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("PINDO_INPUT_TEST") != "1")
                Skip = "Run separately on an idle desktop with PINDO_INPUT_TEST=1; this test sends native modifier input.";
        }
    }

    [NativeInputFact]
    public void NativeMenuMaskDoesNotOpenStartOrLeaveModifiersPressed() => Sta(() =>
    {
        byte[] modifiers = [0xA2, 0xA3, 0x5B, 0x5C];
        Assert.All(modifiers, key => Assert.Equal(0, GetAsyncKeyState(key) & 0x8000));
        try
        {
            MethodInfo mask = typeof(Pindo.App.Input.ModifierHotkey).GetMethod("MaskStartMenu", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (object[] variant in ModifierChordTests.Chords())
            {
                // No focus stealing: the mask must preserve whichever window was already active.
                nint handle = GetForegroundWindow();
                Assert.NotEqual(nint.Zero, handle);
                byte ctrl = (byte)(int)variant[0], win = (byte)(int)variant[1];
                bool ctrlFirst = (bool)variant[2], ctrlReleasedFirst = (bool)variant[3];
                keybd_event(ctrlFirst ? ctrl : win, 0, 0, 0);
                keybd_event(ctrlFirst ? win : ctrl, 0, 0, 0);
                PumpUntil(() => (GetAsyncKeyState(ctrl) & 0x8000) != 0 && (GetAsyncKeyState(win) & 0x8000) != 0);
                Assert.True((bool)mask.Invoke(null, null)!);
                keybd_event(ctrlReleasedFirst ? ctrl : win, 0, 2, 0);
                keybd_event(ctrlReleasedFirst ? win : ctrl, 0, 2, 0);
                PumpUntil(() => modifiers.All(key => (GetAsyncKeyState(key) & 0x8000) == 0));
                DateTime settle = DateTime.UtcNow.AddMilliseconds(100);
                PumpUntil(() => DateTime.UtcNow >= settle);
                Assert.Equal(handle, GetForegroundWindow());
            }
        }
        finally
        {
            foreach (byte key in modifiers) keybd_event(key, 0, 2, 0);
        }
    });

    private sealed class Handler(Func<Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => response();
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void PumpUntil(Func<bool> ready)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ready())
        {
            Assert.True(DateTime.UtcNow < deadline, "UI did not settle in five seconds.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(5);
        }
    }
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "UI thread did not stop.");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    [Fact]
    public void ComposerOpensWithoutTargetAndCancellationRejectsDelayedReply() => Sta(() =>
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(() => pending.Task));
        using var planner = new OpenRouterPlanner(http, name => name == "OPENROUTER_API_KEY" ? "test" : null);
        using var controller = new UtilityController(planner);
        ChatWindow chat = Field<ChatWindow>(controller, "_chat");
        Call(controller, "Toggle");
        Assert.True(chat.IsVisible);
        Call(controller, "Submit", "Make a ZIP");
        Assert.False(((TextBox)chat.FindName("Question")).IsEnabled);
        ((Button)chat.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(chat.IsVisible);
        pending.SetResult(new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
        { choices = new[] { new { finish_reason = "stop", message = new { content = "{\"kind\":\"clarification\",\"message\":\"LATE\"}" } } } })) });
        Call(controller, "Toggle");
        PumpUntil(() => pending.Task.IsCompleted);
        for (int i = 0; i < 10; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
        Assert.NotEqual("LATE", ((TextBlock)chat.FindName("Instruction")).Text);
        Assert.True(((TextBox)chat.FindName("Question")).IsEnabled);
        Assert.Null(chat.FindName("MicrophoneButton"));
        Assert.Null(chat.FindName("CheckButton"));
        chat.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(chat.ActualWidth), (int)Math.Ceiling(chat.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(chat);
        string directory = Path.Combine(Path.GetTempPath(), "pindo-utility-qa");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, "composer.png"));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
    });

    [Fact]
    public void ReviewWindowCancelAndRunAreDistinct() => Sta(() =>
    {
        foreach (bool run in new[] { false, true })
        {
            var review = new ToolReviewWindow("ZIP preview", "Creates lesson.zip from the selected lesson.txt", true);
            review.Loaded += (_, _) => review.Dispatcher.BeginInvoke(() =>
            {
                var panel = (DockPanel)review.Content;
                var buttons = panel.Children.OfType<StackPanel>().Single();
                Button button = buttons.Children.OfType<Button>().Single(item => (string)item.Content == (run ? "Run" : "Cancel"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            Assert.Equal(run, review.ShowDialog());
        }
    });
}
