namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Codestream marker constants and decoder limits
    // ================================================================================================

    private const ushort MarkerSoc = 0xFF4F;
    private const ushort MarkerSiz = 0xFF51;
    private const ushort MarkerCod = 0xFF52;
    private const ushort MarkerCoc = 0xFF53;
    private const ushort MarkerCap = 0xFF50;
    private const ushort MarkerQcd = 0xFF5C;
    private const ushort MarkerQcc = 0xFF5D;
    private const ushort MarkerRgn = 0xFF5E;
    private const ushort MarkerPoc = 0xFF5F;
    private const ushort MarkerPpm = 0xFF60;
    private const ushort MarkerPpt = 0xFF61;
    private const ushort MarkerSot = 0xFF90;
    private const ushort MarkerSod = 0xFF93;
    private const ushort MarkerEoc = 0xFFD9;

    /// <summary>The maximum number of image components the decoder accepts (more are rejected as unsupported).</summary>
    internal const int MaxComponents = 16;

    /// <summary>The largest number of tiles the format can address (the SOT tile index is 16 bits wide).</summary>
    private const int FormatMaxTiles = 65535;

    /// <summary>The largest number of decomposition levels the format allows.</summary>
    private const int MaxDecompositionLevels = 32;

    /// <summary>
    ///     The largest number of magnitude bit-planes of one code-block the decoder handles. It is
    ///     validated once while the sub-band geometry is built (guard bits, exponent and ROI shift), so
    ///     every later stage can rely on it.
    /// </summary>
    internal const int MaxBitPlanes = 30;

    /// <summary>The progression-step allowance every decode gets regardless of the input length.</summary>
    private const long ProgressionStepsBase = 1L << 22;

    /// <summary>The additional progression steps allowed per input byte (a packet costs at least one header bit).</summary>
    private const long ProgressionStepsPerInputByte = 64;

    /// <summary>The entropy-decoding work (sample-passes) every decode gets regardless of the input length.</summary>
    private const long Tier1WorkBase = 1L << 24;

    /// <summary>
    ///     The additional entropy-decoding work (sample-passes) allowed per input byte. The worst valid case measured
    ///     (a flat 16-bit plane) needs about 4,560 sample-passes per byte; 2^14 leaves a margin of about 3.6 while
    ///     keeping the CPU time a hostile stream can demand to roughly 55 microseconds per input byte.
    /// </summary>
    private const long Tier1WorkPerInputByte = 1L << 14;

    /// <summary>Creates the exception thrown for malformed JPEG 2000 data.</summary>
    /// <param name="message">A description of what is malformed.</param>
    /// <returns>A new <see cref="InvalidDataException"/>.</returns>
    private static InvalidDataException Malformed(string message) => new("Invalid JPEG 2000 data: " + message);

    /// <summary>Creates the exception thrown for valid but unsupported JPEG 2000 features.</summary>
    /// <param name="feature">The stable machine-matchable feature token.</param>
    /// <param name="message">A description of the unsupported feature.</param>
    /// <returns>A new <see cref="UnsupportedImageFeatureException"/>.</returns>
    private static UnsupportedImageFeatureException Unsupported(string feature, string message) =>
        new(feature, "Unsupported JPEG 2000 feature: " + message);

    /// <summary>Computes <c>ceil(a / b)</c> for non-negative <paramref name="a"/> and positive <paramref name="b"/>.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="b">The divisor.</param>
    /// <returns>The rounded-up quotient.</returns>
    private static long CeilDiv(long a, long b) => (a + b - 1) / b;

    /// <summary>Computes <c>ceil(a / 2^s)</c> for non-negative <paramref name="a"/>.</summary>
    /// <param name="a">The dividend.</param>
    /// <param name="s">The shift, 0 to 62.</param>
    /// <returns>The rounded-up quotient.</returns>
    private static long CeilDivPow2(long a, int s) => (a + (1L << s) - 1) >> s;

    // ================================================================================================
    // Byte reader
    // ================================================================================================

    /// <summary>A bounds-checked big-endian reader over a window of a byte array.</summary>
    internal sealed class ByteReader
    {
        private readonly byte[] _data;
        private readonly int _end;

        /// <summary>Initializes a new instance of the <see cref="ByteReader"/> class.</summary>
        /// <param name="data">The backing array.</param>
        /// <param name="start">The first readable offset.</param>
        /// <param name="end">The exclusive end offset.</param>
        public ByteReader(byte[] data, int start, int end)
        {
            if (start < 0 || end < start || end > data.Length)
            {
                throw Malformed("segment bounds are outside the data.");
            }

            _data = data;
            Position = start;
            _end = end;
        }

        /// <summary>Gets or sets the current absolute offset.</summary>
        public int Position { get; set; }

        /// <summary>Gets the number of unread bytes.</summary>
        public int Remaining => _end - Position;

        /// <summary>Reads one byte.</summary>
        /// <returns>The byte value.</returns>
        public int ReadU8()
        {
            if (Position >= _end)
            {
                throw Malformed("unexpected end of data.");
            }

            return _data[Position++];
        }

        /// <summary>Reads a big-endian 16-bit value.</summary>
        /// <returns>The value.</returns>
        public int ReadU16()
        {
            var hi = ReadU8();
            return (hi << 8) | ReadU8();
        }

        /// <summary>Reads a big-endian 32-bit value.</summary>
        /// <returns>The value.</returns>
        public long ReadU32()
        {
            var hi = (long)ReadU16();
            return (hi << 16) | (uint)ReadU16();
        }

        /// <summary>Reads a big-endian 64-bit value, rejecting values above <see cref="long.MaxValue"/>.</summary>
        /// <returns>The value.</returns>
        public long ReadU64()
        {
            var hi = ReadU32();
            var value = (hi << 32) | ReadU32();
            if (value < 0)
            {
                throw Malformed("64-bit value is out of range.");
            }

            return value;
        }

        /// <summary>Copies <paramref name="count"/> bytes out and advances.</summary>
        /// <param name="count">The number of bytes.</param>
        /// <returns>A new array holding the bytes.</returns>
        public byte[] ReadBytes(int count)
        {
            if (count < 0 || count > Remaining)
            {
                throw Malformed("unexpected end of data.");
            }

            var result = new byte[count];
            Array.Copy(_data, Position, result, 0, count);
            Position += count;
            return result;
        }
    }

    // ================================================================================================
    // SIZ and coding parameter model
    // ================================================================================================

    /// <summary>The decoded SIZ (image and tile size) marker segment.</summary>
    internal sealed class SizInfo
    {
        /// <summary>Gets the reference-grid width (right edge).</summary>
        public long Xsiz { get; init; }

        /// <summary>Gets the reference-grid height (bottom edge).</summary>
        public long Ysiz { get; init; }

        /// <summary>Gets the horizontal image offset.</summary>
        public long XOsiz { get; init; }

        /// <summary>Gets the vertical image offset.</summary>
        public long YOsiz { get; init; }

        /// <summary>Gets the tile width.</summary>
        public long XTsiz { get; init; }

        /// <summary>Gets the tile height.</summary>
        public long YTsiz { get; init; }

        /// <summary>Gets the horizontal tile grid offset.</summary>
        public long XTOsiz { get; init; }

        /// <summary>Gets the vertical tile grid offset.</summary>
        public long YTOsiz { get; init; }

        /// <summary>Gets the per-component bit depth (1 to 16 accepted).</summary>
        public required int[] Depth { get; init; }

        /// <summary>Gets the per-component signedness.</summary>
        public required bool[] Signed { get; init; }

        /// <summary>Gets the per-component horizontal subsampling.</summary>
        public required int[] XR { get; init; }

        /// <summary>Gets the per-component vertical subsampling.</summary>
        public required int[] YR { get; init; }

        /// <summary>Gets the number of components.</summary>
        public int Csiz => Depth.Length;

        /// <summary>Gets the image width in reference-grid samples.</summary>
        public long Width => Xsiz - XOsiz;

        /// <summary>Gets the image height in reference-grid samples.</summary>
        public long Height => Ysiz - YOsiz;

        /// <summary>Gets the number of tile columns.</summary>
        public int NumXTiles => (int)CeilDiv(Xsiz - XTOsiz, XTsiz);

        /// <summary>Gets the number of tile rows.</summary>
        public int NumYTiles => (int)CeilDiv(Ysiz - YTOsiz, YTsiz);

        /// <summary>Gets the left edge of component <paramref name="c"/> in component samples.</summary>
        /// <param name="c">The component index.</param>
        /// <returns>The component-grid coordinate.</returns>
        public long CompX0(int c) => CeilDiv(XOsiz, XR[c]);

        /// <summary>Gets the top edge of component <paramref name="c"/> in component samples.</summary>
        /// <param name="c">The component index.</param>
        /// <returns>The component-grid coordinate.</returns>
        public long CompY0(int c) => CeilDiv(YOsiz, YR[c]);

        /// <summary>Gets the right edge of component <paramref name="c"/> in component samples.</summary>
        /// <param name="c">The component index.</param>
        /// <returns>The component-grid coordinate.</returns>
        public long CompX1(int c) => CeilDiv(Xsiz, XR[c]);

        /// <summary>Gets the bottom edge of component <paramref name="c"/> in component samples.</summary>
        /// <param name="c">The component index.</param>
        /// <returns>The component-grid coordinate.</returns>
        public long CompY1(int c) => CeilDiv(Ysiz, YR[c]);

        /// <summary>
        ///     Checks the declared image against the resource limits. This runs on the SIZ values alone,
        ///     before any plane, tile or code-block structure is allocated.
        /// </summary>
        /// <param name="limits">The limits to enforce.</param>
        public void CheckLimits(Jpeg2000DecoderLimits limits)
        {
            if (Width > limits.MaxWidth || Height > limits.MaxHeight)
            {
                throw Malformed($"image dimensions {Width}x{Height} exceed the {limits.MaxWidth}x{limits.MaxHeight} pixel limit.");
            }

            if ((long)NumXTiles * NumYTiles > limits.MaxTiles)
            {
                throw Malformed("too many tiles.");
            }

            // Width and Height are within int range here, so each plane (at most Width x Height plus a
            // rounding sample per axis) fits comfortably in a long.
            long planeSamples = 0;
            for (var c = 0; c < Csiz; c++)
            {
                planeSamples += (CompX1(c) - CompX0(c)) * (CompY1(c) - CompY0(c));
                if (planeSamples > limits.MaxTotalSamples)
                {
                    throw Malformed("image exceeds the decoder sample limit.");
                }
            }
        }

        /// <summary>Parses and validates a SIZ marker segment payload.</summary>
        /// <param name="r">A reader positioned just after the segment length.</param>
        /// <returns>The validated SIZ information.</returns>
        public static SizInfo Parse(ByteReader r)
        {
            // Rsiz capability bits: bit 14 flags HTJ2K and bit 15 flags Part 2 extensions.
            var rsiz = r.ReadU16();
            if ((rsiz & 0x8000) != 0 || (rsiz & 0x4000) != 0)
            {
                throw Unsupported("jpeg2000-extensions", "Part 2 / high-throughput capabilities are not supported.");
            }

            var xsiz = r.ReadU32();
            var ysiz = r.ReadU32();
            var xo = r.ReadU32();
            var yo = r.ReadU32();
            var xt = r.ReadU32();
            var yt = r.ReadU32();
            var xto = r.ReadU32();
            var yto = r.ReadU32();
            var csiz = r.ReadU16();

            // Geometry invariants from ITU-T T.800 Table A.9.
            if (xsiz == 0 || ysiz == 0 || xt == 0 || yt == 0 || xo >= xsiz || yo >= ysiz
                || xto > xo || yto > yo || xto + xt <= xo || yto + yt <= yo)
            {
                throw Malformed("inconsistent image or tile size.");
            }

            if (csiz < 1 || csiz > 16384)
            {
                throw Malformed("invalid component count.");
            }

            if (csiz > MaxComponents)
            {
                throw Unsupported("jpeg2000-component-count", "more than " + MaxComponents + " components.");
            }

            if (r.Remaining < csiz * 3)
            {
                throw Malformed("SIZ segment is truncated.");
            }

            var depth = new int[csiz];
            var signed = new bool[csiz];
            var xr = new int[csiz];
            var yr = new int[csiz];
            for (var c = 0; c < csiz; c++)
            {
                var ssiz = r.ReadU8();
                depth[c] = (ssiz & 0x7F) + 1;
                signed[c] = (ssiz & 0x80) != 0;
                xr[c] = r.ReadU8();
                yr[c] = r.ReadU8();
                if (xr[c] == 0 || yr[c] == 0)
                {
                    throw Malformed("component subsampling must not be zero.");
                }

                if (depth[c] > 16)
                {
                    throw Unsupported("jpeg2000-bit-depth", "component bit depth above 16.");
                }
            }

            // The tile count is bounded here, before any per-tile structure is created.
            var tilesX = CeilDiv(xsiz - xto, xt);
            var tilesY = CeilDiv(ysiz - yto, yt);
            if (tilesX * tilesY > FormatMaxTiles)
            {
                throw Malformed("too many tiles.");
            }

            return new SizInfo
            {
                Xsiz = xsiz,
                Ysiz = ysiz,
                XOsiz = xo,
                YOsiz = yo,
                XTsiz = xt,
                YTsiz = yt,
                XTOsiz = xto,
                YTOsiz = yto,
                Depth = depth,
                Signed = signed,
                XR = xr,
                YR = yr,
            };
        }
    }

    /// <summary>The per-component coding style (COD/COC) parameters.</summary>
    internal sealed class CodingParams
    {
        /// <summary>Gets the number of decomposition levels.</summary>
        public int Levels { get; init; }

        /// <summary>Gets the code-block width exponent.</summary>
        public int Xcb { get; init; }

        /// <summary>Gets the code-block height exponent.</summary>
        public int Ycb { get; init; }

        /// <summary>Gets the code-block style flags.</summary>
        public int Style { get; init; }

        /// <summary>Gets a value indicating whether the reversible 5/3 wavelet is used.</summary>
        public bool Reversible { get; init; }

        /// <summary>Gets the precinct width exponents per resolution.</summary>
        public required int[] PPx { get; init; }

        /// <summary>Gets the precinct height exponents per resolution.</summary>
        public required int[] PPy { get; init; }

        /// <summary>Parses the SPcod/SPcoc parameter block.</summary>
        /// <param name="r">The reader positioned at the block.</param>
        /// <param name="hasPrecincts">Whether explicit precinct sizes follow.</param>
        /// <returns>The parsed parameters.</returns>
        public static CodingParams Parse(ByteReader r, bool hasPrecincts)
        {
            var levels = r.ReadU8();
            var xcb = r.ReadU8() + 2;
            var ycb = r.ReadU8() + 2;
            var style = r.ReadU8();
            var transform = r.ReadU8();
            if (levels > MaxDecompositionLevels)
            {
                throw Malformed("too many decomposition levels.");
            }

            if (xcb > 10 || ycb > 10 || xcb + ycb > 12)
            {
                throw Malformed("invalid code-block size.");
            }

            if ((style & 0xC0) != 0)
            {
                throw Unsupported("jpeg2000-codeblock-style", "high-throughput or Part 2 code-block style.");
            }

            if (transform > 1)
            {
                throw Unsupported("jpeg2000-transform", "wavelet transform other than 5/3 or 9/7.");
            }

            var ppx = new int[levels + 1];
            var ppy = new int[levels + 1];
            for (var i = 0; i <= levels; i++)
            {
                if (hasPrecincts)
                {
                    var b = r.ReadU8();
                    ppx[i] = b & 0xF;
                    ppy[i] = b >> 4;

                    // A precinct dimension of 1 sample is only meaningful at the lowest resolution.
                    if (i > 0 && (ppx[i] == 0 || ppy[i] == 0))
                    {
                        throw Malformed("invalid precinct size.");
                    }
                }
                else
                {
                    ppx[i] = 15;
                    ppy[i] = 15;
                }
            }

            return new CodingParams
            {
                Levels = levels,
                Xcb = xcb,
                Ycb = ycb,
                Style = style,
                Reversible = transform == 1,
                PPx = ppx,
                PPy = ppy,
            };
        }
    }

    /// <summary>The per-component quantization (QCD/QCC) parameters.</summary>
    internal sealed class QuantParams
    {
        /// <summary>Gets the quantization style: 0 none, 1 scalar derived, 2 scalar expounded.</summary>
        public int Style { get; init; }

        /// <summary>Gets the number of guard bits.</summary>
        public int GuardBits { get; init; }

        /// <summary>Gets the sub-band exponents.</summary>
        public required int[] Exp { get; init; }

        /// <summary>Gets the sub-band mantissas.</summary>
        public required int[] Mant { get; init; }

        /// <summary>Parses a QCD/QCC style byte and its sub-band entries.</summary>
        /// <param name="r">The reader positioned at the style byte.</param>
        /// <returns>The parsed parameters.</returns>
        public static QuantParams Parse(ByteReader r)
        {
            var sq = r.ReadU8();
            var style = sq & 0x1F;
            var guard = sq >> 5;
            if (style > 2)
            {
                throw Malformed("invalid quantization style.");
            }

            int[] exp;
            int[] mant;
            if (style == 0)
            {
                var n = r.Remaining;
                exp = new int[n];
                mant = new int[n];
                for (var i = 0; i < n; i++)
                {
                    exp[i] = r.ReadU8() >> 3;
                }
            }
            else if (style == 1)
            {
                var v = r.ReadU16();
                exp = [v >> 11];
                mant = [v & 0x7FF];
            }
            else
            {
                var n = r.Remaining / 2;
                exp = new int[n];
                mant = new int[n];
                for (var i = 0; i < n; i++)
                {
                    var v = r.ReadU16();
                    exp[i] = v >> 11;
                    mant[i] = v & 0x7FF;
                }
            }

            if (exp.Length == 0)
            {
                throw Malformed("quantization segment has no entries.");
            }

            return new QuantParams { Style = style, GuardBits = guard, Exp = exp, Mant = mant };
        }
    }

    /// <summary>One progression order change (POC) entry.</summary>
    /// <param name="ResStart">The first resolution of the volume.</param>
    /// <param name="CompStart">The first component of the volume.</param>
    /// <param name="LayerEnd">The exclusive layer end.</param>
    /// <param name="ResEnd">The exclusive resolution end.</param>
    /// <param name="CompEnd">The exclusive component end.</param>
    /// <param name="Order">The progression order, 0 to 4.</param>
    internal readonly record struct ProgressionChange(int ResStart, int CompStart, int LayerEnd, int ResEnd, int CompEnd, int Order);

    /// <summary>
    ///     The mutable coding state in effect for the main header or for a tile: the COD/COC, QCD/QCC,
    ///     RGN and POC parameters with the marker-precedence rules of ITU-T T.800 A.6.
    /// </summary>
    internal sealed class CodingState
    {
        private readonly int _components;
        private readonly int _maxPoc;
        private readonly bool[] _cocSet;
        private readonly bool[] _qccSet;

        /// <summary>Initializes an empty state for <paramref name="components"/> components.</summary>
        /// <param name="components">The component count.</param>
        /// <param name="maxPocEntries">The maximum number of progression-order change entries the state may hold.</param>
        public CodingState(int components, int maxPocEntries)
        {
            _components = components;
            _maxPoc = maxPocEntries;
            _cocSet = new bool[components];
            _qccSet = new bool[components];
            Coding = new CodingParams?[components];
            Quant = new QuantParams?[components];
            RoiShift = new int[components];
        }

        /// <summary>Gets the progression order.</summary>
        public int Progression { get; private set; }

        /// <summary>Gets the number of layers.</summary>
        public int Layers { get; private set; } = 1;

        /// <summary>Gets a value indicating whether the multiple component transform is used.</summary>
        public bool Mct { get; private set; }

        /// <summary>Gets a value indicating whether SOP markers may be present.</summary>
        public bool Sop { get; private set; }

        /// <summary>Gets a value indicating whether EPH markers are present.</summary>
        public bool Eph { get; private set; }

        /// <summary>Gets a value indicating whether a COD marker has been seen.</summary>
        public bool HasCod { get; private set; }

        /// <summary>Gets a value indicating whether a QCD marker has been seen.</summary>
        public bool HasQcd { get; private set; }

        /// <summary>Gets the per-component coding parameters.</summary>
        public CodingParams?[] Coding { get; }

        /// <summary>Gets the per-component quantization parameters.</summary>
        public QuantParams?[] Quant { get; }

        /// <summary>Gets the per-component ROI max-shift values.</summary>
        public int[] RoiShift { get; }

        /// <summary>Gets or sets the POC entries, or <see langword="null"/> when none were signalled.</summary>
        public List<ProgressionChange>? Poc { get; set; }

        private bool _pocInherited;

        /// <summary>Creates a copy used as the starting state of a tile.</summary>
        /// <returns>The copy; main-header POC entries are inherited until the tile-part header signals its own.</returns>
        public CodingState CloneForTile()
        {
            var copy = new CodingState(_components, _maxPoc)
            {
                Progression = Progression,
                Layers = Layers,
                Mct = Mct,
                Sop = Sop,
                Eph = Eph,
                HasCod = HasCod,
                HasQcd = HasQcd,
            };
            Array.Copy(Coding, copy.Coding, _components);
            Array.Copy(Quant, copy.Quant, _components);
            Array.Copy(RoiShift, copy.RoiShift, _components);
            if (Poc is not null)
            {
                copy.Poc = [.. Poc];
                copy._pocInherited = true;
            }

            return copy;
        }

        /// <summary>Applies a COD segment.</summary>
        /// <param name="r">The segment payload reader.</param>
        public void ApplyCod(ByteReader r)
        {
            var scod = r.ReadU8();
            var prog = r.ReadU8();
            var layers = r.ReadU16();
            var mct = r.ReadU8();
            if (prog > 4)
            {
                throw Malformed("invalid progression order.");
            }

            if (layers < 1)
            {
                throw Malformed("layer count must be at least 1.");
            }

            if (mct > 1)
            {
                throw Unsupported("jpeg2000-mct", "multiple component transform other than the Part 1 transforms.");
            }

            var p = CodingParams.Parse(r, (scod & 1) != 0);
            Progression = prog;
            Layers = layers;
            Mct = mct == 1;
            Sop = (scod & 2) != 0;
            Eph = (scod & 4) != 0;
            HasCod = true;
            for (var c = 0; c < _components; c++)
            {
                if (!_cocSet[c])
                {
                    Coding[c] = p;
                }
            }
        }

        /// <summary>Applies a COC segment.</summary>
        /// <param name="r">The segment payload reader.</param>
        public void ApplyCoc(ByteReader r)
        {
            var c = r.ReadU8();
            var scoc = r.ReadU8();
            if (c >= _components)
            {
                throw Malformed("COC component index out of range.");
            }

            Coding[c] = CodingParams.Parse(r, (scoc & 1) != 0);
            _cocSet[c] = true;
        }

        /// <summary>Applies a QCD segment.</summary>
        /// <param name="r">The segment payload reader.</param>
        public void ApplyQcd(ByteReader r)
        {
            var q = QuantParams.Parse(r);
            HasQcd = true;
            for (var c = 0; c < _components; c++)
            {
                if (!_qccSet[c])
                {
                    Quant[c] = q;
                }
            }
        }

        /// <summary>Applies a QCC segment.</summary>
        /// <param name="r">The segment payload reader.</param>
        public void ApplyQcc(ByteReader r)
        {
            var c = r.ReadU8();
            if (c >= _components)
            {
                throw Malformed("QCC component index out of range.");
            }

            Quant[c] = QuantParams.Parse(r);
            _qccSet[c] = true;
        }

        /// <summary>Applies an RGN (region of interest) segment.</summary>
        /// <param name="r">The segment payload reader.</param>
        public void ApplyRgn(ByteReader r)
        {
            var c = r.ReadU8();
            var style = r.ReadU8();
            var shift = r.ReadU8();
            if (c >= _components)
            {
                throw Malformed("RGN component index out of range.");
            }

            if (style != 0)
            {
                throw Unsupported("jpeg2000-roi", "ROI style other than implicit max-shift.");
            }

            if (shift > 37)
            {
                throw Malformed("ROI shift is out of range.");
            }

            RoiShift[c] = shift;
        }

        /// <summary>Appends the entries of a POC segment.</summary>
        /// <param name="r">The segment payload reader.</param>
        public void ApplyPoc(ByteReader r)
        {
            if (_pocInherited || Poc is null)
            {
                // A tile-part POC replaces any entries inherited from the main header.
                Poc = [];
                _pocInherited = false;
            }

            if (r.Remaining == 0 || r.Remaining % 7 != 0)
            {
                throw Malformed("invalid POC segment length.");
            }

            while (r.Remaining > 0)
            {
                var rs = r.ReadU8();
                var cs = r.ReadU8();
                var lye = r.ReadU16();
                var re = r.ReadU8();
                var ce = r.ReadU8();
                var order = r.ReadU8();
                if (ce == 0)
                {
                    ce = 256;
                }

                if (order > 4 || rs >= re || cs >= ce || lye < 1)
                {
                    throw Malformed("invalid POC entry.");
                }

                if (Poc.Count >= _maxPoc)
                {
                    throw Malformed("too many progression order changes.");
                }

                Poc.Add(new ProgressionChange(rs, cs, lye, re, ce, order));
            }
        }
    }

    // ================================================================================================
    // Codestream container
    // ================================================================================================

    /// <summary>The data collected for one tile from all of its tile-parts.</summary>
    internal sealed class TileData
    {
        /// <summary>Gets or sets the tile-specific coding state, or <see langword="null"/> before the first tile-part.</summary>
        public CodingState? State { get; set; }

        /// <summary>Gets the concatenated tile-part bodies.</summary>
        public MemoryStream Body { get; } = new();

        /// <summary>Gets the concatenated packed packet headers (from PPM/PPT).</summary>
        public MemoryStream PackedHeaders { get; } = new();

        /// <summary>Gets or sets a value indicating whether packed packet headers are in use.</summary>
        public bool UsesPackedHeaders { get; set; }

        /// <summary>Gets or sets the number of tile-parts read so far.</summary>
        public int Parts { get; set; }
    }

    /// <summary>A parsed JPEG 2000 codestream: main header parameters and per-tile data.</summary>
    internal sealed class Codestream
    {
        /// <summary>Gets the image and tile size information.</summary>
        public required SizInfo Siz { get; init; }

        /// <summary>Gets the main-header coding state.</summary>
        public required CodingState Main { get; init; }

        /// <summary>Gets the per-tile data, indexed by tile number; every tile has at least one tile-part.</summary>
        public required TileData[] Tiles { get; init; }

        /// <summary>Reads only the SIZ segment of a codestream.</summary>
        /// <param name="data">The buffer holding the codestream.</param>
        /// <param name="offset">The offset of the SOC marker.</param>
        /// <param name="end">The exclusive end offset of the codestream.</param>
        /// <param name="afterSiz">Receives the offset just past the SIZ marker segment.</param>
        /// <returns>The validated SIZ information.</returns>
        public static SizInfo ParseSizOnly(byte[] data, int offset, int end, out int afterSiz)
        {
            var r = new ByteReader(data, offset, end);
            if (r.ReadU16() != MarkerSoc)
            {
                throw Malformed("codestream does not start with SOC.");
            }

            if (r.ReadU16() != MarkerSiz)
            {
                throw Malformed("SIZ marker segment must follow SOC.");
            }

            var len = r.ReadU16();
            if (len < 41)
            {
                throw Malformed("invalid SIZ length.");
            }

            afterSiz = SegmentEnd(r, len);
            return SizInfo.Parse(new ByteReader(data, r.Position, afterSiz));
        }

        /// <summary>Parses the main header and splits the tile-parts per tile.</summary>
        /// <param name="data">The buffer holding the codestream.</param>
        /// <param name="offset">The offset of the SOC marker.</param>
        /// <param name="end">The exclusive end offset of the codestream.</param>
        /// <param name="limits">The resource limits; the image size is checked against them before anything is allocated.</param>
        /// <returns>The parsed codestream.</returns>
        public static Codestream Parse(byte[] data, int offset, int end, Jpeg2000DecoderLimits limits)
        {
            var siz = ParseSizOnly(data, offset, end, out var afterSiz);
            siz.CheckLimits(limits);
            var r = new ByteReader(data, afterSiz, end);
            var main = new CodingState(siz.Csiz, limits.MaxProgressionChanges);
            var ppmSegments = new List<(int Index, byte[] Data)>();

            // Main header: process marker segments until the first SOT.
            while (true)
            {
                var marker = r.ReadU16();
                if (marker == MarkerSot)
                {
                    r.Position -= 2;
                    break;
                }

                if (marker == MarkerEoc || marker == MarkerSod || marker < 0xFF00)
                {
                    throw Malformed("unexpected marker in main header.");
                }

                if (marker is >= 0xFF30 and <= 0xFF3F)
                {
                    continue;
                }

                var len = r.ReadU16();
                if (len < 2)
                {
                    throw Malformed("invalid marker segment length.");
                }

                var segEnd = SegmentEnd(r, len);
                var seg = new ByteReader(data, r.Position, segEnd);
                ApplyMainMarker(marker, seg, main, ppmSegments);
                r.Position = segEnd;
            }

            if (!main.HasCod || !main.HasQcd)
            {
                throw Malformed("main header lacks a COD or QCD marker segment.");
            }

            for (var c = 0; c < siz.Csiz; c++)
            {
                if (main.Coding[c] is null || main.Quant[c] is null)
                {
                    throw Malformed("component lacks coding or quantization parameters.");
                }
            }

            var tileSlots = new TileData?[siz.NumXTiles * siz.NumYTiles];
            var ppmChunks = ppmSegments.Count > 0 ? SplitPpm(ppmSegments) : null;
            ReadTileParts(data, r, end, main, tileSlots, ppmChunks);

            // Fail closed: a codestream cut short or lacking whole tiles must not decode to a partial image.
            var tiles = new TileData[tileSlots.Length];
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i] = tileSlots[i] ?? throw Malformed($"tile {i} has no tile-part (the codestream is incomplete).");
            }

            return new Codestream { Siz = siz, Main = main, Tiles = tiles };
        }

        private static void ApplyMainMarker(int marker, ByteReader seg, CodingState main, List<(int Index, byte[] Data)> ppmSegments)
        {
            switch (marker)
            {
                case MarkerCod:
                    main.ApplyCod(seg);
                    break;
                case MarkerCoc:
                    main.ApplyCoc(seg);
                    break;
                case MarkerQcd:
                    main.ApplyQcd(seg);
                    break;
                case MarkerQcc:
                    main.ApplyQcc(seg);
                    break;
                case MarkerRgn:
                    main.ApplyRgn(seg);
                    break;
                case MarkerPoc:
                    main.ApplyPoc(seg);
                    break;
                case MarkerCap:
                    throw Unsupported("jpeg2000-extensions", "high-throughput (CAP) codestream.");
                case MarkerPpm:
                    var z = seg.ReadU8();
                    ppmSegments.Add((z, seg.ReadBytes(seg.Remaining)));
                    break;
                case MarkerSiz:
                    throw Malformed("duplicate SIZ marker.");
                default:
                    // TLM, PLM, CRG, COM and unknown segments carry nothing the decoder needs.
                    break;
            }
        }

        private static int SegmentEnd(ByteReader r, int length)
        {
            // The length field counts itself; the reader has already consumed it.
            var segEnd = r.Position + length - 2;
            if (length < 2 || length - 2 > r.Remaining)
            {
                throw Malformed("marker segment extends beyond the data.");
            }

            return segEnd;
        }

        private static List<byte[]> SplitPpm(List<(int Index, byte[] Data)> segments)
        {
            // Zppm orders the segments; the concatenation is a sequence of (Nppm, Ippm) tile-part chunks.
            segments.Sort((a, b) => a.Index.CompareTo(b.Index));
            using var all = new MemoryStream();
            foreach (var data in segments.Select(s => s.Data))
            {
                all.Write(data, 0, data.Length);
            }

            var buffer = all.ToArray();
            var chunks = new List<byte[]>();
            var pos = 0;
            while (pos < buffer.Length)
            {
                if (buffer.Length - pos < 4)
                {
                    throw Malformed("PPM data is truncated.");
                }

                var n = ((long)buffer[pos] << 24) | ((long)buffer[pos + 1] << 16) | ((long)buffer[pos + 2] << 8) | buffer[pos + 3];
                pos += 4;
                if (n > buffer.Length - pos)
                {
                    throw Malformed("PPM chunk extends beyond the data.");
                }

                var chunk = new byte[n];
                Array.Copy(buffer, pos, chunk, 0, (int)n);
                chunks.Add(chunk);
                pos += (int)n;
            }

            return chunks;
        }

        private static void ReadTileParts(byte[] data, ByteReader r, int end, CodingState main, TileData?[] tiles, List<byte[]>? ppm)
        {
            var partCount = 0;
            while (r.Remaining >= 2)
            {
                // A missing EOC is tolerated; anything else after a tile-part must be a SOT or EOC.
                var sotPos = r.Position;
                var marker = r.ReadU16();
                if (marker == MarkerEoc)
                {
                    break;
                }

                if (marker != MarkerSot)
                {
                    throw Malformed("expected SOT marker.");
                }

                var lsot = r.ReadU16();
                var isot = r.ReadU16();
                var psot = r.ReadU32();
                var tpsot = r.ReadU8();
                r.ReadU8(); // TNsot is advisory and not enforced.
                if (lsot != 10 || isot >= tiles.Length)
                {
                    throw Malformed("invalid SOT marker segment.");
                }

                // Psot of zero means the last tile-part extends to the end of the codestream (minus a trailing EOC).
                long partEnd = sotPos + psot;
                if (psot == 0)
                {
                    partEnd = end;
                    if (end - sotPos >= 16 && data[end - 2] == 0xFF && data[end - 1] == 0xD9)
                    {
                        partEnd = end - 2;
                    }
                }
                else if (psot < 14 || partEnd > end)
                {
                    throw Malformed("tile-part length extends beyond the data.");
                }

                var tile = tiles[isot] ??= new TileData();
                CheckTilePartSequence(tile, tpsot);
                tile.State ??= main.CloneForTile();
                var bodyStart = ReadTilePartHeader(data, r.Position, (int)partEnd, tile, partCount, ppm);
                tile.Body.Write(data, bodyStart, (int)partEnd - bodyStart);
                r.Position = (int)partEnd;
                partCount++;
                if (psot == 0)
                {
                    break;
                }
            }

            if (ppm is not null && partCount > ppm.Count)
            {
                throw Malformed("PPM data does not cover every tile-part.");
            }
        }

        /// <summary>
        ///     Checks the tile-part index (TPsot) of the next tile-part of a tile: the tile-parts of a tile must arrive in
        ///     order. The tile-part count (TNsot) is advisory and deliberately not enforced: real encoders write it
        ///     incorrectly (for example one too small) and the decoder, which concatenates the tile-parts it finds,
        ///     does not depend on it.
        /// </summary>
        /// <param name="tile">The tile the tile-part belongs to.</param>
        /// <param name="tpsot">The tile-part index.</param>
        private static void CheckTilePartSequence(TileData tile, int tpsot)
        {
            if (tpsot != tile.Parts)
            {
                throw Malformed("tile-part index is out of sequence.");
            }

            tile.Parts++;
        }

        private static int ReadTilePartHeader(byte[] data, int start, int partEnd, TileData tile, int partIndex, List<byte[]>? ppm)
        {
            var state = tile.State!;
            var pptSegments = new List<(int Index, byte[] Data)>();
            var rr = new ByteReader(data, start, partEnd);
            while (true)
            {
                var marker = rr.ReadU16();
                if (marker == MarkerSod)
                {
                    break;
                }

                if (marker < 0xFF00 || marker == MarkerSot || marker == MarkerEoc)
                {
                    throw Malformed("unexpected marker in tile-part header.");
                }

                if (marker is >= 0xFF30 and <= 0xFF3F)
                {
                    continue;
                }

                var len = rr.ReadU16();
                if (len < 2)
                {
                    throw Malformed("invalid marker segment length.");
                }

                var segEnd = SegmentEnd(rr, len);
                ApplyTileMarker(marker, new ByteReader(data, rr.Position, segEnd), state, pptSegments);
                rr.Position = segEnd;
            }

            if (ppm is not null)
            {
                tile.UsesPackedHeaders = true;
                if (partIndex < ppm.Count)
                {
                    tile.PackedHeaders.Write(ppm[partIndex], 0, ppm[partIndex].Length);
                }
            }

            if (pptSegments.Count > 0)
            {
                tile.UsesPackedHeaders = true;
                pptSegments.Sort((a, b) => a.Index.CompareTo(b.Index));
                foreach (var chunk in pptSegments.Select(s => s.Data))
                {
                    tile.PackedHeaders.Write(chunk, 0, chunk.Length);
                }
            }

            return rr.Position;
        }

        private static void ApplyTileMarker(int marker, ByteReader seg, CodingState state, List<(int Index, byte[] Data)> pptSegments)
        {
            switch (marker)
            {
                case MarkerCod:
                    state.ApplyCod(seg);
                    break;
                case MarkerCoc:
                    state.ApplyCoc(seg);
                    break;
                case MarkerQcd:
                    state.ApplyQcd(seg);
                    break;
                case MarkerQcc:
                    state.ApplyQcc(seg);
                    break;
                case MarkerRgn:
                    state.ApplyRgn(seg);
                    break;
                case MarkerPoc:
                    state.ApplyPoc(seg);
                    break;
                case MarkerPpt:
                    var z = seg.ReadU8();
                    pptSegments.Add((z, seg.ReadBytes(seg.Remaining)));
                    break;
                case MarkerPpm:
                case MarkerSiz:
                    throw Malformed("marker not allowed in a tile-part header.");
                default:
                    // PLT, COM and unknown segments carry nothing the decoder needs.
                    break;
            }
        }
    }
}
