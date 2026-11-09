using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Unit tests of <see cref="StyleDeclarations"/>: the declaration subset, <c>!important</c>, style sheets and the
///     value parsers.
/// </summary>
public class StyleDeclarationsTests
{
    /// <summary>The location the tests report at.</summary>
    private static readonly MermaidLocation Here = new("s.mmd", 2, 3);

    /// <summary>
    ///     Validates the items of a style statement: supported properties kept in lower case, <c>!important</c> read,
    ///     and the cascade within the list.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ParseItems_SupportedProperties()
    {
        // Arrange
        string[] items = ["fill:#f9f", "Stroke: #333 ! important", "stroke-width:4px", "stroke:red", "color:#fff"];

        // Act
        var (set, warnings, _) = Collect(sink => StyleDeclarations.ParseItems(items, Here, sink));

        // Assert
        Assert.Empty(warnings);
        Assert.Equal("#f9f", set["fill"]);
        Assert.Equal("#333", set["stroke"]);
        Assert.True(set.IsImportant("stroke"));
        Assert.Equal(4, set.Length("stroke-width", 1));
        Assert.Equal(CssColor.Parse("#fff"), set.Paint("color"));
    }

    /// <summary>
    ///     Validates that an unsupported property, a declaration without a colon and an invalid value are reported as
    ///     ignored, at the statement's location.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ParseItems_Ignored_Reported()
    {
        // Arrange
        string[] items = ["text-decoration:underline", "fill", "fill:nonsense", "stroke-width:-2px", "opacity:0.5"];

        // Act
        var (set, warnings, _) = Collect(sink => StyleDeclarations.ParseItems(items, Here, sink));

        // Assert
        Assert.Equal(4, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal((MermaidDiagnosticCodes.StyleIgnored, Here), (warning.Code, warning.Location)));
        Assert.Equal("0.5", Assert.Single(set.Declarations).Value);
    }

    /// <summary>
    ///     Validates a declaration block with quotes and functions that contain the separators.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ParseBlock_QuotesAndFunctions()
    {
        // Act
        var (set, warnings, _) = Collect(sink => StyleDeclarations.ParseBlock("font-family: \"a;b\", serif; fill: rgb(1, 2, 3); ;", true, Here, sink));

        // Assert
        Assert.Empty(warnings);
        Assert.Equal("\"a;b\", serif", set["font-family"]);
        Assert.Equal(new Rgba32(1, 2, 3, 255), set.Paint("fill"));
    }

    /// <summary>
    ///     Validates the cascade: a later declaration replaces an earlier one unless the earlier one is important.
    /// </summary>
    [Fact]
    public void StyleDeclarationSet_Cascade_HonorsImportant()
    {
        // Arrange
        var lower = new StyleDeclarationSet([new("fill", "red", true), new("stroke", "red", false)]);
        var upper = new StyleDeclarationSet([new("fill", "blue", false), new("stroke", "blue", false), new("color", "green", false)]);

        // Act
        var set = lower.Cascade(upper);
        var forced = set.Cascade(new StyleDeclarationSet([new("fill", "black", true)]));

        // Assert
        Assert.Equal(("red", "blue", "green"), (set["fill"], set["stroke"], set["color"]));
        Assert.Equal("black", forced["fill"]);
    }

    /// <summary>
    ///     Validates a style sheet: comments removed, known selectors kept (each of a selector list), and unknown
    ///     selectors, at-rules and nested rules reported as unsupported.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ParseSheet_KnownAndUnsupported()
    {
        // Arrange
        const string Css = "/* theme */ .node   rect, .unknown { fill: red; } @media print { .node rect { fill: blue } } .cluster rect { .x { fill: red } } .edgeLabel>p{color:red}";
        var known = new HashSet<string>(StringComparer.Ordinal) { ".node rect", ".cluster rect", ".edgeLabel > p" };

        // Act
        var (rules, _, unsupported) = Collect(sink => StyleDeclarations.ParseSheet(Css, known, Here, sink));

        // Assert
        Assert.Equal([".node rect", ".edgeLabel > p"], rules.Select(rule => rule.Selector));
        Assert.Equal("red", rules[0].Declarations["fill"]);
        Assert.Equal(3, unsupported.Count);
        Assert.All(unsupported, report => Assert.Equal((MermaidDiagnosticCodes.ThemeCssUnsupported, Here), (report.Feature, report.Location)));
    }

    /// <summary>
    ///     Validates that strict mode stops at the first unsupported rule of a style sheet.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ParseSheet_Strict_Throws()
    {
        // Arrange
        var sink = new MermaidReportSink(100, throwOnUnsupported: true);

        // Act
        var ex = Assert.Throws<MermaidUnsupportedFeatureException>(() =>
            StyleDeclarations.ParseSheet(".mine { fill: red }", new HashSet<string>(), Here, sink));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.ThemeCssUnsupported, ex.Feature);
    }

    /// <summary>
    ///     Validates the selector normalization.
    /// </summary>
    /// <param name="selector">The selector.</param>
    /// <param name="expected">The normalized selector.</param>
    [Theory]
    [InlineData("  .node \t rect ", ".node rect")]
    [InlineData(".a>b", ".a > b")]
    [InlineData(".a  >  b", ".a > b")]
    public void StyleDeclarations_NormalizeSelector(string selector, string expected)
    {
        // Act
        var normalized = StyleDeclarations.NormalizeSelector(selector);

        // Assert
        Assert.Equal(expected, normalized);
    }

    /// <summary>
    ///     Validates the length parser.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="expected">The length, or null when invalid.</param>
    [Theory]
    [InlineData("4px", 4.0)]
    [InlineData("4", 4.0)]
    [InlineData("1.5em", 15.0)]
    [InlineData("1rem", 16.0)]
    [InlineData("3pt", 4.0)]
    [InlineData("-1px", null)]
    [InlineData("wide", null)]
    public void StyleDeclarations_TryParseLength(string value, double? expected)
    {
        // Act
        var parsed = StyleDeclarations.TryParseLength(value, 10, out var length);

        // Assert
        Assert.Equal(expected.HasValue, parsed);
        if (expected is { } pixels)
        {
            Assert.Equal(pixels, length, 9);
        }
    }

    /// <summary>
    ///     Validates the font size parser: a length that text can be drawn at, and not zero, a size that is zero in
    ///     single precision, nor one above <see cref="StyleDeclarations.MaxFontSize"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="expected">The size, or null when it is not a drawable font size.</param>
    [Theory]
    [InlineData("12px", 12.0)]
    [InlineData("2em", 20.0)]
    [InlineData("32768px", 32_768.0)]
    [InlineData("0", null)]
    [InlineData("1e-300px", null)]
    [InlineData("1e300px", null)]
    [InlineData("32769px", null)]
    [InlineData("1e300em", null)]
    public void StyleDeclarations_TryParseFontSize(string value, double? expected)
    {
        // Act
        var parsed = StyleDeclarations.TryParseFontSize(value, 10, out var size);

        // Assert
        Assert.Equal(expected.HasValue, parsed);
        if (expected is { } pixels)
        {
            Assert.Equal(pixels, size, 9);
        }
    }

    /// <summary>
    ///     Validates that a font size that cannot be drawn is reported as ignored, with a hint naming the range, and
    ///     left out of the declarations.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ParseItems_FontSizeNotDrawable_IsIgnoredWithHint()
    {
        // Act
        var (set, warnings, _) = Collect(sink => StyleDeclarations.ParseItems(["font-size:1e-300px", "fill:#f00"], new MermaidLocation(null, 3, 1), sink));

        // Assert
        Assert.Null(set["font-size"]);
        Assert.Equal("#f00", set["fill"]);
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.StyleIgnored, warning.Code);
        Assert.Equal(3, warning.Location.Line);
        Assert.Contains("32768", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the dash array, opacity, weight, style and paint parsers.
    /// </summary>
    [Fact]
    public void StyleDeclarations_ValueParsers()
    {
        // Act and Assert
        Assert.True(StyleDeclarations.TryParseDashArray("5, 5", out var dashes));
        Assert.Equal([5.0, 5.0], dashes);
        Assert.True(StyleDeclarations.TryParseDashArray("3", out var odd));
        Assert.Equal([3.0, 3.0], odd);
        Assert.True(StyleDeclarations.TryParseDashArray("0", out var solid));
        Assert.Empty(solid);
        Assert.False(StyleDeclarations.TryParseDashArray("a b", out _));
        Assert.True(StyleDeclarations.TryParseOpacity("50%", out var opacity));
        Assert.Equal(0.5, opacity);
        Assert.True(StyleDeclarations.TryParseBold("700", out var bold) && bold);
        Assert.True(StyleDeclarations.TryParseBold("normal", out var normal) && !normal);
        Assert.False(StyleDeclarations.TryParseBold("heavy", out _));
        Assert.True(StyleDeclarations.TryParseItalic("oblique 10deg", out var italic) && italic);
        Assert.True(StyleDeclarations.TryParsePaint("none", out var none) && none is null);
        Assert.False(StyleDeclarations.TryParsePaint("nonsense", out _));
    }

    /// <summary>
    ///     Runs a parse with a collecting sink.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="parse">The parse.</param>
    /// <returns>The result, the warnings and the unsupported features.</returns>
    private static (T Result, List<MermaidDiagnostic> Warnings, List<MermaidUnsupportedFeature> Unsupported) Collect<T>(Func<MermaidReportSink, T> parse)
    {
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var result = parse(sink);
        var warnings = new List<MermaidDiagnostic>();
        var unsupported = new List<MermaidUnsupportedFeature>();
        sink.Deliver(warnings.Add, unsupported.Add);
        return (result, warnings, unsupported);
    }
}
