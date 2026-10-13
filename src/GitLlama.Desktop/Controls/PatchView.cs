using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using GitLlama.Git;

namespace GitLlama.Desktop.Controls;

public sealed class PatchView : UserControl
{
    public static readonly StyledProperty<string> PatchProperty = AvaloniaProperty.Register<PatchView, string>(nameof(Patch), "");
    public static readonly StyledProperty<string> FilePathProperty = AvaloniaProperty.Register<PatchView, string>(nameof(FilePath), "");
    public static readonly StyledProperty<GitPatchLine?> SelectedLineProperty = AvaloniaProperty.Register<PatchView, GitPatchLine?>(nameof(SelectedLine), defaultBindingMode: BindingMode.TwoWay);
    private readonly ListBox _lines;
    private IReadOnlyDictionary<int, SourceLine> _highlightedLines = new Dictionary<int, SourceLine>();
    public string Patch { get => GetValue(PatchProperty); set => SetValue(PatchProperty, value); }
    public string FilePath { get => GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }
    public GitPatchLine? SelectedLine { get => GetValue(SelectedLineProperty); set => SetValue(SelectedLineProperty, value); }
    public PatchView()
    {
        _lines = new ListBox
        {
            FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"), FontSize = 13,
            ItemTemplate = new FuncDataTemplate<GitPatchLine>((line, _) =>
            {
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("42,42,20,*"), ColumnSpacing = 8, Height = 22 };
                var oldNumber = new TextBlock { Text = line?.OldNumber?.ToString() ?? "", Foreground = Brush.Parse("#80848E"), TextAlignment = TextAlignment.Right, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                var newNumber = new TextBlock { Text = line?.NewNumber?.ToString() ?? "", Foreground = Brush.Parse("#80848E"), TextAlignment = TextAlignment.Right, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                var sign = new TextBlock { Text = line?.Kind == "Added" ? "+" : line?.Kind == "Deleted" ? "−" : "", Foreground = Brush.Parse(line?.Kind == "Deleted" ? "#F28B91" : "#85DAA0"), TextAlignment = TextAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                var body = line?.Text ?? "";
                if (line?.Kind is "Added" or "Deleted" or "Context" && body.Length > 0 && body[0] is '+' or '-' or ' ') body = body[1..];
                var code = CodeView.Highlight(_highlightedLines.TryGetValue(line?.Index ?? -1, out var highlighted) ? highlighted : new SourceLine(body, []));
                code.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                if (line?.Kind is "Hunk" or "Metadata") code.Foreground = Brush.Parse("#AAB1BC");
                grid.Children.Add(oldNumber); Grid.SetColumn(newNumber, 1); grid.Children.Add(newNumber); Grid.SetColumn(sign, 2); grid.Children.Add(sign); Grid.SetColumn(code, 3); grid.Children.Add(code);
                return new Border { Background = Brush.Parse(line?.Kind == "Added" ? "#263C2C" : line?.Kind == "Deleted" ? "#422A2D" : line?.Kind == "Hunk" ? "#292E37" : "#1E1F22"), Padding = new Thickness(8,0), Child = grid };
            })
        };
        CodeView.CompactRows(_lines);
        ScrollViewer.SetHorizontalScrollBarVisibility(_lines, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        _lines.SelectionChanged += (_, _) => SetCurrentValue(SelectedLineProperty, _lines.SelectedItem as GitPatchLine);
        _lines.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control) && _lines.SelectedItem is GitPatchLine line && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            { await clipboard.SetTextAsync(line.Text); e.Handled = true; }
        };
        Content = _lines;
    }
    internal static IReadOnlyList<GitPatchLine> DisplayLines(string patch)
    {
        var lines = GitPartialStageService.Lines(patch);
        var code = lines.Where(line => line.Kind != "Metadata").ToArray();
        if (code.Length > 0) return code;
        var message = patch.Contains("diff --git", StringComparison.Ordinal) ? (patch.Contains("Binary", StringComparison.OrdinalIgnoreCase) ? "Binary files differ; no text preview is available." : "No text changes to display.") : patch.Trim();
        return string.IsNullOrEmpty(message) ? [] : [new GitPatchLine(0, message, "Metadata", "")];
    }
    internal static IReadOnlyDictionary<int, SourceLine> HighlightDiff(IReadOnlyList<GitPatchLine> lines, string path)
    {
        var result = new Dictionary<int, SourceLine>();
        foreach (var hunk in lines.Where(line => !string.IsNullOrEmpty(line.Hunk)).GroupBy(line => line.Hunk))
        {
            // Deleted lines belong to the old document; additions belong to the new document.
            // Keep their multiline parser states separate so one side cannot color the other.
            foreach (var side in new[] { "Deleted", "Added" })
            {
                var source = hunk.Where(line => line.Kind == "Context" || line.Kind == side).ToArray();
                if (source.Length == 0) continue;
                var highlighted = SourceHighlighter.Highlight(string.Join("\n", source.Select(line => line.Text.Length > 0 ? line.Text[1..] : "")), path);
                for (var i = 0; i < source.Length; i++) result[source[i].Index] = highlighted[i];
            }
        }
        return result;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_lines is null) return;
        if (change.Property == PatchProperty || change.Property == FilePathProperty)
        {
            var lines = DisplayLines(Patch ?? "");
            _highlightedLines = HighlightDiff(lines, FilePath);
            _lines.ItemsSource = lines;
        }
        else if (change.Property == SelectedLineProperty && SelectedLine is { } selected)
            _lines.SelectedItem = _lines.ItemsSource?.Cast<GitPatchLine>().FirstOrDefault(line => line.Index == selected.Index && line.Text == selected.Text);
    }
}
