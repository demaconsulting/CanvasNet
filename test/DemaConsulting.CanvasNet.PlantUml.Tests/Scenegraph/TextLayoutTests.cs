using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Scenegraph;

/// <summary>
///     Unit tests for the <c>TextLayout</c> unit: the metrics of the bundled fonts as PlantUML measures them, the line
///     breaks, the creole subset, wrapping and the reports.
/// </summary>
public class TextLayoutTests
{
    /// <summary>The default participant font of PlantUML.</summary>
    private static readonly SceneFont Font14 = new("SansSerif", 14, false, false);

    /// <summary>
    ///     Creates a text layout over the bundled fonts.
    /// </summary>
    /// <returns>The layout.</returns>
    private static TextLayout CreateLayout() => new(TextLayout.CreateFontResolver());

    /// <summary>
    ///     Validates the early fidelity check: Liberation Sans measures "Alice" at 14 pixels as Java measures it with
    ///     Arial (the <c>textLength</c> of PlantUML's SVG is 30.345).
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Width_AliceAt14_MatchesPlantUml()
    {
        // Arrange
        var layout = CreateLayout();

        // Act
        var alice = layout.Width("Alice", Font14);
        var hello = layout.Width("hello", Font14 with { Size = 13 });

        // Assert: the widths of PlantUML's own SVG of the same texts
        Assert.Equal(30.345, alice, 0.01);
        Assert.Equal(27.466, hello, 0.01);
    }

    /// <summary>
    ///     Validates the line metrics of Java's logical fonts: a line of 14 pixels is 17.609 high with a descent of 3.076.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_LineMetrics_FollowLogicalFont()
    {
        // Arrange
        var layout = CreateLayout();

        // Act
        var (height, descent) = layout.LineMetrics(Font14);

        // Assert
        Assert.Equal(17.609, height, 0.001);
        Assert.Equal(3.076, descent, 0.001);
    }

    /// <summary>
    ///     Validates that <c>\n</c> and real line feeds break lines, an escaped backslash stays, and the block stacks the
    ///     lines.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Layout_LineBreaks_StackLines()
    {
        // Arrange
        var layout = CreateLayout();

        // Act
        var block = layout.Layout("one\\ntwo\nthree \\\\n", Font14, SkinColors.Black);

        // Assert
        Assert.Equal(["one", "two", "three \\n"], block.Lines.Select(l => string.Concat(l.Runs.Select(r => r.Text))));
        Assert.Equal(3 * 17.609, block.Height, 0.01);
        Assert.Equal(block.Lines.Max(l => l.Width), block.Width);
    }

    /// <summary>
    ///     Validates the creole subset: bold, italic, mono, underline, strike, color, size and font, and the escape.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Layout_CreoleSubset_StylesRuns()
    {
        // Arrange
        var layout = CreateLayout();

        // Act
        var runs = layout.Layout("a **b** //c// \"\"d\"\" __e__ --f-- <color:red>g</color> <size:20>h</size> <font:Serif>i</font> ~**j", Font14, SkinColors.Black)
            .Lines.Single().Runs.Where(r => r.Text.Trim().Length > 0).ToDictionary(r => r.Text.Trim());

        // Assert
        Assert.True(runs["b"].Font.Bold);
        Assert.True(runs["c"].Font.Italic);
        Assert.Equal("Monospaced", runs["d"].Font.Family);
        Assert.True(runs["e"].Underline);
        Assert.True(runs["f"].Strike);
        Assert.Equal(new Rgba32(255, 0, 0, 255), runs["g"].Color);
        Assert.Equal(20, runs["h"].Font.Size);
        Assert.Equal("Serif", runs["i"].Font.Family);
        Assert.Contains(runs.Keys, k => k.EndsWith("**j", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a dash pair without a closing pair, and a URL, are text.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Layout_LoneMarkupCharacters_AreText()
    {
        // Arrange
        var layout = CreateLayout();

        // Act
        var text = layout.Layout("a -- b http://x", Font14, SkinColors.Black).Text;

        // Assert
        Assert.Equal("a -- b http://x", text);
    }

    /// <summary>
    ///     Validates that markup outside the subset is drawn as text and reported once.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Layout_UnsupportedMarkup_IsReportedAndKept()
    {
        // Arrange
        var layout = CreateLayout();
        var sink = new PlantUmlReportSink(10, throwOnUnsupported: false);
        var reports = new List<PlantUmlUnsupportedFeature>();

        // Act
        var block = layout.Layout("see <img:x.png> and <$sprite>", Font14, SkinColors.Black, 0, sink, new PlantUmlLocation(null, 3, 1));
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal("see <img:x.png> and <$sprite>", block.Text);
        var report = Assert.Single(reports);
        Assert.Equal((PlantUmlDiagnosticCodes.MarkupUnsupported, 3), (report.Feature, report.Location.Line));
    }

    /// <summary>
    ///     Validates that a line is wrapped at spaces to the wrapping width, and a word wider than the width stays whole.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Layout_Wrap_BreaksAtSpaces()
    {
        // Arrange
        var layout = CreateLayout();

        // Act
        var block = layout.Layout("one two three four", Font14, SkinColors.Black, wrapWidth: 60);
        var whole = layout.Layout("extraordinarily", Font14, SkinColors.Black, wrapWidth: 10);

        // Assert
        Assert.True(block.Lines.Count > 1);
        Assert.All(block.Lines, line => Assert.True(line.Width <= 60.001));
        Assert.Equal("one two three four", block.Text.Replace('\n', ' '));
        Assert.Single(whole.Lines);
    }

    /// <summary>
    ///     Validates that a character missing from the bundled face is reported.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Layout_MissingGlyph_Warns()
    {
        // Arrange
        var layout = CreateLayout();
        var sink = new PlantUmlReportSink(10, throwOnUnsupported: false);
        var warnings = new List<PlantUmlDiagnostic>();

        // Act
        var block = layout.Layout("data \U0001F600", Font14, SkinColors.Black, 0, sink, default);
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.True(block.Width > 0);
        Assert.Equal(PlantUmlDiagnosticCodes.TextMissingGlyph, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Validates the mapping of PlantUML's families to the bundled ones.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_CssFamily_MapsToBundledFamilies()
    {
        // Arrange / Act / Assert
        Assert.Equal("sans-serif", TextLayout.CssFamily("SansSerif"));
        Assert.Equal("sans-serif", TextLayout.CssFamily("Arial"));
        Assert.Equal("sans-serif", TextLayout.CssFamily("Unknown Font"));
        Assert.Equal("serif", TextLayout.CssFamily("Times"));
        Assert.Equal("monospace", TextLayout.CssFamily("Courier"));
        Assert.Equal("monospace", TextLayout.CssFamily("Monospaced"));
    }

    /// <summary>
    ///     Validates that a block is drawn with its baselines at the line top plus the line height minus the descent, and
    ///     its lines aligned.
    /// </summary>
    [Fact]
    public void PlantUmlTextLayout_Draw_PlacesBaselinesAndAlignment()
    {
        // Arrange
        var layout = CreateLayout();
        var block = layout.Layout("wide line\\nx", Font14, SkinColors.Black);

        // Act
        var lines = block.Draw(10, 20, TextAlign.Center, "label", default).ToList();

        // Assert
        Assert.Equal(20 + 17.609 - 3.076, lines[0].Baseline, 0.001);
        Assert.Equal(20 + (2 * 17.609) - 3.076, lines[1].Baseline, 0.001);
        Assert.Equal(10, lines[0].X, 0.001);
        Assert.Equal(10 + ((block.Width - lines[1].Box.Width) / 2), lines[1].X, 0.001);
    }
}
