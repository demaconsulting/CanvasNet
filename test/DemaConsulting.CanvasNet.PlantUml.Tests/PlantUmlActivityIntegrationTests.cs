using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for activity diagrams: the public API from text to pixels.
/// </summary>
public class PlantUmlActivityIntegrationTests
{
    /// <summary>
    ///     Gets the names of the activity fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> Fixtures() => PlantUmlReferenceLayoutTests.ActivityFixtures();

    /// <summary>
    ///     Validates that an activity diagram is painted with PlantUML's default colors: a white background, light
    ///     grey actions, and a dark start node.
    /// </summary>
    [Fact]
    [Trait("Category", "Platform")]
    public void CanvasNetPlantUml_Activity_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nstart\n:Open the file;\nstop\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var box = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "activity")])!.Value;
        var start = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "start")])!.Value;

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SkinColors.White, surface.GetRowSpan(1)[1]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(box.Y + box.Height - 4))[(int)(box.X + 15)]);
        Assert.Equal(new Canvas.Rgba32(0x22, 0x22, 0x22, 255), surface.GetRowSpan((int)(start.Y + (start.Height / 2)))[(int)(start.X + (start.Width / 2))]);
    }

    /// <summary>
    ///     Validates that every original fixture renders with the features it uses drawn: no unsupported feature, no
    ///     warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetPlantUml_Activity_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("activity", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates the facts of an activity diagram: nodes, arrows, groups and the title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Activity_Facts_CountNodesArrowsAndGroups()
    {
        // Act
        var page = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity", "16-page.puml"));
        var partition = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity", "12-partition.puml"));

        // Assert
        Assert.Equal("Order handling", page.Title);
        Assert.Equal((3, 2, 0), (page.NodeCount, page.EdgeCount, page.SubgraphCount));
        Assert.Equal((5, 4, 2), (partition.NodeCount, partition.EdgeCount, partition.SubgraphCount));
    }

    /// <summary>
    ///     Validates that two renders of the same document have identical pixels, and that the diagram's scale
    ///     multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Activity_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity", "08-fork.puml"));
        var document = PlantUmlDocument.Parse(text);
        var scaled = PlantUmlDocument.Parse(text.Replace("@startuml\n", "@startuml\nscale 2\n", StringComparison.Ordinal));

        // Act
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        var size = scaled.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(PlantUmlTestHelpers.PixelsEqual(first, second));
        Assert.Equal((first.Width * 2, first.Height * 2), (size.Width, size.Height));
    }

    /// <summary>
    ///     Validates that statements that are not drawn are reported at their lines, drawn without them, and fail in
    ///     strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Activity_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nstart\n:one|\ngoto there\n:two;\nstop\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([3, 4], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.True(surface.Width > 20);
    }

    /// <summary>
    ///     Validates that a malformed diagram fails with a located syntax error naming the line, the column and a hint.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Activity_SyntaxError_IsLocated()
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nstart\nif (x) then\n:a;\nstop\n@enduml\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal((3, 1), (ex.Location.Line, ex.Location.Column));
        Assert.Contains("endif", ex.Hint, StringComparison.Ordinal);
    }
}
