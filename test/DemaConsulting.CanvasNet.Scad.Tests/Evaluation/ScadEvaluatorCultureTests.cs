using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests that the text of every diagnostic is the same in every culture. Integers in messages are formatted with
///     the invariant culture by construction (<c>string.Create(CultureInfo.InvariantCulture, ...)</c>), so these tests
///     are a regression guard: for non-negative integers .NET output does not vary between the standard cultures, and
///     the synthetic culture below (other negative sign, Arabic-Indic native digits with native digit substitution) is
///     the one that would expose a culture-sensitive format of a number.
/// </summary>
public class ScadEvaluatorCultureTests
{
    /// <summary>The names of the cultures every message is produced under, besides the synthetic one.</summary>
    private static readonly string[] CultureNames = ["de-DE", "fr-FR", "tr-TR", "ar-SA", "sv-SE"];

    /// <summary>
    ///     The failing programs: for each limit a function that evaluates it and returns the exception message, and the
    ///     text that message must contain.
    /// </summary>
    private static readonly Dictionary<string, (Func<string> Produce, string Expected)> LimitCases = new()
    {
        ["recursion"] = (() => Fail("function f(n) = 1 + f(n + 1);\necho(f(0));", new ScadLimits { MaxRecursionDepth = 16 }), "Recursion too deep (limit 16)"),
        ["steps"] = (() => Fail("function f(n) = f(n + 1);\necho(f(0));", new ScadLimits { MaxEvaluationSteps = 1000 }), "Evaluation step limit exceeded (limit 1000)"),
        ["loops"] = (() => Fail("for (i = [0 : 100]) {}", new ScadLimits { MaxLoopIterations = 10 }), "Loop iteration limit exceeded (limit 10)"),
        ["list"] = (() => Fail("x = [for (i = [0 : 10]) i];", new ScadLimits { MaxListLength = 3 }), "List too long (limit 3)"),
        ["string"] = (() => Fail("x = str(\"abcdef\");", new ScadLimits { MaxStringLength = 3 }), "String too long (limit 3)"),
        ["instances"] = (() => Fail("for (i = [0 : 5]) cube(1);", new ScadLimits { MaxInstances = 3 }), "Instance limit exceeded (limit 3)"),
        ["output"] = (() => Fail("echo(\"abcdefghij\");", new ScadLimits { MaxOutputLength = 5 }), "Echo output limit exceeded (limit 5)"),
        ["nesting"] = (() => Fail("function f(n) = n == 0 ? [] : [for (i = [0]) f(n - 1)];\necho(len(f(300)));", new ScadLimits()), "List nesting too deep (limit 256)"),
        ["matrix-product"] = (
            () => Fail(
                "m = [for (i = [0 : 4]) [for (j = [0 : 4]) 1]];\nw = m * m;",
                new ScadLimits { MaxListLength = 20 }),
            "List too long (limit 20)"),
        ["include-depth"] = (
            () => FailWithFiles("include <a.scad>\n", new ScadLimits { MaxIncludeDepth = 1 }, ("a.scad", "include <b.scad>\n"), ("b.scad", "x = 1;\n")),
            "Include nesting too deep (limit 1)"),
        ["include-files"] = (
            () => FailWithFiles("include <a.scad>\ninclude <b.scad>\n", new ScadLimits { MaxIncludedFiles = 1 }, ("a.scad", "x = 1;\n"), ("b.scad", "y = 1;\n")),
            "Included file limit exceeded (limit 1)"),
        ["include-statements"] = (
            () => FailWithFiles("include <a.scad>\n", new ScadLimits { MaxInstances = 3 }, ("a.scad", "a = 1; b = 2; c = 3; d = 4; e = 5;\n")),
            "Included statement limit exceeded (limit 3)"),
    };

    /// <summary>Gets the names of the limit cases as theory data.</summary>
    public static TheoryData<string> LimitCaseNames => [.. LimitCases.Keys];

    /// <summary>
    ///     Validates that the message of every resource-limit error is identical in every culture.
    /// </summary>
    /// <param name="name">The case name.</param>
    [Theory]
    [MemberData(nameof(LimitCaseNames))]
    public void ScadEvaluatorCulture_LimitMessages_AreIdenticalInEveryCulture(string name)
    {
        // Arrange
        var (produce, expected) = LimitCases[name];

        // Act / Assert
        AssertIdenticalInEveryCulture(produce, expected);
    }

    /// <summary>
    ///     Validates that the messages of the parser, the lexer, the source-size check, the limits validation and the
    ///     built-in argument warnings are identical in every culture.
    /// </summary>
    [Fact]
    public void ScadEvaluatorCulture_ParserAndLimitsMessages_AreIdenticalInEveryCulture()
    {
        // Arrange
        var deep = new string('(', ScadParser.MaxNestingDepth + 10) + "1" + new string(')', ScadParser.MaxNestingDepth + 10) + ";";

        // Act / Assert
        AssertIdenticalInEveryCulture(
            () => Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse("x = " + deep)).Message,
            "Nesting too deep (limit 256)");
        AssertIdenticalInEveryCulture(
            () => Assert.Throws<ScadSyntaxException>(() => ScadDocument.Parse("x = \"\\x4\";")).Message,
            "expected 2 hexadecimal digits");
        AssertIdenticalInEveryCulture(
            () => ScadParser.CreateSourceTooLongException().Message,
            "Source exceeds the maximum length of 4194304 characters");
        AssertIdenticalInEveryCulture(
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new ScadLimits { MaxRecursionDepth = 0 }).Message,
            "The value must be between 16 and 16384.");
        AssertIdenticalInEveryCulture(
            () => Warn("echo(len());\necho(rands(1));\necho(rands(1, 2, 3, 4, 5, 6));", new ScadLimits()),
            "Function 'len' expects 1 arguments, got 0");
        AssertIdenticalInEveryCulture(
            () => Warn("echo(len());\necho(rands(1));\necho(rands(1, 2, 3, 4, 5, 6));", new ScadLimits()),
            "Function 'rands' expects 3 to 4 arguments, got 1");
        AssertIdenticalInEveryCulture(
            () => Warn("echo(a);\necho(b);\necho(c);\necho(d);", new ScadLimits { MaxDiagnostics = 2 }),
            "Further diagnostics suppressed (limit 2)");
    }

    /// <summary>
    ///     Produces a text under the invariant culture, checks that it contains the expected text, and checks that it
    ///     is identical under every other culture.
    /// </summary>
    /// <param name="produce">The function that produces the text.</param>
    /// <param name="expected">The text the baseline must contain.</param>
    private static void AssertIdenticalInEveryCulture(Func<string> produce, string expected)
    {
        var baseline = WithCulture(CultureInfo.InvariantCulture, produce);
        Assert.Contains(expected, baseline, StringComparison.Ordinal);
        foreach (var culture in CultureNames.Select(CultureInfo.GetCultureInfo).Append(CreateSyntheticCulture()))
        {
            Assert.Equal(baseline, WithCulture(culture, produce));
        }
    }

    /// <summary>
    ///     Creates a culture that formats numbers as differently as the framework allows.
    /// </summary>
    /// <returns>A writable copy of the invariant culture with another negative sign, Arabic-Indic native digits, and native digit substitution.</returns>
    private static CultureInfo CreateSyntheticCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        culture.NumberFormat.NumberGroupSeparator = "'";
        culture.NumberFormat.NativeDigits = ["\u0660", "\u0661", "\u0662", "\u0663", "\u0664", "\u0665", "\u0666", "\u0667", "\u0668", "\u0669"];
        culture.NumberFormat.DigitSubstitution = DigitShapes.NativeNational;
        return culture;
    }

    /// <summary>
    ///     Evaluates a program that must fail with an evaluation error.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The error message.</returns>
    private static string Fail(string source, ScadLimits limits) => Fails(source, With(limits)).Message;

    /// <summary>
    ///     Evaluates a program whose files come from an in-memory set and that must fail with an evaluation error.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="files">The files as (path, text) pairs.</param>
    /// <returns>The error message.</returns>
    private static string FailWithFiles(string source, ScadLimits limits, params (string Path, string Text)[] files) =>
        Assert.Throws<ScadEvaluationException>(() => Run(source, With(limits), new ScadVirtualFiles(files))).Message;

    /// <summary>
    ///     Evaluates a program and joins all its warnings.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The warnings, one per line.</returns>
    private static string Warn(string source, ScadLimits limits) =>
        string.Join('\n', Run(source, With(limits)).Diagnostics.Select(d => d.Message));
}
