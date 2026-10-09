using System.Reflection;
using System.Windows.Controls;
using Pointly.App.Presentation;

namespace Pointly.Tests;

public sealed class ChatBusyTests
{
    [Fact]
    public void BusyRejectsDuplicateSubmissionAndRestoresInput()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var chat = new ChatWindow();
                int submitted = 0;
                chat.QuestionSubmitted += _ => submitted++;
                var input = (TextBox)chat.FindName("Question");
                MethodInfo busy = typeof(ChatWindow).GetMethod("SetBusy", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MethodInfo submit = typeof(ChatWindow).GetMethod("Submit", BindingFlags.Instance | BindingFlags.NonPublic)!;
                input.Text = "Insert a table";
                busy.Invoke(chat, [true]);
                submit.Invoke(chat, null);
                Assert.Equal(0, submitted);
                Assert.False(input.IsEnabled);
                Assert.True(((Button)chat.FindName("CancelButton")).IsEnabled);
                busy.Invoke(chat, [false]);
                Assert.True(input.IsEnabled);
                Assert.Equal("Insert a table", input.Text);
                submit.Invoke(chat, null);
                Assert.Equal(1, submitted);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) throw failure;
    }
}
