using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the d3-style number formats used for axis labels. Every expected string is derived by hand from the
///     specifier grammar, the rounding rules of JavaScript's <c>toFixed</c>, <c>toExponential</c> and
///     <c>toPrecision</c> (half up on the exact binary value) and the d3 formatting steps (sign, grouping, padding).
/// </summary>
public class NumberFormatTests
{
    /// <summary>The minus sign that d3 uses for negative numbers (U+2212).</summary>
    private const string M = "\u2212";

    /// <summary>
    ///     Validates the formatted text of quantitative labels against hand-derived vectors that cover every type, flag,
    ///     alignment, fill, width, rounding edge and special value.
    /// </summary>
    [Fact]
    public void NumberFormat_Format_QuantitativeLabelsMatchHandDerivedExpectedVector()
    {
        // Arrange: (specifier, value, expected text)
        var vectors = new (string Specifier, double Value, string Expected)[]
        {
            // fixed notation with grouping and rounding
            (",.2f", 1234.5, "1,234.50"),
            (",.2f", 1234567.891, "1,234,567.89"),
            (",.0f", 1234567, "1,234,567"),
            (",.1f", 999.95, "1,000.0"),
            ("f", 1e-7, "0.000000"),
            (".10f", 0.1, "0.1000000000"),
            (".20f", 0.1, "0.10000000000000000555"),
            (".0f", 1e21, "1e+21"),
            ("f", 1.7976931348623157e308, "1.7976931348623157e+308"),

            // rounding is half up on the exact binary value: 1.005 is just below, 0.25 is exact, 0.45 is just above
            (".2f", 1.005, "1.00"),
            (".2f", 2.675, "2.67"),
            (".2f", 0.995, "0.99"),
            (".0f", 2.5, "3"),
            (".0f", 1.5, "2"),
            (".0f", 0.5, "1"),
            (".0f", 9.5, "10"),
            (".1f", 0.25, "0.3"),
            (".1f", 0.35, "0.3"),
            (".1f", 0.45, "0.5"),

            // percent
            (".0%", 0.123, "12%"),
            (".1%", 0.5, "50.0%"),
            ("%", 0.256, "25.600000%"),
            ("~%", 0.5, "50%"),
            (".1~%", 0.1234, "12.3%"),
            ("p", 0.1234, "12.3400%"),
            (".2p", 0.1234, "12%"),

            // signs and symbols
            (".2f", -3.14159, M + "3.14"),
            (".2f", -0.001, "0.00"),
            ("+.2f", -0.001, M + "0.00"),
            ("+.1f", 5, "+5.0"),
            ("+.1f", 0, "+0.0"),
            (" .1f", 5, " 5.0"),
            (" .1f", -5, M + "5.0"),
            ("(.1f", -5, "(5.0)"),
            ("(.1f", 5, "5.0"),
            ("$,.2f", 1234.5, "$1,234.50"),
            ("$,.2f", -1234.5, M + "$1,234.50"),
            ("$.0f", 0.5, "$1"),
            ("$,d", 1e6, "$1,000,000"),
            ("+d", 5, "+5"),
            ("+d", -5, M + "5"),
            ("(d", -12, "(12)"),

            // width, fill and alignment
            ("08.2f", 3.14159, "00003.14"),
            ("08.2f", -3.14159, M + "0003.14"),
            (">10.2f", 3.14159, "      3.14"),
            ("<10.2f", 3.14159, "3.14      "),
            ("^10.2f", 3.14159, "   3.14   "),
            ("*^9.1f", 2.5, "***2.5***"),
            ("=+8.1f", 12.34, "+   12.3"),
            ("*>10", 5, "*********5"),
            ("<5d", 12, "12   "),
            ("^6d", 12, "  12  "),
            ("010,d", 1234567, "01,234,567"),
            ("08,d", 1234, "0,001,234"),

            // SI prefixes
            ("s", 1500, "1.50000k"),
            (".3s", 1500, "1.50k"),
            (".2s", 1500, "1.5k"),
            ("~s", 1500, "1.5k"),
            (".3~s", 1000, "1k"),
            (".1s", 1500, "2k"),
            (".3s", 12, "12.0"),
            (".3s", 0.5, "500m"),
            (".3s", 0.00015, "150\u00b5"),
            (".3s", 0, "0.00"),
            (".3s", 1e-25, "0.10y"),
            (".3s", 1234567, "1.23M"),
            (".3s", 999999, "1.00M"),
            (".2s", -1500, M + "1.5k"),
            ("$.2s", 1500000, "$1.5M"),
            (".3s", 1e24, "1.00Y"),
            (".3s", 1e27, "1000Y"),

            // exponent, significant digits and rounded digits
            ("e", 12345.678, "1.234568e+4"),
            (".2e", 0.000123456, "1.23e-4"),
            (".2e", 9.996, "1.00e+1"),
            (".0e", 5, "5e+0"),
            (".1e", 0, "0.0e+0"),
            (".3~e", 1500, "1.5e+3"),
            ("e", 1.7976931348623157e308, "1.797693e+308"),
            ("g", 1234.5678, "1234.57"),
            (".3g", 1234.5678, "1.23e+3"),
            (".3g", 0.0001234, "0.000123"),
            (".3g", 1e-7, "1.00e-7"),
            (".3g", 999.5, "1.00e+3"),
            (".4~g", 1234.5, "1235"),
            (".5g", 5e-324, "4.9407e-324"),
            ("r", 1234.5678, "1234.57"),
            ("r", 0.000123456, "0.000123456"),
            (".2r", 1234.5678, "1200"),
            (".3r", 999.5, "1000"),
            ("n", 1234.5678, "1,234.57"),
            ("n", 1234567.891, "1.23457e+6"),

            // trimming of insignificant zeros
            (".2~f", 1.5, "1.5"),
            (".2~f", 2, "2"),
            (".2~f", 0, "0"),
            (".3~g", 0.000123456, "0.000123"),

            // the empty type and unknown types are ".12~g"
            ("", 0.1 + 0.2, "0.3"),
            ("", 1234567.891, "1234567.891"),
            ("", 1e21, "1e+21"),
            ("", 0, "0"),
            ("", -0.0, "0"),
            ("", 100, "100"),
            ("", 0.000001234, "0.000001234"),
            ("", 1.234e-7, "1.234e-7"),
            ("z", 0.5, "0.5"),
            ("Z", 0.5, "0.5"),

            // integers and radixes
            ("d", 1234.5, "1235"),
            (",d", 1234567.4, "1,234,567"),
            ("d", -2.5, M + "3"),
            ("d", 0.4, "0"),
            ("d", -0.4, "0"),
            ("d", 1e21, "1000000000000000000000"),
            ("x", 255, "ff"),
            ("X", 255, "FF"),
            ("#x", 255, "0xff"),
            ("#X", 255, "0xFF"),
            ("x", 255.6, "100"),
            ("04x", 255, "00ff"),
            ("b", 10, "1010"),
            ("#b", 5, "0b101"),
            ("o", 64, "100"),
            ("#o", 8, "0o10"),
            ("c", 65, "65"),
            ("c", 0.5, "0.5"),

            // special values keep their sign, padding and alignment
            (".2f", double.NaN, "NaN"),
            (".2f", double.PositiveInfinity, "Infinity"),
            (".2f", double.NegativeInfinity, M + "Infinity"),
            (">10.2f", double.PositiveInfinity, "  Infinity"),
            ("s", double.PositiveInfinity, "Infinity"),
        };

        // Act: format every vector with the specifier under test
        var failures = new List<string>();
        foreach (var (specifier, value, expected) in vectors)
        {
            var actual = NumberFormat.Create(specifier).Format(value);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                failures.Add($"format(\"{specifier}\")({value:R}) = \"{actual}\", expected \"{expected}\"");
            }
        }

        // Assert
        Assert.Empty(failures);
        Assert.True(vectors.Length >= 100);
    }

    /// <summary>
    ///     Validates that the output does not depend on the current culture.
    /// </summary>
    [Fact]
    public void NumberFormat_Format_IgnoresTheCurrentCulture()
    {
        // Arrange
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            // Act: a culture with a decimal comma, a space as group separator and another minus sign
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");

            // Assert
            Assert.Equal("1,234.50", NumberFormat.Create(",.2f").Format(1234.5));
            Assert.Equal("12%", NumberFormat.Create(".0%").Format(0.123));
            Assert.Equal(M + "1.5k", NumberFormat.Create(".2s").Format(-1500));
            Assert.Equal("1.234568e+4", NumberFormat.Create("e").Format(12345.678));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    /// <summary>
    ///     Validates the parsing of specifiers, their canonical text and the rejection of invalid ones.
    /// </summary>
    [Fact]
    public void NumberFormat_ParseSpecifier_ReadsEveryPartAndRejectsInvalidText()
    {
        // Arrange / Act: every part present
        var full = NumberFormat.ParseSpecifier("*^+$010,.3~g");

        // Assert
        Assert.Equal("*", full.Fill);
        Assert.Equal("^", full.Align);
        Assert.Equal("+", full.Sign);
        Assert.Equal("$", full.Symbol);
        Assert.True(full.Zero);
        Assert.Equal(10, full.Width);
        Assert.True(full.Comma);
        Assert.Equal(3, full.Precision);
        Assert.True(full.Trim);
        Assert.Equal("g", full.Type);
        Assert.Equal("*^+$010,.3~g", full.ToString());

        // Assert: the defaults of an empty specifier
        var empty = NumberFormat.ParseSpecifier(string.Empty);
        Assert.Equal((" ", ">", "-", string.Empty, string.Empty), (empty.Fill, empty.Align, empty.Sign, empty.Symbol, empty.Type));
        Assert.False(empty.Zero || empty.Comma || empty.Trim);
        Assert.Null(empty.Width);
        Assert.Null(empty.Precision);
        Assert.Equal(" >-d", NumberFormat.ParseSpecifier("d").ToString());
        Assert.Equal(" >-.2f", NumberFormat.ParseSpecifier(".2f").ToString());

        // Assert: a fill needs an alignment, and "<<5" is a fill and an alignment
        Assert.Equal("<", NumberFormat.ParseSpecifier("<<5").Fill);

        // Assert: invalid text is rejected
        foreach (var invalid in new[] { "abc", "ff", ".2.2f", "10.f", ".f", "\n>5", "2,,f", "~~g", "$$f" })
        {
            Assert.Throws<FormatException>(() => NumberFormat.ParseSpecifier(invalid));
            Assert.False(NumberFormat.TryParseSpecifier(invalid, out _));
        }

        Assert.False(NumberFormat.TryParseSpecifier(null, out _));
        Assert.Throws<ArgumentNullException>(() => NumberFormat.ParseSpecifier(null!));
        Assert.Throws<FormatException>(() => NumberFormat.Create("nope!"));
    }

    /// <summary>
    ///     Validates that a huge width is limited instead of allocating unbounded padding.
    /// </summary>
    [Fact]
    public void NumberFormat_Format_HugeWidthIsLimited()
    {
        // Arrange / Act
        var text = NumberFormat.Create(">99999999999d").Format(7);

        // Assert
        Assert.Equal(1000, text.Length);
        Assert.EndsWith("7", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the precision helpers against hand-derived values.
    /// </summary>
    [Fact]
    public void NumberFormat_PrecisionHelpers_MatchHandDerivedValues()
    {
        // Arrange / Act / Assert: decimals needed to tell numbers a step apart
        Assert.Equal(1, NumberFormat.PrecisionFixed(0.1));
        Assert.Equal(1, NumberFormat.PrecisionFixed(0.5));
        Assert.Equal(2, NumberFormat.PrecisionFixed(0.05));
        Assert.Equal(2, NumberFormat.PrecisionFixed(-0.01));
        Assert.Equal(0, NumberFormat.PrecisionFixed(1));
        Assert.Equal(0, NumberFormat.PrecisionFixed(25));
        Assert.True(double.IsNaN(NumberFormat.PrecisionFixed(0)));

        // Assert: significant digits needed for a step up to a maximum (1.01 needs three digits at a step of 0.01)
        Assert.Equal(3, NumberFormat.PrecisionRound(0.01, 1.01));
        Assert.Equal(2, NumberFormat.PrecisionRound(1, 100));
        Assert.True(double.IsNaN(NumberFormat.PrecisionRound(0, 0)));

        // Assert: decimals after an SI prefix (a step of 10 thousand at 2.7 million: two decimals of millions)
        Assert.Equal(2, NumberFormat.PrecisionPrefix(1e4, 2.7e6));
        Assert.Equal(1, NumberFormat.PrecisionPrefix(1e5, 1.3e6));
        Assert.Equal(3, NumberFormat.PrecisionPrefix(1, 1500));
        Assert.True(double.IsNaN(NumberFormat.PrecisionPrefix(1, 0)));
    }

    /// <summary>
    ///     Validates the SI-prefix formatter that scales by the prefix of a reference value.
    /// </summary>
    [Fact]
    public void NumberFormat_CreatePrefix_ScalesByThePrefixOfTheReferenceValue()
    {
        // Arrange
        var thousands = NumberFormat.CreatePrefix(".1f", 1500);
        var micro = NumberFormat.CreatePrefix(".2f", 1e-6);
        var none = NumberFormat.CreatePrefix(".0f", 12);
        var zero = NumberFormat.CreatePrefix(".0f", 0);

        // Act / Assert
        Assert.Equal("1.5k", thousands.Format(1500));
        Assert.Equal("2.5k", thousands.Format(2500));
        Assert.Equal(M + "0.5k", thousands.Format(-500));
        Assert.Equal("1.23\u00b5", micro.Format(0.00000123));
        Assert.Equal("5", none.Format(5));

        // Assert: a reference of zero has no prefix and every number is NaN, as in d3
        Assert.Equal("NaN", zero.Format(5));
    }

    /// <summary>
    ///     Validates the axis label formats that d3-scale derives from the domain and the tick count.
    /// </summary>
    [Fact]
    public void NumberFormat_TickFormat_ChoosesThePrecisionFromTheTickStep()
    {
        // Arrange: (start, stop, count, specifier, value, expected); the step of each domain is derived in the comment
        var vectors = new (double Start, double Stop, double Count, string? Specifier, double Value, string Expected)[]
        {
            // step 20: no decimals, grouped by the default ",f"
            (0, 100, 5, null, 1234, "1,234"),
            (0, 100, 5, null, 20, "20"),
            (0, 100, 5, null, -40, M + "40"),

            // step 0.1: one decimal
            (0, 1, 10, null, 0.5, "0.5"),
            (0, 1, 10, null, 1, "1.0"),
            (0, 1, 10, null, 0, "0.0"),

            // step 0.01: two decimals
            (0, 0.05, 5, null, 0.03, "0.03"),
            (0, 0.05, 5, null, 0.05, "0.05"),

            // a step of 0.2 as a percentage: one decimal minus two digits, never below zero
            (0, 1, 5, "%", 0.2, "20%"),
            (0, 1, 5, "%", 1, "100%"),

            // SI prefix: step 20000 at 100000 needs no decimals of thousands; step 200000 at 1e6 needs one decimal of millions
            (0, 1e5, 5, "s", 40000, "40k"),
            (0, 1e5, 5, "s", 0, "0k"),
            (0, 1e6, 5, "s", 400000, "0.4M"),
            (0, 1e6, 5, "s", 1e6, "1.0M"),

            // significant digits: a step of 0.2 up to 1 needs one digit; a step of 20 up to 100 with an exponent needs none after the point
            (0, 1, 5, "g", 0.2, "0.2"),
            (0, 1, 5, "g", 0.6, "0.6"),
            (0, 100, 5, "e", 40, "4e+1"),

            // negative domain: step 0.5, one decimal
            (-1, 1, 4, null, -0.5, M + "0.5"),
            (-1, 1, 4, null, 0, "0.0"),

            // a specifier that has a precision is used as it is
            (0, 100, 5, ".3f", 2, "2.000"),
            (0, 100, 5, "$,.0f", 1500, "$1,500"),
            (0, 100, 5, ",d", 1234.4, "1,234"),
        };

        // Act / Assert
        var failures = new List<string>();
        foreach (var (start, stop, count, specifier, value, expected) in vectors)
        {
            var actual = NumberFormat.TickFormat(start, stop, count, specifier).Format(value);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                failures.Add($"tickFormat({start}, {stop}, {count}, {specifier ?? "null"})({value:R}) = \"{actual}\", expected \"{expected}\"");
            }
        }

        Assert.Empty(failures);
        Assert.Throws<FormatException>(() => NumberFormat.TickFormat(0, 1, 5, "bad!"));
    }
}
