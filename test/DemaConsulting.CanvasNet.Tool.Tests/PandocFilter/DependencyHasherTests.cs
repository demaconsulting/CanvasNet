using System.Text;
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
        var roots = new[] { Collapse(directory.Combine("docs")) };

        // Act
        var confined = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", roots);
        var open = DependencyHasher.ForPicture("scad", Collapse(model), "a.scad", null);

        // Assert
        Assert.Equal(["a.scad"], Labels(confined));
        Assert.Equal(["a.scad", "../outside/b.scad"], Labels(open));
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
