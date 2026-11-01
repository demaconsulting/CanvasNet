using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Resolves chart paints (a CSS color or no color, plus an opacity) to <see cref="Rgba32"/> values.
/// </summary>
/// <remarks>
///     <para>
///         Charts spell colors the CSS way (the Vega-Lite defaults are <c>#4c78a8</c> and <c>#ddd</c>); the color
///         grammar itself is the shared <see cref="CssColor"/> of the core library. This unit adds only the paint
///         rules of a chart on top of it.
///     </para>
///     <para>
///         A paint is a color or no color: <see langword="null"/>, an empty string and <c>none</c> mean "do not draw"
///         (<see cref="IsNone"/>). <see cref="TryResolvePaint"/> applies that rule and an opacity in one step; a text
///         that is neither a color nor none is treated as no paint as well, which is what a canvas does with a color it
///         cannot parse. The methods are pure functions and thread-safe.
///     </para>
/// </remarks>
internal static class CssPaint
{
    /// <summary>
    ///     Determines whether a paint means "do not draw": null, empty, white space or <c>none</c>.
    /// </summary>
    /// <param name="text">The paint.</param>
    /// <returns><see langword="true"/> for no paint.</returns>
    public static bool IsNone(string? text) =>
        string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Resolves a paint and an opacity into the color to draw with.
    /// </summary>
    /// <param name="paint">The paint: a CSS color, <c>none</c> or null.</param>
    /// <param name="opacity">The opacity that multiplies the alpha of the color, clamped to 0 to 1.</param>
    /// <param name="color">The color to draw with when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when there is nothing to draw: no paint, an unparseable paint, or a fully transparent result.</returns>
    public static bool TryResolvePaint(string? paint, double opacity, out Rgba32 color)
    {
        if (IsNone(paint) || !CssColor.TryParse(paint, out color))
        {
            color = default;
            return false;
        }

        color = WithOpacity(color, opacity);
        return color.A > 0;
    }

    /// <summary>
    ///     Multiplies the alpha of a color by an opacity.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="opacity">The opacity; clamped to 0 to 1, and not a number counts as 1.</param>
    /// <returns>The color with the new alpha, rounded half away from zero.</returns>
    public static Rgba32 WithOpacity(Rgba32 color, double opacity)
    {
        var factor = double.IsNaN(opacity) ? 1 : Math.Clamp(opacity, 0, 1);
        if (factor >= 1)
        {
            return color;
        }

        var alpha = Math.Clamp(Math.Round(color.A * factor, MidpointRounding.AwayFromZero), 0, 255);
        return new Rgba32(color.R, color.G, color.B, (byte)alpha);
    }
}
