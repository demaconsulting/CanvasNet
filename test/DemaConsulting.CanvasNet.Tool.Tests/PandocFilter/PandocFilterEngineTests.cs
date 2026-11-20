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
    ///     Test that a code block of class <c>plantuml</c> is written to the cache as a <c>.puml</c> file and becomes a
    ///     figure with its own class dropped and the others kept, that a PlantUML type the tool does not draw is left as
    ///     written with a warning, and that the block's name in a message names the kind.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_PlantUmlBlocks_BecomeFigureOrAreLeftAsWritten()
    {
        // Arrange: a captioned sequence block, and a legacy activity diagram the tool reports as not drawn
        using var tree = new FilterTree();
        var captioned = PandocDoc.CodeBlock("fig-s", ["plantuml", "wide"], "@startuml\nAlice -> Bob\n@enduml", ("caption", "The talk"), ("scale", "2"));
        var unsupported = PandocDoc.CodeBlock("", ["plantuml"], "@startuml\n(*) --> First\n@enduml");
        var legacyFile = Path.GetFileName(PictureCache.BlockSourceFile("cache", "@startuml\n(*) --> First\n@enduml\n", PictureAttributes.PlantUml));
        var reporting = new StubDrawer
        {
            Report = name => name == legacyFile
                ? new PictureDrawing(true, [new("plantuml-diagram-unsupported", "activity-legacy diagrams are not drawn", false)], [])
                : new PictureDrawing(true, [], [])
        };

        // Act
        var output = tree.Run(reporting, PandocDoc.Document(captioned, unsupported));

        // Assert: the figure with the kind's class dropped, and the unsupported block as written with its warning
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        Assert.Equal("Figure", blocks[0].GetProperty("t").GetString());
        var image = blocks[0].GetProperty("c")[2][0].GetProperty("c")[0];
        Assert.Equal("""["",["wide"],[]]""", image.GetProperty("c")[0].GetRawText());
        Assert.Equal("CodeBlock", blocks[1].GetProperty("t").GetString());
        Assert.Equal(["plantuml block (@startuml): activity-legacy diagrams are not drawn; it is left as written and not drawn"], PandocDoc.Warnings(output));
        Assert.Equal(2, tree.CacheFiles().Count(name => name.EndsWith(".puml", StringComparison.Ordinal)));
        Assert.Single(tree.CacheFiles(), name => name.EndsWith(".unsupported", StringComparison.Ordinal));
        Assert.Equal(2, reporting.Calls.Count);
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
    ///     Test that a failed run leaves the same cache files for one job and four: with four jobs, later pictures are drawn
    ///     while an earlier one is still failing, but nothing after the first failure is committed into the cache and no
    ///     temporary file is left.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_FailureWithOneAndFourJobs_LeavesTheSameCache()
    {
        // Arrange: six diagrams; the second fails, and with four jobs it fails only once the fourth has been drawn
        using var tree = new FilterTree();
        var images = Enumerable.Range(0, 6).Select(i =>
        {
            tree.Write($"d{i}.mmd", $"flowchart LR\n  A{i}");
            return PandocDoc.Image($"d{i}.mmd");
        }).ToArray();
        var document = PandocDoc.Document(PandocDoc.Para(images));
        static Exception Broken() => new ViewerException(ErrorCodes.MermaidError, "broken");
        var sequentialDrawer = new StubDrawer { Failure = name => name == "d1.mmd" ? Broken() : null };
        using var laterDrawn = new ManualResetEventSlim();
        var parallelDrawer = new StubDrawer
        {
            Failure = name =>
            {
                if (name != "d1.mmd")
                {
                    return null;
                }

                laterDrawn.Wait();
                return Broken();
            },
            Report = name =>
            {
                if (name == "d3.mmd")
                {
                    laterDrawn.Set();
                }

                return new PictureDrawing(true, [], []);
            }
        };

        // Act: one job, then four jobs over an empty cache
        var sequential = Assert.Throws<ToolException>(() => tree.Run(sequentialDrawer, document, jobs: 1));
        var sequentialFiles = tree.CacheFiles();
        Directory.Delete(tree.Cache, true);
        var parallel = Assert.Throws<ToolException>(() => tree.Run(parallelDrawer, document, jobs: 4));

        // Assert: the same failure and cache files (the picture of d0 only), though four jobs drew d2 and d3
        Assert.Equal(sequential.Message, parallel.Message);
        Assert.Equal(sequential.Code, parallel.Code);
        Assert.Equal(sequentialFiles, tree.CacheFiles());
        Assert.Single(sequentialFiles);
        Assert.StartsWith("d0-", sequentialFiles[0], StringComparison.Ordinal);
        Assert.Contains("render d3.mmd", parallelDrawer.Calls);
        Assert.DoesNotContain("render d3.mmd", sequentialDrawer.Calls);
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
    ///     Test that a failed run keeps the warnings of the pictures before the failure, in document order and whatever the
    ///     number of jobs, without those of later pictures, and that a run that succeeds has none.
    /// </summary>
    /// <param name="jobs">The pictures drawn at once.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void PandocFilterEngine_Run_FailureAfterWarnings_KeepsTheEarlierWarnings(int jobs)
    {
        // Arrange: four diagrams that each report a note; the third fails
        using var tree = new FilterTree();
        var images = Enumerable.Range(0, 4).Select(i =>
        {
            tree.Write($"d{i}.mmd", $"flowchart LR\n  A{i}");
            return PandocDoc.Image($"d{i}.mmd");
        }).ToArray();
        var drawer = new StubDrawer
        {
            Report = name => new PictureDrawing(true, [new("mermaid-note", $"note of {name}", false)], []),
            Failure = name => name == "d2.mmd" ? new ViewerException(ErrorCodes.MermaidError, "broken") : null
        };
        var engine = new PandocFilterEngine(tree.Settings(), drawer, jobs);

        // Act: a run that fails, then a run of the same engine that succeeds
        Assert.Throws<ToolException>(() => engine.Run(PandocDoc.Document(PandocDoc.Para(images)), "3.12.1"));
        var earlier = engine.WarningsBeforeFailure;
        engine.Run(PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("d0.mmd"))), "3.12.1");

        // Assert
        Assert.Equal(["d0.mmd: note of d0.mmd", "d1.mmd: note of d1.mmd"], earlier);
        Assert.Empty(engine.WarningsBeforeFailure);
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

    /// <summary>
    ///     Test that every picture and code block the engine does not draw is passed through untouched without a draw: the
    ///     rasters and extension-less paths outside <see cref="PictureAttributes.PictureSuffixes"/>, a supported name
    ///     followed by a fragment or a query (the name must end in the suffix), remote pictures, and code blocks without
    ///     one of <see cref="PictureAttributes.BlockClasses"/> (the class is exact, so <c>Mermaid</c> is not one).
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_PicturesItDoesNotDraw_PassThroughUntouched()
    {
        // Arrange: none of the sources exist, so a picture the engine tried to draw would fail the run
        using var tree = new FilterTree();
        var drawer = new StubDrawer();
        var images = new[]
        {
            "a.bmp", "scan.tif", "favicon.ico", "photo.avif", "photo.webp", "photo.JPG", "diagram", "docs/README",
            "report.pdf#page=2", "chart.vl.json?x", "https://example.com/flow.mmd", "data:,drawing.svg"
        }.Select(src => PandocDoc.Image(src)).ToArray();
        var untouched = PandocDoc.Document(
            PandocDoc.Para(images),
            PandocDoc.CodeBlock("", ["bash"], "echo hi"),
            PandocDoc.CodeBlock("", ["yaml"], "a: 1"),
            PandocDoc.CodeBlock("", ["csharp"], "var x = 1;"),
            PandocDoc.CodeBlock("", ["text"], "plain"),
            PandocDoc.CodeBlock("", ["Mermaid"], "flowchart LR"),
            PandocDoc.CodeBlock("", [], "no class"));

        // Act
        var output = tree.Run(drawer, untouched);

        // Assert
        Assert.Equal(untouched, output);
        Assert.Empty(drawer.Calls);
        Assert.Empty(tree.CacheFiles());
    }

    /// <summary>
    ///     Test that a picture larger than the maximum image size whose error carries the scale that fits gets a hint that
    ///     names the <c>scale</c> attribute (of the block, or of the picture) instead of the command-line options, and that a
    ///     size-limit error without that scale keeps the tool's hint.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_OversizedPicture_HintNamesTheScaleAttribute()
    {
        // Arrange: a size-limit error with the scale that fits, as the viewer raises it, and one without
        using var tree = new FilterTree();
        tree.Write("wide.mmd", "flowchart LR\n  A");
        tree.Write("tall.mmd", "flowchart TD\n  B");
        var drawer = new StubDrawer
        {
            Failure = name =>
            {
                var error = new ViewerException(ErrorCodes.SizeLimit, $"'{name}' is too large; a scale of 0.56 or less fits.", "Use --scale 0.56 (or less).");
                if (name != "tall.mmd")
                {
                    error.Data["DemaConsulting.CanvasNet.Viewer.FitScale"] = "0.56";
                }

                return error;
            }
        };

        // Act
        var block = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.CodeBlock("", ["mermaid"], "flowchart TD\n  C"))));
        var image = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("wide.mmd")))));
        var plain = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("tall.mmd")))));

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, block.Code);
        Assert.EndsWith(
            "is too large; a scale of 0.56 or less fits. (Set the block's scale attribute to 0.56 or less, for example {.mermaid scale=0.56}.)",
            block.Message,
            StringComparison.Ordinal);
        Assert.StartsWith("drawing 'mermaid block (flowchart TD)' failed: ", block.Message, StringComparison.Ordinal);
        Assert.Equal(
            "drawing 'wide.mmd' failed: 'wide.mmd' is too large; a scale of 0.56 or less fits. (Set the picture's scale attribute to 0.56 or less, for example {scale=0.56}.)",
            image.Message);
        Assert.Equal("drawing 'tall.mmd' failed: 'tall.mmd' is too large; a scale of 0.56 or less fits. (Use --scale 0.56 (or less).)", plain.Message);
    }
}
