using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

// cspell:ignore precomputation
namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Evaluates a <see cref="TilePaint"/>'s repeating tile bitmap at individual device-space
///     points, mirroring <see cref="GradientEvaluator"/>'s own "per-fill precomputation, per-row
///     evaluation" shape.
/// </summary>
/// <remarks>
///     <para>
///     <b>Per-fill precomputation.</b> Exactly like <see cref="GradientEvaluator"/>, the only
///     quantity that is invariant across an entire fill operation - the inverse of
///     <see cref="TilePaint.Transform"/> - is computed exactly once, via <see cref="CreatePlan"/>,
///     and reused for every row via <see cref="EvaluateRow"/>, instead of being recomputed per
///     pixel.
///     </para>
///     <para>
///     <b>Degenerate Transform policy - deliberate divergence from <see cref="Gradient"/>.</b>
///     When <see cref="TilePaint.Transform"/> is not invertible (or its inverse has a non-finite
///     component), every pixel in the affected fill is left fully transparent (alpha zero,
///     <c>default</c>(<see cref="Rgba32"/>)) - this is NOT the same policy <see cref="Gradient"/>
///     uses for its own "Degenerate Transform" case (which flat-fills with the last stop's
///     color - see <see cref="GradientEvaluator"/>'s remarks). <see cref="TilePaint"/> has no
///     stop/last-color concept at all, so there is no equivalent fallback color to flat-fill
///     with. Instead, this mirrors <see cref="GradientEvaluator"/>'s own <i>separate</i>
///     "no valid root" radial-gradient case, which also leaves affected pixels fully transparent/
///     unpainted. This is an intentional, documented design decision - not an oversight - so a
///     future reader is not confused by the difference from <see cref="Gradient"/>'s own
///     degenerate-transform behavior.
///     </para>
///     <para>
///     <b>Sampling.</b> A device-space point is mapped back into pattern space via the
///     precomputed inverse transform, then its X/Y coordinates are each reduced modulo
///     <see cref="TilePaint.XStep"/>/<see cref="TilePaint.YStep"/> using floor-mod (never the CLR
///     <c>%</c> operator, which is negative for a negative dividend and would wrap a negative
///     coordinate - or a negative step - to the wrong side of the tile, matching
///     <see cref="GradientEvaluator"/>'s own documented <c>Repeat</c> floor-mod precedent) into
///     the half-open range <c>[0, |XStep|) x [0, |YStep|)</c>. That wrapped point is then mapped
///     into the tile <see cref="Surface"/>'s own pixel coordinate space by the ratio of the tile
///     surface's pixel dimensions to <see cref="TilePaint.XStep"/>/<see cref="TilePaint.YStep"/>,
///     clamped into <c>[0, Width - 1] x [0, Height - 1]</c> (guarding a floating-point edge case
///     landing exactly on the width/height boundary), and sampled via nearest-neighbor
///     (<c>tile.Surface[tx, ty]</c>) - bilinear interpolation is explicitly out of scope.
///     </para>
/// </remarks>
internal static class TilePaintEvaluator
{
    /// <summary>
    ///     Holds every quantity <see cref="EvaluateRow"/> can compute once per
    ///     <see cref="TilePaint"/> and reuse for every point evaluated against it, instead of
    ///     recomputing per pixel - see this class's "Per-fill precomputation" remarks.
    /// </summary>
    internal readonly record struct TilePaintPlan
    {
        /// <summary>The tile paint this plan was built for.</summary>
        public required TilePaint Tile { get; init; }

        /// <summary>
        ///     Whether <see cref="TilePaint.Transform"/> is invertible with a finite inverse;
        ///     when <see langword="false"/>, every point in the fill is left fully transparent
        ///     (the "Degenerate Transform" case) and <see cref="InverseTransform"/> is not
        ///     meaningful.
        /// </summary>
        public required bool HasInverseTransform { get; init; }

        /// <summary>The inverse of <see cref="TilePaint.Transform"/>, when <see cref="HasInverseTransform"/>.</summary>
        public Matrix3x2 InverseTransform { get; init; }
    }

    /// <summary>
    ///     Computes every per-<paramref name="tile"/> (never per-point) quantity once, for reuse
    ///     across an entire fill operation (every row of a <see cref="ScanlineRasterizer"/> tile
    ///     fill) via <see cref="EvaluateRow"/>.
    /// </summary>
    /// <param name="tile">The tile paint to build a plan for. Must not be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tile"/> is <see langword="null"/>.</exception>
    internal static TilePaintPlan CreatePlan(TilePaint tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var hasInverse = Matrix3x2.Invert(tile.Transform, out var inverse) && IsFinite(inverse);

        return new TilePaintPlan
        {
            Tile = tile,
            HasInverseTransform = hasInverse,
            InverseTransform = inverse,
        };
    }

    /// <summary>
    ///     Evaluates <paramref name="plan"/>'s tile paint once per pixel center of a horizontal
    ///     run of <paramref name="count"/> pixels starting at column <paramref name="x"/> on row
    ///     <paramref name="y"/>, writing each pixel's color into <paramref name="destination"/>.
    /// </summary>
    /// <param name="plan">
    ///     The tile paint's precomputed per-fill-invariant plan, built once per fill operation via
    ///     <see cref="CreatePlan"/> and reused across every row.
    /// </param>
    /// <param name="y">The zero-based row, in the same coordinate space <paramref name="plan"/>'s tile's <see cref="TilePaint.Transform"/> maps into.</param>
    /// <param name="x">The zero-based column at which the run starts.</param>
    /// <param name="count">The number of pixels to evaluate.</param>
    /// <param name="destination">
    ///     The destination span to receive each pixel's color, one entry per pixel. Must have a
    ///     length of at least <paramref name="count"/>.
    /// </param>
    internal static void EvaluateRow(in TilePaintPlan plan, int y, int x, int count, Span<Rgba32> destination)
    {
        if (!plan.HasInverseTransform)
        {
            // Degenerate Transform case applies to the whole fill, not just this row - resolve
            // it once for the entire run instead of per pixel. See this class's remarks for why
            // this leaves pixels transparent rather than flat-filling, unlike Gradient.
            for (var i = 0; i < count; i++)
            {
                destination[i] = default;
            }

            return;
        }

        var tile = plan.Tile;
        var surface = tile.Surface;
        var xStep = Math.Abs((double)tile.XStep);
        var yStep = Math.Abs((double)tile.YStep);
        var scaleX = surface.Width / xStep;
        var scaleY = surface.Height / yStep;

        for (var i = 0; i < count; i++)
        {
            var devicePoint = new Vector2(x + i + 0.5f, y + 0.5f);
            var patternPoint = Vector2.Transform(devicePoint, plan.InverseTransform);

            var wrappedX = FloorMod(patternPoint.X, xStep);
            var wrappedY = FloorMod(patternPoint.Y, yStep);

            var tx = Math.Clamp((int)(wrappedX * scaleX), 0, surface.Width - 1);
            var ty = Math.Clamp((int)(wrappedY * scaleY), 0, surface.Height - 1);

            destination[i] = surface[tx, ty];
        }
    }

    /// <summary>
    ///     Reduces <paramref name="value"/> modulo <paramref name="modulus"/> using floor-mod
    ///     (always returning a non-negative result in <c>[0, modulus)</c>), rather than the CLR
    ///     <c>%</c> operator - matching <see cref="GradientEvaluator"/>'s own documented
    ///     <c>Repeat</c> floor-mod precedent.
    /// </summary>
    /// <param name="value">The value to reduce. May be negative.</param>
    /// <param name="modulus">The modulus; must already be positive (the absolute step value).</param>
    private static double FloorMod(double value, double modulus) =>
        value - (Math.Floor(value / modulus) * modulus);

    /// <summary>
    ///     Determines whether every component of <paramref name="matrix"/> is a finite value.
    /// </summary>
    private static bool IsFinite(Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
        float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
        float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);
}
