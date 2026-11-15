using System.Globalization;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the position bindings of the SvgFlattener unit: the numbers of a resolved binding are baked from the
///     captured picture (its size and the positions recorded when it was drawn) and its <c>cn:</c> attribute is removed,
///     with every other byte kept; a binding that is not resolved keeps its attribute and numbers and is a warning.
/// </summary>
public class SvgFlattenerBindingTests
{
    /// <summary>A flowchart with two nodes.</summary>
    private const string Flowchart = "flowchart LR\n    A[Start] --> B[End]\n";

    /// <summary>The pattern of the PNG data URI of the copy.</summary>
    private static readonly Regex DataUri = new("data:image/png;base64,[A-Za-z0-9+/=]+", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Writes a 400 by 200 drawing whose image <c>pic</c> places <c>flow.mmd</c> at 20,30 in a 300 by 120 box.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="body">The markup after the image.</param>
    /// <param name="prefix">The prefix of the CanvasNet namespace.</param>
    /// <returns>The path and the text of the drawing.</returns>
    private static (string Path, string Text) WriteDrawing(TempDirectory directory, string body, string prefix = "cn")
    {
        FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        var text = $"<svg xmlns='http://www.w3.org/2000/svg' xmlns:{prefix}='https://demaconsulting.com/canvasnet' width='400' height='200' viewBox='0 0 400 200'>\n"
                   + "  <image id='pic' href='flow.mmd' x='20' y='30' width='300' height='120'/>\n"
                   + $"  {body}\n</svg>\n";
        return (FixtureFactory.WriteText(directory.Path, "figure.svg", text), text);
    }

    /// <summary>
    ///     Computes, independently of the flattener, where a node of the diagram lands in the copy: the PNG picture of the
    ///     copy is drawn alone at its own size, and its pixel is fitted into the 300 by 120 placement at 20,30 the way an
    ///     SVG reader fits the picture (<c>xMidYMid meet</c>).
    /// </summary>
    /// <param name="directory">The folder of the diagram.</param>
    /// <param name="copy">The text of the copy.</param>
    /// <param name="node">The node id.</param>
    /// <returns>The expected coordinates, formatted as the copy writes them.</returns>
    private static (string X, string Y) Expected(TempDirectory directory, string copy, string node)
    {
        var bytes = Convert.FromBase64String(DataUri.Match(copy).Value[SvgFlattener.DataUriPrefix.Length..]);
        using var stream = new MemoryStream(bytes);
        var info = PngCodec.GetInfo(stream);
        using var alone = SourceLoader.Load(
            directory.Combine("flow.mmd"),
            new SourceOptions { Width = info.Width, Height = info.Height, Positions = PositionSelection.Parse(["node:" + node]) });
        var pixel = Assert.Single(alone.Positions).Pixel!.Value;
        var scale = Math.Min(300.0 / info.Width, 120.0 / info.Height);
        var x = 20 + ((300 - (info.Width * scale)) / 2) + (pixel.X * scale);
        var y = 30 + ((120 - (info.Height * scale)) / 2) + (pixel.Y * scale);
        static string Format(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture);
        return (Format(x), Format(y));
    }

    /// <summary>
    ///     Flattens a drawing next to it.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="path">The drawing.</param>
    /// <param name="strict"><see langword="true"/> for the strict policy.</param>
    /// <returns>The result.</returns>
    private static FlattenResult Flatten(TempDirectory directory, string path, bool strict = false)
    {
        return SvgFlattener.Execute(SourceLoader.Probe(path), new FlattenRequest(directory.Combine("flat.svg")) { Strict = strict });
    }

    /// <summary>
    ///     Test that a resolved binding writes the numbers of the captured picture into the existing coordinates, removes
    ///     its attribute and the namespace declaration, counts as a change, and keeps every other byte.
    /// </summary>
    /// <param name="prefix">The prefix of the CanvasNet namespace.</param>
    [Theory]
    [InlineData("cn")]
    [InlineData("q")]
    public void SvgFlattener_Execute_ResolvedBinding_IsBaked(string prefix)
    {
        // Arrange
        using var directory = new TempDirectory();
        var (path, text) = WriteDrawing(directory, $"<line x1='0' y1='0' x2='1' y2='1' {prefix}:x2y2='#pic node:B'/>", prefix);

        // Act
        var result = Flatten(directory, path);
        var copy = File.ReadAllText(directory.Combine("flat.svg"));

        // Assert: the copy is the drawing with the link, the two numbers, the binding, and the declaration changed
        var (x, y) = Expected(directory, copy, "B");
        var expected = text
            .Replace($" xmlns:{prefix}='https://demaconsulting.com/canvasnet'", string.Empty, StringComparison.Ordinal)
            .Replace("href='flow.mmd'", $"href='{DataUri.Match(copy).Value}'", StringComparison.Ordinal)
            .Replace($"x2='1' y2='1' {prefix}:x2y2='#pic node:B'", $"x2='{x}' y2='{y}'", StringComparison.Ordinal);
        Assert.Equal(expected, copy);
        Assert.True(result.Flattened);
        Assert.Equal((1, 0), (result.ResolvedBindings, result.UnresolvedBindings));
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Test that coordinates the element does not have are inserted where the binding was, with its quote.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_MissingCoordinates_AreInserted()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (path, _) = WriteDrawing(directory, "<circle r='2' cn:center='#pic node:A'/>");

        // Act
        var result = Flatten(directory, path);
        var copy = File.ReadAllText(directory.Combine("flat.svg"));

        // Assert
        var (x, y) = Expected(directory, copy, "A");
        Assert.Contains($"<circle r='2' cx='{x}' cy='{y}'/>", copy, StringComparison.Ordinal);
        Assert.Equal(1, result.ResolvedBindings);
    }

    /// <summary>
    ///     Test that a binding that is not resolved keeps its attribute, its numbers and the declaration, and is a warning,
    ///     while the other binding is baked.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_UnresolvedBinding_IsKeptAndWarned()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (path, _) = WriteDrawing(directory, "<line x1='0' y1='0' x2='1' y2='1' cn:x2y2='#pic node:Zed'/><circle r='2' cn:center='#pic node:A'/>");

        // Act
        var result = Flatten(directory, path);
        var copy = File.ReadAllText(directory.Combine("flat.svg"));

        // Assert
        Assert.Contains("xmlns:cn='https://demaconsulting.com/canvasnet'", copy, StringComparison.Ordinal);
        Assert.Contains("x2='1' y2='1' cn:x2y2='#pic node:Zed'", copy, StringComparison.Ordinal);
        Assert.DoesNotContain("cn:center", copy, StringComparison.Ordinal);
        Assert.Equal((1, 1), (result.ResolvedBindings, result.UnresolvedBindings));
        Assert.Equal(SvgPositionBinding.UnmatchedCode, Assert.Single(result.Diagnostics).Code);
    }

    /// <summary>
    ///     Test that a binding that is not resolved fails a strict flatten before anything is written.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_StrictUnresolvedBinding_WritesNothing()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (path, _) = WriteDrawing(directory, "<line x1='0' y1='0' x2='1' y2='1' cn:x2y2='#pic node:Zed'/>");

        // Act
        var error = Assert.Throws<ViewerException>(() => Flatten(directory, path, strict: true));

        // Assert
        Assert.Equal(ErrorCodes.SvgBindingFailed, error.Code);
        Assert.False(File.Exists(directory.Combine("flat.svg")));
    }

    /// <summary>
    ///     Test that a binding to an image that a <c>use</c> draws again is unsupported: the fallback is kept and warned.
    /// </summary>
    [Fact]
    public void SvgFlattener_Execute_BindingToUsedImage_IsKept()
    {
        // Arrange
        using var directory = new TempDirectory();
        var (path, _) = WriteDrawing(directory, "<use href='#pic' x='5'/><line x1='0' y1='0' x2='1' y2='1' cn:x2y2='#pic node:A'/>");

        // Act
        var result = Flatten(directory, path);
        var copy = File.ReadAllText(directory.Combine("flat.svg"));

        // Assert
        Assert.Contains("cn:x2y2='#pic node:A'", copy, StringComparison.Ordinal);
        Assert.Equal((0, 1), (result.ResolvedBindings, result.UnresolvedBindings));
        Assert.Equal(SvgPositionBinding.UnsupportedCode, Assert.Single(result.Diagnostics).Code);
    }
}
