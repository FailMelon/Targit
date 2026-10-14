using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Controls;

/// <summary>Which pane of the conflict resolver a <see cref="ConflictPaneView"/> shows.</summary>
public enum ConflictPane
{
    Ours,
    Theirs,
    Output,
}

/// <summary>
/// A syntax-highlighted, line-numbered view of one resolver pane. Conflict lines are tinted by side, and the A and
/// B panes show a checkbox at the start of each conflict to choose its lines.
/// </summary>
public sealed class ConflictPaneView : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<ConflictLine>?> LinesProperty = AvaloniaProperty.Register<
        ConflictPaneView,
        IReadOnlyList<ConflictLine>?
    >(nameof(Lines));
    public static readonly StyledProperty<string> FilePathProperty = AvaloniaProperty.Register<
        ConflictPaneView,
        string
    >(nameof(FilePath), "");
    public static readonly StyledProperty<ConflictPane> PaneProperty = AvaloniaProperty.Register<
        ConflictPaneView,
        ConflictPane
    >(nameof(Pane));

    // Blue (the app accent) for A, violet for B and amber for an unresolved conflict, each with a stronger bar at
    // the edge. Green and red are left to mean added and removed lines, as in diffs.
    private static readonly IBrush OursBackground = Brush.Parse("#1D2735");
    private static readonly IBrush OursSelected = Brush.Parse("#2A4060");
    private static readonly IBrush OursBar = Brush.Parse("#7F9FC9");
    private static readonly IBrush TheirsBackground = Brush.Parse("#272135");
    private static readonly IBrush TheirsSelected = Brush.Parse("#3E3060");
    private static readonly IBrush TheirsBar = Brush.Parse("#B48EDB");
    private static readonly IBrush UnresolvedBackground = Brush.Parse("#433820");
    private static readonly IBrush UnresolvedBar = Brush.Parse("#E5C07B");
    private static readonly IBrush NumberBrush = Brush.Parse("#80848E");
    private static readonly IBrush PlaceholderBrush = Brush.Parse("#B5BAC1");

    private readonly ListBox _list;

    private sealed record Row(ConflictLine Line, SourceLine Source, ConflictPane Pane);

    public ConflictPaneView()
    {
        _list = new ListBox
        {
            Background = Brushes.Transparent,
            FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"),
            FontSize = 13,
            ItemTemplate = new FuncDataTemplate<Row>((row, _) => row is null ? new Panel() : Build(row)),
        };
        CodeView.CompactRows(_list);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        Content = _list;
    }

    public IReadOnlyList<ConflictLine>? Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }
    public string FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }
    public ConflictPane Pane
    {
        get => GetValue(PaneProperty);
        set => SetValue(PaneProperty, value);
    }

    /// <summary>Scrolls so the first line of a conflict is in view.</summary>
    public void ScrollTo(ConflictChunk chunk)
    {
        if (_list.ItemsSource is not IReadOnlyList<Row> rows)
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i].Line.Chunk, chunk))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        // Bring a few lines of context above the conflict into view as well.
        _list.ScrollIntoView(Math.Min(rows.Count - 1, index + 6));
        _list.ScrollIntoView(Math.Max(0, index - 3));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinesProperty || change.Property == FilePathProperty || change.Property == PaneProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var lines = Lines ?? [];
        // Highlight the real lines as one document so multi-line comments and strings colour correctly.
        var content = lines.Where(line => !line.IsPlaceholder).ToArray();
        var highlighted = SourceHighlighter.Highlight(string.Join("\n", content.Select(line => line.Text)), FilePath);
        var next = 0;
        _list.ItemsSource = lines
            .Select(line =>
                line.IsPlaceholder || next >= highlighted.Length
                    ? new Row(line, new SourceLine(line.Text, []), Pane)
                    : new Row(line, highlighted[next++], Pane)
            )
            .ToArray();
    }

    private static Control Build(Row row)
    {
        var line = row.Line;
        var (background, bar) = line.Kind switch
        {
            ConflictLineKind.Ours => (OursBackground, OursBar),
            ConflictLineKind.Theirs => (TheirsBackground, TheirsBar),
            ConflictLineKind.Unresolved => (UnresolvedBackground, UnresolvedBar),
            _ => ((IBrush)Brushes.Transparent, (IBrush)Brushes.Transparent),
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("3,22,22,46,*"),
            ColumnSpacing = 4,
            Height = 22,
            Background = background,
        };
        grid.Children.Add(new Border { Background = bar });
        var side = row.Pane == ConflictPane.Ours ? "A" : "B";

        // The first line of a conflict holds the hunk checkbox, which keeps or drops all of its lines.
        if (row.Pane != ConflictPane.Output && line.StartsChunk && line.Chunk is { } chunk)
        {
            grid.Children.Add(
                Check(
                    row.Pane == ConflictPane.Ours ? nameof(ConflictChunk.TakeOurs) : nameof(ConflictChunk.TakeTheirs),
                    chunk,
                    $"Keep every line of this conflict from {side}",
                    column: 1
                )
            );
        }

        // Hovering a conflict line shows + to keep just that line, or − to drop it again; kept lines are tinted
        // more strongly.
        if (row.Pane != ConflictPane.Output && line.Choice is { } choice)
        {
            var toggle = new Button
            {
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                Width = 18,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = bar,
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            toggle.Bind(
                ContentControl.ContentProperty,
                new Binding(nameof(ConflictLineChoice.IsSelected))
                {
                    Source = choice,
                    Converter = new FuncValueConverter<bool, string>(isSelected => isSelected ? "−" : "+"),
                }
            );
            toggle.Bind(
                ToolTip.TipProperty,
                new Binding(nameof(ConflictLineChoice.IsSelected))
                {
                    Source = choice,
                    Converter = new FuncValueConverter<bool, string>(isSelected =>
                        isSelected ? "Drop this line from the output" : $"Keep just this line from {side}"
                    ),
                }
            );
            toggle.Click += (_, _) => choice.IsSelected = !choice.IsSelected;
            grid.PointerEntered += (_, _) => toggle.Opacity = 1;
            grid.PointerExited += (_, _) => toggle.Opacity = 0;
            Grid.SetColumn(toggle, 2);
            grid.Children.Add(toggle);
            var selected = row.Pane == ConflictPane.Ours ? OursSelected : TheirsSelected;
            grid.Bind(
                Panel.BackgroundProperty,
                new Binding(nameof(ConflictLineChoice.IsSelected))
                {
                    Source = choice,
                    Converter = new FuncValueConverter<bool, IBrush>(isSelected => isSelected ? selected : background),
                }
            );
        }

        var number = new TextBlock
        {
            Text = line.Number?.ToString() ?? "",
            Foreground = NumberBrush,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(number, 3);
        grid.Children.Add(number);

        var code = line.IsPlaceholder
            ? new TextBlock
            {
                Text = line.Text,
                Foreground = PlaceholderBrush,
                FontStyle = FontStyle.Italic,
                FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"),
                FontSize = 13,
            }
            : CodeView.Highlight(row.Source);
        code.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(code, 4);
        grid.Children.Add(code);
        return grid;
    }

    private static Control Check(string property, object source, string tip, int column)
    {
        var check = new CheckBox
        {
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        check.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding(property) { Source = source, Mode = BindingMode.TwoWay }
        );
        // Scale the standard checkbox down so it fits the compact code rows.
        var box = new Viewbox
        {
            Height = 24,
            Child = check,
            VerticalAlignment = VerticalAlignment.Center,
            [ToolTip.TipProperty] = tip,
        };
        Grid.SetColumn(box, column);
        return box;
    }
}
