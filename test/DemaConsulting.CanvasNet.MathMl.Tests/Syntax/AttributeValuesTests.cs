using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="AttributeValues"/> and <see cref="StyleDeclarations"/>: lengths, named spaces,
///     booleans, script levels, colors, and the inline style subset.
/// </summary>
public class AttributeValuesTests
{
    /// <summary>
    ///     Validates every CSS unit, with its conversion to pixels or its relative unit.
    /// </summary>
    /// <param name="text">The value.</param>
    /// <param name="value">The expected number.</param>
    /// <param name="unit">The name of the expected unit.</param>
    [Theory]
    [InlineData("2em", 2, "Em")]
    [InlineData(" -0.5ex ", -0.5, "Ex")]
    [InlineData("12px", 12, "Px")]
    [InlineData("3pt", 4, "Px")]
    [InlineData("1pc", 16, "Px")]
    [InlineData("1in", 96, "Px")]
    [InlineData("2.54cm", 96, "Px")]
    [InlineData("25.4MM", 96, "Px")]
    [InlineData("50%", 50, "Percent")]
    [InlineData("0", 0, "Px")]
    [InlineData(".5em", 0.5, "Em")]
    public void AttributeValues_TryParseLength_EachUnit_IsParsed(string text, double value, string unit)
    {
        // Act
        var parsed = AttributeValues.TryParseLength(text, out var length);

        // Assert
        Assert.True(parsed);
        Assert.Equal(Enum.Parse<MathLengthUnit>(unit), length.Unit);
        Assert.Equal(value, length.Value, 9);
    }

    /// <summary>
    ///     Validates the named spaces of MathML 3 and their negative forms, in eighteenths of an em.
    /// </summary>
    [Fact]
    public void AttributeValues_TryParseLength_NamedSpaces_AreEighteenthsOfAnEm()
    {
        // Act / Assert
        Assert.True(AttributeValues.TryParseLength("thinmathspace", out var thin));
        Assert.True(AttributeValues.TryParseLength("negativeveryverythickmathspace", out var negative));
        Assert.Equal(new MathLength(3 / 18.0, MathLengthUnit.Em), thin);
        Assert.Equal(new MathLength(-7 / 18.0, MathLengthUnit.Em), negative);
    }

    /// <summary>
    ///     Validates that invalid lengths are refused: a unitless number, an unknown unit, the legacy pseudo-units, text.
    /// </summary>
    /// <param name="text">The value.</param>
    [Theory]
    [InlineData("2")]
    [InlineData("2 em")]
    [InlineData("2furlongs")]
    [InlineData("+1height")]
    [InlineData("big")]
    [InlineData("")]
    [InlineData(null)]
    public void AttributeValues_TryParseLength_InvalidInput_IsRefused(string? text)
    {
        // Act / Assert
        Assert.False(AttributeValues.TryParseLength(text, out _));
    }

    /// <summary>
    ///     Validates the resolution of lengths against the font size, the x-height and a percentage base, clamped.
    /// </summary>
    [Fact]
    public void AttributeValues_MathLength_Resolve_UsesBasesAndClamps()
    {
        // Act / Assert
        Assert.Equal(32, new MathLength(2, MathLengthUnit.Em).Resolve(16, 8, 100));
        Assert.Equal(16, new MathLength(2, MathLengthUnit.Ex).Resolve(16, 8, 100));
        Assert.Equal(50, new MathLength(50, MathLengthUnit.Percent).Resolve(16, 8, 100));
        Assert.Equal(MathLength.MaxMagnitude, new MathLength(1e300, MathLengthUnit.Px).Resolve(16, 8, 100));
    }

    /// <summary>
    ///     Validates booleans and unsigned integers.
    /// </summary>
    [Fact]
    public void AttributeValues_TryParseBooleanAndUnsignedInteger_AcceptOnlyValidValues()
    {
        // Act / Assert
        Assert.True(AttributeValues.TryParseBoolean(" TRUE ", out var yes) && yes);
        Assert.True(AttributeValues.TryParseBoolean("false", out var no) && !no);
        Assert.False(AttributeValues.TryParseBoolean("yes", out _));
        Assert.True(AttributeValues.TryParseUnsignedInteger("42", out var number) && number == 42);
        Assert.False(AttributeValues.TryParseUnsignedInteger("-1", out _));
        Assert.False(AttributeValues.TryParseUnsignedInteger("99999999999", out _));
    }

    /// <summary>
    ///     Validates the three forms of <c>scriptlevel</c>.
    /// </summary>
    [Fact]
    public void AttributeValues_TryParseScriptLevel_ReadsChangesAndLevels()
    {
        // Act / Assert
        Assert.True(AttributeValues.TryParseScriptLevel("+2", out var plusRelative, out var plus));
        Assert.True(AttributeValues.TryParseScriptLevel("-1", out var minusRelative, out var minus));
        Assert.True(AttributeValues.TryParseScriptLevel("3", out var setRelative, out var set));
        Assert.Equal((true, 2, true, -1, false, 3), (plusRelative, plus, minusRelative, minus, setRelative, set));
        Assert.False(AttributeValues.TryParseScriptLevel("+", out _, out _));
        Assert.False(AttributeValues.TryParseScriptLevel("1.5", out _, out _));
    }

    /// <summary>
    ///     Validates colors: names, <c>transparent</c>, the hexadecimal forms with and without alpha, the
    ///     <c>rgb()</c> and <c>hsl()</c> functions with and without alpha, and invalid text.
    /// </summary>
    [Fact]
    public void AttributeValues_TryParseColor_ReadsCssColors()
    {
        // Act / Assert
        Assert.True(AttributeValues.TryParseColor("red", out var red));
        Assert.True(AttributeValues.TryParseColor("#00f", out var blue));
        Assert.True(AttributeValues.TryParseColor("rgb(0, 128, 0)", out var green));
        Assert.Equal((new Rgba32(255, 0, 0, 255), new Rgba32(0, 0, 255, 255), new Rgba32(0, 128, 0, 255)), (red, blue, green));
        Assert.True(AttributeValues.TryParseColor("transparent", out var transparent));
        Assert.True(AttributeValues.TryParseColor("#0000ff80", out var hexAlpha));
        Assert.True(AttributeValues.TryParseColor("rgba(255, 0, 0, 0.5)", out var rgba));
        Assert.True(AttributeValues.TryParseColor("hsl(240, 100%, 50%)", out var hsl));
        Assert.True(AttributeValues.TryParseColor("hsla(0, 100%, 50%, 25%)", out var hsla));
        Assert.Equal(new Rgba32(0, 0, 0, 0), transparent);
        Assert.Equal((new Rgba32(0, 0, 255, 128), new Rgba32(255, 0, 0, 128)), (hexAlpha, rgba));
        Assert.Equal((new Rgba32(0, 0, 255, 255), new Rgba32(255, 0, 0, 64)), (hsl, hsla));
        Assert.False(AttributeValues.TryParseColor("reddish", out _));
        Assert.False(AttributeValues.TryParseColor("hsl(240, 100%)", out _));
    }

    /// <summary>
    ///     Validates the supported style subset, with the last declaration of a property winning and !important
    ///     ignored.
    /// </summary>
    [Fact]
    public void StyleDeclarations_Parse_SupportedSubset_IsRead()
    {
        // Act
        var style = StyleDeclarations.Parse("color: blue; color: red !important; background-color: #fff; font-size: 150%; math-style: compact; math-depth: add(2); font-style: normal; display: none; /* note */");

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), style.Color);
        Assert.Equal(new Rgba32(255, 255, 255, 255), style.BackgroundColor);
        Assert.Equal(new MathLength(150, MathLengthUnit.Percent), style.FontSize);
        Assert.Equal((false, new MathDepthValue(MathDepthKind.Add, 2), false, true), (style.DisplayStyle, style.MathDepth, style.Italic, style.DisplayNone));
        Assert.Empty(style.Issues);
    }

    /// <summary>
    ///     Validates the other forms of <c>math-depth</c> and <c>math-style</c>.
    /// </summary>
    [Fact]
    public void StyleDeclarations_Parse_MathDepthForms_AreRead()
    {
        // Act
        var autoAdd = StyleDeclarations.Parse("math-depth: auto-add; math-style: normal");
        var set = StyleDeclarations.Parse("math-depth:-3;font-style:italic");

        // Assert
        Assert.Equal((new MathDepthValue(MathDepthKind.AutoAdd, 0), true), (autoAdd.MathDepth, autoAdd.DisplayStyle));
        Assert.Equal((new MathDepthValue(MathDepthKind.Set, -3), true), (set.MathDepth, set.Italic));
    }

    /// <summary>
    ///     Validates that declarations outside the subset are unsupported issues and invalid values are invalid issues,
    ///     in order, and that semicolons inside parentheses do not split.
    /// </summary>
    [Fact]
    public void StyleDeclarations_Parse_UnsupportedDeclarations_AreIssues()
    {
        // Act
        var style = StyleDeclarations.Parse("border: 1px solid red; color: notacolor; background-color: rgb(1;2;3); display: block; font-style: oblique; nonsense");

        // Assert
        Assert.Equal([true, false, false, true, true, false], style.Issues.Select(issue => issue.IsUnsupported));
        Assert.Contains("border", style.Issues[0].Message, StringComparison.Ordinal);
        Assert.Null(style.Color);
    }
}
