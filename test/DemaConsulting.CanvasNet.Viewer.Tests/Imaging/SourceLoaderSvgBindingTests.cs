using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

/// <summary>
///     Unit tests for the SVG position bindings of the SourceLoader class: a binding to a Mermaid diagram, a Vega-Lite chart
///     or an OpenSCAD model lands where the picture drawn alone at the size of its image reports the feature, plus the
///     image origin; every failure is a warning with its code, and <c>--strict</c> fails with <c>svg-binding-failed</c>
///     after the pictures that were not drawn.
/// </summary>
public class SourceLoaderSvgBindingTests
{
    /// <summary>A flowchart with two nodes.</summary>
    private const string Flowchart = "flowchart LR\n    A[Start] --> B[End]\n";

    /// <summary>A bar chart with two bars.</summary>
    private const string BarChart =
        "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\","
        + "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}},"
        + "\"width\":200,\"height\":100}";

    /// <summary>A cube with one anchor at a corner of its top face.</summary>
    private const string Model = "cube(10);\necho(\"anchor\", \"corner\", [10, 10, 10]);\n";

    /// <summary>
    ///     Writes a 400 by 200 SVG file whose image <c>pic</c> places a file at 20,30 in a 300 by 120 box, with a line
    ///     bound to a selector of it.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="href">The link of the image.</param>
    /// <param name="selector">The selector of the binding.</param>
    /// <param name="name">The name of the SVG file.</param>
    /// <returns>The path of the SVG file.</returns>
    private static string WriteFigure(string directory, string href, string selector, string name = "figure.svg")
    {
        return FixtureFactory.WriteText(
            directory,
            name,
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:cn='https://demaconsulting.com/canvasnet' width='400' height='200' viewBox='0 0 400 200'>"
            + $"<image id='pic' href='{href}' x='20' y='30' width='300' height='120'/>"
            + $"<line x1='0' y1='0' x2='1' y2='1' stroke='black' cn:x2y2='#pic {selector}'/></svg>");
    }

    /// <summary>
    ///     Gets the written x and y of the single <c>bind</c> operation of a load.
    /// </summary>
    /// <param name="loaded">The loaded source.</param>
    /// <returns>The coordinates.</returns>
    private static (double X, double Y) Bound(LoadedSource loaded)
    {
        var bind = Assert.Single(loaded.Operations, o => o.Op == "bind");
        return ((double)bind.Fields.Single(f => f.Key == "x").Value, (double)bind.Fields.Single(f => f.Key == "y").Value);
    }

    /// <summary>
    ///     Gets the pixel of the single position a selector picks when a file is drawn alone at 300 by 120.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="selector">The selector.</param>
    /// <returns>The pixel.</returns>
    private static PositionPoint Alone(string path, string selector)
    {
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 300, Height = 120, Positions = PositionSelection.Parse([selector]) });
        return Assert.Single(loaded.Positions).Pixel!.Value;
    }

    /// <summary>
    ///     Test that a binding to a node of a Mermaid diagram is written at the pixel the diagram reports alone at the size
    ///     of its image, plus the image origin, as the operation <c>bind</c>, without a <c>positions-unmatched</c> note.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgBindingToMermaid_LandsOnTheNode()
    {
        // Arrange
        using var directory = new TempDirectory();
        var diagram = FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        var path = WriteFigure(directory.Path, "flow.mmd", "node:B");
        var expected = Alone(diagram, "node:B");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });

        // Assert
        var (x, y) = Bound(loaded);
        Assert.Equal(expected.X + 20, x, 3);
        Assert.Equal(expected.Y + 30, y, 3);
        Assert.Empty(loaded.Diagnostics);
        var bind = Assert.Single(loaded.Operations, o => o.Op == "bind");
        Assert.Equal(["element", "attribute", "image", "selector", "x", "y"], bind.Fields.Select(f => f.Key));
    }

    /// <summary>
    ///     Test that a binding to a bar of a Vega-Lite chart lands on the bar.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgBindingToVegaLite_LandsOnTheMark()
    {
        // Arrange
        using var directory = new TempDirectory();
        var chart = FixtureFactory.WriteText(directory.Path, "bars.vl.json", BarChart);
        var path = WriteFigure(directory.Path, "bars.vl.json", "mark:x=B");
        var expected = Alone(chart, "mark:x=B");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert
        var (x, y) = Bound(loaded);
        Assert.Equal(expected.X + 20, x, 3);
        Assert.Equal(expected.Y + 30, y, 3);
    }

    /// <summary>
    ///     Test that a binding to an anchor of a model (kind <c>anchor</c>, id the anchor name) lands on the anchor.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgBindingToScadAnchor_LandsOnTheAnchor()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = FixtureFactory.WriteText(directory.Path, "part.scad", Model);
        var path = WriteFigure(directory.Path, "part.scad", "anchor:corner");
        using var alone = SourceLoader.Load(model, new SourceOptions { Width = 300, Height = 120 });
        var anchor = Assert.Single(alone.Anchors);

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());

        // Assert
        var (x, y) = Bound(loaded);
        Assert.Equal(anchor.PixelX!.Value + 20, x, 3);
        Assert.Equal(anchor.PixelY!.Value + 30, y, 3);
    }

    /// <summary>
    ///     Test that a selector that matches nothing is the warning <c>svg-binding-unmatched</c> (and not
    ///     <c>positions-unmatched</c>), and with <c>--strict</c> the error <c>svg-binding-failed</c>.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgBindingUnmatched_WarnsOrFailsStrictly()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        var path = WriteFigure(directory.Path, "flow.mmd", "node:Zed");

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions());
        var error = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { Strict = true }));

        // Assert
        var note = Assert.Single(loaded.Diagnostics);
        Assert.Equal(("svg", "warning", SvgPositionBinding.UnmatchedCode), (note.Source, note.Severity, note.Code));
        Assert.DoesNotContain(loaded.Operations, o => o.Op == "bind");
        Assert.Equal(ErrorCodes.SvgBindingFailed, error.Code);
        Assert.Contains("figure.svg", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a picture the file-access policy denies keeps its <c>image-access-denied</c> warning, its binding is
    ///     <c>svg-binding-unavailable</c>, and with <c>--strict</c> the error is still <c>file-access-denied</c>.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgBindingToDeniedPicture_KeepsAccessDenied()
    {
        // Arrange: the diagram is outside the only allowed folder
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        Directory.CreateDirectory(directory.Combine("site"));
        var path = WriteFigure(directory.Combine("site"), "../flow.mmd", "node:B");
        var policy = FileAccessPolicy.UnderRoots(directory.Combine("site"));

        // Act
        using var loaded = SourceLoader.Load(path, new SourceOptions { FileAccess = policy });
        var error = Assert.Throws<ViewerException>(() => SourceLoader.Load(path, new SourceOptions { FileAccess = policy, Strict = true }));

        // Assert
        Assert.Equal([FolderImageResolver.AccessDeniedCode, SvgPositionBinding.UnavailableCode], loaded.Diagnostics.Select(d => d.Code));
        Assert.Equal(ErrorCodes.FileAccessDenied, error.Code);
    }

    /// <summary>
    ///     Test that a binding inside a nested SVG file resolves against the images of that file, and that one that fails
    ///     is a warning of the outer render that names it, and a strict failure.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_NestedSvgBinding_ResolvesOrFailsStrictly()
    {
        // Arrange: inner.svg binds to its own image of the diagram; broken.svg binds to a node that does not exist
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        WriteFigure(directory.Path, "flow.mmd", "node:B", "inner.svg");
        WriteFigure(directory.Path, "flow.mmd", "node:Zed", "broken.svg");
        var good = FixtureFactory.WriteText(directory.Path, "good.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200'><image href='inner.svg' width='400' height='200'/></svg>");
        var bad = FixtureFactory.WriteText(directory.Path, "bad.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='400' height='200'><image href='broken.svg' width='400' height='200'/></svg>");

        // Act
        using var resolved = SourceLoader.Load(good, new SourceOptions { Strict = true });
        using var warned = SourceLoader.Load(bad, new SourceOptions());
        var error = Assert.Throws<ViewerException>(() => SourceLoader.Load(bad, new SourceOptions { Strict = true }));

        // Assert
        Assert.Empty(resolved.Diagnostics);
        var note = Assert.Single(warned.Diagnostics);
        Assert.Equal(SvgPositionBinding.UnmatchedCode, note.Code);
        Assert.Equal(["broken.svg"], note.Origin);
        Assert.Equal(ErrorCodes.SvgBindingFailed, error.Code);
        Assert.Contains("broken.svg", error.Message, StringComparison.Ordinal);
    }
}
