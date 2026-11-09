using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     The upstream vectors of the shape library and the label builder: every case of <c>shapes.json</c>, recorded from
///     the pinned Mermaid in headless Chrome by <c>gen-shape-vectors.mjs</c>, drawn with this library and compared with
///     Mermaid's node size, label placement, intersections and paint.
/// </summary>
public sealed class ShapeLibraryVectorTests
{
    /// <summary>
    ///     The shapes that Mermaid draws as rough.js circles, whose curve is up to 0.1 % wider than the true circle this
    ///     library draws.
    /// </summary>
    private static readonly HashSet<string> RoughCircles = new(StringComparer.Ordinal)
    {
        "filledCircle", "stateEnd", "crossedCircle", "anchor", "iconCircle",
    };

    /// <summary>
    ///     The cases whose label this library measures differently on purpose, with the largest difference allowed.
    /// </summary>
    private static readonly Dictionary<string, double> KnownLabelDifferences = new(StringComparer.Ordinal)
    {
        // <sup> and <code> are not supported: their text is drawn plain (mermaid-html-tag-ignored)
        ["label-ignored-tag-html"] = 4,
    };

    /// <summary>
    ///     Gets the names of every case.
    /// </summary>
    public static TheoryData<string> AllCases => ShapeVectors.Names(_ => true);

    /// <summary>
    ///     Gets the names of the cases whose paint is compared (the icon cases draw Mermaid's unknown-icon placeholder,
    ///     which this library does not draw).
    /// </summary>
    public static TheoryData<string> PaintCases => ShapeVectors.Names(name => name.EndsWith("-short", StringComparison.Ordinal) || name.StartsWith("style-", StringComparison.Ordinal) || name.StartsWith("image-", StringComparison.Ordinal));

    /// <summary>
    ///     Checks that the vectors cover every documented shape with a short label, a wrapping label and an SVG label.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Vectors_CoverEveryDocumentedShape()
    {
        // Arrange
        var names = ShapeVectors.Cases.Select(item => (string)item!["name"]!).ToHashSet(StringComparer.Ordinal);

        // Act
        var missing = ShapeNames.ShortNames
            .SelectMany(shape => new[] { $"shape-{shape}-short", $"shape-{shape}-long", $"shape-{shape}-svg" })
            .Where(name => !names.Contains(name))
            .ToList();

        // Assert
        Assert.Equal(53, ShapeNames.ShortNames.Count);
        Assert.Empty(missing);
    }

    /// <summary>
    ///     Draws the node of a case with Mermaid's own measured label and checks the shape geometry: the node's box, the
    ///     label's place and the intersections.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(AllCases))]
    public void ShapeLibrary_Draw_UpstreamVectorWithMermaidLabel_MatchesGeometry(string name)
    {
        // Arrange
        var vector = ShapeVectors.Case(name);
        var expected = vector["expected"]![0]!.AsObject();

        // Act
        var result = ShapeVectors.Draw(vector, chromeLabel: true);

        // Assert
        var tolerance = RoughCircles.Contains(result.Handler) ? 0.06 : 0.02;
        var bbox = expected["bbox"]!.AsObject();
        AssertNear(name, "width", (double)expected["width"]!, result.Width, tolerance);
        AssertNear(name, "height", (double)expected["height"]!, result.Height, tolerance);
        AssertNear(name, "bbox x", (double)bbox["x"]!, result.Bounds.X, tolerance);
        AssertNear(name, "bbox y", (double)bbox["y"]!, result.Bounds.Y, tolerance);
        if (expected["label"] is JsonObject label)
        {
            AssertNear(name, "label x", (double)label["offset"]!["x"]!, result.LabelOffset.X, 1e-3);
            AssertNear(name, "label y", (double)label["offset"]!["y"]!, result.LabelOffset.Y, 1e-3);
        }

        var node = new NodeBox(0, 0, (double)expected["width"]!, (double)expected["height"]!);
        foreach (var hit in expected["intersects"]!.AsArray())
        {
            var point = new ShapePoint((double)hit!["p"]!["x"]!, (double)hit["p"]!["y"]!);
            var actual = result.Intersect(node, point);
            AssertNear(name, $"intersect x of {point}", (double)hit["r"]!["x"]!, actual.X, 0.01);
            AssertNear(name, $"intersect y of {point}", (double)hit["r"]!["y"]!, actual.Y, 0.01);
        }
    }

    /// <summary>
    ///     Draws the node of a case with this library's own label measurement and checks the label's box and the node's
    ///     size: HTML labels as the browser measures them, SVG labels within the browser's glyph rounding.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(AllCases))]
    public void ShapeLibrary_Draw_UpstreamVector_MatchesNodeSize(string name)
    {
        // Arrange
        var vector = ShapeVectors.Case(name);
        var expected = vector["expected"]![0]!.AsObject();

        // Act
        var result = ShapeVectors.Draw(vector, chromeLabel: false);

        // Assert
        var html = expected["label"]?["html"] is not JsonValue flag || (bool)flag;
        var plainTolerance = html ? 0.02 : 2;
        var labelTolerance = KnownLabelDifferences.TryGetValue(name, out var known) ? known : plainTolerance;
        var sizeTolerance = labelTolerance + (RoughCircles.Contains(result.Handler) ? 0.06 : 0.02);
        if (expected["label"] is JsonObject label)
        {
            AssertNear(name, "label width", (double)label["box"]!["width"]!, result.Label.Width, labelTolerance);
            AssertNear(name, "label height", (double)label["box"]!["height"]!, result.Label.Height, labelTolerance);
        }

        AssertNear(name, "width", (double)expected["width"]!, result.Width, sizeTolerance);
        AssertNear(name, "height", (double)expected["height"]!, result.Height, sizeTolerance);
    }

    /// <summary>
    ///     Checks that the parts of a case are painted with the colors and widths that Mermaid's elements compute to.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(PaintCases))]
    public void ShapeLibrary_Draw_UpstreamVector_MatchesPaint(string name)
    {
        // Arrange
        var vector = ShapeVectors.Case(name);
        var expected = vector["expected"]![0]!.AsObject();
        var expectedFills = new SortedSet<string>(StringComparer.Ordinal);
        var expectedStrokes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var part in expected["parts"]!.AsArray().Select(item => item!.AsObject()))
        {
            CollectExpected(part, expectedFills, expectedStrokes);
        }

        // Act
        var result = ShapeVectors.Draw(vector, chromeLabel: true);
        var fills = new SortedSet<string>(StringComparer.Ordinal);
        var strokes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var item in result.Items)
        {
            switch (item)
            {
                case ScenePath path when path.Role != "label-background":
                    if (path.Fill is { A: > 0 } fill)
                    {
                        fills.Add(Key(fill));
                    }

                    if (path.Stroke is { Width: > 0, Color.A: > 0 } stroke)
                    {
                        strokes.Add(Key(stroke.Color) + " " + stroke.Width.ToString("0.###", CultureInfo.InvariantCulture));
                    }

                    break;
                case SceneText text when text.Role == "node-glyph":
                    fills.Add(Key(text.Runs[0].Color));
                    break;
            }
        }

        // Assert
        Assert.True(expectedFills.SetEquals(fills), $"{name}: fills {string.Join(", ", fills)}, Mermaid {string.Join(", ", expectedFills)}");
        Assert.True(expectedStrokes.SetEquals(strokes), $"{name}: strokes {string.Join(", ", strokes)}, Mermaid {string.Join(", ", expectedStrokes)}");
    }

    /// <summary>
    ///     Adds the visible fill and stroke of a recorded element.
    /// </summary>
    /// <param name="part">The element.</param>
    /// <param name="fills">The fills.</param>
    /// <param name="strokes">The strokes.</param>
    private static void CollectExpected(JsonObject part, SortedSet<string> fills, SortedSet<string> strokes)
    {
        var tag = (string)part["tag"]!;
        var box = part["box"]!.AsObject();
        if (tag == "image" || ((double)box["width"]! <= 0 && (double)box["height"]! <= 0))
        {
            return;
        }

        var opacity = double.Parse((string)part["opacity"]!, CultureInfo.InvariantCulture);
        var fillOpacity = double.Parse((string)part["fillOpacity"]!, CultureInfo.InvariantCulture);
        var strokeOpacity = double.Parse((string)part["strokeOpacity"]!, CultureInfo.InvariantCulture);
        if (tag != "line" && ParseCss((string)part["fill"]!, opacity * fillOpacity) is { A: > 0 } fill)
        {
            fills.Add(Key(fill));
        }

        var width = double.Parse(((string)part["strokeWidth"]!).Replace("px", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        if (tag != "text" && width > 0 && ParseCss((string)part["stroke"]!, opacity * strokeOpacity) is { A: > 0 } stroke)
        {
            strokes.Add(Key(stroke) + " " + width.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    ///     Reads a computed CSS color (<c>rgb(...)</c>, <c>rgba(...)</c> or <c>none</c>) with an opacity.
    /// </summary>
    /// <param name="text">The computed value.</param>
    /// <param name="opacity">The opacity to fold in.</param>
    /// <returns>The color, or null for none.</returns>
    private static Rgba32? ParseCss(string text, double opacity)
    {
        if (!text.StartsWith("rgb", StringComparison.Ordinal))
        {
            return null;
        }

        var values = text[(text.IndexOf('(', StringComparison.Ordinal) + 1)..text.IndexOf(')', StringComparison.Ordinal)]
            .Split(',')
            .Select(part => double.Parse(part.Trim(), CultureInfo.InvariantCulture))
            .ToArray();
        var alpha = values.Length > 3 ? values[3] : 1;
        return new Rgba32((byte)values[0], (byte)values[1], (byte)values[2], (byte)Math.Round(alpha * opacity * 255, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     Gets the comparison key of a color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The key.</returns>
    private static string Key(Rgba32 color) => string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}");

    /// <summary>
    ///     Asserts that a number is near another, naming the case and the value.
    /// </summary>
    /// <param name="name">The case.</param>
    /// <param name="what">The value.</param>
    /// <param name="expected">Mermaid's value.</param>
    /// <param name="actual">This library's value.</param>
    /// <param name="tolerance">The largest difference allowed.</param>
    private static void AssertNear(string name, string what, double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, string.Create(CultureInfo.InvariantCulture, $"{name}: {what} is {actual}, Mermaid {expected} (tolerance {tolerance})"));
}
