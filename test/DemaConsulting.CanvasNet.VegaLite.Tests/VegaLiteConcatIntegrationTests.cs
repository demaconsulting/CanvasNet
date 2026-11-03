using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of concatenated views (Phase 11): <c>concat</c>, <c>hconcat</c> and <c>vconcat</c> specifications are drawn chart by chart, each chart with
///     its own size, axes and title, laid out in a grid with the spacing, columns, alignment and bounds of the concatenation, with the legends that the charts share drawn once
///     beside the grid. The charts are rendered through the public API and checked by the positions of the cells, the marks and the guides, with every number derived by hand
///     from the layout rules (a one pixel <c>#ddd</c> border on each cell whose half pixel reaches outside the cell, so that the next cell starts at <c>ceil(100.5) + 20 + ceil(0.5)</c>
///     = 122 pixels, and a padding of 5 pixels around the whole).
/// </summary>
public class VegaLiteConcatIntegrationTests
{
    /// <summary>Three categories with a value <c>v</c> of 4, 8 and 6, a second measure <c>w</c> and a color <c>c</c> of two values.</summary>
    private const string CategoryData = """
        "data": { "values": [ { "a": "A", "v": 4, "w": 100, "c": "u" }, { "a": "B", "v": 8, "w": 400, "c": "v" }, { "a": "C", "v": 6, "w": 1000, "c": "u" } ] }
        """;

    /// <summary>A 100 by 80 bar chart without axes, so that its bounds are its data rectangle and the half pixel of its border.</summary>
    private const string Bare = """{ "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";

    /// <summary>A 100 by 80 point chart without axes whose color shows <c>c</c>.</summary>
    private const string BareColor = """{ "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "nominal" } } }""";

    /// <summary>A bar chart of 100 by 80 pixels with both axes.</summary>
    private const string WithAxes = """{ "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }""";

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the cells of a composition: the plot areas of its charts, in drawing order, as the position of the corner on the canvas and the size.
    /// </summary>
    /// <param name="scene">The scene (its first view group is the root of the composition, which has no size).</param>
    /// <returns>The cells.</returns>
    private static List<(double X, double Y, double Width, double Height)> Cells(Scene scene) =>
        [.. scene.FindAll("view").Skip(1).Select(e => (e.OffsetX + ((SceneGroup)e.Item).X, e.OffsetY + ((SceneGroup)e.Item).Y, ((SceneGroup)e.Item).Width, ((SceneGroup)e.Item).Height))];

    /// <summary>
    ///     Gets the items of a role in a scene as a given kind.
    /// </summary>
    /// <typeparam name="T">The kind of item.</typeparam>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items in drawing order.</returns>
    private static List<T> Items<T>(Scene scene, string role)
        where T : SceneItem => [.. scene.FindAll(role).Select(e => (T)e.Item)];

    /// <summary>
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. Items<SceneText>(scene, role).Select(t => t.Text)];

    /// <summary>
    ///     Renders a specification and collects what it reports.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The warnings and the unsupported features, each as <c>identifier: message</c>.</returns>
    private static (List<string> Warnings, List<string> Unsupported) Report(string json, VegaLiteLimits? limits = null)
    {
        var warnings = new List<string>();
        var unsupported = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w => warnings.Add(w.Message),
            UnsupportedFeatureHandler = u => unsupported.Add($"{u.Feature}: {u.Message}"),
            Limits = limits ?? VegaLiteLimits.Default,
        };
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        return (warnings, unsupported);
    }

    /// <summary>
    ///     Validates that the two charts of an <c>hconcat</c> each draw their own axes and titles, side by side on one canvas, with the second chart's left axis in the gap.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_HConcat_TwoChartsAreLaidOutWithTheirOwnAxes()
    {
        // Arrange: the second chart shows another field on x
        var json = $$"""
            {
              {{CategoryData}},
              "hconcat": [
                {{WithAxes}},
                { "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": "w", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: two cells of the size of the charts, on one row; each chart has an x and a y axis of its own (the axes are never shared by a concatenation)
        Assert.Equal(2, cells.Count);
        Assert.All(cells, c => Assert.Equal((100.0, 80.0), (c.Width, c.Height)));
        Assert.Equal(cells[0].Y, cells[1].Y);
        Assert.Equal(4, scene.FindAll("axis").Count());
        Assert.Equal(["a", "v", "w", "v"], Texts(scene, "axis-title"));

        // Assert: the second chart starts after the first (100 pixels), the 20 pixel spacing, the half pixel of the border and its own y axis (at least 20 pixels wide)
        Assert.True(cells[1].X - (cells[0].X + 100) > 40);
    }

    /// <summary>
    ///     Validates the position of the second chart of an <c>hconcat</c> of charts without guides: <c>ceil(100.5) + 20 + ceil(0.5)</c> = 122 pixels from the first, and the canvas
    ///     is the content plus the padding.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_HConcat_SecondChartStartsAfterFirstBoundsPlusSpacing()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "hconcat": [ {{Bare}}, {{Bare}} ] }""";
        var document = VegaLiteDocument.Parse(json);

        // Act
        var cells = Cells(SceneOf(json));
        var size = document.Measure(cancellationToken: Ct);

        // Assert: the root is at (6, 6) (the padding of 5 and the ceil of the half pixel of the border), the second cell 122 pixels to the right
        Assert.Equal([(6.0, 6.0, 100.0, 80.0), (128.0, 6.0, 100.0, 80.0)], cells);

        // Assert: left 1 + ceil(222.5) = 224, top 1 + ceil(80.5) = 82, and the padding of 10 on each axis
        Assert.Equal(new VegaLiteSize(234, 92), size);
    }

    /// <summary>
    ///     Validates that a <c>vconcat</c> stacks its charts in one column with 20 pixels (and the half pixels of the borders) between them.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_VConcat_ChartsStackWithTwentyPixelsBetween()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "vconcat": [ {{Bare}}, {{Bare}} ] }""";
        var document = VegaLiteDocument.Parse(json);

        // Act
        var cells = Cells(SceneOf(json));
        var size = document.Measure(cancellationToken: Ct);

        // Assert: the second cell is ceil(80.5) + 20 + ceil(0.5) = 102 pixels below the first; the canvas is 1 + ceil(100.5) + 10 wide and 1 + ceil(182.5) + 10 high
        Assert.Equal([(6.0, 6.0, 100.0, 80.0), (6.0, 108.0, 100.0, 80.0)], cells);
        Assert.Equal(new VegaLiteSize(112, 194), size);
    }

    /// <summary>
    ///     Validates that <c>concat</c> with <c>columns</c> wraps the charts into rows and that the rows and columns are aligned each by default.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ColumnsWrapsAndAlignsEach()
    {
        // Arrange: four charts of widths 100, 60, 40 and 60 in two columns; the first column is 100 wide, so the second column starts at 122 in both rows
        const string narrow60 = """{ "width": 60, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        const string narrow40 = """{ "width": 40, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        var json = $$"""{ {{CategoryData}}, "columns": 2, "concat": [ {{Bare}}, {{narrow60}}, {{narrow40}}, {{narrow60}} ] }""";

        // Act
        var cells = Cells(SceneOf(json));

        // Assert: the cells in row-major order, the second row 102 pixels below the first
        Assert.Equal(
            [(6.0, 6.0, 100.0, 80.0), (128.0, 6.0, 60.0, 80.0), (6.0, 108.0, 40.0, 80.0), (128.0, 108.0, 60.0, 80.0)],
            cells);
    }

    /// <summary>
    ///     Validates that the spacing of a concatenation can differ between columns and rows (<c>{"column": 40, "row": 10}</c>).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_SpacingColumnAndRowObject()
    {
        // Arrange: three charts in two columns
        var json = $$"""{ {{CategoryData}}, "columns": 2, "spacing": { "column": 40, "row": 10 }, "concat": [ {{Bare}}, {{Bare}}, {{Bare}} ] }""";

        // Act
        var cells = Cells(SceneOf(json));

        // Assert: 101 + 40 + 1 = 142 between the column origins and 81 + 10 + 1 = 92 between the rows (plus the root's 6)
        Assert.Equal([(6.0, 6.0), (148.0, 6.0), (6.0, 98.0)], cells.Select(c => (c.X, c.Y)));
    }

    /// <summary>
    ///     Validates that <c>bounds: "flush"</c> places the charts by their data rectangles, ignoring the axes: the second chart is exactly 100 + 20 pixels from the first.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_BoundsFlushIgnoresAxes()
    {
        // Arrange: charts with axes, laid out by their boxes only
        var json = $$"""{ {{CategoryData}}, "bounds": "flush", "hconcat": [ {{WithAxes}}, {{WithAxes}} ] }""";
        var fullJson = $$"""{ {{CategoryData}}, "hconcat": [ {{WithAxes}}, {{WithAxes}} ] }""";

        // Act
        var flush = Cells(SceneOf(json));
        var full = Cells(SceneOf(fullJson));

        // Assert: the data rectangles are 100 + 20 apart; with the full bounds the axis of the second chart pushes it further
        Assert.Equal(120.0, flush[1].X - flush[0].X);
        Assert.True(full[1].X - full[0].X > 120.0);
    }

    /// <summary>
    ///     Validates that <c>center</c> centers a narrower chart in the column of a <c>vconcat</c>: a 60 pixel wide chart under a 100 pixel wide one is 20 pixels to the right.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_CenterCentersChildren()
    {
        // Arrange: flush bounds make the boxes the data rectangles
        const string narrow = """{ "width": 60, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        var plain = $$"""{ {{CategoryData}}, "bounds": "flush", "vconcat": [ {{Bare}}, {{narrow}} ] }""";
        var centered = $$"""{ {{CategoryData}}, "bounds": "flush", "center": true, "vconcat": [ {{Bare}}, {{narrow}} ] }""";

        // Act
        var plainCells = Cells(SceneOf(plain));
        var centeredCells = Cells(SceneOf(centered));

        // Assert: (100 - 60) / 2 = 20
        Assert.Equal(plainCells[0].X, plainCells[1].X);
        Assert.Equal(centeredCells[0].X + 20, centeredCells[1].X);
    }

    /// <summary>
    ///     Validates that a color that the charts share (a shared scale by default) is one legend at the root: drawn once, with the entries of the union of the domains, beside the
    ///     grid at <c>ceil(222.5) + 18</c> = 241 pixels from the origin of the concatenation and at its top.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_SharedColorLegendIsMergedAtTheRoot()
    {
        // Arrange: the first chart shows the values u and v, the second only w; the union is one legend
        const string first = """{ "data": { "values": [ { "a": "A", "v": 1, "c": "u" }, { "a": "B", "v": 2, "c": "v" } ] }, "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "nominal" } } }""";
        const string second = """{ "data": { "values": [ { "a": "A", "v": 3, "c": "w" } ] }, "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "nominal" } } }""";
        var json = $$"""{ "hconcat": [ {{first}}, {{second}} ] }""";

        // Act
        var scene = SceneOf(json);
        var legend = Assert.Single(scene.FindAll("legend"));
        var group = (SceneGroup)legend.Item;

        // Assert: one legend with the three entries and one title
        Assert.Equal(["u", "v", "w"], Texts(scene, "legend-label"));
        Assert.Equal(["c"], Texts(scene, "legend-title"));
        Assert.Equal((241.0, 0.0), (group.X, group.Y));
    }

    /// <summary>
    ///     Validates that <c>resolve: {"legend": {"color": "independent"}}</c> keeps a legend in each chart, drawn inside the chart's cell and pushing the next chart away.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ResolveColorIndependentKeepsLegendsInTheCharts()
    {
        // Arrange
        var shared = $$"""{ {{CategoryData}}, "hconcat": [ {{BareColor}}, {{BareColor}} ] }""";
        var independent = $$"""{ {{CategoryData}}, "resolve": { "scale": { "color": "independent" } }, "hconcat": [ {{BareColor}}, {{BareColor}} ] }""";

        // Act
        var sharedScene = SceneOf(shared);
        var independentScene = SceneOf(independent);
        var independentCells = Cells(independentScene);

        // Assert: one legend for shared scales, one in each chart for independent scales; the first chart's legend is between the charts
        Assert.Single(sharedScene.FindAll("legend"));
        var legends = independentScene.FindAll("legend").ToList();
        Assert.Equal(2, legends.Count);
        Assert.True(legends[0].OffsetX + ((SceneGroup)legends[0].Item).X > independentCells[0].X + 100);
        Assert.True(legends[0].OffsetX + ((SceneGroup)legends[0].Item).X < independentCells[1].X);
    }

    /// <summary>
    ///     Validates that <c>resolve: {"scale": {"x": "shared"}}</c> gives the charts one x domain (the union of their domains) while each chart keeps its own axis.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ResolveScaleXSharedUnionsDomainsButKeepsAxes()
    {
        // Arrange: the first chart's x values reach 10, the second's 100
        const string small = """{ "data": { "values": [ { "x": 2, "y": 1 }, { "x": 10, "y": 2 } ] }, "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative", "axis": null } } }""";
        const string large = """{ "data": { "values": [ { "x": 2, "y": 1 }, { "x": 100, "y": 2 } ] }, "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative", "axis": null } } }""";
        var independent = $$"""{ "hconcat": [ {{small}}, {{large}} ] }""";
        var shared = $$"""{ "resolve": { "scale": { "x": "shared" } }, "hconcat": [ {{small}}, {{large}} ] }""";

        // Act
        var independentScene = SceneOf(independent);
        var sharedScene = SceneOf(shared);

        // Assert: independent, the first axis ends at 10 and the second at 100; shared, both axes run to 100 (the same ticks), and there are still two x axes
        Assert.Equal(1, Texts(independentScene, "axis-label").Count(t => t == "100"));
        Assert.Equal(2, Texts(sharedScene, "axis-label").Count(t => t == "100"));
        Assert.Equal(2, sharedScene.FindAll("axis").Count());
        Assert.Equal(["x", "x"], Texts(sharedScene, "axis-title"));

        // Assert: the shared scale spans the union, so the same value is at the same pixel in both charts
        var points = Items<SceneSymbol>(sharedScene, "mark-point");
        Assert.Equal(points[0].X, points[2].X);
    }

    /// <summary>
    ///     Validates that the children of a concatenation can have their own data and transforms, and that a child without data uses the data of the concatenation.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ChildrenWithOwnDataAndTransforms()
    {
        // Arrange: the first child filters the inherited three rows to two, the second has its own two rows
        const string own = """{ "data": { "values": [ { "a": "X", "v": 1 }, { "a": "Y", "v": 2 } ] }, "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        const string filtered = """{ "transform": [ { "filter": "datum.v > 4" } ], "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        var json = $$"""{ {{CategoryData}}, "hconcat": [ {{filtered}}, {{own}}, {{Bare}} ] }""";

        // Act
        var scene = SceneOf(json);

        // Assert: the marks groups hold 2 (v > 4), 2 (own data) and 3 (inherited data) bars
        Assert.Equal([2, 2, 3], Items<SceneGroup>(scene, "marks").Select(g => g.Children.Count));
    }

    /// <summary>
    ///     Validates that the data and the transform of the concatenation are inherited by its charts: a calculated field of the top-level transform is what the bars show.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_TopLevelDataAndTransformAreInherited()
    {
        // Arrange: w = 10 v gives 40, 80 and 60 over the domain 0 to 80, so the bars of a chart that is 80 pixels high are 40, 80 and 60 pixels high
        const string chart = """{ "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "w", "type": "quantitative", "axis": null } } }""";
        const string json = $$"""
            {
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] },
              "transform": [ { "calculate": "datum.v * 10", "as": "w" } ],
              "hconcat": [ {{chart}}, {{chart}} ]
            }
            """;

        // Act
        var bars = Items<SceneRect>(SceneOf(json), "mark-bar");

        // Assert
        Assert.Equal([40.0, 80.0, 60.0, 40.0, 80.0, 60.0], bars.Select(b => b.Height));
    }

    /// <summary>
    ///     Validates that the title of a chart stays inside its cell (centered over its data rectangle) and that the title of the concatenation is anchored at the start, above
    ///     everything, and drawn after the cells.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ChildTitleStaysInsideItsCellAndCompositionTitleIsAnchoredAtTheStart()
    {
        // Arrange
        const string one = """{ "title": "One", "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";
        var json = $$"""{ {{CategoryData}}, "title": "All", "hconcat": [ {{one}}, {{Bare}} ] }""";

        // Act
        var scene = SceneOf(json);
        var titles = Items<SceneText>(scene, "title-text");
        var cells = Cells(scene);

        // Assert: the chart's title first (inside its cell group), the title of the concatenation last
        Assert.Equal(["One", "All"], titles.Select(t => t.Text));
        Assert.Equal(["center", "left"], titles.Select(t => t.Align));
        var entries = scene.FindAll("title-text").ToList();
        Assert.True(entries[1].OffsetY + titles[1].Y < entries[0].OffsetY + titles[0].Y);

        // Assert: the title of the chart is centered over its 100 pixel wide data rectangle
        Assert.Equal(cells[0].X + 50, entries[0].OffsetX + titles[0].X);
    }

    /// <summary>
    ///     Validates that a <c>vconcat</c> of an <c>hconcat</c> and a chart nests: the inner row is laid out as one cell of the column.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_NestedVConcatOfHConcat()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "vconcat": [ { "hconcat": [ {{Bare}}, {{Bare}} ] }, {{Bare}} ] }""";

        // Act
        var cells = Cells(SceneOf(json));

        // Assert: the inner row has its cells at 0 and 122, the chart under it at ceil(80.5) + 20 + ceil(0.5) = 102 (plus the root's 6)
        Assert.Equal([(6.0, 6.0), (128.0, 6.0), (6.0, 108.0)], cells.Select(c => (c.X, c.Y)));
    }

    /// <summary>
    ///     Validates that a layered view is a chart of a concatenation: both layers are drawn in one cell with one pair of axes.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_LayerChildRenders()
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "hconcat": [
                { "width": 100, "height": 80, "layer": [ { "mark": "bar" }, { "mark": "rule" } ],
                  "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                {{Bare}}
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: three marks groups (two layers and the bars), two cells, and one pair of axes for the layered chart plus none for the bare one
        Assert.Equal(3, Items<SceneGroup>(scene, "marks").Count);
        Assert.Equal(2, Cells(scene).Count);
        Assert.Equal(2, scene.FindAll("axis").Count());
    }

    /// <summary>
    ///     Validates that a composite mark is a chart of a concatenation and draws its parts.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_CompositeMarkChildRenders()
    {
        // Arrange
        var json = $$"""
            {
              {{CategoryData}},
              "hconcat": [
                { "width": 100, "height": 80, "mark": "errorbar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                {{Bare}}
              ]
            }
            """;

        // Act
        var (warnings, unsupported) = Report(json);
        var scene = SceneOf(json);

        // Assert: nothing is reported, and the expanded errorbar and the bars are drawn in two cells
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
        Assert.Equal(2, Cells(scene).Count);
        Assert.True(Items<SceneGroup>(scene, "marks").Count >= 2);
    }

    /// <summary>
    ///     Validates that a legend owned by a nested concatenation (the legend is independent above it) is placed beside that concatenation and not beside the whole.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_LegendOwnedByNestedConcatIsPlacedBesideIt()
    {
        // Arrange: the outer vconcat keeps the legends apart, so the inner hconcat owns the legend of its two charts
        var json = $$"""
            {
              {{CategoryData}},
              "resolve": { "legend": { "color": "independent" } },
              "vconcat": [ { "hconcat": [ {{BareColor}}, {{BareColor}} ] }, {{Bare}} ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);
        var legend = Assert.Single(scene.FindAll("legend"));

        // Assert: the legend is right of the inner row (its cells end at 228 on the canvas), at ceil(222.5) + 18 = 241 from the origin of the inner row, at its top
        var group = (SceneGroup)legend.Item;
        Assert.Equal((241.0, 0.0), (group.X, group.Y));
        Assert.Equal(cells[0].Y, legend.OffsetY);

        // Assert: the chart below starts below the inner row
        Assert.Equal(cells[0].Y + 102, cells[2].Y);
    }

    /// <summary>
    ///     Validates that the second axis of a layered chart goes to the opposite side only within that chart: the chart next to it keeps its single y axis on the left.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_SecondAxisIsSeparatedPerChart()
    {
        // Arrange: the first chart has two layers with independent y scales (two y axes), the second is a plain chart
        var json = $$"""
            {
              {{CategoryData}},
              "hconcat": [
                { "width": 100, "height": 80, "resolve": { "scale": { "y": "independent" } },
                  "layer": [ { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative" } } },
                             { "mark": "line", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "w", "type": "quantitative" } } } ] },
                { "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);
        var axes = scene.FindAll("axis").Select(e => (Left: e.OffsetX + ((SceneGroup)e.Item).X - cells[0].X, Right: e.OffsetX + ((SceneGroup)e.Item).X - cells[1].X)).ToList();

        // Assert: three y axes: the first chart's two (left, and right at 100.5), the second chart's one on the left (0.5 from its cell), not moved by the first chart's axes
        Assert.Equal(["v", "w", "v"], Texts(scene, "axis-title"));
        Assert.Equal(3, axes.Count);
        Assert.Equal(0.5, axes[0].Left);
        Assert.Equal(100.5, axes[1].Left);
        Assert.Equal(0.5, axes[2].Right);
    }

    /// <summary>
    ///     Validates that the size that <c>Measure</c> reports is the size of the surface that <c>Render</c> returns for a concatenation.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_MeasureEqualsRenderSize()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "title": "T", "hconcat": [ {{WithAxes}}, {{BareColor}} ] }""";
        var document = VegaLiteDocument.Parse(json);

        // Act
        var size = document.Measure(cancellationToken: Ct);
        using var surface = document.Render(cancellationToken: Ct);

        // Assert
        Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
    }

    /// <summary>
    ///     Validates that rendering a concatenation is deterministic, also when the same document is rendered from several threads at once.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_DeterministicAndConcurrent()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "hconcat": [ {{WithAxes}}, {{BareColor}} ] }""";
        var document = VegaLiteDocument.Parse(json);
        using var reference = document.Render(cancellationToken: Ct);
        var expected = VegaLiteTestHelpers.Snapshot(reference);
        var snapshots = new byte[6][];

        // Act
        Parallel.For(0, snapshots.Length, new ParallelOptions { CancellationToken = Ct }, i =>
        {
            using var surface = document.Render(cancellationToken: Ct);
            snapshots[i] = VegaLiteTestHelpers.Snapshot(surface);
        });

        // Assert
        Assert.All(snapshots, s => Assert.Equal(expected, s));
    }

    /// <summary>
    ///     Validates the limit on the number of views: the concatenation and its charts count, and the error is located at the concatenation.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ViewCountLimitIsEnforced()
    {
        // Arrange: three charts and the concatenation are four views
        var json = $$"""{ {{CategoryData}}, "hconcat": [ {{Bare}}, {{Bare}}, {{Bare}} ] }""";

        // Act
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxViews = 3 }));

        // Assert
        Assert.Contains("MaxViews", tooMany.Message, StringComparison.Ordinal);
        Assert.Empty(Report(json, new VegaLiteLimits { MaxViews = 4 }).Warnings);
    }

    /// <summary>
    ///     Validates the limit on the depth of nested compositions: a concatenation inside a concatenation is two levels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_DepthLimitIsEnforced()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "vconcat": [ { "hconcat": [ {{Bare}} ] } ] }""";

        // Act
        var tooDeep = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxCompositionDepth = 1 }));

        // Assert
        Assert.Contains("MaxCompositionDepth", tooDeep.Message, StringComparison.Ordinal);
        Assert.Empty(Report(json, new VegaLiteLimits { MaxCompositionDepth = 2 }).Warnings);
    }

    /// <summary>
    ///     Validates that the limit on the number of scene items is enforced for a composition before the items are created.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_SceneItemLimitIsEnforced()
    {
        // Arrange: eight charts with axes need far more than 40 items
        var charts = string.Join(", ", Enumerable.Repeat(WithAxes, 8));
        var json = $$"""{ {{CategoryData}}, "concat": [ {{charts}} ] }""";

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxSceneItems = 40 }));

        // Assert
        Assert.Contains("MaxSceneItems", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a concatenation uses only supported constructs: strict mode has nothing to throw for and nothing is delivered.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_StrictModeHasNothingToReport()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "title": "T", "vconcat": [ { "hconcat": [ {{WithAxes}}, {{BareColor}} ] }, {{Bare}} ] }""";
        var unsupported = new List<VegaLiteUnsupportedFeature>();
        var warnings = new List<VegaLiteDiagnostic>();

        // Act
        using var surface = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = unsupported.Add, WarningHandler = warnings.Add }, Ct);

        // Assert
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates that the transforms of a concatenation's charts are applied and not reported (the Phase 10 report that said they were not applied is retired).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_TransformsAreNotReported()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "hconcat": [ { "transform": [ { "calculate": "datum.v * 10", "as": "w" } ], "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "w", "type": "quantitative" } } } ] }""";

        // Act
        var (warnings, unsupported) = Report(json);

        // Assert
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates that a <c>"container"</c> width of a concatenation is ignored with a warning: the canvas is the size of the content.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_ContainerWidthIsIgnoredWithWarning()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "width": "container", "hconcat": [ {{Bare}}, {{Bare}} ] }""";

        // Act
        var (warnings, _) = Report(json);
        var size = VegaLiteDocument.Parse(json).Measure(new VegaLiteRenderOptions { ContainerWidth = 800, ContainerHeight = 600 }, Ct);

        // Assert
        Assert.Single(warnings, w => w.StartsWith("vl-composition-property-ignored:", StringComparison.Ordinal));
        Assert.Equal(new VegaLiteSize(234, 92), size);
    }

    /// <summary>
    ///     Validates that <c>autosize: fit</c> on a concatenation is padded with a warning (Vega-Lite drops the fit of a composition), and that it is not reported as unsupported.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_AutosizeFitIsPadWithWarning()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "autosize": "fit", "hconcat": [ {{Bare}}, {{Bare}} ] }""";

        // Act
        var (warnings, unsupported) = Report(json);
        var size = VegaLiteDocument.Parse(json).Measure(cancellationToken: Ct);

        // Assert
        Assert.Single(warnings, w => w.StartsWith("vl-composition-property-ignored:", StringComparison.Ordinal));
        Assert.Empty(unsupported);
        Assert.Equal(new VegaLiteSize(234, 92), size);
    }

    /// <summary>
    ///     Validates that sharing the axes of a concatenation is a warning and that the axes stay independent.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_AxisSharedResolveWarns()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "resolve": { "axis": { "y": "shared" } }, "hconcat": [ {{WithAxes}}, {{WithAxes}} ] }""";

        // Act
        var (warnings, unsupported) = Report(json);
        var scene = SceneOf(json);

        // Assert: one warning, delivered once, and two y axes
        Assert.Single(warnings, w => w.StartsWith("vl-concat-axis-shared:", StringComparison.Ordinal));
        Assert.Empty(unsupported);
        Assert.Equal(["a", "v", "a", "v"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates that a chart whose mark is not supported is reported and drawn as an empty cell, and that the other charts of the concatenation are drawn.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_UnsupportedMarkChildIsReportedAndTheRestDrawn()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "hconcat": [ { "mark": "geoshape" }, {{Bare}} ] }""";

        // Act
        var (_, unsupported) = Report(json);
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: the empty plot area has the default 300 by 300 size and the bars are drawn in the second cell, to its right
        Assert.Equal(["vl-mark-geoshape"], unsupported.Select(u => u[..u.IndexOf(':', StringComparison.Ordinal)]));
        Assert.Equal((300.0, 300.0), (cells[0].Width, cells[0].Height));
        Assert.Equal(3, Items<SceneRect>(scene, "mark-bar").Count);
        Assert.Equal(cells[0].X + 300 + 20 + 2, cells[1].X);
    }

    /// <summary>
    ///     Validates that a facet inside a concatenation is drawn like any other child: its two cells (one for each value of <c>c</c>, with the label of the value above each) are laid out
    ///     as one chart of the concatenation, and the other chart is placed after the whole facet.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Concat_FacetChildIsDrawnWithTheOtherCharts()
    {
        // Arrange
        var json = $$"""{ {{CategoryData}}, "hconcat": [ { "facet": { "field": "c", "type": "nominal" }, "spec": {{Bare}} }, {{Bare}} ] }""";

        // Act
        var (warnings, unsupported) = Report(json);
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: nothing is reported (the facet is drawn); the cells of the facet (values u, v), then the other chart, which has the three bars A, B, C
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
        Assert.Equal(3, cells.Count);
        Assert.Equal(6, Items<SceneRect>(scene, "mark-bar").Count);

        // Assert: the cells of the facet are 122 pixels apart (101 + 20 + 1); the facet's bounds are 223 wide ([-0.5, 222.5]), so the next chart starts ceil(222.5) + 20 + ceil(0.5) = 244
        // pixels after the first cell, and both start at the top of the row (the labels and the title of the facet lie above the cell and do not move it)
        Assert.Equal((122.0, 244.0, 0.0), (cells[1].X - cells[0].X, cells[2].X - cells[0].X, cells[2].Y - cells[0].Y));
    }
}
