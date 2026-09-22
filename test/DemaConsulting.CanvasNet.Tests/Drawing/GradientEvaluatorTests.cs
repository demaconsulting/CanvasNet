// cspell:ignore precomputation
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
    private static readonly Rgba32 Green = new(0, 255, 0, 255);
    private static readonly Rgba32 Yellow = new(255, 255, 0, 255);

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
    ///     Proves that, at an exact interior tie between two stops sharing the same offset, the
    ///     later-supplied stop's color wins outright - not the earlier-supplied one - matching the
    ///     documented "later stop wins" hard-stop policy.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_InteriorDuplicateStopOffsets_ExactTieResolvesToLaterSuppliedStop()
    {
        GradientStop[] stops =
        [
            new GradientStop(0f, Red),
            new GradientStop(0.5f, Green), // earlier-supplied stop at the tied offset
            new GradientStop(0.5f, Yellow), // later-supplied stop at the tied offset - must win
            new GradientStop(1f, Blue),
        ];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        var atTie = GradientEvaluator.EvaluatePoint(gradient, new Vector2(5f, 0));

        Assert.Equal(Yellow, atTie);
    }

    /// <summary>
    ///     Proves that, at an exact tie between two stops sharing the gradient's very first offset,
    ///     the later-supplied stop's color wins outright - not the "first stop in the sorted array"
    ///     shortcut's earlier-supplied stop - matching the documented "later stop wins" hard-stop
    ///     policy even at the boundary.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_DuplicateStopOffsetsAtGradientStart_ExactTieResolvesToLaterSuppliedStop()
    {
        GradientStop[] stops =
        [
            new GradientStop(0f, Green), // earlier-supplied stop at the gradient's first offset
            new GradientStop(0f, Yellow), // later-supplied stop at the gradient's first offset - must win
            new GradientStop(1f, Blue),
        ];
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), stops);

        var atTie = GradientEvaluator.EvaluatePoint(gradient, new Vector2(0f, 0));

        Assert.Equal(Yellow, atTie);
    }

    /// <summary>
    ///     Proves that an extreme-but-invertible (large uniform scale) Transform, combined with an
    ///     extreme-magnitude evaluation point, still resolves to a finite, non-NaN, correct color -
    ///     distinct from the already-covered singular-transform degenerate case (ordinary
    ///     coordinates) and the already-covered extreme-coordinate case (identity transform): here
    ///     both the transform's own magnitude and the point's magnitude are extreme simultaneously.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Linear_ExtremeScaleInvertibleTransformWithExtremeCoordinates_ProducesFiniteNonNaNColor()
    {
        const float scale = 1.0e15f;
        var transform = Matrix3x2.CreateScale(scale);
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops(), transform: transform);

        // World-space point (0,0) inverse-transforms back to gradient-space (0,0) -> first stop.
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 0)));

        // World-space point (10 * scale, 0) inverse-transforms back to gradient-space (10,0) -> last stop.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(10f * scale, 0)));

        // A further, independently extreme-magnitude world-space point still resolves without
        // exception/NaN, Pad-clamped to the last stop's color.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(1.0e30f, 0)));
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
    ///     Proves that an explicitly-supplied all-zero transform (<see langword="default"/>(<see cref="Matrix3x2"/>))
    ///     - now preserved by the constructor rather than silently replaced with the identity
    ///     matrix - is itself singular (the all-zero matrix has no inverse) and correctly falls
    ///     back to the "Degenerate Transform" flat-fill behavior at evaluation time.
    /// </summary>
    [Fact]
    public void EvaluatePoint_ExplicitAllZeroTransform_FlatFillsWithLastStopColor()
    {
        var gradient = new LinearGradient(
            new Vector2(0, 0), new Vector2(10, 0), TwoStops(), transform: default(Matrix3x2));

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
    ///     Proves that coincident centers with equal but <b>nonzero</b> radii - a swept family of
    ///     a single unchanging circle, just like the all-zero-radii case, but never previously
    ///     recognized as degenerate - also flat-fills with the last stop's color, rather than
    ///     leaving points on/off that one circle unpainted or transparent via a spurious
    ///     <see langword="null"/> root.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_EqualNonZeroRadiiCoincidentCenters_FlatFillsWithLastStopColor()
    {
        var center = new Vector2(2, 2);
        var gradient = new RadialGradient(center, 5f, center, 5f, TwoStops());

        // On the (only) circle itself.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(7, 2)));

        // Off the circle entirely (previously would have resolved as "no valid root" and been
        // left transparent, instead of flat-filling per the degenerate-case policy).
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(500, -500)));

        // At the shared center itself.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, center));
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
    ///     Proves that a point exactly on the start circle's boundary resolves to the first
    ///     stop's color even when the two-circle quadratic has a second, spurious, also-valid
    ///     root a hair's breadth away (a self-intersecting sliver of the swept family nearest the
    ///     start circle) - reproducing the exact scenario reported in review: start circle
    ///     (0,0)/r=5, end circle (20,0)/r=6, point (0,5). Naively preferring the numerically
    ///     larger of the two valid roots resolves to an interpolated color close to, but not
    ///     exactly, the first stop's color; the correct root is the boundary-conforming one.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_TwoCircle_PointOnStartCircleWithSpuriousSecondRoot_ResolvesToFirstStopColor()
    {
        var gradient = new RadialGradient(new Vector2(0, 0), 5f, new Vector2(20, 0), 6f, TwoStops());

        var result = GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 5));

        Assert.Equal(Red, result);
    }

    /// <summary>
    ///     Proves the mirror-image scenario of
    ///     <see cref="EvaluatePoint_Radial_TwoCircle_PointOnStartCircleWithSpuriousSecondRoot_ResolvesToFirstStopColor"/>:
    ///     a point exactly on the <b>end</b> circle's boundary, with a shrinking radius from start
    ///     to end, resolves to the last stop's color rather than a spurious near-boundary root.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_TwoCircle_PointOnEndCircleWithSpuriousSecondRoot_ResolvesToLastStopColor()
    {
        var gradient = new RadialGradient(new Vector2(20, 0), 6f, new Vector2(0, 0), 5f, TwoStops());

        var result = GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 5));

        Assert.Equal(Blue, result);
    }

    /// <summary>
    ///     Proves that the tangent-family fully-degenerate case - where the two-circle
    ///     quadratic's coefficients all vanish (<c>a ≈ 0</c>, <c>b ≈ 0</c>, <c>c ≈ 0</c>), so
    ///     that the evaluated point lies on the swept family's boundary circle at
    ///     every <c>t</c> - resolves to the start-side endpoint color for a growing radius
    ///     (<c>dr &gt; 0</c>), per the boundary-conforming selection policy, rather than being
    ///     left fully transparent. Reproduces the exact scenario reported in review: start
    ///     circle (0,0)/r=5, end circle (5,0)/r=10, point (-5,0) - which sits exactly on every
    ///     interpolated circle in the swept family (verified: at t=0, circle (0,0)/r=5 passes
    ///     through (-5,0); at t=1, circle (5,0)/r=10 also passes through (-5,0); at t=0.5,
    ///     circle (2.5,0)/r=7.5 also passes through (-5,0)).
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_TangentFamilyFullyDegenerate_GrowingRadius_ResolvesToFirstStopColor()
    {
        var gradient = new RadialGradient(new Vector2(0, 0), 5f, new Vector2(5, 0), 10f, TwoStops());

        var result = GradientEvaluator.EvaluatePoint(gradient, new Vector2(-5, 0));

        Assert.Equal(Red, result);
        Assert.NotEqual(0, result.A);
    }

    /// <summary>
    ///     Mirror-image scenario of
    ///     <see cref="EvaluatePoint_Radial_TangentFamilyFullyDegenerate_GrowingRadius_ResolvesToFirstStopColor"/>:
    ///     the same tangent-family fully-degenerate configuration, but with a shrinking radius
    ///     from start to end, resolves to the end-side endpoint color instead.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_TangentFamilyFullyDegenerate_ShrinkingRadius_ResolvesToLastStopColor()
    {
        var gradient = new RadialGradient(new Vector2(5, 0), 10f, new Vector2(0, 0), 5f, TwoStops());

        var result = GradientEvaluator.EvaluatePoint(gradient, new Vector2(-5, 0));

        Assert.Equal(Blue, result);
        Assert.NotEqual(0, result.A);
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
    ///     Proves that a radial gradient with equal, nonzero start/end radii but distinct
    ///     start/end centers (a "cylindrical" gradient) varies normally along its swept family of
    ///     equal-radius circles, rather than flat-filling - flat-filling is reserved for the
    ///     stricter "both radii zero and both centers equal" degenerate case only.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_EqualRadiiDistinctCenters_VariesAlongCylindricalSweptFamily()
    {
        // StartCenter (0,0), EndCenter (20,0), both radii 5: the swept family is a "cylinder" of
        // radius-5 circles centered along the x axis from 0 to 20.
        var gradient = new RadialGradient(new Vector2(0, 0), 5f, new Vector2(20, 0), 5f, TwoStops());

        // (0, 5) lies exactly on the start circle (t = 0): distance((0,5), center(0)) = 5.
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(0, 5)));

        // (20, 5) lies exactly on the end circle (t = 1): distance((20,5), center(20,0)) = 5.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(20, 5)));

        // (10, 5) lies exactly on the midpoint circle (t = 0.5): distance((10,5), (10,0)) = 5.
        // Its color must differ from both endpoints, proving the gradient varies continuously
        // rather than flat-filling with a single (e.g. last-stop) color everywhere.
        var midpoint = GradientEvaluator.EvaluatePoint(gradient, new Vector2(10, 5));
        Assert.NotEqual(Red, midpoint);
        Assert.NotEqual(Blue, midpoint);
    }

    /// <summary>
    ///     Proves that extreme float32-magnitude radial-gradient coordinates still resolve to the
    ///     correct endpoint colors without overflow-induced NaN/incorrect results. This targets the
    ///     two-circle quadratic's dx*dx / dy*dy / dr*dr coefficient terms specifically - a distinct
    ///     overflow risk from the linear case's dot-product projection, which is already covered by
    ///     EvaluatePoint_Linear_ExtremeMagnitudeCoordinates_ResolvesCorrectly.
    /// </summary>
    [Fact]
    public void EvaluatePoint_Radial_ExtremeMagnitudeCoordinates_ResolvesCorrectly()
    {
        const float large = 1.0e18f;
        var gradient = new RadialGradient(
            new Vector2(-large, 0), 0f,
            new Vector2(large, 0), large,
            TwoStops());

        // t = 0: the swept circle degenerates to the point (-large, 0) (radius 0).
        Assert.Equal(Red, GradientEvaluator.EvaluatePoint(gradient, new Vector2(-large, 0)));

        // t = 1: the swept circle is (center (large, 0), radius large) - (2 * large, 0) lies
        // exactly on its edge.
        Assert.Equal(Blue, GradientEvaluator.EvaluatePoint(gradient, new Vector2(2 * large, 0)));
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

        var plan = GradientEvaluator.CreatePlan(gradient);
        GradientEvaluator.EvaluateRow(in plan, 0, 0, 4, destination);

        for (var i = 0; i < 4; i++)
        {
            var expected = GradientEvaluator.EvaluatePoint(gradient, new Vector2(i + 0.5f, 0.5f));
            Assert.Equal(expected, destination[i]);
        }
    }

    /// <summary>
    ///     Proves that EvaluateRow's per-fill precomputation (the matrix inverse and two-circle
    ///     quadratic coefficients computed once for the whole fill operation via
    ///     <see cref="GradientEvaluator.CreatePlan"/>, not once per pixel or per row - see this
    ///     class's remarks) still produces results identical to per-pixel EvaluatePoint for a
    ///     <see cref="RadialGradient"/>, across a run wide enough to cross the start circle, the
    ///     midpoint, and the end circle. This is a regression/equivalence test for the
    ///     per-fill-precomputation refactor: it proves the refactor is a pure performance change
    ///     with no observable difference in pixel output.
    /// </summary>
    [Fact]
    public void EvaluateRow_Radial_MultiPixelRun_MatchesPerPixelEvaluatePoint()
    {
        var gradient = new RadialGradient(new Vector2(0, 0), 2f, new Vector2(30, 0), 8f, TwoStops());
        Span<Rgba32> destination = stackalloc Rgba32[40];

        var plan = GradientEvaluator.CreatePlan(gradient);
        GradientEvaluator.EvaluateRow(in plan, 0, -5, 40, destination);

        for (var i = 0; i < 40; i++)
        {
            var expected = GradientEvaluator.EvaluatePoint(gradient, new Vector2(-5 + i + 0.5f, 0.5f));
            Assert.Equal(expected, destination[i]);
        }
    }

    /// <summary>
    ///     Proves that a single <see cref="GradientEvaluator.GradientPlan"/> built once via
    ///     <see cref="GradientEvaluator.CreatePlan"/> and reused across multiple EvaluateRow calls
    ///     for different rows - exactly how <see cref="ScanlineRasterizer"/>'s gradient fill
    ///     overload uses it for a whole multi-row fill operation - produces results identical to
    ///     building (and discarding) a fresh plan for every row. This proves reusing one
    ///     per-fill-invariant plan across many rows is a pure performance change with no
    ///     observable difference in pixel output.
    /// </summary>
    [Fact]
    public void EvaluateRow_Radial_SharedPlanAcrossMultipleRows_MatchesPerRowFreshPlan()
    {
        var gradient = new RadialGradient(new Vector2(0, 0), 2f, new Vector2(30, 0), 8f, TwoStops());
        var sharedPlan = GradientEvaluator.CreatePlan(gradient);

        var sharedPlanRow = new Rgba32[40];
        var freshPlanRow = new Rgba32[40];

        for (var y = -10; y <= 10; y++)
        {
            GradientEvaluator.EvaluateRow(in sharedPlan, y, -5, 40, sharedPlanRow);

            var freshPlan = GradientEvaluator.CreatePlan(gradient);
            GradientEvaluator.EvaluateRow(in freshPlan, y, -5, 40, freshPlanRow);

            for (var i = 0; i < 40; i++)
            {
                Assert.Equal(freshPlanRow[i], sharedPlanRow[i]);
            }
        }
    }

    /// <summary>
    ///     Proves that EvaluateRow's whole-row "Degenerate Transform" fast path (skipping the
    ///     per-pixel loop entirely and flat-filling the whole row directly, since a singular
    ///     transform never varies across a row) still matches per-pixel EvaluatePoint results.
    /// </summary>
    [Fact]
    public void EvaluateRow_SingularTransform_MatchesPerPixelEvaluatePoint()
    {
        var singular = new Matrix3x2(1, 1, 1, 1, 0, 0);
        var gradient = new LinearGradient(new Vector2(0, 0), new Vector2(10, 0), TwoStops(), transform: singular);
        Span<Rgba32> destination = stackalloc Rgba32[4];

        var plan = GradientEvaluator.CreatePlan(gradient);
        GradientEvaluator.EvaluateRow(in plan, 0, 0, 4, destination);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(Blue, destination[i]);
        }
    }

    /// <summary>
    ///     Proves that CreatePlan throws ArgumentNullException when the gradient is null.
    /// </summary>
    [Fact]
    public void CreatePlan_NullGradient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GradientEvaluator.CreatePlan(null!));
    }
}
