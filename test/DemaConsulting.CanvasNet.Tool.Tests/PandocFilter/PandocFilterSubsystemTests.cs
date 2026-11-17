using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
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

    /// <summary>
    ///     Test that a second run over an unchanged document draws nothing and gives the same document, and that a changed
    ///     file the picture reads draws it again.
    /// </summary>
    [Fact]
    public void PandocFilter_Run_UnchangedAndChangedSources_DrawOnlyWhatChanged()
    {
        // Arrange: a model that includes a second file
        using var tree = new FilterTree();
        tree.Write("part.scad", "include <size.scad>\ncube(edge);");
        tree.Write("size.scad", "edge = 5;");
        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("part.scad", ("size", "32x24"))));
        var drawer = new CountingDrawer();

        // Act: draw, draw again, change the included file, and draw again
        var first = tree.Run(drawer, document);
        var afterFirst = drawer.Count;
        var second = tree.Run(drawer, document);
        var afterSecond = drawer.Count;
        tree.Write("size.scad", "edge = 7;");
        var third = tree.Run(drawer, document);

        // Assert: one drawing, then none, then one more with a new picture
        Assert.Equal(1, afterFirst);
        Assert.Equal(1, afterSecond);
        Assert.Equal(first, second);
        Assert.Equal(2, drawer.Count);
        Assert.NotEqual(PandocDoc.ImageSources(first), PandocDoc.ImageSources(third));
        Assert.Equal(2, tree.CacheFiles().Count(name => name.EndsWith(".png", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that a mistake in a picture stops the run with the filter's error and a message that names the picture, and
    ///     that a picture the filter does not handle is left to Pandoc.
    /// </summary>
    [Fact]
    public void PandocFilter_Run_PictureMistakes_StopTheRunNamingThePicture()
    {
        // Arrange: a model with a bad attribute, a picture that is not in the resource path, and a plain raster
        using var tree = new FilterTree();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), Path.Combine(tree.Guide, "cube.scad"));
        var drawer = new ToolPictureDrawer();
        var raster = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("logo.png")));

        // Act
        var badAttribute = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("cube.scad", ("view", "sideways"))))));
        var missing = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("gone.mmd")))));
        var untouched = tree.Run(drawer, raster);

        // Assert
        Assert.Equal("pandoc-filter-error", badAttribute.Code);
        Assert.StartsWith("'cube.scad' has view=sideways", badAttribute.Message, StringComparison.Ordinal);
        Assert.Equal("pandoc-filter-error", missing.Code);
        Assert.Equal("cannot find 'gone.mmd' in the document's resource path", missing.Message);
        Assert.Equal(["logo.png"], PandocDoc.ImageSources(untouched));
        Assert.Empty(tree.CacheFiles());
    }

    /// <summary>
    ///     Test that a picture a restrictive file-access mode denies stops the run with file-access-denied, and that the
    ///     same picture is drawn when the variables allow its folder.
    /// </summary>
    [Fact]
    public void PandocFilter_Run_FileAccessVariables_ConfineWhatAPictureReads()
    {
        // Arrange: a diagram whose node shows a picture in the folder above the guide
        using var tree = new FilterTree();
        tree.Write("flow.mmd", "flowchart LR\n  A@{ img: \"../outside.png\", w: 20, h: 20 }");
        using (var surface = new Surface(4, 4))
        {
            PngCodec.Save(surface, Path.Combine(tree.Root, "docs", "outside.png"));
        }

        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("flow.mmd")));
        var drawer = new ToolPictureDrawer();

        // Act: only the guide folder is allowed, then the docs folder is
        tree.Environment[FilterSettings.FileAccessVariable] = "roots";
        tree.Environment[FilterSettings.RootsVariable] = tree.Guide;
        var denied = Assert.Throws<ToolException>(() => tree.Run(drawer, document));
        tree.Environment[FilterSettings.RootsVariable] = Path.Combine(tree.Root, "docs");
        var allowed = tree.Run(drawer, document);

        // Assert
        Assert.Equal("file-access-denied", denied.Code);
        Assert.StartsWith("drawing 'flow.mmd' failed: ", denied.Message, StringComparison.Ordinal);
        Assert.Empty(PandocDoc.Warnings(allowed));
        Assert.StartsWith("flow-", Path.GetFileName(Assert.Single(PandocDoc.ImageSources(allowed))), StringComparison.Ordinal);
        Assert.Single(tree.CacheFiles(), name => name.EndsWith(".png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the shim the tool writes speaks a protocol the tool accepts, and that a shim of an older protocol is
    ///     told to write the shim again at its own path.
    /// </summary>
    [Fact]
    public void PandocFilter_Shim_WrittenShimSpeaksTheProtocolTheToolAccepts()
    {
        // Arrange
        using var tree = new FilterTree();
        var path = Path.Combine(tree.Root, "docs", "filters", "canvasnet.lua");

        // Act: write the shim, read the protocol it passes, and check it and the one before it
        PandocFilterShim.Write(path);
        var text = File.ReadAllText(path);
        var protocol = int.Parse(Regex.Match(text, "local PROTOCOL = \"(\\d+)\"", RegexOptions.None, TimeSpan.FromSeconds(5)).Groups[1].Value, CultureInfo.InvariantCulture);
        var older = Assert.Throws<ToolException>(() => PandocFilterShim.CheckProtocol(protocol - 1, path));

        // Assert: the shim passes its protocol with every call and the tool accepts it
        Assert.Contains("\"--protocol\", PROTOCOL", text, StringComparison.Ordinal);
        PandocFilterShim.CheckProtocol(protocol, path);
        Assert.Equal("usage-error", older.Code);
        Assert.Equal($"Write the shim again: dotnet canvasnet pandoc-filter --write-shim {path}", older.Hint);
    }

    /// <summary>
    ///     Test that a Mermaid block that lays out larger than the maximum image size stops the run with size-limit, the
    ///     laid-out size, the limit, and the <c>scale</c> attribute that fits, and that the block draws with that scale.
    /// </summary>
    [Fact]
    public void PandocFilter_Run_OversizedMermaidBlock_SuggestsTheScaleAttribute()
    {
        // Arrange: a flowchart of a long chain, far wider than 8192 pixels
        using var tree = new FilterTree();
        var text = "flowchart LR\n" + string.Join("\n", Enumerable.Range(0, 150).Select(i => $"  N{i} --> N{i + 1}"));
        var drawer = new ToolPictureDrawer();

        // Act
        var error = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.CodeBlock("", ["mermaid"], text))));
        var fit = Regex.Match(error.Message, @"\{\.mermaid scale=([0-9.]+)\}", RegexOptions.None, TimeSpan.FromSeconds(5)).Groups[1].Value;
        var drawn = tree.Run(drawer, PandocDoc.Document(PandocDoc.CodeBlock("", ["mermaid"], text, ("scale", fit))));

        // Assert
        Assert.Equal("size-limit", error.Code);
        Assert.StartsWith("drawing 'mermaid block (flowchart LR)' failed: ", error.Message, StringComparison.Ordinal);
        Assert.Contains($" pixels, which is larger than the maximum of 8192 pixels per side; a scale of {fit} or less fits. ", error.Message, StringComparison.Ordinal);
        Assert.Contains($"(Set the block's scale attribute to {fit} or less, for example {{.mermaid scale={fit}}}.)", error.Message, StringComparison.Ordinal);
        Assert.Single(PandocDoc.ImageSources(drawn));
    }

    /// <summary>
    ///     A drawer that draws with the tool and counts the pictures it drew.
    /// </summary>
    private sealed class CountingDrawer : IPictureDrawer
    {
        /// <summary>
        ///     The drawer of the tool.
        /// </summary>
        private readonly ToolPictureDrawer _tool = new();

        /// <summary>
        ///     The number of pictures drawn.
        /// </summary>
        private int _count;

        /// <summary>
        ///     Gets the number of pictures drawn.
        /// </summary>
        public int Count => _count;

        /// <inheritdoc />
        public PictureDrawing Render(IReadOnlyList<string> args)
        {
            Interlocked.Increment(ref _count);
            return _tool.Render(args);
        }

        /// <inheritdoc />
        public PictureDrawing Flatten(IReadOnlyList<string> args)
        {
            Interlocked.Increment(ref _count);
            return _tool.Flatten(args);
        }
    }
}
