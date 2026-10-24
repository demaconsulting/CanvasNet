using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadNumberFormatter"/>.
/// </summary>
public class ScadNumberFormatterTests
{
    /// <summary>
    ///     Gets the pinned number formatting table: the value and its exact text.
    /// </summary>
    public static TheoryData<double, string> Table => new()
    {
        { 0, "0" },
        { -0.0, "-0" },
        { 1, "1" },
        { 1.5, "1.5" },
        { 0.1, "0.1" },
        { 1.0 / 3, "0.333333" },
        { 2.0 / 3, "0.666667" },
        { 100, "100" },
        { 100000, "100000" },
        { 123456, "123456" },
        { 1234567, "1.23457e+06" },
        { 999999.5, "1e+06" },
        { 12345.67, "12345.7" },
        { 0.0001, "0.0001" },
        { 0.00001, "1e-05" },
        { 0.000012345678, "1.23457e-05" },
        { 1e10, "1e+10" },
        { 1e100, "1e+100" },
        { -2.5, "-2.5" },
        { 3.14159265, "3.14159" },
        { 1e-300, "1e-300" },
        { 1.5e-7, "1.5e-07" },
        { 0.00012345, "0.00012345" },
        { 100000.4, "100000" },
        { 99999.95, "99999.9" },
        { 5e-324, "4.94066e-324" },
        { double.MaxValue, "1.79769e+308" },
        { double.NaN, "nan" },
        { double.PositiveInfinity, "inf" },
        { double.NegativeInfinity, "-inf" },
    };

    /// <summary>
    ///     Validates the formatting table under the invariant-independent algorithm.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [MemberData(nameof(Table))]
    public void ScadNumberFormatter_Format_Table_ReturnsPinnedText(double value, string expected)
    {
        // Arrange / Act
        var text = ScadNumberFormatter.Format(value);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates that the output is identical under cultures with different separators and casing rules.
    /// </summary>
    /// <param name="culture">The current culture to use.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void ScadNumberFormatter_Format_OtherCultures_IsIdentical(string culture)
    {
        // Arrange: a mix of fixed, scientific and negative values
        double[] values = [1.5, -2.5, 0.0001, 1.5e-7, 1234567, 12345.67, 1e100];

        // Act
        var invariant = values.Select(ScadNumberFormatter.Format).ToArray();
        var localized = ScadEvaluationTestHelpers.WithCulture(culture, () => values.Select(ScadNumberFormatter.Format).ToArray());

        // Assert
        Assert.Equal(invariant, localized);
        Assert.Equal("1.5", localized[0]);
        Assert.Equal("1.5e-07", localized[3]);
    }

    /// <summary>
    ///     Validates the rounding of an exact decimal tie (one pinned case, so any runtime drift is caught).
    /// </summary>
    [Fact]
    public void ScadNumberFormatter_Format_ExactTie_RoundsToEvenDigit()
    {
        // Arrange: 1234565 is exactly representable and sits exactly between 1.23456e6 and 1.23457e6;
        // .NET's exact digit generation resolves an exact tie to the even digit
        var value = 1234565.0;

        // Act
        var text = ScadNumberFormatter.Format(value);

        // Assert: documented pin (the neighbouring odd case rounds up to the even digit as well)
        Assert.Equal("1.23456e+06", text);
        Assert.Equal("1.23458e+06", ScadNumberFormatter.Format(1234575.0));
    }

    /// <summary>
    ///     Validates the smallest values that still use fixed notation.
    /// </summary>
    [Fact]
    public void ScadNumberFormatter_Format_SmallestFixed_UsesFixedNotation()
    {
        // Arrange / Act / Assert: exponent -4 is fixed, -5 is scientific
        Assert.Equal("0.0001", ScadNumberFormatter.Format(1e-4));
        Assert.Equal("1e-05", ScadNumberFormatter.Format(1e-5));
        Assert.Equal("0.000123457", ScadNumberFormatter.Format(0.000123456789));
    }

    /// <summary>
    ///     Validates that the result never contains culture-specific characters.
    /// </summary>
    [Fact]
    public void ScadNumberFormatter_Format_AnyValue_UsesOnlyInvariantCharacters()
    {
        // Arrange
        double[] values = [-1234.5678, 1e-9, 7e20, 123456.789];

        // Act / Assert
        foreach (var value in values)
        {
            var text = ScadNumberFormatter.Format(value);
            Assert.All(text, c => Assert.True(char.IsAsciiDigit(c) || c is '-' or '+' or '.' or 'e', text));
            Assert.Equal(text, ScadNumberFormatter.Format(value));
        }

        Assert.Equal("-1234.57", ScadNumberFormatter.Format(-1234.5678));
        Assert.DoesNotContain(',', ScadNumberFormatter.Format(1234.5));
    }
}
