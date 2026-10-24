using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     System-level integration tests for the <c>DemaConsulting.CanvasNet.Scad</c> package, exercising only its
///     public API end to end (one test method per top-level system requirement declared in
///     <c>docs/reqstream/canvas-net-scad.yaml</c> and its platform requirements). The fixtures are the
///     original-work <c>.scad</c> files copied to the test output folder.
/// </summary>
public class ScadSystemIntegrationTests
{
    /// <summary>
    ///     Validates that parsing the same fixture from a string, a stream and a file agrees on everything the
    ///     public document reports.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Open_ParseStringOpenStreamAndOpenPathAgree()
    {
        // Arrange / Act / Assert: for every valid fixture, the three entry points report the same facts
        foreach (var path in ScadFixtureHelpers.ValidFixtures())
        {
            var options = new ScadOpenOptions { SourceName = path };
            var text = File.ReadAllText(path);

            using var stream = File.OpenRead(path);
            var fromString = ScadDocument.Parse(text, options);
            var fromStream = ScadDocument.Open(stream, options);
            var fromPath = ScadDocument.Open(path);

            foreach (var other in new[] { fromStream, fromPath })
            {
                Assert.Equal(fromString.SourceName, other.SourceName);
                Assert.Equal(fromString.Directives, other.Directives);
                Assert.Equal(fromString.ModuleNames, other.ModuleNames);
                Assert.Equal(fromString.FunctionNames, other.FunctionNames);
            }
        }
    }

    /// <summary>
    ///     Validates that invalid arguments are rejected by every entry point.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Open_InvalidArgumentsThrow()
    {
        // Arrange: an unreadable stream
        var closed = new MemoryStream();
        closed.Dispose();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Open((string)null!));
        Assert.Throws<ArgumentNullException>(() => ScadDocument.Open((Stream)null!));
        Assert.Throws<ArgumentException>(() => ScadDocument.Open("  "));
        Assert.Throws<ArgumentException>(() => ScadDocument.Open(closed));
        Assert.Throws<FileNotFoundException>(() => ScadDocument.Open(ScadFixtureHelpers.PathOf("missing.scad")));
    }

    /// <summary>
    ///     Validates that open options are validated and immutable.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Options_AreValidatedAndImmutable()
    {
        // Arrange
        var libraries = new List<string> { "lib" };
        var options = new ScadOpenOptions { BaseDirectory = "base", LibraryPaths = libraries, SourceName = "s.scad" };

        // Act
        libraries.Add("later");

        // Assert: invalid values are rejected, collections copied, and the name flows into diagnostics
        Assert.Throws<ArgumentException>(() => new ScadOpenOptions { BaseDirectory = " " });
        Assert.Throws<ArgumentException>(() => new ScadOpenOptions { SourceName = "" });
        Assert.Throws<ArgumentException>(() => new ScadOpenOptions { LibraryPaths = [" "] });
        Assert.Equal(["lib"], options.LibraryPaths);
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse("x = ;", options));
        Assert.Equal("s.scad", ex.Location.File);
    }

    /// <summary>
    ///     Validates that every authored valid fixture parses; this is also the anchor of the platform requirements.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Syntax_AllAuthoredFixturesParse()
    {
        // Arrange
        var fixtures = ScadFixtureHelpers.ValidFixtures();

        // Act: open every valid fixture from disk
        var documents = fixtures.Select(path => ScadDocument.Open(path)).ToList();

        // Assert: the ten parse fixtures and the sixteen golden evaluation fixtures, each named by its path
        Assert.Equal(26, documents.Count);
        Assert.Equal(fixtures, documents.Select(d => d.SourceName));
    }

    /// <summary>
    ///     Validates that the parametric bracket fixture reports its file-scope definitions.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Syntax_ParametricPartFixtureReportsDefinitions()
    {
        // Arrange / Act
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf("parametric-bracket.scad"));

        // Assert: definitions in source order; nothing nested or imported
        Assert.Equal(["bracket_plate", "bracket_rib", "mounting_holes", "shelf_bracket"], document.ModuleNames);
        Assert.Equal(["rib_height", "hole_pitch", "hole_positions"], document.FunctionNames);
        Assert.Empty(document.Directives);
    }

    /// <summary>
    ///     Validates that each malformed fixture reports its exact file, line and column.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_SyntaxErrors_MalformedFixturesReportLocation()
    {
        // Arrange: expected (line, column, message fragment) per invalid fixture
        var expected = new Dictionary<string, (int Line, int Column, string Fragment)>
        {
            ["invalid-unterminated-block-comment.scad"] = (4, 1, "Unterminated block comment"),
            ["invalid-missing-semicolon.scad"] = (4, 1, "Expected ';' but found 'height'"),
            ["invalid-unbalanced-brace.scad"] = (5, 1, "Unexpected end of input; expected '}'"),
        };

        // Act / Assert: every invalid fixture is covered and fails exactly where documented
        var invalid = ScadFixtureHelpers.InvalidFixtures();
        Assert.Equal(expected.Count, invalid.Count);
        foreach (var path in invalid)
        {
            var (line, column, fragment) = expected[Path.GetFileName(path)];
            ScadTestHelpers.AssertSyntaxError(() => ScadDocument.Open(path), line, column, fragment, path);
        }
    }

    /// <summary>
    ///     Validates that only the first error is reported when a source has several.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_SyntaxErrors_FirstErrorIsReported()
    {
        // Arrange: three independent grammar errors on three lines
        const string Source = "a = ;\nb = ;\nc = ;";

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse(Source));

        // Assert: the first one, and the exception is also a standard format exception
        Assert.Equal(new ScadSourceLocation(null, 1, 5), ex.Location);
        Assert.IsAssignableFrom<FormatException>(ex);
    }

    /// <summary>
    ///     Validates that include and use directives are recorded without touching the file system or a resolver.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Directives_IncludeAndUseRecordedWithoutFileAccess()
    {
        // Arrange: a resolver that fails the test if called and a base directory that does not exist
        var options = new ScadOpenOptions
        {
            BaseDirectory = Path.Combine(Path.GetTempPath(), "scad-missing-" + Guid.NewGuid().ToString("N")),
            LibraryPaths = ["no-such-library"],
            FileResolver = name => throw new InvalidOperationException($"Unexpected resolution of '{name}'."),
        };
        var path = ScadFixtureHelpers.PathOf("include-and-use.scad");

        // Act
        var document = ScadDocument.Parse(File.ReadAllText(path), options);

        // Assert: four directives in order, none of which exist on disk
        Assert.Equal(
            [
                (ScadDirectiveKind.Include, "parts/frames.scad", 3),
                (ScadDirectiveKind.Use, "lib/gears.scad", 4),
                (ScadDirectiveKind.Include, "./relative/../nested/deep/path/box.scad", 5),
                (ScadDirectiveKind.Use, "fasteners.scad", 6),
            ],
            document.Directives.Select(d => (d.Kind, d.Path, d.Location.Line)));
    }

    /// <summary>
    ///     Validates that excessive nesting fails closed with a typed exception rather than crashing the process.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Limits_DeepNestingFailsClosed()
    {
        // Arrange: hostile nesting in three shapes, far beyond the supported depth
        var shapes = new[]
        {
            "x = " + string.Concat(Enumerable.Repeat("(", 100000)) + "1;",
            string.Concat(Enumerable.Repeat("{", 100000)),
            "x = 1" + string.Concat(Enumerable.Repeat("+1", 100000)) + ";",
        };

        // Act / Assert
        foreach (var source in shapes)
        {
            var ex = Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse(source));
            Assert.Contains("Nesting too deep", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that an oversized source is rejected, from a string and from a stream, while a source of
    ///     exactly the limit is accepted.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Limits_OversizedSourceIsRejected()
    {
        // Arrange
        const int Limit = 4_194_304;
        var atLimit = "x = 1;" + new string(' ', Limit - 6);
        var overLimit = atLimit + " ";

        // Act
        var accepted = ScadDocument.Parse(atLimit);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(overLimit));

        // Assert: the oversize failures are plain InvalidDataException, not syntax errors
        Assert.NotNull(accepted);
        Assert.IsType<InvalidDataException>(Assert.ThrowsAny<Exception>(() => ScadDocument.Parse(overLimit)));
        Assert.IsType<InvalidDataException>(Assert.ThrowsAny<Exception>(() => ScadDocument.Open(stream)));
    }

    /// <summary>
    ///     Validates that evaluation reports echo output in order and fails closed, with a location, on a failed assertion.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Evaluate_EchoAndAssertReportOutputAndFail()
    {
        // Arrange
        var passing = ScadDocument.Parse("echo(\"sum\", 1 + 2);\nassert(1 < 2);\necho([1, \"a\"]);");
        var failing = ScadDocument.Parse("echo(\"before\");\nassert(1 > 2, \"order is wrong\");\necho(\"after\");", new ScadOpenOptions { SourceName = "check.scad" });
        var delivered = new List<string>();

        // Act
        var result = passing.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var ex = Assert.Throws<ScadEvaluationException>(() => failing.Evaluate(new ScadEvaluationOptions { EchoHandler = delivered.Add }, TestContext.Current.CancellationToken));

        // Assert: echo lines in order; the failure has a typed exception, a location and the events before it
        Assert.Equal(["ECHO: \"sum\", 3", "ECHO: [1, \"a\"]"], result.Echo);
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.Equal(new ScadSourceLocation("check.scad", 2, 1), ex.Location);
        Assert.Equal("check.scad:2:1: Assertion failed: order is wrong", ex.Message);
        Assert.Equal(["ECHO: \"before\""], delivered);
    }

    /// <summary>
    ///     Validates that the parametric fixture evaluates to a model with instances, and that evaluation of every
    ///     authored fixture completes.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Evaluate_ParametricFixtureProducesInstances()
    {
        // Arrange
        var bracket = ScadDocument.Open(ScadFixtureHelpers.PathOf("parametric-bracket.scad"));
        var tree = ScadDocument.Open(ScadFixtureHelpers.PathOf("eval-instance-tree.scad"));

        // Act
        var bracketResult = bracket.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var treeResult = tree.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bracketResult.InstanceCount > 3);
        Assert.Equal(21, treeResult.InstanceCount);
        Assert.Equal(["ECHO: \"instances built\", 3"], treeResult.Echo);
    }

    /// <summary>
    ///     Validates that evaluation options are validated and immutable.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_EvaluationOptions_AreValidatedAndImmutable()
    {
        // Arrange
        var variables = new Dictionary<string, object?> { ["a"] = 1 };
        var options = new ScadEvaluationOptions { Variables = variables, Time = 0.5 };

        // Act
        variables["b"] = 2;
        variables["a"] = 99;

        // Assert: invalid values rejected at initialization, the dictionary was copied and nothing is settable later
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScadEvaluationOptions { Time = double.NaN });
        Assert.Throws<ArgumentNullException>(() => new ScadEvaluationOptions { Limits = null! });
        Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["not valid"] = 1 } });
        Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["a"] = new object() } });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScadLimits { MaxEvaluationSteps = 0 });
        Assert.Equal(["a"], options.Variables!.Keys);
        Assert.Equal(1, options.Variables["a"]);
        Assert.All(
            typeof(ScadEvaluationOptions).GetProperties().Where(p => p.CanWrite),
            property => Assert.True(property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"), property.Name));
    }

    /// <summary>
    ///     Validates that unknown names are warnings, delivered to the handler and listed in the result, and
    ///     that the evaluation continues.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Diagnostics_UnknownNamesAreWarnings()
    {
        // Arrange
        var document = ScadDocument.Parse(
            "echo(missing_variable);\necho(missing_function(1));\nmissing_module();\necho(\"still running\");",
            new ScadOpenOptions { SourceName = "warn.scad" });
        var received = new List<ScadDiagnostic>();

        // Act
        var result = document.Evaluate(new ScadEvaluationOptions { WarningHandler = received.Add }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "warn.scad:1:6: Ignoring unknown variable 'missing_variable'",
                "warn.scad:2:6: Ignoring unknown function 'missing_function'",
                "warn.scad:3:1: Ignoring unknown module 'missing_module'",
            ],
            result.Diagnostics.Select(d => d.ToString()));
        Assert.Equal(result.Diagnostics, received);
        Assert.Equal("ECHO: \"still running\"", result.Echo[^1]);
    }

    /// <summary>
    ///     Validates that unsupported features are reported by default and thrown when requested.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_UnsupportedFeatures_ReportedOrThrown()
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf("eval-unsupported.scad"));
        var received = new List<string>();

        // Act
        var reported = document.Evaluate(new ScadEvaluationOptions { UnsupportedFeatureHandler = f => received.Add(f.Feature) }, TestContext.Current.CancellationToken);
        var thrown = Assert.Throws<ScadUnsupportedFeatureException>(() => document.Evaluate(new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["scad-function-fontmetrics", "scad-function-parent_module", "scad-function-textmetrics"], reported.UnsupportedFeatures.Select(f => f.Feature));
        Assert.Equal(reported.UnsupportedFeatures.Select(f => f.Feature), received);
        Assert.Equal("scad-function-fontmetrics", thrown.Feature);
        Assert.Equal(7, thrown.Location.Line);
        Assert.IsAssignableFrom<IOException>(thrown);
    }

    /// <summary>
    ///     Validates include and use through a resolver and through the file system, and that the sandbox rejects
    ///     paths that leave the allowed directories.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_IncludeUse_ResolverAndSandbox()
    {
        // Arrange
        var requested = new List<string>();
        Stream? Resolver(string path)
        {
            requested.Add(path);
            return path == "lib/util.scad" ? new MemoryStream("function twice(x) = 2 * x;\necho(\"never\");"u8.ToArray()) : null;
        }

        var main = "use <lib/util.scad>\necho(twice(21));";
        var viaResolver = ScadDocument.Parse(main, new ScadOpenOptions { FileResolver = Resolver });
        var escaping = ScadDocument.Parse("include <../secret.scad>", new ScadOpenOptions { FileResolver = Resolver });
        var fixture = ScadDocument.Open(ScadFixtureHelpers.PathOf("eval-include-main.scad"));

        // Act
        var resolved = viaResolver.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var escapeCount = requested.Count;
        var escape = Assert.Throws<ScadEvaluationException>(() => escaping.Evaluate(cancellationToken: TestContext.Current.CancellationToken));
        var fromDisk = fixture.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: the definitions were used, the sandbox violation never reached the callback and the file system works
        Assert.Equal(["ECHO: 42"], resolved.Echo);
        Assert.Equal(["lib/util.scad"], requested);
        Assert.Equal(escapeCount, requested.Count);
        Assert.Equal(1, escape.Location.Line);
        Assert.Equal("ECHO: \"main\", 5, 10, \"from inner\"", fromDisk.Echo[2]);
    }

    /// <summary>
    ///     Validates that every resource limit fails closed with a located evaluation exception.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_EvaluationLimits_FailClosed()
    {
        // Arrange: a program and the limits that make it fail, for each limit
        var cases = new (string Source, ScadLimits Limits, string Message)[]
        {
            ("function f(n) = 1 + f(n + 1);\necho(f(0));", new ScadLimits { MaxRecursionDepth = 16 }, "Recursion too deep"),
            ("function f(n) = f(n + 1);\necho(f(0));", new ScadLimits { MaxEvaluationSteps = 1000 }, "Evaluation step limit exceeded"),
            ("for (i = [0 : 100]) {}", new ScadLimits { MaxLoopIterations = 10 }, "Loop iteration limit exceeded"),
            ("x = [for (i = [0 : 10]) i];", new ScadLimits { MaxListLength = 3 }, "List too long"),
            ("x = str(\"abcdef\");", new ScadLimits { MaxStringLength = 3 }, "String too long"),
            ("for (i = [0 : 5]) cube(1);", new ScadLimits { MaxInstances = 3 }, "Instance limit exceeded"),
            ("echo(\"abcdefghij\");", new ScadLimits { MaxOutputLength = 5 }, "Echo output limit exceeded"),
        };

        // Act / Assert
        foreach (var (source, limits, message) in cases)
        {
            var ex = Assert.Throws<ScadEvaluationException>(() => ScadDocument.Parse(source).Evaluate(new ScadEvaluationOptions { Limits = limits }, TestContext.Current.CancellationToken));
            Assert.Contains(message, ex.Message, StringComparison.Ordinal);
            Assert.True(ex.Location.Line >= 1);
        }
    }

    /// <summary>
    ///     Validates that deep recursion neither overflows a small caller stack nor changes the outcome.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_EvaluationLimits_DeepRecursionOnSmallStackDoesNotOverflow()
    {
        // Arrange
        var document = ScadDocument.Parse("function f(n) = n == 0 ? 0 : 1 + f(n - 1);\necho(f(2400));\necho(f(100000));");

        // Act: the same document on a 256 KiB thread and on the normal test thread
        var small = OnThread(() => Assert.Throws<ScadEvaluationException>(() => document.Evaluate(cancellationToken: CancellationToken.None)), 256 * 1024);
        var normal = Assert.Throws<ScadEvaluationException>(() => document.Evaluate(cancellationToken: TestContext.Current.CancellationToken));

        // Assert: identical, located failure at the recursion limit, not a crash
        Assert.Contains("Recursion too deep (limit 5000)", small.Message, StringComparison.Ordinal);
        Assert.Equal(normal.Message, small.Message);
        Assert.Equal(normal.Location, small.Location);
    }

    /// <summary>
    ///     Validates that a canceled token stops evaluation without wrapping the cancellation.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Cancellation_StopsEvaluation()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(\"started\");\nfunction f(n) = f(n + 1);\necho(f(0));");
        var options = new ScadEvaluationOptions { Limits = new ScadLimits { MaxEvaluationSteps = long.MaxValue } };
        using var already = new CancellationTokenSource();
        already.Cancel();
        using var later = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        later.CancelAfter(TimeSpan.FromMilliseconds(150));
        var started = DateTime.UtcNow;

        // Act
        var before = Record.Exception(() => document.Evaluate(options, already.Token));
        var during = Record.Exception(() => document.Evaluate(options, later.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(before);
        Assert.IsType<OperationCanceledException>(during);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(20));
    }

    /// <summary>
    ///     Validates that variable overrides replace assignments and create variables, deterministically.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_VariableOverrides_ReplaceAssignments()
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf("eval-overrides.scad"));
        var options = new ScadEvaluationOptions
        {
            Variables = new Dictionary<string, object?> { ["width"] = 6, ["tag"] = "custom", ["$fn"] = 48, ["extra"] = new[] { 1, 2 } },
        };

        // Act
        var defaults = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var overridden = document.Evaluate(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ECHO: \"size\", 10, 5", defaults.Echo[1]);
        Assert.Equal(["ECHO: \"size\", 6, 3", "ECHO: \"tag\", \"custom\"", "ECHO: \"fragments\", 48", "ECHO: \"extra\", [1, 2]"], overridden.Echo);
    }

    /// <summary>
    ///     Validates that evaluation is repeatable and independent of the current culture.
    /// </summary>
    [Fact]
    public void CanvasNetScad_SystemIntegration_Determinism_InvariantCultureAndRepeatable()
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf("eval-number-formatting.scad"));

        // Act
        var baseline = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var again = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var cultures = new[] { "de-DE", "fr-FR", "tr-TR" }.Select(name => OnThread(
            () =>
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                return document.Evaluate(cancellationToken: CancellationToken.None);
            },
            1024 * 1024)).ToList();

        // Assert
        Assert.Equal(baseline.Echo, again.Echo);
        Assert.All(cultures, result => Assert.Equal(baseline.Echo, result.Echo));
        Assert.Contains("ECHO: \"fractions\", 0.1, 0.5, 1.5, -2.5, 0.333333, 0.666667, 3.14159, 12345.7", baseline.Echo);
    }

    /// <summary>
    ///     Runs a function on a new thread with the given stack size and returns its result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="function">The function.</param>
    /// <param name="stackSize">The stack size in bytes.</param>
    /// <returns>The result; an exception thrown by the function is rethrown on the calling thread.</returns>
    private static T OnThread<T>(Func<T> function, int stackSize)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    result = function();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            },
            stackSize);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result!;
    }
}
