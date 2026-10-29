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
}
