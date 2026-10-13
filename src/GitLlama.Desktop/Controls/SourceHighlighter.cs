using System.Xml;
using System.Xml.Linq;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace GitLlama.Desktop.Controls;

internal sealed record SourceSpan(int Start, int Length, string Color);
internal sealed record SourceLine(string Text, IReadOnlyList<SourceSpan> Spans);

internal static class SourceHighlighter
{
    private static readonly Lazy<IHighlightingDefinition> CSharp = new(CreateCSharp);
    internal static SourceLine[] Highlight(string source, string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var manager = HighlightingManager.Instance;
        var definition = extension == ".cs" ? CSharp.Value : extension is ".axaml" or ".xaml" or ".csproj" or ".props" or ".targets" ? manager.GetDefinition("XML") : manager.GetDefinitionByExtension(extension);
        var document = new TextDocument(source.Replace("\r\n", "\n"));
        using var highlighter = definition is null ? null : new DocumentHighlighter(document, definition);
        var result = new SourceLine[document.LineCount];
        for (var number = 1; number <= document.LineCount; number++)
        {
            var line = document.GetLineByNumber(number);
            var text = document.GetText(line.Offset, line.Length);
            var sections = highlighter?.HighlightLine(number).Sections;
            var spans = new List<SourceSpan>();
            if (sections is not null)
            {
                var points = sections.SelectMany(section => new[] { section.Offset - line.Offset, section.Offset + section.Length - line.Offset }).Append(0).Append(line.Length).Distinct().Order().ToArray();
                for (var i = 0; i + 1 < points.Length; i++)
                {
                    var start = points[i]; var length = points[i + 1] - start;
                    var section = sections.LastOrDefault(section => section.Offset <= line.Offset + start && section.Offset + section.Length >= line.Offset + start + length && section.Color.Foreground is not null);
                    if (section is not null && length > 0) spans.Add(new(start, length, DarkColor(section.Color)));
                }
            }
            result[number - 1] = new(text, spans);
        }
        return result;
    }
    private static string DarkColor(HighlightingColor color)
    {
        var name = (color.Name ?? "").ToLowerInvariant();
        if (name.Contains("comment")) return "#8EAD70";
        if (name.Contains("string") || name.Contains("char") || name.Contains("attributevalue")) return "#D69D85";
        if (name.Contains("number") || name.Contains("digit")) return "#B5CEA8";
        if (name.Contains("method") || name.Contains("function")) return "#DCDCAA";
        if (name.Contains("typename") || name.Contains("tag") || name.Contains("element")) return "#4EC9B0";
        if (name.Contains("goto") || name.Contains("exception") || name == "keywords") return "#C586C0";
        if (name.Contains("preprocessor")) return "#B6B3CC";
        if (name.Contains("keyword") || name.Contains("visibility") || name.Contains("modifier") || name.Contains("namespace") || name.Contains("getset")) return "#80B8E0";
        var original = color.Foreground?.GetColor(null);
        if (original is { } rgb)
        {
            if (rgb.G > rgb.R * 1.2 && rgb.G > rgb.B * 1.2) return "#8EAD70";
            if (rgb.R > rgb.B * 1.2) return "#D69D85";
        }
        return "#80B8E0";
    }
    private static IHighlightingDefinition CreateCSharp()
    {
        var assembly = typeof(HighlightingManager).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(name => name.EndsWith("CSharp-Mode.xshd", StringComparison.Ordinal)))!;
        var xml = XDocument.Load(stream); XNamespace ns = xml.Root!.Name.Namespace;
        var rules = xml.Root.Elements(ns + "RuleSet").Single(rule => rule.Attribute("name") is null);
        xml.Root.AddFirst(new XElement(ns + "Color", new XAttribute("name", "TypeName"), new XAttribute("foreground", "Teal")));
        // Extend the library's C# grammar for modern contextual keywords and raw strings.
        rules.AddFirst(new XElement(ns + "Rule", new XAttribute("color", "ContextKeywords"), @"\b(?:record|required|init|file|scoped|nint|nuint|with|not|and|or)\b"));
        rules.Add(new XElement(ns + "Rule", new XAttribute("color", "TypeName"), @"\b[A-Z][A-Za-z0-9_]*\b"));
        rules.AddFirst(new XElement(ns + "Span", new XAttribute("color", "String"), new XAttribute("multiline", "true"), new XElement(ns + "Begin", "\"{3,}"), new XElement(ns + "End", "\"{3,}")));
        using var reader = xml.CreateReader();
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
