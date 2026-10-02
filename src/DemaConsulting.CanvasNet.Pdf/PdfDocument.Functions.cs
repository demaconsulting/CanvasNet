// cspell:ignore functiontype bitspersample multiinput
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     A resolved, single-input, <c>N</c>-output PDF function (any of the 3 supported
    ///     <c>/FunctionType</c>s: <c>0</c>/<see cref="SampledFunction"/>, <c>2</c>/
    ///     <see cref="ExponentialFunction"/>, <c>3</c>/<see cref="StitchingFunction"/>) - the
    ///     common shape <see cref="ResolveFunctionOrFunctionArray"/> (<c>PdfDocument.Patterns.Shading.cs</c>)
    ///     evaluates against a shading pattern's own <c>/Function</c> entry.
    /// </summary>
    internal interface IPdfFunction
    {
        /// <summary>The number of outputs <c>N</c> this function produces.</summary>
        int OutputCount { get; }

        /// <summary>Evaluates this function at <paramref name="input"/>, returning one value per output (<see cref="OutputCount"/> elements).</summary>
        double[] Evaluate(double input);
    }

    /// <summary>
    ///     A resolved PDF <c>/FunctionType 0</c> (sampled) function: 1 input, <c>N</c> outputs,
    ///     evaluated by clamping the input to <c>/Domain</c>, mapping it into sample-index space
    ///     via <c>/Encode</c>, linearly interpolating between the two nearest samples (matching
    ///     this narrow evaluator's implicit <c>/Order 1</c> assumption - see
    ///     <see cref="ResolveFunction"/>'s remarks), and mapping each interpolated output through
    ///     <c>/Decode</c>, clipped to <c>/Range</c>.
    /// </summary>
    /// <remarks>
    ///     Consumed by <c>PdfDocument.Patterns.Shading.cs</c>'s <c>ResolveFunctionOrFunctionArray</c>
    ///     (via <see cref="IPdfFunction"/>) when a shading pattern's own <c>/Function</c> is a
    ///     <c>/FunctionType 0</c> function, and directly by callers that only need a sampled
    ///     function on its own (for example <see cref="ResolveFunction"/>'s own existing callers).
    /// </remarks>
    internal sealed class SampledFunction : IPdfFunction
    {
        /// <summary>The single input's valid range (2 elements: <c>[min, max]</c>), values are clamped into this range before evaluation.</summary>
        internal required double[] Domain { get; init; }

        /// <summary>The <c>N</c> outputs' valid ranges (<c>2N</c> elements: <c>[min0, max0, min1, max1, ...]</c>), the final evaluated value of each output is clipped into its own range.</summary>
        internal required double[] Range { get; init; }

        /// <summary>The <c>N</c> outputs' raw-sample-to-output-value linear mapping (<c>2N</c> elements), defaults to <see cref="Range"/> when <c>/Decode</c> is absent.</summary>
        internal required double[] Decode { get; init; }

        /// <summary>The single input's domain-to-sample-index linear mapping (2 elements: <c>[0, Size - 1]</c> when <c>/Encode</c> is absent).</summary>
        internal required double[] Encode { get; init; }

        /// <summary>The number of samples along the single input dimension (<c>/Size</c>'s one element, since this function has exactly 1 input).</summary>
        internal required int Size { get; init; }

        /// <summary>The number of bits each raw sample value occupies (<c>8</c> or <c>16</c>, the only two widths this narrow evaluator supports).</summary>
        internal required int BitsPerSample { get; init; }

        /// <summary>The function stream's fully filter-decoded sample bytes, packed <c>Size</c> sample groups of <c>OutputCount</c> <see cref="BitsPerSample"/>-wide big-endian values each, with no padding between groups or samples (per the PDF specification's own packing rule for a 1-input sampled function).</summary>
        internal required byte[] Samples { get; init; }

        /// <summary>The number of outputs <c>N</c>, derived from <see cref="Range"/>'s own length.</summary>
        internal int OutputCount => Range.Length / 2;

        /// <inheritdoc/>
        int IPdfFunction.OutputCount => OutputCount;

        /// <summary>
        ///     Evaluates this sampled function at <paramref name="input"/>, returning one value
        ///     per output (<see cref="OutputCount"/> elements).
        /// </summary>
        /// <param name="input">The single input value; clamped into <see cref="Domain"/> before evaluation.</param>
        /// <returns>The evaluated, <see cref="Decode"/>-mapped, <see cref="Range"/>-clipped output values.</returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown when <see cref="Samples"/> is too short to supply every sample this
        ///     evaluation needs (a malformed/truncated function stream).
        /// </exception>
        internal double[] Evaluate(double input)
        {
            var clampedInput = Math.Clamp(input, Math.Min(Domain[0], Domain[1]), Math.Max(Domain[0], Domain[1]));
            var encoded = Interpolate(clampedInput, Domain[0], Domain[1], Encode[0], Encode[1]);
            var sampleIndex = Math.Clamp(encoded, 0, Size - 1);

            var lowIndex = (int)Math.Floor(sampleIndex);
            var highIndex = Math.Min(lowIndex + 1, Size - 1);
            var fraction = sampleIndex - lowIndex;

            var maxSampleValue = (double)((1UL << BitsPerSample) - 1);
            var outputCount = OutputCount;
            var outputs = new double[outputCount];
            for (var outputIndex = 0; outputIndex < outputCount; outputIndex++)
            {
                var rawLow = ReadSample(lowIndex, outputIndex, outputCount);
                var rawHigh = ReadSample(highIndex, outputIndex, outputCount);
                var raw = rawLow + (fraction * (rawHigh - rawLow));

                var decoded = Interpolate(
                    raw, 0, maxSampleValue, Decode[2 * outputIndex], Decode[(2 * outputIndex) + 1]);

                var rangeMin = Math.Min(Range[2 * outputIndex], Range[(2 * outputIndex) + 1]);
                var rangeMax = Math.Max(Range[2 * outputIndex], Range[(2 * outputIndex) + 1]);
                outputs[outputIndex] = Math.Clamp(decoded, rangeMin, rangeMax);
            }

            return outputs;
        }

        /// <inheritdoc/>
        double[] IPdfFunction.Evaluate(double input) => Evaluate(input);

        /// <summary>
        ///     Reads a single raw (not yet <see cref="Decode"/>-mapped) sample value: the
        ///     <paramref name="outputIndex"/>'th <see cref="BitsPerSample"/>-wide big-endian
        ///     value within the <paramref name="sampleGroupIndex"/>'th sample group.
        /// </summary>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the requested bits extend past the end of <see cref="Samples"/>.
        /// </exception>
        private double ReadSample(int sampleGroupIndex, int outputIndex, int outputCount)
        {
            var bitOffset = (long)((sampleGroupIndex * outputCount) + outputIndex) * BitsPerSample;
            return ReadBits(Samples, bitOffset, BitsPerSample);
        }

        /// <summary>
        ///     Reads <paramref name="bitCount"/> bits (most-significant bit first, per the PDF
        ///     specification's own sample-packing convention) starting at bit offset
        ///     <paramref name="bitOffset"/> within <paramref name="data"/>, generically -
        ///     supporting any bit width, even though <see cref="BitsPerSample"/> itself is
        ///     restricted (by <see cref="ResolveFunction"/>) to <c>8</c> or <c>16</c>.
        /// </summary>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the requested bit range extends past the end of <paramref name="data"/>.
        /// </exception>
        private static long ReadBits(byte[] data, long bitOffset, int bitCount)
        {
            var lastBitIndex = bitOffset + bitCount - 1;
            if (lastBitIndex / 8 >= data.Length)
            {
                throw new InvalidDataException(
                    "Sampled function stream does not contain enough sample bytes for its declared /Size.");
            }

            long value = 0;
            for (var i = 0; i < bitCount; i++)
            {
                var bitIndex = bitOffset + i;
                var byteIndex = (int)(bitIndex / 8);
                var bitInByte = 7 - (int)(bitIndex % 8);
                var bit = (long)((data[byteIndex] >> bitInByte) & 1);
                value = (value << 1) | bit;
            }

            return value;
        }

        /// <summary>Linearly maps <paramref name="value"/> from <c>[xMin, xMax]</c> into <c>[yMin, yMax]</c>.</summary>
        private static double Interpolate(double value, double xMin, double xMax, double yMin, double yMax)
        {
            var span = xMax - xMin;
            return Math.Abs(span) < double.Epsilon ? yMin : yMin + ((value - xMin) * (yMax - yMin) / span);
        }
    }

    /// <summary>
    ///     A resolved PDF <c>/FunctionType 2</c> (exponential interpolation) function: 1 input,
    ///     <c>N</c> outputs, evaluated as <c>C0 + input^N * (C1 - C0)</c> (PDF 32000-1 §7.10.3)
    ///     after clamping the input value into <see cref="Domain"/>.
    /// </summary>
    internal sealed class ExponentialFunction : IPdfFunction
    {
        /// <summary>The single input's valid range (2 elements: <c>[min, max]</c>).</summary>
        internal required double[] Domain { get; init; }

        /// <summary>The function's output value(s) when <c>input == 0</c>. Defaults to a single <c>0.0</c> element when <c>/C0</c> is absent.</summary>
        internal required double[] C0 { get; init; }

        /// <summary>The function's output value(s) when <c>input == 1</c>. Defaults to a single <c>1.0</c> element when <c>/C1</c> is absent.</summary>
        internal required double[] C1 { get; init; }

        /// <summary>The interpolation exponent <c>/N</c>.</summary>
        internal required double N { get; init; }

        /// <inheritdoc/>
        public int OutputCount => C0.Length;

        /// <inheritdoc/>
        public double[] Evaluate(double input)
        {
            var clampedInput = Math.Clamp(input, Math.Min(Domain[0], Domain[1]), Math.Max(Domain[0], Domain[1]));
            var raised = Math.Pow(clampedInput, N);

            var outputs = new double[OutputCount];
            for (var i = 0; i < outputs.Length; i++)
            {
                outputs[i] = C0[i] + (raised * (C1[i] - C0[i]));
            }

            return outputs;
        }
    }

    /// <summary>
    ///     A resolved PDF <c>/FunctionType 3</c> (stitching) function: 1 input, <c>N</c> outputs,
    ///     partitioning <see cref="Domain"/> into <c>k</c> sub-domains (<see cref="Bounds"/>'
    ///     <c>k - 1</c> interior boundaries) and delegating evaluation to the corresponding
    ///     sub-<see cref="IPdfFunction"/> in <see cref="Functions"/>, after remapping the input
    ///     into that sub-domain's own <c>/Encode</c>-declared range (PDF 32000-1 §7.10.4).
    /// </summary>
    internal sealed class StitchingFunction : IPdfFunction
    {
        /// <summary>The single input's valid range (2 elements: <c>[min, max]</c>).</summary>
        internal required double[] Domain { get; init; }

        /// <summary>The <c>k</c> sub-functions, each resolved via <see cref="ResolveFunctionGeneric"/>.</summary>
        internal required IReadOnlyList<IPdfFunction> Functions { get; init; }

        /// <summary>The <c>k - 1</c> interior sub-domain boundaries, in increasing order.</summary>
        internal required double[] Bounds { get; init; }

        /// <summary>The <c>2k</c>-element input-remapping array: sub-domain <c>i</c> maps linearly to <c>[Encode[2i], Encode[2i + 1]]</c>.</summary>
        internal required double[] Encode { get; init; }

        /// <inheritdoc/>
        public int OutputCount => Functions.Count > 0 ? Functions[0].OutputCount : 0;

        /// <inheritdoc/>
        public double[] Evaluate(double input)
        {
            var clampedInput = Math.Clamp(input, Math.Min(Domain[0], Domain[1]), Math.Max(Domain[0], Domain[1]));

            var k = Functions.Count;
            var subIndex = 0;
            while (subIndex < Bounds.Length && clampedInput >= Bounds[subIndex])
            {
                subIndex++;
            }

            subIndex = Math.Clamp(subIndex, 0, k - 1);

            var lowBound = subIndex == 0 ? Domain[0] : Bounds[subIndex - 1];
            var highBound = subIndex == k - 1 ? Domain[1] : Bounds[subIndex];

            var encodedLow = Encode[2 * subIndex];
            var encodedHigh = Encode[(2 * subIndex) + 1];

            var span = highBound - lowBound;
            var encodedInput = Math.Abs(span) < double.Epsilon
                ? encodedLow
                : encodedLow + ((clampedInput - lowBound) * (encodedHigh - encodedLow) / span);

            return Functions[subIndex].Evaluate(encodedInput);
        }
    }

    /// <summary>
    ///     The maximum number of nested <c>/Function</c> resolutions <see cref="ResolveFunctionGeneric"/>
    ///     allows before failing closed. A malformed or adversarial PDF can declare a
    ///     <c>/FunctionType 3</c> stitching function whose own <c>/Functions</c> array references
    ///     itself (directly, or via a cycle of several functions); without this bound such a
    ///     document would recurse until the process' call stack is exhausted, raising an
    ///     unrecoverable <see cref="StackOverflowException"/> that crashes the whole process rather
    ///     than failing this document open/render call alone.
    /// </summary>
    private const int MaxFunctionRecursionDepth = 32;

    /// <summary>
    ///     The current <c>/Function</c> resolution recursion depth, incremented/decremented around
    ///     every <see cref="ResolveFunctionGeneric"/> call.
    /// </summary>
    private int _functionRecursionDepth;

    /// <summary>
    ///     Resolves a <c>/Function</c> entry (<c>/FunctionType 0</c>/<c>2</c>/<c>3</c>, the 3
    ///     single-input function types a shading pattern's own <c>/Function</c> entry may use)
    ///     into the common <see cref="IPdfFunction"/> shape.
    /// </summary>
    /// <param name="functionEntry">The (possibly indirect-reference) <c>/Function</c> entry to resolve.</param>
    /// <returns>The resolved function.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved value is not a dictionary/stream, or when a required entry is
    ///     missing or malformed - propagated from the type-specific resolver - or when the
    ///     function resolves back to itself (directly or via a cycle of several sub-functions), or
    ///     otherwise nests deeper than <see cref="MaxFunctionRecursionDepth"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/FunctionType</c> is not <c>0</c>, <c>2</c>, or <c>3</c> (feature
    ///     <c>pdf-functiontype-{n}</c>), or propagated from the type-specific resolver for a
    ///     further-unsupported shape (for example a multi-input <c>/FunctionType 0</c> function).
    /// </exception>
    internal IPdfFunction ResolveFunctionGeneric(PdfObject functionEntry)
    {
        if (_functionRecursionDepth >= MaxFunctionRecursionDepth)
        {
            throw new InvalidDataException(
                $"/Function resolution exceeds the maximum supported nesting depth of {MaxFunctionRecursionDepth}.");
        }

        _functionRecursionDepth++;
        try
        {
            var function = Resolve(functionEntry);
            var functionType = RequireIntEntry(function, "FunctionType");
            return functionType switch
            {
                0 => ResolveFunction(functionEntry),
                2 => ResolveExponentialFunction(function),
                3 => ResolveStitchingFunction(function),
                _ => throw new UnsupportedImageFeatureException(
                    $"pdf-functiontype-{functionType}",
                    $"/FunctionType {functionType} is not supported; only /FunctionType 0 (sampled), " +
                    "2 (exponential), and 3 (stitching) are supported."),
            };
        }
        finally
        {
            _functionRecursionDepth--;
        }
    }

    /// <summary>Resolves an already-resolved <c>/FunctionType 2</c> dictionary into an <see cref="ExponentialFunction"/>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Domain</c> is missing/malformed, or <c>/C0</c>/<c>/C1</c> (when
    ///     present) do not have the same length as one another.
    /// </exception>
    private ExponentialFunction ResolveExponentialFunction(PdfObject function)
    {
        var domain = RequireNumberArray(function, "Domain");
        if (domain.Length != 2)
        {
            throw new InvalidDataException("/Domain must have exactly 2 elements for a 1-input function.");
        }

        var c0 = ResolveOptionalNumberArray(function, "C0") ?? [0.0];
        var c1 = ResolveOptionalNumberArray(function, "C1") ?? [1.0];
        if (c0.Length != c1.Length)
        {
            throw new InvalidDataException("/C0 and /C1 must have the same number of elements.");
        }

        var n = RequireNumberEntry(function, "N");

        return new ExponentialFunction
        {
            Domain = domain,
            C0 = c0,
            C1 = c1,
            N = n,
        };
    }

    /// <summary>Resolves an already-resolved <c>/FunctionType 3</c> dictionary into a <see cref="StitchingFunction"/>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Domain</c>/<c>/Functions</c>/<c>/Bounds</c>/<c>/Encode</c> are
    ///     missing/malformed, or their element counts do not match the PDF specification's own
    ///     required shape (<c>/Bounds</c> has <c>k - 1</c> elements and <c>/Encode</c> has
    ///     <c>2k</c> elements, for <c>k == /Functions</c>' element count).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="ResolveFunctionGeneric"/> for an unsupported sub-function.
    /// </exception>
    private StitchingFunction ResolveStitchingFunction(PdfObject function)
    {
        var domain = RequireNumberArray(function, "Domain");
        if (domain.Length != 2)
        {
            throw new InvalidDataException("/Domain must have exactly 2 elements for a 1-input function.");
        }

        var functionsEntry = function.Get("Functions") ?? throw new InvalidDataException("/FunctionType 3 is missing required /Functions.");
        var functionsArray = Resolve(functionsEntry);
        if (functionsArray.Kind != PdfKind.Array || functionsArray.Items.Count == 0)
        {
            throw new InvalidDataException("/Functions must be a non-empty array.");
        }

        var functions = functionsArray.Items.Select(ResolveFunctionGeneric).ToArray();
        var k = functions.Length;

        var bounds = ResolveOptionalNumberArray(function, "Bounds") ?? [];
        if (bounds.Length != k - 1)
        {
            throw new InvalidDataException($"/Bounds must have exactly {k - 1} element(s) for {k} sub-function(s).");
        }

        var encode = RequireNumberArray(function, "Encode");
        if (encode.Length != 2 * k)
        {
            throw new InvalidDataException($"/Encode must have exactly {2 * k} element(s) for {k} sub-function(s).");
        }

        return new StitchingFunction
        {
            Domain = domain,
            Functions = functions,
            Bounds = bounds,
            Encode = encode,
        };
    }

    /// <summary>Reads a required numeric dictionary entry (resolving an indirect reference).</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the entry is absent or does not resolve to a number.
    /// </exception>
    private double RequireNumberEntry(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key) ?? throw new InvalidDataException($"Dictionary is missing required /{key}.");
        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Number)
        {
            throw new InvalidDataException($"/{key} must be a number.");
        }

        return resolved.Number;
    }

    /// <summary>
    ///     Resolves a <c>/Function</c> entry restricted to <c>/FunctionType 0</c> (sampled
    ///     function) into a <see cref="SampledFunction"/>, decoding its stream bytes via
    ///     <see cref="GetStreamDecodedBytes"/> (already filter-aware - no new filter code needed).
    /// </summary>
    /// <remarks>
    ///     Only a single-input (<c>/Domain</c> a 2-element array) <c>/FunctionType 0</c> function
    ///     is supported - the shape every axial/radial (<c>/ShadingType 2</c>/<c>3</c>) shading
    ///     function uses. A multi-input <c>/FunctionType 0</c> function (used for
    ///     <c>/DeviceN</c>/<c>/Separation</c> tint transforms, themselves already out of scope -
    ///     see <c>PdfDocument.Color.cs</c>) is rejected, as are <c>/FunctionType</c> <c>2</c>
    ///     (exponential interpolation), <c>3</c> (stitching), and <c>4</c> (PostScript
    ///     calculator) - none of which occur in this evaluator's own motivating real-world PDF.
    ///     <c>/Order</c> is never consulted: this evaluator always linearly interpolates between
    ///     the two nearest samples, matching every sampled function this evaluator's motivating
    ///     PDF declares (each has <c>/Order</c> absent or <c>1</c>).
    /// </remarks>
    /// <param name="functionEntry">
    ///     The (possibly indirect-reference) <c>/Function</c> entry to resolve.
    /// </param>
    /// <returns>The resolved sampled function.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved value is not a stream, or when a required entry
    ///     (<c>/Domain</c>, <c>/Range</c>, <c>/Size</c>, <c>/BitsPerSample</c>) is missing or
    ///     malformed.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/FunctionType</c> is not <c>0</c> (feature
    ///     <c>pdf-functiontype-{n}</c>), when <c>/Domain</c> does not have exactly 2 elements (a
    ///     multi-input function, feature <c>pdf-function-multiinput</c>), or when
    ///     <c>/BitsPerSample</c> is not <c>8</c> or <c>16</c> (feature
    ///     <c>pdf-function-bitspersample-{n}</c>).
    /// </exception>
    internal SampledFunction ResolveFunction(PdfObject functionEntry)
    {
        var function = Resolve(functionEntry);
        if (function.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/Function must resolve to a stream (only /FunctionType 0 is supported).");
        }

        var functionType = RequireIntEntry(function, "FunctionType");
        if (functionType != 0)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-functiontype-{functionType}",
                $"/FunctionType {functionType} is not supported; only /FunctionType 0 (sampled) is supported.");
        }

        var domain = RequireNumberArray(function, "Domain");
        if (domain.Length != 2)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-function-multiinput",
                "A /FunctionType 0 function with more than 1 input (a /Domain with more than 2 " +
                "elements) is not supported.");
        }

        var range = RequireNumberArray(function, "Range");
        if (range.Length == 0 || range.Length % 2 != 0)
        {
            throw new InvalidDataException("/Range must be a non-empty array of an even number of elements.");
        }

        var sizeArray = RequireNumberArray(function, "Size");
        if (sizeArray.Length != 1)
        {
            throw new InvalidDataException("/Size must have exactly 1 element for a 1-input function.");
        }

        var size = (int)sizeArray[0];
        if (size < 1)
        {
            throw new InvalidDataException("/Size must be a positive integer.");
        }

        var bitsPerSample = RequireIntEntry(function, "BitsPerSample");
        if (bitsPerSample is not (8 or 16))
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-function-bitspersample-{bitsPerSample}",
                $"/BitsPerSample {bitsPerSample} is not supported; only 8 and 16 are supported.");
        }

        var encode = ResolveOptionalNumberArray(function, "Encode") ?? [0, size - 1];
        if (encode.Length != 2)
        {
            throw new InvalidDataException("/Encode must have exactly 2 elements for a 1-input function.");
        }

        var decode = ResolveOptionalNumberArray(function, "Decode") ?? range;
        if (decode.Length != range.Length)
        {
            throw new InvalidDataException("/Decode must have the same number of elements as /Range.");
        }

        var samples = GetStreamDecodedBytes(function);

        return new SampledFunction
        {
            Domain = domain,
            Range = range,
            Decode = decode,
            Encode = encode,
            Size = size,
            BitsPerSample = bitsPerSample,
            Samples = samples,
        };
    }

    /// <summary>Reads a required numeric-array dictionary entry (resolving an indirect reference and resolving each element).</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the entry is absent, does not resolve to an array, or any element does not
    ///     resolve to a number.
    /// </exception>
    private double[] RequireNumberArray(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key) ?? throw new InvalidDataException($"Stream is missing required /{key}.");
        return ResolveNumberArray(entry, key);
    }

    /// <summary>Reads an optional numeric-array dictionary entry, or <see langword="null"/> when absent.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the entry is present but does not resolve to an array, or any element does
    ///     not resolve to a number.
    /// </exception>
    private double[]? ResolveOptionalNumberArray(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key);
        return entry is null ? null : ResolveNumberArray(entry, key);
    }

    /// <summary>Resolves an already-looked-up entry into a numeric array, resolving each element.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved value is not an array, or any element does not resolve to a
    ///     number.
    /// </exception>
    private double[] ResolveNumberArray(PdfObject entry, string key)
    {
        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Array)
        {
            throw new InvalidDataException($"/{key} must be an array.");
        }

        var result = new double[resolved.Items.Count];
        for (var i = 0; i < resolved.Items.Count; i++)
        {
            var item = Resolve(resolved.Items[i]);
            if (item.Kind != PdfKind.Number)
            {
                throw new InvalidDataException($"/{key} array elements must all be numbers.");
            }

            result[i] = item.Number;
        }

        return result;
    }
}
