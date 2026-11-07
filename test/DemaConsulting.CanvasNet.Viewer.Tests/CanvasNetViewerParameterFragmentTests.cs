// cspell:ignore href hrefs scad openscad vega
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     End-to-end tests of the prefixed parameters of the fragment of a link and of the matching option of the Viewer API:
///     <c>part.scad#def.width=20</c> sets a variable of a model (like <c>-D</c>), <c>chart.vl.json#param.cutoff=4</c> sets a
///     parameter of a chart (like <c>--param</c>), and <see cref="ViewerRenderOptions.VegaLite"/> sets the same parameters of a
///     chart that is rendered directly. An override is proved by drawing the same picture from a file whose own text has
///     the value, and comparing the pixels.
/// </summary>
public class CanvasNetViewerParameterFragmentTests
{
    /// <summary>The color of the backdrop of the document that embeds the pictures.</summary>
    private static readonly Rgba32 Backdrop = new(128, 128, 128, 255);

    /// <summary>A model whose width is a variable: a box of 5 by 5 by 5.</summary>
    private const string Box = "width = 5; cube([width, 5, 5], center = true);";

    /// <summary>The same model with the width written into the file.</summary>
    private const string WideBox = "width = 20; cube([width, 5, 5], center = true);";

    /// <summary>A model that draws a cube when <c>label</c> is "A" and a sphere otherwise.</summary>
    private const string LabelModel = "label = \"x\"; if (label == \"A\") { cube(10, center = true); } else { sphere(4, $fn = 16); }";

    /// <summary>A model that draws a cube when <c>show</c> is true.</summary>
    private const string ShowModel = "show = false; if (show) { cube(10, center = true); } else { sphere(4, $fn = 16); }";

    /// <summary>
    ///     Gets a chart with a numeric parameter <paramref name="cutoff"/> that filters the bars and an interval
    ///     selection <c>brush</c> that colors the bars inside it, laid out with a step so the width follows the number of bars.
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
    ///     Creates a folder with the models and charts of the tests beside the diagram.
    /// </summary>
    /// <returns>The folder; the caller disposes it.</returns>
    private static ViewerTestFolder CreateFolder()
    {
        var folder = new ViewerTestFolder();
        folder.WriteText("part.scad", Box);
        folder.WriteText("wide.scad", WideBox);
        folder.WriteText("label.scad", LabelModel);
        folder.WriteText("label-a.scad", LabelModel.Replace("label = \"x\"", "label = \"A\"", StringComparison.Ordinal));
        folder.WriteText("show.scad", ShowModel);
        folder.WriteText("show-true.scad", ShowModel.Replace("show = false", "show = true", StringComparison.Ordinal));
        folder.WriteText("chart.vl.json", Chart(1));
        folder.WriteText("chart-4.vl.json", Chart(4));
        folder.WriteText("chart-brush.vl.json", Chart(1, "{\"y\":[2,4]}"));
        return folder;
    }

    /// <summary>
    ///     Writes a diagram with the given cells and renders it with the viewer.
    /// </summary>
    /// <param name="folder">The folder of the diagram.</param>
    /// <param name="hrefs">The links, one per cell, from the left.</param>
    /// <returns>The picture; the caller disposes it.</returns>
    private static ViewerImage Render(ViewerTestFolder folder, params string[] hrefs)
    {
        // An ampersand in an attribute is written as an entity in XML; the parser gives the link its plain "&" back
        var svg = ViewerFixtures.Svg(string.Concat(hrefs.Select((href, i) => ViewerFixtures.Cell(href.Replace("&", "&amp;", StringComparison.Ordinal), i))));
        var path = folder.WriteText("diagram.svg", svg);
        return CanvasNetViewer.Render(path);
    }

    /// <summary>
    ///     Determines whether two cells of a rendered diagram have the same pixels.
    /// </summary>
    /// <param name="surface">The picture.</param>
    /// <param name="first">The first cell, 0 to 4.</param>
    /// <param name="second">The second cell, 0 to 4.</param>
    /// <returns><see langword="true"/> when every pixel of the first cell equals the one of the second.</returns>
    private static bool SameCell(Surface surface, int first, int second)
    {
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                if (surface[first * 100 + x, y] != surface[second * 100 + x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Test that <c>def.width=20</c> overrides the top-level assignment of the model exactly as the same text in the
    ///     file would, that the name of the parameter is case-insensitive in its prefix only, and that nothing is reported.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadDefineFragment_OverridesTheVariable()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act: the override, the model written with that value, the model as it is, an override of another variable, and the prefix in other case
        using var image = Render(
            folder,
            "part.scad#view=front&def.width=20",
            "wide.scad#view=front",
            "part.scad#view=front",
            "part.scad#view=front&def.WIDTH=20",
            "part.scad#view=front&DEF.width=20");

        // Assert
        var surface = image.Surface;
        Assert.True(SameCell(surface, 0, 1));
        Assert.False(SameCell(surface, 0, 2));
        Assert.True(SameCell(surface, 2, 3), "def.WIDTH is another variable than width, so the picture is the default one");
        Assert.True(SameCell(surface, 0, 4));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a def.* parameter works without <c>view=</c> (the library chooses the view), is applied with the
    ///     precedence of <c>-D</c> (the parameter wins over the assignment of the file), and with the last value of a name
    ///     given twice (with the duplicate warning).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadDefineFragment_WithoutViewAndDuplicates_LastWins()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "part.scad#def.width=20", "wide.scad", "part.scad#def.width=5&def.width=20", "part.scad#def.width=20&def.width=20");

        // Assert
        var surface = image.Surface;
        Assert.True(SameCell(surface, 0, 1));
        Assert.True(SameCell(surface, 0, 2));
        Assert.True(SameCell(surface, 0, 3));
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentDuplicateParameterCode, note.Code);
        Assert.Contains("'def.width'", note.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a quoted value (percent-encoded), an unquoted text and the booleans give the same variables as the
    ///     same assignments in the file, and a name that differs only in case is another variable and not a duplicate.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadDefineFragment_TextAndBooleanValues()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act: %22 is a double quote; the third and fourth give the text A without quotes and with single quotes; the last is the default sphere
        using var image = Render(
            folder,
            "label.scad#view=front&def.label=%22A%22",
            "label-a.scad#view=front",
            "label.scad#view=front&def.label=A",
            "label.scad#view=front&def.label=%27A%27",
            "label.scad#view=front");
        using var booleans = Render(folder, "show-true.scad#view=front", "show.scad#view=front&def.show=true", "show.scad#view=front");

        // Assert
        var surface = image.Surface;
        Assert.True(SameCell(surface, 0, 1));
        Assert.True(SameCell(surface, 0, 2));
        Assert.True(SameCell(surface, 0, 3));
        Assert.False(SameCell(surface, 0, 4), "a sphere is not a cube");
        Assert.Empty(image.Diagnostics);
        Assert.True(SameCell(booleans.Surface, 0, 1));
        Assert.False(SameCell(booleans.Surface, 0, 2));
        Assert.Empty(booleans.Diagnostics);
    }

    /// <summary>
    ///     Test that an encoded <c>&amp;</c> and <c>=</c> stay inside a value: the text is decoded after the split.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ScadDefineFragment_EncodedSeparators_StayInTheValue()
    {
        // Arrange: label is "A&B=C", which is not "A", so the model draws the sphere like the default
        using var folder = CreateFolder();

        // Act: %26 is an ampersand and %3D is an equals sign
        using var image = Render(folder, "label.scad#view=front&def.label=A%26B%3DC", "label.scad#view=front");

        // Assert: one parameter (no unknown 'B=C' warning) and the default picture
        Assert.True(SameCell(image.Surface, 0, 1));
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a value or a name the library does not accept refuses the link with the invalid-fragment code and
    ///     nothing is drawn.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("def.width=[1,2]")]
    [InlineData("def.1x=3")]
    [InlineData("def.a%20b=3")]
    public void CanvasNetViewer_Render_ScadDefineFragment_Invalid_IsAnInvalidFragmentNote(string fragment)
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "part.scad#" + fragment);

        // Assert
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
        Assert.Contains("'def.", note.Message, StringComparison.Ordinal);
        Assert.Equal(Backdrop, image.Surface[50, 50]);
        Assert.Empty(image.Embedded);
    }

    /// <summary>
    ///     Test that <c>param.cutoff=4</c> sets the parameter of a chart exactly as the same value in the file would, and
    ///     that the chart is laid out from the parameterized values (fewer bars, so a narrower plot).
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ChartParamFragment_SetsTheParameter()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "chart.vl.json#param.cutoff=4", "chart-4.vl.json", "chart.vl.json", "chart.vl.json#param.cutoff=1&param.cutoff=4");

        // Assert
        var surface = image.Surface;
        Assert.True(SameCell(surface, 0, 1));
        Assert.False(SameCell(surface, 0, 2));
        Assert.True(SameCell(surface, 0, 3));
        Assert.Equal(["chart.vl.json#param.cutoff=4", "chart-4.vl.json", "chart.vl.json", "chart.vl.json#param.cutoff=1&param.cutoff=4"], image.Embedded.Select(e => e.Href));
        Assert.Equal(FolderImageResolver.FragmentDuplicateParameterCode, Assert.Single(image.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a selection parameter takes a JSON object in the shape of its initial state (percent-encoded in the
    ///     link), and that the picture equals the chart whose own value is that object.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ChartParamFragment_SelectionInterval_TakesAJsonObject()
    {
        // Arrange
        using var folder = CreateFolder();

        // Act: {"y":[2,4]} with the brackets, quotes, colon and comma encoded, and with the quotes only
        using var image = Render(
            folder,
            "chart.vl.json#param.brush=%7B%22y%22%3A%5B2%2C4%5D%7D",
            "chart-brush.vl.json",
            "chart.vl.json#param.brush={\"y\":[2,4]}",
            "chart.vl.json");

        // Assert
        var surface = image.Surface;
        Assert.True(SameCell(surface, 0, 1));
        Assert.True(SameCell(surface, 0, 2));
        Assert.False(SameCell(surface, 0, 3), "the brush colors the selected bars");
        Assert.Empty(image.Diagnostics);
    }

    /// <summary>
    ///     Test that a parameter the chart does not declare is the warning <c>vl-param-unknown</c> and the chart is drawn as
    ///     it would be without it, that the declared ones in the same link still apply, and that <c>Strict</c> does not turn it
    ///     into an error.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_ChartParamFragment_UnknownName_IsAWarningAndTheChartIsDrawn()
    {
        // Arrange
        using var folder = CreateFolder();
        var strictPath = folder.WriteText("strict.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("chart.vl.json#param.nope=1&amp;param.cutoff=4", 0)));

        // Act
        using var image = Render(folder, "chart.vl.json#param.nope=1&param.cutoff=4", "chart-4.vl.json", "chart.vl.json#param.nope=1", "chart.vl.json");
        using var strict = CanvasNetViewer.Render(strictPath, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true });

        // Assert
        Assert.True(SameCell(image.Surface, 0, 1));
        Assert.True(SameCell(image.Surface, 2, 3));
        Assert.Equal(["vl-param-unknown", "vl-param-unknown"], image.Diagnostics.Select(d => d.Code));
        Assert.All(image.Diagnostics, d => Assert.Equal(ImageDiagnosticSeverity.Warning, d.Severity));
        Assert.Contains("'param.nope'", image.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Contains("declared: brush, cutoff", image.Diagnostics[0].Message, StringComparison.Ordinal);
        Assert.Equal("vl-param-unknown", Assert.Single(strict.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a chart value that is not valid JSON refuses the link with the invalid-fragment code and nothing is drawn.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("param.brush={x")]
    [InlineData("param.brush=[1,")]
    [InlineData("param.cutoff=%22abc")]
    public void CanvasNetViewer_Render_ChartParamFragment_InvalidJson_IsAnInvalidFragmentNote(string fragment)
    {
        // Arrange
        using var folder = CreateFolder();

        // Act
        using var image = Render(folder, "chart.vl.json#" + fragment);

        // Assert
        var note = Assert.Single(image.Diagnostics);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, note.Code);
        Assert.Equal(ImageDiagnosticSeverity.Unsupported, note.Severity);
        Assert.Contains("'param.", note.Message, StringComparison.Ordinal);
        Assert.Equal(Backdrop, image.Surface[50, 50]);
        Assert.Empty(image.Embedded);
    }

    /// <summary>
    ///     Test that <c>def.</c> on a chart or a raster and <c>param.</c> on a model are the unknown-parameter warning of
    ///     the resolver (the picture is drawn without it), as is a prefix with nothing after it.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_PrefixOnTheWrongSource_IsAnUnknownParameterWarning()
    {
        // Arrange
        using var folder = CreateFolder();
        folder.WriteBytes("logo.png", ViewerFixtures.Png(4, 4, ViewerFixtures.Green));

        // Act
        using var image = Render(folder, "chart.vl.json#def.width=20", "part.scad#view=front&param.cutoff=4", "logo.png#def.a=1", "part.scad#def.=1");

        // Assert: each is drawn as it is without the parameter
        Assert.All(image.Diagnostics, d => Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, d.Code));
        Assert.All(image.Diagnostics, d => Assert.Equal(ImageDiagnosticSeverity.Warning, d.Severity));
        var warnings = image.Diagnostics.Select(d => d.Message).ToList();
        Assert.Equal(4, warnings.Count);
        Assert.Contains("'def.width'", warnings[0], StringComparison.Ordinal);
        Assert.Contains("'param.cutoff'", warnings[1], StringComparison.Ordinal);
        Assert.Contains("'def.a'", warnings[2], StringComparison.Ordinal);
        Assert.Contains("'def.'", warnings[3], StringComparison.Ordinal);
        Assert.Equal(ViewerFixtures.Green, image.Surface[250, 50]);
        Assert.Equal(4, image.Embedded.Count);
    }

    /// <summary>
    ///     Test that a link in a nested SVG file sets variables and parameters through the resolver of its own scope.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_NestedSvgWithDefineAndParamFragments_UsesTheScopedResolver()
    {
        // Arrange: outer.svg embeds inner.svg, which embeds the overridden model and chart
        using var folder = CreateFolder();
        folder.WriteText(
            "inner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'>"
            + "<image href='part.scad#view=front&amp;def.width=20' x='0' y='0' width='100' height='100'/>"
            + "<image href='chart.vl.json#param.cutoff=4' x='100' y='0' width='100' height='100'/></svg>");
        folder.WriteText(
            "reference.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'>"
            + "<image href='wide.scad#view=front' x='0' y='0' width='100' height='100'/>"
            + "<image href='chart-4.vl.json' x='100' y='0' width='100' height='100'/></svg>");
        var outer = folder.WriteText("outer.svg", ViewerFixtures.Svg("<image href='inner.svg' x='0' y='0' width='200' height='100'/>"));
        var reference = folder.WriteText("outer-reference.svg", ViewerFixtures.Svg("<image href='reference.svg' x='0' y='0' width='200' height='100'/>"));

        // Act
        using var image = CanvasNetViewer.Render(outer);
        using var expected = CanvasNetViewer.Render(reference);

        // Assert: the same pixels as the nested picture that has the values in its files
        Assert.Empty(image.Diagnostics);
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                Assert.Equal(expected.Surface[x, y], image.Surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Test that <see cref="ViewerRenderOptions.VegaLite"/> sets the parameters of a chart that is rendered directly:
    ///     the picture equals the one of the chart that has the value in its file, the natural size follows the
    ///     parameterized layout, and a selection takes the shape of its state.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_VegaLiteOptions_SetTheParametersAndTheLayout()
    {
        // Arrange
        using var folder = CreateFolder();
        var chart = Path.Combine(folder.Path, "chart.vl.json");
        var options = new ViewerRenderOptions
        {
            VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { ["cutoff"] = 4.0 } }
        };
        var brushed = new ViewerRenderOptions
        {
            VegaLite = new ChartOptions
            {
                Parameters = new Dictionary<string, object?> { ["brush"] = new Dictionary<string, object?> { ["y"] = new object[] { 2, 4 } } }
            }
        };

        // Act
        using var parameterized = CanvasNetViewer.Render(chart, options);
        using var reference = CanvasNetViewer.Render(Path.Combine(folder.Path, "chart-4.vl.json"));
        using var plain = CanvasNetViewer.Render(chart);
        using var withBrush = CanvasNetViewer.Render(chart, brushed);
        using var brushReference = CanvasNetViewer.Render(Path.Combine(folder.Path, "chart-brush.vl.json"));

        // Assert: the pixels are the reference's, the size is the narrower parameterized layout, and the selection is drawn
        Assert.Equal((reference.Surface.Width, reference.Surface.Height), (parameterized.Surface.Width, parameterized.Surface.Height));
        Assert.True(parameterized.Surface.Width < plain.Surface.Width);
        for (var y = 0; y < reference.Surface.Height; y++)
        {
            for (var x = 0; x < reference.Surface.Width; x++)
            {
                Assert.Equal(reference.Surface[x, y], parameterized.Surface[x, y]);
            }
        }

        Assert.Equal((brushReference.Surface.Width, brushReference.Surface.Height), (withBrush.Surface.Width, withBrush.Surface.Height));
        Assert.Equal(brushReference.Surface[brushReference.Surface.Width / 2, brushReference.Surface.Height / 2], withBrush.Surface[withBrush.Surface.Width / 2, withBrush.Surface.Height / 2]);
        Assert.Empty(parameterized.Diagnostics);
    }

    /// <summary>
    ///     Test that a direct render with a parameter the chart does not declare is a <c>vega-lite-error</c> that lists the
    ///     declared names (unlike an embedded link, which warns), and that a value the library rejects is refused before the file is read.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_VegaLiteOptions_UnknownAndInvalid_AreRefused()
    {
        // Arrange
        using var folder = CreateFolder();
        var chart = Path.Combine(folder.Path, "chart.vl.json");
        var unknown = new ViewerRenderOptions { VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { ["nope"] = 1.0 } } };
        var warn = new ViewerRenderOptions { VegaLite = new ChartOptions { WarnOnUnknownParameters = true, Parameters = new Dictionary<string, object?> { ["nope"] = 1.0 } } };
        var emptyName = new ViewerRenderOptions { VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { [string.Empty] = 1.0 } } };

        // Act
        var error = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(chart, unknown));
        using var warned = CanvasNetViewer.Render(chart, warn);
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => CanvasNetViewer.Render(chart, emptyName));

        // Assert
        Assert.Equal(ErrorCodes.VegaLiteError, error.Code);
        Assert.Contains("'nope'", error.Message, StringComparison.Ordinal);
        Assert.Contains("declared: brush, cutoff", error.Message, StringComparison.Ordinal);
        Assert.Equal("vl-param-unknown", Assert.Single(warned.Diagnostics).Code);
        Assert.Contains("A --param value is not valid", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the chart options are ignored by every other kind and are not inherited by an embedded chart.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_VegaLiteOptionsOnOtherKindsAndEmbeddedCharts_AreIgnored()
    {
        // Arrange
        using var folder = CreateFolder();
        var options = new ViewerRenderOptions { VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { ["cutoff"] = 4.0 } } };
        var svg = folder.WriteText("diagram.svg", ViewerFixtures.Svg(ViewerFixtures.Cell("chart.vl.json", 0) + ViewerFixtures.Cell("chart-4.vl.json", 1)));

        // Act
        using var scad = CanvasNetViewer.Render(Path.Combine(folder.Path, "part.scad"), options);
        using var plainScad = CanvasNetViewer.Render(Path.Combine(folder.Path, "part.scad"));
        using var diagram = CanvasNetViewer.Render(svg, options);

        // Assert: the model is the same, and the embedded chart keeps its own defaults (the first cell is not the fourth-cutoff chart)
        Assert.Equal((plainScad.Surface.Width, plainScad.Surface.Height), (scad.Surface.Width, scad.Surface.Height));
        Assert.False(SameCell(diagram.Surface, 0, 1));
        Assert.Empty(diagram.Diagnostics);
    }

    /// <summary>
    ///     Test that <see cref="SourceLoader"/> takes the chart options through <see cref="SourceOptions.VegaLite"/>, reports the
    ///     parameters in the render operation, and sizes the page from the parameterized layout.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_ChartOptions_AreAppliedAndReportedInTheOperation()
    {
        // Arrange
        using var folder = CreateFolder();
        var chart = Path.Combine(folder.Path, "chart.vl.json");
        var options = new SourceOptions
        {
            VegaLite = new ChartOptions
            {
                Parameters = new Dictionary<string, object?>
                {
                    ["cutoff"] = 4.0,
                    ["brush"] = new Dictionary<string, object?> { ["y"] = new object[] { 2, 4 } }
                }
            }
        };

        // Act
        using var loaded = SourceLoader.Load(chart, options);
        using var plain = SourceLoader.Load(chart, new SourceOptions());

        // Assert
        var render = Assert.Single(loaded.Operations, o => o.Op == "render");
        var parameters = Assert.IsAssignableFrom<IReadOnlyList<string>>(Assert.Single(render.Fields, f => f.Key == "params").Value);
        Assert.Equal(["brush={\"y\":[2,4]}", "cutoff=4"], parameters);
        Assert.True(loaded.Surface.Width < plain.Surface.Width);
        Assert.Equal(loaded.Surface.Width, loaded.Info.Width);
        Assert.DoesNotContain(plain.Operations.Single(o => o.Op == "render").Fields, f => f.Key == "params");
    }

    /// <summary>
    ///     Test that chart options the library rejects are a <c>usage-error</c> before the file is touched, for a missing file as
    ///     for an existing one, and that a null dictionary is a problem too.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_InvalidChartOptions_ThrowsUsageErrorBeforeTouchingTheFile()
    {
        // Arrange
        var emptyName = new SourceOptions { VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { [string.Empty] = 1.0 } } };
        var tooDeep = new SourceOptions { VegaLite = new ChartOptions { Parameters = new Dictionary<string, object?> { ["x"] = Enumerable.Range(0, 40).Aggregate((object)1, (inner, _) => new[] { inner }) } } };
        var nulls = new SourceOptions { VegaLite = new ChartOptions { Parameters = null! } };

        // Act
        var first = Assert.Throws<ViewerException>(() => SourceLoader.Load("missing.vl.json", emptyName));
        var second = Assert.Throws<ViewerException>(() => SourceLoader.Load("missing.vl.json", tooDeep));
        var third = Assert.Throws<ViewerException>(() => SourceLoader.Load("missing.vl.json", nulls));

        // Assert
        Assert.All(new[] { first, second, third }, e => Assert.Equal(ErrorCodes.UsageError, e.Code));
        Assert.Contains("A --param value is not valid", first.Message, StringComparison.Ordinal);
        Assert.Contains("A --param value is not valid", second.Message, StringComparison.Ordinal);
        Assert.Contains("chart parameters", third.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the sources declare the prefixes they understand, and the other sources none.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_CreateImageResolver_SourcesDeclareTheirPrefixes()
    {
        // Arrange
        using var folder = CreateFolder();
        var resolver = CanvasNetViewer.CreateImageResolver(folder.Path);

        // Act: a link that sets one prefixed parameter of each kind, resolved by the resolver of the viewer
        using var model = resolver.Resolve("part.scad#view=front&def.width=20", 100, 100);
        using var chart = resolver.Resolve("chart.vl.json#param.cutoff=4", 100, 100);

        // Assert
        Assert.NotNull(model);
        Assert.NotNull(chart);
        Assert.DoesNotContain(resolver.Diagnostics, d => d.Code == FolderImageResolver.FragmentUnknownParameterCode);
    }
}
