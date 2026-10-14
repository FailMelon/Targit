using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitLlama.Desktop.Views;
using Xunit;

namespace GitLlama.Desktop.Tests;

public sealed class ConfirmDialogTests
{
    [AvaloniaTheory]
    [InlineData("Merge", true)]
    [InlineData("Cancel", false)]
    public async Task ReturnsWhetherTheActionWasConfirmed(string click, bool expected)
    {
        var owner = new Window();
        owner.Show();
        try
        {
            var result = ConfirmDialog.ShowAsync(owner, "Merge feature into main?", "Merges feature.", "Merge");
            Dispatcher.UIThread.RunJobs();
            var dialog = owner.OwnedWindows.Single();
            var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.Equal(["Cancel", "Merge"], buttons.Select(button => button.Content as string));
            Assert.True(buttons[1].IsDefault);

            buttons
                .Single(button => (string?)button.Content == click)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(expected, await result);
        }
        finally
        {
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task DestructiveActionsDoNotConfirmOnEnter()
    {
        var owner = new Window();
        owner.Show();
        try
        {
            var result = ConfirmDialog.ShowAsync(
                owner,
                "Abort?",
                "Discards changes.",
                "Abort",
                "No",
                destructive: true
            );
            Dispatcher.UIThread.RunJobs();
            var dialog = owner.OwnedWindows.Single();
            Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Button>(), button => button.IsDefault);
            dialog.Close();
            Assert.False(await result);
        }
        finally
        {
            owner.Close();
        }
    }
}
