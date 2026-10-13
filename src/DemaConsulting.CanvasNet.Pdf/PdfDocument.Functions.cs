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

        /// <summary>
        ///     Gets the interior input values at which the function may be discontinuous or change
        ///     slope abruptly (the <c>/Bounds</c> of a stitching function, including those of nested
        ///     stitching sub-functions), in increasing order; empty for smooth functions. Shading
        ///     sampling adds exact hard stops at these values so sharp transitions are not smeared
        ///     between uniform samples.
        /// </summary>
        IReadOnlyList<double> Breakpoints => [];
    }

    /// <summary>
    ///     A resolved PDF function with any number of inputs (<c>m</c>) and outputs (<c>n</c>),
    ///     as used by <c>Separation</c>/<c>DeviceN</c> tint transforms: the multi-input sibling of
    ///     <see cref="IPdfFunction"/>, evaluated without per-call allocation.
    /// </summary>
    internal interface IPdfMultiFunction
    {
        /// <summary>The number of inputs <c>m</c>.</summary>
        int InputCount { get; }

        /// <summary>The number of outputs <c>n</c>.</summary>
        int OutputCount { get; }

        /// <summary>
        ///     Evaluates the function: reads <see cref="InputCount"/> values from
        ///     <paramref name="input"/> and writes <see cref="OutputCount"/> values to
        ///     <paramref name="output"/>.
        /// </summary>
        void Evaluate(ReadOnlySpan<double> input, Span<double> output);

        /// <summary>
        ///     An upper-bound estimate of the work of one <see cref="Evaluate"/> call in abstract
        ///     cost units (roughly one unit per elementary operation), used to budget per-pixel
        ///     tint evaluation.
        /// </summary>
        long EstimatedCost => 1L + InputCount + OutputCount;
    }

    /// <summary>Adapts a single-input <see cref="IPdfFunction"/> (type 2 or 3) to <see cref="IPdfMultiFunction"/>.</summary>
    private sealed class SingleInputFunctionAdapter(IPdfFunction function) : IPdfMultiFunction
    {
        /// <inheritdoc/>
        public int InputCount => 1;

        /// <inheritdoc/>
        public int OutputCount => function.OutputCount;

        /// <inheritdoc/>
        public void Evaluate(ReadOnlySpan<double> input, Span<double> output)
        {
            var result = function.Evaluate(input[0]);
            result.AsSpan(0, Math.Min(result.Length, output.Length)).CopyTo(output);
        }
    }

    /// <summary>The maximum number of inputs of a multi-input sampled (<c>/FunctionType 0</c>) function.</summary>
    private const int MaxSampledFunctionInputs = 8;

    /// <summary>The maximum total number of samples (<c>product(Size) * outputs</c>) a sampled function may declare.</summary>
    private const long MaxSampledFunctionSamples = 1L << 22;

    /// <summary>The maximum number of outputs of a multi-input function.</summary>
    private const int MaxFunctionOutputs = 32;

    /// <summary>
    ///     A resolved <c>/FunctionType 0</c> (sampled) function with <c>m &gt;= 1</c> inputs,
    ///     evaluated by multilinear interpolation (<c>2^m</c> corners) between the samples
    ///     (<c>/Order 1</c>), with <c>/Encode</c>, <c>/Decode</c> and <c>/Range</c> applied; supports
    ///     <c>/BitsPerSample</c> <c>1, 2, 4, 8, 12, 16, 24, 32</c>.
    /// </summary>
    internal sealed class MultiSampledFunction : IPdfMultiFunction
    {
        /// <summary>The <c>2m</c>-element input domain.</summary>
        internal required double[] Domain { get; init; }

        /// <summary>The <c>2n</c>-element output range.</summary>
        internal required double[] Range { get; init; }

        /// <summary>The <c>2m</c>-element input-to-sample-index mapping.</summary>
        internal required double[] Encode { get; init; }

        /// <summary>The <c>2n</c>-element raw-sample-to-output mapping.</summary>
        internal required double[] Decode { get; init; }

        /// <summary>The number of samples along each input dimension.</summary>
        internal required int[] Size { get; init; }

        /// <summary>The bits per raw sample.</summary>
        internal required int BitsPerSample { get; init; }

        /// <summary>The filter-decoded sample bytes (first input varies fastest).</summary>
        internal required byte[] Samples { get; init; }

        /// <inheritdoc/>
        public int InputCount => Size.Length;

        /// <inheritdoc/>
        public int OutputCount => Range.Length / 2;

        /// <inheritdoc/>
        public long EstimatedCost => ((1L << Size.Length) * (Size.Length + OutputCount) * 4) + 16;

        /// <inheritdoc/>
        public void Evaluate(ReadOnlySpan<double> input, Span<double> output)
        {
            var m = Size.Length;
            var n = OutputCount;
            Span<int> low = stackalloc int[MaxSampledFunctionInputs];
            Span<int> high = stackalloc int[MaxSampledFunctionInputs];
            Span<double> fraction = stackalloc double[MaxSampledFunctionInputs];
            Span<long> stride = stackalloc long[MaxSampledFunctionInputs];

            long running = 1;
            for (var i = 0; i < m; i++)
            {
                var d0 = Domain[2 * i];
                var d1 = Domain[(2 * i) + 1];
                var x = Math.Clamp(i < input.Length ? input[i] : 0.0, Math.Min(d0, d1), Math.Max(d0, d1));
                var span = d1 - d0;
                var encoded = Math.Abs(span) < double.Epsilon
                    ? Encode[2 * i]
                    : Encode[2 * i] + ((x - d0) * (Encode[(2 * i) + 1] - Encode[2 * i]) / span);
                encoded = Math.Clamp(encoded, 0, Size[i] - 1);

                low[i] = (int)Math.Floor(encoded);
                high[i] = Math.Min(low[i] + 1, Size[i] - 1);
                fraction[i] = encoded - low[i];
                stride[i] = running;
                running *= Size[i];
            }

            output[..n].Clear();
            for (var corner = 0; corner < (1 << m); corner++)
            {
                double weight = 1.0;
                long index = 0;
                for (var i = 0; i < m; i++)
                {
                    if (((corner >> i) & 1) == 1)
                    {
                        weight *= fraction[i];
                        index += high[i] * stride[i];
                    }
                    else
                    {
                        weight *= 1.0 - fraction[i];
                        index += low[i] * stride[i];
                    }
                }

                if (weight == 0.0)
                {
                    continue;
                }

                for (var j = 0; j < n; j++)
                {
                    output[j] += weight * ReadSample((index * n) + j);
                }
            }

            var maxRaw = (double)((1UL << BitsPerSample) - 1);
            for (var j = 0; j < n; j++)
            {
                var decoded = Decode[2 * j] + (output[j] * (Decode[(2 * j) + 1] - Decode[2 * j]) / maxRaw);
                output[j] = Math.Clamp(
                    decoded,
                    Math.Min(Range[2 * j], Range[(2 * j) + 1]),
                    Math.Max(Range[2 * j], Range[(2 * j) + 1]));
            }
        }

        /// <summary>Reads the raw sample at the given flat sample index (most-significant bit first).</summary>
        /// <remarks>Byte-aligned widths (8/16/24/32 bits) take a direct big-endian fast path; the rest use the bitwise path.</remarks>
        internal double ReadSample(long sampleIndex)
        {
            var bitOffset = sampleIndex * BitsPerSample;
            if (((bitOffset + BitsPerSample - 1) >> 3) >= Samples.Length)
            {
                throw new InvalidDataException(
                    "Sampled function stream does not contain enough sample bytes for its declared /Size.");
            }

            if ((BitsPerSample & 7) == 0)
            {
                var at = (int)(bitOffset >> 3);
                long fast = 0;
                for (var i = 0; i < BitsPerSample >> 3; i++)
                {
                    fast = (fast << 8) | Samples[at + i];
                }

                return fast;
            }

            return ReadSampleBitwise(bitOffset);
        }

        /// <summary>Reads one raw sample bit by bit (any width).</summary>
        internal double ReadSampleBitwise(long bitOffset)
        {
            long value = 0;
            for (var i = 0; i < BitsPerSample; i++)
            {
                var bit = bitOffset + i;
                value = (value << 1) | (long)((Samples[(int)(bit >> 3)] >> (7 - (int)(bit & 7))) & 1);
            }

            return value;
        }
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
        ///     Thrown when the requested bits extend past the end of <see cref="Samples"/>, or
        ///     when the sample-table dimensions overflow during bit-offset computation.
        /// </exception>
        private double ReadSample(int sampleGroupIndex, int outputIndex, int outputCount)
        {
            long bitOffset;
            try
            {
                bitOffset = checked((((long)sampleGroupIndex * outputCount) + outputIndex) * (long)BitsPerSample);
            }
            catch (OverflowException ex)
            {
                throw new InvalidDataException(
                    "Sampled function sample-table dimensions overflow during bit-offset computation.", ex);
            }

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
        /// <remarks>
        ///     The interior <see cref="Bounds"/> plus, for each sub-function, its own breakpoints
        ///     mapped back from its <see cref="Encode"/>d range into this function's input domain.
        /// </remarks>
        public IReadOnlyList<double> Breakpoints
        {
            get
            {
                var result = new SortedSet<double>(Bounds.Where(double.IsFinite));
                var k = Functions.Count;
                for (var i = 0; i < k; i++)
                {
                    var subBreakpoints = Functions[i].Breakpoints;
                    if (subBreakpoints.Count == 0)
                    {
                        continue;
                    }

                    var low = i == 0 ? Domain[0] : Bounds[i - 1];
                    var high = i == k - 1 ? Domain[1] : Bounds[i];
                    var encodedLow = Encode[2 * i];
                    var encodedSpan = Encode[(2 * i) + 1] - encodedLow;
                    if (Math.Abs(encodedSpan) < double.Epsilon)
                    {
                        continue;
                    }

                    foreach (var encoded in subBreakpoints)
                    {
                        var mapped = low + ((encoded - encodedLow) * (high - low) / encodedSpan);
                        if (double.IsFinite(mapped) && mapped > Math.Min(low, high) && mapped < Math.Max(low, high))
                        {
                            result.Add(mapped);
                        }
                    }
                }

                return [.. result];
            }
        }

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
            var functionType = RequireStrictIntEntry(function, "FunctionType");
            return functionType switch
            {
                0 => ResolveFunction(functionEntry),
                2 => ResolveExponentialFunction(function),
                3 => ResolveStitchingFunction(function),
                4 => ResolvePostScriptFunction(function) is { InputCount: 1 } postScript
                    ? postScript
                    : throw new UnsupportedImageFeatureException(
                        "pdf-function-multiinput",
                        "A /FunctionType 4 function with more than 1 input is only supported as a Separation/DeviceN tint transform."),
                _ => throw new UnsupportedImageFeatureException(
                    $"pdf-functiontype-{functionType}",
                    $"/FunctionType {functionType} is not supported; only /FunctionType 0 (sampled), " +
                    "2 (exponential), 3 (stitching) and 4 (PostScript calculator) are supported."),
            };
        }
        finally
        {
            _functionRecursionDepth--;
        }
    }

    /// <summary>
    ///     Resolves a <c>/Function</c> entry into the multi-input <see cref="IPdfMultiFunction"/>
    ///     shape used by <c>Separation</c>/<c>DeviceN</c> tint transforms: <c>/FunctionType 0</c>
    ///     with any number of inputs (up to <see cref="MaxSampledFunctionInputs"/>), <c>2</c>/<c>3</c>
    ///     (single input) and <c>4</c> (PostScript calculator).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown for a malformed function, an oversized sample table, or nesting deeper than
    ///     <see cref="MaxFunctionRecursionDepth"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown for an unsupported <c>/FunctionType</c> (feature <c>pdf-functiontype-{n}</c>), too many
    ///     sampled-function inputs (feature <c>pdf-function-multiinput</c>) or an unsupported
    ///     <c>/BitsPerSample</c> (feature <c>pdf-function-bitspersample-{n}</c>).
    /// </exception>
    private IPdfMultiFunction ResolveMultiInputFunction(PdfObject functionEntry)
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
            var functionType = RequireStrictIntEntry(function, "FunctionType");
            return functionType switch
            {
                0 => ResolveMultiSampledFunction(function),
                4 => ResolvePostScriptFunction(function),
                2 or 3 => new SingleInputFunctionAdapter(ResolveFunctionGeneric(functionEntry)),
                _ => throw new UnsupportedImageFeatureException(
                    $"pdf-functiontype-{functionType}",
                    $"/FunctionType {functionType} is not supported; only /FunctionType 0 (sampled), " +
                    "2 (exponential), 3 (stitching) and 4 (PostScript calculator) are supported."),
            };
        }
        finally
        {
            _functionRecursionDepth--;
        }
    }

    /// <summary>Resolves a (possibly multi-input) <c>/FunctionType 0</c> stream into a <see cref="MultiSampledFunction"/>.</summary>
    private MultiSampledFunction ResolveMultiSampledFunction(PdfObject function)
    {
        if (function.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FunctionType 0 function must be a stream.");
        }

        var domain = RequireNumberArray(function, "Domain");
        if (domain.Length < 2 || domain.Length % 2 != 0 || domain.Any(static d => !double.IsFinite(d)))
        {
            throw new InvalidDataException("/Domain must be a non-empty array of finite number pairs.");
        }

        var inputCount = domain.Length / 2;
        if (inputCount > MaxSampledFunctionInputs)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-function-multiinput",
                $"A /FunctionType 0 function with more than {MaxSampledFunctionInputs} inputs is not supported.");
        }

        var range = RequireNumberArray(function, "Range");
        if (range.Length == 0 || range.Length % 2 != 0 || range.Length / 2 > MaxFunctionOutputs
            || range.Any(static r => !double.IsFinite(r)))
        {
            throw new InvalidDataException(
                $"/Range must be 1 to {MaxFunctionOutputs} finite number pairs.");
        }

        var outputCount = range.Length / 2;
        var sizeValues = RequireNumberArray(function, "Size");
        if (sizeValues.Length != inputCount || sizeValues.Any(static s => !IsIntegralInRange(s, 1, int.MaxValue)))
        {
            // A fractional entry (for example 1.5) is malformed, never silently truncated.
            throw new InvalidDataException("/Size must have one positive integer per input.");
        }

        var size = sizeValues.Select(static s => (int)s).ToArray();
        long totalSamples = outputCount;
        foreach (var dimension in size)
        {
            totalSamples *= dimension;
            if (totalSamples > MaxSampledFunctionSamples)
            {
                throw new InvalidDataException(
                    $"/Size declares more than the supported {MaxSampledFunctionSamples} samples.");
            }
        }

        var bitsPerSample = RequireStrictIntEntry(function, "BitsPerSample");
        if (bitsPerSample is not (1 or 2 or 4 or 8 or 12 or 16 or 24 or 32))
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-function-bitspersample-{bitsPerSample}",
                $"/BitsPerSample {bitsPerSample} is not supported; only 1, 2, 4, 8, 12, 16, 24 and 32 are supported.");
        }

        var encode = ResolveOptionalNumberArray(function, "Encode");
        if (encode is null)
        {
            encode = new double[2 * inputCount];
            for (var i = 0; i < inputCount; i++)
            {
                encode[(2 * i) + 1] = size[i] - 1;
            }
        }
        else if (encode.Length != 2 * inputCount || encode.Any(static e => !double.IsFinite(e)))
        {
            throw new InvalidDataException("/Encode must have one finite number pair per input.");
        }

        var decode = ResolveOptionalNumberArray(function, "Decode") ?? range;
        if (decode.Length != range.Length || decode.Any(static d => !double.IsFinite(d)))
        {
            throw new InvalidDataException("/Decode must have the same number of finite elements as /Range.");
        }

        var samples = GetStreamDecodedBytes(function);
        if (((totalSamples * bitsPerSample) + 7) / 8 > samples.Length)
        {
            throw new InvalidDataException(
                "Sampled function stream does not contain enough sample bytes for its declared /Size.");
        }

        return new MultiSampledFunction
        {
            Domain = domain,
            Range = range,
            Encode = encode,
            Decode = decode,
            Size = size,
            BitsPerSample = bitsPerSample,
            Samples = samples,
        };
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

        // Evaluate returns the selected branch's array unchanged, so every branch must produce the
        // same number of outputs as the first (branches are single-input by IPdfFunction's contract).
        if (functions.Any(branch => branch.OutputCount != functions[0].OutputCount))
        {
            throw new InvalidDataException("Every /Functions sub-function of a stitching function must have the same output count.");
        }

        var bounds = ResolveOptionalNumberArray(function, "Bounds") ?? [];
        if (bounds.Length != k - 1)
        {
            throw new InvalidDataException($"/Bounds must have exactly {k - 1} element(s) for {k} sub-function(s).");
        }

        // A degenerate (equal) /Domain is tolerated by Evaluate, so it stays accepted; a reversed or
        // non-finite one is malformed. Equal consecutive bounds are valid (hard color stops).
        if (!double.IsFinite(domain[0]) || !double.IsFinite(domain[1]) || domain[0] > domain[1])
        {
            throw new InvalidDataException("/Domain must hold two finite numbers with Domain0 <= Domain1.");
        }

        var previousBound = domain[0];
        foreach (var bound in bounds)
        {
            if (!double.IsFinite(bound))
            {
                throw new InvalidDataException("/Bounds must contain only finite numbers.");
            }

            if (bound < previousBound)
            {
                throw new InvalidDataException("/Bounds must be non-decreasing and within /Domain.");
            }

            previousBound = bound;
        }

        if (bounds.Length > 0 && bounds[^1] > domain[1])
        {
            throw new InvalidDataException("/Bounds must be non-decreasing and within /Domain.");
        }

        var encode = RequireNumberArray(function, "Encode");
        if (encode.Length != 2 * k)
        {
            throw new InvalidDataException($"/Encode must have exactly {2 * k} element(s) for {k} sub-function(s).");
        }

        if (encode.Any(value => !double.IsFinite(value)))
        {
            throw new InvalidDataException("/Encode must contain only finite numbers.");
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
    ///     Thrown when the resolved value is not a stream, when a required entry
    ///     (<c>/Domain</c>, <c>/Range</c>, <c>/Size</c>, <c>/BitsPerSample</c>) is missing or
    ///     malformed, or when <c>/Size</c>'s element is not a finite number (defense-in-depth: a
    ///     NaN/Infinity value would otherwise reach an unchecked numeric cast).
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

        var functionType = RequireStrictIntEntry(function, "FunctionType");
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

        // Reject a non-finite, fractional or out-of-range /Size element before casting it to int:
        // NaN/Infinity cast to int is an unchecked, unspecified-magnitude conversion in C#, and a
        // fractional value (for example 1.5) would be silently truncated, so a crafted function
        // dictionary could otherwise smuggle a wrong `size` past the checks.
        if (!IsIntegralInRange(sizeArray[0], 1, int.MaxValue))
        {
            throw new InvalidDataException("/Size must be a positive integer.");
        }

        var size = (int)sizeArray[0];

        var bitsPerSample = RequireStrictIntEntry(function, "BitsPerSample");
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

    /// <summary>Gets a value indicating whether <paramref name="value"/> is finite, has no fractional part, and lies in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    private static bool IsIntegralInRange(double value, double min, double max)
        => double.IsFinite(value) && double.IsInteger(value) && value >= min && value <= max;

    /// <summary>
    ///     Reads a required integer function-dictionary entry, rejecting a fractional, non-finite or
    ///     out-of-range number instead of silently truncating it.
    /// </summary>
    /// <param name="dictionary">The function dictionary or stream.</param>
    /// <param name="key">The entry name.</param>
    /// <returns>The exact integer value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the entry is missing or not an exact integer.</exception>
    private int RequireStrictIntEntry(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key) ?? throw new InvalidDataException($"Stream is missing required /{key}.");
        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Number || !IsIntegralInRange(resolved.Number, int.MinValue, int.MaxValue))
        {
            throw new InvalidDataException($"/{key} must be an integer.");
        }

        return (int)resolved.Number;
    }
}
