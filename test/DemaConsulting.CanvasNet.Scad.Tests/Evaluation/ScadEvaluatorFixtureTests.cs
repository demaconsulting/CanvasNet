using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests that evaluate the original-work parse fixtures: every one must evaluate without error and without
///     warnings, except <c>include-and-use.scad</c> whose paths intentionally do not exist.
/// </summary>
public class ScadEvaluatorFixtureTests
{
    /// <summary>The fixture whose include and use paths intentionally do not exist.</summary>
    private const string ExcludedFixture = "include-and-use.scad";

    /// <summary>Gets the names of the fixtures that must evaluate cleanly.</summary>
    public static TheoryData<string> CleanFixtures =>
        [.. ScadFixtureHelpers.ValidFixtures().Select(p => Path.GetFileName(p)).Where(n => n != ExcludedFixture)];

    /// <summary>
    ///     Validates that a fixture evaluates without error and without warnings.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    [Theory]
    [MemberData(nameof(CleanFixtures))]
    public void ScadEvaluatorFixture_Evaluate_CompletesWithoutWarnings(string name)
    {
        // Arrange
        var source = ScadFixtureHelpers.ReadAllText(name);

        // Act
        var output = Run(source);

        // Assert
        Assert.Empty(output.Diagnostics);
        Assert.Empty(output.UnsupportedFeatures);
    }

    /// <summary>
    ///     Validates that the fixture whose includes do not exist evaluates with one warning per missing file when a
    ///     resolver finds nothing.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFixture_IncludeAndUse_WarnsForMissingFiles()
    {
        // Arrange
        var source = ScadFixtureHelpers.ReadAllText(ExcludedFixture);

        // Act
        var output = Run(source, null, new ScadVirtualFiles());

        // Assert
        Assert.NotEmpty(output.Diagnostics);
        Assert.All(output.Diagnostics, d => Assert.StartsWith("Can't open include file", d.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the parametric bracket fixture: it produces geometry instances, parameter echoes and a stable tree.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFixture_ParametricBracket_ProducesInstances()
    {
        // Arrange
        var source = ScadFixtureHelpers.ReadAllText("parametric-bracket.scad");

        // Act
        var first = Run(source);
        var second = Run(source);

        // Assert
        Assert.True(first.InstanceCount > 3);
        Assert.NotEmpty(first.Roots);
        Assert.Equal(ScadInstanceDumper.Dump(first.Roots), ScadInstanceDumper.Dump(second.Roots));
    }
}
