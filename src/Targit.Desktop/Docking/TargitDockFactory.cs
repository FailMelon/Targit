using System;
using System.Collections.Generic;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace Targit.Desktop.Docking;

/// <summary>
/// Creates a Visual Studio-style workspace with draggable document and tool docks.
/// Stable dockable IDs are used to support workspace layout restoration.
/// </summary>
public sealed class TargitDockFactory : Factory
{
    public override IRootDock CreateLayout()
    {
        var history = new Document { Id = "history", Title = "History" };
        var changes = new Document { Id = "changes", Title = "Changes" };
        var branches = new Document { Id = "branches", Title = "Branches" };
        var repositories = new Tool { Id = "repositories", Title = "Repositories" };
        var inspector = new Tool { Id = "inspector", Title = "Inspector" };
        var output = new Tool { Id = "output", Title = "Output" };

        var left = new ToolDock
        {
            Id = "left-tools",
            Title = "Navigation",
            Alignment = Alignment.Left,
            Proportion = 0.22,
            ActiveDockable = repositories,
            VisibleDockables = CreateList<IDockable>(repositories)
        };
        var documents = new DocumentDock
        {
            Id = "documents",
            Title = "Documents",
            Proportion = 0.56,
            ActiveDockable = history,
            VisibleDockables = CreateList<IDockable>(history, changes, branches)
        };
        var right = new ToolDock
        {
            Id = "right-tools",
            Title = "Inspector",
            Alignment = Alignment.Right,
            Proportion = 0.22,
            ActiveDockable = inspector,
            VisibleDockables = CreateList<IDockable>(inspector)
        };
        var center = new ProportionalDock
        {
            Id = "center",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                left, new ProportionalDockSplitter(), documents,
                new ProportionalDockSplitter(), right)
        };
        var bottom = new ToolDock
        {
            Id = "bottom-tools",
            Title = "Output",
            Alignment = Alignment.Bottom,
            Proportion = 0.2,
            ActiveDockable = output,
            VisibleDockables = CreateList<IDockable>(output)
        };
        var main = new ProportionalDock
        {
            Id = "main",
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(center, new ProportionalDockSplitter(), bottom)
        };
        var root = CreateRootDock();
        root.Id = "root";
        root.Title = "Targit";
        root.ActiveDockable = main;
        root.DefaultDockable = main;
        root.VisibleDockables = CreateList<IDockable>(main);
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
