using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of <see cref="TimeUnitSpec.TryGetUnitSpecifier"/>: Vega's <c>timeUnitSpecifier</c> with its own defaults, which the
///     expression function of the same name uses (the Vega-Lite overrides of <see cref="TimeUnitSpec.Specifier"/> are not
///     applied).
/// </summary>
public class TimeUnitSpecifierTests
{
    /// <summary>
    ///     Validates the specifiers of valid unit lists, written out by hand from Vega's default table.
    /// </summary>
    /// <param name="units">The units, separated by commas.</param>
    /// <param name="expected">The expected specifier.</param>
    [Theory]
    [InlineData("year", "%Y")]
    [InlineData("year,month", "%Y-%m")]
    [InlineData("month,year", "%Y-%m")]
    [InlineData("year,month,date", "%Y-%m-%d")]
    [InlineData("month,date", "%b %d")]
    [InlineData("hours,minutes", "%H:%M")]
    [InlineData("hours,minutes,seconds", "%H:%M:%S")]
    [InlineData("year,isoweek", "%G W%V")]
    [InlineData("year,week", "%Y W%U")]
    [InlineData("year,dayofyear", "%Y %j")]
    [InlineData("quarter,month", "Q%q %b")]
    [InlineData("day,hours", "%a %H:00")]
    [InlineData("milliseconds", ".%L")]
    public void TimeUnitSpec_TryGetUnitSpecifier_ValidUnits_JoinTheDefaults(string units, string expected)
    {
        // Act
        var valid = TimeUnitSpec.TryGetUnitSpecifier(units.Split(','), null, out var specifier);

        // Assert
        Assert.True(valid);
        Assert.Equal(expected, specifier);
    }

    /// <summary>
    ///     Validates that an override replaces a default and that a combination wins over its parts.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_TryGetUnitSpecifier_Overrides_ReplaceTheDefaults()
    {
        // Arrange
        var overrides = new Dictionary<string, string> { ["year-month"] = "%B %Y", ["date"] = "%e" };

        // Act
        var combined = TimeUnitSpec.TryGetUnitSpecifier(["year", "month"], overrides, out var yearMonth);
        var single = TimeUnitSpec.TryGetUnitSpecifier(["month", "date"], overrides, out var monthDate);

        // Assert
        Assert.True(combined);
        Assert.Equal("%B %Y", yearMonth);
        Assert.True(single);
        Assert.Equal("%b %e", monthDate);
    }

    /// <summary>
    ///     Validates that the lists Vega rejects are rejected here: none, unknown, repeated, too many and incompatible.
    /// </summary>
    /// <param name="units">The units, separated by commas; empty for none.</param>
    [Theory]
    [InlineData("")]
    [InlineData("fortnight")]
    [InlineData("Year")]
    [InlineData("year,year")]
    [InlineData("week,dayofyear")]
    [InlineData("day,dayofyear")]
    [InlineData("month,dayofyear")]
    [InlineData("quarter,dayofyear")]
    [InlineData("week,isoweek")]
    [InlineData("year,quarter,month,week,isoweek,date,day,dayofyear,hours,minutes,seconds,milliseconds,year")]
    public void TimeUnitSpec_TryGetUnitSpecifier_InvalidUnits_AreRejected(string units)
    {
        // Act
        var valid = TimeUnitSpec.TryGetUnitSpecifier(units.Length == 0 ? [] : units.Split(','), null, out var specifier);

        // Assert
        Assert.False(valid);
        Assert.Equal(string.Empty, specifier);
    }

    /// <summary>
    ///     Validates that the Vega-Lite specifier of a unit still has its overrides after the shared joining code was
    ///     extracted (<c>yearmonth</c> is <c>%b %Y</c> in Vega-Lite and <c>%Y-%m</c> in Vega).
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Specifier_KeepsTheVegaLiteOverrides()
    {
        // Arrange
        Assert.True(TimeUnitSpec.TryParse("yearmonth", out var unit, out _));

        // Act
        var vegaLite = unit.Specifier();
        var vega = TimeUnitSpec.TryGetUnitSpecifier(["year", "month"], null, out var vegaSpecifier);

        // Assert
        Assert.Equal("%b %Y", vegaLite);
        Assert.True(vega);
        Assert.Equal("%Y-%m", vegaSpecifier);
    }
}
