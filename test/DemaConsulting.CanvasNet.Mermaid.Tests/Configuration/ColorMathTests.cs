using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Unit tests of <see cref="ColorMath"/>, the khroma port: every function the themes use, checked against the
///     results of khroma itself.
/// </summary>
public class ColorMathTests
{
    /// <summary>
    ///     Gets the indexes of the recorded khroma cases.
    /// </summary>
    public static TheoryData<int> ColorCases => UpstreamVectors.Indexes(UpstreamVectors.Colors);

    /// <summary>
    ///     Validates each recorded khroma call: the same text (hexadecimal, rgb, rgba, hsl, hsla, unchanged input,
    ///     NaN), the same number or the same error.
    /// </summary>
    /// <param name="index">The index of the case.</param>
    [Theory]
    [MemberData(nameof(ColorCases))]
    public void ColorMath_Function_MatchesKhroma(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Colors[index]!.AsObject();
        var function = (string)vector["fn"]!;
        var arguments = vector["args"]!.AsArray();

        // Act
        var (result, error) = Call(function, arguments);

        // Assert
        var description = $"{function}({arguments.ToJsonString()})";
        if (vector["error"] is { } expectedError)
        {
            Assert.True((string?)expectedError == error, $"{description}: expected error '{expectedError}', got '{error ?? result?.ToJsonString()}'");
        }
        else
        {
            Assert.True(error is null, $"{description}: unexpected error '{error}'");
            var difference = UpstreamVectors.Difference(vector["result"], result);
            Assert.True(difference is null, $"{description}: {difference}");
        }
    }

    /// <summary>
    ///     Validates that an unchanged color keeps its written form and a keyword is written as its hexadecimal form.
    /// </summary>
    [Fact]
    public void ColorMath_Adjust_NoChange_KeepsWrittenForm()
    {
        // Act
        var hex = ColorMath.Adjust("#ECECFF", h: 0);
        var keyword = ColorMath.Adjust("White", h: 0);

        // Assert
        Assert.Equal("#ECECFF", hex);
        Assert.Equal("#ffffff", keyword);
    }

    /// <summary>
    ///     Validates khroma's rounding to ten decimals and JavaScript's halves-up rounding.
    /// </summary>
    [Fact]
    public void ColorMath_Round_TenDecimalsHalfUp()
    {
        // Act
        var rounded = ColorMath.Round(86.27450980392157);
        var negativeHalf = ColorMath.JsRound(-2.5);
        var positiveHalf = ColorMath.JsRound(2.5);
        var belowHalf = ColorMath.JsRound(0.49999999999999994);

        // Assert
        Assert.Equal(86.2745098039, rounded);
        Assert.Equal(-2, negativeHalf);
        Assert.Equal(3, positiveHalf);
        Assert.Equal(0, belowHalf);
    }

    /// <summary>
    ///     Validates JavaScript's number formatting: shortest digits, exponent forms, NaN and negative zero.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="expected">The JavaScript text.</param>
    [Theory]
    [InlineData(96.2745098039, "96.2745098039")]
    [InlineData(0.49, "0.49")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(1e-6, "0.000001")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1.5e300, "1.5e+300")]
    [InlineData(123456789012345678901.0, "123456789012345680000")]
    [InlineData(double.NaN, "NaN")]
    [InlineData(-0.0, "0")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(250.0, "250")]
    public void ColorMath_FormatNumber_FollowsJavaScript(double value, string expected)
    {
        // Act
        var text = ColorMath.FormatNumber(value);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates that a value that is not a string cannot be read as a color, and that the exception carries it.
    /// </summary>
    [Fact]
    public void ColorMath_Invert_NotAString_ThrowsWithValue()
    {
        // Act
        var ex = Assert.Throws<ColorFormatException>(() => ColorMath.Invert(5.0));

        // Assert
        Assert.Equal(5.0, ex.Value);
    }

    /// <summary>
    ///     Validates the parsers on forms near their edges (white space inside functions, signs, exponents, units,
    ///     khroma's keyword table), with the answers khroma gives.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">Whether khroma reads it.</param>
    [Theory]
    [InlineData("rgb( 1 , 2 3 )", true)]
    [InlineData("RGBA(1,2,3,/ 50%)", false)]
    [InlineData("rgba(1,2,3 / +.5)", true)]
    [InlineData("hsl(1e1deg, 50%, 50%)", true)]
    [InlineData("HSL(1GRAD 50% 50%)", true)]
    [InlineData("hsl(10, 50, 50%)", false)]
    [InlineData("rgb(1,,2,3)", false)]
    [InlineData(" #fff", false)]
    [InlineData("#ff", false)]
    [InlineData("steelblue", false)]
    [InlineData("CyanAqua", true)]
    public void ColorMath_IsColor_ParserEdges(string text, bool expected)
    {
        // Act
        var parsed = ColorMath.IsColor(text);

        // Assert
        Assert.Equal(expected, parsed);
    }

    /// <summary>
    ///     Calls the khroma function of a vector.
    /// </summary>
    /// <param name="function">The function name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result as JSON, or the error message.</returns>
    private static (JsonNode? Result, string? Error) Call(string function, JsonArray arguments)
    {
        try
        {
            var color = arguments[0]!.GetValueKind() == System.Text.Json.JsonValueKind.String ? (string?)arguments[0] : null;
            JsonNode? result = function switch
            {
                "invert" => ColorMath.Invert(color),
                "darken" => ColorMath.Darken(color, (double)arguments[1]!),
                "lighten" => arguments.Count > 1 ? ColorMath.Lighten(color, (double)arguments[1]!) : ColorMath.Lighten(color),
                "adjust" => Adjust(color, arguments[1]!.AsObject()),
                "rgba" when color is not null => ColorMath.Rgba(color, (double)arguments[1]!),
                "rgba" => ColorMath.Rgba((double)arguments[0]!, (double)arguments[1]!, (double)arguments[2]!, (double)arguments[3]!),
                "isDark" => ColorMath.IsDark(color),
                "luminance" => ColorMath.Luminance(color),
                "channel" => ColorMath.Channel(color, ((string)arguments[1]!)[0]),
                _ => throw new ArgumentException($"unknown function {function}", nameof(function)),
            };
            return (result, null);
        }
        catch (Exception ex) when (ex is ColorFormatException or InvalidOperationException)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>
    ///     Calls <see cref="ColorMath.Adjust"/> with the channels of a JSON object.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="channels">The channel changes.</param>
    /// <returns>The result.</returns>
    private static string Adjust(string? color, JsonObject channels)
    {
        double Get(string name) => channels[name] is { } value ? (double)value : 0;
        return ColorMath.Adjust(color, Get("h"), Get("s"), Get("l"), Get("r"), Get("g"), Get("b"));
    }
}
