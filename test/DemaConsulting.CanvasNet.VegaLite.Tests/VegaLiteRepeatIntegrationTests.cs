using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of repeated views (Phase 11): <c>repeat</c> specifications (a list of fields, rows and columns, layers) are expanded by the specification
///     model into concatenations and layered views of copies of the <c>spec</c>, with <c>{"repeat": ...}</c> references replaced by the repeated field, and drawn as such. The
///     charts are rendered through the public API and checked by the positions of the cells and the titles of the axes, with every number derived by hand from the layout rules
///     (the next cell of a row starts <c>ceil(100.5) + 20 + ceil(0.5)</c> = 122 pixels after the first and the next row <c>ceil(80.5) + 20 + 1</c> = 102 pixels below it).
/// </summary>
public class VegaLiteRepeatIntegrationTests
{
    /// <summary>Five rows of four quantitative fields and a nominal one.</summary>
    private const string Data = """
        "data": { "values": [
          { "a": 1, "b": 5, "c": 3, "d": 9, "k": "x" }, { "a": 2, "b": 3, "c": 8, "d": 1, "k": "y" }, { "a": 3, "b": 9, "c": 2, "d": 4, "k": "x" },
          { "a": 4, "b": 4, "c": 6, "d": 7, "k": "y" }, { "a": 5, "b": 7, "c": 4, "d": 2, "k": "x" } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json) => VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the cells of a composition: the plot areas of its charts, in drawing order, as the position of the corner on the canvas.
    /// </summary>
    /// <param name="scene">The scene (its first view group is the root of the composition, which has no size).</param>
    /// <returns>The cells.</returns>
    private static List<(double X, double Y)> Cells(Scene scene) =>
        [.. scene.FindAll("view").Skip(1).Select(e => (e.OffsetX + ((SceneGroup)e.Item).X, e.OffsetY + ((SceneGroup)e.Item).Y))];

    /// <summary>
    ///     Gets the texts of the items of a role in a scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The texts in drawing order.</returns>
    private static List<string> Texts(Scene scene, string role) => [.. scene.FindAll(role).Select(e => ((SceneText)e.Item).Text)];

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
    ///     Validates that a repeat of a list of fields is a concatenation of one chart for each field, wrapped at <c>columns</c>, with no axes (so that the positions are exact).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_ArrayRepeatsFieldAsConcatWithColumns()
    {
        // Arrange: three fields in two columns
        var json = $$"""
            {
              {{Data}},
              "repeat": [ "a", "b", "c" ], "columns": 2,
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative", "axis": null }, "y": { "field": "d", "type": "quantitative", "axis": null } } }
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: the third chart wraps to the second row, 102 pixels below the first (plus the root's 6)
        Assert.Equal([(6.0, 6.0), (128.0, 6.0), (6.0, 108.0)], Cells(scene));
    }

    /// <summary>
    ///     Validates that a repeat of rows and columns draws the cross product in row-major order, each chart with the repeated fields on its axes.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_RowColumnMatrixIsRowMajor()
    {
        // Arrange: two rows (a, b) and two columns (c, d)
        var json = $$"""
            {
              {{Data}},
              "repeat": { "row": [ "a", "b" ], "column": [ "c", "d" ] },
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative" }, "y": { "field": { "repeat": "row" }, "type": "quantitative" } } }
            }
            """;

        // Act
        var scene = SceneOf(json);
        var cells = Cells(scene);

        // Assert: four cells in a 2 by 2 grid; the first row first, and the axis titles chart by chart (x then y) are the column and the row field of each
        Assert.Equal(4, cells.Count);
        Assert.Equal(cells[0].Y, cells[1].Y);
        Assert.Equal(cells[2].Y, cells[3].Y);
        Assert.True(cells[1].X > cells[0].X + 120);
        Assert.True(cells[2].Y > cells[0].Y + 100);
        Assert.Equal(["c", "a", "d", "a", "c", "b", "d", "b"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates that a repeat of rows only is one column of charts, and that a repeat of columns only is one row.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_RowOnlyIsOneColumnAndColumnOnlyIsOneRow()
    {
        // Arrange
        const string spec = """
            "spec": { "width": 100, "height": 80, "mark": "point",
                      "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative", "axis": null }, "y": { "field": "d", "type": "quantitative", "axis": null } } }
            """;
        var columnOnly = $$"""{ {{Data}}, "repeat": { "column": [ "a", "b", "c" ] }, {{spec}} }""";
        var rowOnly = """
            {
              "data": { "values": [ { "a": 1, "d": 2 }, { "a": 2, "d": 3 } ] },
              "repeat": { "row": [ "a", "d" ] },
              "spec": { "width": 100, "height": 80, "mark": "point", "encoding": { "x": { "field": { "repeat": "row" }, "type": "quantitative", "axis": null }, "y": { "field": "d", "type": "quantitative", "axis": null } } }
            }
            """;

        // Act
        var row = Cells(SceneOf(columnOnly));
        var column = Cells(SceneOf(rowOnly));

        // Assert: one row of three cells 122 pixels apart, one column of two cells 102 pixels apart
        Assert.Equal([(6.0, 6.0), (128.0, 6.0), (250.0, 6.0)], row);
        Assert.Equal([(6.0, 6.0), (6.0, 108.0)], column);
    }

    /// <summary>
    ///     Validates that a repeat of layers (alone, and with rows) takes the size of its <c>spec</c> (Vega-Lite sizes a layered view from its layers): the cells are 160 by 70
    ///     pixels, and the layered view of a repeat of layers alone is 160 by 70 plus the padding.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_LayerRepeatTakesTheSizeOfTheSpec()
    {
        // Arrange: no axes, so that the bounds are the plot areas
        const string spec = """
            "spec": { "width": 160, "height": 70, "mark": "line",
                      "encoding": { "x": { "field": "a", "type": "quantitative", "axis": null }, "y": { "field": { "repeat": "layer" }, "type": "quantitative", "axis": null } } }
            """;
        var rows = $$"""{ {{Data}}, "repeat": { "row": [ "a", "b" ], "layer": [ "c", "d" ] }, {{spec}} }""";
        var layers = $$"""{ {{Data}}, "repeat": { "layer": [ "c", "d" ] }, {{spec}} }""";

        // Act
        var rowScene = SceneOf(rows);
        var layersSize = VegaLiteDocument.Parse(layers).Measure(cancellationToken: Ct);

        // Assert: two cells of the size of the spec, 70 + 20 + 2 pixels apart (the half pixels of the borders); a layered view of 160 by 70 and the padding of 5
        Assert.Equal([(160.0, 70.0), (160.0, 70.0)], rowScene.FindAll("view").Skip(1).Select(e => (((SceneGroup)e.Item).Width, ((SceneGroup)e.Item).Height)));
        Assert.Equal([(6.0, 6.0), (6.0, 98.0)], Cells(rowScene));
        Assert.Equal(new VegaLiteSize(170, 80), layersSize);
    }

    /// <summary>
    ///     Validates that a repeat of layers is one layered chart: the layers are drawn over each other in one cell, with one pair of axes whose title joins the repeated fields.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_LayerRepeatDrawsLayers()
    {
        // Arrange
        var json = $$"""
            {
              {{Data}},
              "repeat": { "layer": [ "a", "b" ] },
              "spec": { "width": 100, "height": 80, "mark": "line",
                        "encoding": { "x": { "field": "c", "type": "quantitative" }, "y": { "field": { "repeat": "layer" }, "type": "quantitative" } } }
            }
            """;

        // Act
        var scene = SceneOf(json);
        var (warnings, unsupported) = Report(json);

        // Assert: nothing is reported, two marks groups (one for each layer), and the y axis is titled with both fields
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Equal(2, scene.FindAll("marks").Count());
        Assert.Equal(["c", "a, b"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates that a repeated field gives the title of the axis: one chart for each field of the list, each titled with its own field.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_RefDrivesAxisTitles()
    {
        // Arrange
        var json = $$"""
            {
              {{Data}},
              "repeat": [ "a", "d" ],
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } }
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert
        Assert.Equal(["a", "b", "d", "b"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates that the data of the repeated view is read once for all the copies: a limit of five rows is enough for four copies of a five-row dataset.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_DataIsLoadedOnceForAllCopies()
    {
        // Arrange: the data belongs to the repeated spec; four copies would need 20 rows if each copy read it
        var json = """
            {
              "repeat": [ "a", "b", "c", "d" ],
              "spec": {
                "data": { "values": [ { "a": 1, "b": 5, "c": 3, "d": 9 }, { "a": 2, "b": 3, "c": 8, "d": 1 }, { "a": 3, "b": 9, "c": 2, "d": 4 }, { "a": 4, "b": 4, "c": 6, "d": 7 }, { "a": 5, "b": 7, "c": 4, "d": 2 } ] },
                "width": 100, "height": 80, "mark": "point",
                "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative", "axis": null }, "y": { "field": "d", "type": "quantitative", "axis": null } }
              }
            }
            """;

        // Act
        var scene = SceneOf(json);
        var (warnings, _) = Report(json, new VegaLiteLimits { MaxDataRows = 5 });

        // Assert: four cells with five points each, and no limit error
        Assert.Equal(4, Cells(scene).Count);
        Assert.Equal([5, 5, 5, 5], scene.FindAll("marks").Select(e => ((SceneGroup)e.Item).Children.Count));
        Assert.Empty(warnings);
    }

    /// <summary>
    ///     Validates that the number of copies of a repeat is limited before any copy is made: a 40 by 40 repeat is 1,600 charts, more than the 1,000 views the limit allows.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_ProductLimit()
    {
        // Arrange
        var fields = string.Join(", ", Enumerable.Range(0, 40).Select(i => $"\"f{i}\""));
        var json = $$"""
            {
              "repeat": { "row": [ {{fields}} ], "column": [ {{fields}} ] },
              "spec": { "mark": "point", "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative" } } }
            }
            """;

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Report(json));

        // Assert
        Assert.Contains("MaxViews", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a repeat reference that the enclosing repeat does not define is a warning, delivered once, and that the channel is dropped (as Vega-Lite does).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_UnknownRefWarnsAndDropsChannel()
    {
        // Arrange: a repeat of values has no "column" value
        var json = $$"""
            {
              {{Data}},
              "repeat": [ "a", "b" ],
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative" }, "y": { "field": { "repeat": "column" }, "type": "quantitative" } } }
            }
            """;

        // Act
        var (warnings, unsupported) = Report(json);
        var scene = SceneOf(json);

        // Assert: one warning for the one place it is written (not one for each copy), and the charts have no y axis
        Assert.Single(warnings, w => w.StartsWith("vl-repeat-ref-unresolved:", StringComparison.Ordinal));
        Assert.Empty(unsupported);
        Assert.Equal(["a", "b"], Texts(scene, "axis-title"));
    }

    /// <summary>
    ///     Validates that a facet whose field is a repeat reference with no value is dropped and the <c>spec</c> alone is drawn, with the warning delivered once, and no error.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_FacetWithUnknownRefIsDropped()
    {
        // Arrange: a repeat of values has no "row" value, so the facet has no field
        var json = $$"""
            {
              {{Data}},
              "repeat": [ "a", "b" ],
              "spec": { "facet": { "field": { "repeat": "row" }, "type": "nominal" },
                        "spec": { "width": 100, "height": 80, "mark": "point",
                                  "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative", "axis": null }, "y": { "field": "d", "type": "quantitative", "axis": null } } } }
            }
            """;

        // Act
        var (warnings, unsupported) = Report(json);
        var cells = Cells(SceneOf(json));

        // Assert: one warning, and two charts side by side, as for a repeat without a facet
        Assert.Single(warnings, w => w.StartsWith("vl-repeat-ref-unresolved:", StringComparison.Ordinal));
        Assert.Empty(unsupported);
        Assert.Equal([(6.0, 6.0), (128.0, 6.0)], cells);

        // Arrange: the same facet at the root, where no repeat gives a value; it is the chart of its spec, of the size of the spec
        var root = $$"""
            {
              {{Data}},
              "facet": { "field": { "repeat": "row" }, "type": "nominal" },
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": "a", "type": "quantitative", "axis": null }, "y": { "field": "d", "type": "quantitative", "axis": null } } }
            }
            """;

        // Act
        var (rootWarnings, _) = Report(root);
        var scene = SceneOf(root);

        // Assert
        Assert.Single(rootWarnings, w => w.StartsWith("vl-repeat-ref-unresolved:", StringComparison.Ordinal));
        var plot = (SceneGroup)scene.FindAll("view").First().Item;
        Assert.Equal((100.0, 80.0), (plot.Width, plot.Height));
    }

    /// <summary>
    ///     Validates that <c>columns</c> next to a repeat of rows and columns is ignored with a warning, delivered once.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_ColumnsWithRowColumnWarns()
    {
        // Arrange
        var json = $$"""
            {
              {{Data}},
              "repeat": { "row": [ "a", "b" ], "column": [ "c", "d" ] }, "columns": 3,
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative", "axis": null }, "y": { "field": { "repeat": "row" }, "type": "quantitative", "axis": null } } }
            }
            """;

        // Act
        var (warnings, _) = Report(json);
        var cells = Cells(SceneOf(json));

        // Assert: the warning is delivered once and the grid is two columns wide, not three
        Assert.Single(warnings, w => w.StartsWith("vl-composition-columns-ignored:", StringComparison.Ordinal));
        Assert.Equal([(6.0, 6.0), (128.0, 6.0), (6.0, 108.0), (128.0, 108.0)], cells);
    }

    /// <summary>
    ///     Validates that a repeat shares the color scale of its copies and draws its legend once beside the grid.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Repeat_ColorLegendIsDrawnOnce()
    {
        // Arrange
        var json = $$"""
            {
              {{Data}},
              "repeat": { "row": [ "a" ], "column": [ "c", "d" ] },
              "spec": { "width": 100, "height": 80, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative", "axis": null }, "y": { "field": { "repeat": "row" }, "type": "quantitative", "axis": null },
                                      "color": { "field": "k", "type": "nominal" } } }
            }
            """;

        // Act
        var scene = SceneOf(json);

        // Assert: one legend with the two values of k; its title is the field
        Assert.Single(scene.FindAll("legend"));
        Assert.Equal(["x", "y"], Texts(scene, "legend-label"));
        Assert.Equal(["k"], Texts(scene, "legend-title"));
    }
}
