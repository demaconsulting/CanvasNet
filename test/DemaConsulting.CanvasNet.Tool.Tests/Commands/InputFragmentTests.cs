// cspell:ignore canvasnet scad vega
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the URL fragment of an input path of the command line (<c>part.scad#def.width=20</c>,
///     <c>report.pdf#page=2</c>): every command that takes an input reads it like the link of an SVG image, the fragment
///     overrides the options of the command for its input, a real file name with a <c>#</c> still works, the mistakes and
///     warnings have the codes of an SVG link, and the cells of a montage are labeled with it.
/// </summary>
[Collection("Sequential")]
public class InputFragmentTests
{
    /// <summary>
    ///     A model with an <c>explode</c> variable: three cubes, one above the other, that move apart as it grows. It sits
    ///     around the Z axis so a fixed camera on that axis frames every level.
    /// </summary>
    private const string Assembly =
        "explode = 0;\n"
        + "color(\"steelblue\") cube([20, 20, 10], center = true);\n"
        + "color(\"orange\") translate([0, 0, 15 + explode * 40]) cube([20, 20, 10], center = true);\n"
        + "color(\"seagreen\") translate([0, 0, 30 + explode * 80]) cube([20, 20, 10], center = true);\n";

    /// <summary>
    ///     A model with two variables, for the merging of the variables of the command and of the fragment.
    /// </summary>
    private const string TwoVariables = "width = 5; depth = 6; cube([width, depth, 5], center = true);";

    /// <summary>
    ///     A chart with a numeric parameter that filters its bars, so the size of the chart follows it.
    /// </summary>
    /// <param name="cutoff">The default of the <c>cutoff</c> parameter.</param>
    /// <returns>The text of the chart.</returns>
    private static string Chart(int cutoff)
    {
        return "{\"params\":[{\"name\":\"cutoff\",\"value\":" + cutoff + "}],"
               + "\"data\":{\"values\":[{\"x\":1,\"y\":3},{\"x\":2,\"y\":5},{\"x\":3,\"y\":2},{\"x\":4,\"y\":4},{\"x\":5,\"y\":1}]},"
               + "\"transform\":[{\"filter\":\"datum.x >= cutoff\"}],\"width\":{\"step\":20},\"height\":80,\"mark\":\"bar\","
               + "\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"ordinal\"},\"y\":{\"field\":\"y\",\"type\":\"quantitative\"}}}";
    }

    /// <summary>
    ///     Runs the tool with <c>--json</c> and asserts that it succeeds.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>The document and what was written to standard error; the caller disposes the document.</returns>
    private static (JsonDocument Document, string StdErr) RunOk(params string[] args)
    {
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke([.. args, "--json"]);
        Assert.True(exitCode == 0, stdOut + stdErr);
        return (JsonDocument.Parse(stdOut), stdErr);
    }

    /// <summary>
    ///     Runs the tool with <c>--json</c> and asserts that it fails.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>The code of the error.</returns>
    private static string RunFails(params string[] args)
    {
        var (exitCode, stdOut, _) = ToolHost.Invoke([.. args, "--json"]);
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        return document.RootElement.GetProperty("error").GetProperty("code").GetString()!;
    }

    /// <summary>
    ///     Finds the codes of the notes of a result.
    /// </summary>
    /// <param name="document">The result.</param>
    /// <returns>The code and severity of each diagnostic, in order.</returns>
    private static List<(string Code, string Severity)> Notes(JsonDocument document)
    {
        return [.. document.RootElement.GetProperty("diagnostics").EnumerateArray()
            .Select(d => (d.GetProperty("code").GetString()!, d.GetProperty("severity").GetString()!))];
    }

    /// <summary>
    ///     Gets the left, top, right, and bottom of the pixels in a region that are not white.
    /// </summary>
    /// <param name="surface">The picture.</param>
    /// <param name="left">The first column of the region.</param>
    /// <param name="width">The width of the region.</param>
    /// <param name="height">The height of the region, from the top.</param>
    /// <returns>The bounds, inclusive; the region must hold something that is not white.</returns>
    private static (int Left, int Top, int Right, int Bottom) Bounds(Surface surface, int left, int width, int height)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                var pixel = surface[x, y];
                if (pixel.R < 250 || pixel.G < 250 || pixel.B < 250)
                {
                    (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                }
            }
        }

        Assert.True(maxX >= 0, "The region is empty.");
        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    ///     Asserts that two pictures have the same size and the same pixels.
    /// </summary>
    /// <param name="expected">The expected picture.</param>
    /// <param name="actual">The actual picture.</param>
    private static void AssertSamePixels(Surface expected, Surface actual)
    {
        Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }

    /// <summary>
    ///     Test that a montage of three explode levels of one model, each chosen by the fragment of its input and drawn
    ///     from one fixed camera (<c>def.$vpd</c> and <c>def.$vpt</c>), has three different cells at one scale: the cubes
    ///     are as wide in every cell and the lowest one stays where it is, while the stack grows taller.
    /// </summary>
    [Fact]
    public void InputFragment_Montage_ExplodeLevelsWithFixedCamera_DifferButShareScale()
    {
        // Arrange: the model, and the camera that every level shares
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "assembly.scad", Assembly);
        const string camera = "def.$vpd=500&def.$vpt=[0,0,55]";
        var output = directory.Combine("fixed.png");

        // Act: three cells of 192x192 in one row, without spacing, padding, or labels
        var (document, _) = RunOk(
            "montage", $"{model}#def.explode=0&{camera}", $"{model}#def.explode=0.5&{camera}", $"{model}#def.explode=1&{camera}",
            "--cell", "192x192", "--columns", "3", "--spacing", "0", "--padding", "0", "-o", output);
        using var sheet = PngCodec.Load(output);

        // Assert: one row of three cells
        Assert.Equal((576, 192), (sheet.Width, sheet.Height));
        var cells = Enumerable.Range(0, 3).Select(i => Bounds(sheet, i * 192, 192, 192)).ToList();

        // Assert: the same scale - every cell has the same width and the lowest cube has the same bottom edge - and the
        // stack grows with the explode level
        Assert.Single(cells.Select(c => c.Right - c.Left).Distinct());
        Assert.Single(cells.Select(c => c.Bottom).Distinct());
        Assert.True(cells[0].Bottom - cells[0].Top < cells[1].Bottom - cells[1].Top);
        Assert.True(cells[1].Bottom - cells[1].Top < cells[2].Bottom - cells[2].Top);
        Assert.NotEqual(PixelsOf(sheet, 0), PixelsOf(sheet, 1));
        Assert.NotEqual(PixelsOf(sheet, 1), PixelsOf(sheet, 2));

        // Assert: the result lists the fragments of the inputs
        var listed = document.RootElement.GetProperty("cells");
        Assert.Equal(
            ["def.explode=0&" + camera, "def.explode=0.5&" + camera, "def.explode=1&" + camera],
            listed.EnumerateArray().Select(c => c.GetProperty("fragment").GetString()));
        Assert.All(listed.EnumerateArray(), c => Assert.Equal(model, c.GetProperty("input").GetString()));
        document.Dispose();
    }

    /// <summary>
    ///     Test that the same montage without a fixed camera fits each cell to its own geometry, so the cubes are
    ///     narrower in a more exploded cell: the fixed camera is what keeps the scale.
    /// </summary>
    [Fact]
    public void InputFragment_Montage_WithoutFixedCamera_FitsEachCell()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "assembly.scad", Assembly);
        var output = directory.Combine("fitted.png");

        // Act
        var (document, _) = RunOk(
            "montage", $"{model}#def.explode=0", $"{model}#def.explode=1",
            "--cell", "192x192", "--columns", "2", "--spacing", "0", "--padding", "0", "-o", output);
        document.Dispose();
        using var sheet = PngCodec.Load(output);

        // Assert: the exploded stack is fitted into the cell, so its cubes are narrower than those of the compact one
        var compact = Bounds(sheet, 0, 192, 192);
        var exploded = Bounds(sheet, 192, 192, 192);
        Assert.True(exploded.Right - exploded.Left < compact.Right - compact.Left);
    }

    /// <summary>
    ///     Test that the camera of the command (<c>-D $vpd</c> and <c>-D $vpt</c>) and the camera of the fragment draw the
    ///     same cells: the viewport variables are honored by both paths, a vector included.
    /// </summary>
    [Fact]
    public void InputFragment_Montage_CameraByCommandOrByFragment_DrawTheSameCells()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "assembly.scad", Assembly);
        var byFragment = directory.Combine("fragment.png");
        var byCommand = directory.Combine("command.png");
        string[] layout = ["--cell", "96x96", "--columns", "2", "-o"];

        // Act
        RunOk([
            "montage", $"{model}#def.explode=0&def.$vpd=500&def.$vpt=[0,0,55]", $"{model}#def.explode=1&def.$vpd=500&def.$vpt=[0,0,55]",
            .. layout, byFragment]).Document.Dispose();
        RunOk([
            "montage", $"{model}#def.explode=0", $"{model}#def.explode=1", "-D", "$vpd=500", "-D", "$vpt=[0,0,55]", .. layout, byCommand]).Document.Dispose();

        // Assert
        using var a = PngCodec.Load(byFragment);
        using var b = PngCodec.Load(byCommand);
        AssertSamePixels(a, b);
    }

    /// <summary>
    ///     Test that <c>-D $vpd</c> sets the scale of a model: doubling the distance halves the picture of an orthographic
    ///     view.
    /// </summary>
    [Fact]
    public void InputFragment_Render_VpdVariable_SetsTheScale()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", "cube([20, 20, 10], center = true);");

        // Act: a fragment and the option, at two distances
        RunOk("render", $"{model}#def.$vpd=200", "--size", "200x200", "-o", directory.Combine("near.png")).Document.Dispose();
        RunOk("render", model, "-D", "$vpd=400", "--size", "200x200", "-o", directory.Combine("far.png")).Document.Dispose();
        using var near = PngCodec.Load(directory.Combine("near.png"));
        using var far = PngCodec.Load(directory.Combine("far.png"));

        // Assert: the wider the camera, the smaller the cube (about half)
        var nearWidth = Bounds(near, 0, 200, 200);
        var farWidth = Bounds(far, 0, 200, 200);
        var ratio = (double)(nearWidth.Right - nearWidth.Left) / (farWidth.Right - farWidth.Left);
        Assert.InRange(ratio, 1.8, 2.2);
    }

    /// <summary>
    ///     Test that the labels of a montage show the fragment, so the cells of one file are told apart: the labels are in
    ///     the result and the strips under two cells of one file are drawn differently.
    /// </summary>
    [Fact]
    public void InputFragment_Montage_Labels_ShowTheFragment()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "assembly.scad", Assembly);
        var output = directory.Combine("labeled.png");

        // Act: the default cell is wide enough for the whole label
        var (document, _) = RunOk(
            "montage", $"{model}#def.explode=0", $"{model}#def.explode=1", "--labels", "--cell", "256x128", "--columns", "2",
            "--spacing", "0", "--padding", "0", "-o", output);

        // Assert: the label is the file name, a #, and the fragment
        var labels = document.RootElement.GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("label").GetString()).ToList();
        document.Dispose();
        Assert.Equal(["assembly.scad#def.explode=0", "assembly.scad#def.explode=1"], labels);

        // Assert: the strips below the cells hold text, and the two texts differ
        using var sheet = PngCodec.Load(output);
        var first = LabelPixels(sheet, 0);
        var second = LabelPixels(sheet, 1);
        Assert.Contains(first, p => p.R < 200);
        Assert.NotEqual(first, second);
    }

    /// <summary>
    ///     Test that the label of a document page named by the fragment shows the fragment and no second <c>p</c> page number.
    /// </summary>
    [Fact]
    public void InputFragment_Montage_DocumentPage_LabelHasNoRedundantPageNumber()
    {
        // Arrange: a two-page PDF
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act: one input with a page fragment, one without
        var (document, _) = RunOk("montage", $"{pdf}#page=2", pdf, "--labels", "-o", directory.Combine("sheet.png"));
        using var result = document;

        // Assert
        var cells = result.RootElement.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal(["source.pdf#page=2", "source.pdf p1"], cells.Select(c => c.GetProperty("label").GetString()));
        Assert.Equal([2, 1], cells.Select(c => c.GetProperty("page").GetInt32()));
    }

    /// <summary>
    ///     Test that <c>render</c> and a verb command draw the page the fragment names, that it overrides <c>--page</c>,
    ///     and that it makes <c>--all-pages</c> a single page.
    /// </summary>
    [Fact]
    public void InputFragment_RenderAndVerb_PageFragment_ChoosesThePage()
    {
        // Arrange: a two-page PDF of pages of different sizes
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        RunOk("render", pdf, "--page", "2", "-o", directory.Combine("option.png")).Document.Dispose();
        var (byFragment, _) = RunOk("render", $"{pdf}#page=2", "-o", directory.Combine("fragment.png"));
        RunOk("render", $"{pdf}#page=2", "--page", "1", "-o", directory.Combine("override.png")).Document.Dispose();
        var (all, _) = RunOk("render", $"{pdf}#page=2", "--all-pages", "-o", directory.Combine("single.png"));
        RunOk("render", pdf, "-o", directory.Combine("first.png")).Document.Dispose();
        RunOk("crop", $"{pdf}#page=2", "0,0,50%,100%", "-o", directory.Combine("crop.png")).Document.Dispose();
        RunOk("crop", pdf, "0,0,50%,100%", "--page", "2", "-o", directory.Combine("crop-option.png")).Document.Dispose();

        // Assert: the fragment draws what --page 2 draws, and that is not page 1
        using var option = PngCodec.Load(directory.Combine("option.png"));
        using var fragment = PngCodec.Load(directory.Combine("fragment.png"));
        using var overridden = PngCodec.Load(directory.Combine("override.png"));
        using var single = PngCodec.Load(directory.Combine("single.png"));
        using var first = PngCodec.Load(directory.Combine("first.png"));
        AssertSamePixels(option, fragment);
        AssertSamePixels(option, overridden);
        AssertSamePixels(option, single);
        Assert.NotEqual((first.Width, first.Height), (option.Width, option.Height));

        // Assert: the result names the page and the fragment, and one page was written for --all-pages
        var input = byFragment.RootElement.GetProperty("input");
        Assert.Equal((2, "page=2"), (input.GetProperty("page").GetInt32(), input.GetProperty("fragment").GetString()));
        Assert.Single(all.RootElement.GetProperty("outputs").EnumerateArray());
        Assert.Equal(2, all.RootElement.GetProperty("input").GetProperty("page").GetInt32());
        byFragment.Dispose();
        all.Dispose();

        // Assert: a verb takes it too
        using var crop = PngCodec.Load(directory.Combine("crop.png"));
        using var cropOption = PngCodec.Load(directory.Combine("crop-option.png"));
        AssertSamePixels(cropOption, crop);
    }

    /// <summary>
    ///     Test that the text result names the input with its fragment, and that <c>info</c> describes the page the
    ///     fragment names and reports the fragment.
    /// </summary>
    [Fact]
    public void InputFragment_TextResultAndInfo_NameTheFragment()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var output = directory.Combine("page.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", $"{pdf}#page=2", "-o", output);
        var (infoCode, infoOut, _) = ToolHost.Invoke("info", $"{pdf}#page=2", "--all-pages", "--json");

        // Assert: "from <input>#<fragment> page 2"
        Assert.Equal(0, exitCode);
        Assert.Contains($"from {pdf}#page=2 page 2", stdOut, StringComparison.Ordinal);
        Assert.Equal(0, infoCode);
        using var info = JsonDocument.Parse(infoOut);
        var input = info.RootElement.GetProperty("input");
        Assert.Equal((pdf, "page=2"), (input.GetProperty("path").GetString(), input.GetProperty("fragment").GetString()));
        var pages = info.RootElement.GetProperty("document").GetProperty("pages").EnumerateArray().ToList();
        Assert.Equal([2], pages.Select(p => p.GetProperty("page").GetInt32()));
    }

    /// <summary>
    ///     Test that <c>inspect</c> reads the fragment of its input.
    /// </summary>
    [Fact]
    public void InputFragment_Inspect_ReadsThePage()
    {
        // Arrange
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");

        // Act
        var (byFragment, _) = RunOk("inspect", "colors", $"{pdf}#page=2");
        var (byOption, _) = RunOk("inspect", "colors", pdf, "--page", "2");

        // Assert: the same image size and colors, and the page and the fragment in the input
        var input = byFragment.RootElement.GetProperty("input");
        Assert.Equal((2, "page=2"), (input.GetProperty("page").GetInt32(), input.GetProperty("fragment").GetString()));
        Assert.Equal(
            byOption.RootElement.GetProperty("image").GetProperty("width").GetInt32(),
            byFragment.RootElement.GetProperty("image").GetProperty("width").GetInt32());
        Assert.Equal(byOption.RootElement.GetProperty("colors").GetRawText(), byFragment.RootElement.GetProperty("colors").GetRawText());
        byFragment.Dispose();
        byOption.Dispose();
    }

    /// <summary>
    ///     Test that <c>compare</c> reads a fragment on each input: two views of one model differ, the same view is
    ///     identical, and the result lists the fragments.
    /// </summary>
    [Fact]
    public void InputFragment_Compare_ComparesTheViewsOfAModel()
    {
        // Arrange: an asymmetric model that looks different from the top and from the front
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", FixtureFactory.AsymmetricModel);

        // Act
        var (different, _) = RunOk("compare", $"{model}#view=top", $"{model}#view=front", "--resize-to", "64x64");
        var (same, _) = RunOk("compare", $"{model}#view=top", $"{model}#view=top&def.unused=1", "--resize-to", "64x64");

        // Assert
        Assert.False(different.RootElement.GetProperty("metrics").GetProperty("identical").GetBoolean());
        Assert.Equal("view=front", different.RootElement.GetProperty("b").GetProperty("fragment").GetString());
        Assert.Equal("view=top", different.RootElement.GetProperty("a").GetProperty("fragment").GetString());
        Assert.True(same.RootElement.GetProperty("metrics").GetProperty("identical").GetBoolean());
        different.Dispose();
        same.Dispose();
    }

    /// <summary>
    ///     Test that a fragment wins over the options of the command for its input: the variables are merged by name, the
    ///     view is replaced, and the parameters of a chart are merged by name.
    /// </summary>
    [Fact]
    public void InputFragment_OverridesTheCommandOptions_ForItsInput()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", TwoVariables);
        var chart = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        var reference = FixtureFactory.WriteText(directory.Path, "reference.vl.json", Chart(4));

        // Act: -D width=5 is replaced by the fragment and -D depth=9 is kept; --view front by the view of the fragment
        var (merged, _) = RunOk("render", $"{model}#def.width=20&view=top", "-D", "width=5", "-D", "depth=9", "--view", "front", "-o", directory.Combine("merged.png"));
        RunOk("render", model, "-D", "width=20", "-D", "depth=9", "--view", "top", "-o", directory.Combine("expected.png")).Document.Dispose();
        RunOk("render", $"{chart}#param.cutoff=4", "--param", "cutoff=1", "-o", directory.Combine("chart.png")).Document.Dispose();
        RunOk("render", reference, "-o", directory.Combine("chart-expected.png")).Document.Dispose();

        // Assert: the operation lists the merged variables and the view that was drawn
        var render = merged.RootElement.GetProperty("operations")[0];
        Assert.Equal(["depth=9", "width=20"], render.GetProperty("defines").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal("top", render.GetProperty("view").GetString());
        merged.Dispose();

        // Assert: the same pictures as the equivalent options
        using var actual = PngCodec.Load(directory.Combine("merged.png"));
        using var expected = PngCodec.Load(directory.Combine("expected.png"));
        AssertSamePixels(expected, actual);
        using var chartActual = PngCodec.Load(directory.Combine("chart.png"));
        using var chartExpected = PngCodec.Load(directory.Combine("chart-expected.png"));
        AssertSamePixels(chartExpected, chartActual);
    }

    /// <summary>
    ///     Test that a vector variable (<c>def.$vpt</c>) is accepted by a fragment and by <c>-D</c> and gives one picture, and
    ///     that the operation lists it as a vector.
    /// </summary>
    [Fact]
    public void InputFragment_VectorVariable_WorksByFragmentAndByOption()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Assembly);

        // Act
        var (byFragment, _) = RunOk("render", $"{model}#def.$vpd=400&def.$vpt=[0,0,40]", "--size", "100x100", "-o", directory.Combine("a.png"));
        RunOk("render", model, "-D", "$vpd=400", "-D", "$vpt=[0,0,40]", "--size", "100x100", "-o", directory.Combine("b.png")).Document.Dispose();

        // Assert
        using var a = PngCodec.Load(directory.Combine("a.png"));
        using var b = PngCodec.Load(directory.Combine("b.png"));
        AssertSamePixels(a, b);
        var defines = byFragment.RootElement.GetProperty("operations")[0].GetProperty("defines").EnumerateArray().Select(d => d.GetString()).ToList();
        Assert.Equal(["$vpd=400", "$vpt=[0, 0, 40]"], defines);
        byFragment.Dispose();
    }

    /// <summary>
    ///     Test that a chart takes <c>param.</c> in the fragment of its input, and that a name the chart does not declare is
    ///     the warning <c>vl-param-unknown</c> (also with <c>--strict</c>) while <c>--param</c> keeps failing for it.
    /// </summary>
    [Fact]
    public void InputFragment_Chart_ParamFragment_SetsParametersAndWarnsAboutUnknownNames()
    {
        // Arrange
        using var directory = new TempDirectory();
        var chart = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));

        // Act
        var (known, _) = RunOk("render", $"{chart}#param.cutoff=4", "-o", directory.Combine("known.png"));
        var (unknown, stdErr) = RunOk("render", $"{chart}#param.nope=1&param.cutoff=3", "--strict", "-o", directory.Combine("unknown.png"));
        var optionCode = RunFails("render", chart, "--param", "nope=1", "-o", directory.Combine("option.png"));

        // Assert
        Assert.Equal(["cutoff=4"], known.RootElement.GetProperty("operations")[0].GetProperty("params").EnumerateArray().Select(p => p.GetString()));
        Assert.Empty(Notes(known));
        Assert.Equal([("vl-param-unknown", "warning")], Notes(unknown));
        Assert.Equal(["cutoff=3"], unknown.RootElement.GetProperty("operations")[0].GetProperty("params").EnumerateArray().Select(p => p.GetString()));
        Assert.Contains("nope", stdErr + unknown.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("vega-lite-error", optionCode);
        known.Dispose();
        unknown.Dispose();
    }

    /// <summary>
    ///     Test that a parameter the file does not use, and a parameter given twice, are warnings with the codes of an SVG link
    ///     (never errors, not even with <c>--strict</c>) and that the last value of a repeated name wins.
    /// </summary>
    [Fact]
    public void InputFragment_UnknownAndDuplicateParameters_AreWarnings()
    {
        // Arrange: a raster that takes no parameter
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", TwoVariables);
        var png = CompareCommandTests.SaveSolid(directory, "logo.png", 8, 8, MontageCommandTests.Red);

        // Act
        var (unknown, _) = RunOk("render", $"{model}#pag=2", "--strict", "-o", directory.Combine("a.png"));
        var (duplicate, _) = RunOk("render", $"{model}#def.width=1&def.width=20", "-o", directory.Combine("b.png"));
        var (raster, stdErr) = RunOk("render", $"{png}#page=2", "-o", directory.Combine("c.png"));

        // Assert
        Assert.Equal([("image-fragment-unknown-parameter", "warning")], Notes(unknown));
        Assert.Equal([("image-fragment-duplicate-parameter", "warning")], Notes(duplicate));
        Assert.Equal([("image-fragment-unknown-parameter", "warning")], Notes(raster));
        Assert.Equal(["width=20"], duplicate.RootElement.GetProperty("operations")[0].GetProperty("defines").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(string.Empty, stdErr);
        unknown.Dispose();
        duplicate.Dispose();
        raster.Dispose();

        // Assert: without --json the warning is on standard error and the exit code is 0
        var (exitCode, _, text) = ToolHost.Invoke("render", $"{png}#page=2", "-o", directory.Combine("d.png"));
        Assert.Equal(0, exitCode);
        Assert.Contains("Warning: image-fragment-unknown-parameter:", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a real file whose name contains a <c>#</c> keeps working and wins over a file that has a fragment of that
    ///     name, that the longest existing file is the path, and that a <c>#</c> in a folder name is no problem.
    /// </summary>
    [Fact]
    public void InputFragment_RealFileWithHash_WinsAndLongestExistingFileIsThePath()
    {
        // Arrange: x.png (red) and a real file named x.png#1.png (blue); a model p#1.scad and a folder c#
        using var directory = new TempDirectory();
        CompareCommandTests.SaveSolid(directory, "x.png", 8, 8, MontageCommandTests.Red);
        CompareCommandTests.SaveSolid(directory, "x.png#1.png", 8, 8, MontageCommandTests.Blue);
        var hashed = FixtureFactory.WriteText(directory.Path, "p#1.scad", TwoVariables);
        var folder = FixtureFactory.WriteText(directory.Path, "c#/q.scad", TwoVariables);

        // Act
        var (literal, _) = RunOk("render", directory.Combine("x.png#1.png"), "-o", directory.Combine("literal.png"));
        var (viaFragment, _) = RunOk("render", directory.Combine("x.png#page=1"), "-o", directory.Combine("fragment.png"));
        var (hashedModel, _) = RunOk("render", $"{hashed}#def.width=9", "--size", "50x50", "-o", directory.Combine("hashed.png"));
        var (inFolder, _) = RunOk("render", $"{folder}#def.width=9", "--size", "50x50", "-o", directory.Combine("folder.png"));
        var (wholeName, _) = RunOk("render", hashed, "--size", "50x50", "-o", directory.Combine("whole.png"));

        // Assert: the literal file is the blue one with no fragment; the other name is the red file with a fragment
        using var literalPicture = PngCodec.Load(directory.Combine("literal.png"));
        using var fragmentPicture = PngCodec.Load(directory.Combine("fragment.png"));
        Assert.Equal(new Rgba32(0, 0, 255, 255), literalPicture[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), fragmentPicture[0, 0]);
        Assert.False(literal.RootElement.GetProperty("input").TryGetProperty("fragment", out _));
        Assert.Equal("page=1", viaFragment.RootElement.GetProperty("input").GetProperty("fragment").GetString());

        // Assert: the longest existing name is the file, so a fragment after p#1.scad is a fragment
        var hashedInput = hashedModel.RootElement.GetProperty("input");
        Assert.Equal((hashed, "def.width=9"), (hashedInput.GetProperty("path").GetString(), hashedInput.GetProperty("fragment").GetString()));
        Assert.Equal(["width=9"], hashedModel.RootElement.GetProperty("operations")[0].GetProperty("defines").EnumerateArray().Select(d => d.GetString()));
        Assert.Equal(folder, inFolder.RootElement.GetProperty("input").GetProperty("path").GetString());
        Assert.False(wholeName.RootElement.GetProperty("input").TryGetProperty("fragment", out _));
        literal.Dispose();
        viaFragment.Dispose();
        hashedModel.Dispose();
        inFolder.Dispose();
        wholeName.Dispose();
    }

    /// <summary>
    ///     Test that a missing file with a fragment is <c>input-not-found</c> and says that a <c>#</c> starts a fragment.
    /// </summary>
    [Fact]
    public void InputFragment_MissingFile_IsInputNotFoundWithAHint()
    {
        // Arrange
        using var directory = new TempDirectory();
        var missing = directory.Combine("nothing.scad") + "#view=top";

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", missing, "-o", directory.Combine("o.png"), "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal("input-not-found", error.GetProperty("code").GetString());
        Assert.Contains("a file named", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("fragment", error.GetProperty("hint").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a fragment that cannot be used is the error <c>image-fragment-invalid</c> in every command that takes an
    ///     input, and that a page the file does not have is <c>page-out-of-range</c>; nothing is written.
    /// </summary>
    [Theory]
    [InlineData("part.scad", "def.width=[1,2", "image-fragment-invalid")]
    [InlineData("part.scad", "def.width=[{\"a\":1}]", "image-fragment-invalid")]
    [InlineData("part.scad", "def.1x=3", "image-fragment-invalid")]
    [InlineData("part.scad", "view=sideways", "image-fragment-invalid")]
    [InlineData("part.scad", "view", "image-fragment-invalid")]
    [InlineData("chart.vl.json", "param.brush={x", "image-fragment-invalid")]
    [InlineData("report.pdf", "page=abc", "image-fragment-invalid")]
    [InlineData("report.pdf", "page=0", "image-fragment-invalid")]
    [InlineData("report.pdf", "page=9", "page-out-of-range")]
    public void InputFragment_EveryCommand_MalformedFragment_FailsWithTheCode(string name, string fragment, string code)
    {
        // Arrange: a model, a chart, and a PDF
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", TwoVariables);
        FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        var input = $"{directory.Combine(name)}#{fragment}";
        var output = directory.Combine("o.png");
        var other = directory.Combine("part.scad");

        // Act / Assert: each command fails with the code and writes no output
        Assert.Equal(code, RunFails("render", input, "-o", output));
        Assert.Equal(code, RunFails("crop", input, "0,0,50%,100%", "-o", output));
        Assert.Equal(code, RunFails("inspect", "colors", input));
        Assert.Equal(code, RunFails("info", input));
        Assert.Equal(code, RunFails("compare", input, other, "--resize-to", "32x32"));
        Assert.Equal(code, RunFails("montage", other, input, "-o", output));
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a fragment over its limits is <c>image-fragment-invalid</c>, as for a link.
    /// </summary>
    [Fact]
    public void InputFragment_OverTheLimits_IsInvalid()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", TwoVariables);
        var manyParameters = string.Join('&', Enumerable.Range(0, 33).Select(i => $"def.v{i}=1"));
        var tooLong = "def.label=" + new string('a', 2100);

        // Act / Assert
        Assert.Equal("image-fragment-invalid", RunFails("render", $"{model}#{manyParameters}", "-o", directory.Combine("a.png")));
        Assert.Equal("image-fragment-invalid", RunFails("render", $"{model}#{tooLong}", "-o", directory.Combine("b.png")));
    }

    /// <summary>
    ///     Test that a fragment is read as a link reads it: parameters are separated by <c>&amp;</c>, names are not case
    ///     sensitive, and the value is percent-decoded after the split.
    /// </summary>
    [Fact]
    public void InputFragment_Grammar_IsTheGrammarOfALink()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", "label = \"x\"; echo(label); cube(5);");

        // Act: an upper-case view name, a prefix in capitals, and an encoded & inside a quoted value
        var (document, _) = RunOk("render", $"{model}#VIEW=Top&DEF.label=%22a%26b%22", "--size", "40x40", "-o", directory.Combine("o.png"));

        // Assert: the model printed the decoded value
        Assert.Equal(["ECHO: \"a&b\""], document.RootElement.GetProperty("echo").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("top", document.RootElement.GetProperty("operations")[0].GetProperty("view").GetString());
        document.Dispose();
    }

    /// <summary>
    ///     Test that the help documents the fragment of an input path: the <c>input fragment</c> term of the <c>svg</c> topic
    ///     (the rule for the number sign, the quoting, the overrides, the codes), the fixed camera of the <c>scad</c> topic,
    ///     the error code in the <c>errors</c> topic, the page examples of the document topics, and the usage text of every
    ///     command that takes an input.
    /// </summary>
    [Fact]
    public void HelpTopics_InputFragments_AreDocumentedInTopicsAndUsages()
    {
        // Arrange
        static string TextOf(string name) => string.Join("\n", HelpTopics.Find(name)!.Sections.SelectMany(s => s.Items).Select(i => $"{i.Term} {i.Text}"));
        var term = Assert.Single(HelpTopics.Find("svg")!.Sections.First(s => s.Id == "image-links").Items, i => i.Term == "input fragment").Text;
        var registry = CommandRegistry.CreateDefault();

        // Assert: the rules of the svg topic
        foreach (var expected in new[]
                 {
                     "no file has that exact name", "Quote the input", "overrides --page, --view, -D, and --param",
                     "image-fragment-invalid", "page-out-of-range", "vl-param-unknown", "also with --strict", "Labels of montage"
                 })
        {
            Assert.Contains(expected, term, StringComparison.Ordinal);
        }

        // Assert: the other topics
        Assert.Contains("def.$vpd=300", TextOf("svg"), StringComparison.Ordinal);
        Assert.Contains("canvasnet montage", TextOf("scad"), StringComparison.Ordinal);
        Assert.Contains("'$vpd=300'", TextOf("scad"), StringComparison.Ordinal);
        Assert.Contains("image-fragment-invalid", TextOf("errors"), StringComparison.Ordinal);
        Assert.Contains("report.pdf#page=3", TextOf("pdf"), StringComparison.Ordinal);
        Assert.Contains("deck.pptx#slide=3", TextOf("pptx"), StringComparison.Ordinal);
        Assert.Contains("drawing.vsdx#page=2", TextOf("vsdx"), StringComparison.Ordinal);
        Assert.Contains("chart.vl.json#param.cutoff=50", TextOf("vega-lite"), StringComparison.Ordinal);

        // Assert: every command that takes an input says so in its usage
        foreach (var name in new[] { "render", "crop", "inspect", "compare", "montage", "info" })
        {
            Assert.Contains("fragment", registry.Find(name)!.Usage, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Gets the pixels of one cell of a sheet of 192-pixel cells in one row.
    /// </summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="index">The 0-based cell.</param>
    /// <returns>The colors of the cell, row by row.</returns>
    private static List<Rgba32> PixelsOf(Surface sheet, int index)
    {
        var pixels = new List<Rgba32>();
        for (var y = 0; y < 192; y++)
        {
            for (var x = index * 192; x < (index + 1) * 192; x++)
            {
                pixels.Add(sheet[x, y]);
            }
        }

        return pixels;
    }

    /// <summary>
    ///     Gets the pixels of the label strip below one cell of a sheet of 256-pixel cells that are 128 pixels high, in a row.
    /// </summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="index">The 0-based cell.</param>
    /// <returns>The colors of the strip, row by row.</returns>
    private static List<Rgba32> LabelPixels(Surface sheet, int index)
    {
        var pixels = new List<Rgba32>();
        for (var y = 128; y < sheet.Height; y++)
        {
            for (var x = index * 256; x < (index + 1) * 256; x++)
            {
                pixels.Add(sheet[x, y]);
            }
        }

        return pixels;
    }
}
