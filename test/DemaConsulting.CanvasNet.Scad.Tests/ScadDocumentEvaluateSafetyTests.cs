using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of the fail-closed behavior of the public
///     <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/>: resource limits, cancellation,
///     the include sandbox and stack safety.
/// </summary>
public class ScadDocumentEvaluateSafetyTests
{
    /// <summary>The longest a canceled evaluation may take before the test gives up.</summary>
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(20);

    /// <summary>The recursive function used by the stack tests (two evaluation frames per level).</summary>
    private const string Countdown = "function f(n) = n == 0 ? 0 : 1 + f(n - 1);\n";

    /// <summary>Gets one case per limit: name, program and the message the failure must carry.</summary>
    public static TheoryData<string, string, string> LimitCases => new()
    {
        { "recursion", "function f(n) = 1 + f(n + 1);\necho(f(0));", "Recursion too deep (limit 16)" },
        { "steps", "function f(n) = f(n + 1);\necho(f(0));", "Evaluation step limit exceeded (limit 1000)" },
        { "loops", "for (i = [0 : 100]) {}", "Loop iteration limit exceeded (limit 10)" },
        { "list", "x = [for (i = [0 : 10]) i];", "List too long (limit 3)" },
        { "string", "x = str(\"abcdef\");", "String too long (limit 3)" },
        { "instances", "for (i = [0 : 5]) cube(1);", "Instance limit exceeded (limit 3)" },
        { "output", "echo(\"abcdefghij\");", "Echo output limit exceeded (limit 5)" },
    };

    /// <summary>
    ///     Validates that every resource limit fails closed with an evaluation exception that carries a location.
    /// </summary>
    /// <param name="name">The limit under test.</param>
    /// <param name="source">The program that exceeds it.</param>
    /// <param name="message">The expected message fragment.</param>
    [Theory]
    [MemberData(nameof(LimitCases))]
    public void ScadDocument_Evaluate_Limit_ThrowsEvaluationExceptionWithLocation(string name, string source, string message)
    {
        // Arrange
        var document = ScadDocument.Parse(source, new ScadOpenOptions { SourceName = "limits.scad" });
        var options = new ScadEvaluationOptions { Limits = LimitsFor(name) };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => document.Evaluate(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
        Assert.Equal("limits.scad", ex.Location.File);
        Assert.True(ex.Location.Line >= 1);
        Assert.StartsWith(ex.Location + ": ", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a program exactly at a limit succeeds, while one beyond it fails.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Limit_AtLimitSucceedsOneOverFails()
    {
        // Arrange
        const string Source = "for (i = [1 : 5]) cube(i);";
        var atLimit = new ScadEvaluationOptions { Limits = new ScadLimits { MaxLoopIterations = 5, MaxInstances = 6 } };
        var oneOver = new ScadEvaluationOptions { Limits = new ScadLimits { MaxLoopIterations = 4, MaxInstances = 6 } };
        var document = ScadDocument.Parse(Source);

        // Act
        var result = document.Evaluate(atLimit, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(6, result.InstanceCount);
        Assert.Throws<ScadEvaluationException>(() => document.Evaluate(oneOver, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that the include limits (depth, file count and cycles) fail closed through the public API.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_IncludeLimitsAndCycles_ThrowEvaluationException()
    {
        // Arrange
        var chain = new ScadVirtualFiles(("c1.scad", "include <c2.scad>\n"), ("c2.scad", "include <c3.scad>\n"), ("c3.scad", "x = 1;\n"));
        var cycle = new ScadVirtualFiles(("a.scad", "include <b.scad>\n"), ("b.scad", "include <a.scad>\n"));
        var self = new ScadVirtualFiles(("self.scad", "include <self.scad>\n"));
        var diamond = new ScadVirtualFiles(("d.scad", "x = 1;\n"));

        // Act
        var depth = Assert.Throws<ScadEvaluationException>(() => Evaluate("include <c1.scad>", chain, new ScadLimits { MaxIncludeDepth = 2 }));
        var cycleEx = Assert.Throws<ScadEvaluationException>(() => Evaluate("include <a.scad>", cycle, null));
        var selfEx = Assert.Throws<ScadEvaluationException>(() => Evaluate("include <self.scad>", self, null));
        var count = Assert.Throws<ScadEvaluationException>(() => Evaluate("include <d.scad>\ninclude <d.scad>\ninclude <d.scad>", diamond, new ScadLimits { MaxIncludedFiles = 2 }));

        // Assert
        Assert.Contains("Include nesting too deep (limit 2)", depth.Message, StringComparison.Ordinal);
        Assert.Contains("Include cycle", cycleEx.Message, StringComparison.Ordinal);
        Assert.Contains("Include cycle", selfEx.Message, StringComparison.Ordinal);
        Assert.Contains("Included file limit exceeded (limit 2)", count.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a token canceled before the call stops evaluation before any resolver callback or handler.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_PreCanceledToken_ThrowsBeforeAnyWork()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n"));
        var document = ScadDocument.Parse("include <lib.scad>\necho(1);", new ScadOpenOptions { FileResolver = files.Resolve });
        var delivered = new List<string>();

        // Act
        Assert.Throws<OperationCanceledException>(() => document.Evaluate(new ScadEvaluationOptions { EchoHandler = delivered.Add }, cts.Token));

        // Assert
        Assert.Empty(files.Requests);
        Assert.Empty(delivered);
    }

    /// <summary>
    ///     Validates that cancellation during an endless evaluation stops it promptly, is not wrapped, and that the
    ///     events produced before it are still delivered.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_CanceledDuringRun_StopsPromptlyAndDeliversEarlierEvents()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));
        var document = ScadDocument.Parse("echo(\"started\");\nfunction f(n) = f(n + 1);\necho(f(0));");
        var lines = new List<string>();
        var options = new ScadEvaluationOptions
        {
            Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue },
            EchoHandler = lines.Add,
        };
        var started = DateTime.UtcNow;

        // Act
        var ex = Record.Exception(() => document.Evaluate(options, cts.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(ex);
        Assert.True(DateTime.UtcNow - started < Generous);
        Assert.Equal(["ECHO: \"started\""], lines);
    }

    /// <summary>
    ///     Validates that cancellation during include loading stops before the next file is requested.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_CanceledDuringIncludeLoading_StopsBeforeNextFile()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var files = new ScadVirtualFiles(("a.scad", "include <b.scad>\n"), ("b.scad", "x = 1;\n"))
        {
            OnRequest = path =>
            {
                if (path == "a.scad")
                {
                    cts.Cancel();
                }
            },
        };
        var document = ScadDocument.Parse("include <a.scad>\n", new ScadOpenOptions { FileResolver = files.Resolve });

        // Act
        Assert.Throws<OperationCanceledException>(() => document.Evaluate(cancellationToken: cts.Token));

        // Assert
        Assert.Equal(["a.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that an include that leaves the base directory or names an absolute path is rejected when
    ///     files are read from the file system, and that the outside file is never executed.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_FileSystemSandbox_EscapesAreRejected()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        var outside = directory.Write("outside.scad", "echo(\"leaked\");\n");
        directory.Write("project/ok.scad", "echo(\"ok\");\n");
        var attempts = new[] { "../outside.scad", outside, "sub/../../outside.scad", "..\\outside.scad" };

        // Act / Assert
        foreach (var attempt in attempts)
        {
            var main = directory.Write("project/main.scad", $"include <{attempt}>\n");
            var lines = new List<string>();
            var document = ScadDocument.Open(main);

            var ex = Assert.Throws<ScadEvaluationException>(() => document.Evaluate(new ScadEvaluationOptions { EchoHandler = lines.Add }, TestContext.Current.CancellationToken));

            Assert.Empty(lines);
            Assert.Contains("outside.scad", ex.Message, StringComparison.Ordinal);
            Assert.Equal(1, ex.Location.Line);
        }

        // The same directory, inside the sandbox, works
        var inside = directory.Write("project/main.scad", "include <ok.scad>\n");
        Assert.Equal(["ECHO: \"ok\""], ScadDocument.Open(inside).Evaluate(cancellationToken: TestContext.Current.CancellationToken).Echo);
    }

    /// <summary>
    ///     Validates that an explicit library path widens the sandbox to exactly that directory.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_FileSystemSandbox_LibraryPathIsAllowedRoot()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("libs/shared.scad", "function shared() = 7;\n");
        var main = directory.Write("project/main.scad", "use <shared.scad>\necho(shared());\n");
        var document = ScadDocument.Open(main, new ScadOpenOptions { LibraryPaths = [directory.Combine("libs")] });

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 7"], result.Echo);
    }

    /// <summary>
    ///     Validates that in resolver mode hostile paths are rejected before the callback and only logical paths are requested.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_ResolverSandbox_HostilePathsNeverReachTheCallback()
    {
        // Arrange
        var attempts = new[] { "../x.scad", "/etc/passwd", "C:/x.scad", "a/../../x.scad", "\\\\server\\share\\x.scad" };

        foreach (var attempt in attempts)
        {
            var files = new ScadVirtualFiles(("x.scad", "echo(\"leaked\");\n"));
            var document = ScadDocument.Parse($"include <{attempt}>\n", new ScadOpenOptions { FileResolver = files.Resolve });

            // Act
            var ex = Assert.Throws<ScadEvaluationException>(() => document.Evaluate(cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.Empty(files.Requests);
            Assert.Equal(1, ex.Location.Line);
        }
    }

    /// <summary>
    ///     Validates that the resolver is asked only for normalized logical paths and never touches the file system.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_ResolverMode_RequestsNormalizedLogicalPaths()
    {
        // Arrange
        var files = new ScadVirtualFiles(("parts/box.scad", "echo(\"box\");\n"));
        var options = new ScadOpenOptions { FileResolver = files.Resolve, BaseDirectory = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N")) };
        var document = ScadDocument.Parse("include <parts\\sub\\..\\box.scad>\n", options);

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: \"box\""], result.Echo);
        Assert.Equal(["parts/box.scad"], files.Requests);
    }

    /// <summary>
    ///     Validates that non-tail recursion at the default limit succeeds from a small caller stack and one level
    ///     more fails with the same message and location as on a normal stack.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_DeepRecursionOnSmallStack_AtLimitSucceedsAndOverLimitFailsIdentically()
    {
        // Arrange: two frames per level plus four, against the default limit of 5 000
        var atLimit = ScadDocument.Parse(Countdown + "echo(f(2400));");
        var overLimit = ScadDocument.Parse(Countdown + "echo(f(100000));");

        // Act
        var small = OnSmallStack(() => atLimit.Evaluate(cancellationToken: CancellationToken.None).Echo.Single());
        var smallError = OnSmallStack(() => Assert.Throws<ScadEvaluationException>(() => overLimit.Evaluate(cancellationToken: CancellationToken.None)));
        var normalError = Assert.Throws<ScadEvaluationException>(() => overLimit.Evaluate(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("ECHO: 2400", small);
        Assert.Contains("Recursion too deep (limit 5000)", smallError.Message, StringComparison.Ordinal);
        Assert.Equal(normalError.Message, smallError.Message);
        Assert.Equal(normalError.Location, smallError.Location);
    }

    /// <summary>
    ///     Validates that the maximum recursion limit works from a small caller stack without overflowing it.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_MaximumRecursionLimitOnSmallStack_Succeeds()
    {
        // Arrange
        var document = ScadDocument.Parse(Countdown + "echo(f(8000));");
        var options = new ScadEvaluationOptions { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } };

        // Act
        var echo = OnSmallStack(() => document.Evaluate(options, CancellationToken.None).Echo.Single(), 64 * 1024);

        // Assert
        Assert.Equal("ECHO: 8000", echo);
    }

    /// <summary>
    ///     Validates that the caller-visible result of a small-stack evaluation equals that of a normal one.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_SmallAndNormalStack_ProduceIdenticalResults()
    {
        // Arrange
        var document = ScadDocument.Parse(Countdown + "echo(f(1500));\necho(nope);\nmodule m(n) { if (n > 0) { cube(1); m(n - 1); } }\nm(300);");

        // Act
        var normal = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var small = OnSmallStack(() => document.Evaluate(cancellationToken: CancellationToken.None));

        // Assert
        Assert.Equal(normal.Echo, small.Echo);
        Assert.Equal(normal.Diagnostics, small.Diagnostics);
        Assert.Equal(normal.InstanceCount, small.InstanceCount);
    }

    /// <summary>
    ///     Gets the limits that make the named case fail with a small, fast program.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <returns>The limits.</returns>
    private static ScadLimits LimitsFor(string name) => name switch
    {
        "recursion" => new ScadLimits { MaxRecursionDepth = 16 },
        "steps" => new ScadLimits { MaxEvaluationSteps = 1000 },
        "loops" => new ScadLimits { MaxLoopIterations = 10 },
        "list" => new ScadLimits { MaxListLength = 3 },
        "string" => new ScadLimits { MaxStringLength = 3 },
        "instances" => new ScadLimits { MaxInstances = 3 },
        "output" => new ScadLimits { MaxOutputLength = 5 },
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>
    ///     Parses a program with a resolver and evaluates it.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="files">The virtual files.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The result.</returns>
    private static ScadEvaluationResult Evaluate(string source, ScadVirtualFiles files, ScadLimits? limits)
    {
        var document = ScadDocument.Parse(source, new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });
        var options = new ScadEvaluationOptions { Limits = limits ?? ScadLimits.Default };
        return document.Evaluate(options, TestContext.Current.CancellationToken);
    }
}
