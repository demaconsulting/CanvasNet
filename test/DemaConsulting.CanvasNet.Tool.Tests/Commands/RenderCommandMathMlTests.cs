using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the render command (and <c>inspect</c>) with MathML equations: the picture and its operation, the size
///     options, the background, the display fragment, the options that only a Mermaid diagram takes, and the notes and
///     <c>--strict</c>.
/// </summary>
[Collection("Sequential")]
public class RenderCommandMathMlTests
{
    /// <summary>The namespace of MathML.</summary>
    private const string Namespace = "http://www.w3.org/1998/Math/MathML";

    /// <summary>A sum with limits: below and above the sign in block display, beside it inline.</summary>
    private const string Sum =
        "<math xmlns=\"" + Namespace + "\" display=\"block\"><munderover><mo>&#x2211;</mo>"
        + "<mrow><mi>k</mi><mo>=</mo><mn>1</mn></mrow><mi>n</mi></munderover><mi>k</mi></math>\n";

    /// <summary>White.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

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
    ///     Copies the quadratic formula fixture into a folder.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <returns>The path of the copy.</returns>
    private static string Quadratic(TempDirectory directory)
    {
        var path = directory.Combine("quadratic.mml");
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml")), path);
        return path;
    }

    /// <summary>
    ///     Counts the pixels of a picture with dark ink.
    /// </summary>
    /// <param name="surface">The picture.</param>
    /// <returns>The number of opaque pixels whose red channel is below 96.</returns>
    private static int Ink(Surface surface) =>
        Enumerable.Range(0, surface.Height).Sum(y => Enumerable.Range(0, surface.Width).Count(x => surface[x, y].A == 255 && surface[x, y].R < 96));

    /// <summary>
    ///     Test that an equation is drawn strictly at the natural size a probe reports, over white with dark ink, and that
    ///     the result names the format, the natural size, and a render operation without a theme or a display.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MathMlEquation_DrawsAtTheNaturalSizeOverWhite()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = Quadratic(directory);
        var output = directory.Combine("quadratic.png");
        var info = SourceLoader.Probe(input);

        // Act
        using var document = RunJson(input, "-o", output, "--strict");

        // Assert: the format, the natural size, and the picture
        var root = document.RootElement;
        Assert.Equal("mathml", root.GetProperty("input").GetProperty("format").GetString());
        var natural = root.GetProperty("input").GetProperty("natural");
        Assert.Equal((info.Width, info.Height), (natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()));
        using var image = PngCodec.Load(output);
        Assert.Equal((info.Width, info.Height), (image.Width, image.Height));
        Assert.Equal(White, image[0, 0]);
        Assert.True(Ink(image) > 100);

        // Assert: one render operation, no theme, no display, and no diagnostic
        var render = Assert.Single(root.GetProperty("operations").EnumerateArray());
        Assert.Equal("render", render.GetProperty("op").GetString());
        Assert.False(render.TryGetProperty("theme", out _));
        Assert.False(render.TryGetProperty("display", out _));
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());

        // Assert: the usage text names MathML equations, the display fragment, and an example
        var usage = new RenderCommand().Usage;
        Assert.Contains("MathML (.mml)", usage, StringComparison.Ordinal);
        Assert.Contains("eq.mml#display=inline", usage, StringComparison.Ordinal);
        Assert.Contains("canvasnet render quadratic.mml --scale 3 -o quadratic.png", usage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <c>--scale</c> lays the equation out again at about that multiple of its natural size, that
    ///     <c>--width</c> draws it into that width, and that <c>--background transparent</c> leaves the picture transparent.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MathMlScaleAndBackground_FollowTheOptions()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = Quadratic(directory);
        var info = SourceLoader.Probe(input);

        // Act
        using var scaled = RunJson(input, "-o", directory.Combine("scaled.png"), "--scale", "3");
        using var wide = RunJson(input, "-o", directory.Combine("wide.png"), "--width", "400");
        using var clear = RunJson(input, "-o", directory.Combine("clear.png"), "--background", "transparent");

        // Assert: three times the size, rounded out at that scale, with ink
        using var large = PngCodec.Load(directory.Combine("scaled.png"));
        Assert.InRange(large.Width, (info.Width * 3) - 3, info.Width * 3);
        Assert.InRange(large.Height, (info.Height * 3) - 3, info.Height * 3);
        Assert.True(Ink(large) > 900);

        // Assert: the width that was asked for, and the aspect ratio kept
        using var fitted = PngCodec.Load(directory.Combine("wide.png"));
        Assert.Equal(400, fitted.Width);
        Assert.InRange(fitted.Height, (400 * info.Height / info.Width) - 2, (400 * info.Height / info.Width) + 2);

        // Assert: a transparent corner, and the background in the operation
        using var transparent = PngCodec.Load(directory.Combine("clear.png"));
        Assert.Equal(0, transparent[0, 0].A);
        Assert.Equal("#00000000", clear.RootElement.GetProperty("operations")[0].GetProperty("background").GetString());
        Assert.Equal("render", scaled.RootElement.GetProperty("operations")[0].GetProperty("op").GetString());
        Assert.Equal(400, wide.RootElement.GetProperty("outputs")[0].GetProperty("width").GetInt32());
    }

    /// <summary>
    ///     Test that the <c>display=inline</c> fragment of the input lays a block sum out inline, with its limits beside the
    ///     sign, so the picture is lower than the block one, and that the operation and the input name the display.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MathMlDisplayFragment_ChangesTheSize()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "sum.mml", Sum);

        // Act
        using var block = RunJson(input, "-o", directory.Combine("block.png"));
        using var inline = RunJson(input + "#display=inline", "-o", directory.Combine("inline.png"));

        // Assert: the inline picture is lower, and each picture has the size its result reports
        using var blockImage = PngCodec.Load(directory.Combine("block.png"));
        using var inlineImage = PngCodec.Load(directory.Combine("inline.png"));
        Assert.True(inlineImage.Height < blockImage.Height, $"inline {inlineImage.Height} is not lower than block {blockImage.Height}");
        Assert.Equal(inlineImage.Width, inline.RootElement.GetProperty("input").GetProperty("natural").GetProperty("width").GetInt32());

        // Assert: the fragment is reported on the input and the display on the operation
        Assert.Equal("display=inline", inline.RootElement.GetProperty("input").GetProperty("fragment").GetString());
        Assert.Equal("inline", inline.RootElement.GetProperty("operations")[0].GetProperty("display").GetString());
        Assert.False(block.RootElement.GetProperty("input").TryGetProperty("fragment", out _));
    }

    /// <summary>
    ///     Test that an SVG drawing that links an equation draws it at its placement, sharply and over the drawing (an
    ///     embedded equation is transparent), with an embed operation, and that <c>flatten</c> replaces the link with a PNG
    ///     picture of the placement size.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgLinkingAnEquation_DrawsAndFlattensIt()
    {
        // Arrange: a light gray drawing that places the quadratic formula at twice its natural size
        using var directory = new TempDirectory();
        var equation = Quadratic(directory);
        var info = SourceLoader.Probe(equation);
        var (width, height) = (info.Width * 2, info.Height * 2);
        var drawing = FixtureFactory.WriteText(
            directory.Path,
            "figure.svg",
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width + 20}\" height=\"{height + 20}\">"
            + $"<rect width=\"{width + 20}\" height=\"{height + 20}\" fill=\"#e0e0e0\"/>"
            + $"<image href=\"quadratic.mml\" x=\"10\" y=\"10\" width=\"{width}\" height=\"{height}\"/></svg>");
        var flattened = directory.Combine("flat.svg");

        // Act
        using var document = RunJson(drawing, "-o", directory.Combine("figure.png"), "--strict");
        var (flattenExit, flattenOut, _) = ToolHost.Invoke("flatten", drawing, "-o", flattened, "--json");

        // Assert: the embed, the gray drawing showing through the equation's box, and dark ink inside it
        var operations = document.RootElement.GetProperty("operations").EnumerateArray().Select(o => o.GetProperty("op").GetString()).ToList();
        Assert.Contains("embed", operations);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        using var image = PngCodec.Load(directory.Combine("figure.png"));
        var gray = new Rgba32(0xE0, 0xE0, 0xE0, 255);
        Assert.Equal(gray, image[12, 12]);
        Assert.True(Ink(image) > 300);

        // Assert: the flattened copy holds a PNG of the placement size instead of the link
        Assert.Equal(0, flattenExit);
        using var result = JsonDocument.Parse(flattenOut);
        var replaced = Assert.Single(result.RootElement.GetProperty("replaced").EnumerateArray());
        Assert.Equal(("quadratic.mml", "mathml"), (replaced.GetProperty("href").GetString(), replaced.GetProperty("format").GetString()));
        Assert.Equal((width, height), (replaced.GetProperty("width").GetInt32(), replaced.GetProperty("height").GetInt32()));
        Assert.DoesNotContain("quadratic.mml", File.ReadAllText(flattened), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <c>--theme</c> and <c>--positions</c>, which only a Mermaid diagram (or a chart) takes, are usage errors
    ///     on an equation for render and inspect, before anything is drawn.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MathMlThemeOrPositions_IsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = Quadratic(directory);

        // Act
        var theme = RunFailing(input, "--theme", "dark", "-o", directory.Combine("a.png"));
        var (positionsExit, _, positionsErr) = ToolHost.Invoke("render", input, "--positions", "all", "-o", directory.Combine("b.png"), "--json");
        var (inspectExit, _, inspectErr) = ToolHost.Invoke("inspect", "colors", input, "--theme", "dark");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, theme.Code);
        Assert.Contains("--theme only applies to a Mermaid diagram", theme.Message, StringComparison.Ordinal);
        Assert.Contains("mathml file", theme.Message, StringComparison.Ordinal);
        Assert.Equal(1, positionsExit);
        Assert.Contains("Error: usage-error: The option --positions only applies to a Mermaid diagram or a Vega-Lite chart", positionsErr, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("a.png")));
        Assert.False(File.Exists(directory.Combine("b.png")));
        Assert.Equal(1, inspectExit);
        Assert.Contains("Error: usage-error: The option --theme only applies to a Mermaid diagram", inspectErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an element outside MathML Core is the located diagnostic <c>mathml-element-unsupported</c> and an
    ///     invalid attribute value the warning <c>mathml-warning</c> with the specific code in <c>detail</c>, and that with
    ///     <c>--strict</c> the element fails the render with <c>unsupported-feature</c> and no file is written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MathMlStrictUnsupportedElement_IsUnsupportedFeature()
    {
        // Arrange: an mfenced (not in MathML Core) on line 2, and a width without a unit on line 3
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(
            directory.Path,
            "old.mml",
            "<math xmlns=\"" + Namespace + "\">\n<mfenced><mi>x</mi></mfenced>\n<mspace width=\"3\"/>\n</math>\n");

        // Act
        using var document = RunJson(input, "-o", directory.Combine("old.png"));
        var strict = RunFailing(input, "--strict", "-o", directory.Combine("strict.png"));

        // Assert: the unsupported element, located on line 2
        var diagnostics = document.RootElement.GetProperty("diagnostics").EnumerateArray().ToList();
        var unsupported = Assert.Single(diagnostics, d => d.GetProperty("severity").GetString() == "unsupported");
        Assert.Equal(("mathml", "mathml-element-unsupported"), (unsupported.GetProperty("source").GetString(), unsupported.GetProperty("code").GetString()));
        Assert.Equal(2, unsupported.GetProperty("location").GetProperty("line").GetInt32());

        // Assert: the warning, with its specific code and line
        var warning = Assert.Single(diagnostics, d => d.GetProperty("severity").GetString() == "warning");
        Assert.Equal(("mathml-warning", "mathml-attribute-invalid"), (warning.GetProperty("code").GetString(), warning.GetProperty("detail").GetString()));
        Assert.Equal(3, warning.GetProperty("location").GetProperty("line").GetInt32());
        Assert.True(File.Exists(directory.Combine("old.png")));

        // Assert: strict mode fails without output
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.False(File.Exists(directory.Combine("strict.png")));
    }
}
