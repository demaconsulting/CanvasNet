using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="GradientStop"/>.
/// </summary>
public class GradientStopTests
{
    /// <summary>
    ///     Proves that the constructor rejects an offset outside [0, 1].
    /// </summary>
    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void GradientStop_Constructor_OffsetOutsideZeroToOne_ThrowsArgumentOutOfRangeException(float offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GradientStop(offset, new Rgba32(1, 2, 3, 4)));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-finite offset (NaN or either infinity).
    /// </summary>
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void GradientStop_Constructor_NonFiniteOffset_ThrowsArgumentOutOfRangeException(float offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GradientStop(offset, new Rgba32(1, 2, 3, 4)));
    }

    /// <summary>
    ///     Proves that the boundary offsets zero and one are both accepted.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void GradientStop_Constructor_BoundaryOffsetsZeroAndOne_Succeeds(float offset)
    {
        var stop = new GradientStop(offset, new Rgba32(9, 8, 7, 6));

        Assert.Equal(offset, stop.Offset);
        Assert.Equal(new Rgba32(9, 8, 7, 6), stop.Color);
    }
}
