using Avalonia.Controls;
using Avalonia.Threading;
using GitLlama.Desktop.Docking;
using GitLlama.Desktop.ViewModels;
using System.ComponentModel;

namespace GitLlama.Desktop.Views;

public sealed class ContextCenterView : ContentControl
{
    private MainWindowViewModel? _workspace;
    private readonly Dictionary<string, Control> _views = [];
    private string? _visibleContext;
    private bool _renderPending;
    public ContextCenterView()
    {
        DataContextChanged += (_, _) => Connect();
        AttachedToVisualTree += (_, _) => Connect();
        DetachedFromVisualTree += (_, _) => { if (_workspace is not null) _workspace.PropertyChanged -= Changed; };
    }
    private void Connect()
    {
        if (_workspace is not null) _workspace.PropertyChanged -= Changed;
        if (!ReferenceEquals(_workspace, DataContext)) { _views.Clear(); _visibleContext = null; }
        _workspace = DataContext as MainWindowViewModel;
        if (_workspace is not null) _workspace.PropertyChanged += Changed;
        RenderContext();
    }
    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainWindowViewModel.CenterSection) or nameof(MainWindowViewModel.IsFileReview) or nameof(MainWindowViewModel.IsUpdatingContextSnapshot))
        {
            if (_renderPending || _workspace?.IsUpdatingContextSnapshot != false) return;
            _renderPending = true;
            Dispatcher.UIThread.Post(() => { _renderPending = false; RenderContext(); });
        }
    }
    private void RenderContext()
    {
        if (_workspace is null || (_workspace.IsUpdatingContextSnapshot && _visibleContext is not null)) return;
        var context = _workspace.IsFileReview ? "FileReview" : _workspace.CenterSection;
        if (_visibleContext == context) return;
        if (!_views.TryGetValue(context, out var view))
        {
            view = context == "FileReview" ? new FileReviewView() : context == "History" ? new HistoryView() :
                new DockContentTemplate(_workspace).Build(new Dock.Model.Mvvm.Controls.Document { Id = context.ToLowerInvariant(), Title = context });
            if (view is null) return;
            view.DataContext = _workspace;
            _views[context] = view;
        }
        _visibleContext = context;
        Content = view;
    }
}
