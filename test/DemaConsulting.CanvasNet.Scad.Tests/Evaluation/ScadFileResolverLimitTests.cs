using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadResolverTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the limits, cycles, errors and cancellation of <see cref="ScadFileResolver"/>.
/// </summary>
public class ScadFileResolverLimitTests
{
    /// <summary>
    ///     Builds a chain of files f1 includes f2 includes ... fN, each defining one variable.
    /// </summary>
    /// <param name="length">The number of files.</param>
    /// <returns>The virtual files.</returns>
    private static ScadVirtualFiles Chain(int length)
    {
        var files = new ScadVirtualFiles();
        for (var i = 1; i <= length; i++)
        {
            var next = i < length ? $"include <f{i + 1}.scad>\n" : string.Empty;
            files.Set($"f{i}.scad", $"v{i} = {i};\n{next}");
        }

        return files;
    }

    /// <summary>
    ///     Validates the include depth limit: exactly at the limit succeeds and one deeper fails.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_IncludeDepth_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var limits = new ScadLimits { MaxIncludeDepth = 3 };

        // Act
        var atLimit = LoadVirtual("include <f1.scad>\n", Chain(3), limits);
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("include <f1.scad>\n", Chain(4), limits));

        // Assert
        Assert.Equal(["v1", "v2", "v3"], AssignmentNames(atLimit));
        Assert.Contains("Include nesting too deep (limit 3)", ex.Message, StringComparison.Ordinal);
        Assert.Equal("f3.scad", ex.Location.File);
    }

    /// <summary>
    ///     Validates that a long chain of includes generated in memory is stopped by the depth limit, not by the stack.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_DefaultDepthLimit_StopsLongChain_OnSmallStack()
    {
        // Arrange
        var files = Chain(200);

        // Act
        var ex = ScadEvaluationTestHelpers.OnSmallStack(
            () => Record.Exception(() => LoadVirtual("include <f1.scad>\n", files)));

        // Assert
        var evaluationException = Assert.IsType<ScadEvaluationException>(ex);
        Assert.Contains("Include nesting too deep (limit 16)", evaluationException.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the maximum configurable depth of 64 is handled without stack problems.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_MaximumDepth64_OnSmallStack_Succeeds()
    {
        // Arrange
        var limits = new ScadLimits { MaxIncludeDepth = 64 };
        var files = Chain(64);

        // Act
        var unit = ScadEvaluationTestHelpers.OnSmallStack(() => LoadVirtual("include <f1.scad>\n", files, limits));

        // Assert
        Assert.Equal(64, unit.Statements.Count);
    }

    /// <summary>
    ///     Validates that an include cycle through two files is an error naming the chain.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_IncludeCycle_ThrowsWithChain()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "include <b.scad>\n"), ("b.scad", "include <a.scad>\n"));

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("include <a.scad>\n", files));

        // Assert
        Assert.Contains("Include cycle: a.scad \u2192 b.scad \u2192 a.scad", ex.Message, StringComparison.Ordinal);
        Assert.Equal("b.scad", ex.Location.File);
    }

    /// <summary>
    ///     Validates that a file that includes itself is a cycle.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_SelfInclude_ThrowsCycle()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "include <a.scad>\n"));

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("include <a.scad>\n", files));

        // Assert
        Assert.Contains("Include cycle: a.scad \u2192 a.scad", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that including the same file in two separate branches (a diamond) is not a cycle.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_DiamondInclude_IsNotACycle()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("left.scad", "include <shared.scad>\n"),
            ("right.scad", "include <shared.scad>\n"),
            ("shared.scad", "s = 1;\n"));

        // Act
        var unit = LoadVirtual("include <left.scad>\ninclude <right.scad>\n", files);

        // Assert
        Assert.Equal(["s", "s"], AssignmentNames(unit));
    }

    /// <summary>
    ///     Validates the included-file limit: exactly at the limit succeeds and one more fails.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_IncludedFiles_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var limits = new ScadLimits { MaxIncludedFiles = 3 };
        var files = new ScadVirtualFiles(("a.scad", "a = 1;\n"));

        // Act
        var atLimit = LoadVirtual("include <a.scad>\ninclude <a.scad>\ninclude <a.scad>\n", files, limits);
        var ex = Assert.Throws<ScadEvaluationException>(
            () => LoadVirtual("include <a.scad>\ninclude <a.scad>\ninclude <a.scad>\ninclude <a.scad>\n", files, limits));

        // Assert
        Assert.Equal(3, atLimit.Statements.Count);
        Assert.Contains("Included file limit exceeded (limit 3)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(4, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that libraries count against the included-file limit, once per distinct library.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_Libraries_CountOncePerDistinctFile()
    {
        // Arrange
        var limits = new ScadLimits { MaxIncludedFiles = 2 };
        var files = new ScadVirtualFiles(("a.scad", "a = 1;\n"), ("b.scad", "b = 1;\n"), ("c.scad", "c = 1;\n"));

        // Act
        var unit = LoadVirtual("use <a.scad>\nuse <a.scad>\nuse <b.scad>\n", files, limits);
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("use <a.scad>\nuse <b.scad>\nuse <c.scad>\n", files, limits));

        // Assert
        Assert.Equal(2, unit.Uses.Count);
        Assert.Contains("Included file limit exceeded (limit 2)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a diamond blow-up (each level includes the next one twice) is stopped by the limit.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_DiamondBlowUp_IsStoppedByInclusionLimit()
    {
        // Arrange: level i includes level i+1 twice, so full expansion would be 2^40 inclusions
        var files = new ScadVirtualFiles();
        for (var i = 1; i < 40; i++)
        {
            files.Set($"d{i}.scad", $"include <d{i + 1}.scad>\ninclude <d{i + 1}.scad>\n");
        }

        files.Set("d40.scad", "leaf = 1;\n");
        var limits = new ScadLimits { MaxIncludeDepth = 64 };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("include <d1.scad>\n", files, limits));

        // Assert
        Assert.Contains("Included file limit exceeded (limit 1000)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(40, files.Requests.Count);
    }

    /// <summary>
    ///     Validates the bound on expanded statements: at the limit succeeds, one over fails.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_ExpandedStatements_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var limits = new ScadLimits { MaxInstances = 4 };
        var files = new ScadVirtualFiles(("two.scad", "a = 1;\nb = 2;\n"));

        // Act
        var atLimit = LoadVirtual("include <two.scad>\ninclude <two.scad>\n", files, limits);
        var ex = Assert.Throws<ScadEvaluationException>(() => LoadVirtual("include <two.scad>\ninclude <two.scad>\nc = 3;\n", files, limits));

        // Assert
        Assert.Equal(4, atLimit.Statements.Count);
        Assert.Contains("Included statement limit exceeded (limit 4)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a directive without any file source is an error, while a document without directives is fine.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_NoBaseNoLibraryNoResolver_DirectiveIsError()
    {
        // Arrange
        var options = new ScadOpenOptions { SourceName = "main.scad" };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Load("a = 1;\ninclude <x.scad>\n", options));
        var fine = Load("a = 1;\n", options);
        var useEx = Assert.Throws<ScadEvaluationException>(() => Load("use <x.scad>\n", options));

        // Assert
        Assert.Contains("no base directory, library path or file resolver", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, ex.Location.Line);
        Assert.Single(fine.Statements);
        Assert.Contains("'x.scad'", useEx.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a syntax error in an included file is a syntax exception naming that file.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_SyntaxErrorInInclude_ThrowsSyntaxExceptionWithFileName()
    {
        // Arrange
        var files = new ScadVirtualFiles(("bad.scad", "ok = 1;\nbroken = ;\n"));

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => LoadVirtual("include <bad.scad>\n", files));

        // Assert
        Assert.Equal("bad.scad", ex.Location.File);
        Assert.Equal(2, ex.Location.Line);
        Assert.StartsWith("bad.scad:2:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an included file larger than the source limit is an invalid-data error.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_OversizeInclude_ThrowsInvalidData()
    {
        // Arrange
        var files = new ScadVirtualFiles(("big.scad", new string(' ', 4_194_305)), ("limit.scad", new string(' ', 4_194_304)));

        // Act
        var atLimit = LoadVirtual("include <limit.scad>\n", files);

        // Assert: the limit counts characters, so exactly the limit parses and one more is refused
        Assert.Empty(atLimit.Statements);
        Assert.Throws<InvalidDataException>(() => LoadVirtual("include <big.scad>\n", files));
    }

    /// <summary>
    ///     Validates that an already canceled token stops before any file is requested.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_CanceledToken_ThrowsBeforeAnyRequest()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "a = 1;\n"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => LoadVirtual("include <a.scad>\n", files, cancellation: source.Token));
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that cancellation between files stops loading with the plain cancellation exception.
    /// </summary>
    [Fact]
    public void ScadFileResolver_Load_CancelDuringLoading_StopsBetweenFiles()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var files = new ScadVirtualFiles(("a.scad", "include <b.scad>\n"), ("b.scad", "include <c.scad>\n"), ("c.scad", "c = 1;\n"))
        {
            OnRequest = path =>
            {
                if (path == "b.scad")
                {
                    source.Cancel();
                }
            },
        };

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => LoadVirtual("include <a.scad>\n", files, cancellation: source.Token));

        // Assert: canceled while b was requested, so c is never asked for
        Assert.IsNotType<ScadEvaluationException>(ex);
        Assert.Equal(["a.scad", "b.scad"], files.Requests);
    }
}
