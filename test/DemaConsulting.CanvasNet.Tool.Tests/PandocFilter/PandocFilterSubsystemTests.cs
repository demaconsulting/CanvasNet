using System.Text.Json;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Subsystem tests for the PandocFilter subsystem: real pictures drawn by the tool's commands through the engine.
/// </summary>
public class PandocFilterSubsystemTests
{
    /// <summary>
    ///     Test that a document of every kind of picture is drawn by the tool, as figures and pictures in the cache, and
    ///     that one job and four jobs give the same document and the same cache files.
    /// </summary>
    [Fact]
    public void PandocFilter_Run_RealPictures_DrawnTheSameWithOneOrFourJobs()
    {
        // Arrange: a model, a chart, a diagram, a drawing that links the diagram, and a captioned diagram block
        using var tree = new FilterTree();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), Path.Combine(tree.Guide, "cube.scad"));
        File.Copy(FixtureFactory.FixturePath("baskets.vl.json"), Path.Combine(tree.Guide, "baskets.vl.json"));
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), Path.Combine(tree.Guide, "flow.mmd"));
        tree.Write("drawing.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='60' height='40'><image href='flow.mmd' width='60' height='40'/></svg>");
        var document = PandocDoc.Document(
            PandocDoc.Para(
                PandocDoc.Image("cube.scad", ("size", "32x24")),
                PandocDoc.Image("baskets.vl.json"),
                PandocDoc.Image("flow.mmd", ("theme", "forest")),
                PandocDoc.Image("drawing.svg")),
            PandocDoc.CodeBlock("fig-order", ["mermaid"], "flowchart LR\n  O[Order] --> P[Pay]", ("caption", "The order")));
        var drawer = new ToolPictureDrawer();

        // Act: one job, then four jobs over an empty cache
        var sequential = tree.Run(drawer, document, jobs: 1);
        var files = tree.CacheFiles();
        Directory.Delete(tree.Cache, true);
        var parallel = tree.Run(drawer, document, jobs: 4);

        // Assert: the same document and files either way, no warnings, and the model at the size asked for
        Assert.Equal(sequential, parallel);
        Assert.Equal(files, tree.CacheFiles());
        Assert.Empty(PandocDoc.Warnings(parallel));
        Assert.Equal(5, PandocDoc.ImageSources(parallel).Count);
        Assert.Contains(files, name => name.StartsWith("baskets-", StringComparison.Ordinal) && name.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains(files, name => name.StartsWith("drawing-", StringComparison.Ordinal) && name.EndsWith(".svg", StringComparison.Ordinal));
        using var model = PngCodec.Load(Path.Combine(tree.Cache, files.Single(name => name.StartsWith("cube-", StringComparison.Ordinal))));
        Assert.Equal((32, 24), (model.Width, model.Height));
        using var json = JsonDocument.Parse(parallel);
        Assert.Equal("Figure", json.RootElement.GetProperty("blocks")[1].GetProperty("t").GetString());
    }
}
