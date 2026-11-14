using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Marks;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Unit tests for <see cref="ScenePositions"/> on hand-built scenes: guide boxes, the geometry and aim of each mark
///     item, vertices, ids, value conversion, the placement and the kind filter.
/// </summary>
public class ScenePositionsTests
{
    /// <summary>
    ///     The text measurer with the default fonts.
    /// </summary>
    private static readonly TextMeasurer Measurer = new(new VegaLiteFontOptions());

    /// <summary>
    ///     A context whose rows give one value: <c>x</c> is the row number.
    /// </summary>
    private static readonly SceneMarkContext Context = new(row => [new("x", PositionValue.FromNumber(row))]) { View = "hconcat[1]", ViewName = "right" };

    /// <summary>
    ///     Builds a scene with a view group at (10, 20) holding the given children in a marks group.
    /// </summary>
    /// <param name="children">The mark items.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(params SceneItem[] children)
    {
        var marks = new SceneGroup("marks", 0, 0) { Children = children, Marks = Context };
        var view = new SceneGroup("view", 10, 20)
        {
            Width = 100,
            Height = 50,
            Children = [marks],
            Position = new ScenePositionTag("view", "hconcat[1]") { View = "hconcat[1]", ViewName = "right" },
        };
        return new Scene(200, 100, null, new SceneGroup("root", 0, 0) { Children = [view] });
    }

    /// <summary>
    ///     Validates a view's box (its data rectangle) and a bar's box and center, numbered per mark type in its view,
    ///     with its values and view.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_ViewAndBars_BoxesCentersAndIds()
    {
        // Arrange
        var scene = SceneOf(
            new SceneRect("mark-bar", 0, 10, 20, 40) { Datum = 0 },
            new SceneRect("mark-bar", 30, 50, 20, -30) { Datum = 1 });

        // Act
        var positions = ScenePositions.Collect(scene, new ScenePlacement(1, 0, 0), Measurer, null);

        // Assert
        Assert.Equal(["view", "mark", "mark"], positions.Select(p => p.Kind));
        Assert.Equal(new PositionBox(10, 20, 100, 50), positions[0].Box);
        Assert.Equal(["bar-0", "bar-1"], positions.Skip(1).Select(p => p.Id));
        Assert.Equal(new PositionBox(10, 30, 20, 40), positions[1].Box);
        Assert.Equal(new PositionPoint(20, 50), positions[1].Pixel);
        Assert.Equal(new PositionBox(40, 40, 20, 30), positions[2].Box);
        Assert.Equal(("hconcat[1]", "right", "bar"), (positions[1].View, positions[1].ViewName, positions[1].Mark));
        Assert.True(positions[2].TryGetValue("x", out var x));
        Assert.Equal(PositionValue.FromNumber(1), x);
    }

    /// <summary>
    ///     Validates the aim of a rule (its midpoint), a symbol (its center, the box its shape), a text (its anchor) and
    ///     an arc (the mid-angle at the mid-radius).
    /// </summary>
    [Fact]
    public void ScenePositions_Geometry_RuleSymbolTextArc_AimAsDocumented()
    {
        // Arrange
        var rule = new SceneRule("mark-rule", 0, 0, 40, 20) { Datum = 0 };
        var square = new SceneSymbol("mark-square", 10, 10) { Shape = "square", Size = 16, Datum = 0 };
        var text = new SceneText("mark-text", 5, 6, ["Hi"]) { Dx = 2, Dy = 3, Datum = 0 };
        var arc = new SceneArc("mark-arc", 0, 0) { InnerRadius = 0, OuterRadius = 10, StartAngle = 0, EndAngle = Math.PI, Datum = 0 };

        // Act
        var ruleGeometry = ScenePositions.Geometry(rule, Measurer)!.Value;
        var squareGeometry = ScenePositions.Geometry(square, Measurer)!.Value;
        var textGeometry = ScenePositions.Geometry(text, Measurer)!.Value;
        var arcGeometry = ScenePositions.Geometry(arc, Measurer)!.Value;

        // Assert
        Assert.Equal((20.0, 10.0), ruleGeometry.Aim);
        Assert.Equal(new SceneBounds(0, 0, 40, 20), ruleGeometry.Bounds);
        Assert.Equal((10.0, 10.0), squareGeometry.Aim);
        Assert.Equal(8, squareGeometry.Bounds.X1, 3);
        Assert.Equal(12, squareGeometry.Bounds.X2, 3);
        Assert.Equal((7.0, 9.0), textGeometry.Aim);
        Assert.False(textGeometry.Bounds.IsEmpty);

        // The half disk from 12 o'clock clockwise to 6 o'clock: its middle angle is 3 o'clock, at radius 5
        Assert.Equal(5, arcGeometry.Aim!.Value.X, 6);
        Assert.Equal(0, arcGeometry.Aim.Value.Y, 6);
        Assert.Equal(0, arcGeometry.Bounds.X1, 2);
        Assert.Equal(10, arcGeometry.Bounds.X2, 2);
    }

    /// <summary>
    ///     Validates that a path mark is one position per vertex, with a zero-size box at the vertex, mapped by the
    ///     placement.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_LineVertices_OnePositionPerVertex()
    {
        // Arrange
        var line = new ScenePath("mark-line", [new ScenePoint(0, 0), new ScenePoint(10, 10)])
        {
            Vertices = [new SceneVertex(0, 0, 0), new SceneVertex(1, 10, 10)],
        };
        var scene = SceneOf(line);

        // Act: drawn at scale 2 with an offset
        var positions = ScenePositions.Collect(scene, new ScenePlacement(2, 1, 1), Measurer, kind => kind == "mark");

        // Assert
        Assert.Equal(["line-0", "line-1"], positions.Select(p => p.Id));
        Assert.Equal(new PositionBox(41, 61, 0, 0), positions[1].Box);
        Assert.Equal(new PositionPoint(41, 61), positions[1].Pixel);
    }

    /// <summary>
    ///     Validates that items without a recorded row report nothing, and that the filter skips marks.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_NoDatumOrFilteredOut_ReportsNoMarks()
    {
        // Arrange
        var scene = SceneOf(new SceneRect("mark-bar", 0, 0, 5, 5));

        // Act
        var unrecorded = ScenePositions.Collect(scene, new ScenePlacement(1, 0, 0), Measurer, null);
        var filtered = ScenePositions.Collect(SceneOf(new SceneRect("mark-bar", 0, 0, 5, 5) { Datum = 0 }), new ScenePlacement(1, 0, 0), Measurer, kind => kind == "view");

        // Assert
        Assert.Equal(["view"], unrecorded.Select(p => p.Kind));
        Assert.Equal(["view"], filtered.Select(p => p.Kind));
    }

    /// <summary>
    ///     Validates the conversion of data values: dates as UTC ISO text, numbers that are not finite as null, and
    ///     arrays left out.
    /// </summary>
    [Fact]
    public void MarkBuilder_TryConvert_DataValues_ConvertAsDocumented()
    {
        // Act / Assert
        Assert.True(MarkBuilder.TryConvert(DataValue.FromDate(Date(2024, 11, 1)), out var date));
        Assert.Equal(PositionValue.FromString("2024-11-01T00:00:00.000Z"), date);
        Assert.True(MarkBuilder.TryConvert(DataValue.FromNumber(double.NaN), out var nan));
        Assert.Equal(PositionValue.Null, nan);
        Assert.True(MarkBuilder.TryConvert(DataValue.FromString("A"), out var text));
        Assert.Equal(PositionValue.FromString("A"), text);
        Assert.True(MarkBuilder.TryConvert(DataValue.Null, out var none));
        Assert.Equal(PositionValue.Null, none);
    }

    /// <summary>
    ///     Gets the milliseconds of a UTC date.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <returns>The milliseconds since 1970.</returns>
    private static double Date(int year, int month, int day) =>
        (new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalMilliseconds;
}
