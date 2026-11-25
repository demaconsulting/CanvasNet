using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Shared helpers of the MathML tests: report collection, layout access, pixel checks and temporary files.
/// </summary>
internal static class MathMlTestHelpers
{
    /// <summary>The MathML namespace, for the texts of the tests.</summary>
    public const string Namespace = "http://www.w3.org/1998/Math/MathML";

    /// <summary>
    ///     Wraps content in a <c>math</c> element of the MathML namespace.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="attributes">Attributes of the <c>math</c> element, or empty.</param>
    /// <returns>The MathML text.</returns>
    public static string Math(string content, string attributes = "") =>
        $"<math xmlns=\"{Namespace}\"{(attributes.Length > 0 ? " " + attributes : string.Empty)}>{content}</math>";

    /// <summary>
    ///     Creates render options that collect every report.
    /// </summary>
    /// <param name="strict">Whether the first unsupported feature throws.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The options and the lists the reports go to.</returns>
    public static (MathMlRenderOptions Options, List<MathMlDiagnostic> Warnings, List<MathMlUnsupportedFeature> Unsupported) Collecting(bool strict = false, double fontSize = 16)
    {
        var warnings = new List<MathMlDiagnostic>();
        var unsupported = new List<MathMlUnsupportedFeature>();
        var options = new MathMlRenderOptions
        {
            FontSize = fontSize,
            WarningHandler = warnings.Add,
            UnsupportedFeatureHandler = unsupported.Add,
            ThrowOnUnsupportedFeature = strict,
        };
        return (options, warnings, unsupported);
    }

    /// <summary>
    ///     Reads a text into its equation tree.
    /// </summary>
    /// <param name="text">The MathML text.</param>
    /// <returns>The <c>math</c> element.</returns>
    public static MathElement Tree(string text) => MathMlReader.Read(text, null, MathMlLimits.Default).Root;

    /// <summary>
    ///     Builds a token element directly (without the reader).
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="text">The text content, or empty.</param>
    /// <param name="attributes">The attributes as name and value pairs.</param>
    /// <returns>The element, located at line 1, column 1.</returns>
    public static MathElement Token(string name, string text, params (string Name, string Value)[] attributes) =>
        new(name, Attributes(attributes), text.Length == 0 ? [] : [new MathText(text, new MathMlLocation(null, 1, 1))], new MathMlLocation(null, 1, 1));

    /// <summary>
    ///     Builds a layout element directly (without the reader).
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <param name="children">The children.</param>
    /// <param name="attributes">The attributes as name and value pairs.</param>
    /// <returns>The element, located at line 1, column 1.</returns>
    public static MathElement Node(string name, IReadOnlyList<MathNode> children, params (string Name, string Value)[] attributes) =>
        new(name, Attributes(attributes), children, new MathMlLocation(null, 1, 1));

    /// <summary>
    ///     Builds the attribute dictionary of an element.
    /// </summary>
    /// <param name="attributes">The attributes as name and value pairs.</param>
    /// <returns>The attributes, located at line 1, column 1.</returns>
    private static Dictionary<string, MathAttribute> Attributes((string Name, string Value)[] attributes) =>
        attributes.ToDictionary(attribute => attribute.Name, attribute => new MathAttribute(attribute.Value, new MathMlLocation(null, 1, 1)), StringComparer.Ordinal);

    /// <summary>
    ///     Lays a text out through the layout engine with the real math font.
    /// </summary>
    /// <param name="text">The MathML text.</param>
    /// <param name="fontSize">The font size of the math element.</param>
    /// <param name="sink">The sink, or null for a new non-strict one.</param>
    /// <returns>The box of the math element.</returns>
    public static MathBox LayoutText(string text, double fontSize = 100, MathMlReportSink? sink = null)
    {
        var root = Tree(text);
        var engine = new LayoutEngine(MathFont.Default, MathMlLimits.Default, sink ?? new MathMlReportSink(100, false), CancellationToken.None);
        var style = engine.Styles.ResolveRoot(root, fontSize, new Rgba32(0, 0, 0, 255), null);
        return engine.Layout(root, style);
    }

    /// <summary>
    ///     Collects every box of a tree with its origin relative to the root origin, in tree order.
    /// </summary>
    /// <param name="root">The root box.</param>
    /// <returns>The boxes and their origins.</returns>
    public static List<(MathBox Box, double X, double Y)> Flatten(MathBox root)
    {
        var result = new List<(MathBox Box, double X, double Y)>();
        void Walk(MathBox box, double x, double y)
        {
            result.Add((box, x, y));
            foreach (var child in box.Children)
            {
                Walk(child.Box, x + child.X, y + child.Y);
            }
        }

        Walk(root, 0, 0);
        return result;
    }

    /// <summary>
    ///     Gets the text that the glyphs of a box would spell with the math font's cmap reversed: the code points of the
    ///     paint items, resolved back through the glyph lookup of each code point candidate.
    /// </summary>
    /// <param name="box">A token box.</param>
    /// <param name="candidates">The code points that may have been drawn.</param>
    /// <returns>The code points drawn, in order.</returns>
    public static List<int> DrawnCodePoints(MathBox box, IEnumerable<int> candidates)
    {
        var byGlyph = new Dictionary<(Fonts.TrueTypeFont, int), int>();
        foreach (var codePoint in candidates)
        {
            var (font, glyph) = MathFont.Default.Resolve(codePoint);
            byGlyph.TryAdd((font, glyph), codePoint);
        }

        return [.. box.Items.OfType<GlyphItem>().Select(item => byGlyph.TryGetValue((item.Font, item.Glyph), out var codePoint) ? codePoint : -1)];
    }

    /// <summary>
    ///     Gets whether every pixel of a surface is fully transparent.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsFullyTransparent(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel.A != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Counts the pixels of a surface that are not fully transparent.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The count.</returns>
    public static int InkedPixels(Surface surface)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel.A != 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Gets whether two surfaces have the same size and identical pixels.
    /// </summary>
    /// <param name="a">The first surface.</param>
    /// <param name="b">The second surface.</param>
    /// <returns>Whether they are identical.</returns>
    public static bool PixelsEqual(Surface a, Surface b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var y = 0; y < a.Height; y++)
        {
            if (!a.GetRowSpan(y).SequenceEqual(b.GetRowSpan(y)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes a temporary file and returns its path.
    /// </summary>
    /// <param name="content">The text, written as UTF-8 without a byte-order mark.</param>
    /// <returns>The path; the caller deletes it.</returns>
    public static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"canvasnet-mathml-{Guid.NewGuid():N}.mml");
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(content));
        return path;
    }
}
