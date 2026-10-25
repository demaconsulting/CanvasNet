using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadText"/> and <see cref="ScadNumbers"/>.
/// </summary>
public class ScadTextTests
{
    /// <summary>The text "a", U+1F600 (a surrogate pair), "b".</summary>
    private const string Mixed = "a\U0001F600b";

    /// <summary>
    ///     Validates scalar counting with and without surrogate pairs and with an unpaired surrogate.
    /// </summary>
    [Fact]
    public void ScadText_ScalarCount_CountsPairsOnce()
    {
        // Arrange / Act / Assert
        Assert.Equal(0, ScadText.ScalarCount(string.Empty));
        Assert.Equal(3, ScadText.ScalarCount("abc"));
        Assert.Equal(3, ScadText.ScalarCount(Mixed));
        Assert.Equal(1, ScadText.ScalarCount("\uD800"));
        Assert.Equal(2, ScadText.ScalarCount("\uD800a"));
        Assert.Equal(2, ScadText.ScalarCount("\uDC00\uD800"));
    }

    /// <summary>
    ///     Validates scalar access by index.
    /// </summary>
    [Fact]
    public void ScadText_ScalarAt_ReturnsScalarOrNull()
    {
        // Arrange / Act / Assert
        Assert.Equal("a", ScadText.ScalarAt("abc", 0));
        Assert.Equal("c", ScadText.ScalarAt("abc", 2));
        Assert.Null(ScadText.ScalarAt("abc", 3));
        Assert.Null(ScadText.ScalarAt("abc", -1));
        Assert.Null(ScadText.ScalarAt("abc", long.MaxValue));
        Assert.Equal("\U0001F600", ScadText.ScalarAt(Mixed, 1));
        Assert.Equal("b", ScadText.ScalarAt(Mixed, 2));
        Assert.Null(ScadText.ScalarAt(Mixed, 3));
        Assert.Equal("\uD800", ScadText.ScalarAt("\uD800", 0));
        Assert.Null(ScadText.ScalarAt("x\U0001F600", 5));
    }

    /// <summary>
    ///     Validates splitting into scalars.
    /// </summary>
    [Fact]
    public void ScadText_Scalars_SplitsIntoScalars()
    {
        // Arrange / Act
        var scalars = ScadText.Scalars(Mixed);

        // Assert
        Assert.Equal(["a", "\U0001F600", "b"], scalars);
        Assert.Empty(ScadText.Scalars(string.Empty));
    }

    /// <summary>
    ///     Validates the first code point.
    /// </summary>
    [Fact]
    public void ScadText_FirstCodePoint_HandlesPairsAndLoneSurrogates()
    {
        // Arrange / Act / Assert
        Assert.Equal(97, ScadText.FirstCodePoint("abc"));
        Assert.Equal(0x1F600, ScadText.FirstCodePoint("\U0001F600x"));
        Assert.Equal(0xD800, ScadText.FirstCodePoint("\uD800"));
    }

    /// <summary>
    ///     Validates conversion of code points to strings, including invalid values.
    /// </summary>
    [Fact]
    public void ScadText_FromCodePoint_ValidAndInvalid()
    {
        // Arrange / Act / Assert
        Assert.Equal("A", ScadText.FromCodePoint(65));
        Assert.Equal("\U0001F600", ScadText.FromCodePoint(0x1F600));
        Assert.Equal("\0", ScadText.FromCodePoint(0));
        Assert.Null(ScadText.FromCodePoint(-1));
        Assert.Null(ScadText.FromCodePoint(0x110000));
        Assert.Null(ScadText.FromCodePoint(0xD800));
        Assert.Null(ScadText.FromCodePoint(0xDFFF));
        Assert.Null(ScadText.FromCodePoint(65.5));
        Assert.Null(ScadText.FromCodePoint(double.NaN));
        Assert.Null(ScadText.FromCodePoint(double.PositiveInfinity));
    }

    /// <summary>
    ///     Validates the IEEE equality helper.
    /// </summary>
    [Fact]
    public void ScadNumbers_AreEqual_FollowsIeee()
    {
        // Arrange / Act / Assert
        Assert.True(ScadNumbers.AreEqual(1, 1));
        Assert.True(ScadNumbers.AreEqual(0, -0.0));
        Assert.True(ScadNumbers.AreEqual(double.PositiveInfinity, double.PositiveInfinity));
        Assert.False(ScadNumbers.AreEqual(double.NaN, double.NaN));
        Assert.False(ScadNumbers.AreEqual(double.NaN, 1));
        Assert.False(ScadNumbers.AreEqual(1, double.NaN));
        Assert.False(ScadNumbers.AreEqual(1, 2));
    }
}
