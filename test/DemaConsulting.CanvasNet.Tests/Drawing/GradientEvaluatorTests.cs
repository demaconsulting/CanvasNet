using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="GradientEvaluator"/> class.
/// </summary>
public class GradientEvaluatorTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

    private static GradientStop[] TwoStops() =>
    [
        new GradientStop(0f, Red),
        new GradientStop(1f, Blue),
    ];

    /// <summary>
    ///     Proves that EvaluatePoint throws ArgumentNullException when the gradient is null.
    /// </summary>
    [Fact]
    public void EvaluatePoint_NullGradient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GradientEvaluator.EvaluatePoint(null!, Vector2.Zero));
    }

    /// <summary>
    ///     Proves that a single-stop gradient resolves to that stop's color everywhere, for both
    ///     linear and radial gradients.
    /// </summary>
    [Fact]
    public void EvaluatePoint_SingleStop_ResolvesToThatColorEverywhere()
    {
        var stops = new[] { new GradientStop(0.5f, new Rgba32(10, 20, 30, 255)) };
        var linear = new LinearGradient(Vector2.Zero, new Vector2(10, 0), stops);
        var radial = new RadialGradient(Vector2.Zero, 0f, Vector2.Zero, 10f, stops);

        Assert.Equal(new Rgba32(10, 20, 30, 255), GradientEvaluator.EvaluatePoint(linear, new Vector2(-100, 5)));
        Assert.Equal(new Rgba32(10, 20, 30, 255), GradientEvaluator.EvaluatePoint(linear, new Vector2(100, 5)));
        Assert.Equal(new Rgba32(10, 20, 30, 255), GradientEvaluator.EvaluatePoint(radial, new Vector2(1, 1)));
    }

    /// <summary>
    ///     Proves that a linear gradient exactly reproduces its first stop's color at Start and
    ///     its last stop's color at End.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_EndpointsMatchFirstAndLastStopColors()
    {
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops());

        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 0)));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(10, 0)));
    }

    /// <summary>
    ///     Proves that a linear gradient with a zero-length vector (Start equal to End) flat-fills
    ///     with the last stop's color, per the degenerate-case policy.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_ZeroLengthVector_FlatFillsWithLastStopColor()
    {
        var point = new Vector2(3, 3);
        var gradient = new LinearGradient(point, point, TwoStops());

        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(-500, 500)));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, point));
    }

    /// <summary>
    ///     Proves that GradientSpread.Pad clamps a linear gradient's out-of-range parameter to
    ///     the nearest endpoint's color, rather than wrapping or reflecting.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_PadSpread_ClampsBeyondEndpoints()
    {
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops(), GradientSpread.Pad);

        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(-50, 0)));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(500, 0)));
    }

    /// <summary>
    ///     Proves that GradientSpread.Repeat wraps a linear gradient's out-of-range parameter back
    ///     into [0, 1], including for a negative raw parameter (exercising the floor-mod, not a
    ///     naive "%").
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_RepeatSpread_WrapsNegativeAndPositiveOutOfRange()
    {
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops(), GradientSpread.Repeat);

        // x = 10 -> raw t = 1.0 -> Blue. x = 20 -> raw t = 2.0 -> wraps to 0.0 -> Red.
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(20, 0)));

        // x = -10 -> raw t = -1.0 -> wraps to 0.0 -> Red. x = -5 -> raw t = -0.5 -> wraps to 0.5.
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(-10, 0)));
    }

    /// <summary>
    ///     Proves that GradientSpread.Reflect folds a linear gradient's out-of-range parameter
    ///     into a period-2 triangle wave, so a raw t of 1.5 and a raw t of 0.5 resolve to the
    ///     same color.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_ReflectSpread_FoldsIntoTriangleWave()
    {
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops(), GradientSpread.Reflect);

        // x = 15 -> raw t = 1.5 -> reflect -> 0.5, matching x = 5 -> raw t = 0.5.
        var atHalf = GradientEvaluator.EvaluatePoint(gradient, new Vector2(5, 0));
        var atReflected = GradientEvaluator.EvaluatePoint(gradient, new Vector2(15, 0));

        Assert.Equal(atHalf, atReflected);
    }

    /// <summary>
    ///     Proves that out-of-order input stops are resolved correctly at evaluation time (the
    ///     constructor's defensive sort is what makes this correct) - the visual ramp still goes
    ///     from the offset-0 stop's color to the offset-1 stop's color regardless of input order.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_UnsortedInputStops_ResolvesInSortedOrder()
    {
        GradientStop[] stops = [new GradientStop(1f, Blue), new GradientStop(0f, Red)];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 0)));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(10, 0)));
    }

    /// <summary>
    ///     Proves that duplicate stop offsets ("hard stop") produce a sharp step at that offset
    ///     rather than a divide-by-zero exception or NaN result.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_DuplicateStopOffsets_ProducesHardStepWithoutError()
    {
        GradientStop[] stops =
        [
            new GradientStop(0f, Red),
            new GradientStop(0.5f, Red),
            new GradientStop(0.5f, Blue),
            new GradientStop(1f, Blue),
        ];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        // Just below the hard stop: still red. Just above: blue. No exception, no NaN channel.
        var justBelow = GradientEvaluator.EvaluatePoint(gradient, new Vector2(4.99f, 0));
        var justAbove = GradientEvaluator.EvaluatePoint(gradient, new Vector2(5.1f, 0));

        Assert.Equal(Red, justBelow);
        Assert.Equal(Blue, justAbove);
    }

    /// <summary>
    ///     Proves that a non-finite (NaN) transform component is rejected at construction, not
    ///     silently tolerated at evaluation time.
    /// </summary>
    [Fact]
    public void Constructor_NonFiniteTransform_ThrowsArgumentOutOfRangeException()
    {
        var transform = new Matrix3x2(float.NaN, 0, 0, 1, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LinearGradient(Vector2.Zero, Vector2.One, TwoStops(), transform: transform));
    }

    /// <summary>
    ///     Proves that a non-invertible (singular) transform flat-fills with the last stop's
    ///     color, per the degenerate-case policy - constructed via a scale-by-zero matrix, which
    ///     passes the finiteness check but is not invertible.
    /// </summary>
    [Fact]
    public void EvaluatePoint_SingularTransform_FlatFillsWithLastStopColor()
    {
        // A matrix with linearly dependent rows (determinant zero) that is not the all-zero
        // default(Matrix3x2) - which the constructor would otherwise substitute with the
        // identity transform, defeating this test's purpose.
        var singular = new Matrix3x2(1, 1, 1, 1, 0, 0);
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops(), transform: singular);

        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(3, 3)));
    }

    /// <summary>
    ///     Proves that extreme float32-magnitude gradient coordinates still resolve to the
    ///     correct endpoint colors without overflow-induced NaN/incorrect results, since the
    ///     evaluator performs its projection math in double precision.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_ExtremeMagnitudeCoordinates_ResolvesCorrectly()
    {
        const float large = 1.0e30f;
        var gradient = new LinearGradient(new Vector2(-large, 0), new Vector2(large, 0), TwoStops());

        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(-large, 0)));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(large, 0)));
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(-large * 2, 0)));
    }

    /// <summary>
    ///     Proves that a radial gradient (single-circle-equivalent configuration: start radius
    ///     zero, coincident centers) resolves to the first stop's color at the shared center and
    ///     the last stop's color at/beyond the end circle's radius.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_SingleCircleEquivalent_CenterAndEdgeMatchEndpointColors()
    {
        var center = new Vector2(0, 0);
        var gradient = new RadialGradient(center, 0f, center, 10f, TwoStops());

        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, center));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(10, 0)));
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(20, 0)));
    }

    /// <summary>
    ///     Proves that both radii zero with coincident centers flat-fills with the last stop's
    ///     color everywhere, per the degenerate-case policy.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_BothRadiiZeroCoincidentCenters_FlatFillsWithLastStopColor()
    {
        var center = new Vector2(2, 2);
        var gradient = new RadialGradient(center, 0f, center, 0f, TwoStops());

        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(500, -500)));
    }

    /// <summary>
    ///     Proves that a two-circle radial gradient with genuinely distinct, non-nested start and
    ///     end circles leaves a point outside both circles' swept family (the "no valid root"
    ///     region) fully transparent, rather than clamping it to an endpoint color.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_TwoCircle_NoValidRootRegion_IsFullyTransparent()
    {
        // Two small, well-separated, equal-radius circles: a point far off to the side of both
        // is never inside any interpolated circle in the swept family.
        var gradient = new RadialGradient(new Vector2(-10, 0), 1f, new Vector2(10, 0), 1f, TwoStops());

        var result = GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 100));

        Assert.Equal(0, result.A);
    }

    /// <summary>
    ///     Proves that a genuinely two-circle radial gradient (distinct centers and distinct
    ///     radii) resolves the first stop's color at the start circle's center and the last
    ///     stop's color at the end circle's edge - proving the two-circle model is fully
    ///     exercised, not reduced to a single-circle equivalent.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_TwoCircle_DistinctCentersAndRadii_ResolvesAlongSweptFamily()
    {
        var gradient = new RadialGradient(new Vector2(0, 0), 0f, new Vector2(20, 0), 6f, TwoStops());

        // At t = 0, the swept circle degenerates to the point (0, 0) (radius 0).
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 0)));

        // At t = 1, the swept circle is (center (20,0), radius 6) - a point on its edge.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(26, 0)));
    }

    /// <summary>
    ///     Proves that mixed transparent/opaque stops interpolate in premultiplied alpha space:
    ///     the midpoint between an opaque red stop and a fully transparent white stop must not
    ///     resolve to a washed-out pink/gray straight-alpha-lerp fringe color, and its alpha must
    ///     equal a plain midpoint of the endpoint alphas.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_TransparentAndOpaqueStops_InterpolatesInPremultipliedAlphaSpace()
    {
        GradientStop[] stops =
        [
            new GradientStop(0f, new Rgba32(255, 0, 0, 255)),
            new GradientStop(1f, new Rgba32(255, 255, 255, 0)),
        ];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        var mid = GradientEvaluator.EvaluatePoint(gradient, new Vector2(5, 0));

        // Premultiplied lerp at fraction 0.5: premultiplied red channel goes 255 -> 0 (half:
        // 127.5), alpha goes 255 -> 0 (half: 127.5). Unpremultiplied red = 127.5 / 127.5 * 255 =
        // 255 - i.e. the color channel stays saturated red as alpha fades, never washing out to
        // gray/pink the way a naive straight-alpha lerp would.
        Assert.Equal(128, mid.A);
        Assert.Equal(255, mid.R);
        Assert.Equal(0, mid.G);
        Assert.Equal(0, mid.B);
    }

    /// <summary>
    ///     Proves that a fully-zero interpolated premultiplied alpha resolves to (0, 0, 0, 0),
    ///     matching Surface.UnpremultiplyAlpha's "alpha == 0 implies R = G = B = 0" convention,
    ///     rather than propagating a divide-by-zero NaN into the color channels.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_BothStopsFullyTransparent_ResolvesToAllZeroPixel()
    {
        GradientStop[] stops =
        [
            new GradientStop(0f, new Rgba32(10, 20, 30, 0)),
            new GradientStop(1f, new Rgba32(40, 50, 60, 0)),
        ];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        var mid = GradientEvaluator.EvaluatePoint(gradient, new Vector2(5, 0));

        Assert.Equal(new Rgba32(0, 0, 0, 0), mid);
    }

    /// <summary>
    ///     Proves that EvaluateRow fills a destination span with one color per pixel across a
    ///     multi-pixel run, matching per-pixel EvaluatePoint results at each pixel's center.
    /// </summary>
    [Fact]
    public void EvaluateRow_MultiPixelRun_MatchesPerPixelEvaluatePoint()
    {
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(4, 0), TwoStops());
        Span<Rgba32> destination = stackalloc Rgba32[4];

        GradientEvaluator.EvaluateRow(gradient, 0, 0, 4, destination);

        for (var i = 0; i < 4; i++)
        {
            var expected = GradientEvaluator.EvaluatePoint(gradient, new Vector2(i + 0.5f, 0.5f));
            Assert.Equal(expected, destination[i]);
        }
    }

    /// <summary>
    ///     Proves that EvaluateRow throws ArgumentNullException when the gradient is null.
    /// </summary>
    [Fact]
    public void EvaluateRow_NullGradient_ThrowsArgumentNullException()
    {
        Span<Rgba32> destination = stackalloc Rgba32[1];

        // A plain try/catch is used instead of Assert.Throws(() => ...) because Span<Rgba32> is a
        // ref struct and cannot be captured by a lambda expression.
        var threw = false;
        try
        {
            GradientEvaluator.EvaluateRow(null!, 0, 0, 1, destination);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }

        Assert.True(threw, "Expected EvaluateRow to throw ArgumentNullException.");
    }
}
