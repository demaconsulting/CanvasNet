namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartTitle"/>.
/// </summary>
public class ChartTitleTests
{
    /// <summary>Proves constructing with only text sets the text and leaves the font size unset.</summary>
    [Fact]
    public void Constructor_TextOnly_SetsTextAndLeavesFontSizeUnset()
    {
        var title = new ChartTitle("Revenue");

        Assert.Equal("Revenue", title.Text);
        Assert.Null(title.FontSize);
    }

    /// <summary>Proves constructing with both text and a font size sets both members.</summary>
    [Fact]
    public void Constructor_TextAndFontSize_SetsBothMembers()
    {
        var title = new ChartTitle("Revenue", 18f);

        Assert.Equal("Revenue", title.Text);
        Assert.Equal(18f, title.FontSize);
    }

    /// <summary>Proves a null text argument is rejected.</summary>
    [Fact]
    public void Constructor_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ChartTitle(null!));
    }

    /// <summary>Proves an empty text argument is rejected.</summary>
    [Fact]
    public void Constructor_EmptyText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ChartTitle(""));
    }

    /// <summary>Proves a whitespace-only text argument is rejected.</summary>
    [Fact]
    public void Constructor_WhitespaceText_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new ChartTitle("   "));
    }

    /// <summary>Proves a non-finite or non-positive font size is rejected.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_InvalidFontSize_ThrowsArgumentOutOfRangeException(float fontSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChartTitle("Revenue", fontSize));
    }
}
