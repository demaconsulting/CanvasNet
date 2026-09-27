// cspell:ignore turbulence lattice fractalnoise stitchtiles nostitch basefrequency numoctaves perlin
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feTurbulence</c> primitive, synthesizing brand-new imagery from the SVG
    ///     specification's own published Perlin-noise reference algorithm (SVG 1.1 &#167;15.22/15.24)
    ///     rather than sampling any input buffer.
    /// </summary>
    /// <param name="element">The <c>feTurbulence</c> element.</param>
    /// <param name="width">The filter buffer width.</param>
    /// <param name="height">The filter buffer height.</param>
    /// <param name="scale">The local-space-to-pixel-space scale factor (see <see cref="EstimateUniformScale"/>).</param>
    /// <param name="regionPixelX">The enclosing filter region's absolute pixel-space X origin.</param>
    /// <param name="regionPixelY">The enclosing filter region's absolute pixel-space Y origin.</param>
    /// <returns>A new, independent, straight-alpha output buffer.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved <c>numOctaves</c> exceeds <see cref="MaxTurbulenceOctaves"/>.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         Each of the 4 output channels (R, G, B, A, in that order) is an independent noise
    ///         field, using the shared permutation/gradient tables' own per-channel slice (see
    ///         <see cref="TurbulenceTables"/>) - this primitive introduces brand-new imagery with no
    ///         existing alpha to premultiply against, so the output is plain straight-alpha.
    ///     </para>
    ///     <para>
    ///         <c>stitchTiles="stitch"</c> is parsed but tolerantly falls back to the
    ///         <c>noStitch</c> algorithm below rather than implementing the spec's tile-seam-free
    ///         frequency-adjustment/wraparound bookkeeping - a documented, intentional scope
    ///         boundary (no occurrence of <c>stitchTiles="stitch"</c> exists anywhere in this
    ///         repository's fixture corpus), consistent with this codec's existing tolerant-fallback
    ///         philosophy for out-of-scope attribute values (e.g. <c>kernelUnitLength</c>,
    ///         <c>primitiveUnits="objectBoundingBox"</c>).
    ///     </para>
    ///     <para>
    ///         The pixel-to-local-space point mapping used to feed the reference algorithm's
    ///         <c>point[]</c> is this codec's own necessary design decision (the spec's <c>point[]</c>
    ///         is otherwise abstract, and this codec has no <c>filterRes</c>/<c>primitiveUnits</c>
    ///         support to anchor it precisely): <c>point.x = (regionPixelX + pixelX) / scale</c>, and
    ///         symmetrically for <c>y</c> - i.e. each pixel's own absolute pixel-space position,
    ///         converted back to local units by the same <paramref name="scale"/> factor
    ///         <c>surfaceScale</c>/light-<c>z</c> use in <see cref="ApplyFeDiffuseLighting"/>/
    ///         <see cref="ApplyFeSpecularLighting"/>. <c>baseFrequency</c> itself is used exactly as
    ///         authored (already expressed in local/user units per spec).
    ///     </para>
    /// </remarks>
    private static Surface ApplyFeTurbulence(XElement element, int width, int height, float scale, int regionPixelX, int regionPixelY)
    {
        var seedAttribute = ParseFirstNumberToken((string?)element.Attribute("seed")) ?? 0f;
        var seed = (long)MathF.Truncate(seedAttribute);
        var tables = new TurbulenceTables(seed);

        var (baseFrequencyX, baseFrequencyY) = ResolveTurbulenceBaseFrequency(element);
        var numOctaves = ResolveTurbulenceNumOctaves(element);
        var fractalSum = string.Equals(((string?)element.Attribute("type"))?.Trim(), "fractalNoise", StringComparison.OrdinalIgnoreCase);

        var output = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            var row = output.GetRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                var pointX = (regionPixelX + x) / scale;
                var pointY = (regionPixelY + y) / scale;

                var r = EvaluateTurbulenceChannel(tables, 0, pointX, pointY, baseFrequencyX, baseFrequencyY, numOctaves, fractalSum);
                var g = EvaluateTurbulenceChannel(tables, 1, pointX, pointY, baseFrequencyX, baseFrequencyY, numOctaves, fractalSum);
                var b = EvaluateTurbulenceChannel(tables, 2, pointX, pointY, baseFrequencyX, baseFrequencyY, numOctaves, fractalSum);
                var a = EvaluateTurbulenceChannel(tables, 3, pointX, pointY, baseFrequencyX, baseFrequencyY, numOctaves, fractalSum);

                row[x] = new Rgba32(ToByte(r), ToByte(g), ToByte(b), ToByte(a));
            }
        }

        return output;
    }

    /// <summary>
    ///     Converts one channel's raw <c>turbulence()</c> function result into a byte-range color
    ///     value, per the spec's exact <c>type="turbulence"</c>/<c>type="fractalNoise"</c> formulas.
    /// </summary>
    /// <param name="tables">The already-initialized permutation/gradient tables.</param>
    /// <param name="channel">The channel index (0=R, 1=G, 2=B, 3=A).</param>
    /// <param name="pointX">The local-space X coordinate.</param>
    /// <param name="pointY">The local-space Y coordinate.</param>
    /// <param name="baseFrequencyX">The resolved <c>baseFrequency</c> X component.</param>
    /// <param name="baseFrequencyY">The resolved <c>baseFrequency</c> Y component.</param>
    /// <param name="numOctaves">The resolved <c>numOctaves</c>.</param>
    /// <param name="fractalSum">
    ///     <see langword="true"/> for <c>type="fractalNoise"</c> (signed sum, remapped to
    ///     <c>((sum * 255) + 255) / 2</c>); <see langword="false"/> for <c>type="turbulence"</c>
    ///     (absolute-value sum, remapped to <c>sum * 255</c>).
    /// </param>
    /// <returns>The clamped <c>[0, 255]</c> channel value.</returns>
    private static float EvaluateTurbulenceChannel(
        TurbulenceTables tables,
        int channel,
        float pointX,
        float pointY,
        float baseFrequencyX,
        float baseFrequencyY,
        int numOctaves,
        bool fractalSum)
    {
        var sum = tables.Turbulence(channel, pointX, pointY, baseFrequencyX, baseFrequencyY, numOctaves, fractalSum);
        return fractalSum
            ? Math.Clamp(((sum * 255f) + 255f) / 2f, 0f, 255f)
            : Math.Clamp(sum * 255f, 0f, 255f);
    }

    /// <summary>
    ///     Resolves <c>feTurbulence</c>'s <c>baseFrequency</c> attribute (one or two
    ///     whitespace/comma-separated components; a lone component applies to both axes).
    /// </summary>
    /// <param name="element">The <c>feTurbulence</c> element.</param>
    /// <returns>The resolved X/Y base frequency, defaulting to <c>0</c> per spec when absent.</returns>
    private static (float X, float Y) ResolveTurbulenceBaseFrequency(XElement element)
    {
        var raw = (string?)element.Attribute("baseFrequency");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (0f, 0f);
        }

        var numbers = ParseNumberList(raw);
        if (numbers.Count == 0)
        {
            return (0f, 0f);
        }

        var x = numbers[0];
        var y = numbers.Count > 1 ? numbers[1] : numbers[0];
        return (float.IsFinite(x) ? x : 0f, float.IsFinite(y) ? y : 0f);
    }

    /// <summary>
    ///     Resolves and validates <c>feTurbulence</c>'s <c>numOctaves</c> attribute.
    /// </summary>
    /// <param name="element">The <c>feTurbulence</c> element.</param>
    /// <returns>
    ///     The resolved octave count, defaulting to <c>1</c> per spec when absent, floored to
    ///     <c>0</c> for any non-positive resolved value (an octave count of <c>0</c> tolerantly
    ///     produces a flat, zero-signal output rather than throwing or being ignored).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved value exceeds <see cref="MaxTurbulenceOctaves"/>.
    /// </exception>
    private static int ResolveTurbulenceNumOctaves(XElement element)
    {
        var raw = (string?)element.Attribute("numOctaves");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1;
        }

        var value = ParseFirstNumberToken(raw);
        var numOctaves = value.HasValue && float.IsFinite(value.Value) ? (int)MathF.Truncate(value.Value) : 1;
        if (numOctaves > MaxTurbulenceOctaves)
        {
            throw new InvalidDataException($"The feTurbulence numOctaves value must not exceed {MaxTurbulenceOctaves}.");
        }

        return Math.Max(numOctaves, 0);
    }

    /// <summary>
    ///     Holds one <c>feTurbulence</c> primitive's own independently-seeded permutation-selector
    ///     and gradient tables, and implements the SVG specification's exact reference Perlin-noise
    ///     algorithm (SVG 1.1 &#167;15.24's published C reference implementation) against them.
    /// </summary>
    /// <remarks>
    ///     Deliberately an instance (not a shared static/mutable field on <see cref="SvgCodec"/>):
    ///     two <c>feTurbulence</c> primitives in the same document may use different <c>seed</c>
    ///     values and must not share state, and this codec's <c>Load</c> entry point offers no other
    ///     natural place to cache a per-seed table. Only <see cref="ApplyFeTurbulence"/> and this
    ///     type's own unit tests (via reflection) ever construct or call this type.
    /// </remarks>
    private sealed class TurbulenceTables
    {
        /// <summary>The spec's fixed lattice/gradient table size.</summary>
        private const int BSize = 256;

        /// <summary>The spec's fixed lattice index bitmask (<c>BSize - 1</c>).</summary>
        private const int BM = 255;

        /// <summary>The spec's fixed fractional-coordinate offset, keeping <c>t</c> non-negative.</summary>
        private const int PerlinN = 4096;

        /// <summary>The Park &amp; Miller minimal-standard PRNG's modulus.</summary>
        private const int RandM = 2147483647;

        /// <summary>The Park &amp; Miller minimal-standard PRNG's multiplier.</summary>
        private const int RandA = 16807;

        /// <summary>The Park &amp; Miller minimal-standard PRNG's Schrage-decomposition quotient.</summary>
        private const int RandQ = 127773;

        /// <summary>The Park &amp; Miller minimal-standard PRNG's Schrage-decomposition remainder.</summary>
        private const int RandR = 2836;

        /// <summary>The permutation/lattice selector table, sized <c>2*BSize + 2</c> per spec.</summary>
        private readonly int[] _latticeSelector = new int[(BSize * 2) + 2];

        /// <summary>
        ///     The per-channel gradient table, sized <c>[4][2*BSize + 2][2]</c> per spec (4 =
        ///     R/G/B/A, exactly matching this codec's own <see cref="Rgba32"/> channel order).
        /// </summary>
        private readonly float[,,] _gradient = new float[4, (BSize * 2) + 2, 2];

        /// <summary>
        ///     Initializes a new set of tables, seeded from <paramref name="seed"/>, running the
        ///     spec's exact <c>init()</c> reference algorithm.
        /// </summary>
        /// <param name="seed">The already-truncated-toward-zero raw <c>seed</c> attribute value.</param>
        public TurbulenceTables(long seed)
        {
            var currentSeed = SetupSeed(seed);

            // Per-channel permutation-selector identity fill and gradient-vector generation, for
            // lattice indices 0..255. The running currentSeed threads through every random() call
            // below in document/loop order - this exact sequencing (not just the individual
            // formulas) is part of the spec's own published test-vector-verified algorithm.
            for (var channel = 0; channel < 4; channel++)
            {
                for (var i = 0; i < BSize; i++)
                {
                    _latticeSelector[i] = i;

                    for (var j = 0; j < 2; j++)
                    {
                        currentSeed = Random(currentSeed);
                        _gradient[channel, i, j] = ((currentSeed % 512) - 256) / 256f;
                    }

                    var s = MathF.Sqrt((_gradient[channel, i, 0] * _gradient[channel, i, 0]) + (_gradient[channel, i, 1] * _gradient[channel, i, 1]));
                    if (s != 0f)
                    {
                        _gradient[channel, i, 0] /= s;
                        _gradient[channel, i, 1] /= s;
                    }
                }
            }

            // Fisher-Yates-style shuffle of the lattice selector table, continuing to consume
            // random() calls from wherever the loop above left off. The spec's own reference
            // implementation pre-decrements its loop index from BSize (256) and tests truthiness
            // on each pass, so the loop body visits index values 255 down to 1 inclusive, and
            // never visits index value 0. Do not "simplify" this into an ordinary descending
            // range that also includes index value 0 - that extra pass is not part of the spec's
            // own published algorithm and would silently change every downstream noise value.
            var latticeIndex = BSize;
            while (--latticeIndex != 0)
            {
                var k = _latticeSelector[latticeIndex];
                currentSeed = Random(currentSeed);
                var j = (int)(currentSeed % BSize);
                _latticeSelector[latticeIndex] = _latticeSelector[j];
                _latticeSelector[j] = k;
            }

            // Duplicate the first BSize+2 entries into the table's second half, so lattice lookups
            // never need to wrap the index themselves.
            for (var i = 0; i < BSize + 2; i++)
            {
                _latticeSelector[BSize + i] = _latticeSelector[i];
                for (var channel = 0; channel < 4; channel++)
                {
                    for (var j = 0; j < 2; j++)
                    {
                        _gradient[channel, BSize + i, j] = _gradient[channel, i, j];
                    }
                }
            }
        }

        /// <summary>
        ///     Normalizes a raw <c>seed</c> attribute value into the PRNG's valid range, per the
        ///     spec's <c>setup_seed()</c> reference function.
        /// </summary>
        /// <param name="seed">The already-truncated-toward-zero raw seed value.</param>
        /// <returns>The normalized seed, always in <c>[1, RandM - 1]</c>.</returns>
        internal static long SetupSeed(long seed)
        {
            if (seed <= 0)
            {
                seed = -(seed % (RandM - 1)) + 1;
            }

            if (seed > RandM - 1)
            {
                seed = RandM - 1;
            }

            return seed;
        }

        /// <summary>
        ///     Generates the next value in the Park &amp; Miller minimal-standard PRNG sequence, per
        ///     the spec's own <c>random()</c> reference function.
        /// </summary>
        /// <param name="seed">The current seed/state value.</param>
        /// <returns>
        ///     The next seed/state value. Per the spec's own published conformance test vector, the
        ///     10,000th value generated by repeatedly calling this method starting from seed
        ///     <c>1</c> must equal exactly <c>1043618065</c> (see
        ///     <c>SvgTurbulencePrng_10000thValueFromSeedOne_MatchesSpecPublishedTestVector</c>).
        /// </returns>
        internal static long Random(long seed)
        {
            var result = (RandA * (seed % RandQ)) - (RandR * (seed / RandQ));
            if (result <= 0)
            {
                result += RandM;
            }

            return result;
        }

        /// <summary>
        ///     Computes the smoothstep-style interpolation curve <c>t*t*(3-2*t)</c> the spec's
        ///     reference algorithm uses to blend lattice corners.
        /// </summary>
        /// <param name="t">The fractional interpolation position, in <c>[0, 1]</c>.</param>
        /// <returns>The smoothed interpolation position.</returns>
        private static float SCurve(float t) => t * t * (3f - (2f * t));

        /// <summary>Performs one linear interpolation, per the spec's own <c>lerp()</c> reference function.</summary>
        /// <param name="t">The interpolation position.</param>
        /// <param name="a">The value at <paramref name="t"/> = 0.</param>
        /// <param name="b">The value at <paramref name="t"/> = 1.</param>
        /// <returns>The interpolated value.</returns>
        private static float Lerp(float t, float a, float b) => a + (t * (b - a));

        /// <summary>
        ///     Evaluates the spec's per-channel 2D Perlin noise function <c>noise2()</c> at one
        ///     point, using this instance's own <c>noStitch</c> tables (see
        ///     <see cref="ApplyFeTurbulence"/>'s remarks for the documented <c>stitchTiles</c> scope
        ///     decision).
        /// </summary>
        /// <param name="channel">The channel index (0=R, 1=G, 2=B, 3=A).</param>
        /// <param name="vecX">The X component of the (possibly octave-scaled) sample point.</param>
        /// <param name="vecY">The Y component of the (possibly octave-scaled) sample point.</param>
        /// <returns>The noise value at the given point, typically in roughly <c>[-1, 1]</c>.</returns>
        private float Noise2(int channel, float vecX, float vecY)
        {
            var t = vecX + PerlinN;
            var bx0 = (int)t & BM;
            var bx1 = (bx0 + 1) & BM;
            var rx0 = t - (int)t;
            var rx1 = rx0 - 1f;

            t = vecY + PerlinN;
            var by0 = (int)t & BM;
            var by1 = (by0 + 1) & BM;
            var ry0 = t - (int)t;
            var ry1 = ry0 - 1f;

            var i = _latticeSelector[bx0];
            var j = _latticeSelector[bx1];

            var b00 = _latticeSelector[i + by0];
            var b10 = _latticeSelector[j + by0];
            var b01 = _latticeSelector[i + by1];
            var b11 = _latticeSelector[j + by1];

            var sx = SCurve(rx0);
            var sy = SCurve(ry0);

            var u = (rx0 * _gradient[channel, b00, 0]) + (ry0 * _gradient[channel, b00, 1]);
            var v = (rx1 * _gradient[channel, b10, 0]) + (ry0 * _gradient[channel, b10, 1]);
            var a = Lerp(sx, u, v);

            u = (rx0 * _gradient[channel, b01, 0]) + (ry1 * _gradient[channel, b01, 1]);
            v = (rx1 * _gradient[channel, b11, 0]) + (ry1 * _gradient[channel, b11, 1]);
            var b = Lerp(sx, u, v);

            return Lerp(sy, a, b);
        }

        /// <summary>
        ///     Evaluates the spec's <c>turbulence()</c> reference function: an octave-summed,
        ///     frequency-doubling accumulation of <see cref="Noise2"/>.
        /// </summary>
        /// <param name="channel">The channel index (0=R, 1=G, 2=B, 3=A).</param>
        /// <param name="pointX">The local-space X coordinate.</param>
        /// <param name="pointY">The local-space Y coordinate.</param>
        /// <param name="baseFrequencyX">The <c>baseFrequency</c> X component.</param>
        /// <param name="baseFrequencyY">The <c>baseFrequency</c> Y component.</param>
        /// <param name="numOctaves">The number of octaves to accumulate.</param>
        /// <param name="fractalSum">
        ///     <see langword="true"/> to accumulate signed noise values (<c>type="fractalNoise"</c>);
        ///     <see langword="false"/> to accumulate their absolute value (<c>type="turbulence"</c>).
        /// </param>
        /// <returns>The accumulated, unclamped turbulence value.</returns>
        public float Turbulence(int channel, float pointX, float pointY, float baseFrequencyX, float baseFrequencyY, int numOctaves, bool fractalSum)
        {
            var vecX = pointX * baseFrequencyX;
            var vecY = pointY * baseFrequencyY;
            var sum = 0f;
            var ratio = 1f;

            for (var octave = 0; octave < numOctaves; octave++)
            {
                var n = Noise2(channel, vecX, vecY);
                sum += (fractalSum ? n : MathF.Abs(n)) / ratio;
                vecX *= 2f;
                vecY *= 2f;
                ratio *= 2f;
            }

            return sum;
        }
    }
}
