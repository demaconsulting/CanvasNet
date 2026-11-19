namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System tests for the diagram types that are recognized but not drawn: the empty, transparent picture, the report,
///     and strict mode.
/// </summary>
public class PlantUmlUnsupportedDiagramTests
{
    /// <summary>
    ///     Validates that a type that is not drawn renders an empty, transparent 20 by 20 picture and reports
    ///     <c>plantuml-diagram-unsupported</c> at the construct it was recognized by.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UnsupportedType_RendersEmptyPictureAndReports()
    {
        // Arrange
        var document = PlantUmlDocument.Parse(PlantUmlTestHelpers.UnsupportedText, new PlantUmlOpenOptions { SourceName = "flow.puml" });
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((20, 20), (surface.Width, surface.Height));
        Assert.True(PlantUmlTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(warnings);
        var report = Assert.Single(unsupported);
        Assert.Equal((PlantUmlDiagnosticCodes.DiagramUnsupported, new PlantUmlLocation("flow.puml", 2, 1)), (report.Feature, report.Location));
        Assert.Contains("activity-legacy", report.Message, StringComparison.Ordinal);
        Assert.NotNull(report.Hint);
    }

    /// <summary>
    ///     Validates that measuring a type that is not drawn reports the same as rendering it.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UnsupportedType_MeasureReportsAsRender()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startmindmap\n* root\n@endmindmap\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new PlantUmlSize(20, 20), size);
        Assert.Equal(PlantUmlDiagnosticCodes.DiagramUnsupported, Assert.Single(unsupported).Feature);
    }

    /// <summary>
    ///     Validates that strict mode throws for a type that is not drawn after delivering its report.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UnsupportedType_StrictThrows()
    {
        // Arrange
        var document = PlantUmlDocument.Parse(PlantUmlTestHelpers.UnsupportedText);
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.DiagramUnsupported, ex.Feature);
        Assert.Single(unsupported);
    }
}
