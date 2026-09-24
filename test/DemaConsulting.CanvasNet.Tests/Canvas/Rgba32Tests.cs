using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>Unit tests for the <see cref="Rgba32"/> parse/tryparse behavior.</summary>
public class Rgba32Tests
{
    /// <summary>Rgba32_Parse_6HexUppercase_ReturnsExpectedRgbaWithAlpha255.</summary>
    [Fact]
    public void Rgba32_Parse_6HexUppercase_ReturnsExpectedRgbaWithAlpha255()
    {
        var color = Rgba32.Parse("#ABCDEF");
        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), color);
    }

    /// <summary>Rgba32_Parse_6HexLowercase_ReturnsExpectedRgbaWithAlpha255.</summary>
    [Fact]
    public void Rgba32_Parse_6HexLowercase_ReturnsExpectedRgbaWithAlpha255()
    {
        var color = Rgba32.Parse("#abcdef");
        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), color);
    }

    /// <summary>Rgba32_Parse_6HexMixedCase_ReturnsExpectedRgba.</summary>
    [Fact]
    public void Rgba32_Parse_6HexMixedCase_ReturnsExpectedRgba()
    {
        Assert.Equal(new Rgba32(0xA1, 0xB2, 0xC3, 255), Rgba32.Parse("#a1B2c3"));
    }

    /// <summary>Rgba32_Parse_8HexUppercase_ReturnsExpectedArgb.</summary>
    [Fact]
    public void Rgba32_Parse_8HexUppercase_ReturnsExpectedArgb()
    {
        var color = Rgba32.Parse("#80112233");
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x80), color);
    }

    /// <summary>Rgba32_Parse_8HexLowercase_ReturnsExpectedArgb.</summary>
    [Fact]
    public void Rgba32_Parse_8HexLowercase_ReturnsExpectedArgb()
    {
        var color = Rgba32.Parse("#80aabbcc");
        Assert.Equal(new Rgba32(0xAA, 0xBB, 0xCC, 0x80), color);
    }

    /// <summary>Rgba32_Parse_MissingHash_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_MissingHash_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("ABCDEF"));
    }

    /// <summary>Rgba32_Parse_3HexShortForm_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_3HexShortForm_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#ABC"));
    }

    /// <summary>Rgba32_Parse_4HexArgbShortForm_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_4HexArgbShortForm_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#ABCD"));
    }

    /// <summary>Rgba32_Parse_7Hex_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_7Hex_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#ABCDEFA"));
    }

    /// <summary>Rgba32_Parse_9Hex_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_9Hex_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#123456789"));
    }

    /// <summary>Rgba32_Parse_NonHexCharacter_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_NonHexCharacter_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse("#GGGGGG"));
    }

    /// <summary>Rgba32_Parse_Null_ThrowsArgumentNullException.</summary>
    [Fact]
    public void Rgba32_Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Rgba32.Parse(null!));
    }

    /// <summary>Rgba32_Parse_EmptyString_ThrowsFormatException.</summary>
    [Fact]
    public void Rgba32_Parse_EmptyString_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Rgba32.Parse(""));
    }

    /// <summary>Rgba32_TryParse_ValidInput_ReturnsTrueAndSetsResult.</summary>
    [Fact]
    public void Rgba32_TryParse_ValidInput_ReturnsTrueAndSetsResult()
    {
        Assert.True(Rgba32.TryParse("#010203", out var c));
        Assert.Equal(new Rgba32(1, 2, 3, 255), c);
    }

    /// <summary>Rgba32_TryParse_InvalidInput_ReturnsFalseAndSetsResultToDefault.</summary>
    [Fact]
    public void Rgba32_TryParse_InvalidInput_ReturnsFalseAndSetsResultToDefault()
    {
        Assert.False(Rgba32.TryParse("#ZZZ", out var c));
        Assert.Equal(default, c);
    }

    /// <summary>Rgba32_TryParse_Null_ReturnsFalse.</summary>
    [Fact]
    public void Rgba32_TryParse_Null_ReturnsFalse()
    {
        Assert.False(Rgba32.TryParse(null, out var c));
        Assert.Equal(default, c);
    }

    /// <summary>Rgba32_Parse_ExceptionMessage_ContainsFormatGuidance.</summary>
    [Fact]
    public void Rgba32_Parse_ExceptionMessage_ContainsFormatGuidance()
    {
        var ex = Assert.Throws<FormatException>(() => Rgba32.Parse("XYZ"));
        Assert.Contains("#RRGGBB", ex.Message, StringComparison.Ordinal);
    }
}
