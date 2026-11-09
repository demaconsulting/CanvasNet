using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     System tests of class diagrams through the public API: every fixture renders strictly, the pixels carry the
///     theme's colors, the classifiers style their members, the metadata is reported, the scale and the look behave, and
///     rendering is deterministic and can be canceled.
/// </summary>
public class MermaidClassIntegrationTests
{
    /// <summary>A small diagram: a class with an attribute, a static and an abstract method, an extension and a composition.</summary>
    private const string Simple =
        "classDiagram\n    class Animal {\n        +String name\n        +count()$ int\n        +move()*\n    }\n    class Dog\n    class Cat\n" +
        "    Animal <|-- Dog\n    Animal *-- Cat\n";

    /// <summary>
    ///     Gets the names of the class fixtures.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class"), "*.mmd").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every class fixture renders in strict mode, draws something, gives no report, and measures as it
    ///     renders.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMermaid_ClassDiagram_Fixture_RendersStrictly(string name)
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", name + ".mmd"));
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("class", document.DiagramType);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        Assert.False(MermaidTestHelpers.IsFullyTransparent(surface));
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates the metadata of a class diagram: the classes, relations and namespaces are counted (notes, lollipop
    ///     interfaces and note links are not), and the direction is the diagram's.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_Metadata_CountsClassesRelationsAndNamespaces()
    {
        // Arrange
        const string Text = "classDiagram\n    direction LR\n    namespace Shapes {\n        class A\n    }\n    class B\n" +
            "    A --> B\n    B --() Runnable\n    note for A \"a note\"\n";

        // Act
        var document = MermaidDocument.Parse(Text);

        // Assert
        Assert.Equal(("class", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((2, 2, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Equal("LR", document.Direction);
        Assert.Null(document.Title);
    }

    /// <summary>
    ///     Validates the default theme's colors in the pixels: the class box fill <c>#ECECFF</c>, the divider in the
    ///     node border <c>#9370DB</c>, the composition marker filled with the line color <c>#333333</c> and the extension
    ///     marker left hollow.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_DefaultTheme_PaintsMermaidsColors()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var scene = SceneQuery.Layout(Simple);
        var parts = SceneQuery.Parts(scene)["classId-Animal-0"];
        var box = parts.First(part => part.Kind == "path").Box;
        var divider = parts.First(part => part.Kind == "divider").Box;
        var composition = MarkerBox(scene, "id_Animal_Cat_2");
        var extension = MarkerBox(scene, "id_Animal_Dog_1");

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 4 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), Pixel(surface, scene, box.X + 3, box.Y + 3, 4));
        Assert.Equal(new Rgba32(0x93, 0x70, 0xDB, 255), Pixel(surface, scene, divider.X + 6, divider.Y + 0.05, 4));
        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), Pixel(surface, scene, composition.X + (composition.Width / 2), composition.Y + (composition.Height / 2), 4));
        Assert.Equal(0, Pixel(surface, scene, extension.X + (extension.Width / 2), extension.Y + (extension.Height / 2), 4).A);
    }

    /// <summary>
    ///     Validates that the theme changes the class box fill (the theme's <c>mainBkg</c>).
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="r">The expected red.</param>
    /// <param name="g">The expected green.</param>
    /// <param name="b">The expected blue.</param>
    [Theory]
    [InlineData("default", 0xEC, 0xEC, 0xFF)]
    [InlineData("dark", 0x1F, 0x20, 0x20)]
    [InlineData("forest", 0xCD, 0xE4, 0x98)]
    [InlineData("neutral", 0xEE, 0xEE, 0xEE)]
    public void CanvasNetMermaid_ClassDiagram_Theme_ChangesTheBoxFill(string theme, byte r, byte g, byte b)
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var options = new MermaidRenderOptions { Scale = 2, Theme = theme };
        var scene = SceneQuery.Layout(Simple, options);
        var box = SceneQuery.Parts(scene)["classId-Animal-0"].First(part => part.Kind == "path").Box;

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new Rgba32(r, g, b, 255), Pixel(surface, scene, box.X + 3, box.Y + 3, 2));
    }

    /// <summary>
    ///     Validates the classifiers: a static member is underlined and an abstract member is italic, and a class's
    ///     <c>style</c> fills its box.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_ClassifiersAndStyles_StyleTheBox()
    {
        // Arrange
        const string Text = Simple + "    style Dog fill:#ff0000\n";
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);
        var runs = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().SelectMany(text => text.Runs).ToList();
        var dog = SceneQuery.Parts(scene)["classId-Dog-1"].First(part => part.Kind == "path").Box;

        // Act
        using var surface = document.Render(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(runs, run => run.Text.Contains("count()", StringComparison.Ordinal) && run.Underline && !run.Font.Italic);
        Assert.Contains(runs, run => run.Text.Contains("move()", StringComparison.Ordinal) && run.Font.Italic && !run.Underline);
        Assert.Contains(runs, run => run.Text == "Animal" && run.Font.Bold);
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), Pixel(surface, scene, dog.X + 3, dog.Y + 3, 2));
    }

    /// <summary>
    ///     Validates that a look other than classic is reported once and drawn classic, and that strict mode refuses it.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_HandDrawnLook_IsReportedAndDrawnClassic()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  look: handDrawn\n---\n" + Simple);
        var classic = MermaidDocument.Parse(Simple);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();
        var (strict, _, _) = MermaidTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var expected = classic.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var report = Assert.Single(unsupported);
        Assert.Equal(MermaidDiagnosticCodes.LookUnsupported, report.Feature);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
        Assert.Throws<MermaidUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that a layout other than dagre is reported once and laid out with dagre.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_ElkLayout_IsReportedAndLaidOutWithDagre()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\nconfig:\n  layout: elk\n---\n" + Simple);
        var dagre = MermaidDocument.Parse(Simple);
        var (options, _, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        using var expected = dagre.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LayoutUnsupported, Assert.Single(unsupported).Feature);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
    }

    /// <summary>
    ///     Validates that the legacy id (a <c>classDiagram</c> with another <c>class.defaultRenderer</c>) is drawn as the
    ///     same diagram.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_LegacyRenderer_IsDrawnTheSame()
    {
        // Arrange
        var legacy = MermaidDocument.Parse("---\nconfig:\n  class:\n    defaultRenderer: dagre-d3\n---\n" + Simple);
        var current = MermaidDocument.Parse(Simple);

        // Act
        using var surface = legacy.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var expected = current.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("class", legacy.DiagramType);
        Assert.True(MermaidTestHelpers.PixelsEqual(expected, surface));
    }

    /// <summary>
    ///     Validates that the scale multiplies the natural size, which is the view box's.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_Scale_MultipliesTheNaturalSize()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        var viewBox = SceneQuery.Layout(Simple).ViewBox;

        // Act
        var natural = document.Measure(cancellationToken: TestContext.Current.CancellationToken);
        var doubled = document.Measure(new MermaidRenderOptions { Scale = 2 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(((int)Math.Ceiling(viewBox.Width), (int)Math.Ceiling(viewBox.Height)), (natural.Width, natural.Height));
        Assert.Equal((natural.Width * 2, natural.Height * 2), (doubled.Width, doubled.Height));
    }

    /// <summary>
    ///     Validates that repeated and concurrent renders of a document give identical pixels.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_RepeatedAndConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = MermaidDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "class", "10-namespaces.mmd"));
        using var first = document.Render(cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var concurrent = Enumerable.Range(0, 6).AsParallel().Select(_ =>
        {
            using var surface = document.Render();
            return MermaidTestHelpers.PixelsEqual(first, surface);
        }).ToList();

        // Assert
        Assert.True(MermaidTestHelpers.PixelsEqual(first, second));
        Assert.All(concurrent, Assert.True);
    }

    /// <summary>
    ///     Validates that a canceled token stops a class diagram render.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_CanceledToken_Throws()
    {
        // Arrange
        var document = MermaidDocument.Parse(Simple);
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a layout that needs more work than allowed fails with the limit code instead of running on.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_WorkLimit_FailsWithLimitExceeded()
    {
        // Arrange: many related classes and a small work limit
        var text = "classDiagram\n" + string.Concat(Enumerable.Range(0, 60).Select(i => $"    C{i} --> C{i + 1}\n"));
        var document = MermaidDocument.Parse(text);
        var small = new MermaidRenderOptions { Limits = new MermaidLimits { MaxLayoutWork = 2_000 } };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => document.Measure(small, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a style property this version does not draw is a warning located at its class, while the styles the diagram
    ///     writes itself (the note colors, the lollipop interface's opacity) are not reported.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_ClassDiagram_UserStyleIgnored_IsReportedButDiagramStylesAreNot()
    {
        // Arrange
        var document = MermaidDocument.Parse("classDiagram\n    class A\n    A --() Runnable\n    note for A \"note\"\n    style A cursor:pointer\n");
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.StyleIgnored, 2), (warning.Code, warning.Location.Line));
        Assert.Empty(unsupported);
    }

    /// <summary>
    ///     Gets the box of the markers of an edge.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="edgeId">The edge id.</param>
    /// <returns>The box.</returns>
    private static SceneRect MarkerBox(Scene scene, string edgeId)
    {
        SceneRect? box = null;
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is ScenePath { Role: "marker" } path && path.Id == edgeId && Scene.BoundsOf([path]) is { } b)
            {
                box = box is { } current ? current.Union(b.Offset(dx, dy)) : b.Offset(dx, dy);
            }
        }

        return box!.Value;
    }

    /// <summary>
    ///     Reads the pixel under a point of the scene.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="scene">The scene (for its view box).</param>
    /// <param name="x">The scene x.</param>
    /// <param name="y">The scene y.</param>
    /// <param name="scale">The scale of the render.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 Pixel(Surface surface, Scene scene, double x, double y, double scale) =>
        surface.GetRowSpan((int)Math.Floor((y - scene.ViewBox.Y) * scale))[(int)Math.Floor((x - scene.ViewBox.X) * scale)];
}
