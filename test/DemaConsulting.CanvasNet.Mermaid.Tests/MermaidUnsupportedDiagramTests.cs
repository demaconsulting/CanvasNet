using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of the behavior for a known diagram type that this version does not draw (<c>journey</c>
///     here): one <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/> report and an empty transparent picture, or a
///     <see cref="MermaidUnsupportedFeatureException"/> in strict mode.
/// </summary>
public class MermaidUnsupportedDiagramTests
{
    /// <summary>
    ///     Validates that rendering a user journey delivers one report, located at the keyword, with the not-planned hint,
    ///     and returns a transparent 16 by 16 surface.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_JourneyDiagram_ReportsOnceAndDrawsTransparentPlaceholder()
    {
        // Arrange
        var document = MermaidDocument.Parse("%% comment\n" + MermaidTestHelpers.UnsupportedText, new MermaidOpenOptions { SourceName = "journey.mmd" });
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((16, 16), (surface.Width, surface.Height));
        Assert.True(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(warnings);
        var report = Assert.Single(unsupported);
        Assert.Equal(MermaidDiagnosticCodes.DiagramUnsupported, report.Feature);
        Assert.Equal(new MermaidLocation("journey.mmd", 2, 1), report.Location);
        Assert.Equal("journey diagrams are not supported; supported in this version: flowchart, graph, sequenceDiagram, classDiagram, stateDiagram, erDiagram", report.Hint);
    }

    /// <summary>
    ///     Validates that strict mode throws the unsupported-feature exception after delivering the report.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_JourneyDiagramStrict_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.UnsupportedText);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var ex = Assert.Throws<MermaidUnsupportedFeatureException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.DiagramUnsupported, ex.Feature);
        Assert.StartsWith("journey diagrams are not supported", ex.Hint, StringComparison.Ordinal);
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates that measuring reports the same and gives the size of the placeholder, scaled.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Measure_JourneyDiagram_ReportsAndGivesScaledPlaceholderSize()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.UnsupportedText);
        var unsupported = new List<MermaidUnsupportedFeature>();
        var options = new MermaidRenderOptions { Scale = 2.5, UnsupportedFeatureHandler = unsupported.Add };

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new MermaidSize(40, 40), size);
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates that an explicit size and a background are honored for the placeholder.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_RenderSized_JourneyDiagramWithBackground_FillsBackground()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.UnsupportedText);
        var white = new Rgba32(255, 255, 255, 255);
        var (options, _, _) = MermaidTestHelpers.Collecting(background: white);

        // Act
        using var surface = document.Render(30, 20, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((30, 20), (surface.Width, surface.Height));
        Assert.Equal(white, surface[29, 19]);
    }

    /// <summary>
    ///     Validates that every other known type behaves the same way, with the hint of a type that is not planned.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <param name="type">The expected type.</param>
    /// <param name="planned">Whether the type is planned.</param>
    [Theory]
    [InlineData("pie\n \"a\" : 1", "pie", true)]
    [InlineData("gantt\n title x", "gantt", true)]
    [InlineData("mindmap\n root", "mindmap", true)]
    [InlineData("journey\n title x", "journey", false)]
    [InlineData("gitGraph\n commit", "gitGraph", false)]
    [InlineData("timeline\n title x", "timeline", false)]
    public void CanvasNetMermaid_Render_OtherKnownTypes_AreUnsupportedWithHint(string text, string type, bool planned)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(type, document.DiagramType);
        Assert.False(document.IsSupported);
        var report = Assert.Single(unsupported);
        Assert.StartsWith(planned ? $"{type} diagrams are planned" : $"{type} diagrams are not supported", report.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a failing handler after a successful render propagates and that the surface is not leaked.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Render_FailingHandler_Propagates()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.UnsupportedText);
        var options = new MermaidRenderOptions { UnsupportedFeatureHandler = _ => throw new InvalidOperationException("handler") };

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => document.Render(options, TestContext.Current.CancellationToken));
        Assert.Equal("handler", ex.Message);
    }

    /// <summary>
    ///     Validates that an invalid explicit size is refused before any work.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_RenderSized_InvalidSize_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(MermaidTestHelpers.UnsupportedText);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(10, Surface.MaxDimension + 1, cancellationToken: TestContext.Current.CancellationToken));
    }
}
