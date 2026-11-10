using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Benchmarks;

/// <summary>
///     One timed rendering: a specification (parsed once), the options of the render, and the number of units the cost is divided by.
/// </summary>
/// <param name="Name">The short name of the workload.</param>
/// <param name="Description">What the workload stresses.</param>
/// <param name="Spec">The specification text.</param>
/// <param name="Units">The number of rows, items, or pictures that the time is divided by.</param>
/// <param name="UnitName">The name of one unit (<c>row</c>, <c>image</c>, ...).</param>
/// <param name="Options">Makes the render options of one run.</param>
internal sealed record Workload(string Name, string Description, string Spec, long Units, string UnitName, Func<VegaLiteRenderOptions> Options);

/// <summary>
///     One calibration probe: a specification that costs far more than a tightened limit, rendered until the limit stops it, to find how long the stop takes. The time is scaled to the
///     default limit (<paramref name="Scale"/> times the tightened one) to check that the work meter and the paint budget stop a runaway specification within about ten seconds.
/// </summary>
/// <param name="Name">The short name of the probe.</param>
/// <param name="Family">The family of the cost (<c>loess</c>, <c>density</c>, ...).</param>
/// <param name="Spec">The specification text.</param>
/// <param name="Limits">The tightened limits.</param>
/// <param name="Scale">The factor between the default limit and the tightened one.</param>
/// <param name="LimitName">The name of the limit that must stop the probe, as the message of the exception names it.</param>
internal sealed record CalibrationProbe(string Name, string Family, string Spec, VegaLiteLimits Limits, double Scale, string LimitName);

/// <summary>
///     The workloads and the calibration probes of the harness. Every specification is made of a <c>sequence</c> and <c>calculate</c> transforms, so no data file is read and the rows are a
///     function of their index (deterministic, and identical on every machine).
/// </summary>
internal static class Workloads
{
    /// <summary>The steps of the default <see cref="VegaLiteLimits.MaxEvaluationSteps"/>.</summary>
    private const long DefaultSteps = 50_000_000;

    /// <summary>The pixels of the default <see cref="VegaLiteLimits.MaxPaintedPixels"/>.</summary>
    private const long DefaultPixels = 250_000_000;

    /// <summary>
    ///     A resolver that hands out a solid picture of the requested size for every link.
    /// </summary>
    private sealed class SolidResolver : IImageResolver
    {
        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            var surface = new Surface(pixelWidth, pixelHeight);
            surface.Clear(new Rgba32(0x2a, 0x9d, 0x8f, 255));
            return surface;
        }
    }

    /// <summary>
    ///     Makes the workloads, with the sizes of a normal run or the tiny sizes of the smoke run.
    /// </summary>
    /// <param name="smoke">True for the tiny sizes (a quick check that every workload still renders).</param>
    /// <returns>The workloads.</returns>
    public static IReadOnlyList<Workload> Make(bool smoke)
    {
        var scatter = Size(100_000, smoke);
        var stacked = Size(200_000, smoke);
        var facet = Size(60_000, smoke);
        var loess = Size(5_000, smoke);
        var density = Size(200_000, smoke);
        var regression = Size(200_000, smoke);
        var window = Size(50_000, smoke);
        var images = Size(2_000, smoke);
        var rects = Size(200, smoke);

        return
        [
            new Workload("scatter", "A point mark per row, colored by a group, on a 800x500 view", ScatterSpec(scatter), scatter, "row", Defaults),
            new Workload("stacked-bars", "Rows summed into stacked bars: 400 categories by 6 series", StackedBarsSpec(stacked), stacked, "row", Defaults),
            new Workload("facet-grid", "A 6x6 facet of point marks over the rows", FacetSpec(facet), facet, "row", Defaults),
            new Workload("loess", "A loess smoother (bandwidth 0.3) over the rows", LoessSpec(loess, 0.3), loess, "row", Defaults),
            new Workload("density", "A kernel density estimate (200 steps) over the rows", DensitySpec(density, 200), density, "row", Defaults),
            new Workload("regression", "A cubic polynomial regression over the rows", RegressionSpec(regression, 3), regression, "row", Defaults),
            new Workload("window", "A running mean over a window of 101 rows", WindowSpec(window), window, "row", Defaults),
            new Workload("images", "Image marks, each a 24x24 picture from a resolver", ImagesSpec(images), images, "image", ImageOptions),
            new Workload("full-rects", "Rect marks that each cover the whole 1000x800 view (painted pixels)", FullRectsSpec(rects), rects, "rect", Defaults),
        ];
    }

    /// <summary>
    ///     Makes the calibration probes, each of which a tightened limit stops.
    /// </summary>
    /// <param name="smoke">True for the tiny sizes.</param>
    /// <returns>The probes.</returns>
    public static IReadOnlyList<CalibrationProbe> MakeCalibration(bool smoke)
    {
        // The default budget is 50 million steps: the probes run against a fiftieth of it and the time is scaled up
        const double stepScale = 50;
        var limits = new VegaLiteLimits { MaxEvaluationSteps = (long)(DefaultSteps / stepScale), MaxDataRows = 1_000_000 };

        // 10,000,000 pixels is a twenty-fifth of the default painted-pixel limit
        const double pixelScale = 25;
        var pixelLimits = new VegaLiteLimits { MaxPaintedPixels = (long)(DefaultPixels / pixelScale) };

        return
        [
            new CalibrationProbe("loess-bandwidth-1", "loess", LoessSpec(Size(20_000, smoke), 1.0), limits, stepScale, "MaxEvaluationSteps"),
            new CalibrationProbe("density-1000-steps", "density", DensitySpec(Size(200_000, smoke), 1_000), limits, stepScale, "MaxEvaluationSteps"),
            new CalibrationProbe("poly-regression-order-8", "regression", RegressionSpec(Size(200_000, smoke), 8), limits, stepScale, "MaxEvaluationSteps"),
            new CalibrationProbe("window-median-wide-frame", "window", WindowMedianSpec(Size(20_000, smoke)), limits, stepScale, "MaxEvaluationSteps"),
            new CalibrationProbe("bootstrap-ci", "aggregate", BootstrapSpec(Size(20_000, smoke)), limits, stepScale, "MaxEvaluationSteps"),
            new CalibrationProbe("full-surface-rects", "painter", FullRectsSpec(Size(2_000, smoke)), pixelLimits, pixelScale, "MaxPaintedPixels"),
        ];
    }

    /// <summary>
    ///     Gets a size of a normal run, or a hundredth of it (at least 20) for the smoke run.
    /// </summary>
    /// <param name="normal">The size of a normal run.</param>
    /// <param name="smoke">True for the smoke run.</param>
    /// <returns>The size.</returns>
    private static long Size(long normal, bool smoke) => smoke ? Math.Max(normal / 100, 20) : normal;

    /// <summary>
    ///     Gets the default render options of a workload.
    /// </summary>
    /// <returns>The options.</returns>
    private static VegaLiteRenderOptions Defaults() => new() { ThrowOnUnsupportedFeature = true };

    /// <summary>
    ///     Gets the render options of the image workload: a resolver and room for the pictures.
    /// </summary>
    /// <returns>The options.</returns>
    private static VegaLiteRenderOptions ImageOptions() => new()
    {
        ThrowOnUnsupportedFeature = true,
        ImageResolver = new SolidResolver(),
        Limits = new VegaLiteLimits { MaxImages = 100_000, MaxSceneItems = 4_000_000 },
    };

    /// <summary>
    ///     Makes the specification of the scatter plot.
    /// </summary>
    /// <param name="rows">The number of points.</param>
    /// <returns>The text.</returns>
    private static string ScatterSpec(long rows) => $$$"""
        {
          "width": 800, "height": 500,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "datum.i % 1000 + 30 * sin(datum.i * 0.01)", "as": "x"},
            {"calculate": "(datum.i * 7919) % 1013 + 40 * cos(datum.i * 0.003)", "as": "y"},
            {"calculate": "'g' + (datum.i % 5)", "as": "g"}
          ],
          "mark": {"type": "point", "filled": true, "size": 12, "opacity": 0.6},
          "encoding": {
            "x": {"field": "x", "type": "quantitative"},
            "y": {"field": "y", "type": "quantitative"},
            "color": {"field": "g", "type": "nominal"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of the stacked bars.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The text.</returns>
    private static string StackedBarsSpec(long rows) => $$$"""
        {
          "width": 1200, "height": 400,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "'c' + (datum.i % 400)", "as": "category"},
            {"calculate": "'s' + (datum.i % 6)", "as": "series"},
            {"calculate": "1 + (datum.i % 17)", "as": "value"}
          ],
          "mark": "bar",
          "encoding": {
            "x": {"field": "category", "type": "nominal", "axis": null},
            "y": {"field": "value", "type": "quantitative", "aggregate": "sum"},
            "color": {"field": "series", "type": "nominal"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of the facet grid.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The text.</returns>
    private static string FacetSpec(long rows) => $$$"""
        {
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "'r' + (datum.i % 6)", "as": "r"},
            {"calculate": "'c' + (floor(datum.i / 6) % 6)", "as": "c"},
            {"calculate": "datum.i % 997", "as": "x"},
            {"calculate": "(datum.i * 31) % 541", "as": "y"}
          ],
          "facet": {"row": {"field": "r", "type": "nominal"}, "column": {"field": "c", "type": "nominal"}},
          "spec": {
            "width": 100, "height": 70,
            "mark": {"type": "point", "size": 8},
            "encoding": {
              "x": {"field": "x", "type": "quantitative"},
              "y": {"field": "y", "type": "quantitative"}
            }
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of a loess smoother.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="bandwidth">The bandwidth.</param>
    /// <returns>The text.</returns>
    private static string LoessSpec(long rows, double bandwidth) => $$$"""
        {
          "width": 600, "height": 300,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "datum.i / 10", "as": "x"},
            {"calculate": "sin(datum.i / 200) * 40 + (datum.i * 7919) % 31", "as": "y"},
            {"loess": "y", "on": "x", "bandwidth": {{{bandwidth.ToString("0.0###", CultureInfo.InvariantCulture)}}}}
          ],
          "mark": "line",
          "encoding": {
            "x": {"field": "x", "type": "quantitative"},
            "y": {"field": "y", "type": "quantitative"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of a kernel density estimate.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="steps">The number of evaluation points.</param>
    /// <returns>The text.</returns>
    private static string DensitySpec(long rows, int steps) => $$$"""
        {
          "width": 600, "height": 300,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "sin(datum.i * 0.37) * 10 + cos(datum.i * 1.3) * 4 + (datum.i % 7)", "as": "v"},
            {"density": "v", "steps": {{{steps}}}, "as": ["v", "density"]}
          ],
          "mark": "area",
          "encoding": {
            "x": {"field": "v", "type": "quantitative"},
            "y": {"field": "density", "type": "quantitative"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of a polynomial regression.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="order">The order of the polynomial.</param>
    /// <returns>The text.</returns>
    private static string RegressionSpec(long rows, int order) => $$$"""
        {
          "width": 600, "height": 300,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "datum.i / 1000", "as": "x"},
            {"calculate": "sin(datum.x) * 20 + datum.x * 0.1 + (datum.i * 7919) % 13", "as": "y"},
            {"regression": "y", "on": "x", "method": "poly", "order": {{{order}}}}
          ],
          "mark": "line",
          "encoding": {
            "x": {"field": "x", "type": "quantitative"},
            "y": {"field": "y", "type": "quantitative"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of a running mean.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The text.</returns>
    private static string WindowSpec(long rows) => $$$"""
        {
          "width": 600, "height": 300,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "sin(datum.i / 300) * 30 + (datum.i * 7919) % 11", "as": "y"},
            {"window": [{"op": "mean", "field": "y", "as": "avg"}], "frame": [-50, 50]}
          ],
          "mark": "line",
          "encoding": {
            "x": {"field": "i", "type": "quantitative"},
            "y": {"field": "avg", "type": "quantitative"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of a running median over a wide frame (a frame that is recomputed for every row).
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The text.</returns>
    private static string WindowMedianSpec(long rows) => $$$"""
        {
          "width": 600, "height": 300,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "sin(datum.i / 300) * 30 + (datum.i * 7919) % 11", "as": "y"},
            {"window": [{"op": "median", "field": "y", "as": "med"}], "frame": [-5000, 5000]}
          ],
          "mark": "line",
          "encoding": {
            "x": {"field": "i", "type": "quantitative"},
            "y": {"field": "med", "type": "quantitative"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of an aggregate with a bootstrap confidence interval.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <returns>The text.</returns>
    private static string BootstrapSpec(long rows) => $$$"""
        {
          "width": 400, "height": 200,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "'g' + (datum.i % 4)", "as": "g"},
            {"calculate": "sin(datum.i / 50) * 30 + (datum.i * 7919) % 11", "as": "y"},
            {"aggregate": [{"op": "ci0", "field": "y", "as": "lo"}, {"op": "ci1", "field": "y", "as": "hi"}], "groupby": ["g"]}
          ],
          "mark": "bar",
          "encoding": {
            "x": {"field": "g", "type": "nominal"},
            "y": {"field": "lo", "type": "quantitative"},
            "y2": {"field": "hi"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of many pictures.
    /// </summary>
    /// <param name="rows">The number of pictures.</param>
    /// <returns>The text.</returns>
    private static string ImagesSpec(long rows) => $$$"""
        {
          "width": 800, "height": 500,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "transform": [
            {"calculate": "datum.i % 100", "as": "x"},
            {"calculate": "floor(datum.i / 100)", "as": "y"},
            {"calculate": "'picture-' + (datum.i % 3) + '.png'", "as": "src"}
          ],
          "mark": {"type": "image", "width": 24, "height": 24},
          "encoding": {
            "x": {"field": "x", "type": "quantitative"},
            "y": {"field": "y", "type": "quantitative"},
            "url": {"field": "src", "type": "nominal"}
          }
        }
        """;

    /// <summary>
    ///     Makes the specification of rect marks that each cover the whole view.
    /// </summary>
    /// <param name="rows">The number of rects.</param>
    /// <returns>The text.</returns>
    private static string FullRectsSpec(long rows) => $$$"""
        {
          "width": 1000, "height": 800,
          "data": {"sequence": {"start": 0, "stop": {{{rows}}}, "as": "i"}},
          "mark": {"type": "rect", "opacity": 0.5},
          "encoding": {
            "x": {"datum": 0, "type": "quantitative", "scale": {"domain": [0, 1]}},
            "x2": {"datum": 1},
            "y": {"datum": 0, "type": "quantitative", "scale": {"domain": [0, 1]}},
            "y2": {"datum": 1},
            "color": {"field": "i", "type": "quantitative", "legend": null}
          }
        }
        """;
}
