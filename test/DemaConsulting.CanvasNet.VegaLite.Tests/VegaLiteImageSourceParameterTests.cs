// cspell:ignore vega
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Tests of the <c>param.</c> fragment parameters of <see cref="VegaLiteImageSource"/>:
///     <c>chart.vl.json#param.cutoff=4</c> sets a parameter of the chart exactly as <c>--param cutoff=4</c> does, a selection
///     takes a JSON value, a name the chart does not declare is a warning, a value that is not usable refuses the link, and the
///     source declares the prefix to the resolver.
/// </summary>
public class VegaLiteImageSourceParameterTests
{
    /// <summary>
    ///     Gets a chart with a numeric parameter that filters the bars and an interval selection on y that colors the bars in it.
    /// </summary>
    /// <param name="cutoff">The default of the <c>cutoff</c> parameter.</param>
    /// <param name="brush">The JSON of the default value of the selection, or <see langword="null"/> for none.</param>
    /// <returns>The text of the chart.</returns>
    private static string Chart(int cutoff, string? brush = null)
    {
        var value = brush is null ? string.Empty : $",\"value\":{brush}";
        return "{\"params\":[{\"name\":\"cutoff\",\"value\":" + cutoff + "},{\"name\":\"brush\",\"select\":{\"type\":\"interval\",\"encodings\":[\"y\"]}" + value + "}],"
               + "\"data\":{\"values\":[{\"x\":1,\"y\":3},{\"x\":2,\"y\":5},{\"x\":3,\"y\":2},{\"x\":4,\"y\":4},{\"x\":5,\"y\":1}]},"
               + "\"transform\":[{\"filter\":\"datum.x >= cutoff\"}],\"width\":{\"step\":20},\"height\":80,\"mark\":\"bar\","
               + "\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"ordinal\"},\"y\":{\"field\":\"y\",\"type\":\"quantitative\"},"
               + "\"color\":{\"condition\":{\"param\":\"brush\",\"value\":\"red\"},\"value\":\"steelblue\"}}}";
    }

    /// <summary>
    ///     Creates a request for a chart with a fragment.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="fragment">The fragment text, without the hash.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request, with no requested size (the natural size of the parameterized chart).</returns>
    private static ImageSourceRequest Request(string json, string fragment = "", long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "chart.vl.json" + (fragment.Length > 0 ? "#" + fragment : string.Empty),
            Fragment = ImageFragment.Parse(fragment),
            FileName = "chart.vl.json",
            Content = Encoding.UTF8.GetBytes(json),
            Budget = new ImageBudget(remaining)
        };
    }

    /// <summary>
    ///     Determines whether two surfaces have the same size and the same pixels.
    /// </summary>
    /// <param name="first">The first surface.</param>
    /// <param name="second">The second surface.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    private static bool SamePixels(Surface first, Surface second)
    {
        if (first.Width != second.Width || first.Height != second.Height)
        {
            return false;
        }

        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first[x, y] != second[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Test that the source declares the <c>param.</c> prefix and no parameter name, and the code of the warning.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_FragmentParameterPrefixes_DeclaresParam()
    {
        // Arrange
        var source = (IFragmentImageSource)new VegaLiteImageSource();

        // Act / Assert
        Assert.Equal(["param."], source.FragmentParameterPrefixes);
        Assert.Equal("param.", VegaLiteImageSource.ParameterPrefix);
        Assert.Empty(source.FragmentParameters);
        Assert.Equal("vl-param-unknown", VegaLiteImageSource.UnknownParameterCode);
    }

    /// <summary>
    ///     Test that <c>param.cutoff=4</c> gives the chart the value, so the picture, and its natural size (fewer bars, a
    ///     narrower plot), are the ones of the chart that has that default.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_ParamFragment_SetsTheParameterAndTheLayout()
    {
        // Arrange
        var source = new VegaLiteImageSource();

        // Act
        using var overridden = source.Render(Request(Chart(1), "param.cutoff=4")).Surface;
        using var expected = source.Render(Request(Chart(4))).Surface;
        using var plain = source.Render(Request(Chart(1))).Surface;
        using var last = source.Render(Request(Chart(1), "param.cutoff=2&param.cutoff=4")).Surface;

        // Assert
        Assert.True(SamePixels(overridden, expected));
        Assert.True(SamePixels(last, expected));
        Assert.False(SamePixels(overridden, plain));
        Assert.True(overridden.Width < plain.Width, "the natural size follows the parameterized layout");
    }

    /// <summary>
    ///     Test that a selection takes a JSON object (percent-encoded or not), and a JSON <c>null</c> clears it.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_ParamFragment_SelectionTakesJson()
    {
        // Arrange
        var source = new VegaLiteImageSource();
        const string brush = "{\"y\":[2,4]}";

        // Act
        using var encoded = source.Render(Request(Chart(1), "param.brush=%7B%22y%22%3A%5B2%2C4%5D%7D")).Surface;
        using var raw = source.Render(Request(Chart(1), "param.brush={\"y\":[2,4]}")).Surface;
        using var expected = source.Render(Request(Chart(1, brush))).Surface;
        using var plain = source.Render(Request(Chart(1))).Surface;
        using var cleared = source.Render(Request(Chart(1, brush), "param.brush=null")).Surface;

        // Assert
        Assert.True(SamePixels(encoded, expected));
        Assert.True(SamePixels(raw, expected));
        Assert.False(SamePixels(expected, plain));
        Assert.True(SamePixels(cleared, plain), "null clears the initial selection");
    }

    /// <summary>
    ///     Test that a name the chart does not declare is the <c>vl-param-unknown</c> warning, names the declared parameters,
    ///     and the chart is drawn as it is without the name, while a declared parameter in the same link still applies.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_UnknownParameter_IsAWarningAndIsIgnored()
    {
        // Arrange
        var source = new VegaLiteImageSource();

        // Act
        var result = source.Render(Request(Chart(1), "param.nope=1&param.cutoff=4"));
        using var drawn = result.Surface;
        using var expected = source.Render(Request(Chart(4))).Surface;
        var onlyUnknown = source.Render(Request(Chart(1), "param.nope=1"));
        using var plain = onlyUnknown.Surface;
        using var reference = source.Render(Request(Chart(1))).Surface;

        // Assert
        var note = Assert.Single(result.Diagnostics);
        Assert.Equal(("vega-lite", ImageDiagnosticSeverity.Warning, "vl-param-unknown"), (note.Source, note.Severity, note.Code));
        Assert.Contains("'param.nope'", note.Message, StringComparison.Ordinal);
        Assert.Contains("declared: brush, cutoff", note.Message, StringComparison.Ordinal);
        Assert.True(SamePixels(drawn, expected));
        Assert.Equal("vl-param-unknown", Assert.Single(onlyUnknown.Diagnostics).Code);
        Assert.True(SamePixels(plain, reference));
    }

    /// <summary>
    ///     Test that a chart that declares no parameter says so in the warning.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_ChartWithoutParameters_SaysItDeclaresNone()
    {
        // Arrange
        const string bars = "{\"data\": {\"values\": [{\"a\": \"A\", \"b\": 3}]}, \"mark\": \"bar\", \"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"nominal\"}, \"y\": {\"field\": \"b\", \"type\": \"quantitative\"}}}";

        // Act
        var result = new VegaLiteImageSource().Render(Request(bars, "param.x=1"));
        result.Surface.Dispose();

        // Assert
        var note = Assert.Single(result.Diagnostics);
        Assert.Contains("the chart declares none", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a value that is not valid JSON, or that the library does not accept, refuses the link with the
    ///     invalid-fragment code before anything is charged.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("param.brush={x")]
    [InlineData("param.brush=[1,")]
    [InlineData("param.cutoff=%22abc")]
    public void VegaLiteImageSource_Render_InvalidParameter_IsRefusedBeforeCharging(string fragment)
    {
        // Arrange
        var request = Request(Chart(1), fragment, 1_000_000);

        // Act
        var ex = Assert.Throws<ImageSourceRefusedException>(() => new VegaLiteImageSource().Render(request));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
        Assert.Contains("'param.", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1_000_000L, request.Budget!.Remaining);
    }

    /// <summary>
    ///     Test that a structured value that the library rejects (nested beyond its limit, within the limit of the grammar) refuses the link.
    /// </summary>
    [Fact]
    public void VegaLiteImageSource_Render_ValueTheLibraryRejects_IsRefused()
    {
        // Arrange: 33 nested arrays are beyond the 32 levels of the grammar and of the library
        var deep = new string('[', 33) + new string(']', 33);

        // Act
        var ex = Assert.Throws<ImageSourceRefusedException>(() => new VegaLiteImageSource().Render(Request(Chart(1), "param.brush=" + deep)));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
    }

    /// <summary>
    ///     Test through the resolver that a <c>param.</c> link is drawn without an unknown-parameter warning, and that a
    ///     <c>def.</c> parameter on a chart is reported as unknown.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ChartParamLink_IsUnderstoodAndOthersAreUnknown()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "canvasnet-vl-param-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "chart.vl.json"), Chart(1));
            var resolver = new FolderImageResolver(directory, [new VegaLiteImageSource()]);

            // Act
            using var parameterized = resolver.Resolve("chart.vl.json#param.cutoff=4", 120, 100);
            using var ignored = resolver.Resolve("chart.vl.json#param.cutoff=4&def.width=1", 120, 100);

            // Assert
            Assert.NotNull(parameterized);
            Assert.NotNull(ignored);
            var note = Assert.Single(resolver.Diagnostics);
            Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, note.Code);
            Assert.Contains("'def.width'", note.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
