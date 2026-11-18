using DemaConsulting.CanvasNet.PlantUml.Activity;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Activity;

/// <summary>
///     Unit tests for the <c>ActivityLayout</c> unit: the placement of the tiles of an activity diagram (PlantUML's
///     "ftile" layout), its compression, its swimlanes, and its work limit.
/// </summary>
public class ActivityLayoutTests
{
    /// <summary>
    ///     Validates that a sequence of actions is stacked on one axis, 20 pixels apart after the compression, at
    ///     PlantUML's coordinates.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_Sequence_StacksOnOneAxis()
    {
        // Act
        var (_, geometry) = ActivityTestHelpers.Layout("start\n:Open the file;\n:Read the header;\nstop");

        // Assert
        var start = ActivityTestHelpers.Shapes(geometry, "start").Single();
        var boxes = ActivityTestHelpers.Shapes(geometry, "activity");
        Assert.Equal((145, 207), ((int)Math.Ceiling(geometry.Width), (int)Math.Ceiling(geometry.Height)));
        Assert.Equal(70.701, start.X + (start.Width / 2), 2);
        Assert.Equal((26.679, 55.0), (Math.Round(boxes[0].X, 3), Math.Round(boxes[0].Y, 3)));
        Assert.Equal((16.0, 110.094), (Math.Round(boxes[1].X, 3), Math.Round(boxes[1].Y, 3)));
        Assert.Equal(20, boxes[1].Y - (boxes[0].Y + boxes[0].Height), 3);
    }

    /// <summary>
    ///     Validates that the branches of an if are side by side below the diamond, joined again by a merge diamond.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_If_PlacesBranchesSideBySide()
    {
        // Act
        var (_, geometry) = ActivityTestHelpers.Layout("start\nif (ok?) then (yes)\n:left;\nelse (no)\n:right;\nendif\nstop");

        // Assert
        var boxes = ActivityTestHelpers.Shapes(geometry, "activity");
        var diamonds = ActivityTestHelpers.Shapes(geometry, "diamond");
        Assert.Equal(2, diamonds.Count);
        Assert.True(boxes[0].X + boxes[0].Width < boxes[1].X);
        Assert.Equal(boxes[0].Y, boxes[1].Y, 3);
        Assert.True(boxes[0].Y > diamonds[0].Y + diamonds[0].Height && diamonds[1].Y > boxes[0].Y + boxes[0].Height);
    }

    /// <summary>
    ///     Validates the while loop: the hexagon above the body, the exit on the left and the loop arrow back, at
    ///     PlantUML's coordinates.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_While_PlacesTheTestAndTheBody()
    {
        // Act
        var (_, geometry) = ActivityTestHelpers.Layout(ActivityTestHelpers.Fixture("06-while").Replace("@startuml\n", string.Empty, StringComparison.Ordinal).Replace("@enduml\n", string.Empty, StringComparison.Ordinal));

        // Assert
        var hexagon = ActivityTestHelpers.Shapes(geometry, "diamond").Single();
        var boxes = ActivityTestHelpers.Shapes(geometry, "activity");
        var hexagonBox = ActivityTestHelpers.Box(hexagon);
        Assert.Equal((41.0, 110.094), (Math.Round(hexagonBox.X, 3), Math.Round(hexagonBox.Y, 3)));
        Assert.Equal((42.191, 167.013), (Math.Round(boxes[1].X, 3), Math.Round(boxes[1].Y, 3)));
        Assert.Equal((26.508, 307.782), (Math.Round(boxes[3].X, 3), Math.Round(boxes[3].Y, 3)));
        Assert.Contains(geometry.Shapes, s => s.Role == "arrow-text" && s.Text!.Text == "no" && s.X < hexagonBox.X);
    }

    /// <summary>
    ///     Validates that the branches of a fork are between two bars, and a merge ends on a diamond.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_Fork_PlacesBranchesBetweenBars()
    {
        // Act
        var (_, fork) = ActivityTestHelpers.Layout(":a;\nfork\n:b;\nfork again\n:c;\nend fork\n:d;");
        var (_, merge) = ActivityTestHelpers.Layout(":a;\nfork\n:b;\nfork again\n:c;\nend merge\n:d;");

        // Assert
        var bars = ActivityTestHelpers.Shapes(fork, "bar");
        var boxes = ActivityTestHelpers.Shapes(fork, "activity");
        Assert.Equal(2, bars.Count);
        Assert.True(boxes[1].Y > bars[0].Y && boxes[1].Y + boxes[1].Height < bars[1].Y);
        Assert.Equal(boxes[1].Y, boxes[2].Y, 3);
        Assert.Single(ActivityTestHelpers.Shapes(merge, "bar"));
        Assert.Single(ActivityTestHelpers.Shapes(merge, "diamond"));
    }

    /// <summary>
    ///     Validates that the instructions of each swimlane are drawn inside its columns, under its title.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_Swimlanes_PlaceInstructionsInTheirColumns()
    {
        // Act
        var (_, geometry) = ActivityTestHelpers.Layout("|A|\nstart\n:first;\n|B|\n:second;\n|A|\n:third;\nstop");

        // Assert
        var dividers = ActivityTestHelpers.Shapes(geometry, "lane-divider").Select(d => d.X).Order().ToList();
        var titles = ActivityTestHelpers.Shapes(geometry, "lane-title");
        var boxes = ActivityTestHelpers.Shapes(geometry, "activity").OrderBy(b => b.Y).ToList();
        Assert.Equal(3, dividers.Count);
        Assert.True(boxes[0].X > dividers[0] && boxes[0].X + boxes[0].Width < dividers[1]);
        Assert.True(boxes[1].X > dividers[1] && boxes[1].X + boxes[1].Width < dividers[2]);
        Assert.True(boxes[2].X > dividers[0] && boxes[2].X + boxes[2].Width < dividers[1]);
        Assert.All(titles, t => Assert.True(t.Y < boxes[0].Y));
    }

    /// <summary>
    ///     Validates that notes are drawn beside their action (the pointer touches it), and a partition frames its
    ///     instructions.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_NotesAndGroups_SurroundTheirInstructions()
    {
        // Act
        var (_, geometry) = ActivityTestHelpers.Layout("partition Work {\n:a;\nnote right: beside\n}");

        // Assert
        var box = ActivityTestHelpers.Shapes(geometry, "activity").Single();
        var note = ActivityTestHelpers.Shapes(geometry, "note")[0];
        var frame = ActivityTestHelpers.Shapes(geometry, "partition")[0];
        Assert.True(ActivityTestHelpers.Box(note).X >= box.X + box.Width - 1e-6);
        Assert.True(frame.X < box.X && frame.X + frame.Width > box.X + box.Width && frame.Y < box.Y);
    }

    /// <summary>
    ///     Validates that a layout over the work limit fails with plantuml-limit-exceeded, and a canceled layout stops.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_WorkLimitAndCancellation_StopTheLayout()
    {
        // Arrange
        var model = ActivityTestHelpers.Parse(ActivityTestHelpers.Fixture("08-fork").Replace("@startuml\n", string.Empty, StringComparison.Ordinal).Replace("@enduml\n", string.Empty, StringComparison.Ordinal));
        var limited = new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 20 } };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var context = new DiagramRenderContext(new PlantUmlRenderOptions(), new PlantUmlReportSink(100, false), new PlantUml.Scenegraph.TextLayout(PlantUml.Scenegraph.TextLayout.CreateFontResolver()), canceled.Token);

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => ActivityLayout.Run(model, ActivityTestHelpers.Context(limited)));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.ThrowsAny<OperationCanceledException>(() => ActivityLayout.Run(model, context));
    }

    /// <summary>
    ///     Validates that every activity fixture lays out inside its size.
    /// </summary>
    [Fact]
    public void PlantUmlActivityLayout_Fixtures_FitInTheirSize()
    {
        // Arrange
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "activity"), "*.puml");

        // Act
        var geometries = files.Select(f => ActivityLayout.Run((ActivityModel)PlantUmlDocument.Parse(File.ReadAllText(f)).Diagram!, ActivityTestHelpers.Context())).ToList();

        // Assert
        Assert.NotEmpty(geometries);
        Assert.All(geometries, g => Assert.All(g.Shapes.Where(s => s.Kind == ActivityShapeKind.Rect && s.Role == "activity"), s =>
            Assert.True(s.X >= 0 && s.Y >= 0 && s.X + s.Width <= g.Width && s.Y + s.Height <= g.Height)));
    }
}
