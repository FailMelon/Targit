using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Views;

internal static class TagCreationDialog
{
    internal static async Task ShowAsync(Control source, MainWindowViewModel vm, string reference)
    {
        if (TopLevel.GetTopLevel(source) is not Window owner) return;
        var repository = vm.SelectedRepository?.Path;
        var name = new TextBox { Watermark = "Tag name" };
        var message = new TextBox { Watermark = "Tag message", AcceptsReturn = true, MinHeight = 80 };
        var author = new TextBox { Watermark = "Author name", Text = vm.AuthorName };
        var email = new TextBox { Watermark = "Author email", Text = vm.AuthorEmail };
        var feedback = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var create = new Button { Content = "Create annotated tag", HorizontalAlignment = HorizontalAlignment.Right };
        create.Classes.Add("workspace");
        var dialog = new Window { Title = "Create annotated tag", Width = 500, Height = 370, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 10, Children = { new TextBlock { Text = "Target: " + reference, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }, name, message, author, email, feedback, create } };
        create.Click += async (_, _) =>
        {
            if (vm.SelectedRepository?.Path != repository) { feedback.Text = "Repository changed. Close and review the action again."; return; }
            if (new[] { name.Text, message.Text, author.Text, email.Text }.Any(string.IsNullOrWhiteSpace)) { feedback.Text = "Enter a name, message and author identity."; return; }
            vm.AnnotatedTagName = name.Text!; vm.AnnotatedTagMessage = message.Text!;
            vm.AnnotatedTagAuthorName = author.Text!; vm.AnnotatedTagAuthorEmail = email.Text!;
            vm.ContextTagTarget = reference;
            dialog.Close(); await vm.CreateContextAnnotatedTagCommand.ExecuteAsync(null);
        };
        await dialog.ShowDialog(owner);
    }
}
