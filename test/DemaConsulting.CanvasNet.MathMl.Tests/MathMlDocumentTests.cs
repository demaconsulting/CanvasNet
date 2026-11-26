using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for <see cref="MathMlDocument"/>: the open variants, their argument checks, strict UTF-8, the
///     description properties, and the failures of measuring and rendering.
/// </summary>
public class MathMlDocumentTests
{
    /// <summary>
    ///     Validates that a null text is rejected.
    /// </summary>
    [Fact]
    public void MathMlDocument_Parse_NullText_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => MathMlDocument.Parse(null!));
    }

    /// <summary>
    ///     Validates that a null, empty or whitespace path and a null stream are rejected.
    /// </summary>
    [Fact]
    public void MathMlDocument_Open_NullArguments_AreRejected()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => MathMlDocument.Open((string)null!));
        Assert.Throws<ArgumentException>(() => MathMlDocument.Open(" "));
        Assert.Throws<ArgumentNullException>(() => MathMlDocument.Open((Stream)null!));
    }

    /// <summary>
    ///     Validates that a stream that cannot be read is rejected.
    /// </summary>
    [Fact]
    public void MathMlDocument_Open_UnreadableStream_ThrowsArgumentException()
    {
        // Arrange
        using var stream = new UnreadableStream();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => MathMlDocument.Open(stream));
    }

    /// <summary>
    ///     Validates that a missing file is reported as such.
    /// </summary>
    [Fact]
    public void MathMlDocument_Open_MissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"canvasnet-mathml-missing-{Guid.NewGuid():N}.mml");

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => MathMlDocument.Open(path));
    }

    /// <summary>
    ///     Validates that the UTF-8 byte-order mark of a stream is dropped and the stream is left open.
    /// </summary>
    [Fact]
    public void MathMlDocument_Open_StreamWithByteOrderMark_DropsIt()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("<math><mi>x</mi></math>")).ToArray();
        using var stream = new MemoryStream(bytes);

        // Act
        var document = MathMlDocument.Open(stream);

        // Assert
        Assert.Equal(2, document.ElementCount);
        Assert.True(stream.CanRead);
    }

    /// <summary>
    ///     Validates that bytes that are not UTF-8 are refused, never replaced.
    /// </summary>
    [Fact]
    public void MathMlDocument_Open_InvalidUtf8_ThrowsSyntaxException()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("<math><mi>x</mi></math>").Concat(new byte[] { 0xC3, 0x28 }).ToArray();
        using var stream = new MemoryStream(bytes);

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Open(stream, new MathMlOpenOptions { SourceName = "bytes.mml" }));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal("bytes.mml", ex.Location.SourceName);
        Assert.Contains("UTF-8", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the description of a document without a display attribute, alternative text or annotations, and
    ///     that an invalid display value reads as inline.
    /// </summary>
    [Fact]
    public void MathMlDocument_Parse_DescriptionDefaults_AreInlineAndEmpty()
    {
        // Act
        var plain = MathMlDocument.Parse("<math><mn>1</mn></math>");
        var invalid = MathMlDocument.Parse("<math display=\"wide\"><mn>1</mn></math>");

        // Assert
        Assert.Equal((MathMlDisplay.Inline, null, 0, null), (plain.Display, plain.AltText, plain.Annotations.Count, plain.SourceName));
        Assert.Equal(MathMlDisplay.Inline, invalid.Display);
    }

    /// <summary>
    ///     Validates the description of a document that sets them all: a block display, the number of MathML elements
    ///     (the content of annotations excluded), the text alternative, and the annotations with their encodings.
    /// </summary>
    [Fact]
    public void MathMlDocument_Parse_Description_ReadsDisplayCountAltTextAndAnnotations()
    {
        // Arrange
        const string text = "<math display=\"block\" alttext=\"x squared\"><semantics><msup><mi>x</mi><mn>2</mn></msup>" +
            "<annotation encoding=\"application/x-tex\">x^2</annotation></semantics></math>";

        // Act
        var document = MathMlDocument.Parse(text, new MathMlOpenOptions { SourceName = "d.mml" });

        // Assert
        Assert.Equal((MathMlDisplay.Block, 6, "x squared", "d.mml"), (document.Display, document.ElementCount, document.AltText, document.SourceName));
        Assert.Equal([new MathMlAnnotation("application/x-tex", "x^2")], document.Annotations);
    }

    /// <summary>
    ///     Validates the picture frame: the content box united with the ink of a glyph that overhangs it, rounded out to
    ///     whole pixels at a fractional scale, with the baseline on the row of the origin of the math box, equal for
    ///     <c>Measure</c> and <c>Render</c>.
    /// </summary>
    [Fact]
    public void MathMlDocument_Measure_OverhangingGlyph_IsRoundedOutWithBaselineRow()
    {
        // Arrange: an italic f overhangs its advance; scale 1.5 makes the edges fall between pixels
        var document = MathMlDocument.Parse("<math><mi>f</mi></math>");
        var options = new MathMlRenderOptions { Scale = 1.5 };
        var box = document.LayoutBox(options, TestContext.Current.CancellationToken);
        var content = box.ContentRect;
        var extent = MathMlDocument.PictureExtent(box, 1.5);
        var expectedWidth = (int)(Math.Ceiling(extent.Right * 1.5) - Math.Floor(extent.X * 1.5));
        var expectedHeight = (int)(Math.Ceiling(extent.Bottom * 1.5) - Math.Floor(extent.Y * 1.5));
        var expectedBaseline = (int)-Math.Floor(extent.Y * 1.5);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert: the ink reaches outside the content box, and the picture holds both
        Assert.True(extent.X < content.X || extent.Right > content.Right || extent.Y < content.Y || extent.Bottom > content.Bottom);
        Assert.Equal(new MathMlSize(expectedWidth, expectedHeight, expectedBaseline), size);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.Contains(Enumerable.Range(0, surface.Width), x => surface[x, size.Baseline - 1].A > 0);
    }

    /// <summary>
    ///     Validates that a picture holds all the ink of tall stretched constructions, antialiasing included, with its
    ///     outermost rows and columns clear: nested stretched parentheses around a sum and fractions, radicals around
    ///     shallow and tall content, and a display integral with infinite limits, rendered at their natural size at
    ///     scales from 0.5 to 6 equal an unclipped painting of the same layout into a larger surface, keep the baseline
    ///     on the row of the origin, and fitted into exact sizes keep their edges clear too.
    /// </summary>
    [Fact]
    public void MathMlDocument_Render_TallStretchedInk_IsNeverCutAtTheEdges()
    {
        // Arrange: the ink of each reaches past its content box (fences, the tail of a radical, the limits of an integral)
        string[] equations =
        [
            "<mrow><mo>(</mo><mrow><mo>(</mo><munderover><mo>&#x2211;</mo><mrow><mi>k</mi><mo>=</mo><mn>1</mn></mrow>"
                + "<mi>m</mi></munderover><mfrac><msup><mi>x</mi><mi>k</mi></msup><mi>k</mi></mfrac><mo>)</mo></mrow>"
                + "<mo>&#x2212;</mo><mfrac><msup><mi>x</mi><mrow><mi>n</mi><mo>+</mo><mn>1</mn></mrow></msup>"
                + "<mrow><mi>n</mi><mo>+</mo><mn>1</mn></mrow></mfrac><mo>)</mo></mrow>",
            "<msub><mi>&#x3C9;</mi><mi>d</mi></msub><mo>=</mo><msub><mi>&#x3C9;</mi><mi>n</mi></msub>"
                + "<msqrt><mn>1</mn><mo>&#x2212;</mo><msup><mi>&#x3B6;</mi><mn>2</mn></msup></msqrt>",
            "<msqrt><mfrac><mfrac><mn>1</mn><mi>y</mi></mfrac><mfrac><mi>g</mi><mrow><mn>1</mn><mo>&#x2212;</mo>"
                + "<msup><mi>&#x3B6;</mi><mn>2</mn></msup></mrow></mfrac></mfrac></msqrt>",
            "<mi>F</mi><mo>=</mo><msubsup><mo>&#x222B;</mo><mrow><mo>&#x2212;</mo><mi>&#x221E;</mi></mrow><mi>&#x221E;</mi>"
                + "</msubsup><mi>f</mi><mo>(</mo><mi>t</mi><mo>)</mo><mspace width=\"0.1667em\"/><mi>d</mi><mi>t</mi>",
        ];
        const int Margin = 8;
        var token = TestContext.Current.CancellationToken;

        foreach (var equation in equations)
        {
            var document = MathMlDocument.Parse(MathMlTestHelpers.Math(equation, "display=\"block\""));
            foreach (var scale in new[] { 0.5, 1, 1.5, 2, 3, 6 })
            {
                // Act: the natural picture, and the same layout painted unclipped with a margin around the frame
                var options = new MathMlRenderOptions { Scale = scale };
                var size = document.Measure(options, token);
                using var picture = document.Render(options, token);
                var box = document.LayoutBox(options, token);
                var extent = MathMlDocument.PictureExtent(box, scale);
                var originX = -Math.Floor((extent.X * scale) + 1e-6);
                var originY = -Math.Floor((extent.Y * scale) + 1e-6);
                using var unclipped = new Surface(picture.Width + (2 * Margin), picture.Height + (2 * Margin));
                MathMlPainter.Paint(unclipped, box, new MathPlacement(scale, Margin + originX, Margin + originY), null, 10_000, token);

                // Assert: the unclipped painting is the picture (within the rounding of the moved pen positions), nothing
                // more, and the edges of the picture are clear
                var label = $"{equation} at scale {scale}";
                Assert.Equal((size.Width, size.Height, size.Baseline), (picture.Width, picture.Height, (int)originY));
                for (var y = 0; y < unclipped.Height; y++)
                {
                    for (var x = 0; x < unclipped.Width; x++)
                    {
                        var inside = x >= Margin && x < Margin + picture.Width && y >= Margin && y < Margin + picture.Height;
                        var expected = inside ? picture[x - Margin, y - Margin].A : 0;
                        var tolerance = inside ? 4 : 0;
                        Assert.True(Math.Abs(expected - unclipped[x, y].A) <= tolerance, $"{label}: pixel {x - Margin},{y - Margin} differs from the unclipped painting");
                    }
                }

                Assert.True(EdgesClear(picture), $"{label}: ink on the outermost rows or columns of {picture.Width}x{picture.Height}");
                Assert.True(MathMlTestHelpers.InkedPixels(picture) > 0, label);
            }

            // Act / Assert: fitted into exact sizes, larger, smaller, and of other aspect ratios, the edges stay clear
            var natural = document.Measure(cancellationToken: token);
            foreach (var (width, height) in new[]
            {
                (natural.Width * 4, natural.Height * 4), (natural.Width * 2, natural.Height * 5),
                ((natural.Width * 3) + 7, natural.Height), (natural.Width / 2, natural.Height / 2),
            })
            {
                using var fitted = document.Render(width, height, cancellationToken: token);
                Assert.True(EdgesClear(fitted), $"{equation} fitted into {width}x{height}: ink on the outermost rows or columns");
                Assert.True(MathMlTestHelpers.InkedPixels(fitted) > 0);
            }
        }
    }

    /// <summary>
    ///     Validates the fail-closed rule: a defect of the layout or the painting (here an index error thrown while the
    ///     result is produced) becomes a located <c>mathml-render-failed</c> render exception with the original exception
    ///     inside, the reports found before it are still delivered, and cancellation and a disposed object pass unchanged.
    /// </summary>
    [Fact]
    public void MathMlDocument_Run_LayoutDefect_BecomesLocatedRenderFailure()
    {
        // Arrange: a link is a report of reading, which is delivered even though the call fails
        var document = MathMlDocument.Parse("\n<math><mi href=\"#\">x</mi></math>", new MathMlOpenOptions { SourceName = "defect.mml" });
        var delivered = new List<string>();
        var options = new MathMlRenderOptions { UnsupportedFeatureHandler = report => delivered.Add(report.Feature) };
        var defect = new IndexOutOfRangeException("defect");
        var token = TestContext.Current.CancellationToken;

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Run<int>(options, token, _ => throw defect));
        var canceled = Assert.Throws<OperationCanceledException>(() => document.Run<int>(options, token, _ => throw new OperationCanceledException("stop")));
        var disposed = Assert.Throws<ObjectDisposedException>(() => document.Run<int>(options, token, _ => throw new ObjectDisposedException("surface")));

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.RenderFailed, new MathMlLocation("defect.mml", 2, 2)), (ex.Code, ex.Location));
        Assert.Same(defect, ex.InnerException);
        Assert.Contains("IndexOutOfRangeException", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
        Assert.Equal("stop", canceled.Message);
        Assert.Equal("surface", disposed.ObjectName);
        Assert.Equal([MathMlDiagnosticCodes.AttributeUnsupported, MathMlDiagnosticCodes.AttributeUnsupported, MathMlDiagnosticCodes.AttributeUnsupported], delivered);
    }

    /// <summary>
    ///     Validates that an empty equation measures one pixel and renders transparent.
    /// </summary>
    [Fact]
    public void MathMlDocument_Measure_EmptyMath_IsOnePixel()
    {
        // Arrange
        var document = MathMlDocument.Parse("<math/>");

        // Act
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new MathMlSize(1, 1, 0), size);
        Assert.True(MathMlTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates that the layout work budget fails a call with a located limit error that names the property.
    /// </summary>
    [Fact]
    public void MathMlDocument_Render_LayoutWorkLimit_ThrowsRenderException()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math(string.Concat(Enumerable.Repeat("<mi>x</mi>", 20))));
        var options = new MathMlRenderOptions { Limits = new MathMlLimits { MaxLayoutWork = 10 } };

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxLayoutWork", ex.Message, StringComparison.Ordinal);
        Assert.True(ex.Location.IsKnown);
    }

    /// <summary>
    ///     Validates that an equation larger than a surface can be fails with a limit error, and that the fitting overload
    ///     still renders it.
    /// </summary>
    [Fact]
    public void MathMlDocument_Measure_HugeSpace_ThrowsLimitExceeded()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<mspace width=\"100000px\" height=\"10px\"/>"));

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Measure(cancellationToken: TestContext.Current.CancellationToken));
        using var fitted = document.Render(100, 10, new MathMlRenderOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("Surface.MaxDimension", ex.Message, StringComparison.Ordinal);
        Assert.Equal((100, 10), (fitted.Width, fitted.Height));
    }

    /// <summary>
    ///     Validates that the natural size (for the image source) is the picture rounded out at the scale, without the
    ///     limit of a surface: it equals the measured size of a small equation and exceeds a surface for a huge one.
    /// </summary>
    [Fact]
    public void MathMlDocument_MeasureNatural_BeyondSurface_ReturnsTheSize()
    {
        // Arrange
        var small = MathMlDocument.Parse(MathMlTestHelpers.Math("<mi>x</mi><mo>+</mo><mn>1</mn>"));
        var huge = MathMlDocument.Parse(MathMlTestHelpers.Math("<mspace width=\"100000px\" height=\"10px\"/>"));
        var options = new MathMlRenderOptions { Scale = 2 };

        // Act
        var measured = small.Measure(options, TestContext.Current.CancellationToken);
        var natural = small.MeasureNatural(options, TestContext.Current.CancellationToken);
        var beyond = huge.MeasureNatural(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((measured.Width, measured.Height), natural);
        Assert.Equal((100_000, 10), beyond);
    }

    /// <summary>
    ///     Validates that a handler that throws after a successful call propagates its exception, and that the reports
    ///     are delivered after a failed call although the handler throws.
    /// </summary>
    [Fact]
    public void MathMlDocument_Render_HandlerFailures_FollowTheDeliveryRules()
    {
        // Arrange
        var document = MathMlDocument.Parse(MathMlTestHelpers.Math("<menclose><mi>a</mi><mi>b</mi></menclose>"));
        var calls = 0;
        var throwing = new MathMlRenderOptions { UnsupportedFeatureHandler = _ => throw new InvalidOperationException("handler") };
        var failing = new MathMlRenderOptions
        {
            UnsupportedFeatureHandler = _ =>
            {
                calls++;
                throw new InvalidOperationException("handler");
            },
            Limits = new MathMlLimits { MaxPaintedGlyphs = 1 },
        };

        // Act
        var handlerError = Assert.Throws<InvalidOperationException>(() => document.Render(throwing, TestContext.Current.CancellationToken));
        var renderError = Assert.Throws<MathMlRenderException>(() => document.Render(failing, TestContext.Current.CancellationToken));

        // Assert: the failed call delivered its report (the handler ran) and the render failure is the one thrown
        Assert.Equal("handler", handlerError.Message);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, renderError.Code);
        Assert.Equal(1, calls);
    }

    /// <summary>
    ///     Validates the classification of the exceptions that escape the layout: runtime defects and malformed font data
    ///     become render failures; the documented failures, cancellation, and a disposed object pass unchanged.
    /// </summary>
    [Fact]
    public void MathMlDocument_IsLayoutDefect_ClassifiesExceptions()
    {
        // Act / Assert
        Assert.True(MathMlDocument.IsLayoutDefect(new InvalidDataException()));
        Assert.True(MathMlDocument.IsLayoutDefect(new IndexOutOfRangeException()));
        Assert.True(MathMlDocument.IsLayoutDefect(new ArithmeticException()));
        Assert.False(MathMlDocument.IsLayoutDefect(new MathMlRenderException()));
        Assert.False(MathMlDocument.IsLayoutDefect(new MathMlUnsupportedFeatureException()));
        Assert.False(MathMlDocument.IsLayoutDefect(new OperationCanceledException()));
        Assert.False(MathMlDocument.IsLayoutDefect(new ObjectDisposedException("surface")));
    }

    /// <summary>
    ///     Gets whether the outermost rows and columns of a surface are fully transparent.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>Whether no pixel of the edges has ink.</returns>
    private static bool EdgesClear(Surface surface)
    {
        var last = surface.Height - 1;
        var right = surface.Width - 1;
        for (var x = 0; x < surface.Width; x++)
        {
            if (surface[x, 0].A != 0 || surface[x, last].A != 0)
            {
                return false;
            }
        }

        for (var y = 0; y < surface.Height; y++)
        {
            if (surface[0, y].A != 0 || surface[right, y].A != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     A stream that cannot be read.
    /// </summary>
    private sealed class UnreadableStream : MemoryStream
    {
        /// <inheritdoc/>
        public override bool CanRead => false;
    }
}
