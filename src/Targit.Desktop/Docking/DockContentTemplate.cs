using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using Targit.Desktop.ViewModels;

namespace Targit.Desktop.Docking;

/// <summary>Maps stable dockable IDs to actual repository-aware content.</summary>
public sealed class DockContentTemplate(MainWindowViewModel workspace) : IDataTemplate
{
    public bool Match(object? data) => data is Tool or Document;

    public Control Build(object? data)
    {
        var id = data switch
        {
            Tool tool => tool.Id,
            Document document => document.Id,
            _ => null
        };
        var root = new StackPanel { Margin = new Thickness(12), Spacing = 10, DataContext = workspace };
        var heading = new TextBlock { Text = id?.ToUpperInvariant(), FontWeight = FontWeight.SemiBold };
        root.Children.Add(heading);
        switch (id)
        {
            case "repositories":
                var repositories = new ListBox { MinHeight = 100 };
                repositories.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(workspace.Repositories)));
                repositories.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(nameof(workspace.SelectedRepository)) { Mode = BindingMode.TwoWay });
                repositories.ItemTemplate = new FuncDataTemplate<Targit.Core.RepositoryInfo>((repo, _) =>
                    new TextBlock { Text = repo is null ? "" : (repo.IsFavorite ? "★  " : "    ") + repo.Name });
                root.Children.Add(repositories);
                break;
            case "history":
                AddList(root, nameof(workspace.CommitLines), nameof(workspace.SelectedCommitLine));
                break;
            case "changes":
                AddList(root, nameof(workspace.ChangeLines), nameof(workspace.SelectedChangeLine));
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var stage = new Button { Content = "Stage selected" };
                stage.Bind(Button.CommandProperty, new Binding(nameof(workspace.StageSelectedCommand)));
                var unstage = new Button { Content = "Unstage selected" };
                unstage.Bind(Button.CommandProperty, new Binding(nameof(workspace.UnstageSelectedCommand)));
                actions.Children.Add(stage);
                actions.Children.Add(unstage);
                root.Children.Add(actions);
                root.Children.Add(new TextBlock { Text = "Commit staged changes", FontWeight = FontWeight.SemiBold });
                var message = new TextBox { Watermark = "Commit message", AcceptsReturn = true, MinHeight = 60 };
                message.Bind(TextBox.TextProperty, new Binding(nameof(workspace.CommitMessage)) { Mode = BindingMode.TwoWay });
                root.Children.Add(message);
                var name = new TextBox { Watermark = "Author name" };
                name.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AuthorName)) { Mode = BindingMode.TwoWay });
                root.Children.Add(name);
                var email = new TextBox { Watermark = "Author email" };
                email.Bind(TextBox.TextProperty, new Binding(nameof(workspace.AuthorEmail)) { Mode = BindingMode.TwoWay });
                root.Children.Add(email);
                var commit = new Button { Content = "Commit staged" };
                commit.Bind(Button.CommandProperty, new Binding(nameof(workspace.CommitCommand)));
                root.Children.Add(commit);
                break;
            case "branches":
                AddList(root, nameof(workspace.BranchLines), nameof(workspace.SelectedBranchLine));
                break;
            case "inspector":
                var current = new TextBlock();
                current.TextWrapping = TextWrapping.Wrap;
                current.Bind(TextBlock.TextProperty, new Binding(nameof(workspace.InspectorText)));
                root.Children.Add(current);
                break;
            case "output":
                var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
                status.Bind(TextBlock.TextProperty, new Binding(nameof(workspace.Status)));
                root.Children.Add(status);
                break;
            default:
                root.Children.Add(new TextBlock { Text = "No content available." });
                break;
        }
        return new ScrollViewer { Content = root };
    }

    private static void AddList(StackPanel root, string property, string selectionProperty)
    {
        var list = new ListBox { MinHeight = 150 };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding(property));
        list.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(selectionProperty) { Mode = BindingMode.TwoWay });
        root.Children.Add(list);
    }
}