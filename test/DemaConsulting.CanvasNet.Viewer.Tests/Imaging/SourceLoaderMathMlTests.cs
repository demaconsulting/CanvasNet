using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the MathML source of the SourceLoader class: recognizing an equation, probing it, drawing it at its
///     natural, scaled and requested size, its background and display mode, the notes and strict mode, and the mapping of
///     the library's failures.
/// </summary>
public class SourceLoaderMathMlTests
{
    /// <summary>The white an equation is drawn over by default.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Copies the quadratic formula fixture into a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The file name of the copy.</param>
    /// <returns>The path of the copy.</returns>
    private static string CopyQuadratic(TempDirectory directory, string name = "quadratic.mml")
    {
        var path = directory.Combine(name);
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml")), path);
        return path;
    }

    /// <summary>
    ///     Tells whether a pixel is opaque, dark ink.
    /// </summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns><see langword="true"/> for an opaque pixel whose channels are all below 80.</returns>
    private static bool IsInk(Rgba32 pixel) => pixel.A == 255 && pixel.R < 80 && pixel.G < 80 && pixel.B < 80;

    /// <summary>
    ///     Counts the pixels of a surface that are dark ink.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountInk(Surface surface) =>
        Enumerable.Range(0, surface.Height).Sum(y => Enumerable.Range(0, surface.Width).Count(x => IsInk(surface[x, y])));

    /// <summary>
    ///     Test that the <c>.mml</c> extension is recognized by name in any case (even when the text holds an SVG element)
    ///     and that the kind is a vector equation named <c>mathml</c>, while <c>.mathml</c> is not a supported format.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_MathMlExtension_ReturnsMathMl()
    {
        // Arrange: an equation whose text holds an SVG element, under two cases of the name, and a plain equation under the
        // long extension
        using var directory = new TempDirectory();
        const string text = "<math><mtext>&lt;svg/&gt;</mtext><svg xmlns=\"http://www.w3.org/2000/svg\"/></math>";
        var lower = FixtureFactory.WriteText(directory.Path, "eq.mml", text);
        var upper = FixtureFactory.WriteText(directory.Path, "Eq.MML", text);
        var longName = FixtureFactory.WriteText(directory.Path, "eq.mathml", "<math><mi>x</mi></math>");

        // Act / Assert: the extension decides; the long extension is not recognized
        Assert.Equal(SourceKind.MathMl, SourceLoader.Detect(lower));
        Assert.Equal(SourceKind.MathMl, SourceLoader.Detect(upper));
        Assert.Equal(SourceKind.MathMl, SourceLoader.DetectContent("<svg xmlns='http://www.w3.org/2000/svg'/>"u8, ".mml"));
        Assert.Equal(ErrorCodes.UnsupportedFormat, Assert.Throws<ViewerException>(() => SourceLoader.Detect(longName)).Code);
        Assert.True(SourceKind.MathMl.IsVector());
        Assert.True(SourceKind.MathMl.IsEquation());
        Assert.False(SourceKind.MathMl.IsDiagram());
        Assert.False(SourceKind.MathMl.IsChart());
        Assert.False(SourceKind.MathMl.IsDocument());
        Assert.False(SourceKind.PlantUml.IsEquation());
        Assert.Equal("mathml", SourceKind.MathMl.ToFormatName());
        Assert.Equal(SourceKind.MathMl, SourceLoader.ExtensionKinds[".mml"]);
    }

    /// <summary>
    ///     Test that an equation is probed without drawing it: its natural size and baseline as the library measures them,
    ///     a single page with transparency, and the facts of its markup (display mode, element count, text alternative and
    ///     the encodings of its annotations).
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_MathMlEquation_ReportsEquationFacts()
    {
        // Arrange: the quadratic formula, and an equation with annotations in its own display mode (inline)
        using var directory = new TempDirectory();
        var quadratic = CopyQuadratic(directory);
        var annotated = FixtureFactory.WriteText(
            directory.Path,
            "annotated.mml",
            "<math><semantics><mi>x</mi><annotation encoding=\"application/x-tex\">x</annotation><annotation>plain</annotation></semantics></math>");

        // Act
        var info = SourceLoader.Probe(quadratic);
        var annotatedInfo = SourceLoader.Probe(annotated);

        // Assert: the kind, a single page with transparency, the size the library measures, and the facts
        var document = MathMlDocument.Open(quadratic);
        var measured = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(SourceKind.MathMl, info.Format);
        Assert.Equal((1, 4, true), (info.PageCount, info.Channels, info.HasAlpha));
        Assert.Equal((measured.Width, measured.Height), (info.Width, info.Height));
        var facts = Assert.IsType<EquationFacts>(info.Equation);
        Assert.Equal(("block", document.ElementCount, measured.Baseline), (facts.Display, facts.ElementCount, facts.Baseline));
        Assert.StartsWith("x equals minus b", facts.AltText, StringComparison.Ordinal);
        Assert.Empty(facts.AnnotationEncodings);
        Assert.Null(info.Diagram);
        Assert.Null(info.Chart);
        Assert.Equal("inline", annotatedInfo.Equation!.Display);
        Assert.Equal(["application/x-tex", string.Empty], annotatedInfo.Equation.AnnotationEncodings);
        Assert.Null(annotatedInfo.Equation.AltText);
    }

    /// <summary>
    ///     Test that an equation is drawn at its natural size over white, in black ink, with a render operation that gives
    ///     its size and no note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MathMlEquation_DrawsAtTheNaturalSizeOverWhite()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyQuadratic(directory);
        var info = SourceLoader.Probe(path);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert: the size, the white corner, the ink, and the operation
        Assert.Equal((info.Width, info.Height), (loaded.Surface.Width, loaded.Surface.Height));
        Assert.True(loaded.IsVector);
        Assert.Equal(White, loaded.Surface[0, 0]);
        Assert.True(CountInk(loaded.Surface) > 100);
        Assert.Empty(loaded.Diagnostics);
        var render = Assert.Single(loaded.Operations);
        Assert.Equal("render", render.Op);
        Assert.Equal(
            [new KeyValuePair<string, object>("width", info.Width), new KeyValuePair<string, object>("height", info.Height)],
            render.Fields);
    }

    /// <summary>
    ///     Test that a scale lays the equation out again at that size (the size the library measures at the scale, sharp
    ///     rather than magnified), that a box is fitted with the equation centered, and that the background of the options
    ///     replaces white (a transparent one keeps the picture transparent).
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MathMlSizeAndBackground_FollowTheOptions()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyQuadratic(directory);
        var scaled = MathMlDocument.Open(path).Measure(new MathMlRenderOptions { Scale = 3 }, TestContext.Current.CancellationToken);
        var yellow = new Rgba32(255, 255, 0, 255);

        // Act
        using var tripled = SourceLoader.Load(path, new SourceOptions { Scale = 3 });
        using var boxed = SourceLoader.Load(path, new SourceOptions { Width = 400, Height = 300, Background = yellow });
        using var transparent = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) });

        // Assert: the scaled size is the library's at that scale, the box is exact with the background in the margin
        Assert.Equal((scaled.Width, scaled.Height), (tripled.Surface.Width, tripled.Surface.Height));
        Assert.True(CountInk(tripled.Surface) > 900);
        Assert.Equal((400, 300), (boxed.Surface.Width, boxed.Surface.Height));
        Assert.Equal(yellow, boxed.Surface[0, 0]);
        Assert.Equal(yellow, boxed.Surface[200, 5]);
        Assert.True(CountInk(boxed.Surface) > 900);
        Assert.Contains(new KeyValuePair<string, object>("background", "#FFFF00"), Assert.Single(boxed.Operations).Fields);
        Assert.Equal(0, transparent.Surface[0, 0].A);
    }

    /// <summary>
    ///     Test that the display mode of the options lays the equation out again: the inline form of a summation is smaller
    ///     than the block form, the operation names the display mode, and a value outside the enumeration is refused before
    ///     the file is read.
    /// </summary>
    [Fact]
    public void SourceLoader_ProbeInputReference_MathMlDisplayFragment_ChoosesTheDisplay()
    {
        // Arrange: a summation with limits, which is much taller in display style
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "sum.mml",
            "<math><munderover><mo>&sum;</mo><mrow><mi>i</mi><mo>=</mo><mn>1</mn></mrow><mi>n</mi></munderover><mi>i</mi></math>");
        var info = SourceLoader.Probe(path);

        // Act
        using var inline = SourceLoader.Load(path, new SourceOptions());
        using var block = SourceLoader.Load(path, new SourceOptions { MathMl = new EquationOptions { Display = MathMlDisplay.Block } });
        var selected = SourceLoader.Probe(InputReference.Parse(path + "#display=BLOCK"));
        var refused = Assert.Throws<ViewerException>(
            () => SourceLoader.Load(path, new SourceOptions { MathMl = new EquationOptions { Display = (MathMlDisplay)7 } }));

        // Assert: the equation's own display mode is inline; the block form is taller and named in the operation
        Assert.Equal("inline", info.Equation!.Display);
        Assert.Equal((info.Width, info.Height), (inline.Surface.Width, inline.Surface.Height));
        Assert.True(block.Surface.Height > inline.Surface.Height);
        Assert.Contains(new KeyValuePair<string, object>("display", "block"), Assert.Single(block.Operations).Fields);
        Assert.Equal("block", block.Info.Equation!.Display);
        Assert.Equal((block.Surface.Width, block.Surface.Height), (selected.Width, selected.Height));
        Assert.Equal(MathMlDisplay.Block, selected.Selection!.MathMl!.Display);
        Assert.Equal(ErrorCodes.UsageError, refused.Code);
    }

    /// <summary>
    ///     Test that a warning of the library is the note <c>mathml-warning</c> with the specific code in the detail and the
    ///     position in the location, and that an element outside MathML Core is the unsupported note named by its feature,
    ///     with its position; both are drawn.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MathMl_MapsWarningsAndUnsupportedFeatures()
    {
        // Arrange: a length without a unit (a warning) and an mfenced (outside MathML Core) on the second line
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "notes.mml", "<math>\n<mspace width=\"3\"/><mfenced><mi>x</mi></mfenced>\n</math>");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert
        var warning = Assert.Single(loaded.Diagnostics, note => note.Severity == "warning");
        Assert.Equal(("mathml", MathMlImageSource.WarningCode, MathMlDiagnosticCodes.AttributeInvalid), (warning.Source, warning.Code, warning.Detail));
        Assert.Equal((path, 2), (warning.Location?.File, warning.Location?.Line));
        var unsupported = Assert.Single(loaded.Diagnostics, note => note.Severity == "unsupported");
        Assert.Equal(("mathml", MathMlDiagnosticCodes.ElementUnsupported), (unsupported.Source, unsupported.Code));
        Assert.Equal(2, unsupported.Location?.Line);
        Assert.Contains("mfenced", unsupported.Message, StringComparison.Ordinal);
        Assert.True(CountInk(loaded.Surface) > 10);
    }

    /// <summary>
    ///     Test that strict mode makes the first unsupported construct an <c>unsupported-feature</c> error with the feature
    ///     as the detail code and its location.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MathMlUnsupportedElementStrict_IsUnsupportedFeature()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "fenced.mml", "<math>\n  <mfenced><mi>x</mi></mfenced>\n</math>");

        // Act
        var ex = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert
        Assert.Equal((ErrorCodes.UnsupportedFeature, MathMlDiagnosticCodes.ElementUnsupported), (ex.Code, ex.DetailCode));
        Assert.Equal((path, 2), (ex.Location?.File, ex.Location?.Line));
        Assert.IsType<MathMlUnsupportedFeatureException>(ex.InnerException);
        Assert.Contains(SourceLoader.StrictModeHint, ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that text that is not a valid equation is a <c>mathml-error</c> located in the file with the code of the
    ///     library, an empty or blank file at line 1, column 1.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <param name="line">The expected line.</param>
    [Theory]
    [InlineData("<math><mi>x</mo></math>", 1)]
    [InlineData("<math>\n<mi>&notAnEntity;</mi>\n</math>", 2)]
    [InlineData("<html/>", 1)]
    [InlineData("", 1)]
    [InlineData("  \n  ", 1)]
    public void SourceLoader_Probe_MathMlInvalidText_IsMathMlError(string text, int line)
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "bad.mml", text);

        // Act
        var ex = Assert.Throws<ViewerException>(() => SourceLoader.Probe(path));

        // Assert
        Assert.Equal((ErrorCodes.MathMlError, MathMlDiagnosticCodes.SyntaxError), (ex.Code, ex.DetailCode));
        Assert.Equal((path, line), (ex.Location?.File, ex.Location?.Line));
        Assert.StartsWith("MathML syntax error: ", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
        Assert.IsType<MathMlSyntaxException>(ex.InnerException);
    }

    /// <summary>
    ///     Test that a display mode in the fragment of an input that is not block or inline is refused when the input is
    ///     probed, as <c>image-fragment-invalid</c>, and that a parameter an equation does not take is a warning.
    /// </summary>
    [Fact]
    public void SourceLoader_ProbeInputReference_MathMlInvalidDisplay_IsImageFragmentInvalid()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = CopyQuadratic(directory);

        // Act
        var ex = Assert.Throws<ViewerException>(() => SourceLoader.Probe(InputReference.Parse(path + "#display=wide")));
        var unknown = SourceLoader.Probe(InputReference.Parse(path + "#theme=dark"));

        // Assert
        Assert.Equal(ErrorCodes.ImageFragmentInvalid, ex.Code);
        Assert.Contains("display", ex.Message, StringComparison.Ordinal);
        Assert.Contains("wide", ex.Message, StringComparison.Ordinal);
        Assert.Null(unknown.Selection!.MathMl);
        Assert.Equal("image-fragment-unknown-parameter", Assert.Single(unknown.Selection.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a limit of the library met while the text is read is <c>budget-exceeded</c> with the code of the library
    ///     and a hint, and that a text longer than the limit is <c>corrupt-input</c> naming the file.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_MathMlLimits_AreBudgetExceededAndCorruptInput()
    {
        // Arrange: an equation nested deeper than the default limit, and a text longer than the default text limit
        using var directory = new TempDirectory();
        var nested = string.Concat(Enumerable.Repeat("<mrow>", 300)) + "<mi>x</mi>" + string.Concat(Enumerable.Repeat("</mrow>", 300));
        var deep = FixtureFactory.WriteText(directory.Path, "deep.mml", $"<math>{nested}</math>");
        var huge = FixtureFactory.WriteText(directory.Path, "huge.mml", "<math><mi>x</mi></math>" + new string(' ', MathMlLimits.Default.MaxTextSize + 1));

        // Act
        var budget = Assert.Throws<ViewerException>(() => SourceLoader.Probe(deep));
        var corrupt = Assert.Throws<ViewerException>(() => SourceLoader.Probe(huge));

        // Assert
        Assert.Equal((ErrorCodes.BudgetExceeded, MathMlDiagnosticCodes.LimitExceeded), (budget.Code, budget.DetailCode));
        Assert.Contains("MaxNestingDepth", budget.Message, StringComparison.Ordinal);
        Assert.NotNull(budget.Hint);
        Assert.Equal(ErrorCodes.CorruptInput, corrupt.Code);
        Assert.Contains("huge.mml", corrupt.Message, StringComparison.Ordinal);
        Assert.Contains("MaxTextSize", corrupt.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a failure of the library is mapped by its code: a limit is <c>budget-exceeded</c> and anything else
    ///     (a failed layout) is a <c>mathml-error</c>, each with the hint of the library (or a default for a limit).
    /// </summary>
    [Fact]
    public void SourceLoader_MapMathMl_MapsByCode()
    {
        // Arrange
        var limit = new MathMlRenderException(MathMlDiagnosticCodes.LimitExceeded, "the layout work exceeds MaxLayoutWork", new MathMlLocation("a.mml", 1, 1), "split it");
        var bare = new MathMlRenderException(MathMlDiagnosticCodes.LimitExceeded, "too many glyphs", new MathMlLocation("a.mml", 1, 1));
        var failed = new MathMlRenderException(MathMlDiagnosticCodes.RenderFailed, "the layout failed", new MathMlLocation("a.mml", 2, 3));

        // Act
        var mappedLimit = SourceLoader.MapMathMl(limit.Code, "render", limit.Message, limit.Hint, limit);
        var mappedBare = SourceLoader.MapMathMl(bare.Code, "render", bare.Message, bare.Hint, bare);
        var mappedFailed = SourceLoader.MapMathMl(failed.Code, "render", failed.Message, failed.Hint, failed);

        // Assert
        Assert.Equal((ErrorCodes.BudgetExceeded, "split it"), (mappedLimit.Code, mappedLimit.Hint));
        Assert.Same(limit, mappedLimit.InnerException);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, mappedLimit.DetailCode);
        Assert.Equal("Simplify the equation.", mappedBare.Hint);
        Assert.Equal(ErrorCodes.MathMlError, mappedFailed.Code);
        Assert.Equal("MathML render error: a.mml:2:3: mathml-render-failed: the layout failed", mappedFailed.Message);
        Assert.Equal(("a.mml", 2, 3), (mappedFailed.Location?.File, mappedFailed.Location?.Line, mappedFailed.Location?.Column));
        Assert.Null(mappedFailed.Hint);
    }

    /// <summary>
    ///     Test that an equation cut anywhere, or replaced by binary garbage, is an expected error or a smaller valid
    ///     equation, never an unexpected exception.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MathMlTruncatedOrDamaged_IsExpectedErrorOrLoads()
    {
        // Arrange: every prefix of the quadratic formula, and binary garbage
        using var directory = new TempDirectory();
        var text = File.ReadAllText(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml")));
        var inputs = Enumerable.Range(0, text.Length).Select(length => text[..length]).ToList();

        // Act / Assert: each one loads or fails with a code of the library mapping
        foreach (var input in inputs)
        {
            var path = FixtureFactory.WriteText(directory.Path, "cut.mml", input);
            var outcome = Record.Exception(() => SourceLoader.Load(path, new SourceOptions()).Dispose());
            if (outcome is not null)
            {
                var error = Assert.IsType<ViewerException>(outcome);
                Assert.Equal(ErrorCodes.MathMlError, error.Code);
                Assert.NotNull(error.Location);
            }
        }

        var garbage = directory.Combine("garbage.mml");
        File.WriteAllBytes(garbage, FixtureFactory.CreateGarbage(200));
        Assert.Equal(ErrorCodes.MathMlError, Assert.Throws<ViewerException>(() => SourceLoader.Load(garbage, new SourceOptions())).Code);
        File.WriteAllBytes(garbage, Encoding.UTF8.GetBytes(text));
        using var whole = SourceLoader.Load(garbage, new SourceOptions());
        Assert.True(whole.Surface.Width > 0);
    }
}
