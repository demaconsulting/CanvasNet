using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     System-level integration tests of the compositions as a whole (Phase 11): what a concatenation, a facet, and a repeat have in common once they are drawn - the same specification
///     renders to the same bytes on every thread and <c>Measure</c> gives the size of the surface (also for a repeat), the limits on the number of views and the depth of the nesting
///     fail closed for every kind of composition, strict mode has nothing to throw for on a composition that nests the others and throws at the first of the features that are still not
///     drawn, and a canceled token stops the work of every kind. The tests that belong to one kind of composition are in the concatenation, facet, and repeat test classes.
/// </summary>
public class VegaLiteCompositionSystemTests
{
    /// <summary>The rows of the charts: a stall, a month, and two measures.</summary>
    private const string Data = """
        "data": { "values": [
          { "stall": "Apples", "month": 1, "crates": 28, "price": 3 }, { "stall": "Apples", "month": 2, "crates": 35, "price": 4 },
          { "stall": "Pears", "month": 1, "crates": 55, "price": 5 }, { "stall": "Pears", "month": 2, "crates": 48, "price": 6 }
        ] }
        """;

    /// <summary>A bar chart of the crates of every stall, with its axes and a legend of the stalls.</summary>
    private const string Bars = """{ "width": 80, "height": 60, "mark": "bar", "encoding": { "x": { "field": "stall", "type": "nominal" }, "y": { "field": "crates", "type": "quantitative" }, "color": { "field": "stall", "type": "nominal" } } }""";

    /// <summary>A repeat of two measures over two months, with a shared legend.</summary>
    private const string Matrix = """
        { %DATA%, "repeat": { "row": ["crates", "price"], "column": ["crates", "price"] },
          "spec": { "width": 60, "height": 60, "mark": "point", "encoding": {
            "x": { "field": { "repeat": "column" }, "type": "quantitative" },
            "y": { "field": { "repeat": "row" }, "type": "quantitative" },
            "color": { "field": "stall", "type": "nominal" } } } }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Makes a specification text from a template that names the shared data and chart.
    /// </summary>
    /// <param name="template">The template with <c>%DATA%</c> and <c>%BARS%</c>.</param>
    /// <returns>The specification text.</returns>
    private static string Make(string template) => template.Replace("%DATA%", Data, StringComparison.Ordinal).Replace("%BARS%", Bars, StringComparison.Ordinal);

    /// <summary>
    ///     Makes a concatenation of a chart, a facet, and a repeat, which is every kind of composition in one specification.
    /// </summary>
    /// <returns>The specification text.</returns>
    private static string Everything() => "{ " + Data + ", \"vconcat\": [ "
        + "{ \"hconcat\": [ " + Bars + ", " + Bars + " ] }, "
        + "{ \"facet\": { \"column\": { \"field\": \"month\", \"type\": \"ordinal\", \"sort\": \"descending\", \"header\": { \"title\": \"Month\", \"labelAngle\": 0 } } }, \"spec\": " + Bars + " }, "
        + "{ \"repeat\": { \"column\": [\"crates\", \"price\"] }, \"spec\": { \"width\": 60, \"height\": 60, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": { \"repeat\": \"column\" }, \"type\": \"quantitative\" }, \"y\": { \"field\": \"stall\", \"type\": \"nominal\" } } } } "
        + "] }";

    /// <summary>
    ///     Renders a specification and collects what it reports.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The warnings and the unsupported features.</returns>
    private static (List<VegaLiteDiagnostic> Warnings, List<VegaLiteUnsupportedFeature> Unsupported) Report(string json, VegaLiteLimits? limits = null)
    {
        var warnings = new List<VegaLiteDiagnostic>();
        var unsupported = new List<VegaLiteUnsupportedFeature>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = warnings.Add,
            UnsupportedFeatureHandler = unsupported.Add,
            Limits = limits ?? VegaLiteLimits.Default,
        };
        using var surface = VegaLiteDocument.Parse(json).Render(options, Ct);
        return (warnings, unsupported);
    }

    /// <summary>
    ///     Validates that rendering a repeat is deterministic, also when the same document is rendered from several threads at once, and that <c>Measure</c> reports the size of the
    ///     surface that <c>Render</c> returns.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Composition_RepeatDeterministicConcurrentAndMeasureEqualsRenderSize()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Make(Matrix));
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
    ///     Validates that a specification that holds every kind of composition at once (a vertical concatenation of a horizontal one, a facet with a header, and a repeat) renders twice to
    ///     the same bytes and that the size of <c>Measure</c> is the size of the picture.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Composition_NestedCompositionsAreDeterministicAndMeasureEqualsRender()
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Everything());

        // Act
        using var first = document.Render(cancellationToken: Ct);
        using var second = document.Render(cancellationToken: Ct);
        var size = document.Measure(cancellationToken: Ct);

        // Assert
        Assert.Equal(VegaLiteTestHelpers.Snapshot(first), VegaLiteTestHelpers.Snapshot(second));
        Assert.Equal((size.Width, size.Height), (first.Width, first.Height));
    }

    /// <summary>
    ///     Validates that strict mode has nothing to throw for on a full specification: a concatenation that nests a concatenation, a facet with a header, and a repeat is drawn with its
    ///     transform, its legends, and its headers and delivers no unsupported feature and no warning.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Composition_StrictModeOnAFullSpec_NoUnsupportedFeature()
    {
        // Arrange: the transform of the composition is inherited by every chart, which also checks that a transform is not reported for being in a composition
        var json = Everything().Replace("\"vconcat\":", "\"transform\": [ { \"filter\": \"datum.crates > 20\" } ], \"vconcat\":", StringComparison.Ordinal);
        var unsupported = new List<VegaLiteUnsupportedFeature>();
        var warnings = new List<VegaLiteDiagnostic>();

        // Act
        using var surface = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = unsupported.Add, WarningHandler = warnings.Add }, Ct);

        // Assert
        Assert.Empty(unsupported);
        Assert.Empty(warnings);
        Assert.True(surface.Width > 0 && surface.Height > 0);
    }

    /// <summary>
    ///     Validates that strict mode throws, with the identifier and the pointer of the construct, for the features of a composition that are still not drawn: a transform that is not supported
    ///     in a child, a mark that is not supported in a child, and the parts of a facet that are reported (a binned field, a sort by an aggregate, and a side that a header does not draw on).
    /// </summary>
    /// <param name="child">The text of the composition, with <c>%DATA%</c> and <c>%BARS%</c>.</param>
    /// <param name="feature">The identifier of the first unsupported feature.</param>
    /// <param name="pointer">The pointer of the construct.</param>
    [Theory]
    [InlineData("{ %DATA%, \"hconcat\": [ %BARS%, { \"transform\": [ { \"regression\": \"crates\", \"on\": \"price\" } ], \"mark\": \"point\" } ] }", "vl-transform-regression", "/hconcat/1/transform/0")]
    [InlineData("{ %DATA%, \"vconcat\": [ %BARS%, { \"mark\": \"geoshape\" } ] }", "vl-mark-geoshape", "/vconcat/1/mark")]
    [InlineData("{ %DATA%, \"facet\": { \"field\": \"crates\", \"type\": \"quantitative\", \"bin\": true }, \"spec\": %BARS% }", "vl-facet-field-transform", "/facet/bin")]
    [InlineData("{ %DATA%, \"facet\": { \"row\": { \"field\": \"month\", \"type\": \"ordinal\", \"sort\": { \"op\": \"count\" } } }, \"spec\": %BARS% }", "vl-facet-sort", "/facet/row/sort")]
    [InlineData("{ %DATA%, \"facet\": { \"row\": { \"field\": \"month\", \"type\": \"ordinal\", \"header\": { \"labelOrient\": \"top\" } } }, \"spec\": %BARS% }", "vl-header-orient", "/facet/row/header/labelOrient")]
    public void CanvasNetVegaLite_SystemIntegration_Composition_StrictModeThrowsAtTheFirstRemainingUnsupportedFeature(string child, string feature, string pointer)
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Make(child));

        // Act
        var error = Assert.Throws<VegaLiteUnsupportedFeatureException>(
            () => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));

        // Assert
        Assert.Equal(feature, error.Feature);
        Assert.Equal(pointer, error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a canceled token stops the work of a concatenation, a facet, and a repeat, with an unwrapped <see cref="OperationCanceledException"/>, whether the work is measuring or
    ///     rendering.
    /// </summary>
    /// <param name="template">The text of the composition.</param>
    [Theory]
    [InlineData("{ %DATA%, \"hconcat\": [ %BARS%, %BARS% ] }")]
    [InlineData("{ %DATA%, \"facet\": { \"column\": { \"field\": \"month\", \"type\": \"ordinal\" } }, \"spec\": %BARS% }")]
    [InlineData("{ %DATA%, \"repeat\": [\"crates\", \"price\"], \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": { \"repeat\": \"repeat\" }, \"type\": \"quantitative\" } } } }")]
    public void CanvasNetVegaLite_SystemIntegration_Composition_CancellationIsHonored(string template)
    {
        // Arrange
        var document = VegaLiteDocument.Parse(Make(template));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.ThrowsAny<OperationCanceledException>(() => document.Measure(cancellationToken: source.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => document.Render(cancellationToken: source.Token));
    }

    /// <summary>
    ///     Validates the limit on the number of views for the three kinds of composition: the children of a concatenation, the cells of a facet, and the copies of a repeat all count, the limit
    ///     is accepted exactly, and the error names the limit.
    /// </summary>
    /// <param name="template">The text of the composition.</param>
    /// <param name="views">The number of views, the composition itself included.</param>
    [Theory]
    [InlineData("{ %DATA%, \"hconcat\": [ %BARS%, %BARS%, %BARS% ] }", 4)]
    [InlineData("{ %DATA%, \"facet\": { \"column\": { \"field\": \"month\", \"type\": \"ordinal\" } }, \"spec\": %BARS% }", 3)]
    [InlineData("{ %DATA%, \"repeat\": [\"crates\", \"price\", \"month\"], \"spec\": { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": { \"repeat\": \"repeat\" }, \"type\": \"quantitative\" } } } }", 4)]
    public void CanvasNetVegaLite_SystemIntegration_Composition_ViewLimitIsEnforcedForEveryKind(string template, int views)
    {
        // Arrange
        var json = Make(template);

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxViews = views - 1 }));

        // Assert: one view fewer fails, the exact number is accepted
        Assert.Contains("MaxViews", error.Message, StringComparison.Ordinal);
        Assert.Empty(Report(json, new VegaLiteLimits { MaxViews = views }).Unsupported);
    }

    /// <summary>
    ///     Validates the limit on the depth of the nesting for a facet and a repeat: a facet of a layered chart and a repeat of a layered chart are two levels, and the error names the limit.
    /// </summary>
    /// <param name="template">The text of the composition.</param>
    [Theory]
    [InlineData("{ %DATA%, \"facet\": { \"row\": { \"field\": \"month\", \"type\": \"ordinal\" } }, \"spec\": { \"layer\": [ %BARS% ] } }")]
    [InlineData("{ %DATA%, \"repeat\": { \"row\": [\"crates\", \"price\"] }, \"spec\": { \"layer\": [ { \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": { \"repeat\": \"row\" }, \"type\": \"quantitative\" } } } ] } }")]
    public void CanvasNetVegaLite_SystemIntegration_Composition_DepthLimitIsEnforcedForFacetAndRepeat(string template)
    {
        // Arrange
        var json = Make(template);

        // Act
        var error = Assert.Throws<VegaLiteRenderException>(() => Report(json, new VegaLiteLimits { MaxCompositionDepth = 1 }));

        // Assert: two levels are accepted
        Assert.Contains("MaxCompositionDepth", error.Message, StringComparison.Ordinal);
        Assert.Empty(Report(json, new VegaLiteLimits { MaxCompositionDepth = 2 }).Unsupported);
    }

    /// <summary>
    ///     Validates that the host's culture does not change a composition: a concatenation, a facet, and a repeat, a fitted chart with fractional values, and a chart with a container size
    ///     give the same scene and the same pixels under the current culture of languages with another decimal separator, digit grouping, and the Turkish dotted and dotless <c>i</c>
    ///     as under the invariant culture.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Composition_CultureDoesNotChangeTheScenesOrThePixels()
    {
        // Arrange
        const string Fractions = """
            "data": { "values": [ { "k": "a", "v": 1234.5 }, { "k": "b", "v": 7.25 }, { "k": "c", "v": 0.375 } ] }
            """;
        const string Chart = """
            "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }
            """;
        var charts = new (string Name, string Json, VegaLiteRenderOptions Options)[]
        {
            ("everything", Everything(), new VegaLiteRenderOptions()),
            ("fit", "{ " + Fractions + ", \"width\": 200, \"height\": 120, \"autosize\": { \"type\": \"fit\" }, \"title\": \"Fit\", " + Chart + " }", new VegaLiteRenderOptions()),
            ("container", "{ " + Fractions + ", \"width\": \"container\", \"height\": \"container\", " + Chart + " }", new VegaLiteRenderOptions { ContainerWidth = 320, ContainerHeight = 180 }),
        };
        var baseline = new Dictionary<string, (string Scene, byte[] Pixels)>();
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            foreach (var (name, json, options) in charts)
            {
                baseline[name] = Capture(json, options);
            }

            foreach (var cultureName in new[] { "de-DE", "tr-TR", "fr-FR", "ar-SA" })
            {
                CultureInfo culture;
                try
                {
                    culture = new CultureInfo(cultureName);
                }
                catch (CultureNotFoundException)
                {
                    // A host without the culture data (the invariant globalization mode) cannot change the result, so it has nothing to check
                    continue;
                }

                // Act
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                foreach (var (name, json, options) in charts)
                {
                    var (scene, pixels) = Capture(json, options);

                    // Assert
                    Assert.Equal(baseline[name].Scene, scene);
                    Assert.True(baseline[name].Pixels.AsSpan().SequenceEqual(pixels), name + " in " + cultureName);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    /// <summary>
    ///     Pins that the top-level transforms of a layer, a concatenation, a facet and a repeat are applied to every view (item 8 of the polish backlog: the diagnostic that the
    ///     <c>calculate</c> transform is not supported yet no longer exists, every view is drawn): a <c>filter</c> of <c>datum.x &gt; 1</c> and a <c>calculate</c> of <c>z</c> leave three
    ///     of the four rows in every view (a facet by <c>g</c>: one row of a and two of b), as Vega-Lite's children inherit the data and the transforms of their parent, and nothing is
    ///     reported as unsupported or as a warning.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Composition_TopLevelTransformsApplyToEveryViewWithoutDiagnostics()
    {
        // Arrange: four rows in two groups; the filter keeps x = 2, 3, 4 and the calculate doubles x
        const string head = """{ "data": { "values": [ { "x": 1, "g": "a" }, { "x": 2, "g": "a" }, { "x": 3, "g": "b" }, { "x": 4, "g": "b" } ] }, "transform": [ { "filter": "datum.x > 1" }, { "calculate": "datum.x * 2", "as": "z" } ], """;
        const string point = """{ "width": 60, "height": 60, "mark": "point", "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "z", "type": "quantitative" } } }""";
        var specs = new Dictionary<string, (string Json, int[] Counts)>
        {
            ["layer"] = (head + "\"layer\": [ " + point + ", " + point + " ] }", [3, 3]),
            ["hconcat"] = (head + "\"hconcat\": [ " + point + ", " + point + " ] }", [3, 3]),
            ["vconcat"] = (head + "\"vconcat\": [ " + point + ", " + point + " ] }", [3, 3]),
            ["facet"] = (head + "\"facet\": { \"column\": { \"field\": \"g\", \"type\": \"nominal\" } }, \"spec\": " + point + " }", [1, 2]),
            ["repeat"] = (
                head + "\"repeat\": { \"column\": [\"x\", \"z\"] }, \"spec\": { \"width\": 60, \"height\": 60, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": { \"repeat\": \"column\" }, \"type\": \"quantitative\" }, \"y\": { \"field\": \"z\", \"type\": \"quantitative\" } } } }",
                [3, 3]),
        };

        foreach (var (name, (json, counts)) in specs)
        {
            // Act
            var warnings = new List<VegaLiteDiagnostic>();
            var unsupported = new List<VegaLiteUnsupportedFeature>();
            var scene = VegaLiteDocument.Parse(json).BuildScene(new VegaLiteRenderOptions { WarningHandler = warnings.Add, UnsupportedFeatureHandler = unsupported.Add }, Ct);
            var perView = scene.FindAll("marks").Select(e => ((SceneGroup)e.Item).Children.Count(c => c.Role == "mark-point")).Where(n => n > 0).OrderBy(n => n).ToArray();

            // Assert: the points of every view and no diagnostic
            Assert.True(counts.SequenceEqual(perView), $"{name}: expected {string.Join(",", counts)} points per view, got {string.Join(",", perView)}.");
            Assert.Empty(warnings);
            Assert.Empty(unsupported);
        }
    }

    /// <summary>
    ///     Renders a specification and writes its scene (the type, the offset, and the text of every item in drawing order) and its pixels.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="options">The render options.</param>
    /// <returns>The scene text and the pixels.</returns>
    private static (string Scene, byte[] Pixels) Capture(string json, VegaLiteRenderOptions options)
    {
        var document = VegaLiteDocument.Parse(json);
        var scene = document.BuildScene(options, Ct);
        var text = new StringBuilder();
        text.Append(scene.Width.ToString("R", CultureInfo.InvariantCulture)).Append('x').Append(scene.Height.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        foreach (var entry in scene.Enumerate())
        {
            text.Append(entry.Item.GetType().Name)
                .Append(" @").Append(entry.OffsetX.ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(entry.OffsetY.ToString("R", CultureInfo.InvariantCulture));
            if (entry.Item is SceneText label)
            {
                text.Append(' ').Append(label.Text);
            }

            text.Append('\n');
        }

        using var surface = document.Render(options, Ct);
        return (text.ToString(), VegaLiteTestHelpers.Snapshot(surface));
    }
}
