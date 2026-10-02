using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="TilePaint"/>.
/// </summary>
public class TilePaintTests
{
    private static Surface OneByOneSurface() => new(1, 1);

    /// <summary>
    ///     Proves that the constructor rejects a null surface.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_NullSurface_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TilePaint(null!, Matrix3x2.Identity, 1f, 1f));
    }

    /// <summary>
    ///     Proves that the constructor rejects a transform with a NaN component.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_TransformWithNaNComponent_ThrowsArgumentOutOfRangeException()
    {
        using var surface = OneByOneSurface();
        var transform = new Matrix3x2(float.NaN, 0, 0, 1, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TilePaint(surface, transform, 1f, 1f));
    }

    /// <summary>
    ///     Proves that the constructor rejects a transform with an infinity component.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_TransformWithInfinityComponent_ThrowsArgumentOutOfRangeException()
    {
        using var surface = OneByOneSurface();
        var transform = new Matrix3x2(1, 0, 0, float.PositiveInfinity, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TilePaint(surface, transform, 1f, 1f));
    }

    /// <summary>
    ///     Proves that the constructor rejects a zero XStep.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_ZeroXStep_ThrowsArgumentOutOfRangeException()
    {
        using var surface = OneByOneSurface();

        Assert.Throws<ArgumentOutOfRangeException>(() => new TilePaint(surface, Matrix3x2.Identity, 0f, 1f));
    }

    /// <summary>
    ///     Proves that the constructor rejects a zero YStep.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_ZeroYStep_ThrowsArgumentOutOfRangeException()
    {
        using var surface = OneByOneSurface();

        Assert.Throws<ArgumentOutOfRangeException>(() => new TilePaint(surface, Matrix3x2.Identity, 1f, 0f));
    }

    /// <summary>
    ///     Proves that the constructor rejects a NaN or infinite XStep/YStep.
    /// </summary>
    [Theory]
    [InlineData(float.NaN, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(1f, float.NaN)]
    [InlineData(1f, float.NegativeInfinity)]
    public void TilePaint_Constructor_NonFiniteStep_ThrowsArgumentOutOfRangeException(float xStep, float yStep)
    {
        using var surface = OneByOneSurface();

        Assert.Throws<ArgumentOutOfRangeException>(() => new TilePaint(surface, Matrix3x2.Identity, xStep, yStep));
    }

    /// <summary>
    ///     Proves that a negative XStep/YStep is accepted, not rejected - it is a legitimate PDF
    ///     tiling-pattern value meaning the tile repeats in the negative axis direction.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_NegativeSteps_Succeeds()
    {
        using var surface = OneByOneSurface();

        var tile = new TilePaint(surface, Matrix3x2.Identity, -2f, -3f);

        Assert.Equal(-2f, tile.XStep);
        Assert.Equal(-3f, tile.YStep);
    }

    /// <summary>
    ///     Proves that every constructor argument round-trips through its corresponding property.
    /// </summary>
    [Fact]
    public void TilePaint_Constructor_ValidValues_PropertiesRoundTrip()
    {
        using var surface = new Surface(2, 3);
        var transform = Matrix3x2.CreateScale(2f);

        var tile = new TilePaint(surface, transform, 5f, 7f);

        Assert.Same(surface, tile.Surface);
        Assert.Equal(transform, tile.Transform);
        Assert.Equal(5f, tile.XStep);
        Assert.Equal(7f, tile.YStep);
    }

    /// <summary>
    ///     Proves that <see cref="TilePaint.WithTransform"/> composes the supplied transform
    ///     after this tile paint's own existing <see cref="TilePaint.Transform"/> (row-vector
    ///     convention: <c>this.Transform * transform</c>), returning a new <see cref="TilePaint"/>
    ///     that otherwise preserves every other property (Surface/XStep/YStep) unchanged, and
    ///     leaves the original instance untouched.
    /// </summary>
    [Fact]
    public void TilePaint_WithTransform_ComposesTransformPreservingOtherProperties()
    {
        using var surface = OneByOneSurface();
        var original = new TilePaint(surface, Matrix3x2.CreateScale(2f), 4f, 6f);
        var extra = Matrix3x2.CreateTranslation(10f, 5f);

        var result = original.WithTransform(extra);

        Assert.Equal(original.Transform * extra, result.Transform);
        Assert.Same(surface, result.Surface);
        Assert.Equal(4f, result.XStep);
        Assert.Equal(6f, result.YStep);
        Assert.Equal(Matrix3x2.CreateScale(2f), original.Transform);
    }
}
