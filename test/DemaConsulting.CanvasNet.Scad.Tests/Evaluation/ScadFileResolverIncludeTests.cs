using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for textual <c>include</c> expansion in <see cref="ScadFileResolver"/>.
/// </summary>
public class ScadFileResolverIncludeTests
{
    /// <summary>
    ///     Validates that a document without directives passes through unchanged and touches no file.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_NoDirectives_ReturnsStatementsUnchanged()
    {
        // Arrange
        var files = new ScadVirtualFiles();

        // Act
        var unit = LoadVirtual("a = 1;\nb = 2;\n", files);

        // Assert
        Assert.Equal(["a@main.scad:1", "b@main.scad:2"], Assignments(unit));
        Assert.Empty(unit.Uses);
        Assert.Empty(files.Requests);
        Assert.Equal("main.scad", unit.Name);
    }

    /// <summary>
    ///     Validates that an include is replaced in place by the included statements, which keep their own locations.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_Include_ReplacesDirectiveInPlaceWithIncludedLocations()
    {
        // Arrange
        var files = new ScadVirtualFiles(("defs.scad", "\n\nx = 10;\ny = 20;\n"));

        // Act
        var unit = LoadVirtual("before = 1;\ninclude <defs.scad>\nafter = 2;\n", files);

        // Assert: directive removed; the order is before, x, y, after; included locations name the included file
        Assert.Equal(["before@main.scad:1", "x@defs.scad:3", "y@defs.scad:4", "after@main.scad:3"], Assignments(unit));
        Assert.DoesNotContain(unit.Statements, s => s is ScadDirectiveStatement);
        Assert.Equal(["defs.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that includes nest: each file's includes are resolved relative to that file's directory.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_NestedIncludes_ResolveRelativeToIncludingFile()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("libs/outer.scad", "o = 1;\ninclude <inner.scad>\n"),
            ("libs/inner.scad", "i = 2;\ninclude <deeper/deep.scad>\n"),
            ("libs/deeper/deep.scad", "d = 3;\ninclude <../back.scad>\n"),
            ("libs/back.scad", "b = 4;\n"));

        // Act
        var unit = LoadVirtual("include <libs/outer.scad>\nm = 0;\n", files);

        // Assert
        Assert.Equal(
            ["o@libs/outer.scad:1", "i@libs/inner.scad:1", "d@libs/deeper/deep.scad:1", "b@libs/back.scad:1", "m@main.scad:2"],
            Assignments(unit));
        Assert.Equal(["libs/outer.scad", "libs/inner.scad", "libs/deeper/deep.scad", "libs/back.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that backslashes in the path text are treated as separators.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_BackslashPath_IsNormalizedToForwardSlashes()
    {
        // Arrange
        var files = new ScadVirtualFiles(("libs/a.scad", "a = 1;\n"));

        // Act
        var unit = LoadVirtual("include <libs\\a.scad>\n", files);

        // Assert
        Assert.Equal(["a@libs/a.scad:1"], Assignments(unit));
    }

    /// <summary>
    ///     Validates that dot segments are collapsed before the callback is asked.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_DotSegments_AreCollapsedInRequestedPath()
    {
        // Arrange
        var files = new ScadVirtualFiles(("libs/a.scad", "a = 1;\n"));

        // Act
        var unit = LoadVirtual("include <./libs/./x/../a.scad>\n", files);

        // Assert
        Assert.Equal(["a@libs/a.scad:1"], Assignments(unit));
        Assert.Equal(["libs/a.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that including the same file twice expands it twice but reads and parses it only once.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_SameFileTwice_ExpandsTwiceReadsOnce()
    {
        // Arrange
        var files = new ScadVirtualFiles(("c.scad", "c = 1;\n"));

        // Act
        var unit = LoadVirtual("include <c.scad>\ninclude <c.scad>\n", files);

        // Assert
        Assert.Equal(["c@c.scad:1", "c@c.scad:1"], Assignments(unit));
        Assert.Equal(["c.scad"], files.Requests);
        Assert.Same(unit.Statements[0], unit.Statements[1]);
    }

    /// <summary>
    ///     Validates that an included file's own definitions and geometry statements are all brought in.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_Include_BringsInEveryKindOfStatement()
    {
        // Arrange
        var files = new ScadVirtualFiles(("all.scad", "v = 1;\nmodule m() cube(1);\nfunction f(x) = x;\ncube(2);\necho(v);\n"));

        // Act
        var unit = LoadVirtual("include <all.scad>\n", files);

        // Assert
        Assert.Collection(
            unit.Statements,
            s => Assert.IsType<ScadAssignmentStatement>(s),
            s => Assert.IsType<ScadModuleDefinition>(s),
            s => Assert.IsType<ScadFunctionDefinition>(s),
            s => Assert.IsType<ScadModuleInstantiation>(s),
            s => Assert.IsType<ScadEchoStatement>(s));
    }

    /// <summary>
    ///     Validates that an include inside a library path resolves from the library when not found next to the file.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_Include_FallsBackToLibraryPath()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib/common.scad", "k = 5;\n"));

        // Act
        var unit = LoadVirtual("include <common.scad>\n", files, libraryPaths: ["lib"]);

        // Assert: the root candidate misses, then the library prefix is tried
        Assert.Equal(["k@lib/common.scad:1"], Assignments(unit));
        Assert.Equal(["common.scad", "lib/common.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that a missing include is a warning at the directive, evaluation continues, and the directive is skipped.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MissingInclude_WarnsAndContinues()
    {
        // Arrange
        var files = new ScadVirtualFiles();
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var unit = LoadVirtual("a = 1;\ninclude <nope.scad>\nb = 2;\n", files, sink: sink);

        // Assert
        Assert.Equal(["a@main.scad:1", "b@main.scad:3"], Assignments(unit));
        var warning = Assert.Single(sink.Diagnostics);
        Assert.Equal("Can't open include file 'nope.scad'", warning.Message);
        Assert.Equal("main.scad", warning.Location.File);
        Assert.Equal(2, warning.Location.Line);
    }

    /// <summary>
    ///     Validates that a missing file is only asked for once even when several directives name it.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MissingFileTwice_AsksResolverOnce()
    {
        // Arrange
        var files = new ScadVirtualFiles();
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        LoadVirtual("include <nope.scad>\ninclude <nope.scad>\n", files, sink: sink);

        // Assert
        Assert.Equal(["nope.scad"], files.Requests);
        Assert.Equal(2, sink.Diagnostics.Count);
    }

    /// <summary>
    ///     Validates that a library path that is not a valid logical path is ignored in resolver mode.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_InvalidLogicalLibraryPaths_AreIgnored()
    {
        // Arrange
        var files = new ScadVirtualFiles(("good/x.scad", "x = 1;\n"));

        // Act
        var unit = LoadVirtual("include <x.scad>\n", files, libraryPaths: ["C:\\abs", "/rooted", "../escape", "has:colon", "good", "./"]);

        // Assert: only "good" and the root-like "./" prefix produce candidates
        Assert.Equal(["x@good/x.scad:1"], Assignments(unit));
        Assert.Equal(["x.scad", "good/x.scad"], files.Requests);
    }
}
