using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of faceted views (Phase 11): the <c>facet</c> operator (a wrapped field, or a <c>row</c> and <c>column</c> mapping) and the <c>row</c>, <c>column</c> and
///     <c>facet</c> encoding channels of a unit view are drawn as a grid of cells with one cell for each value, headers with the labels of the values, the titles of the fields, and the axes that the
///     cells share once in the outer headers and footers. The charts are rendered through the public API and checked by the positions of the cells, headers and titles, by the marks that each cell
///     draws and by what is reported, with every number derived by hand from the layout rules: a one pixel <c>#ddd</c> border on each cell whose half pixel reaches outside the cell (so the next
///     column starts at <c>ceil(100.5) + 20 + ceil(0.5)</c> = 122 pixels), a header at <c>floor(-0.5)</c> = -1 whose label lies 10 + 10 pixels above the cell, and a title 10 pixels outside the headers.
/// </summary>
public class VegaLiteFacetIntegrationTests
{
    /// <summary>Three rows: <c>A</c> (value 4, column <c>u</c>, row <c>p</c>), <c>B</c> (8, <c>v</c>, <c>q</c>) and <c>C</c> (6, <c>u</c>, <c>q</c>).</summary>
    private const string Data = """
        "data": { "values": [ { "a": "A", "v": 4, "c": "u", "r": "p" }, { "a": "B", "v": 8, "c": "v", "r": "q" }, { "a": "C", "v": 6, "c": "u", "r": "q" } ] }
        """;

    /// <summary>The chart of a cell: bars of 100 by 80 pixels without axes, so that the bounds of a cell are its data rectangle and the half pixel of its border.</summary>
    private const string Bare = """{ "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }""";

    /// <summary>The same chart with both axes.</summary>
    private const string WithAxes = """{ "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }""";

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Makes a bar chart of 100 by 80 pixels with the faceting channels of a unit view written in its encoding.
    /// </summary>
    /// <param name="channels">The faceting channels, such as <c>"column": { ... }</c>.</param>
    /// <param name="axes">Whether the chart has its axes.</param>
    /// <param name="size">The size properties.</param>
    /// <returns>The specification text of a unit view.</returns>
    private static string Faceted(string channels, bool axes = false, string size = "\"width\": 100, \"height\": 80") =>
        "{ " + Data + ", " + size + ", \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"nominal\"" + (axes ? string.Empty : ", \"axis\": null")
        + " }, \"y\": { \"field\": \"v\", \"type\": \"quantitative\"" + (axes ? string.Empty : ", \"axis\": null") + " }, " + channels + " } }";

    /// <summary>
    ///     Makes a facet operator over a chart.
    /// </summary>
    /// <param name="facet">The <c>facet</c> value.</param>
    /// <param name="spec">The chart.</param>
    /// <param name="extra">More properties of the facet view, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string FacetOver(string facet, string spec, string extra = "") =>
        "{ " + Data + ", " + extra + " \"facet\": " + facet + ", \"spec\": " + spec + " }";

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the position of a group: the corner of its origin on the canvas.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The position.</returns>
    private static (double X, double Y) Position(SceneEntry entry) => (entry.OffsetX + ((SceneGroup)entry.Item).X, entry.OffsetY + ((SceneGroup)entry.Item).Y);

    /// <summary>
    ///     Gets the cells of a composition: the plot areas of its charts in drawing order (the first view group is the root of the composition, which has no size).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The position of the corner of each cell on the canvas.</returns>
    private static List<(double X, double Y)> Cells(Scene scene) => [.. scene.FindAll("view").Skip(1).Select(Position)];

    /// <summary>
    ///     Gets the position of every group of a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The positions in drawing order.</returns>
    private static List<(double X, double Y)> Groups(Scene scene, string role) => [.. scene.FindAll(role).Select(Position)];

    /// <summary>
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. scene.FindAll(role).Select(e => ((SceneText)e.Item).Text)];

    /// <summary>
    ///     Gets the texts of the labels and titles inside each group of a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role of the groups, such as <c>column-header</c>.</param>
    /// <returns>For each group, the texts of the text items inside it.</returns>
    private static List<string> HeaderTexts(Scene scene, string role) =>
        [.. scene.FindAll(role).Select(e => string.Join("|", Scene.Enumerate((SceneGroup)e.Item).Select(x => x.Item).OfType<SceneText>().Select(t => t.Text)))];

    /// <summary>
    ///     Gets the heights of the bars of a scene in drawing order.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The heights.</returns>
    private static List<double> BarHeights(Scene scene) => [.. scene.FindAll("mark-bar").Select(e => ((SceneRect)e.Item).Height)];

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
    ///     Validates that a <c>column</c> channel draws one cell for each value of the field (<c>u</c> and <c>v</c>), 122 pixels apart, with a header of the label of each value just
    ///     above its cell and the title of the field ten pixels above the headers, centered over the cells.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_ColumnChannelDrawsOneCellPerValueWithHeaderLabels()
    {
        // Arrange
        var json = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\" }");

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: two cells on one row, 101 + 20 + 1 = 122 pixels apart; the data of u (A, C) and v (B) is partitioned
        Assert.Equal(2, cells.Count);
        Assert.Equal((122.0, 0.0), (cells[1].X - cells[0].X, cells[1].Y - cells[0].Y));
        Assert.Equal(3, BarHeights(scene).Count);

        // Assert: one column header over each cell at floor(-0.5) = -1 with the label of its value; nothing for rows or footers
        Assert.Equal([(cells[0].X, cells[0].Y - 1), (cells[1].X, cells[1].Y - 1)], Groups(scene, "column-header"));
        Assert.Equal(["u", "v"], HeaderTexts(scene, "column-header"));
        Assert.Empty(Groups(scene, "row-header"));
        Assert.Empty(Groups(scene, "column-footer"));
        Assert.Empty(Groups(scene, "row-title"));

        // Assert: the column title is centered on the grid, round(-0.5 + 223 / 2) = 111, and 10 pixels above the headers, whose labels reach 20 up from -1: -1 - 20 - 10 = -31
        Assert.Equal(["c"], Texts(scene, "title-text").Where(t => t == "c").ToList());
        Assert.Equal([(cells[0].X + 111, cells[0].Y - 31)], Groups(scene, "column-title"));
    }

    /// <summary>
    ///     Validates that a <c>row</c> and a <c>column</c> channel draw the cross product of their values, row-major, with empty cells for the combinations that have no data: the
    ///     rows <c>p</c>, <c>q</c> and the columns <c>u</c>, <c>v</c> are four cells, and the cell of <c>p</c> and <c>v</c> has no bar.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_RowAndColumnCrossProductDrawsEmptyCells()
    {
        // Arrange
        var json = Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" }");

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: four cells, 122 pixels apart in x and 81 + 20 + 1 = 102 pixels in y
        Assert.Equal(4, cells.Count);
        Assert.Equal(
            [(0.0, 0.0), (122.0, 0.0), (0.0, 102.0), (122.0, 102.0)],
            cells.Select(c => (c.X - cells[0].X, c.Y - cells[0].Y)));

        // Assert: the data of (p, u) is A, of (q, v) is B and of (q, u) is C; the cell (p, v) is empty, so three bars are drawn in all, in cell order: A, C, B
        Assert.Equal(3, BarHeights(scene).Count);

        // Assert: a header for each row at x = -1 and for each column at y = -1, the labels in the order of the values, the footers none (the chart has no axes)
        Assert.Equal([(cells[0].X - 1, cells[0].Y), (cells[0].X - 1, cells[2].Y)], Groups(scene, "row-header"));
        Assert.Equal([(cells[0].X, cells[0].Y - 1), (cells[1].X, cells[0].Y - 1)], Groups(scene, "column-header"));
        Assert.Equal(["p", "q"], HeaderTexts(scene, "row-header"));
        Assert.Equal(["u", "v"], HeaderTexts(scene, "column-header"));
        Assert.Empty(Groups(scene, "column-footer"));

        // Assert: the row title is rotated, round(-0.5 + 183 / 2) = 91 pixels down from the first cell and 31 left of it; the column title 31 above, centered over 223 pixels
        Assert.Equal([(cells[0].X - 31, cells[0].Y + 91)], Groups(scene, "row-title"));
        Assert.Equal([(cells[0].X + 111, cells[0].Y - 31)], Groups(scene, "column-title"));
        var rowTitle = Assert.Single(scene.FindAll("title-text"), e => ((SceneText)e.Item).Text == "r");
        Assert.Equal(-90.0, ((SceneText)rowTitle.Item).Angle);
    }

    /// <summary>
    ///     Validates the wrapped facet: one field, three values and <c>columns: 2</c> make two rows of cells (the third wraps), each cell is titled with its value (a wrapped
    ///     facet's labels are the titles of the cells, not headers), and the title of the field is above the cells at the edge of their bounds, without the extra ten pixels.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_WrapFacetUsesColumnsAndCellLabelTitles()
    {
        // Arrange: the column field has three values (u, v, w)
        var data = """
            "data": { "values": [ { "a": "A", "v": 4, "c": "u" }, { "a": "B", "v": 8, "c": "v" }, { "a": "C", "v": 6, "c": "w" } ] }
            """;
        var json = "{ " + data + ", \"columns\": 2, \"facet\": { \"field\": \"c\", \"type\": \"nominal\" }, \"spec\": " + Bare + " }";

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: the third cell wraps to the second row; the labels of the cells lie 20 above them, so the rows are ceil(80.5) + 20 + ceil(20.5) = 122 pixels apart
        Assert.Equal(
            [(0.0, 0.0), (122.0, 0.0), (0.0, 122.0)],
            cells.Select(c => (c.X - cells[0].X, c.Y - cells[0].Y)));

        // Assert: the labels are the titles of the cells (inside them), 20 above the origin of each; there are no headers
        Assert.Equal(["u", "v", "w"], Texts(scene, "title-text").Where(t => t != "c").ToList());
        Assert.Empty(Groups(scene, "column-header"));
        Assert.Empty(Groups(scene, "row-header"));

        // Assert: the title of the field is centered, round(-0.5 + 223 / 2) = 111, and at floor(-20.5) = -21 (the edge of the labels' bounds, no offset)
        Assert.Equal([(cells[0].X + 111, cells[0].Y - 21)], Groups(scene, "column-title"));
    }

    /// <summary>
    ///     Validates that a wrapped facet without <c>columns</c> is one row of cells, and that a <c>row</c> channel alone is one column.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_WrapWithoutColumnsIsOneRowAndRowAloneIsOneColumn()
    {
        // Act
        var wrapped = Cells(SceneOf(FacetOver("{ \"field\": \"r\", \"type\": \"nominal\" }", Bare)));
        var rowOnly = Cells(SceneOf(Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\" }")));

        // Assert: p and q side by side (the label above them lifts nothing), and one above the other
        Assert.Equal([(0.0, 0.0), (122.0, 0.0)], wrapped.Select(c => (c.X - wrapped[0].X, c.Y - wrapped[0].Y)));
        Assert.Equal([(0.0, 0.0), (0.0, 102.0)], rowOnly.Select(c => (c.X - rowOnly[0].X, c.Y - rowOnly[0].Y)));
    }

    /// <summary>
    ///     Validates that the axes that the cells share (the default: shared scales) are drawn once in the outer headers and footers: the x axis under each column (in its footer) and
    ///     the y axis left of each row (in its header), and that the cells keep nothing of them but the grid lines.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SharedXAxisIsOnceUnderEachColumnAndSharedYAxisOncePerRow()
    {
        // Arrange
        var json = Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" }", axes: true);

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: a footer under each column at ceil(182.5) = 183 below the first row, holding the x axis; a header at the left of each row holding the y axis and the label
        Assert.Equal([(cells[0].X, cells[0].Y + 183), (cells[1].X, cells[0].Y + 183)], Groups(scene, "column-footer"));
        Assert.Equal(2, scene.FindAll("column-footer").Count(e => Scene.Enumerate((SceneGroup)e.Item).Any(x => x.Item.Role == "axis")));
        Assert.Equal(2, scene.FindAll("row-header").Count(e => Scene.Enumerate((SceneGroup)e.Item).Any(x => x.Item.Role == "axis")));

        // Assert: four axes in all (two x, two y) with their titles, none of them in a cell
        Assert.Equal(["a", "a", "v", "v"], Texts(scene, "axis-title").Order(StringComparer.Ordinal).ToList());
        var cellGroups = scene.FindAll("view").Skip(1).Select(e => (SceneGroup)e.Item).ToList();
        Assert.All(cellGroups, g => Assert.DoesNotContain(g.Children, c => c.Role == "axis"));

        // Assert: each cell keeps the grid of the y axis (a band scale has none for x)
        Assert.All(cellGroups, g => Assert.Single(g.Children, c => c.Role == "axis-grid-group"));
    }

    /// <summary>
    ///     Validates that an independent y scale keeps the y axes in the cells (one in each) while the x axes, which stay shared, are in the footers, and that the row headers
    ///     then hold the labels only.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_IndependentAxisStaysInTheCells()
    {
        // Arrange
        var json = FacetOver(
            "{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }",
            WithAxes,
            "\"resolve\": { \"scale\": { \"y\": \"independent\" } },");

        // Act
        var scene = SceneOf(json);
        var cellGroups = scene.FindAll("view").Skip(1).Select(e => (SceneGroup)e.Item).ToList();

        // Assert: each of the two cells has its own y axis; the x axes (shared) are in the two column footers; the cells have no x axis
        Assert.Equal(2, cellGroups.Count);
        Assert.All(cellGroups, g => Assert.Single(g.Children, c => c.Role == "axis"));
        Assert.Equal(2, scene.FindAll("column-footer").Count(e => Scene.Enumerate((SceneGroup)e.Item).Any(x => x.Item.Role == "axis")));
        Assert.Empty(Groups(scene, "row-header"));
    }

    /// <summary>
    ///     Validates the independent scale: a row facet whose y scale is independent gives each row its own domain (row <c>p</c> has only the value 4, so its domain is 0 to 4 and bar A fills the
    ///     cell; row <c>q</c> has 8 and 6), while a shared scale uses 0 to 8 for all rows.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_IndependentYScaleGivesEachRowItsDomain()
    {
        // Arrange
        var channel = "\"row\": { \"field\": \"r\", \"type\": \"nominal\" }";
        var independent = FacetOver("{ \"row\": { \"field\": \"r\", \"type\": \"nominal\" } }", Bare, "\"resolve\": { \"scale\": { \"y\": \"independent\" } },");

        // Act
        var shared = BarHeights(SceneOf(Faceted(channel)));
        var own = BarHeights(SceneOf(independent));

        // Assert: shared 0 to 8 over 80 pixels is 10 pixels a unit: A 40, B 80, C 60 (in row order p: A; q: B, C); independent: row p is 0 to 4 (A 80), row q is 0 to 8 (B 80, C 60)
        Assert.Equal([40.0, 80.0, 60.0], shared);
        Assert.Equal([80.0, 80.0, 60.0], own);
    }

    /// <summary>
    ///     Validates that a shared scale is the union of all the cells: the band scale of x has the categories A, B and C in every cell, so the cell of <c>v</c> (which has only B) draws its bar
    ///     in the middle of three bands, and with a step width every cell is 3 * 20 = 60 pixels wide.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SharedScaleUsesTheUnionOfAllCellsAndStepWidthTheirCategories()
    {
        // Arrange: the cell of v holds only the category B
        var json = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\" }", size: "\"width\": { \"step\": 20 }, \"height\": 80");

        // Act
        var scene = SceneOf(json);
        var cellGroups = scene.FindAll("view").Skip(1).Select(e => (SceneGroup)e.Item).ToList();
        var bars = scene.FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Assert: both cells are 3 steps of 20 pixels wide, whatever they hold
        Assert.Equal([60.0, 60.0], cellGroups.Select(g => g.Width));

        // Assert: the bar of B (the only one of the cell v) is in the middle band: the band step is 20 and the bar 18 wide (padding 0.1) at 20 + 1 = 21
        var bar = bars[^1];
        Assert.Equal(18.0, bar.Width, 6);
        Assert.Equal(21.0, bar.X, 6);
    }

    /// <summary>
    ///     Validates that an independent step scale sizes each cell by its own categories (u has A and C: 40 pixels, v has B: 20 pixels), that the cells are then not aligned (the second cell
    ///     starts after the width of the first, <c>ceil(40.5) + 20 + ceil(0.5)</c> = 62 pixels), and that an explicit <c>align: all</c> makes every column as wide as the widest cell.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_IndependentStepSizesCellsByOwnCategoriesAndAlignFollowsTheScale()
    {
        // Arrange: three cells of 40, 20 and 60 pixels (u: A, C; v: B; w: A, B, C)
        var data = """
            "data": { "values": [ { "a": "A", "v": 4, "c": "u" }, { "a": "C", "v": 6, "c": "u" }, { "a": "B", "v": 8, "c": "v" }, { "a": "A", "v": 1, "c": "w" }, { "a": "B", "v": 2, "c": "w" }, { "a": "C", "v": 3, "c": "w" } ] }
            """;
        var spec = "{ \"width\": { \"step\": 20 }, \"height\": 80, \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"nominal\", \"axis\": null }, \"y\": { \"field\": \"v\", \"type\": \"quantitative\", \"axis\": null } } }";
        var column = "\"facet\": { \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }, \"spec\": " + spec;
        var independent = "{ " + data + ", \"resolve\": { \"scale\": { \"x\": \"independent\" } }, " + column + " }";
        var aligned = "{ " + data + ", \"align\": \"all\", \"resolve\": { \"scale\": { \"x\": \"independent\" } }, " + column + " }";

        // Act
        var scene = SceneOf(independent);
        var cells = Cells(scene);
        var alignedCells = Cells(SceneOf(aligned));

        // Assert: each cell is the width of its own categories
        Assert.Equal([40.0, 20.0, 60.0], scene.FindAll("view").Skip(1).Select(e => ((SceneGroup)e.Item).Width));

        // Assert: not aligned: the columns follow each other, 62 pixels after the first cell and ceil(20.5) + 20 + 1 = 42 after the second
        Assert.Equal([0.0, 62.0, 104.0], cells.Select(c => c.X - cells[0].X));

        // Assert: aligned: all columns have the width of the largest cell, ceil(60.5) = 61, so they are 61 + 20 + 1 = 82 pixels apart
        Assert.Equal([0.0, 82.0, 164.0], alignedCells.Select(c => c.X - alignedCells[0].X));
    }

    /// <summary>
    ///     Validates that an aggregate of a cell's encoding runs on the rows of the cell: the mean of v for A is 3 in the cell u (values 2 and 4) and 10 in the cell v, so the bars
    ///     are 24 and 80 pixels high on the shared domain 0 to 10, not the 5.33 of all the rows.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_AggregateRunsPerCell()
    {
        // Arrange
        var json = """
            { "data": { "values": [ { "a": "A", "v": 2, "c": "u" }, { "a": "A", "v": 4, "c": "u" }, { "a": "A", "v": 10, "c": "v" } ] },
              "width": 100, "height": 80, "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "aggregate": "mean", "field": "v", "type": "quantitative", "axis": null },
                            "column": { "field": "c", "type": "nominal" } } }
            """;

        // Act
        var heights = BarHeights(SceneOf(json));

        // Assert
        Assert.Equal([24.0, 80.0], heights);
    }

    /// <summary>
    ///     Validates that a stack runs on the rows of the cell: in the cell u the segments of A are 1 and 2 (stacked to 3), in the cell v the segment is 4; on the shared domain 0 to 4 over 80 pixels
    ///     they are 20, 40 and 80 pixels high, and the segment of the cell v starts at the bottom (it is not stacked on the other cell's rows).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_StackRunsPerCell()
    {
        // Arrange
        var json = """
            { "data": { "values": [ { "a": "A", "v": 1, "s": "s", "c": "u" }, { "a": "A", "v": 2, "s": "t", "c": "u" }, { "a": "A", "v": 4, "s": "s", "c": "v" } ] },
              "width": 100, "height": 80, "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "aggregate": "sum", "axis": null },
                            "color": { "field": "s", "type": "nominal", "legend": null }, "column": { "field": "c", "type": "nominal" } } }
            """;

        // Act
        var scene = SceneOf(json);
        var bars = scene.FindAll("mark-bar").Select(e => (SceneRect)e.Item).ToList();

        // Assert: the rows are stacked within their cell: heights 20, 40 and 80; the segment of v spans the whole cell (y 0 to 80)
        Assert.Equal([20.0, 40.0, 80.0], bars.Select(b => b.Height));
        Assert.Equal(0.0, bars[2].Y, 6);
    }

    /// <summary>
    ///     Validates the order of the cells (and so of the headers): ascending by default, descending with <c>"sort": "descending"</c>, an explicit array first and the other values in the
    ///     order they first appear, and <c>sort: null</c> ascending as Vega-Lite orders it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SortDescendingArrayAndNull()
    {
        // Arrange: the values appear in the order b, a, z
        string Spec(string sort) => "{ \"data\": { \"values\": [ { \"a\": \"A\", \"v\": 1, \"c\": \"b\" }, { \"a\": \"A\", \"v\": 1, \"c\": \"a\" }, { \"a\": \"A\", \"v\": 1, \"c\": \"z\" } ] }, \"width\": 20, \"height\": 20, \"mark\": \"bar\", "
            + "\"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"nominal\", \"axis\": null }, \"y\": { \"field\": \"v\", \"type\": \"quantitative\", \"axis\": null }, \"column\": { \"field\": \"c\", \"type\": \"nominal\"" + sort + " } } }";

        // Act
        var ascending = HeaderTexts(SceneOf(Spec(string.Empty)), "column-header");
        var descending = HeaderTexts(SceneOf(Spec(", \"sort\": \"descending\"")), "column-header");
        var array = HeaderTexts(SceneOf(Spec(", \"sort\": [ \"z\", \"a\" ]")), "column-header");
        var none = HeaderTexts(SceneOf(Spec(", \"sort\": null")), "column-header");

        // Assert: the listed values first in the order of the array, then the values it does not list in the order they first appear (b)
        Assert.Equal(["a", "b", "z"], ascending);
        Assert.Equal(["z", "b", "a"], descending);
        Assert.Equal(["z", "a", "b"], array);
        Assert.Equal(["a", "b", "z"], none);
    }

    /// <summary>
    ///     Validates that a null value is a cell of its own, labeled <c>null</c> as Vega-Lite does, first in ascending order.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_NullValueIsItsOwnCell()
    {
        // Arrange
        var json = """
            { "data": { "values": [ { "a": "A", "v": 1, "c": "b" }, { "a": "B", "v": 2, "c": null }, { "a": "C", "v": 3, "c": "a" } ] },
              "width": 20, "height": 20, "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null }, "column": { "field": "c", "type": "nominal" } } }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: three cells, the null value first, each with its bar
        Assert.Equal(["null", "a", "b"], HeaderTexts(scene, "column-header"));
        Assert.Equal(3, BarHeights(scene).Count);
    }

    /// <summary>
    ///     Validates that the properties of the header change its text: the label font size, color and angle of the labels, the font size of the title, and a title that the header or the
    ///     field definition writes.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_HeaderPropertiesChangeLabelAndTitleStyle()
    {
        // Arrange
        var header = "\"header\": { \"labelFontSize\": 14, \"labelColor\": \"red\", \"titleFontSize\": 16, \"title\": \"Group\" }";
        var json = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"title\": \"Overridden\", " + header + " }");

        // Act
        var scene = SceneOf(json);
        var labels = scene.FindAll("column-header").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(e => e.Item).OfType<SceneText>().ToList();
        var title = Assert.Single(scene.FindAll("column-title").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(e => e.Item).OfType<SceneText>());

        // Assert: the labels are 14 pixel red, the title 16 pixel and written by the header (it wins over the title of the field definition)
        Assert.All(labels, t => Assert.Equal((14.0, "red"), (t.FontSize, t.Fill)));
        Assert.Equal((16.0, "Group"), (title.FontSize, title.Text));
    }

    /// <summary>
    ///     Validates the defaults of the header text: the labels are 10 pixel normal, the title 11 pixel bold, and the title is the name of the field unless the field definition gives one.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_HeaderDefaultsAreTenAndElevenBoldWithTheFieldNameAsTitle()
    {
        // Arrange
        var named = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\" }");
        var titled = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"title\": \"Cat\" }");

        // Act
        var scene = SceneOf(named);
        var label = scene.FindAll("column-header").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(e => e.Item).OfType<SceneText>().First();
        var title = scene.FindAll("column-title").SelectMany(e => Scene.Enumerate((SceneGroup)e.Item)).Select(e => e.Item).OfType<SceneText>().Single();

        // Assert
        Assert.Equal((10.0, "normal"), (label.FontSize, label.FontWeight ?? "normal"));
        Assert.Equal((11.0, "c", "bold"), (title.FontSize, title.Text, title.FontWeight));
        Assert.Contains("Cat", Texts(SceneOf(titled), "title-text"));
    }

    /// <summary>
    ///     Validates that <c>"title": null</c> (in the header or in the field definition) draws no title, and that <c>labels: false</c> draws no labels: with neither axes nor labels there are
    ///     no header groups at all.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_HeaderTitleNullAndLabelsFalseHideTheirText()
    {
        // Arrange
        var noTitle = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"header\": { \"title\": null } }");
        var noLabels = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"header\": { \"labels\": false } }");
        var nothing = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"header\": { \"labels\": false, \"title\": null } }");
        var fieldTitle = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"title\": null }");

        // Act
        var withoutTitle = SceneOf(noTitle);
        var withoutLabels = SceneOf(noLabels);
        var bare = SceneOf(nothing);
        var titleOff = SceneOf(fieldTitle);

        // Assert
        Assert.Empty(Groups(withoutTitle, "column-title"));
        Assert.Equal(["u", "v"], HeaderTexts(withoutTitle, "column-header"));
        Assert.Empty(Groups(withoutLabels, "column-header"));
        Assert.Single(Groups(withoutLabels, "column-title"));
        Assert.Empty(Groups(bare, "column-header"));
        Assert.Empty(Groups(bare, "column-title"));
        Assert.Empty(Groups(titleOff, "column-title"));
    }

    /// <summary>
    ///     Validates that a <c>header: null</c> switches off both the labels and the title of a channel.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_HeaderNullHidesLabelsAndTitle()
    {
        // Arrange
        var json = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"header\": null }");

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Empty(Groups(scene, "column-header"));
        Assert.Empty(Groups(scene, "column-title"));
        Assert.Equal(2, Cells(scene).Count);
    }

    /// <summary>
    ///     Validates that the labels are formatted by the <c>format</c> of the header: a number with a d3 format, a date with a time format.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_HeaderLabelsUseTheFormat()
    {
        // Arrange
        var number = """
            { "data": { "values": [ { "a": "A", "v": 1, "n": 1 }, { "a": "A", "v": 2, "n": 2.5 } ] }, "width": 20, "height": 20, "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null },
                            "column": { "field": "n", "type": "quantitative", "header": { "format": ".1f" } } } }
            """;
        var date = """
            { "data": { "values": [ { "a": "A", "v": 1, "d": "2020-01-01" }, { "a": "A", "v": 2, "d": "2021-06-15" } ] }, "width": 20, "height": 20, "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null },
                            "column": { "field": "d", "type": "temporal", "header": { "format": "%Y" } } } }
            """;

        // Act / Assert
        Assert.Equal(["1.0", "2.5"], HeaderTexts(SceneOf(number), "column-header"));
        Assert.Equal(["2020", "2021"], HeaderTexts(SceneOf(date), "column-header"));
    }

    /// <summary>
    ///     Validates that the color legend of the cells is shared (one legend, the union of the values) and drawn outside the grid, at <c>ceil(222.5) + 18</c> = 241 pixels from the origin of the
    ///     facet and at its top.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SharedLegendIsOutsideTheGrid()
    {
        // Arrange: the color shows the row r in a facet by column
        var json = Faceted("\"color\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" }");

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);
        var legend = Assert.Single(scene.FindAll("legend"));

        // Assert: one legend with both values (p, q), right of the two cells and level with the origin of the facet, whose group starts at the first cell
        Assert.Equal(["p", "q"], Texts(scene, "legend-label"));
        Assert.Equal((cells[0].X + 241, cells[0].Y), Position(legend));
    }

    /// <summary>
    ///     Validates that the <c>spec</c> of a facet can be a layered view: each cell draws its layers (bars and a rule), and the layers share the axes of the cell.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_LayerSpecBody()
    {
        // Arrange
        var spec = """
            { "width": 100, "height": 80, "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } },
                { "mark": "rule", "encoding": { "y": { "aggregate": "mean", "field": "v", "type": "quantitative", "axis": null } } } ] }
            """;
        var json = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", spec);

        // Act
        var scene = SceneOf(json);

        // Assert: three bars (A, C in u; B in v) and one rule for each of the two cells, the bar heights on the shared domain 0 to 8
        Assert.Equal(2, Cells(scene).Count);
        Assert.Equal([40.0, 60.0, 80.0], BarHeights(scene));
        Assert.Equal(2, scene.FindAll("mark-rule").Count());
    }

    /// <summary>
    ///     Validates that a unit view with a <c>facet</c> channel is the same drawing as the <c>facet</c> operator over the same chart: the same cells, labels and title.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_UnitSpecWithFacetEncodingEqualsFacetOperator()
    {
        // Arrange
        var channel = Faceted("\"facet\": { \"field\": \"c\", \"type\": \"nominal\", \"columns\": 2 }");
        var operatorForm = FacetOver("{ \"field\": \"c\", \"type\": \"nominal\" }", Bare, "\"columns\": 2,");

        // Act
        var fromChannel = SceneOf(channel);
        var fromOperator = SceneOf(operatorForm);
        var channelCells = Cells(fromChannel);
        var operatorCells = Cells(fromOperator);

        // Assert
        Assert.Equal(operatorCells.Select(c => (c.X - operatorCells[0].X, c.Y - operatorCells[0].Y)), channelCells.Select(c => (c.X - channelCells[0].X, c.Y - channelCells[0].Y)));
        Assert.Equal(Texts(fromOperator, "title-text"), Texts(fromChannel, "title-text"));
        Assert.Equal(BarHeights(fromOperator), BarHeights(fromChannel));
        Assert.Equal((fromOperator.Width, fromOperator.Height), (fromChannel.Width, fromChannel.Height));
    }

    /// <summary>
    ///     Validates that a chart with data of its own is not partitioned: the cells are those of the facet's data (u and v), and each cell draws all three rows of the chart's data.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_OwnDataInBodyIsNotPartitioned()
    {
        // Arrange: the facet has the rows that decide the cells, the chart three rows of its own
        var spec = """
            { "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] }, "width": 100, "height": 80, "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } } }
            """;
        var json = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", spec);

        // Act
        var scene = SceneOf(json);

        // Assert: two cells with three bars each
        Assert.Equal(2, Cells(scene).Count);
        Assert.Equal([40.0, 80.0, 60.0, 40.0, 80.0, 60.0], BarHeights(scene));
    }

    /// <summary>
    ///     Validates that a <c>columns</c> next to a <c>row</c> and <c>column</c> mapping is ignored with a warning (the cells have one column for each value of the column field).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_ColumnsIsIgnoredForAColumnFieldWithWarning()
    {
        // Arrange
        var json = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", Bare, "\"columns\": 1,");

        // Act
        var (warnings, unsupported) = Report(json);
        var cells = Cells(SceneOf(json));

        // Assert: still one row of two cells
        Assert.Single(warnings, w => w.StartsWith("vl-composition-columns-ignored:", StringComparison.Ordinal));
        Assert.Empty(unsupported);
        Assert.Equal(cells[0].Y, cells[1].Y);
    }

    /// <summary>
    ///     Validates the spacing and the bounds of a facet: a <c>spacing</c> of 40 between the columns, and a flush layout that ignores the axes (the data rectangles are 100 + 20 apart).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SpacingAndFlushBoundsApply()
    {
        // Arrange
        var spaced = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", Bare, "\"spacing\": 40,");
        var flush = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", WithAxes, "\"bounds\": \"flush\",");
        var full = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", WithAxes);

        // Act
        var spacedCells = Cells(SceneOf(spaced));
        var flushCells = Cells(SceneOf(flush));
        var fullCells = Cells(SceneOf(full));

        // Assert: 101 + 40 + 1 = 142; flush lays out the data rectangles (100 + 20 = 120), full the bounds with the half pixel borders (122)
        Assert.Equal(142.0, spacedCells[1].X - spacedCells[0].X);
        Assert.Equal(120.0, flushCells[1].X - flushCells[0].X);
        Assert.Equal(122.0, fullCells[1].X - fullCells[0].X);
    }

    /// <summary>
    ///     Validates the header sides that are drawn: <c>labelOrient: bottom</c> puts the labels of a column into the footers (below each cell, at <c>ceil(80.5)</c> = 81) instead of the headers and
    ///     <c>titleOrient: bottom</c> puts the title below the footers, centered, ten pixels below their edge (81 + 20 + 10 = 111 pixels below the cells' origin).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_ColumnLabelsAndTitleAtTheBottomAreFootersAndTitleBelow()
    {
        // Arrange
        var json = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\", \"header\": { \"labelOrient\": \"bottom\", \"titleOrient\": \"bottom\" } }");

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);
        var (warnings, unsupported) = Report(json);

        // Assert: the labels are in the footers, none in headers; the title is below
        Assert.Equal([(cells[0].X, cells[0].Y + 81), (cells[1].X, cells[0].Y + 81)], Groups(scene, "column-footer"));
        Assert.Equal(["u", "v"], HeaderTexts(scene, "column-footer"));
        Assert.Empty(Groups(scene, "column-header"));
        Assert.Equal([(cells[0].X + 111, cells[0].Y + 111)], Groups(scene, "column-title"));

        // Assert: these sides are drawn, so nothing is reported
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates that <c>labelOrient: right</c> and <c>titleOrient: right</c> on a row put the labels into row footers right of the cells (at <c>ceil(100.5)</c> = 101), the shared y axis stays
    ///     in the row headers on the left, and the title is ten pixels right of the footers, which are 20 wide: 101 + 20 + 10 = 131.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_RowLabelsAndTitleAtTheRightAreFootersAndTitleBeside()
    {
        // Arrange
        var json = Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\", \"header\": { \"labelOrient\": \"right\", \"titleOrient\": \"right\" } }", axes: true);

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);
        var title = Assert.Single(scene.FindAll("title-text"), e => ((SceneText)e.Item).Text == "r");

        // Assert: a footer for each row at x = 101 with the label, a header with the axis at x = -1
        Assert.Equal([(cells[0].X + 101, cells[0].Y), (cells[0].X + 101, cells[1].Y)], Groups(scene, "row-footer"));
        Assert.Equal(["p", "q"], HeaderTexts(scene, "row-footer"));
        Assert.Equal([(cells[0].X - 1, cells[0].Y), (cells[0].X - 1, cells[1].Y)], Groups(scene, "row-header"));
        Assert.DoesNotContain("p", HeaderTexts(scene, "row-header").SelectMany(t => t.Split('|')));

        // Assert: the title is right of the footers (131), centered along the rows (round(-0.5 + 183 / 2) = 91) and turned the other way (90 degrees)
        Assert.Equal([(cells[0].X + 131, cells[0].Y + 91)], Groups(scene, "row-title"));
        Assert.Equal(90.0, ((SceneText)title.Item).Angle);
    }

    /// <summary>
    ///     Validates the limit on the number of cells: a facet of 3 rows and 2 columns (the data has 3 values for the row and 2 for the column) is more than the limit of views allows, and
    ///     the error is raised before any cell is created, located at the facet field.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_CellLimitIsEnforcedBeforeAllocation()
    {
        // Arrange: the facet and its chart are two views; six cells of one view each, with the facet itself, are seven (more than the limit of six)
        var data = """
            "data": { "values": [ { "a": "A", "v": 1, "c": "u", "r": "p" }, { "a": "A", "v": 1, "c": "v", "r": "q" }, { "a": "A", "v": 1, "c": "v", "r": "s" } ] }
            """;
        var json = "{ " + data + ", \"width\": 20, \"height\": 20, \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"nominal\" }, \"y\": { \"field\": \"v\", \"type\": \"quantitative\" }, "
            + "\"row\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" } } }";

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxViews = 6 }));

        // Assert
        Assert.Contains("MaxViews", error.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/row", error.Location.Pointer);
        Assert.Empty(Report(json, new VegaLiteLimits { MaxViews = 7 }).Unsupported);
    }

    /// <summary>
    ///     Validates that the parts of a facet that are not drawn are reported with their identifiers and the rest of the chart is drawn: a binned field, a sort by an aggregate, and the
    ///     header's side, label expression and custom format type.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_NotDrawnPartsAreReported()
    {
        // Arrange
        var json = FacetOver(
            "{ \"row\": { \"field\": \"r\", \"type\": \"nominal\", \"sort\": { \"op\": \"count\" }, \"header\": { \"labelOrient\": \"top\", \"labelExpr\": \"datum.label\" } },"
            + " \"column\": { \"field\": \"v\", \"type\": \"quantitative\", \"bin\": true, \"header\": { \"formatType\": \"custom\" } } }",
            Bare);

        // Act
        var (_, unsupported) = Report(json);
        var ids = unsupported.Select(u => u[..u.IndexOf(':', StringComparison.Ordinal)]).Order(StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(
            ["vl-expr-label", "vl-facet-field-transform", "vl-facet-sort", "vl-format-type-custom", "vl-header-orient"],
            ids);
    }

    /// <summary>
    ///     Validates that a facet uses only supported constructs: strict mode has nothing to throw for and nothing is delivered.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_StrictModeHasNothingToReport()
    {
        // Arrange
        var json = Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\", \"header\": { \"labelAngle\": 0 } }, \"column\": { \"field\": \"c\", \"type\": \"nominal\", \"sort\": \"descending\" }, "
            + "\"color\": { \"field\": \"c\", \"type\": \"nominal\" }", axes: true);
        var unsupported = new List<VegaLiteUnsupportedFeature>();
        var warnings = new List<VegaLiteDiagnostic>();

        // Act
        using var surface = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = unsupported.Add, WarningHandler = warnings.Add }, Ct);

        // Assert
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates that the limit on the number of scene items is enforced for a facet before the items are created.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SceneItemLimitIsEnforced()
    {
        // Arrange: four cells with axes and headers need far more than 40 items
        var json = Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" }", axes: true);

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxSceneItems = 40 }));

        // Assert
        Assert.Contains("MaxSceneItems", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the shared axes of a facet are charged to the scene item limit only for the cells that supply a header (the first column and the first row), the other cells
    ///     charging their grids only: a 5 by 5 grid whose scene has exactly the items that the limit allows renders, where charging the full axes of all 25 cells would have failed.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_SharedAxesAreChargedOncePerHeader()
    {
        // Arrange: 25 rows of values, each its own row and column
        var values = string.Join(", ", Enumerable.Range(0, 25).Select(i => $"{{ \"a\": \"A\", \"v\": {i}, \"r\": \"r{i / 5}\", \"c\": \"c{i % 5}\" }}"));
        var json = "{ \"data\": { \"values\": [ " + values + " ] }, \"width\": 60, \"height\": 40, \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"nominal\" }, "
            + "\"y\": { \"field\": \"v\", \"type\": \"quantitative\" }, \"row\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" } } }";
        var items = SceneOf(json).CountItems();

        // Act
        var (warnings, _) = Report(json, new VegaLiteLimits { MaxSceneItems = items });

        // Assert: the scene is what the limit allows
        Assert.Empty(warnings);
        Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxSceneItems = items - 1 }));
    }

    /// <summary>
    ///     Validates that a canceled token stops the work of a facet.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_Cancellation()
    {
        // Arrange
        var json = Faceted("\"column\": { \"field\": \"c\", \"type\": \"nominal\" }");
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => VegaLiteDocument.Parse(json).Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates that rendering a facet is deterministic, also when the same document is rendered from several threads at once, and that <c>Measure</c> reports the size of the surface that
    ///     <c>Render</c> returns.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_DeterministicConcurrentAndMeasureEqualsRenderSize()
    {
        // Arrange
        var json = Faceted("\"row\": { \"field\": \"r\", \"type\": \"nominal\" }, \"column\": { \"field\": \"c\", \"type\": \"nominal\" }, \"color\": { \"field\": \"c\", \"type\": \"nominal\" }", axes: true);
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
        var size = document.Measure(cancellationToken: Ct);

        // Assert
        Assert.All(snapshots, s => Assert.Equal(expected, s));
        Assert.Equal((size.Width, size.Height), (reference.Width, reference.Height));
    }

    /// <summary>
    ///     Validates the edge cases of the data: no rows draw no cells (and no error), and a facet whose marks are not supported still draws its cells, empty, and reports the mark.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Facet_EmptyDataDrawsNoCellsAndUnsupportedMarkDrawsEmptyCells()
    {
        // Arrange
        var empty = """
            { "data": { "values": [] }, "facet": { "column": { "field": "c", "type": "nominal" } },
              "spec": { "width": 100, "height": 80, "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } } }
            """;
        var geo = FacetOver("{ \"column\": { \"field\": \"c\", \"type\": \"nominal\" } }", "{ \"mark\": \"geoshape\" }");

        // Act
        var none = SceneOf(empty);
        var (_, unsupported) = Report(geo);
        var geoCells = Cells(SceneOf(geo));

        // Assert: no cell; two empty cells (the default size of a view without a drawn mark) and the mark reported once
        Assert.Empty(Cells(none));
        Assert.Equal(2, geoCells.Count);
        Assert.Equal(["vl-mark-geoshape"], unsupported.Select(u => u[..u.IndexOf(':', StringComparison.Ordinal)]));
    }
}
