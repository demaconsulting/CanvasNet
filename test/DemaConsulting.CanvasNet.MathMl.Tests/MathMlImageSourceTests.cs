using System.Text;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for <see cref="MathMlImageSource"/>: a <c>.mml</c> file as an embedded picture, its <c>display</c>
///     fragment parameter, its notes, its sizes and its failures.
/// </summary>
public class MathMlImageSourceTests
{
    /// <summary>A small equation with a large operator, whose height depends on the display mode.</summary>
    private const string Sum = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><munderover><mo>&#x2211;</mo><mi>i</mi><mi>n</mi></munderover><mi>x</mi></math>";

    /// <summary>
    ///     Validates what the source claims: the <c>.mml</c> extension only, no scheme and the <c>display</c> parameter.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Properties_ClaimMmlAndTheDisplayFragment()
    {
        // Arrange
        var source = new MathMlImageSource();

        // Act / Assert
        Assert.Equal("mathml", source.Name);
        Assert.Equal([".mml"], source.Extensions);
        Assert.Empty(source.Schemes);
        Assert.Equal(["display"], source.FragmentParameters);
        Assert.Equal("display", MathMlImageSource.DisplayParameter);
        Assert.Equal("mathml-warning", MathMlImageSource.WarningCode);
    }

    /// <summary>
    ///     Validates that an equation is drawn on a transparent background at its natural size (the size
    ///     <see cref="MathMlDocument.Measure"/> gives) and at a requested size.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_DrawsTheEquationOnTransparent()
    {
        // Arrange
        var source = new MathMlImageSource();
        var natural = MathMlDocument.Parse(Sum).Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = source.Render(Request(Sum));
        using var surface = result.Surface;
        var sized = source.Render(Request(Sum, width: 120, height: 90));
        using var sizedSurface = sized.Surface;

        // Assert
        Assert.Equal((natural.Width, natural.Height), (surface.Width, surface.Height));
        Assert.True(MathMlTestHelpers.InkedPixels(surface) > 0);
        Assert.Equal(0, surface.GetRowSpan(0)[0].A);
        Assert.Empty(result.Diagnostics);
        Assert.Equal((120, 90), (sizedSurface.Width, sizedSurface.Height));
        Assert.True(MathMlTestHelpers.InkedPixels(sizedSurface) > 0);
    }

    /// <summary>
    ///     Validates that the <c>display</c> fragment sets the display mode (ASCII case-insensitive), overriding the
    ///     attribute of the <c>math</c> element.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_DisplayFragment_SetsBlockOrInline()
    {
        // Arrange
        var source = new MathMlImageSource();
        var block = MathMlDocument.Parse(Sum).Measure(new MathMlRenderOptions { Display = MathMlDisplay.Block }, TestContext.Current.CancellationToken);
        var inline = MathMlDocument.Parse(Sum).Measure(new MathMlRenderOptions { Display = MathMlDisplay.Inline }, TestContext.Current.CancellationToken);

        // Act
        using var asBlock = source.Render(Request(Sum, "display=block")).Surface;
        using var asUpperBlock = source.Render(Request(Sum, "display=BLOCK")).Surface;
        using var asInline = source.Render(Request(Sum.Replace("<math ", "<math display=\"block\" ", StringComparison.Ordinal), "display=inline")).Surface;

        // Assert
        Assert.True(block.Height > inline.Height);
        Assert.Equal((block.Width, block.Height), (asBlock.Width, asBlock.Height));
        Assert.Equal((block.Width, block.Height), (asUpperBlock.Width, asUpperBlock.Height));
        Assert.Equal((inline.Width, inline.Height), (asInline.Width, asInline.Height));
    }

    /// <summary>
    ///     Validates that a <c>display</c> value other than <c>block</c> or <c>inline</c> refuses the link before the file
    ///     is read (even text that is not MathML is not parsed).
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_InvalidDisplay_IsRefused()
    {
        // Arrange
        var source = new MathMlImageSource();

        // Act
        var ex = Assert.Throws<ImageSourceRefusedException>(() => source.Render(Request("not MathML", "display=wide")));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
        Assert.Contains("wide", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the notes: a warning has the code <c>mathml-warning</c> with the specific code in the detail, an
    ///     unsupported feature its own code, both located; a strict request throws at the first unsupported feature.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_ReportsNotesAndStrictMode()
    {
        // Arrange
        var source = new MathMlImageSource();
        const string Text = "<math>\n<mtd><mi>a</mi></mtd>\n<mi fontweight=\"bold\">x</mi>\n</math>";

        // Act
        var result = source.Render(Request(Text));
        using var surface = result.Surface;

        // Assert
        Assert.Equal(2, result.Diagnostics.Count);
        var warning = result.Diagnostics[0];
        Assert.Equal(("mathml", ImageDiagnosticSeverity.Warning, MathMlImageSource.WarningCode, MathMlDiagnosticCodes.ChildCount), (warning.Source, warning.Severity, warning.Code, warning.Detail));
        Assert.Equal(new ImageDiagnosticLocation("equation.mml", 2, 2, null), warning.Location);
        var unsupported = result.Diagnostics[1];
        Assert.Equal((ImageDiagnosticSeverity.Unsupported, MathMlDiagnosticCodes.AttributeUnsupported), (unsupported.Severity, unsupported.Code));
        Assert.Equal(3, unsupported.Location!.Line);
        Assert.Throws<MathMlUnsupportedFeatureException>(() => source.Render(Request(Text, strict: true)));
    }

    /// <summary>
    ///     Validates that an equation whose natural size is larger than the largest surface is drawn at a smaller
    ///     requested width, with the height from its natural aspect ratio.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_NaturalSizeBeyondMaximum_DrawsAtRequestedWidth()
    {
        // Arrange: a space wider than the largest surface
        var source = new MathMlImageSource();
        const string Text = "<math><mi>x</mi><mspace width=\"10000px\" height=\"20px\" depth=\"20px\" mathbackground=\"red\"/></math>";
        var document = MathMlDocument.Parse(Text);

        // Act
        var result = source.Render(Request(Text, width: 200));
        using var surface = result.Surface;

        // Assert
        var ex = Assert.Throws<MathMlRenderException>(() => document.Measure(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(200, surface.Width);
        Assert.True(surface.Height is >= 1 and < 10);
        Assert.True(MathMlTestHelpers.InkedPixels(surface) > 0);
    }

    /// <summary>
    ///     Validates that invalid text fails with a syntax exception located in the file.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_InvalidText_ThrowsSyntaxException()
    {
        // Arrange
        var source = new MathMlImageSource();

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => source.Render(Request("<math><mi>x</math>")));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal("equation.mml", ex.Location.SourceName);
    }

    /// <summary>
    ///     Validates that a null request is refused.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_NullRequest_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new MathMlImageSource().Render(null!));
    }

    /// <summary>
    ///     Validates that a canceled request stops before any work.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_PreCanceled_Throws()
    {
        // Arrange
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var request = Request(Sum, cancellationToken: canceled.Token);

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => new MathMlImageSource().Render(request));
    }

    /// <summary>
    ///     Validates that a surface over the pixel budget of the request is refused before it is allocated.
    /// </summary>
    [Fact]
    public void MathMlImageSource_Render_OverBudget_IsRefused()
    {
        // Arrange
        var request = Request(Sum, width: 2000, height: 2000, budget: new ImageBudget(1000));

        // Act / Assert
        Assert.Throws<ImageSourceBudgetException>(() => new MathMlImageSource().Render(request));
    }

    /// <summary>
    ///     Creates a request for an equation.
    /// </summary>
    /// <param name="text">The MathML text.</param>
    /// <param name="fragment">The fragment, or null.</param>
    /// <param name="width">The requested width, or null.</param>
    /// <param name="height">The requested height, or null.</param>
    /// <param name="strict">Whether an unsupported feature throws.</param>
    /// <param name="budget">The pixel budget, or null for none.</param>
    /// <param name="cancellationToken">The cancellation token, or none for the test's.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string text, string? fragment = null, int? width = null, int? height = null, bool strict = false, ImageBudget? budget = null, CancellationToken? cancellationToken = null) => new()
    {
        Budget = budget,
        Href = "equation.mml",
        FileName = "equation.mml",
        Content = Encoding.UTF8.GetBytes(text),
        Fragment = ImageFragment.Parse(fragment),
        PixelWidth = width,
        PixelHeight = height,
        ThrowOnUnsupportedFeature = strict,
        CancellationToken = cancellationToken ?? TestContext.Current.CancellationToken,
    };
}
