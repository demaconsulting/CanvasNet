using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Tests of <see cref="LabelBuilder"/>: the line breaking of the upstream vectors, the markdown and plain-string
///     conversions, the HTML subset and the measurement rules of HTML and SVG labels.
/// </summary>
public sealed class LabelBuilderTests
{
    /// <summary>The ascent of Liberation Sans in em (1854 of 2048 units).</summary>
    private const double LiberationAscent = 1854.0 / 2048;

    /// <summary>The default label font of the flowchart (Mermaid's font stack at 16 pixels, dark gray).</summary>
    private static readonly LabelFont Font = new("\"trebuchet ms\", verdana, arial, sans-serif", 16, false, false, new Rgba32(51, 51, 51, 255));

    /// <summary>
    ///     Gets the cases with an HTML label of more than one line or more than one style.
    /// </summary>
    public static TheoryData<string> HtmlLineCases => ShapeVectors.Names(name =>
        name.StartsWith("label-", StringComparison.Ordinal) && name.EndsWith("-html", StringComparison.Ordinal) && name != "label-ignored-tag-html"
        || name.EndsWith("-long", StringComparison.Ordinal));

    /// <summary>
    ///     Gets the cases with an SVG label.
    /// </summary>
    public static TheoryData<string> SvgLineCases => ShapeVectors.Names(name => name.EndsWith("-svg", StringComparison.Ordinal));

    /// <summary>
    ///     Checks that an HTML label of a vector breaks into Mermaid's lines: the same line tops and line starts.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(HtmlLineCases))]
    public void LabelBuilder_Build_UpstreamVectorHtml_MatchesLines(string name)
    {
        // Arrange
        var vector = ShapeVectors.Case(name);
        var expected = vector["expected"]![0]!.AsObject();
        if (expected["label"] is not JsonObject label)
        {
            return;
        }

        var fragments = label["lines"]!.AsArray().Select(item => item!.AsObject()).ToList();

        // Act
        var result = ShapeVectors.Draw(vector, chromeLabel: false);

        // Assert: one line per distinct top, starting where Mermaid's first fragment of the line starts
        var tops = fragments.Select(f => (double)f["y"]!).Distinct().Order().ToList();
        Assert.Equal(tops.Count, result.Label.Lines.Count);
        for (var i = 0; i < tops.Count; i++)
        {
            var first = fragments.Where(f => Math.Abs((double)f["y"]! - tops[i]) < 1e-9).Min(f => (double)f["x"]!);
            var line = result.Label.Lines[i];
            var ascent = Math.Round(LiberationAscent * line.Runs.Max(run => run.Font.Size), MidpointRounding.AwayFromZero);
            Assert.True(Math.Abs(line.X - first) <= 0.02, $"{name}: line {i} starts at {line.X}, Mermaid {first}");
            Assert.True(Math.Abs(line.Baseline - ascent - tops[i]) <= 0.02, $"{name}: line {i} text top {line.Baseline - ascent}, Mermaid {tops[i]}");
        }
    }

    /// <summary>
    ///     Checks that an SVG label of a vector breaks into Mermaid's lines with the same text.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(SvgLineCases))]
    public void LabelBuilder_Build_UpstreamVectorSvg_MatchesLines(string name)
    {
        // Arrange
        var vector = ShapeVectors.Case(name);
        var expected = vector["expected"]![0]!.AsObject();
        if (expected["label"] is not JsonObject label)
        {
            return;
        }

        var lines = label["lines"]!.AsArray().Select(item => (string)item!["text"]!).Where(text => text.Length > 0).ToList();

        // Act
        var result = ShapeVectors.Draw(vector, chromeLabel: false);
        var actual = result.Label.Lines.Select(line => string.Concat(line.Runs.Select(run => run.Text))).Where(text => text.Length > 0).ToList();

        // Assert
        Assert.Equal(lines, actual);
    }

    /// <summary>
    ///     Checks that a markdown string becomes the HTML Mermaid makes of it.
    /// </summary>
    /// <param name="markdown">The markdown.</param>
    /// <param name="html">The expected HTML.</param>
    [Theory]
    [InlineData("**Bold** and *italic* text", "<p><strong>Bold</strong> and <em>italic</em> text</p>")]
    [InlineData("Line one\nLine **two**", "<p>Line one<br/>Line <strong>two</strong></p>")]
    [InlineData("a \\*not em\\* b", "<p>a *not em* b</p>")]
    [InlineData("a <b>b</b> c", "<p>a <b>b</b> c</p>")]
    [InlineData("- item one", "- item one")]
    [InlineData("# Heading", "# Heading")]
    [InlineData("__bold__ _em_", "<p><strong>bold</strong> <em>em</em></p>")]
    [InlineData("***both*** **a *b* c**", "<p><em><strong>both</strong></em> <strong>a <em>b</em> c</strong></p>")]
    [InlineData("a * b * c", "<p>a * b * c</p>")]
    [InlineData("snake_case_word", "<p>snake_case_word</p>")]
    [InlineData("first\n\nsecond", "<p>first</p><p>second</p>")]
    public void LabelBuilder_MarkdownToHtml_Markdown_GivesMermaidHtml(string markdown, string html)
    {
        // Act
        var result = LabelBuilder.MarkdownToHtml(markdown, autoWrap: true);

        // Assert
        Assert.Equal(html, result);
    }

    /// <summary>
    ///     Checks that markdown without auto-wrap keeps its spaces as no-break spaces.
    /// </summary>
    [Fact]
    public void LabelBuilder_MarkdownToHtml_NoAutoWrap_UsesNoBreakSpaces()
    {
        // Act
        var result = LabelBuilder.MarkdownToHtml("a b", autoWrap: false);

        // Assert
        Assert.Equal("<p>a&nbsp;b</p>", result);
    }

    /// <summary>
    ///     Checks that markdown becomes Mermaid's lines of styled words for SVG labels (escapes are not drawn there).
    /// </summary>
    [Fact]
    public void LabelBuilder_MarkdownToLines_StyledText_GivesStyledWords()
    {
        // Act
        var lines = LabelBuilder.MarkdownToLines("**Bold** and *it*\nnext \\*x");

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal(
            [("Bold", LabelBuilder.WordType.Strong), ("and", LabelBuilder.WordType.Normal), ("it", LabelBuilder.WordType.Em)],
            lines[0]);
        Assert.Equal([("next", LabelBuilder.WordType.Normal), ("x", LabelBuilder.WordType.Normal)], lines[1]);
    }

    /// <summary>
    ///     Checks that a plain string becomes a paragraph whose new lines are line breaks, and an empty one nothing.
    /// </summary>
    [Fact]
    public void LabelBuilder_NonMarkdownToHtml_PlainText_GivesParagraph()
    {
        // Act
        var lines = LabelBuilder.NonMarkdownToHtml("one\\ntwo\nthree");
        var empty = LabelBuilder.NonMarkdownToHtml(string.Empty);

        // Assert
        Assert.Equal("<p>one<br />two<br />three</p>", lines);
        Assert.Empty(empty);
    }

    /// <summary>
    ///     Checks that a plain string becomes lines of words with the tags kept as words.
    /// </summary>
    [Fact]
    public void LabelBuilder_NonMarkdownToLines_TagsAndBreaks_SplitAsMermaid()
    {
        // Act
        var lines = LabelBuilder.NonMarkdownToLines("<b>bold</b> text<BR/>two  words");

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal(["<b>", "bold", "</b>", "text"], lines[0].Select(word => word.Content));
        Assert.Equal(["two", "words"], lines[1].Select(word => word.Content));
    }

    /// <summary>
    ///     Checks <c>ts-dedent</c>'s rules: the common indentation goes, the text is trimmed and <c>\n</c> becomes a new
    ///     line.
    /// </summary>
    [Fact]
    public void LabelBuilder_Dedent_IndentedLines_RemovesCommonIndentation()
    {
        // Act
        var result = LabelBuilder.Dedent("  first\n    second\n    third\\nfourth\n  ");

        // Assert
        Assert.Equal("first\nsecond\nthird\nfourth", result);
    }

    /// <summary>
    ///     Checks that Mermaid's entity placeholders are decoded.
    /// </summary>
    [Fact]
    public void LabelBuilder_DecodeEntities_Placeholders_BecomeReferences()
    {
        // Act
        var result = LabelBuilder.DecodeEntities("a \ufb02\u00b0quot\u00b6\u00df b \ufb02\u00b0\u00b035\u00b6\u00df");

        // Assert
        Assert.Equal("a &quot; b &#35;", result);
    }

    /// <summary>
    ///     Checks that an HTML label's characters are decoded and measured, one line of 24 pixels.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_HtmlLabelWithReferences_DecodesThem()
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest("a &amp; b &lt; c", Font));

        // Assert
        Assert.True(label.IsHtml);
        Assert.Equal(24, label.Height);
        Assert.Equal("a & b < c", string.Concat(label.Lines[0].Runs.Select(run => run.Text)));
        Assert.Equal(0, label.OffsetX);
    }

    /// <summary>
    ///     Checks that an HTML label at least as wide as the wrapping width is laid out in a box of exactly that width with
    ///     its lines centered.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_HtmlLabelWiderThanWrappingWidth_WrapsAtWidth()
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest("The quick brown fox jumps over the lazy dog and keeps on running far away", Font));

        // Assert
        Assert.Equal(200, label.Width);
        Assert.Equal(72, label.Height);
        Assert.All(label.Lines, line => Assert.True(line.Width <= 200));
        Assert.All(label.Lines, line => Assert.Equal((200 - line.Width) / 2, line.X, 6));
    }

    /// <summary>
    ///     Checks that a word longer than the wrapping width widens the box to the word (the browser's min-content
    ///     width), breaking after a hyphen.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_HtmlLabelWithLongWord_WidensToTheWord()
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest("Supercalifragilisticexpialidocious-and-even-longer-word-here", Font));

        // Assert
        Assert.True(label.Width > 200);
        Assert.Equal(2, label.Lines.Count);
        Assert.Equal("Supercalifragilisticexpialidocious-", label.Lines[0].Runs[0].Text);
    }

    /// <summary>
    ///     Checks that unsupported HTML elements are dropped with their text kept and reported once each.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_UnsupportedElement_ReportsAndKeepsText()
    {
        // Arrange
        var builder = NewBuilder(out var sink);
        var warnings = new List<MermaidDiagnostic>();

        // Act
        var label = builder.Build(new LabelRequest("<u>under</u> and <u>again</u>", Font) { Location = new MermaidLocation("f.mmd", 2, 3) });
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal("under and again", string.Concat(label.Lines[0].Runs.Select(run => run.Text)));
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.HtmlTagIgnored, warning.Code);
        Assert.Equal(new MermaidLocation("f.mmd", 2, 3), warning.Location);
    }

    /// <summary>
    ///     Checks that bold, italic and a styled span change the runs' fonts and colors.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_StyledElements_StyleTheRuns()
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest("<b>b</b><i>i</i><span style=\"color:red;font-size:20px\">s</span>", Font));

        // Assert
        var runs = label.Lines[0].Runs;
        Assert.True(runs[0].Font.Bold);
        Assert.True(runs[1].Font.Italic);
        Assert.Equal(new Rgba32(255, 0, 0, 255), runs[2].Color);
        Assert.Equal(20, runs[2].Font.Size);
        Assert.Equal(30, label.Height);
    }

    /// <summary>
    ///     Checks that an icon name is removed from an HTML label without a report (the model reports icons).
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_HtmlIcon_IsRemovedSilently()
    {
        // Arrange
        var builder = NewBuilder(out var sink);

        // Act
        var label = builder.Build(new LabelRequest("fa:fa-car Car", Font));

        // Assert
        Assert.Equal("Car", string.Concat(label.Lines[0].Runs.Select(run => run.Text)));
        Assert.Equal(0, sink.Count);
    }

    /// <summary>
    ///     Checks that an empty label measures nothing.
    /// </summary>
    /// <param name="html">Whether the label is an HTML label.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LabelBuilder_Build_EmptyLabel_HasEmptyBox(bool html)
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest(string.Empty, Font) { HtmlLabels = html });

        // Assert
        Assert.Equal(new SceneRect(0, 0, 0, 0), label.Box);
        Assert.DoesNotContain(label.Lines, line => line.Runs.Count > 0);
    }

    /// <summary>
    ///     Checks the SVG label rules: lines 1.1 em apart from one em below the origin, centered on x 0, the box from the
    ///     rounded ascent to the rounded descent, and tags drawn as text.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_SvgLabel_FollowsTspanRules()
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest("one<br>two", Font) { HtmlLabels = false });

        // Assert
        Assert.False(label.IsHtml);
        Assert.Equal(2, label.Lines.Count);
        Assert.Equal(16, label.Lines[0].Baseline, 6);
        Assert.Equal(16 + 17.6, label.Lines[1].Baseline, 6);
        Assert.Equal(-label.Lines[0].Width / 2, label.Lines[0].X, 6);
        Assert.Equal(2, label.Box.Y, 6);
        Assert.Equal(17.6 + 17, label.Box.Height, 6);
        Assert.Equal(label.Box.X, label.OffsetX);
    }

    /// <summary>
    ///     Checks that an SVG label splits a word wider than the wrapping width at characters.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_SvgLabelWithLongWord_SplitsAtCharacters()
    {
        // Arrange
        var builder = NewBuilder(out _);

        // Act
        var label = builder.Build(new LabelRequest("Supercalifragilisticexpialidocious", Font) { HtmlLabels = false, Width = 100 });

        // Assert
        Assert.True(label.Lines.Count >= 2);
        Assert.All(label.Lines, line => Assert.True(line.Width <= 100));
        Assert.Equal("Supercalifragilisticexpialidocious", string.Concat(label.Lines.SelectMany(line => line.Runs).Select(run => run.Text)));
    }

    /// <summary>
    ///     Checks that a background grows an SVG label's box by two pixels on every side.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_SvgLabelWithBackground_GrowsTheBox()
    {
        // Arrange
        var builder = NewBuilder(out _);
        var request = new LabelRequest("Pic", Font) { HtmlLabels = false };

        // Act
        var plain = builder.Build(request);
        var background = builder.Build(request with { AddBackground = true });

        // Assert
        Assert.Equal(plain.Box.X - 2, background.Box.X, 6);
        Assert.Equal(plain.Box.Width + 4, background.Box.Width, 6);
        Assert.Equal(plain.Box.Height + 4, background.Box.Height, 6);
    }

    /// <summary>
    ///     Checks that a character no bundled font has is reported once for the label, located.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_MissingGlyph_ReportsOnce()
    {
        // Arrange
        var builder = NewBuilder(out var sink);
        var warnings = new List<MermaidDiagnostic>();

        // Act
        builder.Build(new LabelRequest("a \U000F0000 b \U000F0000", Font) { Location = new MermaidLocation(null, 4, 1) });
        sink.Deliver(warnings.Add, null);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.TextMissingGlyph, warning.Code);
        Assert.Contains("U+F0000", warning.Message, StringComparison.Ordinal);
        Assert.Equal(4, warning.Location.Line);
    }

    /// <summary>
    ///     Checks that a picture alone in an HTML label is five times the font size wide with the aspect ratio of its
    ///     natural size, and is part of the label's scene.
    /// </summary>
    [Fact]
    public void LabelBuilder_Build_LoneImage_IsFiveEmWide()
    {
        // Arrange
        var sink = new MermaidReportSink(100, false);
        var images = new NodeImages(new FixedResolver(40, 30), 10, sink);
        var builder = new LabelBuilder(ScenePainter.CreateFontResolver(), sink, images);

        // Act
        var label = builder.Build(new LabelRequest("<img src='pic.png'>", Font));
        var items = label.ToScene(0, 0, "label", null, default);

        // Assert
        var image = Assert.Single(label.Images);
        Assert.Equal(new SceneRect(0, 0, 80, 60), image.Box);
        Assert.Equal(new SceneRect(0, 0, 80, 60), label.Box);
        Assert.Equal("pic.png", Assert.IsType<SceneImage>(Assert.Single(items)).Href);
    }

    /// <summary>
    ///     Checks that the text of an HTML label is drawn on its baselines as scene text.
    /// </summary>
    [Fact]
    public void LabelBuilder_ToScene_HtmlLabel_GivesTextLines()
    {
        // Arrange
        var builder = NewBuilder(out _);
        var label = builder.Build(new LabelRequest("one<br>two", Font));

        // Act
        var items = label.ToScene(10, 20, "label", "n0", default);

        // Assert
        Assert.Equal(2, items.Count);
        var first = Assert.IsType<SceneText>(items[0]);
        Assert.Equal(20 + 17, first.Baseline, 6);
        Assert.Equal("n0", first.Id);
        Assert.Equal("one", first.Runs[0].Text);
    }


    /// <summary>
    ///     Creates a builder with a sink and no pictures.
    /// </summary>
    /// <param name="sink">Receives the sink.</param>
    /// <returns>The builder.</returns>
    private static LabelBuilder NewBuilder(out MermaidReportSink sink)
    {
        sink = new MermaidReportSink(100, false);
        return new LabelBuilder(ScenePainter.CreateFontResolver(), sink, null);
    }

    /// <summary>
    ///     A resolver that gives every link a picture of one size.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private sealed class FixedResolver(int width, int height) : IImageResolver
    {
        /// <inheritdoc/>
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight) => new Surface(width, height);
    }
}
