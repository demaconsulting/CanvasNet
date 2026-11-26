using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class with MathML equations: the listing of the format and the info
///     description of an equation (its display, elements, baseline, text alternative, and annotations) in JSON and text.
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsMathMlTests
{
    /// <summary>A block equation of four elements (the math element included), with a text alternative.</summary>
    private const string Equation =
        "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" display=\"block\" alttext=\"x equals 1\">"
        + "<mi>x</mi><mo>=</mo><mn>1</mn></math>\n";

    /// <summary>An inline equation with a LaTeX annotation, an annotation without an encoding, and no text alternative.</summary>
    private const string Annotated =
        "<math xmlns=\"http://www.w3.org/1998/Math/MathML\">"
        + "<semantics><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow>"
        + "<annotation encoding=\"application/x-tex\">a+b</annotation>"
        + "<annotation>a plus b</annotation></semantics></math>\n";

    /// <summary>
    ///     Runs the info command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunInfo(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new DiscoveryCommands.Info();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Test that the formats listing names the MathML format as a vector equation that is not multi-page, with its
    ///     extension and its help topic, in the text and in the JSON.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_IncludesMathMlEquation()
    {
        // Arrange
        using var json = Context.Create(["--json"]);
        using var text = Context.Create([]);
        var command = new DiscoveryCommands.Formats("1.2.3", "4.5.6");

        // Act
        var (jsonOut, _) = ToolHost.Capture(() => command.Run(json, []));
        var (textOut, _) = ToolHost.Capture(() => command.Run(text, []));

        // Assert: the table row, the JSON row, and the text row
        var equation = Assert.Single(DiscoveryCommands.InputFormats, r => r.Name == "mathml");
        Assert.Equal(("equation", true, false, "mathml"), (equation.Kind, equation.Vector, equation.MultiPage, equation.HelpTopic));
        Assert.Equal([".mml"], equation.Extensions);
        using var document = JsonDocument.Parse(jsonOut);
        var row = document.RootElement.GetProperty("inputs").EnumerateArray().Single(i => i.GetProperty("format").GetString() == "mathml");
        Assert.Equal("equation", row.GetProperty("kind").GetString());
        Assert.Matches(@"mathml\s+equation\s+\.mml\s+single\s+yes\s+help mathml", textOut);
    }

    /// <summary>
    ///     Test that info describes a block equation with the <c>equation</c> object (display, elements, baseline, the
    ///     text alternative, and no annotations) after the image object of its natural size, and one line of text with
    ///     the same facts; the <c>display=inline</c> fragment lays it out inline, at another size.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_MathMlEquation_ReportsDisplayElementsAndBaseline()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "eq.mml", Equation);
        var info = SourceLoader.Probe(path);
        var inlineInfo = SourceLoader.Probe(InputReference.Parse(path + "#display=inline"));

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path).TrimEnd();
        var inlineJson = RunInfo(true, path + "#display=inline");

        // Assert: the JSON members, in order
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("mathml", root.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal((info.Width, info.Height), (root.GetProperty("image").GetProperty("width").GetInt32(), root.GetProperty("image").GetProperty("height").GetInt32()));
        var equation = root.GetProperty("equation");
        Assert.Equal(["display", "elements", "baseline", "altText", "annotations"], equation.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("block", 4), (equation.GetProperty("display").GetString(), equation.GetProperty("elements").GetInt32()));
        var baseline = equation.GetProperty("baseline").GetInt32();
        Assert.Equal(info.Equation!.Baseline, baseline);
        Assert.InRange(baseline, 1, info.Height);
        Assert.Equal("x equals 1", equation.GetProperty("altText").GetString());
        Assert.Empty(equation.GetProperty("annotations").EnumerateArray());

        // Assert: one line of text with the same facts
        Assert.Equal(
            $"{path}: mathml, {info.Width}x{info.Height} px (laid out), equation, block, 4 elements, baseline {baseline}, alttext \"x equals 1\", {info.Bytes} bytes",
            text);

        // Assert: the fragment lays the equation out inline, and the facts follow it
        using var inlineDocument = JsonDocument.Parse(inlineJson);
        Assert.Equal("inline", inlineDocument.RootElement.GetProperty("equation").GetProperty("display").GetString());
        Assert.Equal("display=inline", inlineDocument.RootElement.GetProperty("input").GetProperty("fragment").GetString());
        Assert.Equal(inlineInfo.Width, inlineDocument.RootElement.GetProperty("image").GetProperty("width").GetInt32());

        // Assert: the usage text names MathML equations and an example
        var usage = new DiscoveryCommands.Info().Usage;
        Assert.Contains("A MathML equation (.mml)", usage, StringComparison.Ordinal);
        Assert.Contains("canvasnet info quadratic.mml --json", usage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that info lists the encoding of each annotation in order (an empty string for an annotation without one),
    ///     leaves <c>altText</c> out when the equation has none, and names the annotations in the text line.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_MathMlAnnotations_ListsAltTextAndEncodings()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "sum.mml", Annotated);

        // Act
        using var document = JsonDocument.Parse(RunInfo(true, path));
        var text = RunInfo(false, path).TrimEnd();

        // Assert: the inline display of the math element, no altText, and both encodings in order
        var equation = document.RootElement.GetProperty("equation");
        Assert.Equal("inline", equation.GetProperty("display").GetString());
        Assert.False(equation.TryGetProperty("altText", out _));
        Assert.Equal(["application/x-tex", string.Empty], equation.GetProperty("annotations").EnumerateArray().Select(a => a.GetString()));

        // Assert: the text line names them, without an alttext
        Assert.Contains(", equation, inline, ", text, StringComparison.Ordinal);
        Assert.Contains(", 2 annotations (application/x-tex, no encoding), ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alttext", text, StringComparison.Ordinal);
    }
}
