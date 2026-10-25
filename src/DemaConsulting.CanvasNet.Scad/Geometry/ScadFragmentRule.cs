namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Resolves the number of fragments (segments of a circle) from the special variables <c>$fn</c>, <c>$fa</c>
///     and <c>$fs</c> and a radius.
/// </summary>
/// <remarks>
///     <para>
///         The rule is the public, documented formula of the language: a radius below
///         <see cref="MinimumRadius"/> gives <see cref="MinimumFragments"/>; a positive <c>$fn</c> gives
///         <c>max($fn, 3)</c> (truncated to a whole number); otherwise the count is
///         <c>ceil(max(min(360 / $fa, r * 2 pi / $fs), 5))</c>. Two safety rules are added: <c>$fa</c> and
///         <c>$fs</c> are clamped to at least <see cref="MinimumStep"/> (a zero or negative step would otherwise ask
///         for unbounded detail) and a non-finite value falls back to its default, and the result never exceeds
///         <see cref="MaximumFragments"/> (the real cost cap is <see cref="ScadLimits.MaxTriangles"/>).
///     </para>
///     <para>
///         Examples with the defaults <c>$fa = 12</c>, <c>$fs = 2</c>: radius 1 gives 5, radius 10 gives 30;
///         <c>$fn = 1</c> gives 3 and <c>$fn = 64</c> gives 64. The rule is pure, deterministic and thread-safe.
///     </para>
/// </remarks>
internal static class ScadFragmentRule
{
    /// <summary>The fewest fragments any circle has.</summary>
    public const int MinimumFragments = 3;

    /// <summary>The most fragments the rule returns (an overflow guard far above any usable value).</summary>
    public const int MaximumFragments = 1 << 20;

    /// <summary>The radius at or below which a circle degenerates to <see cref="MinimumFragments"/> fragments.</summary>
    public const double MinimumRadius = 1e-8;

    /// <summary>The smallest accepted <c>$fa</c> and <c>$fs</c>.</summary>
    public const double MinimumStep = 0.01;

    /// <summary>The default <c>$fa</c> used when the value is not finite.</summary>
    private const double DefaultFa = 12.0;

    /// <summary>The default <c>$fs</c> used when the value is not finite.</summary>
    private const double DefaultFs = 2.0;

    /// <summary>The lower bound of the angle/size derived count before rounding up.</summary>
    private const double AutomaticFloor = 5.0;

    /// <summary>
    ///     Computes the number of fragments of a circle of a given radius.
    /// </summary>
    /// <param name="radius">The radius; a non-finite value or one at or below <see cref="MinimumRadius"/> gives 3.</param>
    /// <param name="fn">The <c>$fn</c> value; a positive value fixes the count (truncated, at least 3).</param>
    /// <param name="fa">The <c>$fa</c> value, the largest angle in degrees of one fragment.</param>
    /// <param name="fs">The <c>$fs</c> value, the largest length of one fragment.</param>
    /// <returns>The fragment count, from <see cref="MinimumFragments"/> to <see cref="MaximumFragments"/>.</returns>
    public static int Fragments(double radius, double fn, double fa, double fs)
    {
        // A vanishing or meaningless radius is a triangle, whatever the settings
        if (!double.IsFinite(radius) || radius <= MinimumRadius)
        {
            return MinimumFragments;
        }

        // An explicit $fn wins; NaN is neither positive nor negative and falls through to the automatic rule
        if (fn > 0.0)
        {
            return (int)Math.Clamp(Math.Truncate(fn), MinimumFragments, MaximumFragments);
        }

        // Automatic: the angle limit, the size limit, at least five, rounded up
        var angle = double.IsFinite(fa) ? Math.Max(fa, MinimumStep) : DefaultFa;
        var size = double.IsFinite(fs) ? Math.Max(fs, MinimumStep) : DefaultFs;
        var count = Math.Ceiling(Math.Max(Math.Min(360.0 / angle, radius * 2.0 * Math.PI / size), AutomaticFloor));
        return (int)Math.Clamp(count, MinimumFragments, MaximumFragments);
    }

    /// <summary>
    ///     Computes the number of fragments of a circle of a given radius under the fragment settings of an instance.
    /// </summary>
    /// <param name="radius">The radius.</param>
    /// <param name="settings">The <c>$fn</c>, <c>$fa</c> and <c>$fs</c> in effect at the instance.</param>
    /// <returns>The fragment count.</returns>
    public static int Fragments(double radius, Evaluation.ScadFragmentSettings settings) =>
        Fragments(radius, settings.Fn, settings.Fa, settings.Fs);
}
