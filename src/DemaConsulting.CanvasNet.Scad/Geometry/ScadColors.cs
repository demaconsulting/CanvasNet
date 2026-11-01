// cspell:ignore rrggbbaa
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds the <c>color</c> module and parses its color values.
/// </summary>
/// <remarks>
///     <para>
///         <b>Forms.</b> <c>color(c, alpha)</c> takes <c>c</c> as a name from the CSS named-color table of the core
///         library (<see cref="CssColor.TryGetNamedColor"/>, case-insensitive), a hexadecimal string <c>"#rgb"</c>,
///         <c>"#rgba"</c>, <c>"#rrggbb"</c> or <c>"#rrggbbaa"</c> (<see cref="CssColor.TryParseHex"/>), or a vector
///         <c>[r, g, b]</c> or <c>[r, g, b, a]</c> of numbers
///         from 0 to 1 (values outside are clamped); the <c>alpha</c> argument overrides any alpha in <c>c</c>.
///         Channels are rounded to the nearest of 256 levels.
///     </para>
///     <para>
///         <b>Dialect.</b> The color applies to the implicit union of the children with the <i>innermost wins</i>
///         rule of the renderer: a nested <c>color</c> overrides an outer one. An unrecognized value is a warning
///         and leaves the children uncolored by this module; <c>color()</c> without <c>c</c> changes nothing.
///     </para>
///     <para>
///         <b>Alpha.</b> The alpha (from <c>alpha</c>, from the fourth vector component, from the last hex pair or
///         from <c>transparent</c>) becomes the alpha channel of the node's <see cref="NodeAppearance"/>
///         (<c>round(alpha * 255)</c>), and the renderer composites translucent surfaces (see
///         <see cref="NodeAppearance"/>); an alpha of 0 is a surface that is invisible but still solid. As with the
///         color, the innermost <c>color</c> wins, alpha included. An <c>alpha</c> without <c>c</c> has nothing to
///         apply to: it is warned about and ignored. Nothing is reported as unsupported.
///     </para>
///     <para>
///         The methods are stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ScadColors
{
    /// <summary>The parameter names of <c>color</c> in positional order.</summary>
    private static readonly string[] ColorParameters = ["c", "alpha"];

    /// <summary>
    ///     Builds <c>color(c, alpha)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The colored union of the children.</returns>
    public static ScadGeo Color(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, ColorParameters, context);
        Rgba32? color = null;
        double? alpha = null;
        if (args.TryGet("c", out var colorValue))
        {
            if (TryParse(colorValue, out var parsed, out var parsedAlpha))
            {
                color = parsed;
                alpha = parsedAlpha;
            }
            else
            {
                context.Reporter.Warn("color: c must be a color name, a hex string such as \"#ff8800\" or a vector [r, g, b] or [r, g, b, a] of numbers from 0 to 1; the color is ignored", instance.Location);
            }
        }

        if (args.TryGet("alpha", out var alphaValue))
        {
            if (ScadArguments.TryNumber(alphaValue, out var number))
            {
                alpha = Math.Clamp(number, 0.0, 1.0);

                // An alpha has nothing to apply to when no color was given at all
                if (!args.TryGet("c", out _))
                {
                    context.Reporter.Warn("color: alpha needs a color c; it is ignored", instance.Location);
                }
            }
            else
            {
                context.Reporter.Warn("color: alpha must be a number from 0 to 1; it is ignored", instance.Location);
            }
        }

        var union = ScadBooleans.Union(instance, results, context);
        if (union.Kind == ScadGeoKind.Empty || color is null)
        {
            return union;
        }

        var opaque = color.Value;
        var appearance = new NodeAppearance(new Rgba32(opaque.R, opaque.G, opaque.B, ToByte(alpha ?? 1.0)));
        if (union.Kind == ScadGeoKind.Solid)
        {
            return ScadGeo.FromSolid(new GroupNode([union.Node!]) { Appearance = appearance }, union.Bounds, union.IsTight, union.IsApproximate, union.Reach);
        }

        return ScadGeo.FromShape(ScadShape2D.CreateColor(union.Shape!, appearance), union.Bounds, union.IsTight, union.IsApproximate, union.FlatLocation, union.Reach);
    }

    /// <summary>
    ///     Parses a color value.
    /// </summary>
    /// <param name="value">The value: a string or a vector of three or four numbers.</param>
    /// <param name="color">The opaque color when the value is valid (its alpha channel is always 255).</param>
    /// <param name="alpha">The alpha from 0 to 1 when the value is valid (1 when the form has none).</param>
    /// <returns><see langword="true"/> when the value is a valid color.</returns>
    public static bool TryParse(ScadValue value, out Rgba32 color, out double alpha)
    {
        color = default;
        alpha = 1.0;
        if (value.Kind == ScadValueKind.String)
        {
            return TryParseString(value.AsString, out color, out alpha);
        }

        if (value.Kind != ScadValueKind.Vector || value.AsVector.Count is not (3 or 4))
        {
            return false;
        }

        var channels = new double[4] { 0.0, 0.0, 0.0, 1.0 };
        for (var i = 0; i < value.AsVector.Count; i++)
        {
            if (!ScadArguments.TryNumber(value.AsVector[i], out var number))
            {
                return false;
            }

            channels[i] = Math.Clamp(number, 0.0, 1.0);
        }

        color = new Rgba32(ToByte(channels[0]), ToByte(channels[1]), ToByte(channels[2]), 255);
        alpha = channels[3];
        return true;
    }

    /// <summary>
    ///     Parses a color name or a hexadecimal color string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="color">The opaque color when valid.</param>
    /// <param name="alpha">The alpha from 0 to 1 when valid.</param>
    /// <returns><see langword="true"/> when the text is a known name or a valid hexadecimal color.</returns>
    /// <remarks>
    ///     The grammar is deliberately narrower than <see cref="CssColor.TryParse"/>: only the shared name table and
    ///     hexadecimal digits are accepted, so a string such as <c>"rgb(255, 0, 0)"</c> is not a color here.
    /// </remarks>
    private static bool TryParseString(string text, out Rgba32 color, out double alpha)
    {
        color = default;
        alpha = 1.0;
        var found = text.StartsWith('#')
            ? CssColor.TryParseHex(text.AsSpan(1), out var parsed)
            : CssColor.TryGetNamedColor(text, out parsed);
        if (!found)
        {
            return false;
        }

        color = new Rgba32(parsed.R, parsed.G, parsed.B, 255);
        alpha = parsed.A / 255.0;
        return true;
    }

    /// <summary>
    ///     Converts a channel from 0 to 1 to a byte, rounding to the nearest level.
    /// </summary>
    /// <param name="channel">The channel from 0 to 1.</param>
    /// <returns>The byte.</returns>
    private static byte ToByte(double channel) => (byte)Math.Round(channel * 255.0, MidpointRounding.AwayFromZero);
}
