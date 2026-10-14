using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitLlama.Desktop.Views;
using GitLlama.Git;
using Xunit;

namespace GitLlama.Desktop.Tests;

public sealed class SshPromptTests
{
    [AvaloniaFact]
    public async Task UnlockMasksAndClearsPassphraseAndRestoresBackground()
    {
        var owner = new Window();
        owner.Show();
        try
        {
            var result = SshPromptDialog.UnlockAsync(owner, new("test.ppk", false), default);
            Dispatcher.UIThread.RunJobs();
            var dialog = Assert.Single(owner.OwnedWindows);
            var input = dialog.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.NotEqual('\0', input.PasswordChar);
            Assert.Equal(0.65, owner.Opacity);
            input.Text = "disposable-passphrase";
            dialog
                .GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "Unlock"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("disposable-passphrase", await result);
            Assert.Equal("", input.Text);
            Assert.Equal(1, owner.Opacity);
        }
        finally
        {
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task CancelingUnlockReturnsNoPassphrase()
    {
        var owner = new Window();
        owner.Show();
        using var cancel = new CancellationTokenSource();
        try
        {
            var result = SshPromptDialog.UnlockAsync(owner, new("test.ppk", true), cancel.Token);
            Dispatcher.UIThread.RunJobs();
            cancel.Cancel();
            Dispatcher.UIThread.RunJobs();
            Assert.Null(await result);
            Assert.Equal(1, owner.Opacity);
        }
        finally
        {
            owner.Close();
        }
    }

    [AvaloniaFact]
    public async Task ChangedHostRequiresExplicitTrust()
    {
        var owner = new Window();
        owner.Show();
        try
        {
            var result = SshPromptDialog.TrustHostAsync(
                owner,
                new("host", 22, "ssh-ed25519", "SHA256:new", "SHA256:previous"),
                default
            );
            Dispatcher.UIThread.RunJobs();
            var dialog = Assert.Single(owner.OwnedWindows);
            Assert.Equal(
                "SSH server fingerprint changed",
                dialog.GetVisualDescendants().OfType<TextBlock>().First().Text
            );
            dialog.Close(false);
            Assert.False(await result);
            Assert.Equal(1, owner.Opacity);
        }
        finally
        {
            owner.Close();
        }
    }
}
