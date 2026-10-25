using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="ScadFragmentRule"/>, the documented <c>$fn</c>, <c>$fa</c>, <c>$fs</c> formula.
/// </summary>
public class ScadFragmentRuleTests
{
    /// <summary>
    ///     Validates the spot values of the rule with the default <c>$fa</c> and <c>$fs</c>.
    /// </summary>
    /// <param name="radius">The radius.</param>
    /// <param name="expected">The expected fragment count.</param>
    [Theory]
    [InlineData(1.0, 5)]
    [InlineData(2.0, 7)]
    [InlineData(10.0, 30)]
    [InlineData(100.0, 30)]
    [InlineData(0.5, 5)]
    public void ScadFragmentRule_Fragments_DefaultAngleAndSize_GivesAutomaticCount(double radius, int expected)
    {
        // Arrange / Act
        var fragments = ScadFragmentRule.Fragments(radius, 0.0, 12.0, 2.0);

        // Assert
        Assert.Equal(expected, fragments);
    }

    /// <summary>
    ///     Validates that a positive <c>$fn</c> wins, is truncated and is raised to the minimum of three.
    /// </summary>
    /// <param name="fn">The <c>$fn</c> value.</param>
    /// <param name="expected">The expected fragment count.</param>
    [Theory]
    [InlineData(64.0, 64)]
    [InlineData(64.9, 64)]
    [InlineData(1.0, 3)]
    [InlineData(2.9, 3)]
    [InlineData(3.0, 3)]
    [InlineData(0.5, 3)]
    public void ScadFragmentRule_Fragments_PositiveFn_WinsAndIsClamped(double fn, int expected)
    {
        // Arrange / Act
        var fragments = ScadFragmentRule.Fragments(5.0, fn, 12.0, 2.0);

        // Assert
        Assert.Equal(expected, fragments);
    }

    /// <summary>
    ///     Validates that a huge or infinite <c>$fn</c> is capped at the overflow guard.
    /// </summary>
    [Fact]
    public void ScadFragmentRule_Fragments_HugeFn_IsCappedAtMaximum()
    {
        // Arrange / Act / Assert
        Assert.Equal(ScadFragmentRule.MaximumFragments, ScadFragmentRule.Fragments(1.0, 1e18, 12.0, 2.0));
        Assert.Equal(ScadFragmentRule.MaximumFragments, ScadFragmentRule.Fragments(1.0, double.PositiveInfinity, 12.0, 2.0));
    }

    /// <summary>
    ///     Validates that a vanishing, negative or non-finite radius gives three fragments whatever the settings.
    /// </summary>
    /// <param name="radius">The radius.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1e-9)]
    [InlineData(1e-8)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ScadFragmentRule_Fragments_TinyOrInvalidRadius_GivesThree(double radius)
    {
        // Arrange / Act / Assert
        Assert.Equal(3, ScadFragmentRule.Fragments(radius, 0.0, 12.0, 2.0));
        Assert.Equal(3, ScadFragmentRule.Fragments(radius, 100.0, 1.0, 0.01));
    }

    /// <summary>
    ///     Validates the angle and size limits and the clamp of a zero or negative step, and the fallback of a
    ///     non-finite step to its default.
    /// </summary>
    [Fact]
    public void ScadFragmentRule_Fragments_AngleSizeAndInvalidSteps_AreHandled()
    {
        // Arrange / Act / Assert: the angle limit gives 360 / 30 = 12 (the size limit is larger)
        Assert.Equal(12, ScadFragmentRule.Fragments(100.0, 0.0, 30.0, 1.0));

        // The size limit dominates for a small radius: 2 pi 3 / 1 = 18.85 -> 19
        Assert.Equal(19, ScadFragmentRule.Fragments(3.0, 0.0, 1.0, 1.0));

        // A zero or negative fa/fs is clamped to 0.01 instead of asking for unbounded detail
        Assert.Equal(ScadFragmentRule.Fragments(1.0, 0.0, 0.01, 0.01), ScadFragmentRule.Fragments(1.0, 0.0, 0.0, -3.0));
        Assert.Equal(629, ScadFragmentRule.Fragments(1.0, 0.0, 0.0, 0.0));

        // A non-finite fa/fs falls back to the defaults
        Assert.Equal(5, ScadFragmentRule.Fragments(1.0, 0.0, double.NaN, double.NaN));
        Assert.Equal(30, ScadFragmentRule.Fragments(10.0, 0.0, double.PositiveInfinity, double.NegativeInfinity));

        // A NaN $fn is not positive and falls through to the automatic rule
        Assert.Equal(5, ScadFragmentRule.Fragments(1.0, double.NaN, 12.0, 2.0));
    }

    /// <summary>
    ///     Validates that the overload that takes the settings of an instance applies the same rule.
    /// </summary>
    [Fact]
    public void ScadFragmentRule_Fragments_InstanceSettings_MatchesScalarOverload()
    {
        // Arrange
        var settings = new ScadFragmentSettings(0.0, 6.0, 0.5);

        // Act / Assert
        Assert.Equal(ScadFragmentRule.Fragments(4.0, 0.0, 6.0, 0.5), ScadFragmentRule.Fragments(4.0, settings));
        Assert.Equal(60, ScadFragmentRule.Fragments(4.0, new ScadFragmentSettings(0.0, 6.0, 0.25)));
        Assert.Equal(51, ScadFragmentRule.Fragments(4.0, settings));
    }
}
