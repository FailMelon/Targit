using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GitLlama.Desktop.ViewModels;

namespace GitLlama.Desktop.Controls;

public sealed class GraphCell : Control
{
    public static readonly StyledProperty<HistoryRow?> RowProperty = AvaloniaProperty.Register<GraphCell, HistoryRow?>(nameof(Row));
    private static readonly IBrush[] Colors = [Brush.Parse("#E84C91"), Brush.Parse("#A66BFF"), Brush.Parse("#4EA1F3"), Brush.Parse("#E6C547"), Brush.Parse("#65CB85"), Brush.Parse("#4EC9B0")];
    public HistoryRow? Row { get => GetValue(RowProperty); set => SetValue(RowProperty, value); }
    static GraphCell() => AffectsRender<GraphCell>(RowProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Row is not { } row) return;
        double X(int lane) => 16 + lane * 18;
        foreach (var edge in row.Segments)
        {
            var start = new Point(X(edge.FromLane), edge.FromY * Bounds.Height);
            var end = new Point(X(edge.ToLane), edge.ToY * Bounds.Height);
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(start, false);
                var middle = (start.Y + end.Y) / 2;
                path.CubicBezierTo(new Point(start.X, middle), new Point(end.X, middle), end);
                path.EndFigure(false);
            }
            context.DrawGeometry(null, new Pen(Colors[edge.ColorLane % Colors.Length], 1.6), geometry);
        }
        var center = new Point(X(row.Lane), Bounds.Height / 2);
        if (row.IsCurrentBranchTip) context.DrawEllipse(null, new Pen(Brush.Parse("#8DAED5"), 2), center, 7.5, 7.5);
        context.DrawEllipse(Colors[row.Lane % Colors.Length], null, center, 4.5, 4.5);
    }
}
