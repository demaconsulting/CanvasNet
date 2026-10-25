// cspell:ignore rrggbbaa
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds the <c>color</c> module and parses its color values.
/// </summary>
/// <remarks>
///     <para>
///         <b>Forms.</b> <c>color(c, alpha)</c> takes <c>c</c> as a name from the CSS named-color table
///         (<see cref="ScadColorTable"/>, case-insensitive), a hexadecimal string <c>"#rgb"</c>, <c>"#rgba"</c>,
///         <c>"#rrggbb"</c> or <c>"#rrggbbaa"</c>, or a vector <c>[r, g, b]</c> or <c>[r, g, b, a]</c> of numbers
///         from 0 to 1 (values outside are clamped); the <c>alpha</c> argument overrides any alpha in <c>c</c>.
///         Channels are rounded to the nearest of 256 levels.
///     </para>
///     <para>
///         <b>Dialect.</b> The color applies to the implicit union of the children with the <i>innermost wins</i>
///         rule of the renderer: a nested <c>color</c> overrides an outer one. An unrecognized value is a warning
///         and leaves the children uncolored by this module; <c>color()</c> without <c>c</c> changes nothing. An
///         alpha below 1 is not rendered: the surface is drawn opaque and the degradation is reported as the
///         unsupported feature <c>scad-color-alpha</c> (transparency is planned for a later release).
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
            }
            else
            {
                context.Reporter.Warn("color: alpha must be a number from 0 to 1; it is ignored", instance.Location);
            }
        }

        if (alpha < 1.0)
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.ColorAlpha,
                "Transparency is not supported yet; a color with alpha below 1 is drawn opaque",
                instance.Location);
        }

        var union = ScadBooleans.Union(instance, results, context);
        if (union.Kind == ScadGeoKind.Empty || color is null)
        {
            return union;
        }

        var appearance = new NodeAppearance(color.Value);
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
    private static bool TryParseString(string text, out Rgba32 color, out double alpha)
    {
        color = default;
        alpha = 1.0;
        if (text.StartsWith('#'))
        {
            return TryParseHex(text.AsSpan(1), out color, out alpha);
        }

        if (!ScadColorTable.Colors.TryGetValue(text, out var named))
        {
            return false;
        }

        color = new Rgba32(named.R, named.G, named.B, 255);
        alpha = named.A / 255.0;
        return true;
    }

    /// <summary>
    ///     Parses the digits of a hexadecimal color (three, four, six or eight digits).
    /// </summary>
    /// <param name="digits">The digits after the <c>#</c>.</param>
    /// <param name="color">The opaque color when valid.</param>
    /// <param name="alpha">The alpha from 0 to 1 when valid.</param>
    /// <returns><see langword="true"/> when the digits are a valid color.</returns>
    private static bool TryParseHex(ReadOnlySpan<char> digits, out Rgba32 color, out double alpha)
    {
        color = default;
        alpha = 1.0;
        if (digits.Length is not (3 or 4 or 6 or 8))
        {
            return false;
        }

        // Short forms repeat each digit; the long forms use two digits per channel
        var width = digits.Length <= 4 ? 1 : 2;
        var channels = digits.Length / width;
        Span<int> values = stackalloc int[4];
        values[3] = 255;
        for (var i = 0; i < channels; i++)
        {
            // AllowHexSpecifier accepts hexadecimal digits only (no sign, no white space)
            if (!int.TryParse(digits.Slice(i * width, width), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            values[i] = width == 1 ? parsed * 17 : parsed;
        }

        color = new Rgba32((byte)values[0], (byte)values[1], (byte)values[2], 255);
        alpha = values[3] / 255.0;
        return true;
    }

    /// <summary>
    ///     Converts a channel from 0 to 1 to a byte, rounding to the nearest level.
    /// </summary>
    /// <param name="channel">The channel from 0 to 1.</param>
    /// <returns>The byte.</returns>
    private static byte ToByte(double channel) => (byte)Math.Round(channel * 255.0, MidpointRounding.AwayFromZero);
}
