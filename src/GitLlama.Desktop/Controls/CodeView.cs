using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Avalonia.Styling;


namespace GitLlama.Desktop.Controls;

public sealed class CodeView : UserControl
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<CodeView, string>(nameof(Text), "");
    public static readonly StyledProperty<string> FilePathProperty = AvaloniaProperty.Register<CodeView, string>(nameof(FilePath), "");
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string FilePath { get => GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }
    private readonly ListBox _lines;
    private sealed record CodeLine(int Number, SourceLine Source);
    public CodeView()
    {
        _lines = new ListBox { FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"), FontSize = 13, ItemTemplate = new FuncDataTemplate<CodeLine>((line, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("55,*"), ColumnSpacing = 12, Height = 22, Margin = new Thickness(8,0) };
            var number = new TextBlock { Text = line?.Number.ToString(), Foreground = Brush.Parse("#80848E"), TextAlignment = TextAlignment.Right, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var code = Highlight(line?.Source ?? new SourceLine("", [])); code.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            grid.Children.Add(number); Grid.SetColumn(code, 1); grid.Children.Add(code); return grid;
        }) };
        CompactRows(_lines);
        ScrollViewer.SetHorizontalScrollBarVisibility(_lines, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        Content = _lines;
    }
    internal static void CompactRows(ListBox list)
    {
        var style = new Style(selector => selector.OfType<ListBoxItem>());
        style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(ListBoxItem.MinHeightProperty, 22d));
        style.Setters.Add(new Setter(ListBoxItem.HeightProperty, 22d));
        style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Stretch));
        list.Styles.Add(style);
    }
    internal static TextBlock Highlight(string code, string filePath = "") => Highlight(SourceHighlighter.Highlight(code, filePath).First());
    internal static TextBlock Highlight(SourceLine line)
    {
        var text = new TextBlock { FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"), FontSize = 13, Foreground = Brush.Parse("#DBDEE1") };
        var offset = 0;
        foreach (var span in line.Spans)
        {
            if (span.Start > offset) text.Inlines!.Add(new Run(line.Text[offset..span.Start]));
            text.Inlines!.Add(new Run(line.Text.Substring(span.Start, span.Length)) { Foreground = Brush.Parse(span.Color) });
            offset = span.Start + span.Length;
        }
        if (offset < line.Text.Length) text.Inlines!.Add(new Run(line.Text[offset..]));
        return text;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_lines is not null && (change.Property == TextProperty || change.Property == FilePathProperty)) _lines.ItemsSource = SourceHighlighter.Highlight(Text ?? "", FilePath).Select((source, index) => new CodeLine(index + 1, source)).ToArray();
    }
}
