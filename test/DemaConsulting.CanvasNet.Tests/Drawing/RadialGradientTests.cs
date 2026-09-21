using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="RadialGradient"/>.
/// </summary>
public class RadialGradientTests
{
    private static GradientStop[] OneStop() => [new GradientStop(0f, new Rgba32(1, 2, 3, 4))];

    /// <summary>
    ///     Proves that every constructor argument round-trips through its corresponding property,
    ///     using distinct start/end centers and radii to prove the full two-circle model is
    ///     genuinely represented (not silently collapsed to a single-circle equivalent).
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_ValidValues_PropertiesRoundTrip()
    {
        var startCenter = new Vector2(1, 2);
        var endCenter = new Vector2(9, 8);

        var gradient = new RadialGradient(startCenter, 3f, endCenter, 7f, OneStop(), GradientSpread.Repeat);

        Assert.Equal(startCenter, gradient.StartCenter);
        Assert.Equal(3f, gradient.StartRadius);
        Assert.Equal(endCenter, gradient.EndCenter);
        Assert.Equal(7f, gradient.EndRadius);
        Assert.Equal(GradientSpread.Repeat, gradient.Spread);
    }

    /// <summary>
    ///     Proves that a single-circle-equivalent configuration (start radius zero, coincident
    ///     centers) is still accepted, since the two-circle model subsumes it.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_SingleCircleEquivalentConfiguration_Succeeds()
    {
        var center = new Vector2(4, 4);

        var gradient = new RadialGradient(center, 0f, center, 5f, OneStop());

        Assert.Equal(center, gradient.StartCenter);
        Assert.Equal(0f, gradient.StartRadius);
        Assert.Equal(center, gradient.EndCenter);
        Assert.Equal(5f, gradient.EndRadius);
    }

    /// <summary>
    ///     Proves that the constructor rejects a negative start radius.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_NegativeStartRadius_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RadialGradient(Vector2.Zero, -1f, Vector2.One, 1f, OneStop()));
    }

    /// <summary>
    ///     Proves that the constructor rejects a negative end radius.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_NegativeEndRadius_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RadialGradient(Vector2.Zero, 1f, Vector2.One, -1f, OneStop()));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-finite start radius.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_NonFiniteStartRadius_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RadialGradient(Vector2.Zero, float.NaN, Vector2.One, 1f, OneStop()));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-finite start center.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_NonFiniteStartCenter_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RadialGradient(new Vector2(float.NaN, 0), 1f, Vector2.One, 1f, OneStop()));
    }

    /// <summary>
    ///     Proves that the constructor rejects a non-finite end center.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_NonFiniteEndCenter_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RadialGradient(Vector2.Zero, 1f, new Vector2(float.PositiveInfinity, 0), 1f, OneStop()));
    }

    /// <summary>
    ///     Proves that the constructor rejects an empty stops list.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_EmptyStops_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new RadialGradient(Vector2.Zero, 0f, Vector2.One, 1f, []));
    }

    /// <summary>
    ///     Proves that both radii zero with coincident centers - the fully degenerate radial
    ///     configuration - is still accepted at construction (it is resolved at evaluation time,
    ///     not rejected here).
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_BothRadiiZeroCoincidentCenters_Succeeds()
    {
        var center = new Vector2(2, 2);

        var gradient = new RadialGradient(center, 0f, center, 0f, OneStop());

        Assert.Equal(0f, gradient.StartRadius);
        Assert.Equal(0f, gradient.EndRadius);
    }

    /// <summary>
    ///     Proves that a caller-omitted (default) transform is treated as the identity matrix.
    /// </summary>
    [Fact]
    public void RadialGradient_Constructor_DefaultTransform_IsIdentity()
    {
        var gradient = new RadialGradient(Vector2.Zero, 0f, Vector2.One, 1f, OneStop());

        Assert.Equal(Matrix3x2.Identity, gradient.Transform);
    }
}
