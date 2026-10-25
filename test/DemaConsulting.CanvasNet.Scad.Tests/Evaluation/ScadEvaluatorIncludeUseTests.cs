using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of <c>include</c> and <c>use</c> as seen by evaluation: <see cref="ScadEvaluator"/> evaluates the
///     include-expanded main file and imports only definitions from used libraries. The resolver itself is tested
///     separately; these tests pin the evaluation semantics.
/// </summary>
public class ScadEvaluatorIncludeUseTests
{
    /// <summary>
    ///     Validates that an included file is evaluated as if its text stood in place of the directive.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Include_BehavesAsTextualInclusion()
    {
        // Arrange
        var files = new ScadVirtualFiles(("defs.scad", "a = 7;\necho(\"in include\");\nmodule part() { cube(a); }\n"));

        // Act
        var output = Run("include <defs.scad>\necho(a);\npart();", null, files);

        // Assert
        Assert.Equal(["ECHO: \"in include\"", "ECHO: 7"], output.Echo);
        Assert.Equal("group<part>{cube(7)}", ScadInstanceDumper.Dump(output.Roots));
    }

    /// <summary>
    ///     Validates that the later assignment of the including file wins over an included one and that the warning points into the included file.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Include_LaterAssignmentOfIncludingFileWins_WarnsInIncludedFile()
    {
        // Arrange
        var files = new ScadVirtualFiles(("defs.scad", "size = 1;\n"));

        // Act
        var output = Run("include <defs.scad>\nsize = 2;\necho(size);", null, files);

        // Assert
        Assert.Equal("ECHO: 2", Assert.Single(output.Echo));
        var warning = Assert.Single(output.Diagnostics);
        Assert.Equal("defs.scad", warning.Location.File);
        Assert.Equal(1, warning.Location.Line);
    }

    /// <summary>
    ///     Validates that <c>use</c> imports only functions and modules: the library's top-level geometry, echo
    ///     and variables stay out.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Use_ImportsOnlyDefinitions()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "echo(\"lib top\");\ncube(99);\nfunction f() = 5;\nmodule m() { cube(1); }\nv = 3;\n"));

        // Act
        var output = Run("use <lib.scad>\necho(f(), v);\nm();", null, files);

        // Assert
        Assert.Equal("ECHO: 5, undef", Assert.Single(output.Echo));
        Assert.Equal("group<m>{cube(1)}", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("Ignoring unknown variable 'v'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that a library's functions see the library's own top-level assignments, evaluated lazily.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Use_LibraryFunctionsSeeLibraryAssignments()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "k = 10;\nfunction twice() = k * 2;\nmodule show() { echo(k); }\n"));

        // Act
        var output = Run("use <lib.scad>\nk = 1;\necho(twice());\nshow();", null, files);

        // Assert
        Assert.Equal(["ECHO: 20", "ECHO: 10"], output.Echo);
    }

    /// <summary>
    ///     Validates that a library's own <c>use</c> stays private: its functions can call them, the importer cannot.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Use_LibrariesOwnUsesArePrivate()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("a.scad", "use <b.scad>\nfunction fa() = fb() + 1;\n"),
            ("b.scad", "function fb() = 41;\n"));

        // Act
        var output = Run("use <a.scad>\necho(fa(), fb());", null, files);

        // Assert
        Assert.Equal("ECHO: 42, undef", Assert.Single(output.Echo));
        Assert.Equal("Ignoring unknown function 'fb'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates the precedence of definitions: the importer's own definition beats an imported one, and among
    ///     several imports the later <c>use</c> wins.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Use_PrecedenceLocalThenLaterUse()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("a.scad", "function f() = \"a\";\nfunction g() = \"a\";\n"),
            ("b.scad", "function f() = \"b\";\n"));

        // Act
        var output = Run("use <a.scad>\nuse <b.scad>\nfunction g() = \"local\";\necho(f(), g());", null, files);

        // Assert
        Assert.Equal("ECHO: \"b\", \"local\"", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that libraries that use each other work and that each file is read once.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Use_MutuallyUsingLibraries_Work()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("a.scad", "use <b.scad>\nfunction ping(n) = n == 0 ? \"a\" : pong(n - 1);\n"),
            ("b.scad", "use <a.scad>\nfunction pong(n) = n == 0 ? \"b\" : ping(n - 1);\n"));

        // Act
        var output = Run("use <a.scad>\nuse <b.scad>\necho(ping(3), pong(3));", null, files);

        // Assert
        Assert.Equal("ECHO: \"b\", \"a\"", Assert.Single(output.Echo));
        Assert.Single(files.Requests, "a.scad");
        Assert.Single(files.Requests, "b.scad");
    }

    /// <summary>
    ///     Validates that a library module's dynamic <c>$</c> variables come from the caller.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Use_LibraryModulesSeeCallersSpecialVariables()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "module ball() { sphere(1); }\n"));

        // Act
        var output = Run("use <lib.scad>\n$fn = 40;\nball();", null, files);

        // Assert
        Assert.Equal(40, output.Roots[0].Children[0].Fragments.Fn);
    }

    /// <summary>
    ///     Validates that a missing file is a warning and evaluation continues.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_MissingFile_WarnsAndContinues()
    {
        // Arrange
        var files = new ScadVirtualFiles();

        // Act
        var output = Run("include <nope.scad>\nuse <other-missing.scad>\ncube(1);", null, files);

        // Assert
        Assert.Equal("cube(1)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal(
            ["Can't open include file 'nope.scad'", "Can't open include file 'other-missing.scad'"],
            output.Diagnostics.Select(d => d.Message));
    }

    /// <summary>
    ///     Validates that the resolver callback is asked on the calling thread, before any echo output, and only once per file.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Resolver_RunsOnCallingThreadBeforeEvaluation()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "function f() = 1;\n"));
        var callerThread = Environment.CurrentManagedThreadId;

        // Act
        Run("echo(\"first\");\nuse <lib.scad>\necho(f());", null, files);

        // Assert
        Assert.Equal(["lib.scad"], files.Requests);
        Assert.All(files.Threads, thread => Assert.Equal(callerThread, thread));
    }

    /// <summary>
    ///     Validates that a syntax error in an included file propagates with the included file's name.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_SyntaxErrorInIncludedFile_Propagates()
    {
        // Arrange
        var files = new ScadVirtualFiles(("bad.scad", "x = ;\n"));

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => Run("include <bad.scad>", null, files));

        // Assert
        Assert.Equal("bad.scad", ex.Location.File);
    }

    /// <summary>
    ///     Validates that variable overrides apply to the main file and its includes but not to used libraries.
    /// </summary>
    [Fact]
    public void ScadEvaluatorIncludeUse_Overrides_ReplaceIncludedAssignmentsButNotLibraries()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("inc.scad", "w = 1;\n"),
            ("lib.scad", "w = 10;\nfunction library_width() = w;\n"));
        var settings = new ScadEvaluationSettings { Overrides = new Dictionary<string, ScadValue> { ["w"] = ScadValue.FromNumber(99) } };

        // Act
        var output = Run("include <inc.scad>\nuse <lib.scad>\nw = 2;\necho(w, library_width());", settings, files);

        // Assert
        Assert.Equal("ECHO: 99, 10", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }
}
