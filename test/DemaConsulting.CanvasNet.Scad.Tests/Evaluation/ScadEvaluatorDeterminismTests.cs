using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests that evaluation is deterministic: the same document and settings always give the same echo, warnings and
///     tree, whatever the current culture.
/// </summary>
public class ScadEvaluatorDeterminismTests
{
    /// <summary>A program that mixes numbers, strings, warnings, randomness and geometry.</summary>
    private const string Program = """
        function f(n) = n < 2 ? n : f(n - 1) + f(n - 2);
        v = [for (i = [0 : 5]) f(i) / 3];
        echo(v, str(1e-7, " ", 123456.789, " ", -0), PI);
        echo(rands(0, 10, 3), rands(0, 10, 2, 42));
        echo(undefined_name);
        for (i = [0 : 2]) translate([i * 1.5, 0, 0]) cube(i + 0.25);
        """;

    /// <summary>
    ///     Validates that evaluating the same document twice gives identical results.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDeterminism_SameDocumentTwice_GivesIdenticalResults()
    {
        // Arrange / Act
        var first = Describe(Program);
        var second = Describe(Program);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Validates that the current culture never changes the output.
    /// </summary>
    /// <param name="culture">The culture name.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void ScadEvaluatorDeterminism_UnderOtherCultures_GivesIdenticalResults(string culture)
    {
        // Arrange
        var expected = Describe(Program);

        // Act
        var actual = WithCulture(culture, () => Describe(Program));

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates the random stream: reproducible per evaluation, advancing within one, and fixed for a given seed.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDeterminism_Rands_AreReproduciblePerEvaluation()
    {
        // Arrange
        const string Source = "a = rands(0, 1, 2);\nb = rands(0, 1, 2);\necho(a == b, a, rands(0, 1, 2, 5) == rands(0, 1, 2, 5));";

        // Act
        var first = Echo(Source);
        var second = Echo(Source);

        // Assert
        Assert.Equal(first, second);
        Assert.StartsWith("ECHO: false, [", first[0], StringComparison.Ordinal);
        Assert.EndsWith(", true", first[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a program can seed <c>rands</c> by the name <c>seed_value</c> and gets the numbers the
    ///     positional seed gives.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDeterminism_Rands_NamedSeedValue_MatchesPositionalSeed()
    {
        // Arrange
        const string Source = "echo(rands(0, 1, 3, seed_value = 7) == rands(0, 1, 3, 7), rands(1, 2, 2, seed_value = 3) == rands(1, 2, 2, seed_value = 3));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: true, true", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }
    /// <summary>
    ///     Validates that concurrent evaluations of the same program from several threads give identical results.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDeterminism_ConcurrentEvaluations_GiveIdenticalResults()
    {
        // Arrange
        var expected = Describe(Program);
        var results = new string[8];

        // Act
        Parallel.For(0, results.Length, new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken }, i => results[i] = Describe(Program));

        // Assert
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    /// <summary>
    ///     Describes everything observable about an evaluation.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <returns>The description.</returns>
    private static string Describe(string source)
    {
        var output = Run(source);
        return string.Join('\n', output.Echo) + "\n--\n"
            + string.Join('\n', output.Diagnostics.Select(d => $"{d.Location}: {d.Message}")) + "\n--\n"
            + ScadInstanceDumper.Dump(output.Roots) + $"\n--\n{output.Steps} {output.InstanceCount}";
    }
}
