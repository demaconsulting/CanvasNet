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
}
