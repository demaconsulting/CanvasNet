// cspell:ignore vega
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the <c>--param</c> option of the render command, its verb commands and <c>inspect</c>: a Vega-Lite chart
///     parameter set from the command line, the shapes a selection takes, the errors, and the fragment parameters of an SVG
///     link that set a model variable or a chart parameter the same way.
/// </summary>
[Collection("Sequential")]
public class RenderCommandParamTests
{
    /// <summary>A model whose width is a variable.</summary>
    private const string Box = "width = 5; cube([width, 5, 5], center = true);";

    /// <summary>
    ///     Gets a chart with a numeric parameter that filters the bars (the layout follows the number of bars) and an
    ///     interval selection on y that colors the bars in it.
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
    ///     Runs the render command with <c>--json</c> and returns the document it prints.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The parsed document; the caller disposes it.</returns>
    private static JsonDocument RunJson(params string[] args)
    {
        using var context = Context.Create(["--json"]);
        var command = new RenderCommand();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return JsonDocument.Parse(stdOut);
    }

    /// <summary>
    ///     Runs the render command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new RenderCommand();
        return ToolHost.ExpectFailure(() => command.Run(context, args));
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
    ///     Test that <c>--param</c> gives the chart the value: the picture is the one of the chart that has that default, the
    ///     natural size of the result is the size of the parameterized layout (not the default one), and the render operation
    ///     lists the parameter.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ParamScalar_SetsTheParameterAndTheNaturalSize()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        var reference = FixtureFactory.WriteText(directory.Path, "reference.vl.json", Chart(4));

        // Act
        using var parameterized = RunJson(input, "--param", "cutoff=4", "-o", directory.Combine("a.png"));
        using var plain = RunJson(input, "-o", directory.Combine("b.png"));
        using var expected = RunJson(reference, "-o", directory.Combine("c.png"));

        // Assert: same pixels as the reference, and a narrower page than the default chart
        using var a = PngCodec.Load(directory.Combine("a.png"));
        using var b = PngCodec.Load(directory.Combine("b.png"));
        using var c = PngCodec.Load(directory.Combine("c.png"));
        AssertSamePixels(c, a);
        Assert.True(a.Width < b.Width);
        var natural = parameterized.RootElement.GetProperty("input").GetProperty("natural");
        Assert.Equal((a.Width, a.Height), (natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()));
        var render = parameterized.RootElement.GetProperty("operations")[0];
        Assert.Equal(["cutoff=4"], render.GetProperty("params").EnumerateArray().Select(p => p.GetString()));
        Assert.False(plain.RootElement.GetProperty("operations")[0].TryGetProperty("params", out _));
        Assert.Empty(parameterized.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.True(expected.RootElement.GetProperty("ok").GetBoolean());
    }

    /// <summary>
    ///     Test that a selection parameter takes a JSON object on the command line, the last value of a repeated name wins,
    ///     and the parameters are listed in name order with their values as JSON.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ParamSelectionAndRepeat_TakeJsonAndLastWins()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        var reference = FixtureFactory.WriteText(directory.Path, "reference.vl.json", Chart(2, "{\"y\":[2,4]}"));

        // Act
        using var document = RunJson(input, "--param", "cutoff=5", "--param", "brush={\"y\":[2,4]}", "--param", "cutoff=2", "-o", directory.Combine("a.png"));
        using var expected = RunJson(reference, "-o", directory.Combine("b.png"));

        // Assert
        using var a = PngCodec.Load(directory.Combine("a.png"));
        using var b = PngCodec.Load(directory.Combine("b.png"));
        AssertSamePixels(b, a);
        var parameters = document.RootElement.GetProperty("operations")[0].GetProperty("params").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Equal(["brush={\"y\":[2,4]}", "cutoff=2"], parameters);
        Assert.True(expected.RootElement.GetProperty("ok").GetBoolean());
    }

    /// <summary>
    ///     Test that a parameter the chart does not declare is a <c>vega-lite-error</c> that lists the declared names, and
    ///     that nothing is written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ParamUnknownName_IsVegaLiteErrorListingTheDeclaredNames()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));

        // Act
        var exception = RunFailing(input, "--param", "nope=1", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal(ErrorCodes.VegaLiteError, exception.Code);
        Assert.Contains("'nope'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("declared: brush, cutoff", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("o.png")));
    }

    /// <summary>
    ///     Test that a chart without parameters says so, and that a malformed or unusable value is a usage error before
    ///     anything is loaded.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ParamErrors_AreReported()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        var plain = FixtureFactory.WriteSource(directory.Path, "vega-lite");

        // Act
        var none = RunFailing(plain, "--param", "x=1", "-o", directory.Combine("o.png"));
        var malformed = RunFailing(input, "--param", "brush={x", "-o", directory.Combine("o.png"));
        var noEquals = RunFailing(input, "--param", "cutoff", "-o", directory.Combine("o.png"));
        var deep = RunFailing(input, "--param", "brush=" + new string('[', 33) + new string(']', 33), "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal(ErrorCodes.VegaLiteError, none.Code);
        Assert.Contains("declares no parameters", none.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UsageError, malformed.Code);
        Assert.Contains("--param", malformed.Message, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UsageError, noEquals.Code);
        Assert.Equal(ErrorCodes.UsageError, deep.Code);
        Assert.False(File.Exists(directory.Combine("o.png")));
    }

    /// <summary>
    ///     Test that <c>--param</c> on an input that is not a chart is refused rather than silently ignored.
    /// </summary>
    /// <param name="key">The kind of input.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("svg")]
    [InlineData("pdf")]
    [InlineData("scad")]
    public void RenderCommand_Run_ParamOnAnotherKind_ThrowsUsageError(string key)
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, key);

        // Act
        var exception = RunFailing(input, "--param", "x=1", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains($"is a {key} file", exception.Message, StringComparison.Ordinal);
        Assert.Contains("only applies to a Vega-Lite chart", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the model options stay a usage error for a chart (<c>-D</c> is not a chart parameter).
    /// </summary>
    [Fact]
    public void RenderCommand_Run_DefineOnAChart_StaysAUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));

        // Act
        var exception = RunFailing(input, "-D", "cutoff=4", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Contains("only apply to an OpenSCAD model", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the verb commands and <c>inspect</c> take <c>--param</c> as well: a resize of a parameterized chart is
    ///     the size asked for, inspect reads the parameterized picture, and an unknown name fails the same way.
    /// </summary>
    [Fact]
    public void ToolHost_Invoke_ParamOnVerbAndInspect_IsAccepted()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        var output = directory.Combine("resized.png");

        // Act
        var resize = ToolHost.Invoke("resize", input, "60x40", "--param", "cutoff=4", "-o", output);
        var colors = ToolHost.Invoke("inspect", "colors", input, "--param", "cutoff=4", "--json");
        var plain = ToolHost.Invoke("inspect", "colors", input, "--json");
        var unknown = ToolHost.Invoke("inspect", "colors", input, "--param", "nope=1", "--json");
        var png = ToolHost.Invoke("inspect", "colors", FixtureFactory.WriteSource(directory.Path, "png"), "--param", "x=1", "--json");

        // Assert
        Assert.True(resize.ExitCode == 0, resize.StdErr + resize.StdOut);
        using var resized = PngCodec.Load(output);
        Assert.Equal((60, 40), (resized.Width, resized.Height));
        Assert.True(colors.ExitCode == 0, colors.StdErr + colors.StdOut);
        Assert.True(plain.ExitCode == 0, plain.StdErr + plain.StdOut);
        Assert.NotEqual(plain.StdOut, colors.StdOut);
        Assert.Equal(1, unknown.ExitCode);
        Assert.Contains("vega-lite-error", unknown.StdOut, StringComparison.Ordinal);
        Assert.Equal(1, png.ExitCode);
        Assert.Contains("only applies to a Vega-Lite chart", png.StdOut, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the topics document the option and the fragment prefixes: <c>--param</c> in the options of the
    ///     <c>vega-lite</c> topic, <c>def.</c> and <c>param.</c> in the <c>svg</c> topic and <c>#def.</c> in the <c>scad</c>
    ///     topic, and <c>vl-param-unknown</c> in the diagnostics of both the <c>vega-lite</c> and the <c>svg</c> topics.
    /// </summary>
    [Fact]
    public void HelpTopics_ParamOptionAndFragmentPrefixes_AreDocumented()
    {
        // Arrange
        static string TextOf(string name) => string.Join("\n", HelpTopics.Find(name)!.Sections.SelectMany(s => s.Items).Select(i => $"{i.Term} {i.Text}"));
        var vegaLite = HelpTopics.Find("vega-lite")!;
        var svg = HelpTopics.Find("svg")!;
        var svgLinks = svg.Sections.First(s => s.Id == "image-links").Items;
        var variableFragment = Assert.Single(svgLinks, i => i.Term == "variable fragment").Text;

        // Assert: the option, with the grammar and the failure for an unknown name
        var option = Assert.Single(vegaLite.Sections.First(s => s.Id == "options").Items, i => i.Term == "--param <name=value>").Text;
        Assert.Contains("--param brush={", option, StringComparison.Ordinal);
        Assert.Contains("vega-lite-error", option, StringComparison.Ordinal);
        Assert.Contains("param.cutoff", option, StringComparison.Ordinal);

        // Assert: the prefixes in the svg and scad topics, with the codes
        Assert.Contains("def.<name>=<value>", variableFragment, StringComparison.Ordinal);
        Assert.Contains("param.<name>=<value>", variableFragment, StringComparison.Ordinal);
        Assert.Contains("-D name=value", variableFragment, StringComparison.Ordinal);
        Assert.Contains("--param", variableFragment, StringComparison.Ordinal);
        Assert.Contains("vl-param-unknown", variableFragment, StringComparison.Ordinal);
        Assert.Contains("image-fragment-unknown-parameter", variableFragment, StringComparison.Ordinal);
        Assert.Contains("image-fragment-invalid", variableFragment, StringComparison.Ordinal);
        Assert.Contains("part.scad#view=front&amp;def.width=20", TextOf("scad"), StringComparison.Ordinal);
        foreach (var topic in new[] { vegaLite, svg })
        {
            Assert.Contains(topic.Sections.First(s => s.Id == "diagnostics").Items, i => i.Term == "vl-param-unknown");
        }

        // Assert: the cross-references of the vega-lite topic and the strict behavior
        Assert.Contains("chart.vl.json#param.cutoff=50", TextOf("vega-lite"), StringComparison.Ordinal);
        Assert.Contains("vl-param-unknown stay warnings", Assert.Single(svg.Sections.First(s => s.Id == "options").Items, i => i.Term == "--strict").Text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the example the topics and the usage show for <c>--param</c> is a command the tool runs: the usage
    ///     text of <c>render</c> mentions it, and the documented command renders a chart that declares the parameter.
    /// </summary>
    [Fact]
    public void RenderCommand_Usage_MentionsParamAndItsExampleRuns()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));

        // Act
        var usage = new RenderCommand().Usage;
        var run = ToolHost.Invoke("render", input, "--param", "cutoff=50", "-o", directory.Combine("o.png"));

        // Assert
        Assert.Contains("--param name=value", usage, StringComparison.Ordinal);
        Assert.Contains("canvasnet render chart.vl.json --param cutoff=50 -o chart.png", usage, StringComparison.Ordinal);
        Assert.Contains("chart.vl.json#param.cutoff=50", usage, StringComparison.Ordinal);
        Assert.Equal(0, run.ExitCode);
    }

    /// <summary>
    ///     Test that <c>--param</c> is declared once in the option table, takes a value, and is shared by the verb commands and <c>inspect</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_OptionTable_DeclaresParam()
    {
        // Act
        var spec = Assert.Single(RenderCommand.OptionTable, o => o.Name == "--param");

        // Assert
        Assert.True(spec.TakesValue);
        Assert.Equal("name=value", spec.ValueName);
        Assert.Contains("Vega-Lite chart", spec.Description, StringComparison.Ordinal);
        Assert.Contains(InspectCommand.OptionTable, o => o.Name == "--param");
        Assert.Equal(["--param"], RenderCommand.ChartOptionNames);
    }

    /// <summary>
    ///     Test that the fragment of an SVG link sets a model variable and a chart parameter like <c>-D</c> and
    ///     <c>--param</c>: the diagram that embeds them is the one that embeds the files with the values in them, and a name the
    ///     chart does not declare is a warning of the render (with a stable code) rather than an error.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgFragmentDefineAndParam_BehaveLikeTheOptions()
    {
        // Arrange: a diagram with a model and a chart that carry overrides, and one with the values in the files
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", Box);
        FixtureFactory.WriteText(directory.Path, "wide.scad", Box.Replace("width = 5", "width = 20", StringComparison.Ordinal));
        FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        FixtureFactory.WriteText(directory.Path, "chart-4.vl.json", Chart(4));
        string Diagram(string model, string chart) =>
            "<svg xmlns='http://www.w3.org/2000/svg' width='300' height='100' viewBox='0 0 300 100'><rect width='300' height='100' fill='#808080'/>"
            + $"<image href='{model}' x='0' y='0' width='100' height='100'/><image href='{chart}' x='100' y='0' width='200' height='100'/></svg>";
        var overridden = FixtureFactory.WriteText(directory.Path, "overridden.svg", Diagram("part.scad#view=front&amp;def.width=20", "chart.vl.json#param.cutoff=4"));
        var expected = FixtureFactory.WriteText(directory.Path, "expected.svg", Diagram("wide.scad#view=front", "chart-4.vl.json"));
        var unknown = FixtureFactory.WriteText(directory.Path, "unknown.svg", Diagram("part.scad#view=front", "chart-4.vl.json#param.nope=1"));

        // Act
        using var first = RunJson(overridden, "-o", directory.Combine("a.png"));
        using var second = RunJson(expected, "-o", directory.Combine("b.png"));
        using var third = RunJson(unknown, "-o", directory.Combine("c.png"));

        // Assert
        using var a = PngCodec.Load(directory.Combine("a.png"));
        using var b = PngCodec.Load(directory.Combine("b.png"));
        AssertSamePixels(b, a);
        Assert.Empty(first.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Empty(second.RootElement.GetProperty("diagnostics").EnumerateArray());
        var warning = Assert.Single(third.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("vl-param-unknown", warning.GetProperty("code").GetString());
        Assert.Equal("warning", warning.GetProperty("severity").GetString());
        Assert.Contains("param.nope", warning.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an unusable value in the fragment of a link is the <c>image-fragment-invalid</c> note, the link is not
    ///     drawn, and with <c>--strict</c> it is an <c>unsupported-feature</c> error, while the unknown-name warning is not.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgFragmentInvalidValue_IsUnsupportedAndFailsWithStrict()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", Box);
        FixtureFactory.WriteText(directory.Path, "chart.vl.json", Chart(1));
        string Diagram(string href) =>
            $"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><image href='{href}' x='0' y='0' width='100' height='100'/></svg>";
        var invalidDefine = FixtureFactory.WriteText(directory.Path, "a.svg", Diagram("part.scad#def.width=[1,2"));
        var invalidParam = FixtureFactory.WriteText(directory.Path, "b.svg", Diagram("chart.vl.json#param.brush={x"));
        var unknown = FixtureFactory.WriteText(directory.Path, "c.svg", Diagram("chart.vl.json#param.nope=1"));

        // Act
        using var define = RunJson(invalidDefine, "-o", directory.Combine("a.png"));
        using var parameter = RunJson(invalidParam, "-o", directory.Combine("b.png"));
        var strictDefine = ToolHost.Invoke("render", invalidDefine, "--strict", "--json", "-o", directory.Combine("sa.png"));
        var strictUnknown = ToolHost.Invoke("render", unknown, "--strict", "--json", "-o", directory.Combine("sc.png"));

        // Assert
        foreach (var document in new[] { define, parameter })
        {
            var note = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
            Assert.Equal("image-fragment-invalid", note.GetProperty("code").GetString());
            Assert.Equal("unsupported", note.GetProperty("severity").GetString());
        }

        Assert.Equal(1, strictDefine.ExitCode);
        Assert.Contains("unsupported-feature", strictDefine.StdOut, StringComparison.Ordinal);
        Assert.Equal(0, strictUnknown.ExitCode);
    }
}
