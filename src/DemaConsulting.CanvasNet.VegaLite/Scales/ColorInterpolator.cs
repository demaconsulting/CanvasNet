// Portions derived from d3-interpolate and d3-color (ISC, Mike Bostock) and Vega (BSD-3-Clause); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     The color spaces in which two colors can be interpolated (the <c>interpolate</c> types of a Vega-Lite scale).
/// </summary>
internal enum InterpolationSpace
{
    /// <summary>Linear interpolation of the sRGB channels (the default of Vega); the only space that has a gamma.</summary>
    Rgb,

    /// <summary>Hue, saturation and lightness; the hue takes the shorter way around the circle.</summary>
    Hsl,

    /// <summary>Hue, saturation and lightness; the hue takes the way that passes through the hues in between (<c>hsl-long</c>).</summary>
    HslLong,

    /// <summary>CIELAB (D50) lightness and the two opponent axes.</summary>
    Lab,

    /// <summary>The polar form of CIELAB (hue, chroma, lightness); the hue takes the shorter way around the circle.</summary>
    Hcl,

    /// <summary>The polar form of CIELAB; the hue takes the way that passes through the hues in between (<c>hcl-long</c>).</summary>
    HclLong,
}

/// <summary>
///     A color as double-precision sRGB channels (0 to 255) and an opacity (0 to 1).
/// </summary>
/// <remarks>
///     Values are kept unrounded while colors are being interpolated; the interpolation results of
///     <see cref="ColorRamp"/> are integral channels, which is what d3 returns as a CSS color. Equality compares the
///     four numbers.
/// </remarks>
/// <param name="R">The red channel, 0 to 255.</param>
/// <param name="G">The green channel, 0 to 255.</param>
/// <param name="B">The blue channel, 0 to 255.</param>
/// <param name="Opacity">The opacity, 0 (transparent) to 1 (opaque).</param>
internal readonly record struct RgbColor(double R, double G, double B, double Opacity)
{
    /// <summary>
    ///     Creates a color from 8-bit channels.
    /// </summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    /// <param name="a">The alpha channel; 255 (opaque) by default.</param>
    /// <returns>The color, with the opacity <c>a / 255</c>.</returns>
    public static RgbColor FromBytes(byte r, byte g, byte b, byte a = 255) => new(r, g, b, a / 255d);

    /// <summary>
    ///     Creates a color from a core pixel.
    /// </summary>
    /// <param name="color">The pixel (straight, not premultiplied, alpha).</param>
    /// <returns>The color.</returns>
    public static RgbColor FromRgba32(Rgba32 color) => FromBytes(color.R, color.G, color.B, color.A);

    /// <summary>
    ///     Reads a six digit hexadecimal color, with or without a leading <c>#</c>.
    /// </summary>
    /// <param name="text">The text, <c>rrggbb</c> or <c>#rrggbb</c> (either letter case).</param>
    /// <param name="color">Receives the opaque color when the text is valid.</param>
    /// <returns><see langword="true"/> when the text is exactly six hexadecimal digits.</returns>
    public static bool TryParseHex(ReadOnlySpan<char> text, out RgbColor color)
    {
        color = default;
        if (text.Length > 0 && text[0] == '#')
        {
            text = text[1..];
        }

        if (text.Length != 6
            || !byte.TryParse(text[..2], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(text[2..4], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(text[4..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = FromBytes(r, g, b);
        return true;
    }

    /// <summary>
    ///     Converts the color to a core pixel, rounding each channel half away from zero and clamping to 0 to 255.
    /// </summary>
    /// <returns>The pixel.</returns>
    public Rgba32 ToRgba32() => new(ToByte(R), ToByte(G), ToByte(B), ToByte(Opacity * 255));

    /// <summary>
    ///     Formats the color as a lower-case hexadecimal CSS color: <c>#rrggbb</c>, or <c>#rrggbbaa</c> when it is not opaque.
    /// </summary>
    /// <returns>The CSS text.</returns>
    public string ToCss()
    {
        var pixel = ToRgba32();
        return pixel.A == 255
            ? string.Create(CultureInfo.InvariantCulture, $"#{pixel.R:x2}{pixel.G:x2}{pixel.B:x2}")
            : string.Create(CultureInfo.InvariantCulture, $"#{pixel.R:x2}{pixel.G:x2}{pixel.B:x2}{pixel.A:x2}");
    }

    /// <summary>
    ///     Rounds a channel value to a byte; NaN is 0.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rounded and clamped value.</returns>
    private static byte ToByte(double value) =>
        double.IsNaN(value) ? (byte)0 : (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
}

/// <summary>
///     The interpolation of colors in sRGB, HSL, CIELAB and HCL, and the helpers that turn an interpolator into a
///     list of colors.
/// </summary>
/// <remarks>
///     <para>
///         The functions are C# versions of the public algorithms of d3-interpolate (<c>interpolateRgb</c>,
///         <c>interpolateHsl</c>, <c>interpolateHslLong</c>, <c>interpolateLab</c>, <c>interpolateHcl</c>,
///         <c>interpolateHclLong</c>, <c>piecewise</c>) and d3-color (the sRGB, HSL, CIELAB and HCL conversions with
///         the D50 white point), and of the Vega helpers <c>interpolateRange</c> and <c>quantizeInterpolator</c>.
///         The conventions are d3's: a hue that is not a number (a grey) takes the hue of the other color; the
///         shorter way round the hue circle is used unless the long variant is asked for; rgb is the only space with
///         a gamma; and the opacity is interpolated linearly in every space.
///     </para>
///     <para>
///         The result of an interpolation is rounded to whole 8-bit channels (half away from zero, after clamping to
///         0 to 255), which is the rounding of d3's CSS color text, so equal inputs give equal colors on every
///         platform. The arithmetic of the sRGB space is exact (<c>+ - * /</c> only); HSL, CIELAB and HCL use
///         <see cref="Math.Pow(double, double)"/>, <see cref="Math.Atan2(double, double)"/>,
///         <see cref="Math.Sin(double)"/> and <see cref="Math.Cos(double)"/>, whose last bit can differ between
///         platforms, so a result can differ by one in a channel between machines; the scheme tables and the default
///         space never depend on that. The type is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ColorInterpolator
{
    /// <summary>The number of colors between two checks for cancellation of a long list.</summary>
    private const int CancellationInterval = 4096;

    /// <summary>The CIELAB constant 4 / 29.</summary>
    private const double T0 = 4d / 29;

    /// <summary>The CIELAB constant 6 / 29.</summary>
    private const double T1 = 6d / 29;

    /// <summary>The CIELAB constant 3 * (6 / 29)^2.</summary>
    private const double T2 = 3 * T1 * T1;

    /// <summary>The CIELAB constant (6 / 29)^3.</summary>
    private const double T3 = T1 * T1 * T1;

    /// <summary>The x component of the D50 white point.</summary>
    private const double Xn = 0.96422;

    /// <summary>The y component of the D50 white point.</summary>
    private const double Yn = 1;

    /// <summary>The z component of the D50 white point.</summary>
    private const double Zn = 0.82521;

    /// <summary>The factor from degrees to radians.</summary>
    private const double Radians = Math.PI / 180;

    /// <summary>The factor from radians to degrees.</summary>
    private const double Degrees = 180 / Math.PI;

    /// <summary>
    ///     Looks up an interpolation space by the name that Vega-Lite uses in <c>scale.interpolate</c>.
    /// </summary>
    /// <param name="name">One of <c>rgb</c>, <c>hsl</c>, <c>hsl-long</c>, <c>lab</c>, <c>hcl</c> and <c>hcl-long</c>.</param>
    /// <param name="space">Receives the space.</param>
    /// <returns><see langword="false"/> for any other name, including the unsupported <c>cubehelix</c> and <c>cubehelix-long</c>.</returns>
    public static bool TryParseSpace(string? name, out InterpolationSpace space)
    {
        switch (name)
        {
            case "rgb":
                space = InterpolationSpace.Rgb;
                return true;
            case "hsl":
                space = InterpolationSpace.Hsl;
                return true;
            case "hsl-long":
                space = InterpolationSpace.HslLong;
                return true;
            case "lab":
                space = InterpolationSpace.Lab;
                return true;
            case "hcl":
                space = InterpolationSpace.Hcl;
                return true;
            case "hcl-long":
                space = InterpolationSpace.HclLong;
                return true;
            default:
                space = default;
                return false;
        }
    }

    /// <summary>
    ///     Interpolates between two colors.
    /// </summary>
    /// <param name="space">The space to interpolate in.</param>
    /// <param name="start">The color at <paramref name="t"/> = 0.</param>
    /// <param name="end">The color at <paramref name="t"/> = 1.</param>
    /// <param name="t">The position; values outside 0 to 1 extrapolate (the channels are clamped afterwards).</param>
    /// <param name="gamma">The gamma of the sRGB space (1 is linear); ignored by the other spaces.</param>
    /// <returns>The color with whole 8-bit channels.</returns>
    public static RgbColor Interpolate(InterpolationSpace space, RgbColor start, RgbColor end, double t, double gamma = 1) =>
        FromSpace(space, Mix(space, ToSpace(space, start), ToSpace(space, end), t, space == InterpolationSpace.Rgb && !double.IsNaN(gamma) ? gamma : 1));

    /// <summary>
    ///     Interpolates through a list of colors, as d3's <c>piecewise</c> does.
    /// </summary>
    /// <param name="space">The space to interpolate in.</param>
    /// <param name="stops">The colors, at least one; they are spaced evenly over 0 to 1.</param>
    /// <param name="t">The position; NaN counts as 0 and an infinite value as the nearer end.</param>
    /// <param name="gamma">The gamma of the sRGB space.</param>
    /// <returns>The color.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stops"/> is empty.</exception>
    public static RgbColor Piecewise(InterpolationSpace space, IReadOnlyList<RgbColor> stops, double t, double gamma = 1) =>
        new ColorRamp(space, stops, gamma).At(t);

    /// <summary>
    ///     Restricts an interpolator to a sub-range of its input (Vega's <c>interpolateRange</c>).
    /// </summary>
    /// <param name="interpolator">The interpolator over 0 to 1.</param>
    /// <param name="start">The input that the result maps 0 to.</param>
    /// <param name="end">The input that the result maps 1 to; smaller than <paramref name="start"/> reverses the colors.</param>
    /// <returns>The interpolator <c>t =&gt; interpolator(start + t * (end - start))</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interpolator"/> is null.</exception>
    public static Func<double, RgbColor> Range(Func<double, RgbColor> interpolator, double start, double end)
    {
        ArgumentNullException.ThrowIfNull(interpolator);
        var span = end - start;
        return t => interpolator(start + t * span);
    }

    /// <summary>
    ///     Samples an interpolator at <c>i / (count + 1)</c> for <c>i = 1 .. count</c> (Vega's <c>quantizeInterpolator</c>);
    ///     the end points 0 and 1 are not used.
    /// </summary>
    /// <param name="interpolator">The interpolator over 0 to 1.</param>
    /// <param name="count">The number of colors; 0 gives an empty list.</param>
    /// <param name="cancellationToken">A token that stops a long list; it is checked every 4,096 colors.</param>
    /// <returns>The colors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interpolator"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    public static RgbColor[] Quantize(Func<double, RgbColor> interpolator, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interpolator);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var samples = new RgbColor[count];
        var n = count + 1d;
        for (var i = 0; i < count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            samples[i] = interpolator((i + 1) / n);
        }

        return samples;
    }

    /// <summary>
    ///     Interpolates the four channels of two converted colors.
    /// </summary>
    /// <param name="space">The space the channels are in.</param>
    /// <param name="start">The channels at 0.</param>
    /// <param name="end">The channels at 1.</param>
    /// <param name="t">The position.</param>
    /// <param name="gamma">The sRGB gamma.</param>
    /// <returns>The channels at <paramref name="t"/>.</returns>
    internal static Channels Mix(InterpolationSpace space, Channels start, Channels end, double t, double gamma)
    {
        var opacity = Linear(start.O, end.O, t);
        switch (space)
        {
            case InterpolationSpace.Rgb:
                return new Channels(
                    Gamma(start.A, end.A, t, gamma),
                    Gamma(start.B, end.B, t, gamma),
                    Gamma(start.C, end.C, t, gamma),
                    opacity);
            case InterpolationSpace.Hsl:
            case InterpolationSpace.Hcl:
                return new Channels(Hue(start.A, end.A, t), Linear(start.B, end.B, t), Linear(start.C, end.C, t), opacity);
            default:
                // Lab, and the long forms of HSL and HCL, interpolate every channel (the hue too) linearly
                return new Channels(Linear(start.A, end.A, t), Linear(start.B, end.B, t), Linear(start.C, end.C, t), opacity);
        }
    }

    /// <summary>
    ///     Converts a color into the channels of a space.
    /// </summary>
    /// <param name="space">The space.</param>
    /// <param name="color">The color.</param>
    /// <returns>The channels: red, green, blue; hue, saturation, lightness; lightness, a, b; or hue, chroma, lightness.</returns>
    internal static Channels ToSpace(InterpolationSpace space, RgbColor color)
    {
        switch (space)
        {
            case InterpolationSpace.Hsl:
            case InterpolationSpace.HslLong:
                return RgbToHsl(color);
            case InterpolationSpace.Lab:
                return RgbToLab(color);
            case InterpolationSpace.Hcl:
            case InterpolationSpace.HclLong:
                return LabToHcl(RgbToLab(color));
            default:
                return new Channels(color.R, color.G, color.B, color.Opacity);
        }
    }

    /// <summary>
    ///     Converts channels of a space back to a color with whole 8-bit channels.
    /// </summary>
    /// <param name="space">The space.</param>
    /// <param name="channels">The channels.</param>
    /// <returns>The color: each channel is not a number as 0, clamped to 0 to 255 and rounded; the opacity is clamped to 0 to 1.</returns>
    internal static RgbColor FromSpace(InterpolationSpace space, Channels channels)
    {
        var rgb = space switch
        {
            InterpolationSpace.Hsl or InterpolationSpace.HslLong => HslToRgb(channels),
            InterpolationSpace.Lab => LabToRgb(channels),
            InterpolationSpace.Hcl or InterpolationSpace.HclLong => LabToRgb(HclToLab(channels)),
            _ => new RgbColor(channels.A, channels.B, channels.C, channels.O),
        };

        return new RgbColor(Whole(rgb.R), Whole(rgb.G), Whole(rgb.B), double.IsNaN(rgb.Opacity) ? 1 : Math.Clamp(rgb.Opacity, 0, 1));
    }

    /// <summary>
    ///     Rounds a channel to a whole number in 0 to 255; NaN is 0.
    /// </summary>
    /// <param name="value">The channel value.</param>
    /// <returns>The whole channel value.</returns>
    private static double Whole(double value) =>
        double.IsNaN(value) ? 0 : Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);

    /// <summary>
    ///     Interpolates linearly; equal or undefined distances give the defined end (d3's <c>nogamma</c>).
    /// </summary>
    /// <param name="a">The value at 0.</param>
    /// <param name="b">The value at 1.</param>
    /// <param name="t">The position.</param>
    /// <returns>The value at <paramref name="t"/>; when the values are equal or one is not a number, the one that is a number.</returns>
    private static double Linear(double a, double b, double t)
    {
        var d = b - a;
        if (HasDistance(d))
        {
            return a + (t * d);
        }

        return double.IsNaN(a) ? b : a;
    }

    /// <summary>
    ///     Determines whether two values have a usable distance: it is a number and not zero (d3's truthiness test of <c>b - a</c>).
    /// </summary>
    /// <param name="distance">The difference of two values.</param>
    /// <returns><see langword="true"/> when the distance is neither zero nor NaN.</returns>
    private static bool HasDistance(double distance) => !double.IsNaN(distance) && distance.CompareTo(0d) != 0;

    /// <summary>
    ///     Compares two doubles for exact equality, as the d3 algorithms do.
    /// </summary>
    /// <param name="a">One value.</param>
    /// <param name="b">The other value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    private static bool Same(double a, double b) => a.CompareTo(b) == 0;

    /// <summary>
    ///     Interpolates a channel with a gamma (d3's <c>gamma</c>): 1 is <see cref="Linear"/>, otherwise the values are
    ///     raised to the gamma, interpolated linearly and raised to its reciprocal.
    /// </summary>
    /// <param name="a">The value at 0.</param>
    /// <param name="b">The value at 1.</param>
    /// <param name="t">The position.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns>The value at <paramref name="t"/>.</returns>
    private static double Gamma(double a, double b, double t, double gamma)
    {
        if (Same(gamma, 1))
        {
            return Linear(a, b, t);
        }

        var d = b - a;
        if (!HasDistance(d))
        {
            return double.IsNaN(a) ? b : a;
        }

        var start = Math.Pow(a, gamma);
        var delta = Math.Pow(b, gamma) - start;
        return Math.Pow(start + (t * delta), 1 / gamma);
    }

    /// <summary>
    ///     Interpolates a hue along the shorter way round the circle (d3's <c>hue</c>).
    /// </summary>
    /// <param name="a">The hue at 0, in degrees, or NaN.</param>
    /// <param name="b">The hue at 1, in degrees, or NaN.</param>
    /// <param name="t">The position.</param>
    /// <returns>The hue at <paramref name="t"/>.</returns>
    private static double Hue(double a, double b, double t)
    {
        var d = b - a;
        if (!HasDistance(d))
        {
            return double.IsNaN(a) ? b : a;
        }

        if (d > 180 || d < -180)
        {
            d -= 360 * Math.Round(d / 360, MidpointRounding.AwayFromZero);
        }

        return a + (t * d);
    }

    /// <summary>
    ///     Converts sRGB to HSL (d3-color).
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>Hue in degrees, saturation and lightness (0 to 1), opacity; the hue is NaN for a grey, and the saturation too for black and white.</returns>
    private static Channels RgbToHsl(RgbColor color)
    {
        double r = color.R / 255, g = color.G / 255, b = color.B / 255;
        double min = Math.Min(r, Math.Min(g, b)), max = Math.Max(r, Math.Max(g, b));
        var h = double.NaN;
        var s = max - min;
        var l = (max + min) / 2;
        if (HasDistance(s))
        {
            if (Same(r, max))
            {
                h = ((g - b) / s) + (g < b ? 6 : 0);
            }
            else if (Same(g, max))
            {
                h = ((b - r) / s) + 2;
            }
            else
            {
                h = ((r - g) / s) + 4;
            }

            s /= l < 0.5 ? max + min : 2 - max - min;
            h *= 60;
        }
        else
        {
            s = l > 0 && l < 1 ? 0 : h;
        }

        return new Channels(h, s, l, color.Opacity);
    }

    /// <summary>
    ///     Converts HSL to sRGB (d3-color).
    /// </summary>
    /// <param name="channels">Hue, saturation, lightness, opacity.</param>
    /// <returns>The color with unrounded channels.</returns>
    private static RgbColor HslToRgb(Channels channels)
    {
        var hue = channels.A % 360;
        if (channels.A < 0)
        {
            hue += 360;
        }

        var s = double.IsNaN(hue) || double.IsNaN(channels.B) ? 0 : channels.B;
        var l = channels.C;
        var m2 = l + ((l < 0.5 ? l : 1 - l) * s);
        var m1 = (2 * l) - m2;
        return new RgbColor(
            HslChannel(hue >= 240 ? hue - 240 : hue + 120, m1, m2),
            HslChannel(hue, m1, m2),
            HslChannel(hue < 120 ? hue + 240 : hue - 120, m1, m2),
            channels.O);
    }

    /// <summary>
    ///     Computes one sRGB channel from a hue in HSL.
    /// </summary>
    /// <param name="h">The hue offset, in degrees.</param>
    /// <param name="m1">The lower bound of the channel, 0 to 1.</param>
    /// <param name="m2">The upper bound of the channel, 0 to 1.</param>
    /// <returns>The channel, 0 to 255.</returns>
    private static double HslChannel(double h, double m1, double m2)
    {
        if (h < 60)
        {
            return (m1 + ((m2 - m1) * h / 60)) * 255;
        }

        if (h < 180)
        {
            return m2 * 255;
        }

        return h < 240 ? (m1 + ((m2 - m1) * (240 - h) / 60)) * 255 : m1 * 255;
    }

    /// <summary>
    ///     Converts sRGB to CIELAB with the D50 white point (d3-color).
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>Lightness, a, b, opacity.</returns>
    private static Channels RgbToLab(RgbColor color)
    {
        var r = RgbToLinear(color.R);
        var g = RgbToLinear(color.G);
        var b = RgbToLinear(color.B);
        var y = XyzToLab(((0.2225045 * r) + (0.7168786 * g) + (0.0606169 * b)) / Yn);
        double x, z;
        if (Same(r, g) && Same(g, b))
        {
            x = z = y;
        }
        else
        {
            x = XyzToLab(((0.4360747 * r) + (0.3850649 * g) + (0.1430804 * b)) / Xn);
            z = XyzToLab(((0.0139322 * r) + (0.0971045 * g) + (0.7141733 * b)) / Zn);
        }

        return new Channels((116 * y) - 16, 500 * (x - y), 200 * (y - z), color.Opacity);
    }

    /// <summary>
    ///     Converts CIELAB with the D50 white point to sRGB (d3-color).
    /// </summary>
    /// <param name="lab">Lightness, a, b, opacity; a or b may be NaN, which counts as 0.</param>
    /// <returns>The color with unrounded channels.</returns>
    private static RgbColor LabToRgb(Channels lab)
    {
        var y = (lab.A + 16) / 116;
        var x = double.IsNaN(lab.B) ? y : y + (lab.B / 500);
        var z = double.IsNaN(lab.C) ? y : y - (lab.C / 200);
        x = Xn * LabToXyz(x);
        y = Yn * LabToXyz(y);
        z = Zn * LabToXyz(z);
        return new RgbColor(
            LinearToRgb((3.1338561 * x) - (1.6168667 * y) - (0.4906146 * z)),
            LinearToRgb((-0.9787684 * x) + (1.9161415 * y) + (0.0334540 * z)),
            LinearToRgb((0.0719453 * x) - (0.2289914 * y) + (1.4052427 * z)),
            lab.O);
    }

    /// <summary>
    ///     Converts CIELAB to its polar form (d3-color).
    /// </summary>
    /// <param name="lab">Lightness, a, b, opacity.</param>
    /// <returns>Hue in degrees, chroma, lightness, opacity; the hue is NaN when a and b are 0.</returns>
    private static Channels LabToHcl(Channels lab)
    {
        if (Same(lab.B, 0) && Same(lab.C, 0))
        {
            return new Channels(double.NaN, lab.A > 0 && lab.A < 100 ? 0 : double.NaN, lab.A, lab.O);
        }

        var h = Math.Atan2(lab.C, lab.B) * Degrees;
        return new Channels(h < 0 ? h + 360 : h, Math.Sqrt((lab.B * lab.B) + (lab.C * lab.C)), lab.A, lab.O);
    }

    /// <summary>
    ///     Converts the polar form of CIELAB back to CIELAB (d3-color).
    /// </summary>
    /// <param name="hcl">Hue in degrees, chroma, lightness, opacity.</param>
    /// <returns>Lightness, a, b, opacity.</returns>
    private static Channels HclToLab(Channels hcl)
    {
        if (double.IsNaN(hcl.A))
        {
            return new Channels(hcl.C, 0, 0, hcl.O);
        }

        var h = hcl.A * Radians;
        return new Channels(hcl.C, Math.Cos(h) * hcl.B, Math.Sin(h) * hcl.B, hcl.O);
    }

    /// <summary>
    ///     Converts an sRGB channel to linear light.
    /// </summary>
    /// <param name="value">The channel, 0 to 255.</param>
    /// <returns>The linear value, 0 to 1.</returns>
    private static double RgbToLinear(double value)
    {
        value /= 255;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    ///     Converts a linear light value to an sRGB channel.
    /// </summary>
    /// <param name="value">The linear value.</param>
    /// <returns>The channel, 0 to 255 (not clamped).</returns>
    private static double LinearToRgb(double value) =>
        255 * (value <= 0.0031308 ? 12.92 * value : (1.055 * Math.Pow(value, 1 / 2.4)) - 0.055);

    /// <summary>
    ///     The forward CIELAB transfer function (a cube root with a linear part near black).
    /// </summary>
    /// <param name="t">The relative XYZ value.</param>
    /// <returns>The transferred value.</returns>
    private static double XyzToLab(double t) => t > T3 ? Math.Pow(t, 1d / 3) : (t / T2) + T0;

    /// <summary>
    ///     The inverse CIELAB transfer function.
    /// </summary>
    /// <param name="t">The transferred value.</param>
    /// <returns>The relative XYZ value.</returns>
    private static double LabToXyz(double t) => t > T1 ? t * t * t : T2 * (t - T0);

    /// <summary>
    ///     Four channels of a color in an interpolation space.
    /// </summary>
    /// <param name="A">The first channel (red, hue or lightness).</param>
    /// <param name="B">The second channel (green, saturation, a or chroma).</param>
    /// <param name="C">The third channel (blue, lightness, b or lightness).</param>
    /// <param name="O">The opacity.</param>
    internal readonly record struct Channels(double A, double B, double C, double O);
}

/// <summary>
///     A color ramp: piecewise interpolation through a list of colors that are spaced evenly over 0 to 1.
/// </summary>
/// <remarks>
///     The stops are converted to the interpolation space once, so sampling a ramp many times (for every datum of a
///     color scale) costs one conversion back to sRGB per sample. The segment is chosen as in d3's <c>piecewise</c>:
///     <c>i = clamp(floor(t * n), 0, n - 1)</c> with <c>n</c> the number of segments, and the position inside the segment
///     is <c>t * n - i</c>, so a position outside 0 to 1 extrapolates along the first or last segment (the result is
///     clamped to the channel range). A ramp is immutable and thread-safe.
/// </remarks>
internal sealed class ColorRamp
{
    /// <summary>The space the ramp interpolates in.</summary>
    private readonly InterpolationSpace _space;

    /// <summary>The gamma of the sRGB space.</summary>
    private readonly double _gamma;

    /// <summary>The stops in the channels of the space.</summary>
    private readonly ColorInterpolator.Channels[] _stops;

    /// <summary>
    ///     Initializes a ramp.
    /// </summary>
    /// <param name="space">The space to interpolate in.</param>
    /// <param name="stops">The colors, at least one; the list is copied.</param>
    /// <param name="gamma">The gamma of the sRGB space (1 is linear); ignored by the other spaces.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stops"/> is empty.</exception>
    public ColorRamp(InterpolationSpace space, IReadOnlyList<RgbColor> stops, double gamma = 1)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("A color ramp needs at least one color.", nameof(stops));
        }

        _space = space;
        _gamma = space == InterpolationSpace.Rgb && !double.IsNaN(gamma) ? gamma : 1;
        _stops = new ColorInterpolator.Channels[stops.Count];
        for (var i = 0; i < _stops.Length; i++)
        {
            _stops[i] = ColorInterpolator.ToSpace(space, stops[i]);
        }
    }

    /// <summary>Gets the number of colors the ramp passes through.</summary>
    public int StopCount => _stops.Length;

    /// <summary>
    ///     Gets the color at a position.
    /// </summary>
    /// <param name="t">The position, 0 to 1; NaN counts as 0, and an infinite value as the nearer end.</param>
    /// <returns>The color with whole 8-bit channels.</returns>
    public RgbColor At(double t)
    {
        var n = _stops.Length - 1;
        if (n == 0)
        {
            return ColorInterpolator.FromSpace(_space, _stops[0]);
        }

        if (double.IsNaN(t) || double.IsNegativeInfinity(t))
        {
            t = 0;
        }
        else if (double.IsPositiveInfinity(t))
        {
            t = 1;
        }

        var scaled = t * n;
        var i = (int)Math.Max(0, Math.Min(n - 1, Math.Floor(scaled)));
        return ColorInterpolator.FromSpace(
            _space,
            ColorInterpolator.Mix(_space, _stops[i], _stops[i + 1], scaled - i, _gamma));
    }

    /// <summary>
    ///     Gets the CSS text of the color at a position.
    /// </summary>
    /// <param name="t">The position; see <see cref="At"/>.</param>
    /// <returns>The color as <c>#rrggbb</c> (or <c>#rrggbbaa</c> when it is not opaque).</returns>
    public string CssAt(double t) => At(t).ToCss();
}
