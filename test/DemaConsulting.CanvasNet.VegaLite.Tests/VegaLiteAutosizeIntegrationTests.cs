using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of <c>autosize</c> <c>fit</c>, <c>fit-x</c> and <c>fit-y</c> and of <c>"container"</c> sizes (Phase 11): a fitted single or layered view is drawn so
///     that the whole view, the axes, legends, title and padding included, has the size that the specification (or the host's container) gives, a fit that a step size cannot honor is dropped
///     with a warning, and a composition ignores both with a warning. The charts are rendered through the public API; the numbers are derived by hand from the layout rules (a padding of
///     5 pixels, a 13 pixel title with 4 pixels of offset, guides rounded up to whole pixels).
/// </summary>
public class VegaLiteAutosizeIntegrationTests
{
    /// <summary>Three categories with a value <c>v</c> of 4, 8 and 6.</summary>
    private const string Data = """
        "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 } ] }
        """;

    /// <summary>A bar chart with both axes; the size and the autosize come in front of it.</summary>
    private const string Bars = """
        "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }
        """;

    /// <summary>A bar chart without axes, so that its bounds are its data rectangle.</summary>
    private const string BareBars = """
        "mark": "bar", "encoding": { "x": { "field": "a", "type": "nominal", "axis": null }, "y": { "field": "v", "type": "quantitative", "axis": null } }
        """;

    /// <summary>Twelve rows with a color of twelve values: a legend that is taller (or wider) than a small plot.</summary>
    private const string ManyColors = """
        "data": { "values": [ { "x": 1, "y": 1, "c": "c01" }, { "x": 2, "y": 2, "c": "c02" }, { "x": 3, "y": 3, "c": "c03" }, { "x": 4, "y": 4, "c": "c04" },
          { "x": 5, "y": 5, "c": "c05" }, { "x": 6, "y": 6, "c": "c06" }, { "x": 7, "y": 7, "c": "c07" }, { "x": 8, "y": 8, "c": "c08" },
          { "x": 9, "y": 9, "c": "c09" }, { "x": 10, "y": 10, "c": "c10" }, { "x": 11, "y": 11, "c": "c11" }, { "x": 12, "y": 12, "c": "c12" } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lays a specification out without painting.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(string json, VegaLiteRenderOptions? options = null) => VegaLiteDocument.Parse(json).BuildScene(options ?? new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Gets the plot area of a view: the position of its corner on the canvas and its size.
    /// </summary>
    /// <param name="scene">The scene of a single or layered view.</param>
    /// <returns>The data rectangle.</returns>
    private static (double X, double Y, double Width, double Height) Plot(Scene scene)
    {
        var view = scene.FindAll("view").First();
        var group = (SceneGroup)view.Item;
        return (view.OffsetX + group.X, view.OffsetY + group.Y, group.Width, group.Height);
    }

    /// <summary>
    ///     Renders a specification and collects what it reports.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="containerWidth">The width of the container, or null for the default.</param>
    /// <param name="containerHeight">The height of the container, or null for the default.</param>
    /// <param name="strict">Whether an unsupported feature throws.</param>
    /// <returns>The warnings and the unsupported features, each as <c>identifier: message</c>.</returns>
    private static (List<string> Warnings, List<string> Unsupported) Report(string json, int? containerWidth = null, int? containerHeight = null, bool strict = false)
    {
        var warnings = new List<string>();
        var unsupported = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w => warnings.Add(w.Message),
            UnsupportedFeatureHandler = u => unsupported.Add($"{u.Feature}: {u.Message}"),
            ThrowOnUnsupportedFeature = strict,
            ContainerWidth = containerWidth ?? 400,
            ContainerHeight = containerHeight ?? 300,
        };
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        return (warnings, unsupported);
    }

    /// <summary>
    ///     Validates that a fitted view has exactly the size that is given: without guides the data rectangle is that size less the padding, and with axes, a title and a legend the whole
    ///     canvas is still the size.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_FitSurfaceHasTheGivenSize()
    {
        // Arrange: no guides, a view of 300 by 200 that contains its padding of 5
        var bare = $$"""{ {{Data}}, "width": 300, "height": 200, "autosize": { "type": "fit", "contains": "padding" }, {{BareBars}} }""";

        // Act
        var scene = SceneOf(bare);
        var plot = Plot(scene);

        // Assert: the canvas is the given size and the data rectangle is the size less 2 * 5 on each axis, at (5, 5)
        Assert.Equal((300.0, 200.0), (scene.Width, scene.Height));
        Assert.Equal((5.0, 5.0, 290.0, 190.0), plot);
        Assert.Equal(new VegaLiteSize(300, 200), VegaLiteDocument.Parse(bare).Measure(cancellationToken: Ct));

        // Arrange: the same view with its guides: two axes, a title and a color legend; contains content, so the padding is added to the canvas
        var guided = $$"""
            {
              "title": "Fitted",
              "data": { "values": [ { "a": "A", "v": 4, "k": "m" }, { "a": "B", "v": 8, "k": "n" }, { "a": "C", "v": 6, "k": "m" } ] },
              "width": 400, "height": 250, "autosize": "fit",
              "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } }
            }
            """;

        // Act
        var scene2 = SceneOf(guided);
        var plot2 = Plot(scene2);

        // Assert: the canvas is 400 + 10 by 250 + 10 whatever the guides need; the data rectangle is what is left of it
        Assert.Equal((410.0, 260.0), (scene2.Width, scene2.Height));
        Assert.Equal(new VegaLiteSize(410, 260), VegaLiteDocument.Parse(guided).Measure(cancellationToken: Ct));

        // Assert: the left axis and the title push the rectangle in by whole pixels; the right side holds the legend and the bottom the axis: whole pixels, and they sum to the size
        Assert.True(plot2.X > 5 && plot2.Y > 5);
        Assert.Equal(Math.Floor(plot2.X), plot2.X);
        Assert.Equal(Math.Floor(plot2.Y), plot2.Y);
        Assert.True(plot2.Y >= 22);
        var right = 405 - (plot2.X + plot2.Width);
        var bottom = 255 - (plot2.Y + plot2.Height);
        Assert.Equal(Math.Round(right), right, 9);
        Assert.Equal(Math.Round(bottom), bottom, 9);
        Assert.True(right > 40 && bottom > 10);

        // Assert: the same view padded needs more canvas than the size to hold the guides
        var padded = SceneOf(guided.Replace("\"autosize\": \"fit\",", string.Empty, StringComparison.Ordinal));
        Assert.True(padded.Width > 410 && padded.Height > 260);
        Assert.Equal((400.0, 250.0), (Plot(padded).Width, Plot(padded).Height));
    }

    /// <summary>
    ///     Validates that <c>fit-x</c> of a layered view fits the width and lets the height grow around the data rectangle.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_FitXLayerView()
    {
        // Arrange: bars and a rule layer over the same axes
        var json = $$"""
            {
              {{Data}},
              "width": 360, "height": 150, "autosize": "fit-x",
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } },
              "layer": [ { "mark": "bar" }, { "mark": "rule", "encoding": { "y": { "datum": 5 } } } ]
            }
            """;

        // Act
        var scene = SceneOf(json);
        var plot = Plot(scene);

        // Assert: the width of the view is 360 plus the padding, so the canvas is 370 wide; the data rectangle is narrower by the y axis (whole pixels)
        Assert.Equal(370.0, scene.Width);
        Assert.True(plot.Width < 350);
        Assert.Equal(Math.Floor(plot.X), plot.X);
        Assert.Equal(365.0, plot.X + plot.Width);

        // Assert: the height is not fitted: the data rectangle keeps its 150 and the canvas grows by the x axis
        Assert.Equal(150.0, plot.Height);
        Assert.True(scene.Height > 150 + 10 + 20);
        Assert.Equal(2, scene.FindAll("axis").Count());
        Assert.Equal(3, scene.FindAll("mark-bar").Count());
        Assert.Equal(3, scene.FindAll("mark-rule").Count());

        // Assert: the same layered view without autosize has the same data height and a wider data rectangle
        var padded = SceneOf(json.Replace("\"autosize\": \"fit-x\",", string.Empty, StringComparison.Ordinal));
        Assert.Equal((360.0, plot.Height), (Plot(padded).Width, Plot(padded).Height));
        Assert.Equal(scene.Height, padded.Height);
    }

    /// <summary>
    ///     Validates that a fitted view counts a legend only across its side: a tall legend beside a small plot does not grow the canvas, and a wide legend above it does not widen it.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_FitCountsLegendsAcrossTheirSideOnly()
    {
        // Arrange: twelve legend entries beside a plot of 60 pixels with no axes
        var right = $$"""
            {
              {{ManyColors}},
              "width": 200, "height": 60, "autosize": "fit",
              "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative", "axis": null }, "y": { "field": "y", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "nominal" } }
            }
            """;

        // Act
        var fitted = SceneOf(right);
        var padded = SceneOf(right.Replace("\"autosize\": \"fit\",", string.Empty, StringComparison.Ordinal));

        // Assert: the legend is far taller than the plot; fit leaves the canvas at 60 + 10 high, pad grows it to hold the legend; the legend takes its width from the 200 either way
        Assert.Equal(70.0, fitted.Height);
        Assert.True(padded.Height > 100);
        Assert.Equal(60.0, Plot(fitted).Height);
        Assert.Equal(210.0, fitted.Width);
        Assert.True(Plot(fitted).Width < 200);

        // Arrange: the legend above a narrow plot, wider than the plot
        var top = $$"""
            {
              {{ManyColors}},
              "width": 60, "height": 120, "autosize": "fit-x",
              "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative", "axis": null }, "y": { "field": "y", "type": "quantitative", "axis": null }, "color": { "field": "c", "type": "nominal", "legend": { "orient": "top", "columns": 12 } } }
            }
            """;

        // Act
        var fittedTop = SceneOf(top);
        var paddedTop = SceneOf(top.Replace("\"autosize\": \"fit-x\",", string.Empty, StringComparison.Ordinal));

        // Assert: fit-x keeps the canvas 60 + 10 wide however far the legend reaches sideways; pad grows to hold it
        Assert.Equal(70.0, fittedTop.Width);
        Assert.True(paddedTop.Width > 100);
        Assert.Equal(60.0, Plot(fittedTop).Width);
    }

    /// <summary>
    ///     Validates that a <c>"container"</c> size takes the container size of the options: the whole canvas is that size, guides included.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_ContainerUsesOptionsContainerSize()
    {
        // Arrange
        var json = $$"""{ "title": "T", {{Data}}, "width": "container", "height": "container", {{Bars}} }""";

        // Act
        var small = SceneOf(json, new VegaLiteRenderOptions { ContainerWidth = 500, ContainerHeight = 300 });
        var large = SceneOf(json, new VegaLiteRenderOptions { ContainerWidth = 800, ContainerHeight = 450 });
        var measured = VegaLiteDocument.Parse(json).Measure(new VegaLiteRenderOptions { ContainerWidth = 500, ContainerHeight = 300 }, Ct);

        // Assert: exactly the container, a padding of 5 included; the data rectangle takes what the axes and the 17 pixel title leave
        Assert.Equal((500.0, 300.0), (small.Width, small.Height));
        Assert.Equal((800.0, 450.0), (large.Width, large.Height));
        Assert.Equal(new VegaLiteSize(500, 300), measured);
        Assert.True(Plot(small).Y >= 22);
        Assert.Equal(495.0, Plot(small).X + Plot(small).Width);
        Assert.Equal(300.0 - 5.0 - Plot(small).Y - Plot(small).Height, Math.Round(300.0 - 5.0 - Plot(small).Y - Plot(small).Height));
        Assert.True(Plot(large).Width > Plot(small).Width + 250);

        // Arrange: only the width is a container; the height is the default 300 and the canvas grows around it
        var widthOnly = $$"""{ {{Data}}, "width": "container", {{Bars}} }""";

        // Act
        var wide = SceneOf(widthOnly, new VegaLiteRenderOptions { ContainerWidth = 600, ContainerHeight = 100 });

        // Assert: the canvas width is the container; the height ignores the container height
        Assert.Equal(600.0, wide.Width);
        Assert.Equal(300.0, Plot(wide).Height);
        Assert.True(wide.Height > 310);

        // Act: an autosize of pad in the specification takes the container as the data rectangle (Vega-Lite's width signal), so the guides add to it
        var pad = SceneOf($$"""{ {{Data}}, "width": "container", "autosize": "pad", {{Bars}} }""", new VegaLiteRenderOptions { ContainerWidth = 600 });

        // Assert
        Assert.Equal(600.0, Plot(pad).Width);
        Assert.True(pad.Width > 600);
    }

    /// <summary>
    ///     Validates that a fit that a step size cannot honor is dropped with a warning, explicit or implied by a discrete scale, and that the dropped dimension keeps the size of the step.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_FitWithStepWarns()
    {
        // Arrange: an explicit step for the width and a fitted height of 150
        var json = $$"""{ {{Data}}, "width": { "step": 30 }, "height": 150, "autosize": "fit", {{Bars}} }""";
        var unfitted = $$"""{ {{Data}}, "width": { "step": 30 }, "height": 150, {{Bars}} }""";

        // Act
        var (warnings, unsupported) = Report(json);
        var scene = SceneOf(json);

        // Assert: one warning, at the autosize; fit-y remains: the canvas is 150 + 10 high, and the width is the step width, as without the autosize
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-layout-fit-step:", warning, StringComparison.Ordinal);
        Assert.Contains("width", warning, StringComparison.Ordinal);
        Assert.Contains("fit-y", warning, StringComparison.Ordinal);
        Assert.Empty(unsupported);
        Assert.Equal(160.0, scene.Height);
        Assert.Equal(Plot(SceneOf(unfitted)).Width, Plot(scene).Width);
        Assert.Equal(scene.Width, SceneOf(unfitted).Width);

        // Act: no width at all on a discrete scale is a step too
        var implicitStep = $$"""{ {{Data}}, "height": 150, "autosize": "fit", {{Bars}} }""";
        var (implicitWarnings, _) = Report(implicitStep);

        // Assert
        Assert.StartsWith("vl-layout-fit-step:", Assert.Single(implicitWarnings), StringComparison.Ordinal);

        // Act: steps in both dimensions leave a padded view, laid out as without the autosize
        var both = """
            { "data": { "values": [ { "a": "A", "b": "X" }, { "a": "B", "b": "Y" } ] }, "width": { "step": 30 }, "height": { "step": 20 }, "autosize": "fit",
              "mark": "point", "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "nominal" } } }
            """;
        var (bothWarnings, _) = Report(both);
        var bothScene = SceneOf(both);
        var bothPadded = SceneOf(both.Replace("\"autosize\": \"fit\",", string.Empty, StringComparison.Ordinal));

        // Assert
        var bothWarning = Assert.Single(bothWarnings);
        Assert.Contains("width and height", bothWarning, StringComparison.Ordinal);
        Assert.Contains("'pad'", bothWarning, StringComparison.Ordinal);
        Assert.Equal((bothPadded.Width, bothPadded.Height), (bothScene.Width, bothScene.Height));

        // Assert: a fit that nothing drops is silent
        var (silent, _) = Report($$"""{ {{Data}}, "width": 200, "height": 150, "autosize": "fit", {{Bars}} }""");
        Assert.Empty(silent);
    }

    /// <summary>
    ///     Validates that the fit modes are not reported as unsupported any more (the family <c>vl-layout-autosize-&lt;type&gt;</c> is retired), not even in strict mode.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_FitIsNotReportedAnymore()
    {
        // Arrange
        foreach (var autosize in new[] { "\"fit\"", "\"fit-x\"", "\"fit-y\"", "{ \"type\": \"fit\", \"contains\": \"padding\" }", "\"pad\"", "\"none\"" })
        {
            var json = $$"""{ {{Data}}, "width": 200, "height": 150, "autosize": {{autosize}}, {{Bars}} }""";

            // Act: strict mode throws at the first unsupported feature
            var (warnings, unsupported) = Report(json, strict: true);

            // Assert
            Assert.Empty(unsupported);
            Assert.Empty(warnings);
        }
    }

    /// <summary>
    ///     Validates that measuring and rendering agree on the size of a fitted view and of a container view.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_MeasureFitEqualsRender()
    {
        // Arrange
        var options = new VegaLiteRenderOptions { ContainerWidth = 640, ContainerHeight = 360 };
        string[] specs =
        [
            $$"""{ "title": "T", {{Data}}, "width": 400, "height": 250, "autosize": "fit", {{Bars}} }""",
            $$"""{ {{Data}}, "width": 400, "height": 250, "autosize": { "type": "fit-y", "contains": "padding" }, {{Bars}} }""",
            $$"""{ "title": "T", {{Data}}, "width": "container", "height": "container", {{Bars}} }""",
            $$"""{ {{Data}}, "width": "container", {{Bars}} }""",
        ];

        foreach (var json in specs)
        {
            var document = VegaLiteDocument.Parse(json);

            // Act
            var size = document.Measure(options, Ct);
            using var surface = document.Render(options, Ct);

            // Assert
            Assert.Equal((size.Width, size.Height), (surface.Width, surface.Height));
        }
    }

    /// <summary>
    ///     Validates that a composition ignores <c>fit</c>, <c>fit-x</c> and <c>"container"</c> sizes with a warning and is drawn padded, as Vega-Lite does, and that the view inside keeps
    ///     its default size for a <c>"container"</c>.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Autosize_CompositionIgnoresFitAndContainerWithWarning()
    {
        // Arrange
        var bare = $$"""{ "width": 100, "height": 80, {{BareBars}} }""";
        var plain = $$"""{ {{Data}}, "hconcat": [ {{bare}}, {{bare}} ] }""";
        var options = new VegaLiteRenderOptions { ContainerWidth = 800, ContainerHeight = 600 };
        var expected = VegaLiteDocument.Parse(plain).Measure(options, Ct);

        // Act / Assert: fit-x with contains, a container width and height, all on the composition: one warning each, the size of the plain composition
        foreach (var (autosize, size) in new[] { ("\"autosize\": { \"type\": \"fit-x\", \"contains\": \"padding\" },", string.Empty), (string.Empty, "\"width\": \"container\", \"height\": \"container\",") })
        {
            var json = $$"""{ {{Data}}, {{autosize}} {{size}} "hconcat": [ {{bare}}, {{bare}} ] }""";
            var (warnings, unsupported) = Report(json, 800, 600);

            Assert.All(warnings, w => Assert.StartsWith("vl-composition-property-ignored:", w, StringComparison.Ordinal));
            Assert.NotEmpty(warnings);
            Assert.Empty(unsupported);
            Assert.Equal(expected, VegaLiteDocument.Parse(json).Measure(options, Ct));
        }

        // Arrange: a child with a container width is a child of default size (here 300, the default width), as when the width is written out
        var containerChild = $$"""{ {{Data}}, "hconcat": [ { "width": "container", "height": 80, {{BareBars}} } ] }""";
        var numberChild = $$"""{ {{Data}}, "hconcat": [ { "width": 300, "height": 80, {{BareBars}} } ] }""";

        // Act
        var (childWarnings, _) = Report(containerChild, 800, 600);

        // Assert
        Assert.Single(childWarnings, w => w.StartsWith("vl-composition-property-ignored:", StringComparison.Ordinal));
        Assert.Equal(VegaLiteDocument.Parse(numberChild).Measure(options, Ct), VegaLiteDocument.Parse(containerChild).Measure(options, Ct));
    }
}
