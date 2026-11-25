using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Layout;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     System integration tests for the <c>CanvasNetMathMl</c> system: reading, describing, measuring and rendering
///     MathML through the public API.
/// </summary>
public class MathMlSystemIntegrationTests
{
    /// <summary>An equation that exercises the reader end to end: references, a comment, an annotation.</summary>
    private const string Text = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" display=\"block\" alttext=\"x plus one\">\n" +
        "  <!-- a comment -->\n" +
        "  <semantics>\n" +
        "    <mrow><mi>x</mi><mo>&plus;</mo><mn>1</mn></mrow>\n" +
        "    <annotation encoding=\"application/x-tex\">x+1</annotation>\n" +
        "  </semantics>\n" +
        "</math>\n";

    /// <summary>The equation of the exact-pixel test: a blue background, an operator, and an error box.</summary>
    private const string PixelText = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" display=\"block\"><mstyle mathbackground=\"#0000ff\"><mi>x</mi><mo>+</mo><mn>1</mn></mstyle><merror><mtext>e</mtext></merror></math>";

    /// <summary>
    ///     Validates that a string, a stream and a file of the same text give the same document.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Open_StringStreamAndPath_Agree()
    {
        // Arrange
        var path = MathMlTestHelpers.WriteTempFile(Text);
        try
        {
            // Act
            var parsed = MathMlDocument.Parse(Text);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Text));
            var streamed = MathMlDocument.Open(stream);
            var opened = MathMlDocument.Open(path);

            // Assert
            Assert.All([parsed, streamed, opened], document => Assert.Equal((MathMlDisplay.Block, 7, "x plus one"), (document.Display, document.ElementCount, document.AltText)));
            Assert.Equal(path, opened.SourceName);
            Assert.Equal(parsed.Measure(cancellationToken: TestContext.Current.CancellationToken), opened.Measure(cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that text that is not well-formed XML fails with a located, coded, hinted error.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Open_InvalidXml_FailsLocated()
    {
        // Arrange
        const string text = "<math>\n  <mi>x</mo>\n</math>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Parse(text, new MathMlOpenOptions { SourceName = "bad.mml" }));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(("bad.mml", 2), (ex.Location.SourceName, ex.Location.Line));
        Assert.True(ex.Location.Column > 0);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that an unknown named reference fails at its ampersand in the original text, after a known
    ///     reference on the same line that the pre-pass rewrote, with a hint.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Open_UnknownNamedReference_FailsLocatedWithHint()
    {
        // Arrange
        const string text = "<math>\n<mi>&alpha;&notaname;</mi></math>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Parse(text));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MathMlLocation(null, 2, 12), ex.Location);
        Assert.Contains("&#x2062;", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a document type declaration is refused with a located error and a hint.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Open_DocumentTypeDeclaration_IsRefused()
    {
        // Arrange
        const string text = "<!DOCTYPE math [<!ENTITY x \"y\">]>\n<math><mi>&x;</mi></math>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Parse(text));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(1, ex.Location.Line);
        Assert.Contains("document type", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that named references of the W3C set are decoded in text and attributes, with numeric ones.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Open_NamedReferences_AreDecoded()
    {
        // Arrange
        const string text = "<math alttext=\"&alpha; &lt; &#x3B2;\"><mi>&alpha;</mi><mo>&InvisibleTimes;</mo><mi>&Bopf;</mi></math>";

        // Act
        var document = MathMlDocument.Parse(text);

        // Assert
        Assert.Equal("\u03B1 < \u03B2", document.AltText);
        Assert.Equal(["\u03B1", "\u2062", "\U0001D539"], document.Root.ElementChildren.Select(child => child.TextContent));
    }

    /// <summary>
    ///     Validates that an XML declaration of an encoding other than UTF-8 is refused at the encoding.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Open_ForeignEncodingDeclaration_IsRefused()
    {
        // Arrange
        const string text = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><math/>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Parse(text));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MathMlLocation(null, 1, 21), ex.Location);
        Assert.Contains("UTF-8", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the description of a document: display, element count, alternative text and annotations.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Describe_ReportsDisplayCountAltTextAndAnnotations()
    {
        // Arrange / Act
        var document = MathMlDocument.Parse(Text);

        // Assert
        Assert.Equal(MathMlDisplay.Block, document.Display);
        Assert.Equal(7, document.ElementCount);
        Assert.Equal("x plus one", document.AltText);
        Assert.Equal([new MathMlAnnotation("application/x-tex", "x+1")], document.Annotations);
    }

    /// <summary>
    ///     Validates the exact pixels of a rendering: a transparent corner, the blue background away from the glyphs, the
    ///     red border of <c>merror</c>, and ink at the center of the operator glyph, all at positions derived from the
    ///     layout.
    /// </summary>
    [Fact]
    [Trait("Category", "Platform")]
    public void CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels()
    {
        // Arrange: the layout gives the positions of the boxes in the picture
        var document = MathMlDocument.Parse(PixelText);
        var options = new MathMlRenderOptions { FontSize = 32 };
        var root = document.LayoutBox(options, TestContext.Current.CancellationToken);
        var extent = MathMlDocument.PictureExtent(root);
        var originX = -Math.Floor(extent.X + 1e-6);
        var originY = -Math.Floor(extent.Y + 1e-6);
        var boxes = MathMlTestHelpers.Flatten(root);
        var mi = boxes.Single(entry => entry.Box.Element?.Name == "mi");
        var mo = boxes.Single(entry => entry.Box.Element?.Name == "mo");
        var error = boxes.Single(entry => entry.Box.Element?.Name == "merror");

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert: the top right corner is outside every box and ink
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[surface.Width - 1, 0]);

        // The middle of the space before the operator, just below the top of the background, is pure blue
        var gapX = (int)(originX + ((mi.X + mi.Box.Width + mo.X) / 2));
        var backgroundTop = (int)Math.Ceiling(originY - root.Children[0].Box.Ascent);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[gapX, backgroundTop + 1]);

        // The top border line of merror is pure red in its middle
        var borderX = (int)(originX + error.X + (error.Box.Width / 2));
        var borderY = (int)(originY - error.Box.Ascent);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[borderX, borderY]);

        // The center of the plus sign is inked black over the blue background
        var plusX = (int)(originX + mo.X + (mo.Box.Width / 2));
        var plusY = (int)(originY + ((mo.Box.Descent - mo.Box.Ascent) / 2));
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[plusX, plusY]);
    }

    /// <summary>
    ///     Validates the exact pixels of stretched parentheses: the extenders of each assembly are pure black in the middle
    ///     of their stroke, every row of the parenthesis between its ends is inked (the parts join without gaps), the rows
    ///     just outside the box are transparent, and a corner is transparent.
    /// </summary>
    [Fact]
    [Trait("Category", "Platform")]
    public void CanvasNetMathMl_Render_StretchedParentheses_PaintsExactPixels()
    {
        // Arrange: the layout gives the positions of the parts in the picture
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<mo>(</mo><mspace width=\"8px\" height=\"4em\" depth=\"3em\"/><mo>)</mo>"));
        var options = new MathMlRenderOptions { FontSize = 32 };
        var root = document.LayoutBox(options, TestContext.Current.CancellationToken);
        var extent = MathMlDocument.PictureExtent(root);
        var originX = -Math.Floor(extent.X + 1e-6);
        var originY = -Math.Floor(extent.Y + 1e-6);
        var parens = MathMlTestHelpers.Flatten(root).Where(entry => entry.Box.Element?.Name == "mo").ToList();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert: two assemblies of at least a bottom, an extender and a top
        Assert.Equal(2, parens.Count);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[surface.Width - 1, 0]);
        foreach (var (box, x, y) in parens)
        {
            var glyphs = box.Items.OfType<GlyphItem>().ToList();
            Assert.True(glyphs.Count >= 3);

            // The middle of the stroke of every extender is fully inked
            foreach (var extender in glyphs.Skip(1).SkipLast(1))
            {
                var column = (int)Math.Ceiling(originX + x + extender.Ink.X);
                var row = (int)(originY + y + extender.Ink.Y + (extender.Ink.Height / 2));
                Assert.Equal(new Rgba32(0, 0, 0, 255), surface[column, row]);
            }

            // Every row between the ends of the parenthesis has ink in its column range; the rows outside the box have none
            var left = (int)(originX + x);
            var right = (int)Math.Ceiling(originX + x + box.Width);
            var top = (int)Math.Ceiling(originY + y - box.Ascent);
            var bottom = (int)Math.Floor(originY + y + box.Descent);
            bool Inked(int row) => Enumerable.Range(left, right - left).Any(column => surface[column, row].A != 0);
            Assert.All(Enumerable.Range(top + 1, bottom - top - 2), row => Assert.True(Inked(row), $"row {row} has no ink"));
            Assert.False(top - 2 >= 0 && Inked(top - 2));
            Assert.False(bottom + 1 < surface.Height && Inked(bottom + 1));
        }
    }

    /// <summary>
    ///     Validates that the spacing around operators follows the operator dictionary: an infix plus adds 4/18 em on each
    ///     side, a prefix minus none, a comma 3/18 em after it only.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Operators_SpacingFollowsDictionary()
    {
        // Arrange
        double Width(string content) =>
            MathMlDocument.Parse(MathMlTestHelpers.Math(content)).LayoutBox(new MathMlRenderOptions { FontSize = 18 }, TestContext.Current.CancellationToken).Width;

        // Act
        var plus = Width("<mi>a</mi><mo>+</mo><mi>b</mi>") - Width("<mi>a</mi><mo lspace=\"0\" rspace=\"0\">+</mo><mi>b</mi>");
        var minus = Width("<mo>-</mo><mi>b</mi>") - Width("<mo lspace=\"0\" rspace=\"0\">-</mo><mi>b</mi>");
        var comma = Width("<mi>a</mi><mo>,</mo><mi>b</mi>") - Width("<mi>a</mi><mo lspace=\"0\" rspace=\"0\">,</mo><mi>b</mi>");

        // Assert
        Assert.Equal(8, plus, 9);
        Assert.Equal(0, minus, 9);
        Assert.Equal(3, comma, 9);
    }

    /// <summary>
    ///     Validates that a large operator is drawn taller in display style than inline.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_LargeOperatorInDisplayStyle_IsTaller()
    {
        // Arrange
        var inline = MathMlDocument.Parse(MathMlTestHelpers.Math("<mo>&#x2211;</mo><mi>k</mi>"));
        var display = MathMlDocument.Parse(MathMlTestHelpers.Math("<mo>&#x2211;</mo><mi>k</mi>", "display=\"block\""));

        // Act
        var inlineSize = inline.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var displaySize = display.Measure(cancellationToken: TestContext.Current.CancellationToken);
        using var picture = display.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(displaySize.Height > inlineSize.Height);
        Assert.True(MathMlTestHelpers.InkedPixels(picture) > 0);
    }

    /// <summary>
    ///     Validates that a stretchy operator that nothing can stretch is reported, located, and still drawn.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_StretchUnavailable_IsReported()
    {
        // Arrange
        var document = MathMlDocument.Parse("<math>\n<mo stretchy=\"true\">x</mo><mspace width=\"1em\" height=\"3em\"/></math>");
        var (options, warnings, _) = MathMlTestHelpers.Collecting();

        // Act
        using var picture = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Equal((MathMlDiagnosticCodes.StretchUnavailable, new MathMlLocation(null, 2, 2)), (warning.Code, warning.Location));
        Assert.True(MathMlTestHelpers.InkedPixels(picture) > 0);
    }

    /// <summary>
    ///     Validates that a stretchy operator that would need more glyphs than <see cref="MathMlLimits.MaxAssemblyParts"/>
    ///     is refused with a located limit error.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Limits_HugeStretch_IsRefused()
    {
        // Arrange
        var document = MathMlDocument.Parse("<math>\n<mo>(</mo><mspace width=\"1em\" height=\"50em\"/></math>");
        var options = new MathMlRenderOptions { Limits = new MathMlLimits { MaxAssemblyParts = 20 } };

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Measure(options, TestContext.Current.CancellationToken));
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, new MathMlLocation(null, 2, 2)), (ex.Code, ex.Location));
        Assert.Contains("MathMlLimits.MaxAssemblyParts", ex.Message, StringComparison.Ordinal);
        Assert.True(size.Height > 800);
    }

    /// <summary>
    ///     Validates that a single-character <c>mi</c> is drawn in italic and that <c>mathvariant</c> draws the styled
    ///     Mathematical Alphanumeric Symbol: the pictures differ from the upright and plain letters.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_AutoItalicAndMathVariant_UseMathAlphanumerics()
    {
        // Arrange
        var italic = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi>x</mi>"));
        var explicitItalic = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi>&#x1D465;</mi>"));
        var upright = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi mathvariant=\"normal\">x</mi>"));
        var doubleStruck = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi mathvariant=\"double-struck\">R</mi>"));
        var explicitDoubleStruck = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi>&#x211D;</mi>"));
        var options = new MathMlRenderOptions { FontSize = 40 };

        // Act
        using var italicPicture = italic.Render(options, TestContext.Current.CancellationToken);
        using var explicitItalicPicture = explicitItalic.Render(options, TestContext.Current.CancellationToken);
        using var uprightPicture = upright.Render(options, TestContext.Current.CancellationToken);
        using var doubleStruckPicture = doubleStruck.Render(options, TestContext.Current.CancellationToken);
        using var explicitDoubleStruckPicture = explicitDoubleStruck.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(MathMlTestHelpers.PixelsEqual(italicPicture, explicitItalicPicture));
        Assert.False(MathMlTestHelpers.PixelsEqual(italicPicture, uprightPicture));
        Assert.True(MathMlTestHelpers.PixelsEqual(doubleStruckPicture, explicitDoubleStruckPicture));
    }

    /// <summary>
    ///     Validates that <c>mathcolor</c>, <c>mathbackground</c> and <c>mathsize</c> apply: a red glyph, a green
    ///     background, and a size twice as large.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_StyleAttributes_ApplyColorBackgroundAndSize()
    {
        // Arrange
        var plain = MathMlDocument.Parse(MathMlTestHelpers.Math("<mn>8</mn>"));
        var styled = MathMlDocument.Parse(MathMlTestHelpers.Math("<mn mathcolor=\"red\" mathbackground=\"lime\" mathsize=\"200%\">8</mn>"));
        var options = new MathMlRenderOptions { FontSize = 30 };

        // Act
        var plainSize = plain.Measure(options, TestContext.Current.CancellationToken);
        var styledSize = styled.Measure(options, TestContext.Current.CancellationToken);
        using var picture = styled.Render(options, TestContext.Current.CancellationToken);
        var colors = new HashSet<Rgba32>();
        for (var y = 0; y < picture.Height; y++)
        {
            foreach (var pixel in picture.GetRowSpan(y))
            {
                colors.Add(pixel);
            }
        }

        // Assert
        Assert.InRange(styledSize.Width, (2 * plainSize.Width) - 2, (2 * plainSize.Width) + 2);
        Assert.Contains(new Rgba32(255, 0, 0, 255), colors);
        Assert.Contains(new Rgba32(0, 255, 0, 255), colors);
        Assert.DoesNotContain(new Rgba32(0, 0, 0, 255), colors);
    }

    /// <summary>
    ///     Validates that the measured size matches the rendered surface and that the baseline lies on the baseline of
    ///     the glyphs: the row above it is inked and a digit has no ink below it.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Measure_MatchesRenderAndReportsBaseline()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<mn>10</mn><mi>y</mi>"));
        var options = new MathMlRenderOptions { FontSize = 50, Scale = 1.5 };

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.InRange(size.Baseline, 1, size.Height - 1);
        var digitsWidth = size.Width / 2;
        Assert.Contains(surface.GetRowSpan(size.Baseline - 1)[..digitsWidth].ToArray(), pixel => pixel.A > 0);
        Assert.DoesNotContain(surface.GetRowSpan(size.Baseline + 1)[..digitsWidth].ToArray(), pixel => pixel.A > 0);
        Assert.Contains(surface.GetRowSpan(size.Height - 1).ToArray(), pixel => pixel.A > 0);
    }

    /// <summary>
    ///     Validates that rendering to an exact size gives that size with the equation fitted and centered.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_FitsExactSize()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi>a</mi><mo>=</mo><mi>b</mi>"));
        var background = new Rgba32(255, 255, 255, 255);

        // Act
        using var surface = document.Render(300, 200, new MathMlRenderOptions { Background = background }, TestContext.Current.CancellationToken);

        // Assert: the size, a white margin above and below the centered equation, and ink at the left and right edges
        Assert.Equal((300, 200), (surface.Width, surface.Height));
        Assert.All(surface.GetRowSpan(0).ToArray(), pixel => Assert.Equal(background, pixel));
        Assert.All(surface.GetRowSpan(199).ToArray(), pixel => Assert.Equal(background, pixel));
        var middle = Enumerable.Range(0, 200).Select(y => surface.GetRowSpan(y).ToArray()).ToList();
        Assert.Contains(middle, row => row[..30].Any(pixel => pixel != background));
        Assert.Contains(middle, row => row[^30..].Any(pixel => pixel != background));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that renders are identical across calls and across threads.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_IsDeterministicAcrossCallsAndThreads()
    {
        // Arrange
        var document = MathMlDocument.Parse(Text);
        var options = new MathMlRenderOptions { FontSize = 24 };
        using var reference = document.Render(options, TestContext.Current.CancellationToken);

        // Act
        var results = new bool[8];
        Parallel.For(0, results.Length, i =>
        {
            using var surface = document.Render(options, TestContext.Current.CancellationToken);
            results[i] = MathMlTestHelpers.PixelsEqual(reference, surface);
        });

        // Assert
        Assert.All(results, Assert.True);
    }

    /// <summary>
    ///     Validates that a canceled token stops measuring and both kinds of rendering.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_PreCanceledToken_Throws()
    {
        // Arrange
        var document = MathMlDocument.Parse(Text);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Measure(null, source.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(null, source.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(10, 10, null, source.Token));
    }

    /// <summary>
    ///     Validates that a text over the size limit is refused before it is read.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Limits_OversizedText_IsRefused()
    {
        // Arrange
        var options = new MathMlOpenOptions { Limits = new MathMlLimits { MaxTextSize = 100 } };
        var text = MathMlTestHelpers.Math(new string(' ', 200));

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => MathMlDocument.Parse(text, options));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', 1000)));
        var streamed = Assert.Throws<InvalidDataException>(() => MathMlDocument.Open(stream, options));

        // Assert
        Assert.Contains("MathMlLimits.MaxTextSize", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MathMlLimits.MaxTextSize", streamed.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that ten thousand nested rows fail with the nesting limit, never with a stack overflow.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Limits_DeepNesting_IsRefusedWithoutStackOverflow()
    {
        // Arrange
        var text = MathMlTestHelpers.Math(string.Concat(Enumerable.Repeat("<mrow>", 10_000)) + string.Concat(Enumerable.Repeat("</mrow>", 10_000)));

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Parse(text));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxNestingDepth", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that elements this version does not draw are laid out as rows of their children and reported with
    ///     their locations, and that the rendering continues.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_UnsupportedElements_AreDrawnAsRowsAndReported()
    {
        // Arrange
        var document = MathMlDocument.Parse("<math>\n<menclose><mi>a</mi><mi>b</mi></menclose>\n<mfenced><mi>c</mi></mfenced></math>");
        var row = MathMlDocument.Parse("<math><mrow><mi>a</mi><mi>b</mi></mrow><mrow><mi>c</mi></mrow></math>");
        var (options, _, unsupported) = MathMlTestHelpers.Collecting();

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        var rowSize = row.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(rowSize, size);
        Assert.Equal([MathMlDiagnosticCodes.ElementUnsupported, MathMlDiagnosticCodes.ElementUnsupported], unsupported.Select(report => report.Feature));
        Assert.Equal([new MathMlLocation(null, 2, 2), new MathMlLocation(null, 3, 2)], unsupported.Select(report => report.Location));
    }

    /// <summary>
    ///     Validates strict mode: the first unsupported feature throws after it was delivered to the handler.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_StrictMode_FirstUnsupportedFeatureThrows()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<menclose><mi>x</mi></menclose><mfenced/>"));
        var (options, _, unsupported) = MathMlTestHelpers.Collecting(strict: true);

        // Act
        var ex = Assert.Throws<MathMlUnsupportedFeatureException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.ElementUnsupported, ex.Feature);
        Assert.Contains("<menclose>", ex.Message, StringComparison.Ordinal);
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates the exact pixels of a fraction bar: a 4 px bar of <c>linethickness</c> centered on the math axis
    ///     between two empty children is pure black across the content box, and the pixels above and below it and in the
    ///     1 px inline padding are transparent.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_FractionBar_PaintsExactPixels()
    {
        // Arrange: the layout gives the position of the fraction in the picture
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<mfrac linethickness=\"4px\"><mspace width=\"30px\" height=\"10px\"/><mspace width=\"30px\" height=\"10px\"/></mfrac>"));
        var options = new MathMlRenderOptions { FontSize = 32 };
        var root = document.LayoutBox(options, TestContext.Current.CancellationToken);
        var extent = MathMlDocument.PictureExtent(root);
        var originX = -Math.Floor(extent.X + 1e-6);
        var originY = -Math.Floor(extent.Y + 1e-6);
        var (fraction, fractionX, _) = MathMlTestHelpers.Flatten(root).Single(entry => entry.Box.Element?.Name == "mfrac");
        var bar = Assert.IsType<RuleItem>(Assert.Single(fraction.Items)).Rect;

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert: the bar is 4 px thick, centered on the axis (AxisHeight 278 at 32 px), 30 px wide after the padding
        Assert.Equal((1.0, 30.0, 4.0), (bar.X, bar.Width, bar.Height));
        Assert.Equal(-0.278 * 32, bar.Y + 2, 9);
        var top = (int)Math.Ceiling(originY + bar.Y);
        var bottom = (int)Math.Floor(originY + bar.Bottom);
        var left = (int)Math.Ceiling(originX + fractionX + bar.X);
        var right = (int)Math.Floor(originX + fractionX + bar.Right);
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                Assert.Equal(new Rgba32(0, 0, 0, 255), surface[x, y]);
            }
        }

        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[left + 10, top - 2]);
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[left + 10, bottom + 1]);
        Assert.Equal(0, surface[(int)(originX + fractionX), top + 1].A);
    }

    /// <summary>
    ///     Validates the pixels of a square root: the overbar is pure black from the end of the radical symbol over the whole
    ///     base, the space under it (an empty base) is transparent, and the radical symbol is inked at the left.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Render_SquareRoot_PaintsSurdAndOverbar()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<msqrt><mspace width=\"40px\" height=\"20px\" depth=\"5px\"/></msqrt>"));
        var options = new MathMlRenderOptions { FontSize = 32 };
        var root = document.LayoutBox(options, TestContext.Current.CancellationToken);
        var extent = MathMlDocument.PictureExtent(root);
        var originX = -Math.Floor(extent.X + 1e-6);
        var originY = -Math.Floor(extent.Y + 1e-6);
        var (sqrt, sqrtX, _) = MathMlTestHelpers.Flatten(root).Single(entry => entry.Box.Element?.Name == "msqrt");
        var bar = Assert.Single(sqrt.Items.OfType<RuleItem>()).Rect;
        var symbol = sqrt.Items.OfType<GlyphItem>().Single();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert: the overbar covers the base and is fully inked along its middle row
        Assert.Equal(40, bar.Width, 9);
        var row = (int)(originY + bar.Y + (bar.Height / 2));
        var left = (int)Math.Ceiling(originX + sqrtX + bar.X);
        var right = (int)Math.Floor(originX + sqrtX + bar.Right);
        Assert.All(Enumerable.Range(left, right - left), x => Assert.Equal(new Rgba32(0, 0, 0, 255), surface[x, row]));

        // Under the overbar, over the empty base, nothing is painted; the symbol has ink left of the base
        Assert.Equal(0, surface[(left + right) / 2, (int)originY].A);
        var symbolLeft = (int)(originX + sqrtX + symbol.Ink.X);
        var symbolRight = (int)Math.Ceiling(originX + sqrtX + symbol.Ink.Right);
        var inked = Enumerable.Range(symbolLeft, symbolRight - symbolLeft).Sum(x => Enumerable.Range(0, surface.Height).Count(y => surface[x, y].A != 0));
        Assert.True(inked > 50);
    }

    /// <summary>
    ///     Validates that a fraction and a root without two children are reported as <c>mathml-child-count</c> warnings
    ///     with their locations, laid out as rows, and do not stop a strict rendering (invalid markup is not an unsupported
    ///     feature).
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_ChildCount_IsReportedAndLaidOutAsRow()
    {
        // Arrange
        var document = MathMlDocument.Parse("<math>\n<mfrac><mi>a</mi></mfrac>\n<mroot><mi>a</mi><mi>b</mi><mi>c</mi></mroot></math>");
        var (options, warnings, unsupported) = MathMlTestHelpers.Collecting(strict: true);

        // Act
        using var picture = document.Render(options, TestContext.Current.CancellationToken);
        var root = document.LayoutBox(new MathMlRenderOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(unsupported);
        Assert.Equal([MathMlDiagnosticCodes.ChildCount, MathMlDiagnosticCodes.ChildCount], warnings.Select(warning => warning.Code));
        Assert.Equal([new MathMlLocation(null, 2, 2), new MathMlLocation(null, 3, 2)], warnings.Select(warning => warning.Location));
        var mroot = MathMlTestHelpers.Flatten(root).Single(entry => entry.Box.Element?.Name == "mroot").Box;
        Assert.Equal(3, mroot.Children.Count);
        Assert.All(mroot.Children, child => Assert.Equal(0, child.Y));
        Assert.True(MathMlTestHelpers.InkedPixels(picture) > 0);
    }

    /// <summary>
    ///     Validates that the legacy attributes of <c>mfrac</c> that MathML Core dropped (<c>bevelled</c>,
    ///     <c>numalign</c>, <c>denomalign</c>) are reported as unsupported attributes, and that the fraction is still drawn
    ///     as MathML Core draws it (the same as without them).
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Fractions_LegacyAttributes_AreReported()
    {
        // Arrange
        var legacy = MathMlDocument.Parse(MathMlTestHelpers.Math("<mfrac bevelled=\"true\" numalign=\"left\" denomalign=\"right\"><mi>a</mi><mn>10</mn></mfrac>"));
        var core = MathMlDocument.Parse(MathMlTestHelpers.Math("<mfrac><mi>a</mi><mn>10</mn></mfrac>"));
        var (options, warnings, unsupported) = MathMlTestHelpers.Collecting();

        // Act
        using var legacyPicture = legacy.Render(options, TestContext.Current.CancellationToken);
        using var corePicture = core.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(3, unsupported.Count);
        Assert.All(unsupported, report => Assert.Equal(MathMlDiagnosticCodes.AttributeUnsupported, report.Feature));
        Assert.Equal(["'bevelled'", "'numalign'", "'denomalign'"], unsupported.Select(report => report.Message.Split(' ')[2]));
        Assert.True(MathMlTestHelpers.PixelsEqual(legacyPicture, corePicture));
    }

    /// <summary>
    ///     Validates that a character missing from the math font falls back to a bundled font, and that a character no
    ///     bundled font has is reported and drawn as the missing-glyph box.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_MissingGlyph_FallsBackAndReports()
    {
        // Arrange
        var accented = MathMlDocument.Parse(MathMlTestHelpers.Math("<mtext>caf&#xE9;</mtext>"));
        var missing = MathMlDocument.Parse(MathMlTestHelpers.Math("<mtext>&#xE000;</mtext>"));
        var (options, warnings, _) = MathMlTestHelpers.Collecting();

        // Act
        using var accentedPicture = accented.Render(options, TestContext.Current.CancellationToken);
        var accentedWarnings = warnings.Count;
        using var missingPicture = missing.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, accentedWarnings);
        Assert.True(MathMlTestHelpers.InkedPixels(accentedPicture) > 0);
        var warning = Assert.Single(warnings);
        Assert.Equal(MathMlDiagnosticCodes.TextMissingGlyph, warning.Code);
        Assert.Contains("U+E000", warning.Message, StringComparison.Ordinal);
        Assert.True(MathMlTestHelpers.InkedPixels(missingPicture) > 0);
    }

    /// <summary>
    ///     Validates that links are reported and never followed: rendering is offline.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Links_AreReportedNotFetched()
    {
        // Arrange
        const string text = "<math xmlns:xlink=\"http://www.w3.org/1999/xlink\"><mi href=\"https://example.invalid/x\">x</mi><mi xlink:href=\"file:///etc/passwd\">y</mi></math>";
        var (options, _, unsupported) = MathMlTestHelpers.Collecting();

        // Act
        using var surface = MathMlDocument.Parse(text).Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, unsupported.Count);
        Assert.All(unsupported, report => Assert.Equal(MathMlDiagnosticCodes.AttributeUnsupported, report.Feature));
        Assert.All(unsupported, report => Assert.Contains("not followed", report.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the third-party notices ship next to the package (copied to the test output from the package's
    ///     <c>third-party</c> folder), that the W3C notice names its sources, and that the provenance record names the
    ///     license files.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_ThirdPartyNotices_ArePresent()
    {
        // Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, "Notices");

        // Act
        var w3c = File.ReadAllText(Path.Combine(folder, "LICENSE-w3c.txt"));
        var unicode = File.ReadAllText(Path.Combine(folder, "LICENSE-unicode.txt"));
        var provenance = File.ReadAllText(Path.Combine(folder, "third-party-provenance.txt"));

        // Assert
        Assert.Contains("W3C SOFTWARE AND DOCUMENT NOTICE AND LICENSE", w3c, StringComparison.Ordinal);
        Assert.Contains("htmlmathml-f.ent", w3c, StringComparison.Ordinal);
        Assert.Contains("Appendix C.1", w3c, StringComparison.Ordinal);
        Assert.Contains("Appendix B.1", w3c, StringComparison.Ordinal);
        Assert.Contains("Appendix B.4", w3c, StringComparison.Ordinal);
        Assert.Contains("UNICODE LICENSE V3", unicode, StringComparison.Ordinal);
        Assert.Contains("not affiliated", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-w3c.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-unicode.txt", provenance, StringComparison.Ordinal);
    }
}
