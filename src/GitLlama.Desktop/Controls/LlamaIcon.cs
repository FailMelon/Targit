using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace GitLlama.Desktop.Controls;

/// <summary>Draws the supplied SVG paths directly so the brand stays crisp at any display scale.</summary>
public sealed class LlamaIcon : Control
{
    private static readonly Lazy<IReadOnlyList<(Geometry Shape, IBrush Fill)>> Artwork = new(LoadArtwork);
    public LlamaIcon() { Width = Height = 24; }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var size = Math.Min(Bounds.Width, Bounds.Height);
        using var transform = context.PushTransform(Matrix.CreateScale(size / 512, size / 512) * Matrix.CreateTranslation((Bounds.Width - size) / 2, (Bounds.Height - size) / 2));
        foreach (var (shape, fill) in Artwork.Value) context.DrawGeometry(fill, null, shape);
    }
    private static IReadOnlyList<(Geometry, IBrush)> LoadArtwork()
    {
        using var stream = AssetLoader.Open(new Uri("avares://GitLlama/Assets/gitllama.svg"));
        var svg = XDocument.Load(stream);
        var shapes = new List<(Geometry, IBrush)>();
        foreach (var element in svg.Descendants().Where(element => element.Name.LocalName is "path" or "rect"))
        {
            var fill = element.Attribute("fill")?.Value ?? element.Attribute("style")?.Value.Split(';').Select(value => value.Trim()).FirstOrDefault(value => value.StartsWith("fill:", StringComparison.Ordinal))?[5..] ?? "#000000";
            if (fill == "none") continue;
            var shape = element.Name.LocalName == "path" ? (Geometry)StreamGeometry.Parse(element.Attribute("d")!.Value) : new RectangleGeometry(new Rect(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height")));
            shapes.Add((shape, Brush.Parse(fill)));
        }
        return shapes;
    }
    private static double Number(XElement element, string name) => double.Parse(element.Attribute(name)?.Value ?? "0", CultureInfo.InvariantCulture);
}
