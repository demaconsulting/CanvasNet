// cspell:ignore feblend colordodge colorburn hardlight softlight unswapped
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feBlend</c> primitive against <paramref name="input"/> (the "source"
    ///     layer, on top) and <paramref name="input2"/> (the "backdrop" layer, underneath).
    /// </summary>
    /// <param name="element">The <c>feBlend</c> element.</param>
    /// <param name="input">The already-resolved <c>in</c> buffer (the source/top layer).</param>
    /// <param name="input2">The already-resolved <c>in2</c> buffer (the backdrop/bottom layer).</param>
    /// <returns>A new, independent output buffer.</returns>
    /// <remarks>
    ///     Implements all fifteen CSS Compositing Level 1 blend modes
    ///     (<see href="https://www.w3.org/TR/compositing-1/#blending"/>): the twelve separable
    ///     modes (<c>normal</c>, <c>multiply</c>, <c>screen</c>, <c>overlay</c>, <c>darken</c>,
    ///     <c>lighten</c>, <c>color-dodge</c>, <c>color-burn</c>, <c>hard-light</c>,
    ///     <c>soft-light</c>, <c>difference</c>, <c>exclusion</c>) evaluated independently per
    ///     channel, and the four
    ///     non-separable modes (<c>hue</c>, <c>saturation</c>, <c>color</c>, <c>luminosity</c>)
    ///     evaluated on the whole RGB triple via the spec's exact
    ///     <c>Lum</c>/<c>ClipColor</c>/<c>SetLum</c>/<c>Sat</c>/<c>SetSat</c> reference algorithm
    ///     (deliberately not a per-channel approximation). The default <c>mode</c>, and any
    ///     unrecognized <c>mode</c> value, is <c>normal</c>.
    ///     <para>
    ///     Per the CSS Compositing formula, the per-channel blend function
    ///     <c>B(Cb, Cs)</c> is evaluated on straight (non-premultiplied) colors, then combined via
    ///     <c>Cs' = (1 - &#945;b)&#215;Cs + &#945;b&#215;B(Cb, Cs)</c>, and finally simple-alpha
    ///     ("source-over") composited using both inputs' own alphas:
    ///     <c>&#945;o = &#945;s + &#945;b&#215;(1 - &#945;s)</c>,
    ///     <c>Co = &#945;s&#215;Cs' + (1 - &#945;s)&#215;&#945;b&#215;Cb</c> (premultiplied), with
    ///     the stored straight color <c>Co / &#945;o</c> (or <c>0</c> if <c>&#945;o</c> is zero) -
    ///     this mirrors <see cref="CompositeFeOperator"/>'s own inline premultiply/un-premultiply
    ///     pattern rather than calling <see cref="Surface.PremultiplyAlpha"/>.
    ///     </para>
    /// </remarks>
    private static Surface ApplyFeBlend(XElement element, Surface input, Surface input2)
    {
        var mode = ((string?)element.Attribute("mode"))?.Trim();
        var width = input.Width;
        var height = input.Height;
        var output = new Surface(width, height);

        for (var y = 0; y < height; y++)
        {
            var sourceRow = input.GetRowSpan(y);
            var backdropRow = input2.GetRowSpan(y);
            var outputRow = output.GetRowSpan(y);

            for (var x = 0; x < width; x++)
            {
                var source = sourceRow[x];
                var backdrop = backdropRow[x];

                var sourceAlpha = source.A / 255f;
                var backdropAlpha = backdrop.A / 255f;

                var sourceColor = (R: source.R / 255f, G: source.G / 255f, B: source.B / 255f);
                var backdropColor = (R: backdrop.R / 255f, G: backdrop.G / 255f, B: backdrop.B / 255f);

                var blended = EvaluateBlendFunction(mode, backdropColor, sourceColor);

                // Cs' = (1 - ab)*Cs + ab*B(Cb, Cs)
                var blendedR = ((1f - backdropAlpha) * sourceColor.R) + (backdropAlpha * blended.R);
                var blendedG = ((1f - backdropAlpha) * sourceColor.G) + (backdropAlpha * blended.G);
                var blendedB = ((1f - backdropAlpha) * sourceColor.B) + (backdropAlpha * blended.B);

                // Simple alpha (source-over) compositing of the blended source over the backdrop
                var outAlpha = sourceAlpha + (backdropAlpha * (1f - sourceAlpha));

                byte outR, outG, outB;
                if (outAlpha <= 0f)
                {
                    outR = outG = outB = 0;
                }
                else
                {
                    var premultipliedR = (sourceAlpha * blendedR) + ((1f - sourceAlpha) * backdropAlpha * backdropColor.R);
                    var premultipliedG = (sourceAlpha * blendedG) + ((1f - sourceAlpha) * backdropAlpha * backdropColor.G);
                    var premultipliedB = (sourceAlpha * blendedB) + ((1f - sourceAlpha) * backdropAlpha * backdropColor.B);

                    outR = ToByte(Math.Clamp(premultipliedR / outAlpha, 0f, 1f) * 255f);
                    outG = ToByte(Math.Clamp(premultipliedG / outAlpha, 0f, 1f) * 255f);
                    outB = ToByte(Math.Clamp(premultipliedB / outAlpha, 0f, 1f) * 255f);
                }

                outputRow[x] = new Rgba32(outR, outG, outB, ToByte(Math.Clamp(outAlpha, 0f, 1f) * 255f));
            }
        }

        return output;
    }

    /// <summary>
    ///     Evaluates the CSS Compositing <c>B(Cb, Cs)</c> blend function for one named
    ///     <c>mode</c>, dispatching to the separable per-channel formulas or the non-separable
    ///     whole-triple reference algorithm as appropriate.
    /// </summary>
    /// <param name="mode">The raw <c>mode</c> attribute text, or <see langword="null"/>/empty for the default.</param>
    /// <param name="backdrop">The straight backdrop (<c>in2</c>) color, each channel in <c>[0, 1]</c>.</param>
    /// <param name="source">The straight source (<c>in</c>) color, each channel in <c>[0, 1]</c>.</param>
    /// <returns>The blended straight color, each channel in <c>[0, 1]</c>.</returns>
    private static (float R, float G, float B) EvaluateBlendFunction(
        string? mode,
        (float R, float G, float B) backdrop,
        (float R, float G, float B) source) => mode switch
        {
            _ when string.Equals(mode, "multiply", StringComparison.OrdinalIgnoreCase) =>
                (BlendMultiply(backdrop.R, source.R), BlendMultiply(backdrop.G, source.G), BlendMultiply(backdrop.B, source.B)),

            _ when string.Equals(mode, "screen", StringComparison.OrdinalIgnoreCase) =>
                (BlendScreen(backdrop.R, source.R), BlendScreen(backdrop.G, source.G), BlendScreen(backdrop.B, source.B)),

            _ when string.Equals(mode, "overlay", StringComparison.OrdinalIgnoreCase) =>
                (BlendOverlay(backdrop.R, source.R), BlendOverlay(backdrop.G, source.G), BlendOverlay(backdrop.B, source.B)),

            _ when string.Equals(mode, "darken", StringComparison.OrdinalIgnoreCase) =>
                (MathF.Min(backdrop.R, source.R), MathF.Min(backdrop.G, source.G), MathF.Min(backdrop.B, source.B)),

            _ when string.Equals(mode, "lighten", StringComparison.OrdinalIgnoreCase) =>
                (MathF.Max(backdrop.R, source.R), MathF.Max(backdrop.G, source.G), MathF.Max(backdrop.B, source.B)),

            _ when string.Equals(mode, "color-dodge", StringComparison.OrdinalIgnoreCase) =>
                (BlendColorDodge(backdrop.R, source.R), BlendColorDodge(backdrop.G, source.G), BlendColorDodge(backdrop.B, source.B)),

            _ when string.Equals(mode, "color-burn", StringComparison.OrdinalIgnoreCase) =>
                (BlendColorBurn(backdrop.R, source.R), BlendColorBurn(backdrop.G, source.G), BlendColorBurn(backdrop.B, source.B)),

            _ when string.Equals(mode, "hard-light", StringComparison.OrdinalIgnoreCase) =>
                (BlendHardLight(backdrop.R, source.R), BlendHardLight(backdrop.G, source.G), BlendHardLight(backdrop.B, source.B)),

            _ when string.Equals(mode, "soft-light", StringComparison.OrdinalIgnoreCase) =>
                (BlendSoftLight(backdrop.R, source.R), BlendSoftLight(backdrop.G, source.G), BlendSoftLight(backdrop.B, source.B)),

            _ when string.Equals(mode, "difference", StringComparison.OrdinalIgnoreCase) =>
                (BlendDifference(backdrop.R, source.R), BlendDifference(backdrop.G, source.G), BlendDifference(backdrop.B, source.B)),

            _ when string.Equals(mode, "exclusion", StringComparison.OrdinalIgnoreCase) =>
                (BlendExclusion(backdrop.R, source.R), BlendExclusion(backdrop.G, source.G), BlendExclusion(backdrop.B, source.B)),

            _ when string.Equals(mode, "hue", StringComparison.OrdinalIgnoreCase) =>
                SetLum(SetSat(source, Sat(backdrop)), Lum(backdrop)),

            _ when string.Equals(mode, "saturation", StringComparison.OrdinalIgnoreCase) =>
                SetLum(SetSat(backdrop, Sat(source)), Lum(backdrop)),

            _ when string.Equals(mode, "color", StringComparison.OrdinalIgnoreCase) =>
                SetLum(source, Lum(backdrop)),

            _ when string.Equals(mode, "luminosity", StringComparison.OrdinalIgnoreCase) =>
                SetLum(backdrop, Lum(source)),

            // "normal" (the default) and any unrecognized mode value both fall back to B(Cb, Cs) = Cs
            _ => source
        };

    /// <summary>Evaluates the <c>multiply</c> separable blend formula: <c>Cb &#215; Cs</c>.</summary>
    private static float BlendMultiply(float cb, float cs) => cb * cs;

    /// <summary>Evaluates the <c>screen</c> separable blend formula: <c>Cb + Cs - Cb &#215; Cs</c>.</summary>
    private static float BlendScreen(float cb, float cs) => cb + cs - (cb * cs);

    /// <summary>Evaluates the <c>color-dodge</c> separable blend formula per the spec's three-case definition.</summary>
    private static float BlendColorDodge(float cb, float cs)
    {
        if (IsExactly(cb, 0f))
        {
            return 0f;
        }

        return IsExactly(cs, 1f) ? 1f : MathF.Min(1f, cb / (1f - cs));
    }

    /// <summary>Evaluates the <c>color-burn</c> separable blend formula per the spec's three-case definition.</summary>
    private static float BlendColorBurn(float cb, float cs)
    {
        if (IsExactly(cb, 1f))
        {
            return 1f;
        }

        return IsExactly(cs, 0f) ? 0f : 1f - MathF.Min(1f, (1f - cb) / cs);
    }

    /// <summary>
    ///     Compares two channel values for exact bit-pattern equality, matching this repository's
    ///     established exact-sentinel-comparison convention (see <c>Rect.BitsEqual</c>) rather than
    ///     a raw <c>==</c> float comparison (which SonarAnalyzer's S1244 flags as a numerically
    ///     fragile result comparison). This is deliberately used only for the spec's own exact
    ///     <c>0</c>/<c>1</c> channel-range boundary checks - both operands here are always derived
    ///     from an exact <c>byte / 255f</c> division, so bit-exact equality is the correct,
    ///     deterministic test, not an approximation.
    /// </summary>
    /// <param name="value">The channel value to test.</param>
    /// <param name="target">The exact boundary value (<c>0f</c> or <c>1f</c>) to compare against.</param>
    /// <returns><see langword="true"/> when the two values have identical bit patterns.</returns>
    private static bool IsExactly(float value, float target) =>
        BitConverter.SingleToInt32Bits(value) == BitConverter.SingleToInt32Bits(target);

    /// <summary>Evaluates the <c>hard-light</c> separable blend formula (a source-conditioned <c>multiply</c>/<c>screen</c> split).</summary>
    private static float BlendHardLight(float cb, float cs) =>
        cs <= 0.5f ? BlendMultiply(cb, 2f * cs) : BlendScreen(cb, (2f * cs) - 1f);

    /// <summary>
    ///     Evaluates the <c>overlay</c> separable blend formula: per the CSS Compositing Level 1
    ///     spec, <c>Overlay(Cb, Cs) = HardLight(Cs, Cb)</c> - i.e. <c>hard-light</c> with the
    ///     backdrop and source arguments swapped, condition-tested on the <b>backdrop</b> channel
    ///     rather than the source channel.
    /// </summary>
    /// <remarks>
    ///     Substituting <see cref="BlendHardLight"/>'s own <c>(cb, cs)</c> parameters with
    ///     <c>(cs, cb)</c> and simplifying reproduces the spec's direct <c>overlay</c> formula
    ///     exactly: <c>2*Cb*Cs</c> when <c>Cb &lt;= 0.5</c>, else <c>1 - 2*(1-Cb)*(1-Cs)</c>. Calling
    ///     <see cref="BlendHardLight"/> with the arguments in their original (unswapped) order would
    ///     silently condition the branch on the wrong channel and produce an incorrect result.
    /// </remarks>
    /// <param name="cb">The straight backdrop channel value, in <c>[0, 1]</c>.</param>
    /// <param name="cs">The straight source channel value, in <c>[0, 1]</c>.</param>
    /// <returns>The blended channel value, in <c>[0, 1]</c>.</returns>
    private static float BlendOverlay(float cb, float cs)
    {
        // Overlay(Cb, Cs) = HardLight(Cs, Cb) - renamed locals (rather than passing cs/cb
        // directly) so the swapped argument order is unambiguous to both readers and static
        // analysis, rather than looking like an accidental same-name-wrong-order mistake
        var hardLightBackdrop = cs;
        var hardLightSource = cb;
        return BlendHardLight(hardLightBackdrop, hardLightSource);
    }

    /// <summary>Evaluates the <c>soft-light</c> separable blend formula, including its own <c>D(x)</c> helper.</summary>
    private static float BlendSoftLight(float cb, float cs)
    {
        if (cs <= 0.5f)
        {
            return cb - ((1f - (2f * cs)) * cb * (1f - cb));
        }

        var d = cb <= 0.25f ? (((16f * cb) - 12f) * cb + 4f) * cb : MathF.Sqrt(cb);
        return cb + (((2f * cs) - 1f) * (d - cb));
    }

    /// <summary>Evaluates the <c>difference</c> separable blend formula: <c>|Cb - Cs|</c>.</summary>
    private static float BlendDifference(float cb, float cs) => MathF.Abs(cb - cs);

    /// <summary>Evaluates the <c>exclusion</c> separable blend formula: <c>Cb + Cs - 2 &#215; Cb &#215; Cs</c>.</summary>
    private static float BlendExclusion(float cb, float cs) => cb + cs - (2f * cb * cs);

    /// <summary>
    ///     Computes the CSS Compositing non-separable <c>Lum(C)</c> function.
    /// </summary>
    /// <param name="c">The straight RGB triple, each channel in <c>[0, 1]</c>.</param>
    /// <returns>The scalar luminance, using the CSS Compositing (not SVG luma) coefficients.</returns>
    /// <remarks>
    ///     Deliberately distinct from <see cref="ComputeLuminance"/> (used by
    ///     <c>feColorMatrix type="luminanceToAlpha"</c>): the CSS Compositing Level 1 spec defines
    ///     its own non-separable-blending <c>Lum</c> with the coefficients <c>0.3</c>/<c>0.59</c>/
    ///     <c>0.11</c>, not the SVG/sRGB luma coefficients <c>0.2125</c>/<c>0.7154</c>/<c>0.0721</c>.
    /// </remarks>
    private static float Lum((float R, float G, float B) c) => (0.3f * c.R) + (0.59f * c.G) + (0.11f * c.B);

    /// <summary>
    ///     Computes the CSS Compositing non-separable <c>ClipColor(C)</c> function: clamps an
    ///     out-of-<c>[0, 1]</c>-range color back into range while preserving its own luminance.
    /// </summary>
    /// <param name="c">The (possibly out-of-range) straight RGB triple to clip.</param>
    /// <returns>The clipped straight RGB triple, each channel in <c>[0, 1]</c>.</returns>
    private static (float R, float G, float B) ClipColor((float R, float G, float B) c)
    {
        var l = Lum(c);
        var n = MathF.Min(c.R, MathF.Min(c.G, c.B));
        var x = MathF.Max(c.R, MathF.Max(c.G, c.B));

        if (n < 0f)
        {
            c = (
                l + (((c.R - l) * l) / (l - n)),
                l + (((c.G - l) * l) / (l - n)),
                l + (((c.B - l) * l) / (l - n)));
        }

        if (x > 1f)
        {
            c = (
                l + (((c.R - l) * (1f - l)) / (x - l)),
                l + (((c.G - l) * (1f - l)) / (x - l)),
                l + (((c.B - l) * (1f - l)) / (x - l)));
        }

        return c;
    }

    /// <summary>
    ///     Computes the CSS Compositing non-separable <c>SetLum(C, l)</c> function: shifts every
    ///     channel by the same delta so the triple's own <see cref="Lum"/> becomes <paramref name="l"/>,
    ///     then <see cref="ClipColor"/>s the result back into range.
    /// </summary>
    /// <param name="c">The straight RGB triple whose luminance is to be replaced.</param>
    /// <param name="l">The target luminance.</param>
    /// <returns>The adjusted, clipped straight RGB triple.</returns>
    private static (float R, float G, float B) SetLum((float R, float G, float B) c, float l)
    {
        var delta = l - Lum(c);
        return ClipColor((c.R + delta, c.G + delta, c.B + delta));
    }

    /// <summary>
    ///     Computes the CSS Compositing non-separable <c>Sat(C)</c> function: the triple's own
    ///     channel range (<c>max - min</c>).
    /// </summary>
    /// <param name="c">The straight RGB triple to inspect.</param>
    /// <returns>The saturation value.</returns>
    private static float Sat((float R, float G, float B) c) =>
        MathF.Max(c.R, MathF.Max(c.G, c.B)) - MathF.Min(c.R, MathF.Min(c.G, c.B));

    /// <summary>
    ///     Computes the CSS Compositing non-separable <c>SetSat(C, s)</c> function per the spec's
    ///     exact rank-dependent reference algorithm: rescales the mid channel between the min and
    ///     max channels so the triple's own <see cref="Sat"/> becomes <paramref name="s"/>, leaving
    ///     hue unaffected.
    /// </summary>
    /// <param name="c">The straight RGB triple whose saturation is to be replaced.</param>
    /// <param name="s">The target saturation.</param>
    /// <returns>The adjusted straight RGB triple (not yet luminance-corrected/clipped).</returns>
    private static (float R, float G, float B) SetSat((float R, float G, float B) c, float s)
    {
        // Rank the three channels by index (0=R, 1=G, 2=B) so the spec's Max/Mid/Min assignment
        // can be mirrored exactly, including ties, without duplicating the rescale logic 3 times.
        Span<float> values = [c.R, c.G, c.B];
        var maxIndex = 0;
        var minIndex = 0;
        for (var i = 1; i < 3; i++)
        {
            if (values[i] > values[maxIndex])
            {
                maxIndex = i;
            }

            if (values[i] < values[minIndex])
            {
                minIndex = i;
            }
        }

        var midIndex = 3 - maxIndex - minIndex;
        if (maxIndex == minIndex)
        {
            // All three channels are equal: every rescale below would be 0/0. Per the spec, the
            // mid/max channels are separately zeroed to preserve a well-defined result.
            return (0f, 0f, 0f);
        }

        if (values[maxIndex] > values[minIndex])
        {
            values[midIndex] = ((values[midIndex] - values[minIndex]) * s) / (values[maxIndex] - values[minIndex]);
            values[maxIndex] = s;
        }
        else
        {
            values[midIndex] = 0f;
            values[maxIndex] = 0f;
        }

        values[minIndex] = 0f;

        return (values[0], values[1], values[2]);
    }
}
