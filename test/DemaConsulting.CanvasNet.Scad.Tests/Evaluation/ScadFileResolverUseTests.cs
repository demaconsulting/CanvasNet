using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <c>use</c> library loading in <see cref="ScadFileResolver"/>.
/// </summary>
public class ScadFileResolverUseTests
{
    /// <summary>
    ///     Validates that a used file becomes its own unit with its statements, and that the directive leaves the main unit.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_Use_CreatesLibraryUnitAndRemovesDirective()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "k = 1;\nmodule m() cube(k);\nfunction f() = k;\ncube(3);\n"));

        // Act
        var unit = LoadVirtual("use <lib.scad>\nmain = 1;\n", files);

        // Assert
        Assert.Equal(["main@main.scad:2"], Assignments(unit));
        var library = Assert.Single(unit.Uses);
        Assert.Equal("lib.scad", library.Name);
        Assert.Equal(4, library.Statements.Count);
        Assert.IsType<ScadModuleInstantiation>(library.Statements[3]);
        Assert.Equal(["k@lib.scad:1"], Assignments(library));
    }

    /// <summary>
    ///     Validates that using the same resolved file twice loads one unit and reads the file once.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_UseSameFileTwice_LoadsOnce()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "k = 1;\n"), ("other.scad", "use <lib.scad>\no = 1;\n"));

        // Act
        var unit = LoadVirtual("use <lib.scad>\nuse <other.scad>\nuse <./lib.scad>\n", files);

        // Assert: lib appears once (moved to the end by the later use), and was requested once
        Assert.Equal(["other.scad", "lib.scad"], unit.Uses.Select(u => u.Name).ToArray());
        Assert.Single(files.Requests, "lib.scad");
        Assert.Same(unit.Uses[0].Uses[0], unit.Uses[1]);
    }

    /// <summary>
    ///     Validates that the later of several uses of different files is last in the list (so it wins).
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_SeveralUses_KeepsOrderOfLastUse()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "a = 1;\n"), ("b.scad", "b = 1;\n"), ("c.scad", "c = 1;\n"));

        // Act
        var unit = LoadVirtual("use <a.scad>\nuse <b.scad>\nuse <c.scad>\nuse <a.scad>\n", files);

        // Assert
        Assert.Equal(["b.scad", "c.scad", "a.scad"], unit.Uses.Select(u => u.Name).ToArray());
    }

    /// <summary>
    ///     Validates that a library's own uses stay with the library and are not visible as uses of the importer.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_LibraryUse_StaysPrivateToLibrary()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "use <b.scad>\na = 1;\n"), ("b.scad", "b = 1;\n"));

        // Act
        var unit = LoadVirtual("use <a.scad>\n", files);

        // Assert
        var a = Assert.Single(unit.Uses);
        Assert.Equal("b.scad", Assert.Single(a.Uses).Name);
        Assert.Equal(["a@a.scad:2"], Assignments(a));
    }

    /// <summary>
    ///     Validates that libraries that use each other terminate.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MutuallyUsingLibraries_Terminate()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "use <b.scad>\na = 1;\n"), ("b.scad", "use <a.scad>\nb = 1;\n"));

        // Act
        var unit = LoadVirtual("use <a.scad>\n", files);

        // Assert: a uses b and b uses a, as the same two unit objects
        var a = Assert.Single(unit.Uses);
        var b = Assert.Single(a.Uses);
        Assert.Same(a, Assert.Single(b.Uses));
        Assert.Equal(["a.scad", "b.scad"], files.Requests.OrderBy(r => r, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    ///     Validates that a library may use itself.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_SelfUsingLibrary_Terminates()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "use <a.scad>\na = 1;\n"));

        // Act
        var unit = LoadVirtual("use <a.scad>\n", files);

        // Assert
        var a = Assert.Single(unit.Uses);
        Assert.Same(a, Assert.Single(a.Uses));
    }

    /// <summary>
    ///     Validates that a use inside an included file is collected into the includer's uses.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_UseInsideInclude_IsCollectedByIncluder()
    {
        // Arrange
        var files = new ScadVirtualFiles(("inc.scad", "use <lib.scad>\ni = 1;\n"), ("lib.scad", "l = 1;\n"));

        // Act
        var unit = LoadVirtual("include <inc.scad>\n", files);

        // Assert
        Assert.Equal(["lib.scad"], unit.Uses.Select(u => u.Name).ToArray());
        Assert.Equal(["i@inc.scad:2"], Assignments(unit));
    }

    /// <summary>
    ///     Validates that a library's includes are expanded inside the library unit.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_LibraryInclude_IsExpandedInsideLibrary()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib/main.scad", "include <part.scad>\nm = 1;\n"), ("lib/part.scad", "p = 1;\n"));

        // Act
        var unit = LoadVirtual("use <lib/main.scad>\n", files);

        // Assert
        var library = Assert.Single(unit.Uses);
        Assert.Equal(["p@lib/part.scad:1", "m@lib/main.scad:2"], Assignments(library));
    }

    /// <summary>
    ///     Validates that a missing library is a warning and no unit is created.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MissingUse_WarnsAndCreatesNoUnit()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var unit = LoadVirtual("use <nope.scad>\n", new ScadVirtualFiles(), sink: sink);

        // Assert
        Assert.Empty(unit.Uses);
        Assert.Equal("Can't open include file 'nope.scad'", Assert.Single(sink.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that a syntax error in a used library is reported with that file's name.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_SyntaxErrorInLibrary_ThrowsWithLibraryName()
    {
        // Arrange
        var files = new ScadVirtualFiles(("bad.scad", "a = ;\n"));

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => LoadVirtual("use <bad.scad>\n", files));

        // Assert
        Assert.Equal("bad.scad", ex.Location.File);
        Assert.Equal(1, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that libraries load iteratively: a long chain of libraries does not deepen the stack.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_LongLibraryChain_OnSmallStack_Succeeds()
    {
        // Arrange: lib0 uses lib1 uses lib2 ... 900 deep (under the default inclusion limit of 1,000)
        var files = new ScadVirtualFiles();
        for (var i = 0; i < 900; i++)
        {
            files.Set($"lib{i}.scad", $"use <lib{i + 1}.scad>\nv{i} = {i};\n");
        }

        files.Set("lib900.scad", "end = 1;\n");

        // Act
        var count = ScadEvaluationTestHelpers.OnSmallStack(() =>
        {
            var unit = LoadVirtual("use <lib0.scad>\n", files);
            var depth = 0;
            var current = unit;
            while (current.Uses.Count > 0)
            {
                current = current.Uses[0];
                depth++;
            }

            return depth;
        });

        // Assert
        Assert.Equal(901, count);
    }
}
