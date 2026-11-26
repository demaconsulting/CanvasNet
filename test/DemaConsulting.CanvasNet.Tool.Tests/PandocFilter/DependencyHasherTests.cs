using System.Text;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the DependencyHasher unit: the walks of models, charts, diagrams, and drawings, their limits, and the
///     confinement of a roots mode.
/// </summary>
public class DependencyHasherTests
{
    /// <summary>
    ///     Test that a model's includes and uses are followed relative to the including file, each file once (a cycle ends),
    ///     comments included, labeled from the model's folder, in the order met.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_ModelIncludes_AreHashedOnceInOrder()
    {
        // Arrange: a model that includes a library, which uses the model back and a sibling
        using var directory = new TempDirectory();
        var model = Write(directory, "a.scad", "include <lib/b.scad>\n// include < missing.scad >\ncube(1);");
        Write(directory, "lib/b.scad", "use <../a.scad>\nuse < c.scad >\n");
        Write(directory, "lib/c.scad", "module c() {}");

        // Act
        var parts = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", null);

        // Assert: the labels, and the bytes of the first part
        Assert.Equal(["a.scad", "lib/b.scad", "lib/c.scad"], Labels(parts));
        Assert.Equal("a.scad\ninclude <lib/b.scad>\n// include < missing.scad >\ncube(1);", Encoding.UTF8.GetString(parts[0]));
    }

    /// <summary>
    ///     Test the model limits: an include more than 64 levels deep, and more than 512 files, stop the build.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_ModelLimits_StopTheBuild()
    {
        // Arrange: a chain of 66 models, and a model that includes 512 others
        using var directory = new TempDirectory();
        for (var i = 0; i < 66; i++)
        {
            Write(directory, $"chain/f{i}.scad", $"include <f{i + 1}.scad>");
        }

        var includes = string.Concat(Enumerable.Range(0, 512).Select(i => $"include <wide/w{i}.scad>\n"));
        var wide = Write(directory, "wide.scad", includes);
        for (var i = 0; i < 512; i++)
        {
            Write(directory, $"wide/w{i}.scad", "cube(1);");
        }

        // Act
        var deep = Assert.Throws<ToolException>(() => DependencyHasher.ForPicture("scad", Collapse(directory.Combine("chain", "f0.scad")), "f0.scad", null));
        var many = Assert.Throws<ToolException>(() => DependencyHasher.ForPicture("scad", Collapse(wide), "wide.scad", null));

        // Assert: the Lua filter's messages
        Assert.Equal($"'{Collapse(directory.Combine("chain", "f65.scad"))}' is included more than 64 levels deep", deep.Message);
        Assert.Equal(
            "the model and the files it includes are more than 512 files; the limit is 512, counting the model (every file must be hashed so that a changed one is drawn again)",
            many.Message);
        Assert.Equal(ErrorCodes.PandocFilterError, many.Code);
    }

    /// <summary>
    ///     Test the chart walk: data files with a backslash, a literal percent sign, and a byte order mark; remote URLs
    ///     skipped; the pictures of a mark, a url channel, and the inline rows; a nested chart labeled from its own folder;
    ///     and a chart linked back hashed once.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_Chart_HashesDataPicturesAndNestedCharts()
    {
        // Arrange: a chart with every kind of link, and a nested chart that links back
        using var directory = new TempDirectory();
        var chart = Write(
            directory,
            "a.vl.json",
            "\uFEFF" + """
            {"data":{"url":"data\\x.csv"},"layer":[{"data":{"url":"b%20c.csv"}},{"data":{"url":"https://example.com/r.csv"}},
             {"mark":{"type":"image","url":"sub/b.vl.json"}},
             {"mark":"image","encoding":{"url":{"field":"pic"}},"data":{"values":[{"pic":"img/q.png"},{"pic":"img/q.png"}]}}]}
            """);
        Write(directory, "data/x.csv", "a\n1");
        Write(directory, "b%20c.csv", "a\n2");
        Write(directory, "img/q.png", "png");
        Write(directory, "sub/b.vl.json", """{"data":{"url":"d.csv"},"mark":{"type":"image","url":"../a.vl.json"}}""");
        Write(directory, "sub/d.csv", "a\n3");

        // Act
        var parts = DependencyHasher.ForPicture("vegalite", Collapse(chart), "a.vl.json", null);

        // Assert: the chart, its data files sorted, its pictures (the nested chart with its own files), each once
        Assert.Equal(["a.vl.json", "b%20c.csv", "data/x.csv", "img/q.png", "b.vl.json", "d.csv"], Labels(parts));
    }

    /// <summary>
    ///     Test that a chart's picture link is also taken percent-decoded, as the tool's image resolver opens it: the file
    ///     <c>pic name.png</c> of the link <c>pic%20name.png</c> is hashed once, so changing it changes the chart's key.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_ChartPercentEncodedPicture_ChangesTheKey()
    {
        // Arrange: a chart whose image mark names a picture with an encoded space, and the picture under its decoded name
        using var directory = new TempDirectory();
        var chart = Write(directory, "a.vl.json", """{"mark":{"type":"image","url":"pic%20name.png"}}""");
        var picture = Write(directory, "pic name.png", "one");
        string Key() => PictureCache.ComputeKey("vegalite", "1.0.0\ndefault\n", "file-access\n", [], null, DependencyHasher.ForPicture("vegalite", Collapse(chart), "a.vl.json", null));

        // Act
        var labels = Labels(DependencyHasher.ForPicture("vegalite", Collapse(chart), "a.vl.json", null));
        var before = Key();
        File.WriteAllText(picture, "two");
        var after = Key();

        // Assert
        Assert.Equal(["a.vl.json", "pic name.png"], labels);
        Assert.NotEqual(before, after);
    }

    /// <summary>
    ///     Test the chart limits: a specification nested more than 64 levels deep, and more than 512 data files.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_ChartLimits_StopTheBuild()
    {
        // Arrange: a deep specification, and one that names 513 existing data files
        using var directory = new TempDirectory();
        var deep = Write(directory, "deep.vl.json", "{\"a\":" + new string('[', 66) + new string(']', 66) + "}");
        var urls = string.Join(',', Enumerable.Range(0, 513).Select(i => $$$"""{"data":{"url":"d{{{i}}}.csv"}}"""));
        var many = Write(directory, "many.vl.json", $$"""{"layer":[{{urls}}]}""");
        for (var i = 0; i < 513; i++)
        {
            Write(directory, $"d{i}.csv", "a");
        }

        // Act
        var depth = Assert.Throws<ToolException>(() => DependencyHasher.ForPicture("vegalite", Collapse(deep), "deep.vl.json", null));
        var files = Assert.Throws<ToolException>(() => DependencyHasher.ForPicture("vegalite", Collapse(many), "many.vl.json", null));

        // Assert
        Assert.Equal(
            "the chart specification is nested more than 64 levels deep; every data url must be hashed so that a changed data file is drawn again",
            depth.Message);
        Assert.Equal(
            "'many.vl.json' names 513 data files; the limit is 512 (every data file must be hashed so that a changed one is drawn again)",
            files.Message);
    }

    /// <summary>
    ///     Test that a specification nested deeper than the reader allows is treated like any specification that cannot
    ///     be read (the tool reports it), instead of being read to any depth.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_ChartBeyondTheReaderDepth_IsTreatedAsUnreadable()
    {
        // Arrange: a specification nested 5000 levels deep, beyond the reader's limit of 4096
        using var directory = new TempDirectory();
        var hostile = Write(directory, "hostile.vl.json", "{\"a\":" + new string('[', 5000) + new string(']', 5000) + "}");

        // Act
        var exception = Record.Exception(() => DependencyHasher.ForPicture("vegalite", Collapse(hostile), "hostile.vl.json", null));

        // Assert: no depth error and no crash; the tool reports the specification when it draws it
        Assert.Null(exception);
    }

    /// <summary>
    ///     Test the diagram walk: the img of a shape's data quoted, single-quoted (percent-decoded), and unquoted, and the
    ///     src of an img tag quoted and unquoted in any letter case.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_Diagram_HashesEveryPictureForm()
    {
        // Arrange: a diagram with five forms of picture links
        using var directory = new TempDirectory();
        var diagram = Write(
            directory,
            "flow.mmd",
            "flowchart LR\n  A@{ img: \"p.png\" }\n  B@{ img: 'q%20r.png' }\n  C@{img: s.png , h: 3}\n  D[\"<img src='t.png'>\"]\n  E[\"<IMG SRC=u.png/>\"]\n");
        foreach (var name in new[] { "p.png", "q r.png", "s.png", "t.png", "u.png/" })
        {
            Write(directory, name.TrimEnd('/'), "png");
        }

        // Act
        var parts = DependencyHasher.ForPicture("mermaid", Collapse(diagram), "flow.mmd", null);

        // Assert: the diagram and its pictures, sorted (u.png/ names u.png: the trailing slash is collapsed away)
        Assert.Equal(["flow.mmd", "p.png", "q r.png", "s.png", "t.png", "u.png"], Labels(parts));
    }

    /// <summary>
    ///     Test the PlantUML walk: the three include directives are followed relative to the including file, a quoted path
    ///     loses its quotes, each file is hashed once (a repeated include and a cycle end), and the includes PlantUML never
    ///     follows (the standard library, a URL, a named block, a part of a file) and a missing file add nothing.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_PlantUml_HashesEveryLocalInclude()
    {
        // Arrange: a diagram that includes a fragment, which includes a sibling and the diagram back
        using var directory = new TempDirectory();
        var diagram = Write(
            directory,
            "seq.puml",
            "@startuml\n!include parts/people.iuml\n  !include_once \"parts/style.iuml\"\n!include_many parts/people.iuml\n"
            + "!include <C4/C4_Context>\n!include https://example.com/x.puml\n!include [named]\n!include parts/style.iuml!part\n"
            + "!include missing.iuml\nAlice -> Bob\n@enduml\n");
        Write(directory, "parts/people.iuml", "participant Alice\n!include more/extra.iuml\n");
        Write(directory, "parts/style.iuml", "skinparam arrowColor red\n!include ../seq.puml\n");
        Write(directory, "parts/more/extra.iuml", "participant Bob\n");

        // Act
        var parts = DependencyHasher.ForPicture("plantuml", Collapse(diagram), "seq.puml", null);
        var embedded = DependencyHasher.ForSvg(Collapse(Write(directory, "page.svg", "<svg xmlns='http://www.w3.org/2000/svg'><image href='seq.puml' width='9' height='9'/></svg>")), null);

        // Assert: the diagram and its includes in the order met, labeled from the diagram's folder
        Assert.Equal(["seq.puml", "parts/people.iuml", "parts/more/extra.iuml", "parts/style.iuml"], Labels(parts));

        // Assert: a drawing that links the diagram hashes the diagram and its includes, and must be flattened
        Assert.True(embedded.Composite);
        Assert.Contains("parts/more/extra.iuml", Labels(embedded.Parts));
    }

    /// <summary>
    ///     Test the PlantUML include scanner on its own: the path after the directive, without quotes and trailing space,
    ///     only at the start of a line, and without the includes PlantUML never follows.
    /// </summary>
    [Fact]
    public void DependencyHasher_CollectPlantUmlIncludes_KeepsOnlyLocalFiles()
    {
        // Act
        var paths = DependencyHasher.CollectPlantUmlIncludes(
            "!include a.iuml  \n\t!include_once \"b c.iuml\"\nnote: !include not-a-directive.iuml\n!includeurl http://x\n!include C:/d.iuml\n!include <std>\n");

        // Assert
        Assert.Equal(["a.iuml", "b c.iuml", "C:/d.iuml"], paths);
    }

    /// <summary>
    ///     Test that an equation is hashed by its content only (MathML Core references no other file, so a name in its
    ///     text is never followed), and that a drawing that links an equation hashes it as a leaf and must be flattened.
    /// </summary>
    [Fact]
    public void DependencyHasher_Hash_MathMlPicture_HashesItsContentOnly()
    {
        // Arrange: an equation whose text names files, which exist beside it, and a drawing that links the equation
        using var directory = new TempDirectory();
        const string text = "<math><mtext>see other.mml and p.png</mtext><mglyph src=\"p.png\"/></math>";
        var equation = Write(directory, "eq.mml", text);
        Write(directory, "other.mml", "<math><mi>y</mi></math>");
        Write(directory, "p.png", "not a picture");
        var drawing = Write(directory, "page.svg", "<svg xmlns='http://www.w3.org/2000/svg'><image href='eq.mml' width='9' height='9'/></svg>");

        // Act
        var parts = DependencyHasher.ForPicture("mathml", Collapse(equation), "eq.mml", null);
        var embedded = DependencyHasher.ForSvg(Collapse(drawing), null);

        // Assert: one part, the label and the bytes of the equation
        Assert.Equal(["eq.mml"], Labels(parts));
        Assert.Equal("eq.mml\n" + text, Encoding.UTF8.GetString(parts[0]));

        // Assert: the drawing hashes itself and the equation, nothing the equation names, and is composite
        Assert.True(embedded.Composite);
        Assert.Equal(["page.svg", "eq.mml"], Labels(embedded.Parts));
    }

    /// <summary>
    ///     Test the drawing scanner: comments, character data, processing instructions, and a DOCTYPE hold no links; a
    ///     quoted <c>&gt;</c> does not end a tag; a prefixed href counts only in the XLink namespace; entities are decoded;
    ///     a missing diagram is a missing entry; and the drawing is composite.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForSvg_Scanner_FindsOnlyRealImageLinks()
    {
        // Arrange: a drawing whose decoys would show up as missing entries if they were read
        using var directory = new TempDirectory();
        var drawing = Write(
            directory,
            "drawing.svg",
            """
            <?xml version="1.0"?><!DOCTYPE svg [<!ENTITY e "<image href='d.mmd'/>">]>
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:l="http://www.w3.org/1999/xlink" xmlns:x="urn:other">
            <!-- <image href="c.mmd"/> --><![CDATA[<image href="e.mmd"/>]]><?pi <image href="f.mmd"/>?>
            <g title="a>b"><image href="flow.mmd"/></g>
            <image l:href="flow2.mmd"/><image x:href="no.mmd"/>
            <image href=" a&amp;b.png "/><image href="gone.mmd#x"/>
            </svg>
            """);
        Write(directory, "flow.mmd", "flowchart LR");
        Write(directory, "flow2.mmd", "flowchart LR");
        Write(directory, "a&b.png", "png");

        // Act
        var (parts, composite) = DependencyHasher.ForSvg(Collapse(drawing), null);

        // Assert
        Assert.True(composite);
        Assert.Equal(["drawing.svg", "missing gone.mmd", "a&b.png", "flow.mmd", "flow2.mmd"], Labels(parts));
    }

    /// <summary>
    ///     Test that a drawing of rasters only is plain, and one that links a composite drawing is composite.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForSvg_NestedDrawings_CompositeOnlyThroughADiagram()
    {
        // Arrange: a plain drawing, and an outer drawing that links an inner one that links a diagram
        using var directory = new TempDirectory();
        var plain = Write(directory, "plain.svg", "<svg><image href='a.png'/></svg>");
        Write(directory, "a.png", "png");
        var outer = Write(directory, "outer.svg", "<svg><image href='inner.svg'/></svg>");
        Write(directory, "inner.svg", "<svg><image href='flow.mmd'/></svg>");
        Write(directory, "flow.mmd", "flowchart LR");

        // Act / Assert
        Assert.False(DependencyHasher.ForSvg(Collapse(plain), null).Composite);
        var (parts, composite) = DependencyHasher.ForSvg(Collapse(outer), null);
        Assert.True(composite);
        Assert.Equal(["outer.svg", "inner.svg", "flow.mmd"], Labels(parts));
    }

    /// <summary>
    ///     Test that in a roots mode a file outside the folders is never hashed.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_Roots_SkipFilesOutside()
    {
        // Arrange: a model in docs that includes a file outside docs
        using var directory = new TempDirectory();
        var model = Write(directory, "docs/a.scad", "include <../outside/b.scad>");
        Write(directory, "outside/b.scad", "cube(1);");
        var roots = FileAccessPolicy.UnderRoots(directory.Combine("docs"));

        // Act
        var confined = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", roots);
        var open = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", null);

        // Assert
        Assert.Equal(["a.scad"], Labels(confined));
        Assert.Equal(["a.scad", "../outside/b.scad"], Labels(open));
    }

    /// <summary>
    ///     Test that in a roots mode a link inside the folders that leads outside them is judged by where it leads: the
    ///     file it reaches is not read or hashed, while the same include is hashed in the default mode.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_RootsLinkLeadingOutside_IsNotHashed()
    {
        // Arrange: a model in docs that includes a file through a link in docs to a folder outside docs
        using var directory = new TempDirectory();
        var model = Write(directory, "docs/a.scad", "include <lib/b.scad>\ninclude <c.scad>");
        Write(directory, "docs/c.scad", "cube(2);");
        Write(directory, "outside/b.scad", "cube(1);");
        if (!FilterLinkSupport.TryCreateDirectoryLink(directory.Combine("docs", "lib"), directory.Combine("outside"), out var reason))
        {
            Assert.Skip("Directory links are not available: " + reason);
        }

        var roots = FileAccessPolicy.UnderRoots(directory.Combine("docs"));

        // Act
        var confined = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", roots);
        var open = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", null);

        // Assert: the file behind the link is left out under roots, as the tool denies it
        Assert.Equal(["a.scad", "c.scad"], Labels(confined));
        Assert.Equal(["a.scad", "lib/b.scad", "c.scad"], Labels(open));
    }

    /// <summary>
    ///     Test that under no-symlinks a file reached through a link is not read or hashed, even when the link stays
    ///     inside the folders a roots mode allows.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForSvg_NoSymlinksLinkedFile_IsNotHashed()
    {
        // Arrange: a drawing in docs that links a diagram directly and one through a link to a folder inside docs
        using var directory = new TempDirectory();
        var drawing = Write(directory, "docs/d.svg", "<svg><image href='flow.mmd'/><image href='alias/inner.mmd'/></svg>");
        Write(directory, "docs/flow.mmd", "flowchart LR");
        Write(directory, "docs/real/inner.mmd", "flowchart TD");
        if (!FilterLinkSupport.TryCreateDirectoryLink(directory.Combine("docs", "alias"), directory.Combine("docs", "real"), out var reason))
        {
            Assert.Skip("Directory links are not available: " + reason);
        }

        var docs = directory.Combine("docs");

        // Act
        var (noLinks, composite) = DependencyHasher.ForSvg(Collapse(drawing), FileAccessPolicy.NoSymlinks);
        var both = DependencyHasher.ForSvg(Collapse(drawing), FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(docs), FileAccessPolicy.NoSymlinks));
        var roots = DependencyHasher.ForSvg(Collapse(drawing), FileAccessPolicy.UnderRoots(docs));

        // Assert: the linked diagram is noted as missing (the tool denies it), never read; roots alone follows the link
        Assert.True(composite);
        Assert.Equal(["d.svg", "missing alias/inner.mmd", "flow.mmd"], Labels(noLinks));
        Assert.Equal(["d.svg", "missing alias/inner.mmd", "flow.mmd"], Labels(both.Parts));
        Assert.Equal(["d.svg", "alias/inner.mmd", "flow.mmd"], Labels(roots.Parts));
    }

    /// <summary>
    ///     Test that a nested drawing the mode denies makes the drawing that links it composite, so the flatten reports the
    ///     denial, without the nested drawing being read; a missing nested drawing keeps the drawing plain, as before.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForSvg_DeniedNestedDrawing_MakesTheDrawingComposite()
    {
        // Arrange: drawings in docs that link a plain drawing outside docs, and a drawing in docs that does not exist
        using var directory = new TempDirectory();
        var outer = Write(directory, "docs/outer.svg", "<svg><image href='../outside/inner.svg'/></svg>");
        var gone = Write(directory, "docs/gone.svg", "<svg><image href='inner.svg'/></svg>");
        Write(directory, "outside/inner.svg", "<svg><image href='a.png'/></svg>");
        Write(directory, "outside/a.png", "png");
        var roots = FileAccessPolicy.UnderRoots(directory.Combine("docs"));

        // Act
        var denied = DependencyHasher.ForSvg(Collapse(outer), roots);
        var open = DependencyHasher.ForSvg(Collapse(outer), null);
        var missing = DependencyHasher.ForSvg(Collapse(gone), roots);

        // Assert: denied, the drawing is composite and the nested drawing is not hashed; by default it is plain
        Assert.True(denied.Composite);
        Assert.Equal(["outer.svg"], Labels(denied.Parts));
        Assert.False(open.Composite);
        Assert.Equal(["outer.svg", "../outside/inner.svg", "../outside/a.png"], Labels(open.Parts));
        Assert.False(missing.Composite);
        Assert.Equal(["gone.svg"], Labels(missing.Parts));
    }

    /// <summary>
    ///     Test that under no-symlinks a nested drawing reached through a link makes the drawing that links it composite.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForSvg_NoSymlinksLinkedNestedDrawing_MakesTheDrawingComposite()
    {
        // Arrange: a drawing that links a plain drawing through a directory link inside its folder
        using var directory = new TempDirectory();
        var outer = Write(directory, "docs/outer.svg", "<svg><image href='alias/inner.svg'/></svg>");
        Write(directory, "docs/real/inner.svg", "<svg><image href='a.png'/></svg>");
        Write(directory, "docs/real/a.png", "png");
        if (!FilterLinkSupport.TryCreateDirectoryLink(directory.Combine("docs", "alias"), directory.Combine("docs", "real"), out var reason))
        {
            Assert.Skip("Directory links are not available: " + reason);
        }

        // Act
        var (parts, composite) = DependencyHasher.ForSvg(Collapse(outer), FileAccessPolicy.NoSymlinks);

        // Assert
        Assert.True(composite);
        Assert.Equal(["outer.svg"], Labels(parts));
        Assert.False(DependencyHasher.ForSvg(Collapse(outer), null).Composite);
    }

    /// <summary>
    ///     Test that a restrictive mode hashes the files it allows exactly as the default mode does, so the parts of the key
    ///     do not change.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_RestrictiveMode_HashesAllowedFilesIdentically()
    {
        // Arrange: a model with an include, and a chart with a data file and a nested diagram with a picture
        using var directory = new TempDirectory();
        var model = Write(directory, "docs/a.scad", "include <lib/b.scad>\ncube(1);");
        Write(directory, "docs/lib/b.scad", "module b() {}");
        var chart = Write(directory, "docs/c.vl.json", """{"data":{"url":"d.csv"},"mark":{"type":"image","url":"f.mmd"}}""");
        Write(directory, "docs/d.csv", "a\n1");
        Write(directory, "docs/f.mmd", "flowchart LR\n  A@{ img: \"p.png\" }\n");
        Write(directory, "docs/p.png", "png");
        var policy = FileAccessPolicy.Combine(FileAccessPolicy.UnderRoots(directory.Combine("docs")), FileAccessPolicy.NoSymlinks);

        // Act
        var modelParts = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", policy);
        var chartParts = DependencyHasher.ForPicture("vegalite", Collapse(chart), "c.vl.json", policy);

        // Assert: the same parts, byte for byte, as without a policy
        Assert.Equal(DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", null), modelParts);
        Assert.Equal(DependencyHasher.ForPicture("vegalite", Collapse(chart), "c.vl.json", null), chartParts);
        Assert.Equal(["c.vl.json", "d.csv", "f.mmd", "p.png"], Labels(chartParts));
    }

    /// <summary>
    ///     Test that the policy for arguments the tool refuses reads no referenced file, while the source is still read.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_DenyAll_ReadsOnlyTheSource()
    {
        // Arrange
        using var directory = new TempDirectory();
        var model = Write(directory, "a.scad", "include <b.scad>");
        Write(directory, "b.scad", "cube(1);");

        // Act
        var parts = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", DependencyHasher.DenyAll);

        // Assert
        Assert.Equal(["a.scad"], Labels(parts));
    }

    /// <summary>
    ///     Test that an unreadable document page is an error.
    /// </summary>
    [Fact]
    public void DependencyHasher_ForPicture_MissingDocument_ThrowsCannotRead()
    {
        using var directory = new TempDirectory();
        var error = Assert.Throws<ToolException>(() => DependencyHasher.ForPicture("pdf", Collapse(directory.Combine("gone.pdf")), "gone.pdf", null));
        Assert.Equal("cannot read 'gone.pdf'", error.Message);
    }

    /// <summary>
    ///     Test that the files a Vega-Lite block names are found as the chart walk would follow them, the first in the
    ///     order of the text with its line: a local data URL, the link of an image mark, and a link in the inline rows of a
    ///     url field; that remote links, a <c>data:</c> URI, inline values, and text that is not a JSON object are not.
    /// </summary>
    [Fact]
    public void DependencyHasher_FindBlockReference_VegaLite_FindsLocalDataAndPicturesOnly()
    {
        // Arrange: charts that name local files, and charts that name none
        const string data = "{\"data\": {\"url\": \"sales.csv\"}, \"mark\": \"bar\"}";
        const string mark = "{\n  \"data\": {\"values\": [{\"a\": 1}]},\n  \"mark\": {\"type\": \"image\", \"url\": \"logo.png\"}\n}";
        const string rows = "{\n  \"data\": {\"values\": [{\"img\": \"pics/a%20b.png\"}]},\n  \"mark\": \"image\",\n  \"encoding\": {\"url\": {\"field\": \"img\"}},\n  \"layer\": [{\"data\": {\"url\": \"later.csv\"}}]\n}";
        string[] none =
        [
            "{\"data\": {\"url\": \"https://example.com/sales.csv\"}, \"mark\": \"bar\"}",
            "{\"data\": {\"url\": \"//example.com/sales.csv\"}, \"mark\": \"bar\"}",
            "{\"data\": {\"values\": [{\"a\": 1}]}, \"mark\": {\"type\": \"image\", \"url\": \"data:image/png;base64,AAAA\"}}",
            "{\"data\": {\"values\": [{\"a\": \"sales.csv\"}]}, \"mark\": \"bar\", \"title\": \"logo.png\"}",
            "\"sales.csv\"",
            "{\"data\": {\"url\": \"sales.csv\"",
            string.Empty
        ];

        // Act
        var fromData = DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, data);
        var fromMark = DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, mark);
        var fromRows = DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, "\uFEFF" + rows);

        // Assert: the first local file of each, with what the chart does with it and its line
        Assert.Equal(new BlockReference("reads the data file", "sales.csv", 1), fromData);
        Assert.Equal(new BlockReference("draws the picture", "logo.png", 3), fromMark);
        Assert.Equal(new BlockReference("draws the picture", "pics/a%20b.png", 2), fromRows);
        Assert.All(none, text => Assert.Null(DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, text)));
    }

    /// <summary>
    ///     Test that the files an OpenSCAD block names are found by the parser, the first with its line: an include and a
    ///     use; and that a comment, a comparison, an import, a syntax error, and the kinds that keep their own behavior
    ///     give none.
    /// </summary>
    [Fact]
    public void DependencyHasher_FindBlockReference_OpenScad_FindsIncludeAndUseWithLine()
    {
        // Arrange: models that include or use a file, and models that name none
        string[] none =
        [
            "// include <old.scad>\n/* use <older.scad> */\ncube(1);",
            "a = 1; b = 2; c = 3; d = 4;\ne = a < b && d > c;\ncube(e ? 1 : 2);",
            "import(\"part.stl\");",
            "cube(",
            string.Empty
        ];

        // Act
        var include = DependencyHasher.FindBlockReference(PictureAttributes.Scad, "cube(1);\ninclude <lib/shapes.scad>\nuse <more.scad>\n");
        var use = DependencyHasher.FindBlockReference(PictureAttributes.Scad, "use <more.scad>\ncube(1);");

        // Assert: the first directive, with its verb and line; nothing else is a reference
        Assert.Equal(new BlockReference("includes", "lib/shapes.scad", 2), include);
        Assert.Equal(new BlockReference("uses", "more.scad", 1), use);
        Assert.All(none, text => Assert.Null(DependencyHasher.FindBlockReference(PictureAttributes.Scad, text)));
        Assert.Null(DependencyHasher.FindBlockReference(PictureAttributes.PlantUml, "@startuml\n!include people.iuml\n@enduml"));
        Assert.Null(DependencyHasher.FindBlockReference(PictureAttributes.Mermaid, "flowchart LR\n  A[\"<img src='a.png'>\"]"));
    }

    /// <summary>
    ///     Test that the line of a Vega-Lite data file is that of the <c>data.url</c> value that names it, not of an
    ///     earlier <c>title</c> with the same text.
    /// </summary>
    [Fact]
    public void DependencyHasher_FindBlockReference_VegaLite_SameTextInEarlierTitle_ReportsLineOfDataUrl()
    {
        // Arrange: a title with the same text as the data URL, written before it
        const string text = "{\n  \"title\": \"sales.csv\",\n  \"data\": {\"url\": \"sales.csv\"},\n  \"mark\": \"bar\"\n}";

        // Act
        var reference = DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, text);

        // Assert: the line of the data URL
        Assert.Equal(new BlockReference("reads the data file", "sales.csv", 3), reference);
    }

    /// <summary>
    ///     Test that the line of a Vega-Lite picture is that of the image mark's <c>url</c> value that names it, not of an
    ///     earlier <c>title</c> with the same text.
    /// </summary>
    [Fact]
    public void DependencyHasher_FindBlockReference_VegaLite_SameTextInEarlierTitle_ReportsLineOfImageMarkUrl()
    {
        // Arrange: a title with the same text as the image mark's link, written before it
        const string text = "{\n  \"title\": \"logo.png\",\n  \"data\": {\"values\": [{\"a\": 1}]},\n  \"mark\": {\"type\": \"image\", \"url\": \"logo.png\"}\n}";

        // Act
        var reference = DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, text);

        // Assert: the line of the image mark's link
        Assert.Equal(new BlockReference("draws the picture", "logo.png", 4), reference);
    }

    /// <summary>
    ///     Test that a name a Vega-Lite chart both draws and reads gets the action and line of the value written first.
    /// </summary>
    [Fact]
    public void DependencyHasher_FindBlockReference_VegaLite_SameNameReadAndDrawn_ReportsActionOfFirstMember()
    {
        // Arrange: an image mark's link, then a data URL with the same name
        const string text = "{\n  \"mark\": {\"type\": \"image\", \"url\": \"shared.png\"},\n  \"data\": {\"url\": \"shared.png\"}\n}";

        // Act
        var reference = DependencyHasher.FindBlockReference(PictureAttributes.VegaLite, text);

        // Assert: the picture, as the image mark comes first
        Assert.Equal(new BlockReference("draws the picture", "shared.png", 2), reference);
    }

    /// <summary>
    ///     Test that the line of an OpenSCAD include is that of the directive, not of an earlier comment or string that
    ///     names the same file.
    /// </summary>
    [Fact]
    public void DependencyHasher_FindBlockReference_OpenScad_SameNameInEarlierCommentAndString_ReportsLineOfDirective()
    {
        // Arrange: a line comment, a block comment, and a string that name the file before the include
        const string text = "// shapes.scad holds the parts\n/* include <shapes.scad> */\nname = \"shapes.scad\";\ninclude <shapes.scad>\ncube(1);";

        // Act
        var reference = DependencyHasher.FindBlockReference(PictureAttributes.Scad, text);

        // Assert: the line of the directive
        Assert.Equal(new BlockReference("includes", "shapes.scad", 4), reference);
    }

    /// <summary>
    ///     Writes a file below a directory, creating its folder.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The relative name, with forward slashes.</param>
    /// <param name="content">The text.</param>
    /// <returns>The full path.</returns>
    private static string Write(TempDirectory directory, string name, string content)
    {
        var path = directory.Combine(name.Split('/'));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    ///     Collapses a path as the filter names it.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The collapsed path.</returns>
    private static string Collapse(string path)
    {
        return FilterPaths.Collapse(path);
    }

    /// <summary>
    ///     Gets the labels of parts: the text before the first line feed.
    /// </summary>
    /// <param name="parts">The parts.</param>
    /// <returns>The labels.</returns>
    private static List<string> Labels(IReadOnlyList<byte[]> parts)
    {
        return parts.Select(p => Encoding.UTF8.GetString(p).Split('\n')[0]).ToList();
    }
}
