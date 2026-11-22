using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.State;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for state diagrams: the public API from text to pixels.
/// </summary>
public class PlantUmlStateIntegrationTests
{
    /// <summary>
    ///     Gets the names of the state fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> StateFixtures() => PlantUmlReferenceShapeTests.FixturesOf("state");

    /// <summary>
    ///     Validates that a state diagram is painted with PlantUML's default colors: a white background, light grey
    ///     states, and the dark start circle.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_DefaultSkin_PaintsPlantUmlColors()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\n[*] --> Idle\nIdle --> Busy : go\n@enduml\n");
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var box = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "state" && p.Id == "Idle")])!.Value;
        var start = Scene.BoundsOf([scene.Items.OfType<ScenePath>().First(p => p.Role == "state-start")])!.Value;

        // Act
        using var surface = document.Render(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("state", document.DiagramType);
        Assert.Equal(SkinColors.White, surface.GetRowSpan(1)[1]);
        Assert.Equal(new Canvas.Rgba32(0xF1, 0xF1, 0xF1, 255), surface.GetRowSpan((int)(box.Y + box.Height - 8))[(int)(box.X + (box.Width / 2))]);
        Assert.Equal(new Canvas.Rgba32(0x22, 0x22, 0x22, 255), surface.GetRowSpan((int)(start.Y + (start.Height / 2)))[(int)(start.X + (start.Width / 2))]);
    }

    /// <summary>
    ///     Validates that every original state fixture renders with the features it uses drawn: no unsupported feature,
    ///     no warning, a picture of its natural size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(StateFixtures))]
    public void CanvasNetPlantUml_State_Fixtures_RenderWithoutReports(string name)
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", name + ".puml"));
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("state", document.DiagramType);
        Assert.True(document.IsSupported);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates the facts of state diagrams: states and pseudo-states, transitions (note links not counted),
    ///     composite states, and the title.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_Facts_CountStatesTransitionsAndComposites()
    {
        // Act
        var composite = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "04-composite.puml"));
        var concurrent = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "05-concurrent.puml"));
        var notes = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "08-notes.puml"));
        var page = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "13-page.puml"));

        // Assert
        Assert.Equal((9, 9, 2), (composite.NodeCount, composite.EdgeCount, composite.SubgraphCount));
        Assert.Equal((10, 9, 2), (concurrent.NodeCount, concurrent.EdgeCount, concurrent.SubgraphCount));
        Assert.Equal((3, 2, 0), (notes.NodeCount, notes.EdgeCount, notes.SubgraphCount));
        Assert.Equal("Door states", page.Title);
    }

    /// <summary>
    ///     Validates that two renders of the same document have identical pixels, and that the diagram's scale
    ///     multiplies the scale of the call.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_Render_IsDeterministicAndScaled()
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "05-concurrent.puml"));
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
    public void CanvasNetPlantUml_State_UnsupportedStatements_AreReported()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\nstate A {\nstate In <<entryPoint>>\nstate B\n}\nIn --> B\nA x--> C\nhide C\n@enduml\n");
        var (options, _, unsupported) = PlantUmlTestHelpers.Collecting();
        var (strict, _, _) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => document.Render(strict, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([3, 7, 8], unsupported.Select(u => u.Location.Line));
        Assert.All(unsupported, u => Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, u.Feature));
        Assert.Equal(PlantUmlDiagnosticCodes.StatementUnsupported, ex.Feature);
        Assert.True(surface.Width > 20);
    }

    /// <summary>
    ///     Validates that a malformed state diagram fails with a located syntax error naming the line and a hint: an
    ///     unclosed composite state, and a transition between two concurrent regions (which PlantUML refuses too).
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_SyntaxErrors_AreLocated()
    {
        // Act
        var unclosed = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\n[*] --> A\nstate A {\n[*] --> B\n@enduml\n"));
        var regions = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse("@startuml\nstate A {\n[*] --> B\n--\n[*] --> C\n}\nC --> D\n@enduml\n"));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, unclosed.Code);
        Assert.Equal(3, unclosed.Location.Line);
        Assert.Contains("}", unclosed.Hint, StringComparison.Ordinal);
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, regions.Code);
        Assert.Equal(7, regions.Location.Line);
        Assert.Contains("concurrent", regions.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled render stops with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_Render_ObservesCancellation()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "04-composite.puml"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that a composite state with concurrent regions draws its dashed separators: horizontal for
    ///     <c>--</c> and vertical for <c>||</c>, 1.5 wide with dashes of 8 and gaps of 10.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_ConcurrentRegions_DrawDashedSeparators()
    {
        // Arrange
        var document = PlantUmlDocument.Open(Path.Combine(AppContext.BaseDirectory, "Fixtures", "state", "05-concurrent.puml"));

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);
        var model = Assert.IsType<StateModel>(document.Diagram);

        // Assert
        var running = Scene.BoundsOf([scene.Items.OfType<ScenePath>().Single(p => p.Role == "cluster-separator" && p.Id == "Running")])!.Value;
        var panel = Scene.BoundsOf([scene.Items.OfType<ScenePath>().Single(p => p.Role == "cluster-separator" && p.Id == "Panel")])!.Value;
        Assert.Equal(0, running.Height, 6);
        Assert.Equal(0, panel.Width, 6);
        Assert.All(scene.Items.OfType<ScenePath>().Where(p => p.Role == "cluster-separator"), p =>
        {
            Assert.Equal(1.5, p.Stroke!.Width);
            Assert.Equal([8.0, 10.0], p.Stroke.Dash!);
        });
        Assert.Equal((2, 2), (model.Elements.Single(e => e.Id == "Running").Regions.Count, model.Elements.Single(e => e.Id == "Panel").Regions.Count));
    }

    /// <summary>
    ///     Validates that the transition labels keep clear of the lines and the states (the state-labels shape: a label
    ///     on a link into a state beside another, a label between two states of a row, and one inside a composite
    ///     state): every label is at least 3 pixels from every drawn line and arrowhead and at least 8 pixels from every
    ///     state, as PlantUML leaves about 4 and 10 pixels.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_State_TransitionLabels_ClearOfLinesAndStates()
    {
        // Arrange
        var document = PlantUmlDocument.Parse("@startuml\n[*] --> Draft\nDraft --> Review : submit\nReview -left-> Rejected : declined\nstate Shipping {\n  [*] -right-> Packed\n  Packed -right-> Moving : picked up\n}\nReview --> Shipping\n@enduml\n");

        // Act
        var scene = document.LayoutScene(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var labels = scene.Items.OfType<SceneText>().Where(t => t.Role == "link-label").ToList();
        Assert.Equal(["submit", "declined", "picked up"], labels.Select(l => l.Text));
        var lines = scene.Items.OfType<ScenePath>().Where(p => p.Role is "link" or "link-head").Select(PointsAlong).ToList();
        var states = scene.Items.OfType<ScenePath>().Where(p => p.Role == "state").Select(p => Scene.BoundsOf([p])!.Value).ToList();
        foreach (var label in labels)
        {
            var box = label.Box;
            var nearestLine = lines.SelectMany(points => points).Min(p => DistanceTo(box, p));
            Assert.True(nearestLine >= 3, $"'{label.Text}' is {nearestLine} from a line");
            Assert.All(states, state =>
            {
                var gap = Math.Max(Math.Max(state.X - box.Right, box.X - state.Right), Math.Max(state.Y - box.Bottom, box.Y - state.Bottom));
                Assert.True(gap >= 8, $"'{label.Text}' is {gap} from the state at {state}");
            });
        }
    }

    /// <summary>
    ///     Gets points along a path: its ends and 64 points along each of its lines and cubic curves.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The points.</returns>
    private static List<(double X, double Y)> PointsAlong(ScenePath path)
    {
        var points = new List<(double X, double Y)>();
        var (x0, y0) = (0.0, 0.0);
        foreach (var segment in path.Segments.Where(s => s.Kind != SceneSegmentKind.Close))
        {
            for (var k = segment.Kind == SceneSegmentKind.MoveTo ? 64 : 1; k <= 64; k++)
            {
                var t = k / 64.0;
                var u = 1 - t;
                points.Add(segment.Kind == SceneSegmentKind.CubicTo
                    ? ((u * u * u * x0) + (3 * u * u * t * segment.X1) + (3 * u * t * t * segment.X2) + (t * t * t * segment.X),
                       (u * u * u * y0) + (3 * u * u * t * segment.Y1) + (3 * u * t * t * segment.Y2) + (t * t * t * segment.Y))
                    : (x0 + ((segment.X - x0) * t), y0 + ((segment.Y - y0) * t)));
            }

            (x0, y0) = (segment.X, segment.Y);
        }

        return points;
    }

    /// <summary>
    ///     Gets the distance from a rectangle to a point (zero inside it).
    /// </summary>
    /// <param name="box">The rectangle.</param>
    /// <param name="point">The point.</param>
    /// <returns>The distance.</returns>
    private static double DistanceTo(SceneRect box, (double X, double Y) point)
    {
        var dx = Math.Max(0, Math.Max(box.X - point.X, point.X - box.Right));
        var dy = Math.Max(0, Math.Max(box.Y - point.Y, point.Y - box.Bottom));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
