using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GitLlama.Git;

namespace GitLlama.Desktop.Views;

public static class SshPromptDialog
{
    private static Window Dialog(string title, out StackPanel content)
    {
        content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 14 };
        return new Window { Title = title, Width = 540, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#313338")), Content = content };
    }
    public static async Task<string?> UnlockAsync(Window owner, SshUnlockRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dialog = Dialog("Unlock SSH key", out var content);
        content.Children.Add(new TextBlock { Text = "Unlock SSH key", FontSize = 22, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock { Text = Path.GetFileName(request.KeyPath), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = request.Retry ? "That passphrase did not unlock the key. Try again." : "Enter this key's passphrase. The unlocked key stays in memory until you lock SSH, switch profiles, or close GitLlama.", TextWrapping = TextWrapping.Wrap });
        var password = new TextBox { Name = "SshPassphraseInput", PasswordChar = '●', Watermark = "Key passphrase" };
        content.Children.Add(password);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var cancel = new Button { Content = "Cancel" }; var unlock = new Button { Content = "Unlock", Classes = { "workspace", "primary" } };
        cancel.Click += (_, _) => dialog.Close(null);
        unlock.Click += (_, _) => { var result = password.Text ?? ""; password.Text = ""; dialog.Close(result); };
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(null); if (e.Key == Key.Enter) unlock.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); };
        dialog.Opened += (_, _) => password.Focus();
        dialog.Closed += (_, _) => password.Text = "";
        buttons.Children.Add(cancel); buttons.Children.Add(unlock); content.Children.Add(buttons);
        return await ShowAsync<string?>(dialog, owner, ct);
    }
    public static async Task<bool> TrustHostAsync(Window owner, SshHostTrustRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dialog = Dialog("Verify SSH server", out var content);
        content.Children.Add(new TextBlock { Text = request.PreviousFingerprint is null ? "Verify SSH server" : "SSH server fingerprint changed", FontSize = 22, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock { Text = $"{request.Host}:{request.Port} · {request.Algorithm}", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = "Compare this fingerprint with your provider's published fingerprint before trusting this server.", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBox { Text = request.Fingerprint, IsReadOnly = true });
        if (request.PreviousFingerprint is { } previous) content.Children.Add(new TextBlock { Text = "Previously trusted: " + previous + "\nOnly replace it if you have verified the change with your provider.", TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var reject = new Button { Content = "Cancel" }; var trust = new Button { Content = "Trust server", Classes = { "workspace", "primary" } };
        reject.Click += (_, _) => dialog.Close(false); trust.Click += (_, _) => dialog.Close(true);
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(false); };
        buttons.Children.Add(reject); buttons.Children.Add(trust); content.Children.Add(buttons);
        return await ShowAsync<bool>(dialog, owner, ct);
    }
    private static async Task<T> ShowAsync<T>(Window dialog, Window owner, CancellationToken ct)
    {
        var opacity = owner.Opacity;
        var backdrop = owner is MainWindow main ? main.FindControl<Border>("SshPromptBackdrop") : null;
        if (backdrop is not null) backdrop.IsVisible = true;
        else owner.Opacity = 0.65;
        try
        {
            var result = dialog.ShowDialog<T>(owner);
            using var cancel = ct.Register(() => Dispatcher.UIThread.Post(() => dialog.Close()));
            return await result;
        }
        finally { if (backdrop is not null) backdrop.IsVisible = false; else owner.Opacity = opacity; }
    }
}
