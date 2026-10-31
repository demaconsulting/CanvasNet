using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the closed expression function table: the exact whitelist, every function family with expected values
///     written out by hand from the JavaScript and Vega definitions, regular expression safety, limits and the
///     step accounting of the per-evaluation context.
/// </summary>
public class ExpressionFunctionsTests
{
    /// <summary>The tolerance for transcendental functions, whose last bits depend on the host math library.</summary>
    private const double Tolerance = 1e-12;

    /// <summary>
    ///     Validates that the function table is exactly the documented whitelist: nothing more (no randomness, clock,
    ///     number format or color function) and nothing less.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Names_AreExactlyTheWhitelist()
    {
        // Arrange: the whitelist by family
        string[] math =
        [
            "abs", "acos", "acosh", "asin", "asinh", "atan", "atan2", "atanh", "ceil", "clamp", "cos", "cosh", "exp", "floor", "log",
            "log10", "max", "min", "pow", "round", "sign", "sin", "sinh", "sqrt", "tan", "tanh", "trunc",
        ];
        string[] text =
        [
            "lower", "upper", "trim", "substring", "slice", "indexof", "lastindexof", "startswith", "endswith", "includes",
            "replace", "split", "join", "pad", "length",
        ];
        string[] types =
        [
            "isArray", "isBoolean", "isDate", "isFinite", "isNaN", "isNumber", "isObject", "isRegExp", "isString", "isValid",
            "toBoolean", "toNumber", "toString",
        ];
        string[] regex = ["regexp", "test"];
        string[] time =
        [
            "datetime", "utc", "date", "day", "year", "month", "hours", "minutes", "seconds", "milliseconds", "time", "timezoneoffset",
            "utcdate", "utcday", "utcyear", "utcmonth", "utchours", "utcminutes", "utcseconds", "utcmilliseconds",
            "quarter", "utcquarter", "week", "utcweek", "isoweek", "utcisoweek", "dayofyear", "utcdayofyear",
            "timeOffset", "utcOffset", "timeSequence", "utcSequence", "timeUnitSpecifier",
            "timeFormat", "utcFormat", "timeParse", "utcParse", "monthFormat", "monthAbbrevFormat", "dayFormat", "dayAbbrevFormat",
        ];
        var expected = math.Concat(text).Concat(types).Concat(regex).Concat(time).Concat(["toDate"]).Order(StringComparer.Ordinal).ToArray();

        // Act
        var names = ExpressionFunctions.Names();

        // Assert
        Assert.Equal(expected, names);
        Assert.Equal(99, names.Count);
        foreach (var excluded in new[] { "random", "now", "format", "rgb", "scale", "if", "eval", "Function" })
        {
            Assert.DoesNotContain(excluded, names);
            Assert.False(ExpressionFunctions.TryGet(excluded, out _));
        }
    }

    /// <summary>
    ///     Validates that function names are case sensitive and that invoking an unknown name is an argument error.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Invoke_UnknownName_ThrowsArgumentException()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);

        // Act / Assert
        Assert.False(ExpressionFunctions.TryGet("ABS", out _));
        Assert.False(ExpressionFunctions.TryGet("isNan", out _));
        Assert.Throws<ArgumentException>(() => ExpressionFunctions.Invoke("nope", [], context));
        Assert.Throws<ArgumentNullException>(() => ExpressionFunctions.Invoke("abs", [], null!));
        Assert.Equal(3, ExpressionFunctions.Invoke("abs", [DataValue.FromNumber(-3)], context).Number);
    }

    /// <summary>
    ///     Validates the math functions at exactly representable points and the JavaScript special cases.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("abs(-3.5)", "3.5")]
    [InlineData("abs('-2')", "2")]
    [InlineData("abs()", "NaN")]
    [InlineData("abs(null)", "0")]
    [InlineData("acos(1)", "0")]
    [InlineData("acosh(1)", "0")]
    [InlineData("asin(0)", "0")]
    [InlineData("asinh(0)", "0")]
    [InlineData("atan(0)", "0")]
    [InlineData("atan2(0, 1)", "0")]
    [InlineData("atanh(0)", "0")]
    [InlineData("ceil(1.2)", "2")]
    [InlineData("ceil(-1.5)", "-1")]
    [InlineData("floor(-1.2)", "-2")]
    [InlineData("floor(1.8)", "1")]
    [InlineData("clamp(5, 0, 3)", "3")]
    [InlineData("clamp(-1, 0, 3)", "0")]
    [InlineData("clamp(2, 0, 3)", "2")]
    [InlineData("clamp(NaN, 0, 3)", "NaN")]
    [InlineData("cos(0)", "1")]
    [InlineData("cosh(0)", "1")]
    [InlineData("exp(0)", "1")]
    [InlineData("log(1)", "0")]
    [InlineData("log(0)", "-Infinity")]
    [InlineData("log(-1)", "NaN")]
    [InlineData("log10(100)", "2")]
    [InlineData("max(1, 3, 2)", "3")]
    [InlineData("max('4', 2)", "4")]
    [InlineData("max()", "-Infinity")]
    [InlineData("min()", "Infinity")]
    [InlineData("min(1, 2)", "1")]
    [InlineData("min(-1, -5, 0)", "-5")]
    [InlineData("max(1, NaN)", "NaN")]
    [InlineData("min(NaN, 1)", "NaN")]
    [InlineData("pow(2, 10)", "1024")]
    [InlineData("pow(2, -1)", "0.5")]
    [InlineData("pow(1, Infinity)", "NaN")]
    [InlineData("pow(-1, -Infinity)", "NaN")]
    [InlineData("pow(1, NaN)", "NaN")]
    [InlineData("pow(NaN, 0)", "1")]
    [InlineData("pow(2, Infinity)", "Infinity")]
    [InlineData("round(2.5)", "3")]
    [InlineData("round(-2.5)", "-2")]
    [InlineData("round(2.4)", "2")]
    [InlineData("round(-2.6)", "-3")]
    [InlineData("round(0.5)", "1")]
    [InlineData("sign(-5)", "-1")]
    [InlineData("sign(5)", "1")]
    [InlineData("sign(0)", "0")]
    [InlineData("sign(NaN)", "NaN")]
    [InlineData("sin(0)", "0")]
    [InlineData("sinh(0)", "0")]
    [InlineData("sqrt(16)", "4")]
    [InlineData("sqrt(-1)", "NaN")]
    [InlineData("tan(0)", "0")]
    [InlineData("tanh(0)", "0")]
    [InlineData("trunc(-1.7)", "-1")]
    [InlineData("trunc(1.7)", "1")]
    public void ExpressionFunctions_Math_ExactValuesAndSpecialCases(string text, string expected)
    {
        // Arrange / Act
        var value = EvalText(text);

        // Assert
        Assert.Equal(expected, value);
    }

    /// <summary>
    ///     Validates that the rounding and sign functions keep negative zero where JavaScript does.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Math_KeepsNegativeZero()
    {
        // Arrange / Act / Assert: the reciprocal exposes the sign of zero
        Assert.True(double.IsNegativeInfinity(EvalNumber("1 / round(-0.4)")));
        Assert.True(double.IsNegativeInfinity(EvalNumber("1 / ceil(-0.5)")));
        Assert.True(double.IsNegativeInfinity(EvalNumber("1 / sign(-0)")));
        Assert.True(double.IsPositiveInfinity(EvalNumber("1 / max(-0, 0)")));
        Assert.True(double.IsNegativeInfinity(EvalNumber("1 / min(-0, 0)")));
    }

    /// <summary>
    ///     Validates the transcendental functions against reference values within a tolerance.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The reference value.</param>
    [Theory]
    [InlineData("sin(PI / 2)", 1.0)]
    [InlineData("cos(PI)", -1.0)]
    [InlineData("tan(PI / 4)", 1.0)]
    [InlineData("asin(1)", 1.5707963267948966)]
    [InlineData("acos(0)", 1.5707963267948966)]
    [InlineData("atan(1)", 0.7853981633974483)]
    [InlineData("atan2(1, 1)", 0.7853981633974483)]
    [InlineData("atan2(1, 0)", 1.5707963267948966)]
    [InlineData("exp(1)", 2.718281828459045)]
    [InlineData("log(E)", 1.0)]
    [InlineData("log10(2)", 0.3010299956639812)]
    [InlineData("sqrt(2)", 1.4142135623730951)]
    [InlineData("pow(2, 0.5)", 1.4142135623730951)]
    [InlineData("sinh(1)", 1.1752011936438014)]
    [InlineData("cosh(1)", 1.5430806348152437)]
    [InlineData("tanh(1)", 0.7615941559557649)]
    [InlineData("asinh(1)", 0.881373587019543)]
    [InlineData("acosh(2)", 1.3169578969248166)]
    [InlineData("atanh(0.5)", 0.5493061443340548)]
    public void ExpressionFunctions_Math_TranscendentalFunctionsMatchReferenceValues(string text, double expected)
    {
        // Arrange / Act
        var value = EvalNumber(text);

        // Assert
        Assert.InRange(value, expected - Tolerance, expected + Tolerance);
    }

    /// <summary>
    ///     Validates the string functions with hand-derived JavaScript results.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected value as JavaScript text.</param>
    [Theory]
    [InlineData("lower('ABC xYz')", "abc xyz")]
    [InlineData("upper('abc xYz')", "ABC XYZ")]
    [InlineData("upper(null)", "NULL")]
    [InlineData("upper(12)", "12")]
    [InlineData("trim('  a b \\n')", "a b")]
    [InlineData("trim('\\u00a0 a \\ufeff')", "a")]
    [InlineData("trim('')", "")]
    [InlineData("substring('hello', 1, 3)", "el")]
    [InlineData("substring('hello', 3, 1)", "el")]
    [InlineData("substring('hello', -2)", "hello")]
    [InlineData("substring('hello', 2)", "llo")]
    [InlineData("substring('abcd', 1, 100)", "bcd")]
    [InlineData("substring('hello', 2, 2)", "")]
    [InlineData("substring('hello', NaN, 2)", "he")]
    [InlineData("slice('hello', -3)", "llo")]
    [InlineData("slice('hello', 1, -1)", "ell")]
    [InlineData("slice('hello', 3, 1)", "")]
    [InlineData("slice('abcd', 1, 100)", "bcd")]
    [InlineData("slice('hello', -100, 2)", "he")]
    [InlineData("indexof('hello', 'l')", "2")]
    [InlineData("indexof('hello', 'z')", "-1")]
    [InlineData("indexof('abc', '')", "0")]
    [InlineData("indexof(12345, 34)", "2")]
    [InlineData("lastindexof('hello', 'l')", "3")]
    [InlineData("lastindexof('hello', 'z')", "-1")]
    [InlineData("lastindexof('abc', '')", "3")]
    [InlineData("startswith('hello', 'he')", "true")]
    [InlineData("startswith('hello', 'el')", "false")]
    [InlineData("startswith('hello', 'el', 1)", "true")]
    [InlineData("endswith('hello', 'lo')", "true")]
    [InlineData("endswith('hello', 'he')", "false")]
    [InlineData("endswith('hello', 'll', 4)", "true")]
    [InlineData("includes('hello', 'ell')", "true")]
    [InlineData("includes('hello', 'z')", "false")]
    [InlineData("includes('hello', '')", "true")]
    [InlineData("join([1, null, 'a'], '-')", "1--a")]
    [InlineData("join([1, 2])", "1,2")]
    [InlineData("join([], '-')", "")]
    [InlineData("join([[1, 2], 3], ';')", "1,2;3")]
    [InlineData("join('abc')", "null")]
    [InlineData("pad('7', 3, '0', 'left')", "007")]
    [InlineData("pad('ab', 5)", "ab   ")]
    [InlineData("pad('ab', 5, '*', 'right')", "ab***")]
    [InlineData("pad('ab', 5, '*', 'center')", "*ab**")]
    [InlineData("pad('abc', 2)", "abc")]
    [InlineData("pad('x', 3, '-=')", "x-=-=")]
    [InlineData("pad('x', 3, '')", "x  ")]
    [InlineData("pad(5, 3, 0, 'left')", "005")]
    [InlineData("length('abc')", "3")]
    [InlineData("length('')", "0")]
    [InlineData("length([1, 2])", "2")]
    [InlineData("length(5)", "null")]
    [InlineData("length({length: 3})", "null")]
    public void ExpressionFunctions_Text_FollowJavaScriptAndVega(string text, string expected)
    {
        // Arrange: a compiled expression and an evaluator that serves several evaluations
        var compiled = Compile(text);
        var evaluator = new ExpressionEvaluator(null, CancellationToken.None);

        // Act
        var first = evaluator.Evaluate(compiled).ToText();
        var second = evaluator.Evaluate(compiled).ToText();

        // Assert
        Assert.Equal(expected, first);
        Assert.Equal(expected, second);
    }

    /// <summary>
    ///     Validates that the functions that accept both strings and arrays produce arrays, with the SameValueZero
    ///     and strict equality rules of the respective JavaScript methods.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Array_SliceSearchAndMembership()
    {
        // Arrange / Act / Assert: slices are new arrays
        var slice = Eval("slice([1, 2, 3, 4], 1, 3)");
        Assert.True(slice.IsArray);
        Assert.Equal("2,3", slice.ToText());
        Assert.Equal("3", EvalText("slice([1, 2, 3], -1)"));
        Assert.Equal(0, Eval("slice([1, 2, 3], 2, 1)").Elements.Length);
        Assert.True(Eval("slice([1, 2, 3], 5)").IsArray);

        // Assert: indexof and lastindexof use strict equality
        Assert.Equal("1", EvalText("indexof([1, 2, 3, 2], 2)"));
        Assert.Equal("3", EvalText("lastindexof([1, 2, 3, 2], 2)"));
        Assert.Equal("-1", EvalText("indexof([1, 2], '1')"));
        Assert.Equal("-1", EvalText("indexof([NaN], NaN)"));
        Assert.Equal("0", EvalText("indexof([0], -0)"));

        // Assert: includes uses SameValueZero, so NaN is found
        Assert.True(Eval("includes([1, NaN], NaN)").Boolean);
        Assert.False(Eval("includes([1, 2], '1')").Boolean);
        Assert.True(Eval("includes(['a', 'b'], 'b')").Boolean);
        Assert.False(Eval("includes([[1]], [1])").Boolean);
    }

    /// <summary>
    ///     Validates the type predicates for every kind of value.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("isArray([])", true)]
    [InlineData("isArray('a')", false)]
    [InlineData("isArray({})", false)]
    [InlineData("isBoolean(false)", true)]
    [InlineData("isBoolean(0)", false)]
    [InlineData("isDate(1)", false)]
    [InlineData("isDate('2020-01-01')", false)]
    [InlineData("isFinite(1)", true)]
    [InlineData("isFinite('1')", false)]
    [InlineData("isFinite(Infinity)", false)]
    [InlineData("isFinite(NaN)", false)]
    [InlineData("isNaN(NaN)", true)]
    [InlineData("isNaN('x')", false)]
    [InlineData("isNaN(1)", false)]
    [InlineData("isNumber(1)", true)]
    [InlineData("isNumber(NaN)", true)]
    [InlineData("isNumber(Infinity)", true)]
    [InlineData("isNumber('1')", false)]
    [InlineData("isObject({})", true)]
    [InlineData("isObject([])", true)]
    [InlineData("isObject(/a/)", true)]
    [InlineData("isObject(null)", false)]
    [InlineData("isObject('a')", false)]
    [InlineData("isRegExp(/a/)", true)]
    [InlineData("isRegExp('a')", false)]
    [InlineData("isString('')", true)]
    [InlineData("isString(1)", false)]
    [InlineData("isValid(0)", true)]
    [InlineData("isValid('')", true)]
    [InlineData("isValid(false)", true)]
    [InlineData("isValid(null)", false)]
    [InlineData("isValid(NaN)", false)]
    [InlineData("isValid()", false)]
    public void ExpressionFunctions_Types_PredicatesFollowVega(string text, bool expected)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert: a boolean whose JavaScript text is true or false
        Assert.True(value.IsBoolean);
        Assert.Equal(expected, value.Boolean);
        Assert.Equal(expected ? "true" : "false", value.ToText());
    }

    /// <summary>
    ///     Validates the conversion functions: null and the empty string stay null, everything else converts like
    ///     JavaScript.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Conversions_MapNullAndEmptyToNull()
    {
        // Arrange / Act / Assert: toBoolean
        Assert.True(Eval("toBoolean(null)").IsNull);
        Assert.True(Eval("toBoolean('')").IsNull);
        Assert.False(Eval("toBoolean('false')").Boolean);
        Assert.False(Eval("toBoolean('0')").Boolean);
        Assert.False(Eval("toBoolean(0)").Boolean);
        Assert.False(Eval("toBoolean(NaN)").Boolean);
        Assert.True(Eval("toBoolean('x')").Boolean);
        Assert.True(Eval("toBoolean(1)").Boolean);
        Assert.True(Eval("toBoolean('true')").Boolean);
        Assert.True(Eval("toBoolean([])").Boolean);

        // Assert: toNumber
        Assert.True(Eval("toNumber('')").IsNull);
        Assert.True(Eval("toNumber(null)").IsNull);
        Assert.Equal(12.5, EvalNumber("toNumber('12.5')"));
        Assert.True(double.IsNaN(EvalNumber("toNumber('abc')")));
        Assert.Equal(16, EvalNumber("toNumber(' 0x10 ')"));
        Assert.Equal(1, EvalNumber("toNumber(true)"));
        Assert.Equal(0, EvalNumber("toNumber([])"));
        Assert.Equal(-3, EvalNumber("toNumber([-3])"));

        // Assert: toString
        Assert.True(Eval("toString(null)").IsNull);
        Assert.True(Eval("toString('')").IsNull);
        Assert.Equal("12.5", Eval("toString(12.5)").Text);
        Assert.Equal("1e+21", Eval("toString(1e21)").Text);
        Assert.Equal("0", Eval("toString(0)").Text);
        Assert.Equal("false", Eval("toString(false)").Text);
        Assert.Equal("1,2", Eval("toString([1, 2])").Text);
        Assert.Equal("[object Object]", Eval("toString({})").Text);
        Assert.Equal("/a+/g", Eval("toString(/a+/g)").Text);
        Assert.Equal("abc", Eval("toString('abc')").Text);
    }

    /// <summary>
    ///     Validates regular expression construction, testing and the supported flags.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("test(/^\\d+$/, '123')", true)]
    [InlineData("test(/^\\d+$/, '12a')", false)]
    [InlineData("test(/^\\d+$/, '\\u0663')", false)]
    [InlineData("test(/^\\w+$/, 'abc_9')", true)]
    [InlineData("test(/^\\w+$/, '\\u00e9')", false)]
    [InlineData("test(/^\\D\\W$/, 'a-')", true)]
    [InlineData("test(/^[\\d]+$/, '12')", true)]
    [InlineData("test(/^a$/, 'a\\n')", false)]
    [InlineData("test(/^b$/m, 'a\\nb\\nc')", true)]
    [InlineData("test(/^b$/, 'a\\nb\\nc')", false)]
    [InlineData("test(/a.c/, 'a\\nc')", false)]
    [InlineData("test(/a.c/s, 'a\\nc')", true)]
    [InlineData("test(/ABC/i, 'abc')", true)]
    [InlineData("test(/ABC/, 'abc')", false)]
    [InlineData("test(/^[^]$/, '\\n')", true)]
    [InlineData("test(/^[]$/, 'a')", false)]
    [InlineData("test('a.c', 'abc')", true)]
    [InlineData("test(regexp('^a+$', 'i'), 'AaA')", true)]
    [InlineData("test(regexp('a'), 'b')", false)]
    [InlineData("test(regexp(/a/i), 'A')", true)]
    [InlineData("test(regexp(/a/i, 'g'), 'A')", false)]
    [InlineData("test(/a/s, 'a')", true)]
    [InlineData("test(/^(?=a)a+$/, 'aaa')", true)]
    [InlineData("test(/(a)\\1/, 'aa')", true)]
    [InlineData("test(/(a)\\1/, 'ab')", false)]
    [InlineData("test(/^[\\w.]+@[a-z]+\\.com$/, 'x.y@site.com')", true)]
    [InlineData("test(/^[\\W]$/, '\\u00e9')", true)]
    [InlineData("test(/^[\\W]$/, 'a')", false)]
    [InlineData("test(/^[^\\W]$/, '\\u00e9')", false)]
    [InlineData("test(/^[^\\W]$/, 'a')", true)]
    [InlineData("test(/^[a\\W]$/, '\\u00e9')", true)]
    [InlineData("test(/^[a\\W]$/, 'a')", true)]
    [InlineData("test(/^[a\\W]$/, '1')", false)]
    [InlineData("test(/^[\\D]$/, '\\u0663')", true)]
    [InlineData("test(/^[\\D]$/, '5')", false)]
    [InlineData("test(/^[^\\D]$/, '5')", true)]
    [InlineData("test(/^[^\\D]$/, '\\u0663')", false)]
    [InlineData("test(/^[^\\D\\W]$/, '5')", true)]
    [InlineData("test(/^[^\\D\\W]$/, 'a')", false)]
    [InlineData("test(/^\\s$/, '\\u0085')", false)]
    [InlineData("test(/^\\s$/, '\\ufeff')", true)]
    [InlineData("test(/^\\S$/, ' ')", false)]
    [InlineData("test(/^\\S$/, '\\u0085')", true)]
    [InlineData("test(/^[\\S]$/, '\\u00e9')", true)]
    [InlineData("test(/^[^\\S]$/, '\\t')", true)]
    [InlineData("test(/^[\\s\\d]+$/, ' 1')", true)]
    public void ExpressionFunctions_Regex_TestAndFlags(string text, bool expected)
    {
        // Arrange / Act: the test and its negation
        var value = Eval(text);
        var negated = Eval("!" + text);

        // Assert
        Assert.True(value.IsBoolean);
        Assert.Equal(expected, value.Boolean);
        Assert.Equal(!expected, negated.Boolean);
    }

    /// <summary>
    ///     Validates <c>replace</c> with text and regular expression patterns and the JavaScript replacement patterns.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected string.</param>
    [Theory]
    [InlineData("replace('aXbX', 'X', '-')", "a-bX")]
    [InlineData("replace('abc', 'z', '-')", "abc")]
    [InlineData("replace('a.b', '.', '$&$&')", "a..b")]
    [InlineData("replace('abc', 'b', '$`')", "aac")]
    [InlineData("replace('abc', 'b', \"$'\")", "acc")]
    [InlineData("replace('abc', 'b', '$$')", "a$c")]
    [InlineData("replace('abc', 'b', '$1')", "a$1c")]
    [InlineData("replace('abc', /b/, '[$&]')", "a[b]c")]
    [InlineData("replace('a-b-c', /-/, '+')", "a+b-c")]
    [InlineData("replace('a-b-c', /-/g, '+')", "a+b+c")]
    [InlineData("replace('abc', /x/, 'y')", "abc")]
    [InlineData("replace('john smith', /(\\w+) (\\w+)/, '$2, $1')", "smith, john")]
    [InlineData("replace('abab', /(a)(b)/g, '$2$1')", "baba")]
    [InlineData("replace('abc', /(a)(b)(c)/, '$3$2$1$0')", "cba$0")]
    [InlineData("replace('abc', /(a)/, '$2')", "$2bc")]
    [InlineData("replace('abc', /(b)/, '$$1')", "a$1c")]
    [InlineData("replace('abc', regexp('B', 'i'), 'x')", "axc")]
    [InlineData("replace('2024-05', regexp('(?<y>\\\\d+)-(?<m>\\\\d+)'), '$<m>/$<y>')", "05/2024")]
    [InlineData("replace('abc', /x*/g, '-')", "-a-b-c-")]
    [InlineData("replace('aaa', /a/g, '$$')", "$$$")]
    [InlineData("replace('', /^/, 'x')", "x")]
    [InlineData("replace(null, 'l', 'L')", "nuLl")]
    public void ExpressionFunctions_Replace_FollowsJavaScript(string text, string expected)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert: a string result with the expected text
        Assert.True(value.IsString);
        Assert.Equal(expected, value.Text);
    }

    /// <summary>
    ///     Validates <c>split</c> with text and regular expression separators and limits.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected parts joined with a vertical bar.</param>
    [Theory]
    [InlineData("split('a,b,c', ',')", "a|b|c")]
    [InlineData("split('abc', '')", "a|b|c")]
    [InlineData("split('a,b,c', ',', 2)", "a|b")]
    [InlineData("split('a,b', ',', 0)", "")]
    [InlineData("split('a,b,c', ',', 10)", "a|b|c")]
    [InlineData("split('abc', ',')", "abc")]
    [InlineData("split('abc')", "abc")]
    [InlineData("split('abc', null)", "abc")]
    [InlineData("split(',a,', ',')", "|a|")]
    [InlineData("split('a--b', '--')", "a|b")]
    [InlineData("split('a1b22c', /\\d+/)", "a|b|c")]
    [InlineData("split('a1b', /(\\d)/)", "a|1|b")]
    [InlineData("split('a1b2c', /\\d/, 2)", "a|b")]
    [InlineData("split('abc', regexp(''))", "a|b|c")]
    [InlineData("split('abc', /x/)", "abc")]
    [InlineData("split('1a', /\\d/)", "|a")]
    [InlineData("split('abc', /(?:)/)", "a|b|c")]
    [InlineData("split('abc', /x*/)", "a|b|c")]
    [InlineData("split('abc', /()/)", "a||b||c")]
    [InlineData("split('abc', /b*/)", "a|c")]
    [InlineData("split('abc', /(?:)/, 2)", "a|b")]
    [InlineData("split('ab', /(?=b)/)", "a|b")]
    [InlineData("split('a,b', /,*/)", "a|b")]
    [InlineData("split('a  b', / */)", "a|b")]
    [InlineData("split('aXbXc', /(x)|(b)/i)", "a|X|null||null|b||X|null|c")]
    [InlineData("split('abc', /(?:)/, 0)", "")]
    public void ExpressionFunctions_Split_FollowsJavaScript(string text, string expected)
    {
        // Arrange / Act
        var value = Eval(text);

        // Assert: the result is an array whose parts are joined for comparison
        Assert.True(value.IsArray);
        Assert.Equal(expected, string.Join('|', value.Elements.ToArray().Select(e => e.ToText())));
    }

    /// <summary>
    ///     Validates the lengths of the degenerate splits that have no printable form.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Split_EmptyInputs()
    {
        // Arrange / Act / Assert: JavaScript splits "" by "," into one empty part and by "" into none
        Assert.Equal(1, Eval("split('', ',')").Elements.Length);
        Assert.Equal(0, Eval("split('', '')").Elements.Length);
        Assert.Equal(0, Eval("split('a,b', ',', 0)").Elements.Length);
        Assert.Equal(1, Eval("split('', /x/)").Elements.Length);
        Assert.Equal(0, Eval("split('', /x*/)").Elements.Length);
    }

    /// <summary>
    ///     Validates that an empty regular expression splits between every two UTF-16 code units, as JavaScript does, and that
    ///     the limit of the parts and a text separator behave the same way.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Split_EmptyRegexAndTextSplitBetweenCodeUnits()
    {
        // Arrange: a surrogate pair is two code units to split, regular expression or text
        var parameters = new Dictionary<string, DataValue> { ["s"] = DataValue.FromString("a\uD83D\uDE00") };

        // Act
        var byRegex = Eval("split(s, regexp(''))", parameters: parameters).Elements.ToArray().Select(e => e.Text).ToArray();
        var byText = Eval("split(s, '')", parameters: parameters).Elements.ToArray().Select(e => e.Text).ToArray();

        // Assert
        Assert.Equal(["a", "\uD83D", "\uDE00"], byRegex);
        Assert.Equal(byText, byRegex);
        Assert.Equal(3, Eval("split('abc', regexp(''))").Elements.Length);
        Assert.Equal("a,b,c", Eval("join(split('abc', regexp('')), ',')").Text);
    }

    /// <summary>
    ///     Validates the full case mappings that JavaScript applies and the invariant culture of .NET does not: the sharp s,
    ///     the ligatures, the letters with a combining mark and the Greek letters with an iota subscript make longer text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The upper case text.</param>
    [Theory]
    [InlineData("\u00DF", "SS")]
    [InlineData("gro\u00DF", "GROSS")]
    [InlineData("\uFB00", "FF")]
    [InlineData("\uFB01", "FI")]
    [InlineData("\uFB02", "FL")]
    [InlineData("\uFB03", "FFI")]
    [InlineData("\uFB04", "FFL")]
    [InlineData("\uFB05", "ST")]
    [InlineData("\uFB06", "ST")]
    [InlineData("\u0149", "\u02BCN")]
    [InlineData("\u01F0", "J\u030C")]
    [InlineData("\u0587", "\u0535\u0552")]
    [InlineData("\u1E96\u1E97\u1E98\u1E99\u1E9A", "H\u0331T\u0308W\u030AY\u030AA\u02BE")]
    [InlineData("\u0390", "\u0399\u0308\u0301")]
    [InlineData("\u03B0", "\u03A5\u0308\u0301")]
    [InlineData("\u1F80", "\u1F08\u0399")]
    [InlineData("\u1F88", "\u1F08\u0399")]
    [InlineData("\u1F97", "\u1F2F\u0399")]
    [InlineData("\u1FA7", "\u1F6F\u0399")]
    [InlineData("\u1FB3", "\u0391\u0399")]
    [InlineData("\u1FB7", "\u0391\u0342\u0399")]
    [InlineData("\u1FF4", "\u038F\u0399")]
    [InlineData("\uFB13", "\u0544\u0546")]
    [InlineData("a\u00DF b\u00E9\u0131", "ASS B\u00C9I")]
    [InlineData("abc \u00E9\u00FC", "ABC \u00C9\u00DC")]
    [InlineData("", "")]
    public void ExpressionFunctions_Upper_AppliesFullCaseMappings(string text, string expected)
    {
        // Arrange / Act
        var value = Eval("upper(s)", parameters: new Dictionary<string, DataValue> { ["s"] = DataValue.FromString(text) });

        // Assert
        Assert.True(value.IsString);
        Assert.Equal(expected, value.Text);
    }

    /// <summary>
    ///     Validates that the capital I with a dot above lowers to two code points and that the capital sigma lowers to the
    ///     final form at the end of a word and to the ordinary form elsewhere, as JavaScript does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The lower case text.</param>
    [Theory]
    [InlineData("\u0130", "i\u0307")]
    [InlineData("A\u0130B", "ai\u0307b")]
    [InlineData("\u0391\u03A3", "\u03B1\u03C2")]
    [InlineData("\u0391\u03A3\u0391", "\u03B1\u03C3\u03B1")]
    [InlineData("\u03A3", "\u03C3")]
    [InlineData("\u0391\u03A3 \u03A3", "\u03B1\u03C2 \u03C3")]
    [InlineData("\u0391\u03A3.", "\u03B1\u03C2.")]
    [InlineData("\u0391\u03A3.\u0391", "\u03B1\u03C3.\u03B1")]
    [InlineData("a\u00AD\u03A3", "a\u00AD\u03C2")]
    [InlineData("\u0391\u03A3\u03A3", "\u03B1\u03C3\u03C2")]
    [InlineData("\u03A3\u0391", "\u03C3\u03B1")]
    [InlineData("1\u03A3", "1\u03C3")]
    [InlineData("ABC \u00C9\u00DC", "abc \u00E9\u00FC")]
    [InlineData("", "")]
    public void ExpressionFunctions_Lower_AppliesFullCaseMappingsAndFinalSigma(string text, string expected)
    {
        // Arrange / Act
        var value = Eval("lower(s)", parameters: new Dictionary<string, DataValue> { ["s"] = DataValue.FromString(text) });

        // Assert
        Assert.True(value.IsString);
        Assert.Equal(expected, value.Text);
    }

    /// <summary>
    ///     Validates that case conversion does not depend on the current culture (the Turkish dotted and dotless i do not
    ///     change the result) and that a result that grows is bounded by the string limit.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_CaseConversion_IsCultureInvariantAndBounded()
    {
        // Arrange
        var parameters = new Dictionary<string, DataValue> { ["s"] = DataValue.FromString("i I \u00DF") };
        var limits = new VegaLiteLimits { MaxStringLength = 4 };
        var grows = new Dictionary<string, DataValue> { ["s"] = DataValue.FromString("\u00DF\u00DF\u00DF") };

        // Act
        var turkish = UnderCulture("tr-TR", () => Eval("upper(s)", parameters: parameters).Text + "|" + Eval("lower(s)", parameters: parameters).Text);
        var invariant = UnderCulture(string.Empty, () => Eval("upper(s)", parameters: parameters).Text + "|" + Eval("lower(s)", parameters: parameters).Text);
        var atLimit = Eval("upper(s)", limits, new Dictionary<string, DataValue> { ["s"] = DataValue.FromString("\u00DF\u00DF") });
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval("upper(s)", limits, grows));

        // Assert: the results are the same in both cultures; three sharp s (one more character each) pass the input but not the result
        Assert.Equal("I I SS|i i \u00DF", turkish);
        Assert.Equal(turkish, invariant);
        Assert.Equal("SSSS", atLimit.Text);
        Assert.Contains("the limit of 4 characters", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Location, exception.Location);
    }

    /// <summary>
    ///     Validates that a catastrophic pattern is harmless: the linear-time engine finishes quickly with the correct
    ///     answer on input that would take exponential time with backtracking.
    /// </summary>
    /// <param name="pattern">The regular expression literal.</param>
    [Theory]
    [InlineData("/(a+)+$/")]
    [InlineData("/^(a+)+$/")]
    [InlineData("/^(a|aa)+$/")]
    [InlineData("/^(a|a)*$/")]
    [InlineData("/^(.*a){20}$/")]
    public void ExpressionFunctions_Regex_CatastrophicPatternsAreSafe(string pattern)
    {
        // Arrange: 50,000 letters followed by a character that makes every pattern fail
        var parameters = new Dictionary<string, DataValue> { ["s"] = DataValue.FromString(new string('a', 50_000) + "!") };

        // Act
        var value = Eval($"test({pattern}, s)", parameters: parameters);

        // Assert: the match completes and fails
        Assert.True(value.IsBoolean);
        Assert.False(value.Boolean);
    }

    /// <summary>
    ///     Validates that a pattern that the linear-time engine cannot run falls back to a bounded backtracking engine and
    ///     fails closed with a located error when it exceeds the time limit.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Regex_BacktrackingFallbackFailsClosed()
    {
        // Arrange: a look-ahead forces the backtracking engine and the nested quantifier is exponential on 40 letters and a bang
        var parameters = new Dictionary<string, DataValue> { ["s"] = DataValue.FromString(new string('a', 40) + "!") };

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval("test(/^(?=a)(a+)+$/, s)", parameters: parameters));

        // Assert
        Assert.Equal(Location, exception.Location);
        Assert.Contains("exceeded its match time limit", exception.Message, StringComparison.Ordinal);
        Assert.Contains("at expression offset", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that invalid patterns, unknown or repeated flags and over-long patterns are located render errors.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="fragment">A fragment of the expected message.</param>
    /// <param name="offset">The offset of the failing call.</param>
    [Theory]
    [InlineData("regexp('(')", "is not valid", 0)]
    [InlineData("regexp('[')", "is not valid", 0)]
    [InlineData("regexp('a', 'x')", "not supported or repeat", 0)]
    [InlineData("regexp('a', 'gg')", "not supported or repeat", 0)]
    [InlineData("regexp('a', 'u')", "the flags u, v, y and d are not", 0)]
    [InlineData("regexp('a', 'gu')", "not supported or repeat", 0)]
    [InlineData("regexp('a', 'v')", "not supported or repeat", 0)]
    [InlineData("regexp('a', 'y')", "not supported or repeat", 0)]
    [InlineData("regexp('a', 'd')", "not supported or repeat", 0)]
    [InlineData("replace('x', regexp('.', 'iu'), 'x')", "not supported or repeat", 13)]
    [InlineData("test('(', 'a')", "is not valid", 0)]
    [InlineData("replace('a', regexp('*'), 'b')", "is not valid", 13)]
    public void ExpressionFunctions_Regex_InvalidPatternIsLocatedRenderError(string text, string fragment, int offset)
    {
        // Arrange / Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval(text));

        // Assert
        Assert.Equal(Location, exception.Location);
        Assert.Contains(fragment, exception.Message, StringComparison.Ordinal);
        Assert.EndsWith($"at expression offset {offset}.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the pattern length limit of 1,024 characters.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Regex_PatternLengthIsBounded()
    {
        // Arrange
        var atLimit = new Dictionary<string, DataValue> { ["p"] = DataValue.FromString(new string('a', 1_024)) };
        var overLimit = new Dictionary<string, DataValue> { ["p"] = DataValue.FromString(new string('a', 1_025)) };

        // Act / Assert
        Assert.True(Eval("isRegExp(regexp(p))", parameters: atLimit).Boolean);
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval("regexp(p)", parameters: overLimit));
        Assert.Contains("1025 characters, more than the limit of 1024", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the string limit on the results of functions that build strings.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_StringResults_AreBoundedByMaxStringLength()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 20 };

        // Act / Assert: results at the limit are accepted, longer ones are located render errors
        Assert.Equal(20, Eval("pad('', 20, 'x')", limits).Text!.Length);
        Assert.Throws<VegaLiteRenderException>(() => Eval("pad('', 21, 'x')", limits));
        Assert.Throws<VegaLiteRenderException>(() => Eval("pad('', 1e9, 'x')", limits));
        Assert.Throws<VegaLiteRenderException>(() => Eval("pad('', Infinity, 'x')", limits));
        Assert.Equal(20, Eval("replace('aaaaaaaaaa', /a/g, 'bb')", limits).Text!.Length);
        Assert.Throws<VegaLiteRenderException>(() => Eval("replace('aaaaaaaaaaa', /a/g, 'bb')", limits));
        Assert.Throws<VegaLiteRenderException>(() => Eval("join([toString('0123456789'), '0123456789a'], '')", limits));
        Assert.Throws<VegaLiteRenderException>(() => Eval("toString(['0123456789', '0123456789'])", limits));
        Assert.Equal(20, Eval("join(['0123456789', '0123456789'], '')", limits).Text!.Length);
    }

    /// <summary>
    ///     Validates that results of the same call are independent of the culture and identical when repeated.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Repeated_ResultsAreIdentical()
    {
        // Arrange
        const string Text = "join(split(replace(upper('a-b-c'), /-/g, ','), ','), '+') + pad(toString(round(2.5)), 3, '0', 'left')";

        // Act
        var results = Enumerable.Range(0, 50).Select(_ => EvalText(Text)).Distinct().ToArray();

        // Assert
        Assert.Equal(["A+B+C003"], results);
        Assert.Equal(EvalText("random()"), EvalText("random()"));
        Assert.Equal("null", EvalText("random()"));
    }

    /// <summary>
    ///     Validates the step accounting of the per-evaluation context: exact budget, size-proportional charges and
    ///     cancellation at 1,024-step boundaries.
    /// </summary>
    [Fact]
    public void ExpressionFunctionContext_StepsAndCharges_AreExactAndCancelable()
    {
        // Arrange
        var context = new ExpressionFunctionContext(Steps(10), CancellationToken.None);

        // Act: nine single steps and a charge that is below one unit are free of extra cost
        for (var i = 0; i < 9; i++)
        {
            context.Step();
        }

        context.ChargeSize(63);

        // Assert: one step is left and the next charge of a whole unit is the last one the budget allows
        Assert.Equal(9, context.Steps);
        context.ChargeSize(64);
        Assert.Equal(10, context.Steps);
        Assert.Throws<ExpressionRuntimeException>(context.Step);
        Assert.Throws<ExpressionRuntimeException>(() => context.ChargeSize(64));

        // Assert: a huge charge exhausts the budget without overflow
        var huge = new ExpressionFunctionContext(null, CancellationToken.None);
        Assert.Throws<ExpressionRuntimeException>(() => huge.ChargeSize(long.MaxValue));

        // Assert: a charge that crosses a multiple of 1,024 observes cancellation
        using var cancellation = new CancellationTokenSource();
        var cancelable = new ExpressionFunctionContext(null, cancellation.Token);
        cancellation.Cancel();
        cancelable.ChargeSize(64L * 1_000);
        Assert.Throws<OperationCanceledException>(() => cancelable.ChargeSize(64L * 100));
        Assert.Throws<OperationCanceledException>(cancelable.ThrowIfCanceled);
    }

    /// <summary>
    ///     Validates the string limit check and the regular expression cache of the context.
    /// </summary>
    [Fact]
    public void ExpressionFunctionContext_StringLimitAndRegexCache_Work()
    {
        // Arrange
        var context = new ExpressionFunctionContext(new VegaLiteLimits { MaxStringLength = 5 }, CancellationToken.None);

        // Act
        context.CheckStringLength(5);
        var first = context.GetRegex("a+", "i");
        var second = context.GetRegex("a+", "i");
        var other = context.GetRegex("a+", string.Empty);

        // Assert
        Assert.Throws<ExpressionRuntimeException>(() => context.CheckStringLength(6));
        Assert.Same(first, second);
        Assert.NotSame(first, other);
        Assert.Equal("/a+/i", first.ToString());
        Assert.True(context.GetRegex("a", "gi").IsGlobal);
        Assert.Equal("gi", context.GetRegex("a", "ig").Flags);
        for (var i = 0; i < 100; i++)
        {
            Assert.NotNull(context.GetRegex("p" + i, string.Empty));
        }
    }

    /// <summary>
    ///     Validates that a replacement text is charged for every match of a regular expression, even when its patterns
    ///     expand to nothing, so a short input with many (empty) matches cannot read a long replacement unbounded.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Replace_ChargesTheReplacementForEveryMatch()
    {
        // Arrange: '(x)?' matches empty at all 2,001 positions of the input and '$1' (an unmatched group) expands to nothing,
        // so the output stays the input while every match reads the 50,000 characters of the replacement
        const string Text = "replace(pad('', 2000, 'a'), regexp('(x)?', 'g'), pad('', 25000, '$1'))";

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval(Text, Steps(5_000)));
        var evaluator = new ExpressionEvaluator(Steps(10_000_000), CancellationToken.None);
        var value = evaluator.Evaluate(Compile(Text, Steps(10_000_000)));

        // Assert: the small budget fails with the step error; the large one succeeds and was charged for every match
        Assert.Contains("MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new string('a', 2_000), value.Text);
        Assert.True(evaluator.StepsUsed > 2_000 * 390L);
    }

    /// <summary>
    ///     Validates that a replacement pattern such as <c>$`</c> fails at the string limit as it grows instead of
    ///     appending the whole text for every pattern.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Replace_FailsEarlyWhenAReplacementPatternGrowsTheTextPastTheLimit()
    {
        // Arrange: every "$`" appends the 400 characters before the match, so the third one passes the limit of 1,000 characters
        var text = "replace(pad('', 400, 'a') + 'b', 'b', pad('', 10, '$`'))";

        // Act / Assert
        var exception = Assert.Throws<VegaLiteRenderException>(() => Eval(text, new VegaLiteLimits { MaxStringLength = 1_000 }));
        Assert.Contains("MaxStringLength", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the functions whose work is proportional to the length of their input charge it to the step
    ///     budget before converting: one step for every 64 characters, nothing for a short text.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="text">The argument text (digits, so that <c>toNumber</c> is valid too).</param>
    [Theory]
    [InlineData("lower", "AbCdEfGh")]
    [InlineData("upper", "aBcDeFgH")]
    [InlineData("trim", "  12345678  ")]
    [InlineData("toString", "12345678")]
    [InlineData("toNumber", "12345678")]
    public void ExpressionFunctions_SizeProportionalFunctions_ChargeTheLengthOfTheInput(string name, string text)
    {
        // Arrange: a short text and a text of 6,400 characters (100 steps) under a budget of 10 steps
        var shortContext = new ExpressionFunctionContext(Steps(10), CancellationToken.None);
        var longText = string.Concat(Enumerable.Repeat(text, 800));
        var longContext = new ExpressionFunctionContext(Steps(10), CancellationToken.None);

        // Act
        ExpressionFunctions.Invoke(name, [DataValue.FromString(text)], shortContext);

        // Assert: the short text is free, the long one exhausts the budget before the conversion is made
        Assert.Equal(0, shortContext.Steps);
        Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke(name, [DataValue.FromString(longText)], longContext));
        Assert.True(longContext.Steps > 10);
    }

    /// <summary>
    ///     Validates that a case conversion observes cancellation through the charge of its input.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_CaseConversion_ObservesCancellationThroughTheCharge()
    {
        // Arrange: a canceled token is seen when the charge crosses a multiple of 1,024 steps
        using var cancellation = new CancellationTokenSource();
        var context = new ExpressionFunctionContext(null, cancellation.Token);
        cancellation.Cancel();
        var text = DataValue.FromString(new string('a', 64 * 1_100));

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => ExpressionFunctions.Invoke("upper", [text], context));
        Assert.Throws<OperationCanceledException>(() => ExpressionFunctions.Invoke("lower", [text], context));
    }
}
