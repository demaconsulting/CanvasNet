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
    ///     Test that a <c>.mml</c> picture and a code block of class <c>mathml</c> are drawn at scale 2 and shown at their
    ///     natural width (the width of a probe at scale 1, in em of the 16-pixel font: <c>width="Nem"</c>), the block as a
    ///     figure from a <c>.mml</c> file in the cache, and that a second run adds the same width from the cache without
    ///     drawing.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MathMlPictureAndBlock_AreDrawnAtTheirNaturalWidth()
    {
        // Arrange: Ohm's law as a picture, the quadratic formula as a captioned block, and their natural widths
        using var tree = new FilterTree();
        var ohm = tree.Write("ohm.mml", "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>V</mi><mo>=</mo><mi>I</mi><mi>R</mi></math>");
        var quadratic = File.ReadAllText(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml"))).ReplaceLineEndings("\n").TrimEnd('\n');
        var ohmWidth = SourceLoader.Probe(ohm).Width;
        var quadraticWidth = SourceLoader.Probe(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml"))).Width;
        var document = PandocDoc.Document(
            PandocDoc.Para(PandocDoc.Image("ohm.mml", ("id", "ohm"))),
            PandocDoc.CodeBlock("eq-q", ["mathml"], quadratic, ("caption", "The roots")));
        var drawer = new StubDrawer();
        var again = new StubDrawer();

        // Act
        var output = tree.Run(drawer, document);
        var second = tree.Run(again, document);

        // Assert: both drawn at scale 2 (the default when no size or scale is given)
        Assert.Equal(2, drawer.Calls.Count);
        Assert.All(drawer.Arguments, args => Assert.Contains("--scale=2", args));
        Assert.Single(tree.CacheFiles(), name => name.EndsWith(".mml", StringComparison.Ordinal));

        // Assert: the picture keeps its id and gets its natural width; the block is a figure whose picture does too
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        var picture = blocks[0].GetProperty("c")[0].GetProperty("c")[0];
        Assert.Equal($$"""["",[],[["id","ohm"],["width","{{PictureAttributes.Em(ohmWidth)}}"]]]""", picture.GetRawText());
        Assert.Equal("Figure", blocks[1].GetProperty("t").GetString());
        var figureImage = blocks[1].GetProperty("c")[2][0].GetProperty("c")[0];
        Assert.Equal($$"""["",[],[["width","{{PictureAttributes.Em(quadraticWidth)}}"]]]""", figureImage.GetProperty("c")[0].GetRawText());
        Assert.True(quadraticWidth > ohmWidth);

        // Assert: a second run draws nothing and gives the same document
        Assert.Empty(again.Calls);
        Assert.Equal(output, second);
    }

    /// <summary>
    ///     Test that an author's <c>width</c>, <c>height</c>, <c>size</c>, or sizing <c>style</c> on an equation is kept
    ///     without a natural width, that an author's <c>scale</c> replaces the default but still shows the natural width,
    ///     and that an equation that cannot be measured keeps its attributes as written (the draw reports it).
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MathMlWithWidth_KeepsTheAuthorsWidth()
    {
        // Arrange: one equation shown six ways, and a text that is not an equation, each in a paragraph of its own
        using var tree = new FilterTree();
        var path = tree.Write("eq.mml", "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi><mo>=</mo><mn>1</mn></math>");
        tree.Write("broken.mml", "<math><mi>x</mo></math>");
        var natural = SourceLoader.Probe(path).Width;
        var document = PandocDoc.Document(
            PandocDoc.Para(PandocDoc.Image("eq.mml", ("width", "50%"))),
            PandocDoc.Para(PandocDoc.Image("eq.mml", ("height", "2em"))),
            PandocDoc.Para(PandocDoc.Image("eq.mml", ("size", "400x100"))),
            PandocDoc.Para(PandocDoc.Image("eq.mml", ("scale", "3"))),
            PandocDoc.Para(PandocDoc.Image("eq.mml", ("style", "width:3em"))),
            PandocDoc.Para(PandocDoc.Image("broken.mml")));
        var drawer = new StubDrawer();

        // Act
        var output = tree.Run(drawer, document);

        // Assert: the attributes each picture keeps
        using var json = JsonDocument.Parse(output);
        var images = json.RootElement.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("c")[0].GetProperty("c")[0].GetRawText()).ToList();
        Assert.Equal(
            [
                """["",[],[["width","50%"]]]""",
                """["",[],[["height","2em"]]]""",
                """["",[],[]]""",
                $$"""["",[],[["width","{{PictureAttributes.Em(natural)}}"]]]""",
                """["",[],[["style","width:3em"]]]""",
                """["",[],[]]"""
            ],
            images);

        // Assert: the options each draw was given (the width, height, and style pictures share one draw at scale 2)
        var options = drawer.Arguments.Select(args => string.Join(' ', args.Where(a => a.StartsWith("--s", StringComparison.Ordinal)))).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["--scale=2", "--scale=2", "--scale=3", "--size=400x100"], options);
    }

    /// <summary>
    ///     Test that the natural width the engine adds is not part of the cache key: a plain <c>.mml</c> picture and the
    ///     same picture with an author's <c>width</c> share one draw and one cache file, and only the plain one gets the
    ///     natural width.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MathMlPlainAndWithWidth_ShareOneDrawAndCacheFile()
    {
        // Arrange: one equation shown plainly and at half the width of the page, each in a paragraph of its own
        using var tree = new FilterTree();
        var path = tree.Write("eq.mml", "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi><mo>=</mo><mn>1</mn></math>");
        var natural = SourceLoader.Probe(path).Width;
        var document = PandocDoc.Document(PandocDoc.Para(PandocDoc.Image("eq.mml")), PandocDoc.Para(PandocDoc.Image("eq.mml", ("width", "50%"))));
        var drawer = new StubDrawer();

        // Act
        var output = tree.Run(drawer, document);

        // Assert: one draw at scale 2 and one picture in the cache, which both pictures show
        Assert.Equal(["render eq.mml"], drawer.Calls);
        Assert.Contains("--scale=2", Assert.Single(drawer.Arguments));
        var cached = Assert.Single(tree.CacheFiles(), name => name.EndsWith(".png", StringComparison.Ordinal));
        var sources = PandocDoc.ImageSources(output);
        Assert.Equal(2, sources.Count);
        Assert.Equal(sources[0], sources[1]);
        Assert.EndsWith(cached, sources[0], StringComparison.Ordinal);

        // Assert: the plain picture gets the natural width and the other keeps the author's width
        using var json = JsonDocument.Parse(output);
        var images = json.RootElement.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("c")[0].GetProperty("c")[0].GetRawText()).ToList();
        Assert.Equal([$$"""["",[],[["width","{{PictureAttributes.Em(natural)}}"]]]""", """["",[],[["width","50%"]]]"""], images);
    }

    /// <summary>
    ///     Test that an equation in a sentence is shown at its natural width and placed in the line with its baseline on
    ///     the text's (a <c>style</c> before the author's own declarations), that the same equation alone in its paragraph
    ///     gets the width only, and that a second run gives the same document from the cache without drawing.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MathMlInASentence_SitsOnTheBaseline()
    {
        // Arrange: y = x², which reaches below its baseline, in a sentence with the author's color and on its own
        using var tree = new FilterTree();
        var path = tree.Write("eq.mml", "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>y</mi><mo>=</mo><msup><mi>x</mi><mn>2</mn></msup></math>");
        var natural = SourceLoader.Probe(path);
        var depth = natural.Height - natural.Equation!.Baseline;
        var document = PandocDoc.Document(
            PandocDoc.Para(PandocDoc.Str("Then"), PandocDoc.Space(), PandocDoc.Image("eq.mml", ("style", "color:red")), PandocDoc.Str(".")),
            PandocDoc.Para(PandocDoc.Image("eq.mml")));
        var drawer = new StubDrawer();
        var again = new StubDrawer();

        // Act
        var output = tree.Run(drawer, document);
        var second = tree.Run(again, document);

        // Assert: one draw, and the equation's depth below the baseline
        Assert.Single(drawer.Calls);
        Assert.True(depth > 0);

        // Assert: the sentence's equation is placed in the line, the author's style after the filter's; the lone one is not
        var width = PictureAttributes.Em(natural.Width);
        var align = PictureAttributes.FormatNumber(-(double)depth / PictureAttributes.MathMlFontSize);
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        Assert.Equal(
            $$"""["",[],[["style","display:inline;margin:0;vertical-align:{{align}}em;color:red"],["width","{{width}}"]]]""",
            blocks[0].GetProperty("c")[2].GetProperty("c")[0].GetRawText());
        Assert.Equal($$"""["",[],[["width","{{width}}"]]]""", blocks[1].GetProperty("c")[0].GetProperty("c")[0].GetRawText());

        // Assert: a second run draws nothing and gives the same document
        Assert.Empty(again.Calls);
        Assert.Equal(output, second);
    }

    /// <summary>
    ///     Test that the math elements the shim makes (inline <c>Code</c> of class <c>canvasnet-math</c>) become equation
    ///     pictures: inline math in a sentence sits on the baseline, display math alone in its paragraph gets the width
    ///     only even though it is the same equation, both keep the LaTeX as their description, one draw serves both, and
    ///     a second run draws nothing and gives the same document.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_MathElements_BecomeEquationPictures()
    {
        // Arrange: y = x² as inline math in a sentence and as display math on its own
        using var tree = new FilterTree();
        const string mathMl = "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>y</mi><mo>=</mo><msup><mi>x</mi><mn>2</mn></msup></math>";
        var path = tree.Write("probe.mml", mathMl);
        var natural = SourceLoader.Probe(path);
        var depth = natural.Height - natural.Equation!.Baseline;
        var document = PandocDoc.Document(
            PandocDoc.Para(PandocDoc.Str("Then"), PandocDoc.Space(), PandocDoc.MathCode(mathMl, "y=x^2", false), PandocDoc.Str(".")),
            PandocDoc.Para(PandocDoc.MathCode(mathMl, "y=x^2", true)));
        var drawer = new StubDrawer();
        var again = new StubDrawer();

        // Act
        var output = tree.Run(drawer, document);
        var second = tree.Run(again, document);

        // Assert: one draw, and no Code element is left
        Assert.Single(drawer.Calls);
        Assert.DoesNotContain("canvasnet-math", System.Text.Encoding.UTF8.GetString(output), StringComparison.Ordinal);

        // Assert: the inline picture sits on the baseline, the display picture has the width only, both describe the LaTeX
        var width = PictureAttributes.Em(natural.Width);
        var align = PictureAttributes.FormatNumber(-(double)depth / PictureAttributes.MathMlFontSize);
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        var inline = blocks[0].GetProperty("c")[2];
        var display = blocks[1].GetProperty("c")[0];
        Assert.Equal("Image", inline.GetProperty("t").GetString());
        Assert.Equal($$"""["",[],[["style","display:inline;margin:0;vertical-align:{{align}}em"],["width","{{width}}"]]]""", inline.GetProperty("c")[0].GetRawText());
        Assert.Equal($$"""["",[],[["width","{{width}}"]]]""", display.GetProperty("c")[0].GetRawText());
        Assert.Equal("""[{"t":"Str","c":"y=x^2"}]""", display.GetProperty("c")[1].GetRawText());

        // Assert: a second run draws nothing and gives the same document
        Assert.Empty(again.Calls);
        Assert.Equal(output, second);
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

    /// <summary>
    ///     Test that a code block of class <c>vega-lite</c> or <c>vegalite</c> is written to the cache as a <c>.vl.json</c>
    ///     file and drawn with its parameters, becoming a figure with a caption or a paragraph, with its own class dropped
    ///     and the other classes and attributes kept.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_VegaLiteBlocks_BecomeFigureFromAVlJsonFile()
    {
        // Arrange: a captioned chart with a parameter, and a chart under the alias class
        using var tree = new FilterTree();
        var captioned = PandocDoc.CodeBlock(
            "fig-sales", ["vega-lite", "wide"], "{\"mark\": \"bar\", \"data\": {\"values\": [{\"a\": 1}]}}", ("caption", "Sales"), ("param-cutoff", "3"), ("width", "50%"));
        var alias = PandocDoc.CodeBlock("visits", ["vegalite"], "{\"mark\": \"line\", \"data\": {\"values\": [{\"a\": 2}]}}");
        var drawer = new StubDrawer();

        // Act
        var output = tree.Run(drawer, PandocDoc.Document(captioned, alias));

        // Assert: a figure and a paragraph, each picture without the chart's class
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        Assert.Equal("Figure", blocks[0].GetProperty("t").GetString());
        Assert.Equal("fig-sales", blocks[0].GetProperty("c")[0][0].GetString());
        var image = blocks[0].GetProperty("c")[2][0].GetProperty("c")[0];
        Assert.Equal("""["",["wide"],[["width","50%"]]]""", image.GetProperty("c")[0].GetRawText());
        Assert.Equal("Para", blocks[1].GetProperty("t").GetString());
        Assert.Equal("""["visits",[],[]]""", blocks[1].GetProperty("c")[0].GetProperty("c")[0].GetRawText());
        Assert.All(PandocDoc.ImageSources(output), src => Assert.Matches("^../docs/generated/canvasnet/vegalite-[0-9a-f]{40}\\.png$", src));

        // Assert: both texts written as charts, and the parameter passed to the tool
        Assert.Equal(2, tree.CacheFiles().Count(name => name.EndsWith(".vl.json", StringComparison.Ordinal)));
        Assert.Equal(2, drawer.Calls.Count);
        Assert.All(drawer.Calls, call => Assert.EndsWith(".vl.json", call, StringComparison.Ordinal));
        Assert.Single(drawer.Arguments, args => args.Contains("--param=cutoff=3"));
        Assert.Empty(PandocDoc.Warnings(output));
    }

    /// <summary>
    ///     Test that a code block of class <c>openscad</c> stays code unless it also has the class <c>draw</c> (nothing is
    ///     written or drawn for it), and that a block with both is drawn as a model with the model defaults, becoming a
    ///     picture with <c>openscad</c> and <c>draw</c> dropped and the other classes and attributes kept.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_OpenScadBlocks_DrawnOnlyWithTheDrawClass()
    {
        // Arrange: a plain model shown as code (with an include, which is no error for code), and a model to draw
        using var tree = new FilterTree();
        var plain = PandocDoc.CodeBlock("", ["openscad"], "include <lib.scad>\ncube(10);");
        var drawn = PandocDoc.CodeBlock("part", ["openscad", "draw", "wide"], "cube(10);", ("view", "iso"), ("width", "40%"));
        var drawer = new StubDrawer();

        // Act
        var output = tree.Run(drawer, PandocDoc.Document(plain, drawn));

        // Assert: the plain block as written, and the drawn one as a paragraph that holds the picture
        using var json = JsonDocument.Parse(output);
        var blocks = json.RootElement.GetProperty("blocks");
        Assert.Equal(["CodeBlock", "Para"], blocks.EnumerateArray().Select(b => b.GetProperty("t").GetString()));
        Assert.Equal("""["",["openscad"],[]]""", blocks[0].GetProperty("c")[0].GetRawText());
        Assert.Equal("include <lib.scad>\ncube(10);", blocks[0].GetProperty("c")[1].GetString());
        Assert.Equal("""["part",["wide"],[["width","40%"]]]""", blocks[1].GetProperty("c")[0].GetProperty("c")[0].GetRawText());
        Assert.Matches("^../docs/generated/canvasnet/scad-[0-9a-f]{40}\\.png$", Assert.Single(PandocDoc.ImageSources(output)));

        // Assert: one model written and drawn, with the view and the model defaults
        Assert.Single(tree.CacheFiles(), name => name.EndsWith(".scad", StringComparison.Ordinal));
        var args = Assert.Single(drawer.Arguments);
        Assert.EndsWith(".scad", args[0], StringComparison.Ordinal);
        Assert.Contains("--view=iso", args);
        Assert.Contains("--size=1600x1200", args);
        Assert.Contains("--background=white", args);
        Assert.Contains("--max-triangles=2000000", args);
    }

    /// <summary>
    ///     Test that a Vega-Lite or OpenSCAD block that names a local file stops the build before anything is written or
    ///     drawn, with a message that names the block, the file, its line, and the picture to link instead.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_BlockNamingAFile_StopsWithAHintToUseAPicture()
    {
        // Arrange: a chart that reads a data file, and a model that uses a library
        using var tree = new FilterTree();
        var chart = PandocDoc.CodeBlock("sales", ["vega-lite"], "{\n  \"data\": {\"url\": \"sales.csv\"},\n  \"mark\": \"bar\"\n}");
        var model = PandocDoc.CodeBlock("", ["draw", "openscad"], "use <shapes.scad>\nrounded(5);");
        var drawer = new StubDrawer();

        // Act
        var chartError = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(chart)));
        var modelError = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(model)));

        // Assert: the filter's error, naming the fix, and nothing written or drawn
        Assert.Equal(ErrorCodes.PandocFilterError, chartError.Code);
        Assert.Equal(
            "'vega-lite block #sales' reads the data file 'sales.csv' at line 2, but a code block has no folder to find it in; "
            + "put the chart in a .vl.json file beside the files it names and link it as a picture, for example ![Sales](sales.vl.json)",
            chartError.Message);
        Assert.Equal(ErrorCodes.PandocFilterError, modelError.Code);
        Assert.Equal(
            "'openscad block (use <shapes.scad>)' uses 'shapes.scad' at line 1, but a code block has no folder to find it in; "
            + "put the model in a .scad file beside the files it names and link it as a picture, for example ![Part](part.scad){view=iso}",
            modelError.Message);
        Assert.Empty(drawer.Calls);
        Assert.Empty(tree.CacheFiles());
    }

    /// <summary>
    ///     Test that the size-limit hint of an oversized OpenSCAD block names both classes the block needs, and that of a
    ///     Vega-Lite block names the class the author wrote, so the example can be pasted as is.
    /// </summary>
    [Fact]
    public void PandocFilterEngine_Run_OversizedOpenScadBlock_HintNamesBothClasses()
    {
        // Arrange: every draw fails with a size-limit error that carries the scale that fits
        using var tree = new FilterTree();
        var drawer = new StubDrawer
        {
            Failure = name =>
            {
                var error = new ViewerException(ErrorCodes.SizeLimit, $"'{name}' is too large; a scale of 0.5 or less fits.", "Use --scale 0.5 (or less).");
                error.Data["DemaConsulting.CanvasNet.Viewer.FitScale"] = "0.5";
                return error;
            }
        };

        // Act
        var model = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.CodeBlock("", ["openscad", "draw"], "cube(10);", ("scale", "9")))));
        var chart = Assert.Throws<ToolException>(() => tree.Run(drawer, PandocDoc.Document(PandocDoc.CodeBlock("", ["vegalite"], "{\"mark\": \"bar\"}", ("scale", "9")))));

        // Assert
        Assert.Equal(ErrorCodes.SizeLimit, model.Code);
        Assert.StartsWith("drawing 'openscad block (cube(10);)' failed: ", model.Message, StringComparison.Ordinal);
        Assert.EndsWith("(Set the block's scale attribute to 0.5 or less, for example {.openscad .draw scale=0.5}.)", model.Message, StringComparison.Ordinal);
        Assert.EndsWith("(Set the block's scale attribute to 0.5 or less, for example {.vegalite scale=0.5}.)", chart.Message, StringComparison.Ordinal);
    }
}
