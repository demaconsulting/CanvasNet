using System.Collections.Concurrent;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of the public <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/>: documents
///     from every entry point, the result, the handlers, variable overrides and the time.
/// </summary>
public class ScadDocumentEvaluateTests
{
    /// <summary>
    ///     Validates that evaluating without options returns the echo lines, warnings and instance count.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_DefaultOptions_ReturnsEchoDiagnosticsAndInstanceCount()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(\"hi\", 1/3);\necho(nope);\ncube(1);\ntranslate([1, 0, 0]) sphere(2);");

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: \"hi\", 0.333333", "ECHO: undef"], result.Echo);
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal("Ignoring unknown variable 'nope'", warning.Message);
        Assert.Equal(new ScadSourceLocation(null, 2, 6), warning.Location);
        Assert.Empty(result.UnsupportedFeatures);
        Assert.Equal(3, result.InstanceCount);
    }

    /// <summary>
    ///     Validates that null options behave exactly like default options.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_NullOptions_BehavesLikeDefaults()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(1);");

        // Act
        var withNull = document.Evaluate(null, TestContext.Current.CancellationToken);
        var withDefaults = document.Evaluate(new ScadEvaluationOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(withDefaults.Echo, withNull.Echo);
        Assert.Equal(withDefaults.InstanceCount, withNull.InstanceCount);
    }

    /// <summary>
    ///     Validates the instance counting rules visible through the result: groups count, disabled statements do not.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected instance count.</param>
    [Theory]
    [InlineData("", 0)]
    [InlineData("cube(1);", 1)]
    [InlineData("module m() cube(1);\nm();", 2)]
    [InlineData("for (i = [0 : 1]) cube(1);", 3)]
    [InlineData("*cube(1);", 0)]
    [InlineData("unknown_module();", 0)]
    [InlineData("difference() { cube(2); sphere(1); }", 3)]
    public void ScadDocument_Evaluate_InstanceCount_CountsModuleInstancesAndGroups(string source, int expected)
    {
        // Arrange
        var document = ScadDocument.Parse(source);

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, result.InstanceCount);
    }

    /// <summary>
    ///     Validates that the result lists cannot be modified through a cast.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_ResultLists_AreReadOnly()
    {
        // Arrange
        var result = ScadDocument.Parse("echo(nope);").Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Act / Assert
        Assert.Throws<NotSupportedException>(() => ((IList<string>)result.Echo).Add("x"));
        Assert.Throws<NotSupportedException>(() => ((IList<ScadDiagnostic>)result.Diagnostics).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ScadUnsupportedFeature>)result.UnsupportedFeatures).Clear());
    }

    /// <summary>
    ///     Validates evaluation of a document opened from a file: a relative include is resolved against its folder.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_OpenPath_ResolvesRelativeIncludeAndUse()
    {
        // Arrange
        using var directory = new ScadTempDirectory();
        directory.Write("parts/size.scad", "size = 4;\n");
        directory.Write("parts/shapes.scad", "function double(x) = 2 * x;\necho(\"not executed by use\");\n");
        var main = directory.Write("main.scad", "include <parts/size.scad>\nuse <parts/shapes.scad>\necho(double(size));\n");

        // Act
        var result = ScadDocument.Open(main).Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 8"], result.Echo);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Validates evaluation of a document opened from a stream with a resolver: only logical paths are requested.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_OpenStreamWithResolver_UsesTheResolver()
    {
        // Arrange
        var files = new ScadVirtualFiles(("parts/size.scad", "size = 6;\n"));
        var options = new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "stream.scad" };
        using var stream = new MemoryStream("include <parts/size.scad>\necho(size);\n"u8.ToArray());

        // Act
        var result = ScadDocument.Open(stream, options).Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 6"], result.Echo);
        Assert.Equal(["parts/size.scad"], files.Requests);
        Assert.All(files.Threads, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
    }

    /// <summary>
    ///     Validates that a string document with an include but no base directory, library path or resolver fails closed.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_IncludeWithoutBaseOrResolver_ThrowsEvaluationException()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(1);\ninclude <lib.scad>\n", new ScadOpenOptions { SourceName = "main.scad" });

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => document.Evaluate(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("main.scad", ex.Location.File);
        Assert.Equal(2, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that a missing include is a warning with the location of the directive and evaluation continues.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_MissingInclude_IsWarning()
    {
        // Arrange
        var files = new ScadVirtualFiles();
        var document = ScadDocument.Parse("include <gone.scad>\necho(1);", new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "m.scad" });

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 1"], result.Echo);
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal("Can't open include file 'gone.scad'", warning.Message);
        Assert.Equal("m.scad", warning.Location.File);
    }

    /// <summary>
    ///     Validates that a syntax error in an included file propagates with that file's name.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_SyntaxErrorInIncludedFile_ThrowsSyntaxExceptionWithFileName()
    {
        // Arrange
        var files = new ScadVirtualFiles(("bad.scad", "x = ;\n"));
        var document = ScadDocument.Parse("include <bad.scad>\n", new ScadOpenOptions { FileResolver = files.Resolve });

        // Act
        var ex = Assert.Throws<ScadSyntaxException>(() => document.Evaluate(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("bad.scad", ex.Location.File, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the echo, warning and unsupported-feature handlers receive every event, in order, and that
    ///     the lists of the result hold the same events.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Handlers_ReceiveEventsInOrderAndMatchResult()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(1);\necho(nope);\necho(parent_module(1));\necho(2);");
        var events = new List<string>();
        var options = new ScadEvaluationOptions
        {
            EchoHandler = line => events.Add("echo:" + line),
            WarningHandler = warning => events.Add("warn:" + warning.Message),
            UnsupportedFeatureHandler = feature => events.Add("unsupported:" + feature.Feature),
        };

        // Act
        var result = document.Evaluate(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "echo:ECHO: 1",
                "warn:Ignoring unknown variable 'nope'",
                "echo:ECHO: undef",
                "unsupported:scad-function-parent_module",
                "echo:ECHO: undef",
                "echo:ECHO: 2",
            ],
            events);
        Assert.Equal(["ECHO: 1", "ECHO: undef", "ECHO: undef", "ECHO: 2"], result.Echo);
        Assert.Single(result.Diagnostics);
        var unsupported = Assert.Single(result.UnsupportedFeatures);
        Assert.Equal("scad-function-parent_module", unsupported.Feature);
        Assert.Equal(3, unsupported.Location.Line);
    }

    /// <summary>
    ///     Validates that unsupported features are de-duplicated by feature and location.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_UnsupportedFeatures_ReportedOncePerLocation()
    {
        // Arrange
        var document = ScadDocument.Parse("for (i = [0 : 4]) echo(parent_module(1));\necho(parent_module(1));");
        var reported = new List<ScadUnsupportedFeature>();

        // Act
        var result = document.Evaluate(new ScadEvaluationOptions { UnsupportedFeatureHandler = reported.Add }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([1, 2], result.UnsupportedFeatures.Select(f => f.Location.Line));
        Assert.Equal(result.UnsupportedFeatures, reported);
        Assert.Equal(6, result.Echo.Count);
    }

    /// <summary>
    ///     Validates that ThrowOnUnsupportedFeature stops evaluation with a typed exception and still delivers the
    ///     events recorded before it.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_ThrowOnUnsupportedFeature_ThrowsAfterDeliveringEarlierEvents()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(\"before\");\necho(parent_module(1));\necho(\"after\");");
        var lines = new List<string>();
        var options = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true, EchoHandler = lines.Add };

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => document.Evaluate(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("scad-function-parent_module", ex.Feature);
        Assert.Equal(2, ex.Location.Line);
        Assert.StartsWith(ex.Location + ": ", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["ECHO: \"before\""], lines);
    }

    /// <summary>
    ///     Validates that without the throw flag an unsupported feature is reported and evaluation continues.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_UnsupportedFeature_WithoutThrowFlag_Continues()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(parent_module(1));\necho(\"after\");");

        // Act
        var result = document.Evaluate(new ScadEvaluationOptions { ThrowOnUnsupportedFeature = false }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: undef", "ECHO: \"after\""], result.Echo);
        Assert.Single(result.UnsupportedFeatures);
    }

    /// <summary>
    ///     Validates that the handlers receive the events recorded before a failure, and that the failure is still thrown.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Failure_StillDeliversEventsToHandlers()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(\"a\");\necho(nope);\nassert(false, \"stop\");\necho(\"never\");");
        var events = new List<string>();
        var options = new ScadEvaluationOptions
        {
            EchoHandler = line => events.Add(line),
            WarningHandler = warning => events.Add(warning.Message),
        };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => document.Evaluate(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("3:1: Assertion failed: stop", ex.Message);
        Assert.Equal(["ECHO: \"a\"", "Ignoring unknown variable 'nope'", "ECHO: undef"], events);
    }

    /// <summary>
    ///     Validates that an exception thrown by a handler propagates from a successful evaluation.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_HandlerThrowsOnSuccess_Propagates()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(1);");
        var options = new ScadEvaluationOptions { EchoHandler = _ => throw new InvalidOperationException("handler failed") };

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => document.Evaluate(options, TestContext.Current.CancellationToken));
        Assert.Equal("handler failed", ex.Message);
    }

    /// <summary>
    ///     Validates that an exception thrown by a handler does not hide the evaluation failure.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_HandlerThrowsOnFailure_EvaluationFailureWins()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(1);\nassert(false);");
        var options = new ScadEvaluationOptions { EchoHandler = _ => throw new InvalidOperationException("handler failed") };

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => document.Evaluate(options, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that the handlers run on the calling thread, also when the evaluation had to be restarted on a
    ///     large-stack thread because the caller's stack is small, and that every event is delivered exactly once.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Handlers_RunOnCallerThread_EvenAfterStackRestart()
    {
        // Arrange: a recursion deep enough to exhaust a 64 KiB stack and a warning and echo around it
        var document = ScadDocument.Parse(
            "function f(n) = n == 0 ? 0 : 1 + f(n - 1);\necho(\"start\");\necho(nope);\necho(f(2000));");

        // Act
        var (callerThread, evaluatorThread, handlerThreads, lines, warnings) = OnSmallStack(
            () =>
            {
                var threads = new List<int>();
                var echoed = new List<string>();
                var warned = new List<string>();
                var options = new ScadEvaluationOptions
                {
                    EchoHandler = line =>
                    {
                        threads.Add(Environment.CurrentManagedThreadId);
                        echoed.Add(line);
                    },
                    WarningHandler = warning =>
                    {
                        threads.Add(Environment.CurrentManagedThreadId);
                        warned.Add(warning.Message);
                    },
                };
                document.Evaluate(options, CancellationToken.None);
                return (Environment.CurrentManagedThreadId, ScadEvaluator.LastRunThreadId, threads, echoed, warned);
            },
            64 * 1024);

        // Assert: the pass ran elsewhere, the handlers did not, and nothing was duplicated by the restart
        Assert.NotEqual(callerThread, evaluatorThread);
        Assert.All(handlerThreads, id => Assert.Equal(callerThread, id));
        Assert.Equal(["ECHO: \"start\"", "ECHO: undef", "ECHO: 2000"], lines);
        Assert.Equal(["Ignoring unknown variable 'nope'"], warnings);
    }

    /// <summary>
    ///     Validates that a variable override replaces every top-level assignment, creates absent variables and is
    ///     applied without evaluating the replaced right-hand sides.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Variables_ReplaceAndCreateAssignments()
    {
        // Arrange
        var document = ScadDocument.Parse(
            "width = 10;\nwidth = 12;\nlabel = echo(\"rhs evaluated\") \"x\";\necho(width, label, extra);");
        var options = new ScadEvaluationOptions
        {
            Variables = new Dictionary<string, object?> { ["width"] = 5, ["label"] = "y", ["extra"] = new[] { 1, 2 } },
        };

        // Act
        var result = document.Evaluate(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 5, \"y\", [1, 2]"], result.Echo);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Validates the conversion of each supported override kind to a language value.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Variables_ConvertEverySupportedKind()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(a, b, c, d, e, f, g);");
        var options = new ScadEvaluationOptions
        {
            Variables = new Dictionary<string, object?>
            {
                ["a"] = null,
                ["b"] = true,
                ["c"] = 42L,
                ["d"] = 2.5m,
                ["e"] = "text",
                ["f"] = new List<object?> { 1, "s", new[] { false } },
                ["g"] = double.PositiveInfinity,
            },
        };

        // Act
        var result = document.Evaluate(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: undef, true, 42, 2.5, \"text\", [1, \"s\", [false]], inf"], result.Echo);
    }

    /// <summary>
    ///     Validates that overrides apply to assignments in included files but not to used libraries.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Variables_ReplaceIncludedAssignmentsButNotUsedLibraries()
    {
        // Arrange
        var files = new ScadVirtualFiles(
            ("inc.scad", "size = 1;\n"),
            ("lib.scad", "k = 1;\nfunction get_k() = k;\n"));
        var document = ScadDocument.Parse(
            "include <inc.scad>\nuse <lib.scad>\necho(size, get_k());",
            new ScadOpenOptions { FileResolver = files.Resolve });
        var options = new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["size"] = 9, ["k"] = 5 } };

        // Act
        var result = document.Evaluate(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 9, 1"], result.Echo);
    }

    /// <summary>
    ///     Validates that special-variable overrides apply, and that the order of the caller's dictionary is irrelevant.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Variables_SpecialVariablesAndOrderIndependence()
    {
        // Arrange
        var document = ScadDocument.Parse("echo($fn, $custom, a, b);");
        var forward = new Dictionary<string, object?> { ["$fn"] = 36, ["$custom"] = "c", ["a"] = 1, ["b"] = 2 };
        var backward = new Dictionary<string, object?>(forward.Reverse());

        // Act
        var first = document.Evaluate(new ScadEvaluationOptions { Variables = forward }, TestContext.Current.CancellationToken);
        var second = document.Evaluate(new ScadEvaluationOptions { Variables = backward }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 36, \"c\", 1, 2"], first.Echo);
        Assert.Equal(first.Echo, second.Echo);
    }

    /// <summary>
    ///     Validates that the options-time conversion rejects an invalid value before any evaluation happens.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Variables_InvalidValueIsRejectedByOptions()
    {
        // Arrange / Act / Assert
        var ex = Assert.Throws<ArgumentException>(() => new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["bad"] = new object() } });
        Assert.Contains("'bad'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the time option sets <c>$t</c>, and that it defaults to zero.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_Time_SetsSpecialVariable()
    {
        // Arrange
        var document = ScadDocument.Parse("echo($t);");

        // Act
        var defaulted = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var timed = document.Evaluate(new ScadEvaluationOptions { Time = 0.25 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["ECHO: 0"], defaulted.Echo);
        Assert.Equal(["ECHO: 0.25"], timed.Echo);
    }

    /// <summary>
    ///     Validates that a document can be evaluated repeatedly with identical results, under any current culture.
    /// </summary>
    [Fact]
    public void ScadDocument_Evaluate_RepeatedAndCultureIndependent_ProducesIdenticalResults()
    {
        // Arrange
        var document = ScadDocument.Parse("echo(1/3, 1e10, 0.00001, str(0.1), rands(0, 1, 2));\necho(nope);\nfor (i = [0 : 2]) cube(i);");

        // Act
        var first = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var second = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var german = WithCulture("de-DE", () => document.Evaluate(cancellationToken: CancellationToken.None));
        var turkish = WithCulture("tr-TR", () => document.Evaluate(cancellationToken: CancellationToken.None));

        // Assert
        Assert.Equal(first.Echo, second.Echo);
        foreach (var other in new[] { second, german, turkish })
        {
            Assert.Equal(first.Echo, other.Echo);
            Assert.Equal(first.Diagnostics, other.Diagnostics);
            Assert.Equal(first.InstanceCount, other.InstanceCount);
        }

        Assert.StartsWith("ECHO: 0.333333, 1e+10, 1e-05, \"0.1\", [", first.Echo[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that one document can be evaluated from several threads at once with identical results.
    /// </summary>
    [Fact]
    public async Task ScadDocument_Evaluate_ConcurrentCalls_AreIndependentAndIdentical()
    {
        // Arrange
        var document = ScadDocument.Parse("function f(n) = n == 0 ? 0 : 1 + f(n - 1);\nfor (i = [1 : 50]) echo(i, f(i));\necho(nope);");
        var results = new ConcurrentBag<string>();

        // Act
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(
            () =>
            {
                var result = document.Evaluate(cancellationToken: CancellationToken.None);
                results.Add(string.Join('\n', result.Echo) + "|" + result.Diagnostics.Count + "|" + result.InstanceCount);
            },
            TestContext.Current.CancellationToken));
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(8, results.Count);
        Assert.Single(results.Distinct());
    }
}
