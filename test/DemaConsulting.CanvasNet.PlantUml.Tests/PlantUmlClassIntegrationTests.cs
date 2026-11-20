using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for class and object diagrams: the public API from text to pixels.
/// </summary>
public class PlantUmlClassIntegrationTests
{
    /// <summary>
    ///     Gets the names of the class fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> ClassFixtures() => PlantUmlReferenceShapeTests.FixturesOf("class");

    /// <summary>
    ///     Gets the names of the object fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> ObjectFixtures() => PlantUmlReferenceShapeTests.FixturesOf("object");

    /// <summary>
    ///     Validates that a class diagram is painted with PlantUML's default colors: a white background, light grey
    ///     classes, the green spot of a class, and the purple spot of an interface.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Class_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nclass Car\ninterface Drivable\nDrivable <|.. Car\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var box = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "class" && p.Id == "Car")])!.Value;
        var spot = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "class-spot" && p.Id == "Car")])!.Value;
        var interfaceSpot = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "class-spot" && p.Id == "Drivable")])!.Value;

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SkinColors.White, surface.GetRowSpan(1)[1]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(box.Y + box.Height - 4))[(int)(box.X + 15)]);
        Assert.Equal(new Canvas.Rgba32(0xAD, 0xD1, 0xB2, 255), surface.GetRowSpan((int)(spot.Y + 3))[(int)(spot.X + (spot.Width / 2))]);
        Assert.Equal(new Canvas.Rgba32(0xB4, 0xA7, 0xE5, 255), surface.GetRowSpan((int)(interfaceSpot.Y + 3))[(int)(interfaceSpot.X + (interfaceSpot.Width / 2))]);
    }

    /// <summary>
    ///     Validates that every original class fixture renders with the features it uses drawn: no unsupported feature,
    ///     no warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(ClassFixtures))]
    public void CanvasNetPlantUml_Class_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("class", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates that every original object fixture renders with the features it uses drawn, as an object diagram.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(ObjectFixtures))]
    public void CanvasNetPlantUml_Object_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "object", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("object", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.True(surface.Width > 20 && surface.Height > 20);
    }

    /// <summary>
    ///     Validates the facts of class and object diagrams: entities, links (note links not counted), packages, and the
    ///     title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Class_Facts_CountEntitiesLinksAndPackages()
    {
        // Act
        var packages = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", "09-packages.puml"));
        var notes = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", "10-notes.puml"));
        var page = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", "13-page.puml"));
        var objects = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "object", "02-map.puml"));

        // Assert
        Assert.Equal((6, 4, 7), (packages.NodeCount, packages.EdgeCount, packages.SubgraphCount));
        Assert.Equal((2, 1, 0), (notes.NodeCount, notes.EdgeCount, notes.SubgraphCount));
        Assert.Equal("Billing model", page.Title);
        Assert.Equal((2, 1, 0), (objects.NodeCount, objects.EdgeCount, objects.SubgraphCount));
    }

    /// <summary>
    ///     Validates that two renders of the same document have identical pixels, and that the diagram's scale
    ///     multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Class_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", "06-relations.puml"));
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
    ///     without them, and fail in strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Class_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nclass A\nclass B\nA }o-- B\nA \"r\" --> /role B\ntogether {\nclass C\n}\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([4, 5, 6], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.True(surface.Width > 20);
    }

    /// <summary>
    ///     Validates that a malformed class diagram fails with a located syntax error naming the line, the column, and a
    ///     hint, and that mixing an element of another diagram type is refused with the <c>allowmixing</c> hint.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Class_SyntaxErrors_AreLocated()
    {
        // Act
        var unclosed = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nclass A\nclass B {\n+ x : int\n@enduml\n"));
        var mixed = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nclass A\nactor B\n@enduml\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, unclosed.Code);
        Assert.Equal((3, 1), (unclosed.Location.Line, unclosed.Location.Column));
        Assert.Contains("}", unclosed.Hint, StringComparison.Ordinal);
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, mixed.Code);
        Assert.Equal(3, mixed.Location.Line);
        Assert.Contains("allowmixing", mixed.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled render stops with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Class_Render_ObservesCancellation()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", "09-packages.puml"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that an object diagram draws its objects without spots and its map as a table of its entries.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Object_MapAndObject_AreDrawn()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "object", "02-map.puml"));

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var model = Assert.IsType<ClassModel>(document.Diagram);

        // Assert
        Assert.DoesNotContain(scene.Items, i => i.Role == "class-spot");
        Assert.Equal(["http", "https", "ssh"], scene.Items.OfType<SceneText>().Where(t => t.Role == "map-key").Select(t => string.Concat(t.Runs.Select(r => r.Text))));
        Assert.Equal(["80", "443", "22"], scene.Items.OfType<SceneText>().Where(t => t.Role == "map-value").Select(t => string.Concat(t.Runs.Select(r => r.Text))));
        Assert.Equal(ClassKind.Map, model.Entities.Single(e => e.Id == "Ports").Kind);
    }
}
