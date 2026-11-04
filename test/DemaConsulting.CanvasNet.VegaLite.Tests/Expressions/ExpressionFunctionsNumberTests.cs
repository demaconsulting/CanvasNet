using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the number formatting function of the expression function table: <c>format(value, specifier)</c> of Vega, which formats with a d3-format specifier of the en-US locale. The expected texts are worked out by
///     hand from the d3-format definition (the minus sign is U+2212, the thousands separator a comma), the coercion is that of JavaScript's unary plus, and an invalid specifier is a runtime error as in d3-format.
/// </summary>
public class ExpressionFunctionsNumberTests
{
    /// <summary>
    ///     Validates the formatting against d3-format for the common specifiers.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("format(1234.5678, ',.2f')", "1,234.57")]
    [InlineData("format(0.256, '.0%')", "26%")]
    [InlineData("format(0.2, '.0%')", "20%")]
    [InlineData("format(1500000, '.2s')", "1.5M")]
    [InlineData("format(-5, 'd')", "\u22125")]
    [InlineData("format(42, '$,.2f')", "$42.00")]
    [InlineData("format(255, '#x')", "0xff")]
    [InlineData("format(5, '05d')", "00005")]
    [InlineData("format(0.000123, '.2e')", "1.23e-4")]
    [InlineData("format(1200, ',.0f')", "1,200")]
    [InlineData("format(12345.6789, '.3~s')", "12.3k")]
    [InlineData("format(3, '+d')", "+3")]
    [InlineData("format(7, '>5')", "    7")]
    [InlineData("format(1234.5, '')", "1234.5")]
    [InlineData("format(NaN, 'd')", "NaN")]
    [InlineData("format(1 / 0, 'd')", "\u221e")]
    [InlineData("format(-1 / 0, 'd')", "\u2212\u221e")]
    [InlineData("format(1 / 0, 'f')", "Infinity")]
    [InlineData("format(1e21, 'f')", "1e+21")]
    [InlineData("format('foo', 'c')", "foo")]
    [InlineData("format(true, 'c')", "true")]
    [InlineData("format(65, 'c')", "65")]
    [InlineData("format(-5, 'c')", "-5")]
    [InlineData("format(1.5, 'c')", "1.5")]
    [InlineData("format([1, 2], 'c')", "1,2")]
    [InlineData("format({}, 'c')", "[object Object]")]
    [InlineData("format(1 / 0, 'c')", "Infinity")]
    [InlineData("format(NaN, 'c')", "NaN")]
    [InlineData("format('x', '>5c')", "    x")]
    [InlineData("format('x', '*^5c')", "**x**")]
    [InlineData("format('ab', '$c')", "$ab")]
    [InlineData("format('a', '05c')", "0000a")]
    [InlineData("format('x', '+c')", "x")]
    [InlineData("format('1234', ',c')", "1234")]
    public void ExpressionFunctionsNumber_Format_MatchesD3(string text, string expected)
    {
        // Act
        var result = Eval(text);

        // Assert
        Assert.True(result.IsString, $"'{text}' gave {result.Kind}");
        Assert.Equal(expected, result.Text);
    }

    /// <summary>
    ///     Validates the coercion of the value like JavaScript's unary plus and Vega's <c>null</c> rule: null is the text <c>null</c>, an unreadable text and an object are NaN, and a text, a boolean, a date
    ///     and an array of one number are read as numbers.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsNumber_Format_CoercesTheValueLikeUnaryPlus()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        string Call(params DataValue[] arguments) => ExpressionFunctions.Invoke("format", arguments, context).Text!;

        // Act / Assert: Vega gives the text null for null (and not 0)
        Assert.Equal("null", Call(default, DataValue.FromString("d")));
        Assert.Equal("12", Eval("format('12', 'd')").Text);
        Assert.Equal("1", Eval("format(true, 'd')").Text);
        Assert.Equal("NaN", Eval("format('twelve', 'd')").Text);
        Assert.Equal("NaN", Eval("format({}, 'd')").Text);
        Assert.Equal("5", Eval("format([5], 'd')").Text);
        Assert.Equal("1,000", Eval("format(datetime(1970, 0, 1, 0, 0, 1), ',d')").Text);
        Assert.Equal("1,000", Call(DataValue.FromDate(1000), DataValue.FromString(",d")));
    }

    /// <summary>
    ///     Validates that the type <c>c</c> prints the original value as JavaScript text instead of reading it as a number (d3-format's <c>x + ""</c>): a null value is still the text <c>null</c>, a text longer
    ///     than the string limit is a runtime error, any other type still reads the value as a number (a text is NaN), and an invalid specifier is still an error for a text.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsNumber_Format_TypeCPrintsTheOriginalValueAsText()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        var tight = new ExpressionFunctionContext(new VegaLiteLimits { MaxStringLength = 5 }, CancellationToken.None);
        string Call(params DataValue[] arguments) => ExpressionFunctions.Invoke("format", arguments, context).Text!;

        // Act
        var nothing = Call(default, DataValue.FromString("c"));
        var tooLong = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromString("abcdefghij"), DataValue.FromString("c")], tight));
        var invalid = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromString("foo"), DataValue.FromString("$$c")], context));

        // Assert
        Assert.Equal("null", nothing);
        Assert.Equal("foo", Call(DataValue.FromString("foo"), DataValue.FromString("c")));
        Assert.Equal("true", Call(DataValue.FromBoolean(true), DataValue.FromString("c")));
        Assert.Equal("NaN", Call(DataValue.FromString("foo"), DataValue.FromString("d")));
        Assert.Contains("VegaLiteLimits.MaxStringLength", tooLong.Message, StringComparison.Ordinal);
        Assert.Equal("invalid format: $$c", invalid.Message);
    }

    /// <summary>
    ///     Validates that an invalid or missing specifier is a runtime error with the message of d3-format, and that through the evaluator it is a render exception located at the expression.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsNumber_Format_InvalidSpecifierIsARuntimeError()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);

        // Act
        var dots = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1), DataValue.FromString(".2.3f")], context));
        var dollars = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1), DataValue.FromString("$$")], context));
        var missing = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1)], context));
        var located = Assert.Throws<VegaLiteRenderException>(() => Eval("format(1, 'nonsense')"));

        // Assert
        Assert.Equal("invalid format: .2.3f", dots.Message);
        Assert.Equal("invalid format: $$", dollars.Message);
        Assert.Equal("invalid format: undefined", missing.Message);
        Assert.Contains("invalid format: nonsense", located.Message, StringComparison.Ordinal);
        Assert.Equal(Location.Pointer, located.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a null value is the text <c>null</c> without the specifier being read, charged or compiled, as Vega 6.4.0's <c>format.js</c> tests <c>value === null</c> before it compiles the specifier:
    ///     <c>format(null)</c> and <c>format(null, 'nonsense')</c> are the text <c>null</c>, a specifier too long for the step budget is not charged for a null value, while any other value still fails an invalid or
    ///     missing specifier.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsNumber_Format_NullIsTheTextNullWithoutReadingTheSpecifier()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        var budget = new ExpressionFunctionContext(Steps(3), CancellationToken.None);
        string Call(ExpressionFunctionContext on, params DataValue[] arguments) => ExpressionFunctions.Invoke("format", arguments, on).Text!;

        // Act
        var missing = Call(context, default(DataValue));
        var invalid = Call(context, default(DataValue), DataValue.FromString("nonsense"));
        var located = Eval("format(null, 'nonsense')");
        var oversize = Call(budget, default(DataValue), DataValue.FromString(new string('x', 640)));
        var stepsAfterOversize = budget.Steps;

        // Assert: the text null, with no error and no charge for the specifier
        Assert.Equal("null", missing);
        Assert.Equal("null", invalid);
        Assert.True(located.IsString);
        Assert.Equal("null", located.Text);
        Assert.Equal("null", oversize);
        Assert.Equal(0, stepsAfterOversize);
        Assert.Equal("null", Call(context, default(DataValue), DataValue.FromString(",d")));
        Assert.Equal("null", Call(context, default(DataValue), DataValue.FromString("c")));
        Assert.Equal("null", Eval("format(null, ',d')").Text);

        // Assert: a value that is not null still needs a valid specifier, so the check is not vacuous
        var notNullMissing = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1)], context));
        var notNullInvalid = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1), DataValue.FromString("nonsense")], context));
        Assert.Equal("invalid format: undefined", notNullMissing.Message);
        Assert.Equal("invalid format: nonsense", notNullInvalid.Message);
    }

    /// <summary>
    ///     Validates the bounds: the formatter cache holds at most 64 specifiers and is cleared when it is full (and still formats), a specifier longer than 64 characters is not kept, the step budget is charged by the
    ///     specifier and the result, a result over the string limit is a runtime error, and the result does not depend on the current culture.
    /// </summary>
    [Fact]
    public void ExpressionFunctionsNumber_Format_CachesAndBoundsWork()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        var tight = new ExpressionFunctionContext(new VegaLiteLimits { MaxStringLength = 5 }, CancellationToken.None);
        var budget = new ExpressionFunctionContext(Steps(3), CancellationToken.None);
        var longSpecifier = "." + new string('0', 70) + "2f";

        // Act: 65 distinct specifiers clear the cache of 64 and the formatter still works afterwards
        var texts = new List<string>();
        for (var precision = 0; precision < 65; precision++)
        {
            texts.Add(ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1.5), DataValue.FromString("." + precision.ToString(CultureInfo.InvariantCulture) + "f")], context).Text!);
        }

        var again = ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1.5), DataValue.FromString(".1f")], context).Text;
        var again2 = ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1.5), DataValue.FromString(".1f")], context).Text;
        var notKept = ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1.5), DataValue.FromString(longSpecifier)], context).Text;
        var limit = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(123456), DataValue.FromString(",d")], tight));
        var charged = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("format", [DataValue.FromNumber(1), DataValue.FromString(new string('x', 640))], budget));
        var turkish = UnderCulture("tr-TR", () => Eval("format(1234.5, ',.1f')").Text);
        var german = UnderCulture("de-DE", () => Eval("format(1234.5, ',.1f')").Text);

        // Assert
        Assert.Equal("2", texts[0]);
        Assert.Equal("1.5", texts[1]);
        Assert.Equal(65, texts.Count);
        Assert.Equal("1.5", again);
        Assert.Equal("1.5", again2);
        Assert.Equal("1.50", notKept);
        Assert.Contains("VegaLiteLimits.MaxStringLength", limit.Message, StringComparison.Ordinal);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", charged.Message, StringComparison.Ordinal);
        Assert.Equal("1,234.5", turkish);
        Assert.Equal("1,234.5", german);
    }
}
