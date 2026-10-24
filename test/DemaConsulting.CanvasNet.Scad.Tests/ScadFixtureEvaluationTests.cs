using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Golden-file tests: every <c>eval-*.scad</c> fixture is evaluated through the public
///     <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/> and its echo lines, warnings,
///     unsupported features and (through the internal test helper) instantiation tree are compared with the sibling
///     <c>.expected.txt</c>, <c>.diagnostics.txt</c>, <c>.unsupported.txt</c> and <c>.tree.txt</c> files. A missing
///     <c>.diagnostics.txt</c> or <c>.unsupported.txt</c> means "none expected". Line endings are normalized and the
///     comparison is culture-invariant.
/// </summary>
public class ScadFixtureEvaluationTests
{
    /// <summary>The name prefix of the golden-output fixtures.</summary>
    private const string GoldenPrefix = "eval-";

    /// <summary>The fixture that is also evaluated with variable overrides.</summary>
    private const string OverridesFixture = "eval-overrides";

    /// <summary>The fixture that includes and uses other files.</summary>
    private const string IncludeFixture = "eval-include-main";

    /// <summary>Gets the names (without extension) of every golden fixture.</summary>
    public static TheoryData<string> GoldenFixtures => [.. GoldenNames()];

    /// <summary>Gets the names of the golden fixtures that have an expected instantiation tree.</summary>
    public static TheoryData<string> TreeFixtures =>
        [.. GoldenNames().Where(n => File.Exists(ScadFixtureHelpers.PathOf(n + ".tree.txt")))];

    /// <summary>
    ///     Validates that the golden fixtures exist, so the theories below cannot silently run empty.
    /// </summary>
    [Fact]
    public void ScadFixtureEvaluation_GoldenFixtures_AreDiscoveredWithExpectedOutput()
    {
        // Arrange / Act
        var names = GoldenNames().ToList();

        // Assert: the documented topics are present and every fixture has an echo golden
        Assert.True(names.Count >= 15);
        Assert.All(names, name => Assert.True(File.Exists(ScadFixtureHelpers.PathOf(name + ".expected.txt")), name));
        Assert.Contains(IncludeFixture, names);
        Assert.Contains("eval-instance-tree", names);
    }

    /// <summary>
    ///     Validates that every expected-output file belongs to a fixture.
    /// </summary>
    [Fact]
    public void ScadFixtureEvaluation_ExpectedFiles_HaveAFixture()
    {
        // Arrange
        var suffixes = new[] { ".expected.txt", ".diagnostics.txt", ".unsupported.txt", ".tree.txt" };

        // Act
        var orphans = Directory.GetFiles(ScadFixtureHelpers.Directory, "*.txt")
            .Select(p => Path.GetFileName(p))
            .Where(n => !File.Exists(ScadFixtureHelpers.PathOf(StemOf(n, suffixes) + ".scad")))
            .ToList();

        // Assert
        Assert.Empty(orphans);
    }

    /// <summary>
    ///     Validates that a fixture evaluated from its file produces exactly the expected echo lines.
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ScadFixtureEvaluation_Evaluate_MatchesExpectedEcho(string name)
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(name + ".scad"));

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReadLines(name + ".expected.txt"), result.Echo);
    }

    /// <summary>
    ///     Validates that a fixture reports exactly the expected warnings (none when there is no diagnostics file).
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ScadFixtureEvaluation_Evaluate_MatchesExpectedDiagnostics(string name)
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(name + ".scad"));

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReadLines(name + ".diagnostics.txt"), DescribeWarnings(result));
    }

    /// <summary>
    ///     Validates that a fixture reports exactly the expected unsupported features (none when there is no file).
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ScadFixtureEvaluation_Evaluate_MatchesExpectedUnsupportedFeatures(string name)
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(name + ".scad"));

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReadLines(name + ".unsupported.txt"), DescribeUnsupported(result));
    }

    /// <summary>
    ///     Validates that the instantiation tree of a fixture matches its golden dump, and that the public instance
    ///     count equals the number of instances in the tree.
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    [Theory]
    [MemberData(nameof(TreeFixtures))]
    public void ScadFixtureEvaluation_Evaluate_MatchesExpectedInstanceTree(string name)
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(name + ".scad"));
        var settings = new ScadEvaluationSettings { Cancellation = TestContext.Current.CancellationToken };

        // Act: the tree is internal, so it is read through the internal evaluator entry point
        var output = ScadEvaluator.Evaluate(document, settings, new ScadEvaluationSink(settings.Limits));
        var publicResult = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert: one dump line per instance
        var expected = ReadLines(name + ".tree.txt");
        Assert.Equal(expected, ScadInstanceDumper.DumpIndented(output.Roots).Split('\n'));
        Assert.Equal(expected.Count, output.InstanceCount);
        Assert.Equal(output.InstanceCount, publicResult.InstanceCount);
    }

    /// <summary>
    ///     Validates that the override fixture produces the overridden golden output when its variables are supplied.
    /// </summary>
    [Fact]
    public void ScadFixtureEvaluation_Overrides_MatchExpectedEcho()
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(OverridesFixture + ".scad"));
        var options = new ScadEvaluationOptions
        {
            Variables = new Dictionary<string, object?>
            {
                ["width"] = 6,
                ["tag"] = "custom",
                ["$fn"] = 48,
                ["extra"] = new[] { 1, 2 },
            },
        };

        // Act
        var result = document.Evaluate(options, TestContext.Current.CancellationToken);

        // Assert: the replaced assignment of the tag is never evaluated, so its echo does not appear
        Assert.Equal(ReadLines(OverridesFixture + ".overridden.expected.txt"), result.Echo);
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    ///     Validates that the include fixture gives the same output when its files come from an in-memory resolver,
    ///     and that only the three logical paths of the fixture are requested.
    /// </summary>
    [Fact]
    public void ScadFixtureEvaluation_IncludeMain_ThroughResolver_MatchesExpectedEcho()
    {
        // Arrange
        var paths = new[] { "libs/defs.scad", "libs/nested/inner.scad", "libs/shapes.scad" };
        var files = new ScadVirtualFiles([.. paths.Select(p => (p, ScadFixtureHelpers.ReadAllText(p)))]);
        var document = ScadDocument.Parse(
            ScadFixtureHelpers.ReadAllText(IncludeFixture + ".scad"),
            new ScadOpenOptions { FileResolver = files.Resolve, SourceName = IncludeFixture + ".scad" });

        // Act
        var result = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReadLines(IncludeFixture + ".expected.txt"), result.Echo);
        Assert.Equal(ReadLines(IncludeFixture + ".diagnostics.txt"), DescribeWarnings(result));
        Assert.Equal(paths, files.Requests.Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(17, result.InstanceCount);
    }

    /// <summary>
    ///     Validates that the output is identical when the caller has a small stack and a non-English culture.
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ScadFixtureEvaluation_Evaluate_OnSmallStackUnderGermanCulture_MatchesExpectedOutput(string name)
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(name + ".scad"));

        // Act
        var result = OnSmallStack(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            return document.Evaluate(cancellationToken: CancellationToken.None);
        });

        // Assert
        Assert.Equal(ReadLines(name + ".expected.txt"), result.Echo);
        Assert.Equal(ReadLines(name + ".diagnostics.txt"), DescribeWarnings(result));
    }

    /// <summary>
    ///     Validates that evaluating a fixture twice gives identical results.
    /// </summary>
    /// <param name="name">The fixture name without extension.</param>
    [Theory]
    [MemberData(nameof(GoldenFixtures))]
    public void ScadFixtureEvaluation_Evaluate_TwiceIsDeterministic(string name)
    {
        // Arrange
        var document = ScadDocument.Open(ScadFixtureHelpers.PathOf(name + ".scad"));

        // Act
        var first = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        var second = document.Evaluate(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.Echo, second.Echo);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Equal(first.UnsupportedFeatures, second.UnsupportedFeatures);
        Assert.Equal(first.InstanceCount, second.InstanceCount);
    }

    /// <summary>
    ///     Enumerates the names (without extension) of the golden fixtures, ordered by name.
    /// </summary>
    /// <returns>The names.</returns>
    private static IEnumerable<string> GoldenNames() =>
        ScadFixtureHelpers.ValidFixtures()
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Where(n => n.StartsWith(GoldenPrefix, StringComparison.Ordinal));

    /// <summary>
    ///     Reads a golden file as lines with normalized line endings; a missing file is an empty list.
    /// </summary>
    /// <param name="fileName">The file name inside the fixture folder.</param>
    /// <returns>The lines, without trailing empty lines.</returns>
    private static List<string> ReadLines(string fileName)
    {
        var path = ScadFixtureHelpers.PathOf(fileName);
        if (!File.Exists(path))
        {
            return [];
        }

        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    /// <summary>
    ///     Describes the warnings of a result as <c>line:column: message</c> (the file is omitted so the text is the
    ///     same for every way of opening the fixture).
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The descriptions.</returns>
    private static List<string> DescribeWarnings(ScadEvaluationResult result) =>
        [.. result.Diagnostics.Select(d => $"{d.Location.Line}:{d.Location.Column}: {d.Message}")];

    /// <summary>
    ///     Describes the unsupported features of a result as <c>line:column: feature: message</c>.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The descriptions.</returns>
    private static List<string> DescribeUnsupported(ScadEvaluationResult result) =>
        [.. result.UnsupportedFeatures.Select(f => $"{f.Location.Line}:{f.Location.Column}: {f.Feature}: {f.Message}")];

    /// <summary>
    ///     Removes the first matching suffix (and a trailing <c>.overridden</c>) from a file name.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="suffixes">The suffixes to try.</param>
    /// <returns>The stem, or the whole name when no suffix matches.</returns>
    private static string StemOf(string fileName, string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.Ordinal))
            {
                var stem = fileName[..^suffix.Length];
                return stem.EndsWith(".overridden", StringComparison.Ordinal) ? stem[..^".overridden".Length] : stem;
            }
        }

        return fileName;
    }
}
