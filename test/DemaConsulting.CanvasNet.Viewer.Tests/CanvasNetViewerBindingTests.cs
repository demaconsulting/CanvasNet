using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests of the position bindings of <see cref="CanvasNetViewer"/>: its built-in resolver reports the positions of the
///     pictures a binding targets, and a strict render fails on a binding that was not resolved.
/// </summary>
public class CanvasNetViewerBindingTests
{
    /// <summary>A flowchart with two nodes.</summary>
    private const string Flowchart = "flowchart LR\n    A[Start] --> B[End]\n";

    /// <summary>A bar chart with two bars.</summary>
    private const string BarChart =
        "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\","
        + "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";

    /// <summary>A cube with one anchor at a corner of its top face.</summary>
    private const string Model = "cube(10);\necho(\"anchor\", \"corner\", [10, 10, 10]);\n";

    /// <summary>
    ///     Test that the built-in resolver of the viewer reports the positions of a diagram, a chart and a model, and none
    ///     for a raster.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_CreateImageResolver_ResolveWithPositions_ReportsByKind()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        FixtureFactory.WriteText(directory.Path, "bars.vl.json", BarChart);
        FixtureFactory.WriteText(directory.Path, "part.scad", Model);
        using (var surface = new Surface(4, 4))
        {
            PngCodec.Save(surface, directory.Combine("dot.png"));
        }

        var resolver = CanvasNetViewer.CreateImageResolver(directory.Path);

        // Act
        ImageResolution? Resolve(string href, string selector) =>
            resolver.ResolveWithPositions(href, 300, 120, PositionSelection.Parse([selector]));
        var diagram = Resolve("flow.mmd", "node");
        var chart = Resolve("bars.vl.json", "mark");
        var model = Resolve("part.scad", "anchor");
        var raster = Resolve("dot.png", "all");

        // Assert
        Assert.Equal(["A", "B"], diagram!.Positions!.Select(p => p.Id));
        Assert.Equal(2, chart!.Positions!.Count);
        Assert.Equal("corner", Assert.Single(model!.Positions!).Id);
        Assert.Null(raster!.Positions);
        foreach (var resolution in new[] { diagram, chart, model, raster })
        {
            resolution.Surface.Dispose();
        }
    }

    /// <summary>
    ///     Test that the direct render of the viewer fails a strict render of an SVG file whose binding is not resolved.
    /// </summary>
    [Fact]
    public void CanvasNetViewer_Render_StrictUnresolvedBinding_Fails()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", Flowchart);
        var path = FixtureFactory.WriteText(
            directory.Path,
            "figure.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:cn='https://demaconsulting.com/canvasnet' width='400' height='200'>"
            + "<image id='pic' href='flow.mmd' x='20' y='30' width='300' height='120'/>"
            + "<line x1='0' y1='0' x2='1' y2='1' cn:x2y2='#pic node:Zed'/></svg>");

        // Act
        using var image = CanvasNetViewer.Render(path);
        var error = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(path, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true }));

        // Assert
        Assert.Contains(image.Diagnostics, d => d.Code == SvgPositionBinding.UnmatchedCode);
        Assert.Equal(ErrorCodes.SvgBindingFailed, error.Code);
    }
}
