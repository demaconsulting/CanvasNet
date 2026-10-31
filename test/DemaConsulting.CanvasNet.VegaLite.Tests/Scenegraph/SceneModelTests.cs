using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Tests of the immutable scene model: item records, deterministic order, roles and the enumeration helpers.
/// </summary>
public class SceneModelTests
{
    /// <summary>
    ///     Validates that groups and items keep their order, that the helpers enumerate in drawing order with the
    ///     accumulated group translation, and that roles can be found.
    /// </summary>
    [Fact]
    public void SceneModel_Create_GroupsAndItemsPreservesDeterministicOrder()
    {
        // Arrange: root > view (10, 20) > [bar, axis group (0, 100) > [tick, tick, label], symbol]
        var axis = new SceneGroup("axis-x", 0, 100)
        {
            Children =
            [
                new SceneRule("axis-tick", 0, 0, 0, 5) { Stroke = "#888" },
                new SceneRule("axis-tick", 50, 0, 50, 5) { Stroke = "#888" },
                new SceneText("axis-label", 25, 9, ["A"]) { FontSize = 10, Fill = "#000" },
            ],
        };
        var view = new SceneGroup("view", 10, 20)
        {
            Width = 100,
            Height = 100,
            Children =
            [
                new SceneRect("mark-bar", 5, 6, 7, 8) { Fill = "#4c78a8" },
                axis,
                new SceneSymbol("mark-point", 30, 40) { Size = 64 },
            ],
        };
        var scene = new Scene(200, 150, "white", new SceneGroup("root", 0, 0) { Children = [view] });

        // Act
        var entries = scene.Enumerate().ToList();
        var again = scene.Enumerate().Select(e => e.Item.Role).ToList();

        // Assert: parents before children, siblings in list order, and the same sequence every time
        Assert.Equal(
            ["root", "view", "mark-bar", "axis-x", "axis-tick", "axis-tick", "axis-label", "mark-point"],
            entries.Select(e => e.Item.Role));
        Assert.Equal(entries.Select(e => e.Item.Role), again);
        Assert.Equal(8, scene.CountItems());
        Assert.Equal([0, 1, 2, 2, 3, 3, 3, 2], entries.Select(e => e.Depth));

        // Assert: the offsets accumulate the translations of the enclosing groups (the item's own position is not included)
        Assert.Equal((0.0, 0.0), (entries[1].OffsetX, entries[1].OffsetY));
        Assert.Equal((10.0, 20.0), (entries[2].OffsetX, entries[2].OffsetY));
        Assert.Equal((10.0, 120.0), (entries[4].OffsetX, entries[4].OffsetY));
        Assert.Equal((10.0, 120.0), (entries[6].OffsetX, entries[6].OffsetY));

        // Assert: items are found by role (first, all, by type)
        var firstTick = scene.Find("axis-tick");
        Assert.NotNull(firstTick);
        Assert.Equal(0, Assert.IsType<SceneRule>(firstTick.Value.Item).X);
        Assert.Equal([0.0, 50.0], scene.FindAll("axis-tick").Select(e => ((SceneRule)e.Item).X));
        Assert.Null(scene.Find("missing"));
        Assert.Single(scene.OfType<SceneSymbol>());
        Assert.Equal(2, scene.OfType<SceneRule>().Count());
        Assert.Equal(["mark-bar"], scene.OfType<SceneRect>().Select(r => r.Role));

        // Assert: the canvas properties are kept
        Assert.Equal((200.0, 150.0, "white"), (scene.Width, scene.Height, scene.Background));
    }

    /// <summary>
    ///     Validates that lists are copied when set and cannot be changed through the property, so that a scene never
    ///     changes after it is built.
    /// </summary>
    [Fact]
    public void SceneModel_Create_ListsAreCopiedAndReadOnly()
    {
        // Arrange
        var children = new List<SceneItem> { new SceneRect("a", 0, 0, 1, 1) };
        var dash = new List<double> { 4, 2 };
        var lines = new List<string> { "one", "two" };
        var points = new List<ScenePoint> { new(0, 0), new(1, 1) };

        // Act
        var group = new SceneGroup("g", 0, 0) { Children = children, StrokeDash = dash };
        var text = new SceneText("t", 0, 0, lines);
        var path = new ScenePath("p", points);
        children.Add(new SceneRect("b", 0, 0, 1, 1));
        dash.Add(9);
        lines.Add("three");
        points.Add(new ScenePoint(2, 2));

        // Assert: later changes of the source lists have no effect
        Assert.Single(group.Children);
        Assert.Equal([4.0, 2.0], group.StrokeDash);
        Assert.Equal(["one", "two"], text.Lines);
        Assert.Equal("one\ntwo", text.Text);
        Assert.Equal(2, path.Points.Count);

        // Assert: the exposed lists cannot be changed by a cast either
        Assert.Throws<NotSupportedException>(() => ((ICollection<SceneItem>)group.Children).Add(new SceneRect("c", 0, 0, 1, 1)));
        Assert.Throws<NotSupportedException>(() => ((ICollection<string>)text.Lines).Add("x"));
        Assert.Throws<NotSupportedException>(() => ((ICollection<ScenePoint>)path.Points).Add(new ScenePoint(3, 3)));
    }

    /// <summary>
    ///     Validates the documented defaults and that a changed copy leaves the original alone.
    /// </summary>
    [Fact]
    public void SceneModel_With_DefaultsAndCopiesFollowVega()
    {
        // Arrange
        var rect = new SceneRect("mark-bar", 1, 2, 3, 4);
        var text = new SceneText("title-text", 0, 0, ["x"]);
        var symbol = new SceneSymbol("mark-point", 0, 0);

        // Act
        var changed = rect with { Fill = "red", Opacity = 0.5 };

        // Assert: paint defaults are "no paint", full opacity and the default stroke width
        Assert.Null(rect.Fill);
        Assert.Null(rect.Stroke);
        Assert.Null(rect.StrokeWidth);
        Assert.Equal((1.0, 1.0, 1.0), (rect.Opacity, rect.FillOpacity, rect.StrokeOpacity));
        Assert.Equal(0, rect.CornerRadius);

        // Assert: Vega's defaults for text and symbols
        Assert.Equal((11.0, "left", "alphabetic", 0.0, 0.0), (text.FontSize, text.Align, text.Baseline, text.Limit, text.Angle));
        Assert.Equal("\u2026", text.Ellipsis);
        Assert.Equal(("circle", 64.0), (symbol.Shape, symbol.Size));

        // Assert: a with-copy changes only what it names
        Assert.Equal(("red", 0.5), (changed.Fill, changed.Opacity));
        Assert.Null(rect.Fill);
        Assert.Equal((1.0, 2.0, 3.0, 4.0), (changed.X, changed.Y, changed.Width, changed.Height));
    }

    /// <summary>
    ///     Validates the argument checks of the model.
    /// </summary>
    [Fact]
    public void SceneModel_Create_InvalidArgumentsAreRejected()
    {
        // Arrange / Act / Assert: a role is required
        Assert.Throws<ArgumentException>(() => new SceneRect(" ", 0, 0, 1, 1));
        Assert.Throws<ArgumentNullException>(() => new SceneRect(null!, 0, 0, 1, 1));

        // Assert: the canvas size must be a finite number that is not negative, and the root is required
        var root = new SceneGroup("root", 0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Scene(-1, 1, null, root));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Scene(1, double.NaN, null, root));
        Assert.Throws<ArgumentNullException>(() => new Scene(1, 1, null, null!));
        Assert.Throws<ArgumentNullException>(() => Scene.Enumerate(null!));
    }

    /// <summary>
    ///     Validates the gradient fill of an item: the Vega defaults of the factories, the copied and read-only stops,
    ///     the bound on the number of stops and the argument checks.
    /// </summary>
    [Fact]
    public void SceneModel_Gradient_DefaultsCopiesAndLimits()
    {
        // Arrange: an item has no gradient by default, and a gradient does not touch the solid fill
        Assert.Null(new SceneRect("r", 0, 0, 1, 1).FillGradient);
        var source = new List<SceneGradientStop> { new(0, "#ff0000"), new(1, "#0000ff") };

        // Act
        var horizontal = SceneGradient.Horizontal(source);
        var vertical = SceneGradient.Vertical(source);
        var custom = new SceneGradient(0.25, 0.5, 0.75, 1, source);
        source.Add(new SceneGradientStop(0.5, "#00ff00"));
        var item = new SceneRect("r", 0, 0, 10, 10) { Fill = "#00ff00", FillGradient = horizontal };

        // Assert: Vega's defaults run left to right, and a legend gradient runs from the bottom to the top
        Assert.Equal((0.0, 0.0, 1.0, 0.0), (horizontal.X1, horizontal.Y1, horizontal.X2, horizontal.Y2));
        Assert.Equal((0.0, 1.0, 0.0, 0.0), (vertical.X1, vertical.Y1, vertical.X2, vertical.Y2));
        Assert.Equal((0.25, 0.5, 0.75, 1.0), (custom.X1, custom.Y1, custom.X2, custom.Y2));

        // Assert: the stops were copied when the gradient was made and cannot be changed through the property
        Assert.Equal(2, horizontal.Stops.Count);
        Assert.Equal(new SceneGradientStop(1, "#0000ff"), horizontal.Stops[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<SceneGradientStop>)horizontal.Stops).Add(new SceneGradientStop(0.5, "#fff")));

        // Assert: the item keeps both paints, and a copy made with a with-expression keeps the gradient
        Assert.Equal("#00ff00", item.Fill);
        Assert.Same(horizontal, item.FillGradient);
        Assert.Same(horizontal, (item with { Fill = null }).FillGradient);
        Assert.Null((item with { FillGradient = null }).FillGradient);

        // Assert: the stop count is bounded; exactly the limit and no stops at all are accepted
        Assert.Equal(256, SceneGradient.MaxStops);
        Assert.Equal(256, SceneGradient.Horizontal(Enumerable.Range(0, 256).Select(i => new SceneGradientStop(i / 255d, "#000"))).Stops.Count);
        Assert.Throws<ArgumentException>(() => SceneGradient.Horizontal(Enumerable.Range(0, 257).Select(i => new SceneGradientStop(i / 256d, "#000"))));
        Assert.Empty(SceneGradient.Horizontal([]).Stops);
        Assert.Throws<ArgumentNullException>(() => SceneGradient.Horizontal(null!));
    }

    /// <summary>
    ///     Validates the bounding box helper used by layout: union identity, translation and exact quarter turns.
    /// </summary>
    [Fact]
    public void SceneBounds_UnionTranslateAndRotate_AreExact()
    {
        // Arrange
        var box = new SceneBounds(0, 0, 10, 4);

        // Act / Assert: the empty box is the identity of the union
        Assert.True(SceneBounds.Empty.IsEmpty);
        Assert.Equal(box, box.Union(SceneBounds.Empty));
        Assert.Equal(box, SceneBounds.Empty.Union(box));
        Assert.Equal(0, SceneBounds.Empty.Width);
        Assert.Equal(new SceneBounds(-2, 0, 10, 9), box.Union(new SceneBounds(-2, 5, 3, 9)));

        // Assert: translation and the quarter turns have no rounding noise
        Assert.Equal(new SceneBounds(5, 6, 15, 10), box.Translate(5, 6));
        Assert.Equal(new SceneBounds(-4, 0, 0, 10), box.Rotate(90));
        Assert.Equal(new SceneBounds(0, -10, 4, 0), box.Rotate(-90));
        Assert.Equal(new SceneBounds(0, -10, 4, 0), box.Rotate(270));
        Assert.Equal(new SceneBounds(-10, -4, 0, 0), box.Rotate(180));
        Assert.True(SceneBounds.Empty.Rotate(30).IsEmpty);
        Assert.True(SceneBounds.Empty.Translate(1, 1).IsEmpty);
    }

    /// <summary>
    ///     Validates that the segments of a path are copied, read-only and replace the points, that the factories fill the
    ///     documented coordinates and that finiteness follows the coordinates that a command kind uses.
    /// </summary>
    [Fact]
    public void SceneModel_PathSegments_AreCopiedAndTakePrecedenceOverPoints()
    {
        // Arrange
        var source = new List<ScenePathSegment>
        {
            ScenePathSegment.Move(1, 2),
            ScenePathSegment.Cubic(3, 4, 5, 6, 7, 8),
            ScenePathSegment.Line(9, 10),
            ScenePathSegment.Close(),
        };

        // Act
        var path = new ScenePath("mark-line", source) { Stroke = "#4c78a8", Closed = true };
        source.Add(ScenePathSegment.Line(0, 0));
        var points = new ScenePath("axis-domain", [new ScenePoint(0, 0), new ScenePoint(1, 1)]);

        // Assert: the list was copied, keeps its order and cannot be changed through a cast; the points are empty
        Assert.Equal(4, path.Segments.Count);
        Assert.Empty(path.Points);
        Assert.Throws<NotSupportedException>(() => ((ICollection<ScenePathSegment>)path.Segments).Add(ScenePathSegment.Close()));
        Assert.Equal(
            [ScenePathSegmentKind.MoveTo, ScenePathSegmentKind.CubicTo, ScenePathSegmentKind.LineTo, ScenePathSegmentKind.Close],
            path.Segments.Select(s => s.Kind));

        // Assert: the factories use (X, Y) for the end point and the control points for a cubic; unused coordinates are zero
        Assert.Equal((1.0, 2.0, 0.0, 0.0, 0.0, 0.0), (path.Segments[0].X, path.Segments[0].Y, path.Segments[0].X1, path.Segments[0].Y1, path.Segments[0].X2, path.Segments[0].Y2));
        Assert.Equal((7.0, 8.0, 3.0, 4.0, 5.0, 6.0), (path.Segments[1].X, path.Segments[1].Y, path.Segments[1].X1, path.Segments[1].Y1, path.Segments[1].X2, path.Segments[1].Y2));
        Assert.Equal((9.0, 10.0), (path.Segments[2].X, path.Segments[2].Y));
        Assert.Equal(new ScenePathSegment(ScenePathSegmentKind.Close, 0, 0), ScenePathSegment.Close());

        // Assert: a path of points has no segments, so the two forms are told apart by the segment list
        Assert.Empty(points.Segments);
        Assert.Equal(2, points.Points.Count);

        // Assert: a command is finite when the coordinates of its kind are; a close has none
        Assert.True(ScenePathSegment.Close().IsFinite());
        Assert.True(ScenePathSegment.Line(1, 2).IsFinite());
        Assert.False(ScenePathSegment.Line(double.NaN, 2).IsFinite());
        Assert.False(ScenePathSegment.Move(1, double.PositiveInfinity).IsFinite());
        Assert.True(ScenePathSegment.Cubic(1, 2, 3, 4, 5, 6).IsFinite());
        Assert.False(ScenePathSegment.Cubic(double.NaN, 2, 3, 4, 5, 6).IsFinite());
        Assert.False(ScenePathSegment.Cubic(1, 2, 3, double.NegativeInfinity, 5, 6).IsFinite());
        Assert.False(ScenePathSegment.Cubic(1, 2, 3, 4, 5, double.NaN).IsFinite());
        Assert.Throws<ArgumentNullException>(() => new ScenePath("p", (IEnumerable<ScenePathSegment>)null!));
    }

    /// <summary>
    ///     Validates the geometry members and defaults of arcs and trails and that the lists of a trail are copied.
    /// </summary>
    [Fact]
    public void SceneModel_ArcAndTrail_ExposeTheirGeometryAndCopyLists()
    {
        // Arrange / Act: an arc has a center and no extent until the members are set
        var plain = new SceneArc("mark-arc", 100, 120);
        var arc = plain with { InnerRadius = 20, OuterRadius = 50, StartAngle = 0.5, EndAngle = 1.5, PadAngle = 0.02, CornerRadius = 3, Fill = "#4c78a8" };

        // Assert: defaults are zero and a with-copy changes only what it names
        Assert.Equal((100.0, 120.0), (plain.X, plain.Y));
        Assert.Equal((0.0, 0.0, 0.0, 0.0, 0.0, 0.0), (plain.InnerRadius, plain.OuterRadius, plain.StartAngle, plain.EndAngle, plain.PadAngle, plain.CornerRadius));
        Assert.Equal((20.0, 50.0, 0.5, 1.5, 0.02, 3.0), (arc.InnerRadius, arc.OuterRadius, arc.StartAngle, arc.EndAngle, arc.PadAngle, arc.CornerRadius));
        Assert.Equal("mark-arc", arc.Role);
        Assert.Null(plain.Fill);

        // Arrange / Act: a trail copies the runs and the points of each run
        var first = new List<SceneTrailPoint> { new(0, 0, 2), new(10, 5, 4) };
        var second = new List<SceneTrailPoint> { new(20, 5, 6) };
        var runs = new List<IReadOnlyList<SceneTrailPoint>> { first, second };
        var trail = new SceneTrail("mark-trail", runs) { Fill = "#e45756" };
        first.Add(new SceneTrailPoint(30, 30, 8));
        runs.Add([new SceneTrailPoint(40, 40, 1)]);

        // Assert: later changes of the sources have no effect and the exposed lists are read-only
        Assert.Equal(2, trail.Runs.Count);
        Assert.Equal(2, trail.Runs[0].Count);
        Assert.Equal(new SceneTrailPoint(10, 5, 4), trail.Runs[0][1]);
        Assert.Equal((20.0, 5.0, 6.0), (trail.Runs[1][0].X, trail.Runs[1][0].Y, trail.Runs[1][0].Size));
        Assert.Throws<NotSupportedException>(() => ((ICollection<IReadOnlyList<SceneTrailPoint>>)trail.Runs).Add([]));
        Assert.Throws<NotSupportedException>(() => ((ICollection<SceneTrailPoint>)trail.Runs[0]).Add(new SceneTrailPoint(1, 1, 1)));

        // Assert: an empty trail is allowed, a null list is rejected
        Assert.Empty(new SceneTrail("mark-trail", []).Runs);
        Assert.Throws<ArgumentNullException>(() => new SceneTrail("mark-trail", null!));
    }

    /// <summary>
    ///     Validates that the miter limit is absent by default and that a changed copy keeps it.
    /// </summary>
    [Fact]
    public void SceneModel_StrokeMiterLimit_DefaultsToNullAndSurvivesWith()
    {
        // Arrange / Act
        var plain = new SceneRect("mark-bar", 0, 0, 1, 1);
        var limited = plain with { StrokeMiterLimit = 2.5, Fill = "red" };
        var copy = limited with { Opacity = 0.5 };

        // Assert: no value is stored by default (the painter's default of 4 applies), and the value survives a with-copy
        Assert.Null(plain.StrokeMiterLimit);
        Assert.Equal(2.5, limited.StrokeMiterLimit);
        Assert.Equal(2.5, copy.StrokeMiterLimit);
        Assert.Null(new ScenePath("p", [new ScenePoint(0, 0)]).StrokeMiterLimit);
        Assert.Null(new SceneArc("a", 0, 0).StrokeMiterLimit);
    }
}
