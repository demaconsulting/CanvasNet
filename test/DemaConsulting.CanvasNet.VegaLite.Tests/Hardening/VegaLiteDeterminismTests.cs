using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     Determinism tests of the Vega-Lite renderer. The promise is that a rendering is a pure function of the specification, the data and the options: the same <see cref="Surface"/> pixels on every
///     run, every .NET version the package targets, every culture and every thread, and (within a tolerance, because floating point libraries differ in the last bit) every operating system.
///     The pixels are compared, never the bytes of an encoded PNG: the deflate streams of .NET 8 and of .NET 9 and later differ for the same pixels.
/// </summary>
/// <remarks>
///     <para>
///         <b>Two tiers.</b> The exact tier pins the SHA-256 of the pixels of charts whose geometry is made from additions, multiplications and divisions only (bars, rules, ticks, text and linear
///         axes): such a chart has the same pixels wherever the tests run, so the pins are the same on every target framework and operating system. The tolerant tier is for the charts that go through a
///         library function that may differ in the last bit between Windows, Linux and macOS (the circle of a symbol, curves, arcs, logarithmic scales, statistics): their pixels are compared with a committed picture
///         within the tolerance that the repository uses for its committed images (<c>docs/images/render-images.ps1</c>: a root-mean-square difference of one color level and at most 0.1% of the pixels that differ by
///         more than eight levels), which absorbs the few anti-aliased pixels that a last-bit difference moves and nothing else.
///     </para>
///     <para>
///         To capture the pins and the pictures again after a deliberate change of the output, set the environment variable <c>CANVASNET_DETERMINISM_DIR</c> to the folder
///         <c>test/DemaConsulting.CanvasNet.VegaLite.Tests/Fixtures/Determinism</c> and run the tests of this class: they then write the files instead of comparing them.
///     </para>
/// </remarks>
public class VegaLiteDeterminismTests
{
    /// <summary>The environment variable that switches the tests from comparing to writing the pins and the pictures.</summary>
    private const string WriteVariable = "CANVASNET_DETERMINISM_DIR";

    /// <summary>The largest root-mean-square difference of the color levels of the tolerant tier (the tolerance of <c>render-images.ps1</c>).</summary>
    private const double MaxRmse = 1.0;

    /// <summary>The largest share of pixels that differ by more than <see cref="LevelThreshold"/> in the tolerant tier (the tolerance of <c>render-images.ps1</c>).</summary>
    private const double MaxDiffRatio = 0.001;

    /// <summary>The difference of a color level above which a pixel counts as different.</summary>
    private const int LevelThreshold = 8;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The charts of the exact tier: made of rectangles, lines and text on linear and band axes.</summary>
    private static readonly SortedDictionary<string, string> ExactCharts = new(StringComparer.Ordinal)
    {
        ["bar-nominal"] = """
            { "title": "Totals", "width": 200, "height": 140,
              "data": { "values": [ { "a": "A", "v": 4 }, { "a": "B", "v": 8 }, { "a": "C", "v": 6 }, { "a": "D", "v": 2 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "title": "Value" } } }
            """,
        ["stacked-bars"] = """
            { "width": 180, "height": 140,
              "data": { "values": [ { "r": "N", "p": "a", "u": 4 }, { "r": "N", "p": "b", "u": 6 }, { "r": "S", "p": "a", "u": 5 }, { "r": "S", "p": "b", "u": 2 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "r", "type": "nominal" }, "y": { "field": "u", "aggregate": "sum", "type": "quantitative" }, "color": { "field": "p", "type": "nominal", "legend": null } } }
            """,
        ["histogram"] = """
            { "width": 220, "height": 120,
              "data": { "values": [ { "w": 1 }, { "w": 3 }, { "w": 4 }, { "w": 8 }, { "w": 9 }, { "w": 12 }, { "w": 21 }, { "w": 22 }, { "w": 30 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "w", "bin": true, "type": "quantitative" }, "y": { "aggregate": "count", "type": "quantitative" } } }
            """,
        ["rule-tick-strip"] = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "c": "p", "x": 1 }, { "c": "p", "x": 4 }, { "c": "q", "x": 2 }, { "c": "q", "x": 9 }, { "c": "r", "x": 5 } ] },
              "layer": [
                { "mark": "tick", "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "c", "type": "ordinal" } } },
                { "mark": "rule", "encoding": { "x": { "datum": 5 }, "color": { "value": "firebrick" } } } ] }
            """,
        ["text-labels"] = """
            { "width": 200, "height": 100,
              "data": { "values": [ { "a": "one", "v": 3 }, { "a": "two", "v": 7 }, { "a": "three", "v": 5 } ] },
              "mark": { "type": "text", "fontSize": 14, "dy": -4 },
              "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "text": { "field": "v", "type": "quantitative" } } }
            """,
        ["grouped-bars-offset"] = """
            { "width": 200, "height": 120,
              "data": { "values": [ { "g": "x", "k": "m", "v": 3 }, { "g": "x", "k": "n", "v": 5 }, { "g": "y", "k": "m", "v": 4 }, { "g": "y", "k": "n", "v": 1 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "g", "type": "nominal" }, "xOffset": { "field": "k" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal", "legend": null } } }
            """,
    };

    /// <summary>The charts of the tolerant tier: circles, curves, arcs, logarithmic scales and statistics.</summary>
    private static readonly SortedDictionary<string, string> TolerantCharts = new(StringComparer.Ordinal)
    {
        ["scatter-size-legend"] = """
            { "width": 200, "height": 140,
              "data": { "values": [ { "x": 1, "y": 3, "k": "p", "s": 5 }, { "x": 4, "y": 9, "k": "q", "s": 25 }, { "x": 7, "y": 2, "k": "p", "s": 15 }, { "x": 9, "y": 5, "k": "r", "s": 40 } ] },
              "mark": { "type": "point", "filled": true },
              "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" }, "size": { "field": "s", "type": "quantitative" } } }
            """,
        ["line-time-monotone"] = """
            { "width": 220, "height": 120,
              "data": { "values": [ { "d": "2024-01-01", "v": 3 }, { "d": "2024-02-15", "v": 7 }, { "d": "2024-04-01", "v": 5 }, { "d": "2024-06-30", "v": 9 } ] },
              "mark": { "type": "line", "point": true, "interpolate": "monotone" },
              "encoding": { "x": { "field": "d", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } } }
            """,
        ["area-density"] = """
            { "width": 220, "height": 120,
              "data": { "values": [ { "w": 1 }, { "w": 3 }, { "w": 4 }, { "w": 8 }, { "w": 9 }, { "w": 12 }, { "w": 21 }, { "w": 22 }, { "w": 30 } ] },
              "transform": [ { "density": "w", "extent": [0, 35] } ],
              "mark": "area",
              "encoding": { "x": { "field": "value", "type": "quantitative" }, "y": { "field": "density", "type": "quantitative" } } }
            """,
        ["pie-arcs"] = """
            { "width": 140, "height": 140,
              "data": { "values": [ { "k": "a", "v": 4 }, { "k": "b", "v": 6 }, { "k": "c", "v": 2 }, { "k": "d", "v": 3 } ] },
              "mark": { "type": "arc", "innerRadius": 20 },
              "encoding": { "theta": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal", "legend": null } } }
            """,
        ["log-scatter"] = """
            { "width": 200, "height": 140,
              "data": { "values": [ { "x": 1, "y": 10 }, { "x": 10, "y": 200 }, { "x": 100, "y": 3000 }, { "x": 1000, "y": 70 } ] },
              "mark": "circle",
              "encoding": { "x": { "field": "x", "type": "quantitative", "scale": { "type": "log" } }, "y": { "field": "y", "type": "quantitative", "scale": { "type": "log" } } } }
            """,
        ["regression-boxplot"] = """
            { "width": 200, "height": 140,
              "data": { "values": [ { "x": 1, "y": 2 }, { "x": 2, "y": 4 }, { "x": 3, "y": 5 }, { "x": 4, "y": 9 }, { "x": 5, "y": 10 }, { "x": 6, "y": 14 } ] },
              "layer": [
                { "mark": "point", "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } } },
                { "transform": [ { "regression": "y", "on": "x", "method": "quad" } ], "mark": { "type": "line", "color": "firebrick" },
                  "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } } } ] }
            """,
    };

    /// <summary>
    ///     Gets the names of the charts of the tolerant tier (for the theory).
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> TolerantNames() => [.. TolerantCharts.Keys];

    /// <summary>
    ///     Validates the exact tier: the SHA-256 of the pixels of every chart equals the committed pin, which is the same on every .NET version and every operating system.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_ExactPixelPinsAreCurrent()
    {
        // Arrange
        var actual = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, spec) in ExactCharts)
        {
            using var surface = Render(spec);
            actual[name] = $"{surface.Width}x{surface.Height} {VegaLiteFuzzHarness.PixelHash(surface)}";
        }

        var pinFile = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Determinism", "pixel-pins.txt");
        if (Environment.GetEnvironmentVariable(WriteVariable) is { Length: > 0 } directory)
        {
            File.WriteAllLines(Path.Combine(directory, "pixel-pins.txt"), actual.Select(a => a.Key + " " + a.Value));
            return;
        }

        // Act
        var pins = File.ReadAllLines(pinFile).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split(' ', 2)).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);

        // Assert: every chart has a pin, every pin a chart, and the pixels are the pinned ones
        Assert.Equal(pins.Keys.OrderBy(k => k, StringComparer.Ordinal), actual.Keys);
        var different = actual.Where(a => pins[a.Key] != a.Value).Select(a => $"{a.Key}: pinned {pins[a.Key]}, rendered {a.Value}").ToList();
        Assert.True(different.Count == 0, "The pixels of these charts differ from their pins (regenerate with " + WriteVariable + " after a deliberate change):\n" + string.Join("\n", different));
    }

    /// <summary>
    ///     Validates the tolerant tier: the pixels of a chart that goes through a floating point library function match the committed picture within the tolerance of the repository's committed images.
    /// </summary>
    /// <param name="name">The name of the chart.</param>
    [Theory]
    [MemberData(nameof(TolerantNames))]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_TolerantChartMatchesItsGolden(string name)
    {
        // Arrange
        using var actual = Render(TolerantCharts[name]);
        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Determinism", "golden", name + ".png");
        if (Environment.GetEnvironmentVariable(WriteVariable) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(Path.Combine(directory, "golden"));
            PngCodec.Save(actual, Path.Combine(directory, "golden", name + ".png"));
            return;
        }

        // Act
        using var golden = PngCodec.Load(goldenPath);
        Assert.Equal((golden.Width, golden.Height), (actual.Width, actual.Height));
        var (rmse, ratio) = Compare(golden, actual);

        // Assert
        Assert.True(rmse <= MaxRmse, $"{name}: rmse {rmse:F3} is above {MaxRmse}");
        Assert.True(ratio <= MaxDiffRatio, $"{name}: {ratio:P3} of the pixels differ by more than {LevelThreshold} levels (at most {MaxDiffRatio:P1})");
    }

    /// <summary>
    ///     Validates that rendering is strictly deterministic within one process: the same document and options give identical pixels on every call, for every chart of both tiers.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_SameSpecificationGivesIdenticalPixelsTwice()
    {
        // Arrange / Act
        var differences = new List<string>();
        foreach (var (name, spec) in ExactCharts.Concat(TolerantCharts))
        {
            var document = VegaLiteDocument.Parse(spec);
            using var first = document.Render(new VegaLiteRenderOptions(), Ct);
            using var second = document.Render(new VegaLiteRenderOptions(), Ct);
            using var parsedAgain = VegaLiteDocument.Parse(spec).Render(new VegaLiteRenderOptions(), Ct);
            if (VegaLiteFuzzHarness.PixelHash(first) != VegaLiteFuzzHarness.PixelHash(second) || VegaLiteFuzzHarness.PixelHash(first) != VegaLiteFuzzHarness.PixelHash(parsedAgain))
            {
                differences.Add(name);
            }
        }

        // Assert
        Assert.Empty(differences);
    }

    /// <summary>
    ///     Validates that one document rendered from several threads at once gives the pixels of a single-threaded rendering: the order in which the threads run changes nothing.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_ConcurrentRenderingsGiveTheSamePixels()
    {
        // Arrange
        var spec = TolerantCharts["scatter-size-legend"];
        var document = VegaLiteDocument.Parse(spec);
        using var reference = document.Render(new VegaLiteRenderOptions(), Ct);
        var expected = VegaLiteFuzzHarness.PixelHash(reference);

        // Act
        var hashes = new string[8];
        Parallel.For(0, hashes.Length, new ParallelOptions { MaxDegreeOfParallelism = hashes.Length, CancellationToken = Ct }, i =>
        {
            using var surface = document.Render(new VegaLiteRenderOptions(), Ct);
            hashes[i] = VegaLiteFuzzHarness.PixelHash(surface);
        });

        // Assert
        Assert.All(hashes, hash => Assert.Equal(expected, hash));
    }

    /// <summary>
    ///     Validates that the culture of the thread changes nothing: with a culture that writes numbers and dates differently (decimal comma, other digits, a Turkish dotless i) the pixels are those of the invariant culture, for
    ///     a chart with number formats, time labels and upper and lower case text.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_CultureOfTheThreadChangesNothing()
    {
        // Arrange: decimals, grouping, month names, percentages and the letters whose case depends on the culture (I and i in Turkish)
        // cspell:ignore İstanbul illi
        const string spec = """
            { "title": "Impact in İstanbul, 1,234.5 %", "width": 220, "height": 120,
              "data": { "values": [ { "d": "2024-01-15", "v": 1234.5, "k": "Iris" }, { "d": "2024-03-20", "v": 0.25, "k": "illi" }, { "d": "2024-08-02", "v": 98765.4321, "k": "INDIGO" } ] },
              "transform": [ { "calculate": "upper(datum.k) + lower(datum.k)", "as": "u" } ],
              "mark": "point",
              "encoding": { "x": { "field": "d", "type": "temporal", "axis": { "format": "%B %d, %Y" } },
                            "y": { "field": "v", "type": "quantitative", "axis": { "format": "$,.2f" } },
                            "color": { "field": "u", "type": "nominal" } } }
            """;
        var expected = RenderHash(spec);
        var tested = new List<string>();

        // Act / Assert
        foreach (var name in new[] { "tr-TR", "de-DE", "fr-FR", "ar-SA", "ja-JP", "th-TH", "en-IN" })
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                // A host without culture data (invariant globalization) cannot test the culture
                continue;
            }

            var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            try
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                Assert.Equal(expected, RenderHash(spec));
                tested.Add(name);
            }
            finally
            {
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
            }
        }

        Assert.NotNull(tested);
    }

    /// <summary>
    ///     Validates that the time zone of the machine changes nothing: a rendering without a time zone is a rendering in UTC (the local time zone is never read; the source guard test forbids the calls), and
    ///     a rendering in another zone differs only through the zone that the caller names.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_NoTimeZoneMeansUtc()
    {
        // Arrange
        var spec = TolerantCharts["line-time-monotone"];
        var document = VegaLiteDocument.Parse(spec);

        // Act
        using var unspecified = document.Render(new VegaLiteRenderOptions(), Ct);
        using var utc = document.Render(new VegaLiteRenderOptions { TimeZone = TimeZoneInfo.Utc }, Ct);

        // Assert
        Assert.Equal(VegaLiteFuzzHarness.PixelHash(utc), VegaLiteFuzzHarness.PixelHash(unspecified));
    }

    /// <summary>
    ///     Validates that the order in which the caller's dictionaries were filled and the order of the fields inside the rows of the data change nothing: the datasets and the parameters are looked up
    ///     by name, never enumerated in the order of a hash table.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_InsertionOrderOfInputsChangesNothing()
    {
        // Arrange: two named datasets and two parameters, supplied in two orders; the same rows with the fields written in the opposite order
        const string spec = """
            { "width": 200, "height": 120,
              "params": [ { "name": "lo", "value": 1 }, { "name": "hi", "value": 8 } ],
              "layer": [
                { "data": { "name": "left" }, "mark": "point", "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } } },
                { "data": { "name": "right" }, "mark": { "type": "line", "color": "firebrick" }, "encoding": { "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } } } ],
              "transform": [ { "filter": "datum.x >= lo && datum.x <= hi" } ] }
            """;
        var left = new[] { Row(("x", 1), ("y", 2)), Row(("x", 3), ("y", 5)), Row(("x", 9), ("y", 4)) };
        var right = new[] { Row(("x", 2), ("y", 3)), Row(("x", 4), ("y", 1)), Row(("x", 7), ("y", 6)) };
        var leftReversed = left.Select(r => Row(r.Reverse().Select(kv => (kv.Key, (double)kv.Value!)).ToArray())).ToArray();
        var rightReversed = right.Select(r => Row(r.Reverse().Select(kv => (kv.Key, (double)kv.Value!)).ToArray())).ToArray();

        // Act
        var forward = RenderHash(spec, Datasets(("left", left), ("right", right)), Parameters(("lo", 1.0), ("hi", 8.0)));
        var backward = RenderHash(spec, Datasets(("right", rightReversed), ("left", leftReversed)), Parameters(("hi", 8.0), ("lo", 1.0)));

        // Assert
        Assert.Equal(forward, backward);
    }

    /// <summary>
    ///     Validates the numeric edge corpus: specifications that steer a number outside the range of an integer or of a double (the ones that made a conversion differ between .NET 8 and .NET 9 and later) end the same way
    ///     on every runtime. The expected outcomes are written down here, and the tests run on every target framework, so a runtime that differs fails.
    /// </summary>
    /// <param name="name">The name of the case.</param>
    /// <param name="expected">The expected outcome.</param>
    [Theory]
    [InlineData("quantile-step-beyond-int", "VegaLiteRenderException@/transform/0")]
    [InlineData("pivot-limit-beyond-long", "ok")]
    [InlineData("pow-scale-overflow", "VegaLiteRenderException@")]
    [InlineData("denormal-tick-span", "ok")]
    [InlineData("impute-keys-beyond-int", "VegaLiteRenderException@/transform/0")]
    [InlineData("window-frame-beyond-int", "VegaLiteSpecException@/transform/0/frame/0")]
    [InlineData("tick-count-beyond-int", "VegaLiteRenderException@/encoding/x")]
    [InlineData("band-padding-beyond-int", "ok")]
    [InlineData("bin-step-denormal", "VegaLiteRenderException@/encoding/x/bin")]
    [InlineData("sample-beyond-int", "ok")]
    [InlineData("density-steps-beyond-int", "VegaLiteRenderException@/transform/0")]
    [InlineData("regression-order-beyond-int", "VegaLiteSpecException@/transform/0/order")]
    [InlineData("loess-bandwidth-denormal", "ok")]
    [InlineData("size-beyond-int", "ok")]
    public void CanvasNetVegaLite_SystemIntegration_Determinism_NumericEdgeOutcomeIsTheSameOnEveryRuntime(string name, string expected)
    {
        // Arrange
        var spec = NumericEdgeSpecs[name];

        // Act
        string outcome;
        try
        {
            var limits = new VegaLiteLimits { MaxDataRows = 20_000 };
            using var surface = VegaLiteDocument.Parse(spec, new VegaLiteOpenOptions { Limits = limits }).Render(120, 100, new VegaLiteRenderOptions { Limits = limits }, Ct);
            outcome = "ok";
        }
        catch (VegaLiteRenderException ex)
        {
            outcome = nameof(VegaLiteRenderException) + "@" + ex.Location.Pointer;
        }
        catch (VegaLiteSpecException ex)
        {
            outcome = nameof(VegaLiteSpecException) + "@" + ex.Location.Pointer;
        }

        // Assert
        Assert.Equal(expected, outcome);
    }

    /// <summary>The specifications of the numeric edge corpus.</summary>
    private static readonly Dictionary<string, string> NumericEdgeSpecs = new(StringComparer.Ordinal)
    {
        ["quantile-step-beyond-int"] = Chart("\"transform\": [ { \"quantile\": \"y\", \"step\": 1e-300 } ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"prob\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"value\", \"type\": \"quantitative\" } }"),
        ["pivot-limit-beyond-long"] = Chart("\"transform\": [ { \"pivot\": \"k\", \"value\": \"y\", \"groupby\": [\"u\"], \"op\": \"mean\", \"limit\": 1.7976931348623157E+308 } ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"u\", \"type\": \"nominal\" } }"),
        ["pow-scale-overflow"] = Chart("\"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"scale\": { \"type\": \"pow\", \"exponent\": 1e300 } }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }"),
        ["denormal-tick-span"] = "{ \"data\": { \"values\": [ { \"x\": 5e-324, \"y\": 1 }, { \"x\": 1e-300, \"y\": 2 } ] }, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"axis\": { \"tickCount\": 1e10 } }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } } }",
        ["impute-keys-beyond-int"] = Chart("\"transform\": [ { \"impute\": \"y\", \"key\": \"x\", \"keyvals\": { \"start\": 0, \"stop\": 3e9 }, \"value\": 0 } ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" } }"),
        ["window-frame-beyond-int"] = Chart("\"transform\": [ { \"window\": [ { \"op\": \"sum\", \"field\": \"y\", \"as\": \"s\" } ], \"frame\": [-1e18, 1e18] } ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" } }"),
        ["tick-count-beyond-int"] = Chart("\"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\", \"axis\": { \"tickCount\": 1.7976931348623157E+308 } }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }"),
        ["band-padding-beyond-int"] = Chart("\"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"u\", \"type\": \"nominal\", \"scale\": { \"padding\": -4294967296 } }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }"),
        ["bin-step-denormal"] = Chart("\"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"x\", \"bin\": { \"step\": 5e-324 }, \"type\": \"quantitative\" }, \"y\": { \"aggregate\": \"count\", \"type\": \"quantitative\" } }"),
        ["sample-beyond-int"] = Chart("\"transform\": [ { \"sample\": 9.3e18 } ], \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" } }"),
        ["density-steps-beyond-int"] = Chart("\"transform\": [ { \"density\": \"x\", \"steps\": 4294967296 } ], \"mark\": \"line\", \"encoding\": { \"x\": { \"field\": \"value\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"density\", \"type\": \"quantitative\" } }"),
        ["regression-order-beyond-int"] = Chart("\"transform\": [ { \"regression\": \"y\", \"on\": \"x\", \"method\": \"poly\", \"order\": 4294967296 } ], \"mark\": \"line\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" } }"),
        ["loess-bandwidth-denormal"] = Chart("\"transform\": [ { \"loess\": \"y\", \"on\": \"x\", \"bandwidth\": 5e-324 } ], \"mark\": \"line\", \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }"),
        ["size-beyond-int"] = Chart("\"mark\": { \"type\": \"point\", \"size\": 1e300 }, \"encoding\": { \"x\": { \"field\": \"x\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"y\", \"type\": \"quantitative\" } }"),
    };

    /// <summary>
    ///     Builds a specification with a few made-up rows and the given members.
    /// </summary>
    /// <param name="members">The members, as JSON text.</param>
    /// <returns>The specification.</returns>
    private static string Chart(string members) =>
        "{ \"data\": { \"values\": [ { \"x\": 1, \"y\": 2, \"u\": \"a\", \"k\": \"p\" }, { \"x\": 2, \"y\": 4, \"u\": \"b\", \"k\": \"q\" }, { \"x\": 3, \"y\": 3, \"u\": \"a\", \"k\": \"q\" }, { \"x\": 4, \"y\": 9, \"u\": \"b\", \"k\": \"p\" } ] }, " + members + " }";

    /// <summary>
    ///     Renders a specification with the default options at its natural size.
    /// </summary>
    /// <param name="spec">The specification.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    private static Surface Render(string spec) => VegaLiteDocument.Parse(spec).Render(new VegaLiteRenderOptions(), Ct);

    /// <summary>
    ///     Renders a specification and hashes its pixels.
    /// </summary>
    /// <param name="spec">The specification.</param>
    /// <param name="datasets">The named datasets, or <see langword="null"/>.</param>
    /// <param name="parameters">The parameter values, or <see langword="null"/>.</param>
    /// <returns>The hash.</returns>
    private static string RenderHash(string spec, IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? datasets = null, IReadOnlyDictionary<string, object?>? parameters = null)
    {
        using var surface = VegaLiteDocument.Parse(spec).Render(new VegaLiteRenderOptions { Datasets = datasets, Parameters = parameters }, Ct);
        return VegaLiteFuzzHarness.PixelHash(surface);
    }

    /// <summary>
    ///     Makes a row of a dataset with its fields in the order given.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The row.</returns>
    private static Dictionary<string, object?> Row(params (string Name, double Value)[] fields)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in fields)
        {
            row[name] = value;
        }

        return row;
    }

    /// <summary>
    ///     Makes the datasets of the render options, inserted in the order given.
    /// </summary>
    /// <param name="datasets">The names and rows.</param>
    /// <returns>The datasets.</returns>
    private static Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> Datasets(params (string Name, Dictionary<string, object?>[] Rows)[] datasets)
    {
        var result = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var (name, rows) in datasets)
        {
            result[name] = rows;
        }

        return result;
    }

    /// <summary>
    ///     Makes the parameter overrides of the render options, inserted in the order given.
    /// </summary>
    /// <param name="values">The names and values.</param>
    /// <returns>The overrides.</returns>
    private static Dictionary<string, object?> Parameters(params (string Name, double Value)[] values)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            result[name] = value;
        }

        return result;
    }

    /// <summary>
    ///     Compares two pictures of the same size the way the <c>compare</c> command measures them for the tolerance of the committed images.
    /// </summary>
    /// <param name="expected">The committed picture.</param>
    /// <param name="actual">The rendered picture.</param>
    /// <returns>The root-mean-square difference of the color levels (red, green and blue) and the share of pixels that differ by more than <see cref="LevelThreshold"/> levels in any of them.</returns>
    private static (double Rmse, double Ratio) Compare(Surface expected, Surface actual)
    {
        double sumSquares = 0;
        long different = 0;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var (e, a) = (expected[x, y], actual[x, y]);
                int dr = e.R - a.R, dg = e.G - a.G, db = e.B - a.B;
                sumSquares += (dr * dr) + (dg * dg) + (db * db);
                if (Math.Max(Math.Abs(dr), Math.Max(Math.Abs(dg), Math.Abs(db))) > LevelThreshold)
                {
                    different++;
                }
            }
        }

        var pixels = (double)expected.Width * expected.Height;
        return (Math.Sqrt(sumSquares / (pixels * 3)), different / pixels);
    }
}
