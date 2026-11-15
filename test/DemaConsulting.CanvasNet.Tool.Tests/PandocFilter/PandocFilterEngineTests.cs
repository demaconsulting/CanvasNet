using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the PandocFilterEngine unit, with a stub drawer: the cache, duplicate pictures, diagrams left as
///     written, denials, drawings with nothing to flatten, the shapes of a code block, determinism, and which error is
///     reported.
/// </summary>
public class PandocFilterEngineTests
{
    /// <summary>
    ///     Test that a picture is drawn once, that a second run finds it in the cache without drawing, and that its
    ///     <c>src</c> is relative to the HTML output with the other attributes kept.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_CacheHit_DrawsNothing()
    {
        // Arrange: a diagram picture with a theme and a width
        using var tree = new FilterTree();
        tree.Write("flow.mmd", "flowchart LR");
        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("flow.mmd", ("theme", "dark"), ("width", "50%"))));
        var first = new StubDrawer();
        var second = new StubDrawer();

        // Act
        var output = tree.Run(first, document);
        var again = tree.Run(second, document);

        // Assert: one render, then none; the same document both times
        Assert.Equal(["render flow.mmd"], first.Calls);
        Assert.Empty(second.Calls);
        Assert.Equal(output, again);
        var src = Assert.Single(PandocDoc.ImageSources(output));
        Assert.Matches("^../docs/generated/canvasnet/flow-[0-9a-f]{40}\\.png$", src);
        Assert.Contains("""[["width","50%"]]""", Encoding.UTF8.GetString(output));
    }

    /// <summary>
    ///     Test that two pictures with the same key are drawn once and point at the same file, without the second one
    ///     repeating the tool's notes.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_DuplicateKey_DrawsOnce()
    {
        // Arrange: the same diagram twice; the tool reports a note
        using var tree = new FilterTree();
        tree.Write("flow.mmd", "flowchart LR");
        var drawer = new StubDrawer { Report = _ => new PictureDrawing(true, [new("mermaid-note", "a note", false)], ["echo"]) };
        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("flow.mmd"), PandocDoc.Image("flow.mmd")));

        // Act
        var output = tree.Run(drawer, document, jobs: 4);

        // Assert
        Assert.Single(drawer.Calls);
        var sources = PandocDoc.ImageSources(output);
        Assert.Equal(sources[0], sources[1]);
        Assert.Equal(["flow.mmd: a note", "flow.mmd: echo"], PandocDoc.Warnings(output));
    }

    /// <summary>
    ///     Test a diagram type the tool does not draw: the block is left as written with a warning, a marker is written, a
    ///     second run warns from the marker without drawing, and the same report about an embedded picture is an ordinary
    ///     warning.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_UnsupportedDiagram_LeftAsWrittenWithMarker()
    {
        // Arrange: a journey block, and a flowchart picture whose embedded picture is a journey
        using var tree = new FilterTree();
        tree.Write("outer.mmd", "flowchart LR");
        var drawer = new StubDrawer
        {
            Report = name => name == "outer.mmd"
                ? new PictureDrawing(true, [new("mermaid-diagram-unsupported", "inner.mmd: journey is not drawn", true)], [])
                : new PictureDrawing(true, [new("mermaid-diagram-unsupported", "journey is not drawn", false)], [])
        };
        var block = PandocDoc.CodeBlock("", ["mermaid"], "journey\n  title A day");
        var document = PandocDoc.Document(block, PandocDoc.Para(PandocDoc.Image("outer.mmd")));

        // Act
        var output = tree.Run(drawer, document);
        var again = tree.Run(new StubDrawer(), document);

        // Assert: the block stays (images run before blocks, so its warning comes second), the outer picture is drawn
        Assert.Contains(block, Encoding.UTF8.GetString(output));
        Assert.Equal(["outer.mmd: inner.mmd: journey is not drawn", "mermaid block (journey): journey is not drawn; it is left as written and not drawn"], PandocDoc.Warnings(output));
        Assert.Single(tree.CacheFiles(), name => name.EndsWith(".unsupported", StringComparison.Ordinal));
        Assert.Equal(["mermaid block (journey): journey is not drawn; it is left as written and not drawn"], PandocDoc.Warnings(again));
        Assert.DoesNotContain(tree.CacheFiles(), name => name.Contains(".partial", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that in a restrictive mode a denied picture stops the build with the Lua filter's message, and the partial
    ///     file is removed.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_RestrictiveDenial_StopsTheBuild()
    {
        // Arrange: the mode roots, and a tool that reports a denied picture
        using var tree = new FilterTree();
        tree.Write("flow.mmd", "flowchart LR");
        tree.Environment[FilterSettings.FileAccessVariable] = "roots";
        var drawer = new StubDrawer { Report = _ => new PictureDrawing(true, [new("image-access-denied", "'x.png' is outside the roots", false)], []) };

        // Act
        var error = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("flow.mmd")))));

        // Assert
        Assert.Equal(ErrorCodes.FileAccessDenied, error.Code);
        Assert.Equal("drawing 'flow.mmd' failed: 'x.png' is outside the roots (denied by CANVASNET_FILE_ACCESS/CANVASNET_ROOTS)", error.Message);
        Assert.DoesNotContain(tree.CacheFiles(), name => name.Contains(".partial", StringComparison.Ordinal) || name.EndsWith(".png", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test a drawing the tool finds nothing to flatten in: left alone, its warnings on one line each kept in a marker
    ///     and shown again by the next run without starting the tool; a drawing that is flattened points at the copy.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_SvgDrawings_PlainMarkerAndFlattenedCopy()
    {
        // Arrange: two drawings that link a diagram; the tool flattens only the second
        using var tree = new FilterTree();
        tree.Write("flow.mmd", "flowchart LR");
        tree.Write("plain.svg", "<svg><image href='flow.mmd'/></svg>");
        tree.Write("flat.svg", "<svg><image href='flow.mmd' x='1'/></svg>");
        var drawer = new StubDrawer
        {
            Report = name => name == "plain.svg"
                ? new PictureDrawing(false, [new("svg-flatten-image-not-drawn", "line one\r\nline two", false)], [])
                : new PictureDrawing(true, [], [])
        };
        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("plain.svg", ("scale", "2")), PandocDoc.Image("flat.svg", ("scale", "2"), ("width", "9"))));

        // Act
        var output = tree.Run(drawer, document);
        var again = new StubDrawer();
        var second = tree.Run(again, document);

        // Assert: the plain drawing is left as written, the other points at its copy and keeps width only
        Assert.Equal(["flatten flat.svg", "flatten plain.svg"], drawer.Calls.Order(StringComparer.Ordinal));
        var sources = PandocDoc.ImageSources(output);
        Assert.Equal("plain.svg", sources[0]);
        Assert.Matches("^../docs/generated/canvasnet/flat-[0-9a-f]{40}\\.svg$", sources[1]);
        Assert.Contains("""[["width","9"]]""", Encoding.UTF8.GetString(output));
        Assert.Equal(["plain.svg: line one line two"], PandocDoc.Warnings(output));
        Assert.Empty(again.Calls);
        Assert.Equal(output, second);
    }

    /// <summary>
    ///     Test the shapes a code block becomes: a figure with the caption placeholder and the id on the figure, or a
    ///     paragraph with the id on the picture; other classes and attributes stay on the picture.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MermaidBlocks_BecomeFigureOrParagraph()
    {
        // Arrange: a captioned block and a plain one
        using var tree = new FilterTree();
        var captioned = PandocDoc.CodeBlock("fig-a", ["mermaid", "wide"], "flowchart LR\n  A --> B", ("caption", "The *flow*"), ("width", "80%"), ("theme", "dark"));
        var plain = PandocDoc.CodeBlock("b", ["mermaid"], "flowchart TD\n  C --> D");
        var drawer = new StubDrawer();

        // Act
        var output = tree.Run(drawer, PandocDoc.Document(captioned, plain));

        // Assert: the shapes, and the text of each block written to the cache
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        var figure = blocks[0];
        Assert.Equal("Figure", figure.GetProperty("t").GetString());
        Assert.Equal("fig-a", figure.GetProperty("c")[0][0].GetString());
        Assert.Equal("""[{"t":"Plain","c":[{"t":"Span","c":[["",["canvasnet-markdown"],[["markdown","The *flow*"]]],[]]}]}]""", figure.GetProperty("c")[1][1].GetRawText());
        var image = figure.GetProperty("c")[2][0].GetProperty("c")[0];
        Assert.Equal("""["",["wide"],[["width","80%"]]]""", image.GetProperty("c")[0].GetRawText());
        var para = blocks[1];
        Assert.Equal("Para", para.GetProperty("t").GetString());
        Assert.Equal("""["b",[],[]]""", para.GetProperty("c")[0].GetProperty("c")[0].GetRawText());
        Assert.Equal("[]", para.GetProperty("c")[0].GetProperty("c")[1].GetRawText());
        Assert.Equal(2, tree.CacheFiles().Count(name => name.EndsWith(".mmd", StringComparison.Ordinal)));
        Assert.Equal(2, drawer.Calls.Count);
    }

    /// <summary>
    ///     Test that one job and four jobs give the same document, warnings, and cache files.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_OneAndFourJobs_AreIdentical()
    {
        // Arrange: eight diagrams, each with a note
        using var tree = new FilterTree();
        var images = Enumerable.Range(0, 8).Select(i =>
        {
            tree.Write($"d{i}.mmd", $"flowchart LR\n  A{i} --> B");
            return PandocDoc.Image($"d{i}.mmd");
        }).ToArray();
        var document = PandocDoc.Document(PandocDoc.Para(images), PandocDoc.CodeBlock("", ["mermaid"], "flowchart LR\n  X --> Y"));
        var drawer = new StubDrawer { Report = name => new PictureDrawing(true, [new("note", "drawn " + name, false)], []) };

        // Act: one job, then four jobs over an empty cache
        var sequential = tree.Run(drawer, document, jobs: 1);
        var sequentialFiles = tree.CacheFiles();
        Directory.Delete(tree.Cache, true);
        var parallel = tree.Run(drawer, document, jobs: 4);

        // Assert
        Assert.Equal(sequential, parallel);
        Assert.Equal(sequentialFiles, tree.CacheFiles());
        Assert.Equal(9, PandocDoc.Warnings(parallel).Count);
    }

    /// <summary>
    ///     Test that the failure of the lowest position is reported, whatever the number of jobs: the second of two failing
    ///     pictures never wins.
    /// </summary>
    /// <param name="jobs">The pictures drawn at once.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void PandocFilterEngine_Run_TwoFailures_ReportsTheLowestPosition(int jobs)
    {
        // Arrange: seven diagrams; the third and the sixth fail
        using var tree = new FilterTree();
        var images = Enumerable.Range(0, 7).Select(i =>
        {
            tree.Write($"d{i}.mmd", $"flowchart LR\n  A{i}");
            return PandocDoc.Image($"d{i}.mmd");
        }).ToArray();
        var drawer = new StubDrawer { Failure = name => name is "d2.mmd" or "d5.mmd" ? new ViewerException(ErrorCodes.MermaidError, $"{name} is broken", "Fix it.") : null };

        // Act
        var error = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(images)), jobs));

        // Assert: the tool's code, its message and hint after the Lua filter's prefix
        Assert.Equal(ErrorCodes.MermaidError, error.Code);
        Assert.Equal("drawing 'd2.mmd' failed: d2.mmd is broken (Fix it.)", error.Message);
        Assert.Contains("render d2.mmd", drawer.Calls);
        Assert.Contains("render d0.mmd", drawer.Calls);
        Assert.Contains("render d1.mmd", drawer.Calls);
    }

    /// <summary>
    ///     Test the order of a planning error and a drawing failure: an earlier drawing failure wins over a later attribute
    ///     error, and an earlier attribute error wins over a later drawing failure.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_PlanningAndDrawingFailures_LowestPositionWins()
    {
        // Arrange: a failing diagram, a bad attribute, and the same in the other order
        using var tree = new FilterTree();
        tree.Write("bad.mmd", "flowchart LR");
        tree.Write("good.mmd", "flowchart TD");
        var drawer = new StubDrawer { Failure = name => name == "bad.mmd" ? new ViewerException(ErrorCodes.MermaidError, "broken") : null };
        var drawingFirst = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("bad.mmd"), PandocDoc.Image("good.mmd", ("view", "top"))));
        var planningFirst = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("good.mmd", ("view", "top")), PandocDoc.Image("bad.mmd")));

        // Act
        var first = Assert.Throws<ToolException>(() => tree.Run(drawer, drawingFirst, jobs: 4));
        var second = Assert.Throws<ToolException>(() => tree.Run(drawer, planningFirst, jobs: 4));

        // Assert
        Assert.Equal("drawing 'bad.mmd' failed: broken", first.Message);
        Assert.Equal("'good.mmd' has view=top, but view applies to .scad models only", second.Message);
        Assert.Equal(ErrorCodes.PandocFilterError, second.Code);
    }

    /// <summary>
    ///     Test that a missing source is an error naming it, that a missing drawing and other pictures are left to Pandoc,
    ///     and that non-ASCII text passes through.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MissingSourcesAndOtherPictures_FollowTheLuaFilter()
    {
        // Arrange: a missing model, and a document of pictures the filter leaves alone
        using var tree = new FilterTree();
        var drawer = new StubDrawer();
        var untouched = PandocDoc.Document(
            PandocDoc.Para(PandocDoc.Image("gone.svg"), PandocDoc.Image("photo.png"), PandocDoc.Image("https://example.com/a.scad"), """{"t":"Str","c":"Grüße"}"""),
            PandocDoc.CodeBlock("", ["python"], "print()"));

        // Act
        var error = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("gone%20model.scad")))));
        var output = tree.Run(drawer, untouched);

        // Assert
        Assert.Equal("cannot find 'gone%20model.scad' in the document's resource path", error.Message);
        Assert.Equal(untouched, output);
        Assert.Empty(drawer.Calls);
    }
}
