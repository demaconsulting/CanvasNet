using System.Text;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Tests of <see cref="MermaidImageSource"/>: a Mermaid file as an embedded picture, its <c>theme</c> fragment
///     parameter and its notes.
/// </summary>
public class MermaidImageSourceTests
{
    /// <summary>
    ///     Creates a request for a diagram.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <param name="fragment">The fragment, or null.</param>
    /// <param name="width">The requested width, or null.</param>
    /// <param name="height">The requested height, or null.</param>
    /// <param name="strict">Whether an unsupported feature throws.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(string text, string? fragment = null, int? width = null, int? height = null, bool strict = false) => new()
    {
        Href = "diagram.mmd",
        FileName = "diagram.mmd",
        Content = Encoding.UTF8.GetBytes(text),
        Fragment = ImageFragment.Parse(fragment),
        PixelWidth = width,
        PixelHeight = height,
        ThrowOnUnsupportedFeature = strict,
        CancellationToken = TestContext.Current.CancellationToken,
    };

    /// <summary>
    ///     Validates what the source claims: the two extensions, no scheme and the <c>theme</c> parameter.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Properties_ClaimMermaidExtensionsAndTheme()
    {
        // Arrange
        var source = new MermaidImageSource();

        // Act / Assert
        Assert.Equal("mermaid", source.Name);
        Assert.Equal([".mmd", ".mermaid"], source.Extensions);
        Assert.Empty(source.Schemes);
        Assert.Equal(["theme"], source.FragmentParameters);
        Assert.Empty(((IFragmentImageSource)source).FragmentParameterPrefixes);
    }

    /// <summary>
    ///     Validates that a diagram type that is not drawn gives a transparent picture at the requested size and an
    ///     unsupported note with the location.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_UnsupportedType_ReturnsTransparentPictureAndNote()
    {
        // Arrange
        var source = new MermaidImageSource();

        // Act
        var result = source.Render(Request(MermaidTestHelpers.SequenceText, width: 32, height: 24));
        using var surface = result.Surface;

        // Assert
        Assert.Equal((32, 24), (surface.Width, surface.Height));
        Assert.True(MermaidTestHelpers.IsFullyTransparent(surface));
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(("mermaid", ImageDiagnosticSeverity.Unsupported, MermaidDiagnosticCodes.DiagramUnsupported), (note.Source, note.Severity, note.Code));
        Assert.Equal(new ImageDiagnosticLocation("diagram.mmd", 1, 1, null), note.Location);
    }

    /// <summary>
    ///     Validates that a flowchart is drawn through the same pipeline as a document render, at its natural size and at
    ///     a requested size.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_Flowchart_DrawsTheDiagram()
    {
        // Arrange
        var source = new MermaidImageSource();
        var natural = MermaidDocument.Parse(MermaidTestHelpers.FlowchartText).Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = source.Render(Request(MermaidTestHelpers.FlowchartText));
        using var surface = result.Surface;
        var sized = source.Render(Request(MermaidTestHelpers.FlowchartText, "theme=dark", 400, 140));
        using var sizedSurface = sized.Surface;

        // Assert
        Assert.Equal((natural.Width, natural.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(result.Diagnostics);
        Assert.Equal((400, 140), (sizedSurface.Width, sizedSurface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(sizedSurface));
    }

    /// <summary>
    ///     Validates that a diagram whose natural size is larger than the largest surface is drawn at a smaller requested
    ///     width, with the height from its natural aspect ratio (the natural size is only needed for the ratio).
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_NaturalSizeBeyondMaximum_DrawsAtRequestedWidth()
    {
        // Arrange: a long chain from left to right, wider than the largest surface
        var source = new MermaidImageSource();
        var text = "flowchart LR\n    " + string.Join(" --> ", Enumerable.Range(0, 120).Select(i => $"n{i}[Step {i}]")) + "\n";
        var document = MermaidDocument.Parse(text);
        var natural = document.MeasureNatural(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = source.Render(Request(text, width: 200));
        using var surface = result.Surface;

        // Assert
        Assert.True(natural.Width > DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension);
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Measure(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(200, surface.Width);
        Assert.Equal(Math.Max(1, (int)Math.Round(natural.Height * 200.0 / natural.Width, MidpointRounding.AwayFromZero)), surface.Height);
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
    }

    /// <summary>
    ///     Validates that strict requests throw the unsupported-feature exception.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_UnsupportedTypeStrict_Throws()
    {
        // Arrange
        var source = new MermaidImageSource();

        // Act / Assert
        Assert.Throws<MermaidUnsupportedFeatureException>(() => source.Render(Request(MermaidTestHelpers.SequenceText, strict: true)));
    }

    /// <summary>
    ///     Validates that a valid theme is accepted and an invalid one refuses the link.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_ThemeFragment_ValidatedAgainstMermaidThemes()
    {
        // Arrange
        var source = new MermaidImageSource();

        // Act
        using var dark = source.Render(Request(MermaidTestHelpers.SequenceText, "theme=dark")).Surface;
        var ex = Assert.Throws<ImageSourceRefusedException>(() => source.Render(Request(MermaidTestHelpers.SequenceText, "theme=purple")));

        // Assert
        Assert.Equal((16, 16), (dark.Width, dark.Height));
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
        Assert.Contains("purple", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that invalid text fails with a located syntax exception.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_InvalidText_ThrowsSyntaxException()
    {
        // Arrange
        var source = new MermaidImageSource();

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => source.Render(Request("not a diagram")));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.UnknownDiagram, ex.Code);
        Assert.Equal("diagram.mmd", ex.Location.File);
    }

    /// <summary>
    ///     Validates that a null request is refused.
    /// </summary>
    [Fact]
    public void MermaidImageSource_Render_NullRequest_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new MermaidImageSource().Render(null!));
    }
}
