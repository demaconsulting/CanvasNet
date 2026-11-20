using DemaConsulting.CanvasNet.PlantUml.Description;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for use case, component, and deployment diagrams: the public API from text to pixels.
/// </summary>
public class PlantUmlDescriptionIntegrationTests
{
    /// <summary>
    ///     Gets the names of the component fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> ComponentFixtures() => PlantUmlReferenceShapeTests.FixturesOf("component");

    /// <summary>
    ///     Gets the names of the use case fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> UseCaseFixtures() => PlantUmlReferenceShapeTests.FixturesOf("usecase");

    /// <summary>
    ///     Gets the names of the deployment fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> DeploymentFixtures() => PlantUmlReferenceShapeTests.FixturesOf("deployment");

    /// <summary>
    ///     Validates that a use case diagram is painted with PlantUML's default colors: a white background, a light grey
    ///     actor head, and a light grey use case ellipse.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nactor Customer\nCustomer --> (Place Order)\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var head = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "description" && p.Id == "Customer")])!.Value;
        var ellipse = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "description" && p.Id == "Place Order")])!.Value;

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("usecase", document.DiagramType);
        Assert.Equal(SkinColors.White, surface.GetRowSpan(1)[1]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(head.Y + (head.Height / 2)))[(int)(head.X + (head.Width / 2)) + 3]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(ellipse.Y + 3))[(int)(ellipse.X + (ellipse.Width / 2))]);
    }

    /// <summary>
    ///     Validates that every original use case fixture renders with the features it uses drawn: no unsupported
    ///     feature, no warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(UseCaseFixtures))]
    public void CanvasNetPlantUml_UseCase_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("usecase", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates the facts of use case diagrams: actors and use cases, links (note links not counted), boundaries,
    ///     and the title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_Facts_CountElementsLinksAndContainers()
    {
        // Act
        var basic = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", "01-basic.puml"));
        var boundary = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", "03-boundary.puml"));
        var notes = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", "06-notes.puml"));
        var page = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", "07-left-to-right-page.puml"));

        // Assert
        Assert.Equal((6, 5, 0), (basic.NodeCount, basic.EdgeCount, basic.SubgraphCount));
        Assert.Equal((7, 5, 3), (boundary.NodeCount, boundary.EdgeCount, boundary.SubgraphCount));
        Assert.Equal((3, 3, 0), (notes.NodeCount, notes.EdgeCount, notes.SubgraphCount));
        Assert.Equal((5, 4, 1), (page.NodeCount, page.EdgeCount, page.SubgraphCount));
        Assert.Equal("Use Case Page", page.Title);
    }

    /// <summary>
    ///     Validates that two renders of the same use case document have identical pixels, and that the diagram's scale
    ///     multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", "03-boundary.puml"));
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
    ///     Validates that the statements of a use case diagram that are read but not drawn are reported at their lines
    ///     with a stable code, drawn without them, and fail in strict mode: a stereotype of a link, a URL, and
    ///     <c>hide</c>; the actor style is a skin parameter warning.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nactor A\nA --> (U) <<uses>>\n(V) [[http://x]]\nhide A\nskinparam actorStyle awesome\n@enduml\n");
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([3, 4, 5], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.Equal(PlantUmlDiagnosticCodes.SkinParamIgnored, Assert.Single(warnings).Code);
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.True(surface.Width > 20);
    }

    /// <summary>
    ///     Validates that a malformed use case diagram fails with a located syntax error naming the line and a hint: an
    ///     actor that holds elements and a use case declared again as an actor (both refused by PlantUML), and a
    ///     statement of a class diagram.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_SyntaxErrors_AreLocated()
    {
        // Act
        var holder = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nusecase U\nactor A {\n(V)\n}\n@enduml\n"));
        var redefined = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\n(U) --> (V)\nactor U\n@enduml\n"));
        var mixing = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nusecase U\nclass C\n@enduml\n"));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (holder.Code, holder.Location.Line));
        Assert.False(string.IsNullOrEmpty(holder.Hint));
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (redefined.Code, redefined.Location.Line));
        Assert.Contains("already defined", redefined.Message, StringComparison.Ordinal);
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (mixing.Code, mixing.Location.Line));
        Assert.Contains("allowmixing", mixing.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled render of a use case diagram stops with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_Render_ObservesCancellation()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "usecase", "03-boundary.puml"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that the names only written in the links of a use case diagram are drawn as actors (a stick figure),
    ///     as PlantUML draws them, and that a use case in a boundary is found by its short name.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_UseCase_BareNames_BecomeActors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nrectangle Shop {\nusecase Pay\n}\nCustomer --> Pay\n@enduml\n");

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var model = Assert.IsType<DescriptionModel>(document.Diagram);

        // Assert
        Assert.Equal(("Customer", "Shop.Pay"), (model.Links[0].From.Id, model.Links[0].To.Id));
        Assert.Equal(DescriptionSymbol.Actor, model.Elements.Single(e => e.Id == "Customer").Symbol);
        Assert.Equal(2, scene.Items.Count(i => i.Id == "Customer" && i is ScenePath));
        Assert.Contains(scene.Items, i => i.Role == "description" && i.Id == "Shop.Pay");
    }

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
        var document = PlantUmlDocument.Parse("@startuml\ncomponent N {\nport p\n}\n[A] -(0)- [B]\naction X\n@enduml\n");
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

    /// <summary>
    ///     Validates that a deployment diagram is painted with PlantUML's default colors: a white background and light
    ///     grey nodes, queues, and boundaries (the symbols the sequence diagrams fill grey-blue are light grey here).
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nnode Server\nqueue Jobs\nboundary Edge\nServer --> Jobs\nEdge --> Server\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var boxes = new[] { "Server", "Jobs", "Edge" }.Select(id => Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "description" && p.Id == id)])!.Value).ToList();

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        var (node, queue, boundary) = (Middle(boxes[0], 0), Middle(boxes[1], 0), Middle(boxes[2], 6));
        Canvas.Rgba32 Middle(SceneRect box, int dx) => surface.GetRowSpan((int)(box.Y + (box.Height / 2)))[(int)(box.X + (box.Width / 2)) + dx];

        // Assert
        Assert.Equal("deployment", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), node);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), queue);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), boundary);
    }

    /// <summary>
    ///     Validates that every original deployment fixture renders with the features it uses drawn: no unsupported
    ///     feature, no warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(DeploymentFixtures))]
    public void CanvasNetPlantUml_Deployment_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("deployment", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates the facts of deployment diagrams: the leaf elements (notes not counted), the links, the containers,
    ///     and the title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_Facts_CountElementsLinksAndContainers()
    {
        // Act
        var nodes = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "01-nodes.puml"));
        var containers = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "03-containers.puml"));
        var notes = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "05-notes-colors.puml"));
        var page = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "06-left-to-right.puml"));

        // Assert
        Assert.Equal((4, 3, 0), (nodes.NodeCount, nodes.EdgeCount, nodes.SubgraphCount));
        Assert.Equal((6, 3, 8), (containers.NodeCount, containers.EdgeCount, containers.SubgraphCount));
        Assert.Equal((10, 5, 1), (notes.NodeCount, notes.EdgeCount, notes.SubgraphCount));
        Assert.Equal((5, 4, 1), (page.NodeCount, page.EdgeCount, page.SubgraphCount));
        Assert.Equal("Deployment", page.Title);
    }

    /// <summary>
    ///     Validates that two renders of the same deployment document have identical pixels, and that the diagram's
    ///     scale multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "03-containers.puml"));
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
    ///     Validates that the parts of a deployment diagram that are read but not drawn are reported at their lines with
    ///     a stable code, drawn without them (an action and a process container as rectangles), and fail in strict
    ///     mode: an action and a process (a port would make it a component diagram).
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nnode N\naction A\nprocess P {\nartifact X\n}\nA --> X\nN --> P\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var scene = document.LayoutScene(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("deployment", document.DiagramType);
        Assert.Equal([3, 4], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.All(unsupported, u => Assert.Contains("drawn as a rectangle", u.Message, StringComparison.Ordinal));
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.Contains(scene.Items, i => i.Role == "description" && i.Id == "A");
        Assert.Contains(scene.Items, i => i.Role == "cluster" && i.Id == "P");
        Assert.Contains(scene.Items, i => i.Role == "link");
    }

    /// <summary>
    ///     Validates that a malformed deployment diagram fails with a located syntax error naming the line and a hint:
    ///     a person that holds elements (PlantUML refuses it too), an unclosed container, and a statement of a class
    ///     diagram.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_SyntaxErrors_AreLocated()
    {
        // Act
        var holder = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nnode N\nperson P {\nnode Q\n}\n@enduml\n"));
        var unclosed = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nnode N\nstack S {\nnode Q\n@enduml\n"));
        var mixing = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nnode N\nclass C\n@enduml\n"));

        // Assert
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (holder.Code, holder.Location.Line));
        Assert.False(string.IsNullOrEmpty(holder.Hint));
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (unclosed.Code, unclosed.Location.Line));
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, 3), (mixing.Code, mixing.Location.Line));
        Assert.Contains("allowmixing", mixing.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled render of a deployment diagram stops with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_Render_ObservesCancellation()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "03-containers.puml"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that every deployment symbol has its own drawing: an outline for each element but a label (a hexagon
    ///     with the outline PlantUML's Graphviz pipeline draws), no report, and a circle drawn as an interface.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Deployment_Symbols_HaveTheirOwnDrawing()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deployment", "02-symbols.puml"));
        var circle = PlantUmlDocument.Parse("@startuml\nnode N\ncircle C\nN --> C\n@enduml\n");

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var circleScene = circle.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var model = Assert.IsType<DescriptionModel>(document.Diagram);

        // Assert
        Assert.Empty(model.Reports);
        var outlined = scene.Items.Where(i => i.Role == "description").Select(i => i.Id!).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(model.Elements.Where(e => e.Symbol != DescriptionSymbol.Label).Select(e => e.Id).Order(StringComparer.Ordinal), outlined);
        Assert.Contains(scene.Items, i => i is SceneText { Role: "description-name", Id: "Label" });
        var ring = circleScene.Items.OfType<ScenePath>().Single(p => p.Role == "description" && p.Id == "C");
        Assert.Equal(2 * DescriptionRenderer.InterfaceRadius, Scene.BoundsOf([ring])!.Value.Width, 3);
    }
}
