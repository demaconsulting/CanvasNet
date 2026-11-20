using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for component diagrams: the public API from text to pixels.
/// </summary>
public class PlantUmlDescriptionIntegrationTests
{
    /// <summary>
    ///     Gets the names of the component fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> ComponentFixtures() => PlantUmlReferenceShapeTests.FixturesOf("component");

    /// <summary>
    ///     Validates that a component diagram is painted with PlantUML's default colors: a white background, light grey
    ///     components, and light grey interface circles.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\n[Web Server] --> HTTP\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var box = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "description" && p.Id == "Web Server")])!.Value;
        var circle = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "description" && p.Id == "HTTP")])!.Value;

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("component", document.DiagramType);
        Assert.Equal(SkinColors.White, surface.GetRowSpan(1)[1]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(box.Y + box.Height - 4))[(int)(box.X + 6)]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(circle.Y + (circle.Height / 2)))[(int)(circle.X + (circle.Width / 2))]);
    }

    /// <summary>
    ///     Validates that every original component fixture renders with the features it uses drawn: no unsupported
    ///     feature, no warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(ComponentFixtures))]
    public void CanvasNetPlantUml_Component_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("component", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates the facts of component diagrams: leaf elements, links (note links not counted), containers, and the
    ///     title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_Facts_CountElementsLinksAndContainers()
    {
        // Act
        var basic = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", "01-basic.puml"));
        var containers = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", "03-containers.puml"));
        var notes = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", "05-notes.puml"));
        var page = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", "08-page-skinparam.puml"));

        // Assert
        Assert.Equal((5, 4, 0), (basic.NodeCount, basic.EdgeCount, basic.SubgraphCount));
        Assert.Equal((7, 5, 9), (containers.NodeCount, containers.EdgeCount, containers.SubgraphCount));
        Assert.Equal((3, 2, 0), (notes.NodeCount, notes.EdgeCount, notes.SubgraphCount));
        Assert.Equal((4, 3, 1), (page.NodeCount, page.EdgeCount, page.SubgraphCount));
        Assert.Equal("Component Page", page.Title);
    }

    /// <summary>
    ///     Validates that two renders of the same document have identical pixels, and that the diagram's scale
    ///     multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", "03-containers.puml"));
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
    ///     Validates that statements that are read but not drawn are reported at their lines with a stable code, drawn
    ///     without them, and fail in strict mode: a port, a middle decoration, and a symbol drawn as a rectangle.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\ncomponent N {\nport p\n}\n[A] -(0)- [B]\nartifact X\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([3, 5, 6], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.True(surface.Width > 20);
    }

    /// <summary>
    ///     Validates that a malformed component diagram fails with a located syntax error naming the line and a hint: a
    ///     statement of a class diagram (which PlantUML refuses without allowmixing), an element declared again with
    ///     another symbol (which PlantUML refuses as already defined), and an unclosed container.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_SyntaxErrors_AreLocated()
    {
        // Act
        var mixing = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\ncomponent A\nclass C\n@enduml\n"));
        var redefined = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\n[A] --> B\ncomponent B\n@enduml\n"));
        var unclosed = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\n[A]\nnode N {\n[B]\n@enduml\n"));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (mixing.Code, mixing.Location.Line));
        Assert.Contains("allowmixing", mixing.Hint, StringComparison.Ordinal);
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (redefined.Code, redefined.Location.Line));
        Assert.Contains("already defined", redefined.Message, StringComparison.Ordinal);
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (unclosed.Code, unclosed.Location.Line));
        Assert.Contains("}", unclosed.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled render stops with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_Render_ObservesCancellation()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "component", "03-containers.puml"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that the names of a component diagram resolve as PlantUML resolves them: a short name from outside
    ///     its container finds the element, and a name only written in links is an interface circle.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Component_Names_ResolveAsPlantUml()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nnode N {\ncomponent C\n}\n[C] --> D\n@enduml\n");

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var model = Assert.IsType<DescriptionModel>(document.Diagram);

        // Assert
        Assert.Equal(("N.C", "D"), (model.Links[0].From.Id, model.Links[0].To.Id));
        Assert.Equal(DescriptionSymbol.Interface, model.Elements.Single(e => e.Id == "D").Symbol);
        Assert.Contains(scene.Items, i => i.Role == "description" && i.Id == "N.C");
    }
}
