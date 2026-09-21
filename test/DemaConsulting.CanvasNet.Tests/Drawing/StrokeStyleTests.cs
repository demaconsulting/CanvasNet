using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="StrokeStyle"/>.
/// </summary>
public class StrokeStyleTests
{
    /// <summary>
    ///     Proves that the constructor rejects a non-positive or non-finite width.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void StrokeStyle_Constructor_NonPositiveWidth_ThrowsArgumentOutOfRangeException(float width)
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrokeStyle(width));
    }

    /// <summary>
    ///     Proves that the constructor rejects a miter limit below one.
    /// </summary>
    [Fact]
    public void StrokeStyle_Constructor_MiterLimitBelowOne_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrokeStyle(1f, miterLimit: 0.99f));
    }

    /// <summary>
    ///     Proves that the default miter limit is four.
    /// </summary>
    [Fact]
    public void StrokeStyle_Constructor_DefaultMiterLimit_IsFour()
    {
        // Arrange / Act
        var style = new StrokeStyle(3f);

        // Assert
        Assert.Equal(4f, style.MiterLimit);
    }

    /// <summary>
    ///     Proves that null or empty dash arrays are treated as a solid stroke.
    /// </summary>
    [Fact]
    public void StrokeStyle_Constructor_NullOrEmptyDashArray_IsTreatedAsSolid()
    {
        // Arrange / Act
        var nullStyle = new StrokeStyle(2f, dashArray: null);
        var emptyStyle = new StrokeStyle(2f, dashArray: Array.Empty<float>());

        // Assert
        Assert.Null(nullStyle.DashArray);
        Assert.Null(emptyStyle.DashArray);
    }

    /// <summary>
    ///     Proves that the constructor rejects any negative dash-array entry.
    /// </summary>
    [Fact]
    public void StrokeStyle_Constructor_DashArrayWithNegativeEntry_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new StrokeStyle(2f, dashArray: [1f, -1f]));
    }

    /// <summary>
    ///     Proves that the constructor rejects a dash array whose entries are all zero.
    /// </summary>
    [Fact]
    public void StrokeStyle_Constructor_DashArrayAllZero_ThrowsArgumentException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new StrokeStyle(2f, dashArray: [0f, 0f, 0f]));
    }

    /// <summary>
    ///     Proves that valid constructor arguments round-trip through the exposed properties.
    /// </summary>
    [Fact]
    public void StrokeStyle_Constructor_ValidValues_PropertiesRoundTrip()
    {
        // Arrange / Act
        var style = new StrokeStyle(
            5f,
            LineCap.Round,
            LineJoin.Bevel,
            7f,
            [2f, 1f, 3f],
            dashOffset: -0.5f);

        // Assert
        Assert.Equal(5f, style.Width);
        Assert.Equal(LineCap.Round, style.Cap);
        Assert.Equal(LineJoin.Bevel, style.Join);
        Assert.Equal(7f, style.MiterLimit);
        Assert.NotNull(style.DashArray);
        Assert.Equal([2f, 1f, 3f], style.DashArray.ToArray());
        Assert.Equal(-0.5f, style.DashOffset);
    }
}
