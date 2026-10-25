using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the <c>str()</c> and echo text forms of <see cref="ScadValue"/>.
/// </summary>
public class ScadValueFormatTests
{
    /// <summary>
    ///     Validates the str form of scalars: strings raw, numbers via the formatter.
    /// </summary>
    [Fact]
    public void ScadValue_ToStr_Scalars_AreRawAndFormatted()
    {
        // Arrange / Act / Assert
        Assert.Equal("abc", S("abc").ToStr());
        Assert.Equal("a\"b\\c", S("a\"b\\c").ToStr());
        Assert.Equal("1.5", N(1.5).ToStr());
        Assert.Equal("1e+06", N(1_000_000).ToStr());
        Assert.Equal("true", ScadValue.True.ToStr());
        Assert.Equal("false", ScadValue.False.ToStr());
        Assert.Equal("undef", ScadValue.Undef.ToStr());
        Assert.Equal("nan", N(double.NaN).ToStr());
    }

    /// <summary>
    ///     Validates the echo form of scalars: strings quoted and escaped.
    /// </summary>
    [Fact]
    public void ScadValue_ToEchoText_Strings_AreQuotedAndEscaped()
    {
        // Arrange / Act / Assert
        Assert.Equal("\"abc\"", S("abc").ToEchoText());
        Assert.Equal("\"a\\\"b\\\\c\"", S("a\"b\\c").ToEchoText());
        Assert.Equal("\"l1\\nl2\\r\\tend\"", S("l1\nl2\r\tend").ToEchoText());
        Assert.Equal("\"\"", S(string.Empty).ToEchoText());
        Assert.Equal("2.5", N(2.5).ToEchoText());
    }

    /// <summary>
    ///     Validates that an echo line never contains a raw line break even for control characters.
    /// </summary>
    [Fact]
    public void ScadValue_ToEchoText_StringWithBreaks_HasNoRawLineBreak()
    {
        // Arrange
        var value = Vec(S("a\r\nb"), S("c\nd"));

        // Act
        var text = value.ToEchoText();

        // Assert
        Assert.DoesNotContain('\n', text);
        Assert.DoesNotContain('\r', text);
        Assert.Equal("[\"a\\r\\nb\", \"c\\nd\"]", text);
    }

    /// <summary>
    ///     Validates vector text: separators, nesting, and quoting of nested strings in the str form.
    /// </summary>
    [Fact]
    public void ScadValue_ToStr_Vectors_UseCommaSpaceAndQuoteNestedStrings()
    {
        // Arrange
        var value = Vec(N(1), S("a"), Nums(2, 3), Vec(), ScadValue.Undef, ScadValue.True);

        // Act
        var str = value.ToStr();
        var echo = value.ToEchoText();

        // Assert
        Assert.Equal("[1, \"a\", [2, 3], [], undef, true]", str);
        Assert.Equal(str, echo);
        Assert.Equal("[]", Vec().ToStr());
    }

    /// <summary>
    ///     Validates the range text in start, step, end order.
    /// </summary>
    [Fact]
    public void ScadValue_ToStr_Range_ShowsStartStepEnd()
    {
        // Arrange / Act / Assert
        Assert.Equal("[0 : 1 : 5]", ScadValue.FromRange(0, 1, 5).ToStr());
        Assert.Equal("[10 : -2.5 : 0]", ScadValue.FromRange(10, -2.5, 0).ToEchoText());
        Assert.Equal("[[0 : 1 : 2]]", Vec(ScadValue.FromRange(0, 1, 2)).ToStr());
    }

    /// <summary>
    ///     Validates the function text with its parameter names.
    /// </summary>
    [Fact]
    public void ScadValue_ToStr_Function_ShowsParameterNames()
    {
        // Arrange
        var two = ScadValue.FromFunction(new TestFunctionValue("a", "b"));
        var none = ScadValue.FromFunction(new TestFunctionValue());

        // Act / Assert
        Assert.Equal("function(a, b)", two.ToStr());
        Assert.Equal("function()", none.ToEchoText());
    }

    /// <summary>
    ///     Validates that formatting under other cultures gives the same text.
    /// </summary>
    /// <param name="culture">The culture name.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void ScadValue_ToStr_OtherCultures_IsIdentical(string culture)
    {
        // Arrange
        var value = Vec(N(1234.5), N(-0.00001), S("I"), ScadValue.FromRange(0.5, 0.25, 1));

        // Act
        var invariant = value.ToEchoText();
        var localized = WithCulture(culture, () => value.ToEchoText());

        // Assert
        Assert.Equal("[1234.5, -1e-05, \"I\", [0.5 : 0.25 : 1]]", invariant);
        Assert.Equal(invariant, localized);
    }

    /// <summary>
    ///     Validates the length-bounded forms: fits, exactly fits, and exceeds.
    /// </summary>
    [Fact]
    public void ScadValue_TryToStr_Limit_FitsOrFails()
    {
        // Arrange
        var value = Nums(1, 2, 3);

        // Act
        var fits = value.TryToStr(9, out var text);
        var tooLong = value.TryToStr(8, out var empty);
        var echoFits = S("ab").TryToEchoText(4, out var echo);
        var echoTooLong = S("ab").TryToEchoText(3, out var echoEmpty);
        var stringTooLong = S("abcdef").TryToStr(5, out var stringEmpty);

        // Assert
        Assert.True(fits);
        Assert.Equal("[1, 2, 3]", text);
        Assert.False(tooLong);
        Assert.Empty(empty);
        Assert.True(echoFits);
        Assert.Equal("\"ab\"", echo);
        Assert.False(echoTooLong);
        Assert.Empty(echoEmpty);
        Assert.False(stringTooLong);
        Assert.Empty(stringEmpty);
    }

    /// <summary>
    ///     Validates that the bounded form stops early instead of building a huge string.
    /// </summary>
    [Fact]
    public void ScadValue_TryToStr_HugeVector_StopsEarly()
    {
        // Arrange: a vector whose full text would be tens of megabytes
        var element = S(new string('x', 1000));
        var value = ScadValue.FromVector(Enumerable.Repeat(element, 20_000).ToArray());

        // Act
        var fits = value.TryToStr(10_000, out var text);

        // Assert
        Assert.False(fits);
        Assert.Empty(text);
    }

    /// <summary>
    ///     Validates that, under a meter, every vector element visited is charged, nested elements included.
    /// </summary>
    [Fact]
    public void ScadValueFormat_TryToStr_WithMeter_ChargesPerVisitedElement()
    {
        // Arrange: ten numbers, then a vector holding a vector of two numbers and a vector of one (5 elements)
        var host = new ScadTestBuiltinHost();
        var flat = Nums(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        var nested = Vec(Nums(1, 2), Nums(3));

        // Act
        var flatFits = flat.TryToStr(1000, out _, host);
        var flatSteps = host.Steps;
        var nestedFits = nested.TryToStr(1000, out _, host);

        // Assert
        Assert.Equal(8, ScadValue.FormatStepsPerElement);
        Assert.True(flatFits);
        Assert.True(nestedFits);
        Assert.Equal(10 * ScadValue.FormatStepsPerElement, flatSteps);
        Assert.Equal(flatSteps + (5 * ScadValue.FormatStepsPerElement), host.Steps);
    }

    /// <summary>
    ///     Validates that a step limit stops formatting midway instead of after the whole value was formatted.
    /// </summary>
    [Fact]
    public void ScadValueFormat_TryToStr_StepLimitReached_StopsMidFormat()
    {
        // Arrange: a thousand elements would cost 8,000 steps; the budget is 100
        var host = new ScadTestBuiltinHost { MaxSteps = 100 };
        var value = ScadValue.FromVector(Enumerable.Range(0, 1000).Select(i => N(i)).ToArray());

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => value.TryToStr(1_000_000, out _, host));

        // Assert: the thirteenth element is the first to pass the budget
        Assert.Contains("Evaluation step limit exceeded (limit 100)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(104, host.Steps);
    }

    /// <summary>
    ///     Validates that a quoted string element is charged by its length on top of the element charge.
    /// </summary>
    [Fact]
    public void ScadValueFormat_TryToEchoText_WithMeter_QuotedStringsChargedByLength()
    {
        // Arrange: one 160-character string (ten extra steps) and one empty string (none)
        var host = new ScadTestBuiltinHost();
        var value = Vec(S(new string('x', 160)), S(string.Empty));

        // Act
        var fits = value.TryToEchoText(1000, out var text, host);

        // Assert
        Assert.True(fits);
        Assert.Equal(168, text.Length);
        Assert.Equal((2 * ScadValue.FormatStepsPerElement) + 10, host.Steps);
    }
}
