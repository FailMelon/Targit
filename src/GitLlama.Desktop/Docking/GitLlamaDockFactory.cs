using System;
using System.Collections.Generic;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace GitLlama.Desktop.Docking;

/// <summary>
/// Creates a Visual Studio-style workspace with draggable document and tool docks.
/// Stable dockable IDs are used to support workspace layout restoration.
/// </summary>
public sealed class GitLlamaDockFactory : Factory
{
    public override IRootDock CreateLayout()
    {
        var history = new Document { Id = "history", Title = "History" };
        var repositories = new Tool { Id = "repositories", Title = "Repository", CanClose = false, CanFloat = false };
        var inspector = new Tool { Id = "inspector", Title = "Inspector", CanClose = false, CanFloat = false };

        var left = new ToolDock
        {
            Id = "left-tools",
            Title = "Navigation",
            Alignment = Alignment.Left,
            Proportion = 0.18,
            ActiveDockable = repositories,
            VisibleDockables = CreateList<IDockable>(repositories)
        };
        var documents = new DocumentDock
        {
            Id = "documents",
            Title = "Documents",
            Proportion = 1.0,
            CanCloseLastDockable = false,
            ActiveDockable = history,
            VisibleDockables = CreateList<IDockable>(history)
        };
        var right = new ToolDock
        {
            Id = "right-tools",
            Title = "Inspector",
            Alignment = Alignment.Right,
            Proportion = 0.29,
            ActiveDockable = inspector,
            VisibleDockables = CreateList<IDockable>(inspector)
        };
        var documentWorkspace = new ProportionalDock
        {
            Id = "document-workspace",
            Orientation = Orientation.Vertical,
            Proportion = 0.53,
            VisibleDockables = CreateList<IDockable>(documents)
        };
        var main = new ProportionalDock
        {
            Id = "main",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(left, new ProportionalDockSplitter(), documentWorkspace, new ProportionalDockSplitter(), right)
        };
        var root = CreateRootDock();
        root.Id = "root-v3";
        root.Title = "GitLlama";
        root.ActiveDockable = main;
        root.DefaultDockable = main;
        root.VisibleDockables = CreateList<IDockable>(main);
        return root;
    }

    public IRootDock CreatePreset(string name)
    {
        var root = CreateLayout();
        if (root.ActiveDockable is not ProportionalDock main || main.VisibleDockables is not { } regions) return root;
        var left = regions.OfType<ToolDock>().First(d => d.Id == "left-tools");
        var right = regions.OfType<ToolDock>().First(d => d.Id == "right-tools");
        var workspace = regions.OfType<ProportionalDock>().First(d => d.Id == "document-workspace");
        var documents = workspace.VisibleDockables!.OfType<DocumentDock>().First();
        if (name == "Review") { left.Proportion = .15; workspace.Proportion = .55; right.Proportion = .30; }
        if (name == "Merge Resolution")
        {
            left.Proportion = .15; workspace.Proportion = .60; right.Proportion = .25;

        }
        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>();
        DockableLocator = new Dictionary<string, Func<IDockable?>>();
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };
        base.InitLayout(layout);
    }
}
