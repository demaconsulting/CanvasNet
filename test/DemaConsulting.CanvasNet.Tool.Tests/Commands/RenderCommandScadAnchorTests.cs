// cspell:ignore canvasnet scad openscad
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the RenderCommand class with the anchors of an OpenSCAD model: the <c>anchors</c> array of the
///     JSON result, its pixel positions checked against the written image, the mapping of those positions through the
///     pipeline steps, the diagnostics, and the text output that does not change.
/// </summary>
[Collection("Sequential")]
public class RenderCommandScadAnchorTests
{
    /// <summary>The model: a box seen from the top, with anchors on three of its top corners and its center.</summary>
    private const string Model =
        "cube([40, 20, 10]);\n"
        + "echo(\"anchor\", \"upperLeft\", [0, 20, 10]);\n"
        + "echo(\"anchor\", \"lowerRight\", [40, 0, 10]);\n"
        + "translate([20, 10, 0]) { echo(\"anchor\", \"center\", [0, 0, 10]); }\n";

    /// <summary>The white background of a render.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Runs the render command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output and standard error.</returns>
    private static (string StdOut, string StdErr) Run(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new RenderCommand();
        return ToolHost.Capture(() => command.Run(context, args));
    }

    /// <summary>
    ///     Renders the model to a PNG with the given arguments (after the input) and returns the parsed result.
    /// </summary>
    /// <param name="directory">The directory of the model and the output.</param>
    /// <param name="source">The text of the model.</param>
    /// <param name="arguments">The arguments between the input and the output.</param>
    /// <returns>The JSON document of the result; the caller disposes it.</returns>
    private static JsonDocument RenderJsonWith(string directory, string source, params string[] arguments)
    {
        var input = FixtureFactory.WriteText(directory, "model.scad", source);
        var (stdOut, _) = Run(true, [input, .. arguments, "-o", Path.Combine(directory, "out.png")]);
        return JsonDocument.Parse(stdOut);
    }

    /// <summary>
    ///     Renders the model from the top at 400 by 200 pixels, with extra arguments, and returns the parsed result.
    /// </summary>
    /// <param name="directory">The directory of the model and the output.</param>
    /// <param name="source">The text of the model.</param>
    /// <param name="extra">The arguments after the view and the size.</param>
    /// <returns>The JSON document of the result; the caller disposes it.</returns>
    private static JsonDocument RenderJson(string directory, string source, params string[] extra) =>
        RenderJsonWith(directory, source, ["--view", "top", "--size", "400x200", .. extra]);

    /// <summary>
    ///     Reads the pixel of a named anchor from a result.
    /// </summary>
    /// <param name="document">The result.</param>
    /// <param name="name">The anchor name.</param>
    /// <returns>The x and y of the pixel.</returns>
    private static (double X, double Y) PixelOf(JsonDocument document, string name)
    {
        var anchor = document.RootElement.GetProperty("anchors").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name);
        var pixel = anchor.GetProperty("pixel");
        return (pixel[0].GetDouble(), pixel[1].GetDouble());
    }

    /// <summary>
    ///     Reads the <c>inside</c> flag of a named anchor from a result.
    /// </summary>
    /// <param name="document">The result.</param>
    /// <param name="name">The anchor name.</param>
    /// <returns>The flag.</returns>
    private static bool InsideOf(JsonDocument document, string name) =>
        document.RootElement.GetProperty("anchors").EnumerateArray().Single(a => a.GetProperty("name").GetString() == name).GetProperty("inside").GetBoolean();

    /// <summary>
    ///     Asserts that two pixel positions are the same within a tolerance.
    /// </summary>
    /// <param name="expected">The expected position.</param>
    /// <param name="actual">The actual position.</param>
    /// <param name="tolerance">The tolerance in pixels.</param>
    private static void AssertNear((double X, double Y) expected, (double X, double Y) actual, double tolerance = 0.01)
    {
        Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
        Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
    }

    /// <summary>
    ///     Test that the JSON result lists the anchors with their name, world point, pixel and inside flag, in echo
    ///     order, and that the pixels are the corners of what is drawn in the written image.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadAnchors_JsonListsAnchorsThatMatchTheWrittenImage()
    {
        // Arrange / Act
        using var directory = new TempDirectory();
        using var document = RenderJson(directory.Path, Model);
        using var image = PngCodec.Load(Path.Combine(directory.Path, "out.png"));

        // Assert: the three anchors, in order, with their world points
        var anchors = document.RootElement.GetProperty("anchors").EnumerateArray().ToList();
        Assert.Equal(["upperLeft", "lowerRight", "center"], anchors.Select(a => a.GetProperty("name").GetString()));
        Assert.Equal([0.0, 20.0, 10.0], anchors[0].GetProperty("point").EnumerateArray().Select(v => v.GetDouble()));
        Assert.Equal([20.0, 10.0, 10.0], anchors[2].GetProperty("point").EnumerateArray().Select(v => v.GetDouble()));
        Assert.All(anchors, a => Assert.True(a.GetProperty("inside").GetBoolean()));

        // The box covers a rectangle; the corner anchors are on its corners and the center anchor in the middle
        var rows = Enumerable.Range(0, image.Height).Where(y => image.GetRowSpan(y).ToArray().Any(p => p != White)).ToList();
        var columns = Enumerable.Range(0, image.Width).Where(x => Enumerable.Range(0, image.Height).Any(y => image[x, y] != White)).ToList();
        AssertNear((columns[0], rows[0]), PixelOf(document, "upperLeft"), 1.0);
        AssertNear((columns[^1] + 1.0, rows[^1] + 1.0), PixelOf(document, "lowerRight"), 1.0);
        AssertNear(((columns[0] + columns[^1] + 1.0) / 2.0, (rows[0] + rows[^1] + 1.0) / 2.0), PixelOf(document, "center"), 1.0);
    }

    /// <summary>
    ///     Test that a model with no anchors has an empty <c>anchors</c> array, and that a model that is not a model
    ///     source has no <c>anchors</c> member at all.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadWithoutAnchorsAndOtherSources_EmptyArrayOrNoMember()
    {
        // Arrange
        using var directory = new TempDirectory();
        var png = FixtureFactory.WriteSource(directory.Path, "png");

        // Act
        using var model = RenderJson(directory.Path, "cube(5);");
        var (stdOut, _) = Run(true, png, "-o", Path.Combine(directory.Path, "copy.png"));
        using var other = JsonDocument.Parse(stdOut);

        // Assert
        Assert.Equal(0, model.RootElement.GetProperty("anchors").GetArrayLength());
        Assert.False(other.RootElement.TryGetProperty("anchors", out _));
    }

    /// <summary>
    ///     Test that a malformed anchor is a <c>scad-warning</c> diagnostic whose message carries the stable identifier,
    ///     that it is not in the array, and that the echo line is still reported.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadMalformedAnchor_IsADiagnosticAndNotInTheArray()
    {
        // Arrange / Act
        using var directory = new TempDirectory();
        using var document = RenderJson(directory.Path, "cube(5);\necho(\"anchor\", \"bad\", [1, 2]);\necho(\"anchor\", \"good\", [1, 2, 3]);");

        // Assert
        var anchor = Assert.Single(document.RootElement.GetProperty("anchors").EnumerateArray());
        Assert.Equal("good", anchor.GetProperty("name").GetString());
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-warning", diagnostic.GetProperty("code").GetString());
        Assert.Contains("scad-anchor-invalid:", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, document.RootElement.GetProperty("echo").GetArrayLength());
    }

    /// <summary>
    ///     Test that the text output does not change: one <c>wrote</c> line on standard output, with the anchor echo
    ///     lines on standard error only.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadAnchorsInTextMode_StdOutIsUnchangedAndEchoGoesToStdErr()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", Model);
        var output = Path.Combine(directory.Path, "out.png");

        // Act
        var (stdOut, stdErr) = Run(false, input, "--size", "64x64", "-o", output);

        // Assert
        Assert.Equal($"wrote {output} (64x64 png, {new FileInfo(output).Length} bytes) from {input}{Environment.NewLine}", stdOut);
        Assert.Equal(3, stdErr.Split('\n').Count(line => line.Contains("ECHO: \"anchor\"", StringComparison.Ordinal)));
        Assert.DoesNotContain("anchor", stdOut, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the pixels follow the size of the render: twice the size gives twice the pixel position, whether the
    ///     size is given as a size or as a scale.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadAnchorsWithLargerSize_ScalePixels()
    {
        // Arrange / Act
        using var directory = new TempDirectory();
        using var small = RenderJsonWith(directory.Path, Model, "--view", "top", "--size", "400x200");
        using var large = RenderJsonWith(directory.Path, Model, "--view", "top", "--size", "800x400");
        using var natural = RenderJsonWith(directory.Path, Model, "--view", "top");
        using var scaled = RenderJsonWith(directory.Path, Model, "--view", "top", "--scale", "0.5");

        // Assert: the picture is framed the same way at every size, so the positions scale with the image
        var (x, y) = PixelOf(small, "upperLeft");
        AssertNear((x * 2, y * 2), PixelOf(large, "upperLeft"));
        var (nx, ny) = PixelOf(natural, "upperLeft");
        AssertNear((nx / 2, ny / 2), PixelOf(scaled, "upperLeft"));
    }

    /// <summary>
    ///     Test that each pipeline step moves the pixels the way it moves the image, relative to the pixel of the
    ///     unprocessed render of 400 by 200 pixels.
    /// </summary>
    /// <param name="arguments">The pipeline arguments.</param>
    /// <param name="scaleX">The factor of x after the step.</param>
    /// <param name="scaleY">The factor of y after the step.</param>
    /// <param name="offsetX">The offset added to x after scaling.</param>
    /// <param name="offsetY">The offset added to y after scaling.</param>
    /// <param name="width">The expected width of the output.</param>
    /// <param name="height">The expected height of the output.</param>
    [Theory]
    [InlineData("--resize|200x100", 0.5, 0.5, 0, 0, 200, 100)]
    [InlineData("--resize|800x100", 2.0, 0.5, 0, 0, 800, 100)]
    [InlineData("--fit|100x100", 0.25, 0.25, 0, 0, 100, 50)]
    [InlineData("--crop|20,10,300,150", 1.0, 1.0, -20, -10, 300, 150)]
    [InlineData("--pad|10", 1.0, 1.0, 10, 10, 420, 220)]
    [InlineData("--pad|1,2,3,4", 1.0, 1.0, 1, 2, 404, 206)]
    [InlineData("--flip|h", -1.0, 1.0, 400, 0, 400, 200)]
    [InlineData("--flip|v", 1.0, -1.0, 0, 200, 400, 200)]
    [InlineData("--flip|both", -1.0, -1.0, 400, 200, 400, 200)]
    [InlineData("--grayscale", 1.0, 1.0, 0, 0, 400, 200)]
    [InlineData("--invert|--brightness|20", 1.0, 1.0, 0, 0, 400, 200)]
    [InlineData("--crop|20,10,300,150|--resize|150x75", 0.5, 0.5, -10, -5, 150, 75)]
    [InlineData("--pad|10|--flip|h", -1.0, 1.0, 410, 10, 420, 220)]
    public void RenderCommand_Run_ScadAnchorsThroughPipelineSteps_MoveWithThePixels(
        string arguments, double scaleX, double scaleY, double offsetX, double offsetY, int width, int height)
    {
        // Arrange
        using var directory = new TempDirectory();
        using var baseline = RenderJson(directory.Path, Model);

        // Act
        using var document = RenderJson(directory.Path, Model, arguments.Split('|'));

        // Assert: the written image has the expected size and every anchor moved the same way
        var output = document.RootElement.GetProperty("outputs")[0];
        Assert.Equal(width, output.GetProperty("width").GetInt32());
        Assert.Equal(height, output.GetProperty("height").GetInt32());
        foreach (var name in new[] { "upperLeft", "lowerRight", "center" })
        {
            var (x, y) = PixelOf(baseline, name);
            var expected = ((x * scaleX) + offsetX, (y * scaleY) + offsetY);
            AssertNear(expected, PixelOf(document, name), 0.01);
            Assert.Equal(expected.Item1 >= 0 && expected.Item1 < width && expected.Item2 >= 0 && expected.Item2 < height, InsideOf(document, name));
        }
    }

    /// <summary>
    ///     Test that the right-angle rotations map the pixels with the clockwise convention.
    /// </summary>
    /// <param name="degrees">The rotation.</param>
    [Theory]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void RenderCommand_Run_ScadAnchorsThroughRightAngleRotation_MoveWithThePixels(int degrees)
    {
        // Arrange
        using var directory = new TempDirectory();
        using var baseline = RenderJson(directory.Path, Model);
        var (x, y) = PixelOf(baseline, "upperLeft");

        // Act
        using var document = RenderJson(directory.Path, Model, "--rotate", degrees.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Assert: a clockwise quarter turn sends (x, y) of a w x h image to (h - y, x)
        var expected = degrees switch
        {
            90 => (200 - y, x),
            180 => (400 - x, 200 - y),
            _ => (y, 400 - x),
        };
        AssertNear(expected, PixelOf(document, "upperLeft"));
    }

    /// <summary>
    ///     Test that an arbitrary rotation keeps the anchors inside the expanded canvas and moves the center anchor with
    ///     the center of the image.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadAnchorsThroughArbitraryRotation_StayOnTheExpandedCanvas()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act: the center anchor is at the center of the box, which is the center of the framed picture
        using var document = RenderJson(directory.Path, Model, "--rotate", "30");

        // Assert
        var output = document.RootElement.GetProperty("outputs")[0];
        var width = output.GetProperty("width").GetInt32();
        var height = output.GetProperty("height").GetInt32();
        Assert.True(width > 400 && height > 200);
        AssertNear((width / 2.0, height / 2.0), PixelOf(document, "center"), 1.0);
        Assert.True(InsideOf(document, "upperLeft"));
    }

    /// <summary>
    ///     Test that an anchor that a crop cuts away is reported with its position and as not inside the image.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadAnchorOutsideTheCrop_IsNotInside()
    {
        // Arrange / Act: only the upper left part of the picture is kept
        using var directory = new TempDirectory();
        using var document = RenderJson(directory.Path, Model, "--crop", "0,0,100,50");

        // Assert
        Assert.True(InsideOf(document, "upperLeft"));
        Assert.False(InsideOf(document, "lowerRight"));
        Assert.True(PixelOf(document, "lowerRight").X > 100);
    }

    /// <summary>
    ///     Test that a point behind a perspective eye has a null pixel and is not inside the image, while its world
    ///     point is still reported.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadAnchorBehindThePerspectiveEye_HasNullPixel()
    {
        // Arrange: a front view from the fitted distance; a point 500 units in front of the model is behind the eye
        using var directory = new TempDirectory();
        const string Source = "cube(10, center = true);\necho(\"anchor\", \"behind\", [0, -500, 0]);";

        // Act
        using var document = RenderJsonWith(directory.Path, Source, "--view", "front", "--projection", "perspective", "--size", "200x100");

        // Assert
        var anchor = Assert.Single(document.RootElement.GetProperty("anchors").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, anchor.GetProperty("pixel").ValueKind);
        Assert.False(anchor.GetProperty("inside").GetBoolean());
        Assert.Equal(-500.0, anchor.GetProperty("point")[1].GetDouble());
    }
}
