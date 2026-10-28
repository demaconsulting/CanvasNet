using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Tool.Imaging;
using DemaConsulting.CanvasNet.VegaLite;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the Vega-Lite source of the SourceLoader class: detection by suffix and schema, probing a chart,
///     drawing it, the diagnostics, strict mode, and the mapping of the library's failures.
/// </summary>
public class SourceLoaderVegaLiteTests
{
    /// <summary>
    ///     A chart of one arc mark, which the library does not draw (yet), with a property Vega-Lite does not define.
    /// </summary>
    private const string NoisyChart =
        """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "bogus": 4,
          "width": 100,
          "height": 60,
          "data": { "values": [ { "k": "a", "v": 3 } ] },
          "mark": "arc",
          "encoding": { "theta": { "field": "v", "type": "quantitative" } }
        }
        """;

    /// <summary>
    ///     Gets the color of a pixel as a packed value, comparable with <see cref="FixtureFactory.BarColor"/>.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel as 0xAARRGGBB.</returns>
    private static uint Rgb(Surface surface, int x, int y)
    {
        var pixel = surface[x, y];
        return ((uint)pixel.A << 24) | ((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B;
    }

    /// <summary>
    ///     Test that the suffixes <c>.vl.json</c> and <c>.vl</c> and a Vega-Lite schema in a <c>.json</c> file are
    ///     detected as a chart, whatever the text mentions, while other files keep their kind.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_VegaLiteSuffixesAndSchema_ReturnsVegaLite()
    {
        // Arrange: a chart under each name, one with an SVG start tag in its text, and a plain JSON file
        using var directory = new TempDirectory();
        var suffix = FixtureFactory.WriteText(directory.Path, "chart.vl.json", FixtureFactory.TwoBarChart);
        var upper = FixtureFactory.WriteText(directory.Path, "CHART.VL.JSON", "{}");
        var shortSuffix = FixtureFactory.WriteText(directory.Path, "chart.vl", "{}");
        var schema = FixtureFactory.WriteText(directory.Path, "chart.json", FixtureFactory.TwoBarChart);
        var tagged = FixtureFactory.WriteText(directory.Path, "tagged.vl.json", "{\"title\": \"<svg>\"}");
        var taggedSchema = FixtureFactory.WriteText(
            directory.Path,
            "tagged.json",
            "{\"title\": \"<svg>\", \"$schema\": \"https://vega.github.io/schema/vega-lite/v6.json\"}");

        // Act / Assert: a chart by its suffix (any case), by its extension, or by its schema; never an SVG
        foreach (var path in new[] { suffix, upper, shortSuffix, schema, tagged, taggedSchema })
        {
            Assert.Equal(SourceKind.VegaLite, SourceLoader.Detect(path));
        }

        // The content decides first: a PNG named like a chart is a PNG, and an SVG named .json has no schema
        var png = directory.Combine("fake.vl.json");
        File.WriteAllBytes(png, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Assert.Equal(SourceKind.Png, SourceLoader.Detect(png));
        Assert.Equal("vega-lite", SourceKind.VegaLite.ToFormatName());
        Assert.True(SourceKind.VegaLite.IsVector());
        Assert.False(SourceKind.VegaLite.IsModel());
        Assert.False(SourceKind.VegaLite.IsDocument());
    }

    /// <summary>
    ///     Test that a Vega specification and a JSON file that is not a Vega-Lite chart are unsupported formats, each with
    ///     a hint that says what is read instead.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_VegaSchema_IsUnsupported()
    {
        // Arrange: a plain Vega specification, a JSON file without a schema, a JSON file with another schema, and a
        // chart whose schema is beyond the first 4 KiB
        using var directory = new TempDirectory();
        var vega = FixtureFactory.WriteText(directory.Path, "plain.json", "{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}");
        var plain = FixtureFactory.WriteText(directory.Path, "data.json", "{\"values\": [1, 2, 3]}");
        var other = FixtureFactory.WriteText(directory.Path, "other.json", "{\"$schema\": \"https://example.org/schema.json\"}");
        var late = FixtureFactory.WriteText(
            directory.Path,
            "late.json",
            "{\"padding\": \"" + new string('x', 5000) + "\", \"$schema\": \"https://vega.github.io/schema/vega-lite/v5.json\"}");

        // Act
        var vegaError = Assert.Throws<ToolException>(() => SourceLoader.Detect(vega));
        var plainError = Assert.Throws<ToolException>(() => SourceLoader.Detect(plain));
        var otherError = Assert.Throws<ToolException>(() => SourceLoader.Detect(other));
        var lateError = Assert.Throws<ToolException>(() => SourceLoader.Detect(late));

        // Assert: all are unsupported formats; the Vega hint says Vega is not supported, the others say how to name a chart
        Assert.All([vegaError, plainError, otherError, lateError], e => Assert.Equal(ErrorCodes.UnsupportedFormat, e.Code));
        Assert.Contains("Vega specification", vegaError.Message);
        Assert.Contains("Vega is not supported", vegaError.Hint);
        Assert.Contains("not a Vega-Lite chart", plainError.Message);
        Assert.Contains(".vl.json", plainError.Hint);
        Assert.Contains(".vl.json", lateError.Hint);
        Assert.Contains("not a Vega-Lite chart", otherError.Message);
    }

    /// <summary>
    ///     Test that probing a chart reports its natural size, four channels, one page, and what the specification declares.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_VegaLite_ReportsChartFacts()
    {
        // Arrange: a chart with a title, a layer of two marks, a parameter, and inline and named data
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "facts.vl.json",
            """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
              "title": "Facts",
              "width": 100,
              "height": 50,
              "params": [ { "name": "cutoff", "value": 3 } ],
              "data": { "values": [ { "k": "a", "v": 3 }, { "k": "b", "v": 5 } ] },
              "layer": [
                { "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
                { "mark": { "type": "rule" }, "data": { "name": "limits" } }
              ]
            }
            """);

        // Act
        var info = SourceLoader.Probe(path);

        // Assert: the kind and the sizes, from the library's own measure
        Assert.Equal(SourceKind.VegaLite, info.Format);
        Assert.Equal(new FileInfo(path).Length, info.Bytes);
        Assert.Equal(4, info.Channels);
        Assert.True(info.HasAlpha);
        Assert.True(info.CanDecode);
        Assert.Equal((1, 1, 0), (info.FrameCount, info.PageCount, info.Pages.Count));
        var size = VegaLiteDocument.Open(path).Measure(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal((size.Width, size.Height), (info.Width, info.Height));
        Assert.True(info.Width > 100 && info.Height > 50);
        Assert.Empty(info.Modules);
        Assert.Empty(info.Directives);

        // The chart facts, in document order
        var chart = info.Chart!;
        Assert.Equal("Facts", chart.Title);
        Assert.Equal(5, chart.SchemaMajorVersion);
        Assert.Equal(["bar", "rule"], chart.Marks);
        Assert.Equal(["cutoff"], chart.Parameters);
        Assert.Equal(
            ["inline", "name"],
            chart.DataReferences.Select(r => r.Kind));
        Assert.Equal(2, chart.DataReferences[0].Rows);
        Assert.All(chart.DataReferences, r => Assert.Null(r.Url));
    }

    /// <summary>
    ///     Test that a chart is drawn at its natural size over white, with the default bar color at the places the
    ///     layout puts the bars, and that a size option fits the chart sharply.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLite_RendersInlineChartWithExpectedPixels()
    {
        // Arrange: a two-bar chart with a 200x100 data rectangle (see FixtureFactory.TwoBarChart)
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "bars.vl.json", FixtureFactory.TwoBarChart);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert: the natural size, a vector source of one page, no notes, and the render operation
        var surface = loaded.Surface;
        Assert.Equal((loaded.Info.Width, loaded.Info.Height), (surface.Width, surface.Height));
        Assert.True(loaded.IsVector);
        Assert.Equal(1, loaded.Page);
        Assert.Empty(loaded.Diagnostics);
        Assert.Empty(loaded.Echo);
        var operation = Assert.Single(loaded.Operations);
        Assert.Equal("render", operation.Op);
        Assert.Equal(["width", "height", "timeZone"], operation.Fields.Select(f => f.Key));

        // The padding is 5 pixels at the top and the right, the data rectangle is 200x100, and its two bands are 100
        // wide with a bar of 90 in each: the bars span 195 to 105 and 95 to 5 pixels left of the right edge of the
        // rectangle, 3 of the 5 units high for the first and all of it for the second
        var right = surface.Width - 5;
        const uint white = 0xFFFFFFFF;
        const uint opaque = 0xFF000000;
        Assert.Equal(white, Rgb(surface, 0, 0));
        Assert.Equal(FixtureFactory.BarColor | opaque, Rgb(surface, right - 50, 15));
        Assert.Equal(FixtureFactory.BarColor | opaque, Rgb(surface, right - 150, 80));
        Assert.Equal(white, Rgb(surface, right - 150, 15));
        Assert.Equal(white, Rgb(surface, right - 100, 80));

        // Twice the scale is a chart twice the size
        using var doubled = SourceLoader.Load(path, new SourceOptions { Scale = 2 });
        Assert.Equal((surface.Width * 2, surface.Height * 2), (doubled.Surface.Width, doubled.Surface.Height));

        // A background replaces the white and is reported
        using var dark = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(10, 20, 30, 255) });
        Assert.Equal(0xFF0A141Eu, Rgb(dark.Surface, 0, 0));
        Assert.Contains(("background", "#0A141E"), dark.Operations[0].Fields.Select(f => (f.Key, f.Value)));
    }

    /// <summary>
    ///     Test that the library's warnings and unsupported features are diagnostics, that strict mode turns the
    ///     unsupported feature into an error, and that the failures of the library are mapped to the stable codes.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLite_MapsDiagnosticsAndStrictErrors()
    {
        // Arrange: a chart with an unknown property and a mark the library does not draw
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "noisy.vl.json", NoisyChart);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());
        var strict = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert: the warning and the two unsupported features (the mark and its channel), in document order, each on one line with its location
        var warning = loaded.Diagnostics.Single(d => d.Severity == "warning");
        Assert.Equal(("vega-lite", "vega-lite-warning"), (warning.Source, warning.Code));
        Assert.Contains("vl-unknown-property", warning.Message);
        Assert.Contains("/bogus", warning.Message);
        Assert.Contains($"{path}:", warning.Message);
        var unsupportedNotes = loaded.Diagnostics.Where(d => d.Severity == "unsupported").ToList();
        Assert.Equal(["vl-mark-arc", "vl-channel-theta"], unsupportedNotes.Select(d => d.Code));
        var unsupported = unsupportedNotes[0];
        Assert.Equal(("vega-lite", "vl-mark-arc"), (unsupported.Source, unsupported.Code));
        Assert.Contains("/mark", unsupported.Message);
        Assert.Contains($"{path}:", unsupported.Message);
        Assert.All(loaded.Diagnostics, d => Assert.DoesNotContain('\n', d.Message));
        Assert.Null(unsupported.Page);

        // Strict: an unsupported-feature error that names the feature and the hint
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.Contains("vl-mark-arc", strict.Message);
        Assert.Contains("--strict", strict.Hint);
        Assert.IsType<VegaLiteUnsupportedFeatureException>(strict.InnerException);

        // A specification error is a vega-lite-error with the location, on one line
        var broken = FixtureFactory.WriteText(directory.Path, "broken.vl.json", "{\n  \"mark\": \n");
        var syntax = Assert.Throws<ToolException>(() => SourceLoader.Probe(broken));
        Assert.Equal(ErrorCodes.VegaLiteError, syntax.Code);
        Assert.StartsWith("Vega-Lite specification error: ", syntax.Message);
        Assert.Contains(broken, syntax.Message);
        Assert.DoesNotContain('\n', syntax.Message);
        Assert.IsType<VegaLiteSpecException>(syntax.InnerException);

        // Plain Vega is a specification error when the file is named like a chart
        var vega = FixtureFactory.WriteText(directory.Path, "vega.vl.json", "{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}");
        Assert.Equal(ErrorCodes.VegaLiteError, Assert.Throws<ToolException>(() => SourceLoader.Probe(vega)).Code);

        // A missing file is input-not-found, as for every other kind
        Assert.Equal(ErrorCodes.InputNotFound, Assert.Throws<ToolException>(() => SourceLoader.Probe(directory.Combine("gone.vl.json"))).Code);
    }

    /// <summary>
    ///     Test that the failures of the library's render are mapped to <c>vega-lite-error</c>, and to
    ///     <c>budget-exceeded</c> when they report a limit; the texts of the library are pinned here because the
    ///     tool tells the two apart by them.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VegaLite_MapsLimitFailuresToBudgetExceeded()
    {
        // Arrange: a document and options whose limits the chart exceeds (rows, scene items)
        var document = VegaLiteDocument.Parse(FixtureFactory.TwoBarChart);
        var rows = Assert.Throws<VegaLiteRenderException>(
            () => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxDataRows = 1 } }, TestContext.Current.CancellationToken));
        var items = Assert.Throws<VegaLiteRenderException>(
            () => document.Render(new VegaLiteRenderOptions { Limits = new VegaLiteLimits { MaxSceneItems = 2 } }, TestContext.Current.CancellationToken));
        var other = new VegaLiteRenderException("Data file 'x.csv' is missing.");

        // Act
        var mappedRows = SourceLoader.MapVegaLiteRender(rows);
        var mappedItems = SourceLoader.MapVegaLiteRender(items);
        var mappedOther = SourceLoader.MapVegaLiteRender(other);

        // Assert: a limit is a budget error with a hint and the library's exception as the cause; anything else is an error of the chart
        Assert.Equal(ErrorCodes.BudgetExceeded, mappedRows.Code);
        Assert.Equal(ErrorCodes.BudgetExceeded, mappedItems.Code);
        Assert.Contains("exceeds a limit", mappedRows.Message);
        Assert.NotNull(mappedRows.Hint);
        Assert.Same(rows, mappedRows.InnerException);
        Assert.Equal(ErrorCodes.VegaLiteError, mappedOther.Code);
        Assert.Equal("Vega-Lite render error: Data file 'x.csv' is missing.", mappedOther.Message);
        Assert.Null(mappedOther.Hint);
    }

    /// <summary>
    ///     Test that a chart that lays out larger than the maximum image size is a size-limit error, not an unexpected
    ///     exception.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_VegaLiteLargerThanMaximum_IsSizeLimit()
    {
        // Arrange: a chart whose data rectangle is wider than the maximum image size
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "wide.vl.json",
            "{\"width\": 9000, \"height\": 50, \"data\": {\"values\": [{\"k\": \"a\", \"v\": 1}]}, \"mark\": \"bar\", "
            + "\"encoding\": {\"x\": {\"field\": \"k\", \"type\": \"nominal\"}, \"y\": {\"field\": \"v\", \"type\": \"quantitative\"}}}");

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Probe(path));

        // Assert: size-limit, on one line, with a hint
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
        Assert.Contains(path, exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
        Assert.NotNull(exception.Hint);
    }

    /// <summary>
    ///     Test that a specification longer than the limit of the library is corrupt input, as it is for a SCAD model.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_VegaLiteSourceBeyondLimit_IsCorruptInput()
    {
        // Arrange: a chart with a string of more characters than the default source limit (4 MiB)
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "huge.vl.json",
            "{\"description\": \"" + new string('x', VegaLiteLimits.Default.MaxSourceLength + 10) + "\"}");

        // Act
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Probe(path));

        // Assert
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
        Assert.Contains("limit", exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
    }
}
