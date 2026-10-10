#pragma warning disable S3218, S3358, S1172, S107, S3776, S1541, S134, S1244
// cspell:ignore bypass ebcot lblock lifting
namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Coding primitives of the test-only JPEG 2000 encoder: the MQ arithmetic encoder, the raw
///     (bypass) bit writer, the tier-1 (EBCOT) code-block encoder, the packet header bit writer with
///     its tag tree, and the forward wavelet transforms. Everything here is written from the
///     ITU-T T.800 text independently of the production decoder.
/// </summary>
internal static partial class Jpeg2000TestEncoder
{
    /// <summary>The Qe values of Table C.2.</summary>
    internal static readonly int[] MqQe =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    /// <summary>The NMPS column of Table C.2.</summary>
    internal static readonly int[] MqNmps =
    [
        1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
        33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46,
    ];

    /// <summary>The NLPS column of Table C.2.</summary>
    internal static readonly int[] MqNlps =
    [
        1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
        29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46,
    ];

    /// <summary>The SWITCH column of Table C.2.</summary>
    internal static readonly int[] MqSwitch =
    [
        1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0,
    ];

    /// <summary>The MQ arithmetic encoder of ITU-T T.800 Annex C (software conventions).</summary>
    internal sealed class MqEncoder
    {
        private readonly int[] _state = new int[19];
        private readonly int[] _mps = new int[19];
        private List<byte> _buf = [0];
        private int _bp;
        private long _a;
        private long _c;
        private int _ct;

        /// <summary>Sets the contexts to the initial states of Table D.7 (all zero for generic use).</summary>
        /// <param name="tier1">Whether to use the tier-1 initial states (UNI 46, RL 3, ZC0 4).</param>
        public void ResetContexts(bool tier1)
        {
            Array.Clear(_state);
            Array.Clear(_mps);
            if (tier1)
            {
                _state[0] = 4;
                _state[17] = 3;
                _state[18] = 46;
            }
        }

        /// <summary>Starts a new codeword segment (INITENC).</summary>
        public void Start()
        {
            _buf = [0];
            _bp = 0;
            _a = 0x8000;
            _c = 0;
            _ct = 12;
        }

        /// <summary>Encodes one decision (ENCODE).</summary>
        /// <param name="d">The decision.</param>
        /// <param name="cx">The context.</param>
        public void Encode(int d, int cx)
        {
            if (d == _mps[cx])
            {
                CodeMps(cx);
            }
            else
            {
                CodeLps(cx);
            }
        }

        /// <summary>Terminates the segment (FLUSH) and returns its bytes without the leading dummy byte.</summary>
        /// <returns>The segment bytes.</returns>
        public byte[] Flush()
        {
            SetBits();
            _c <<= _ct;
            ByteOut();
            _c <<= _ct;
            ByteOut();
            if (_buf[_bp] != 0xFF)
            {
                _bp++;
            }

            // Bytes 1 .. _bp - 1 hold the segment.
            return _buf.Skip(1).Take(_bp - 1).ToArray();
        }

        private void CodeMps(int cx)
        {
            var qe = MqQe[_state[cx]];
            _a -= qe;
            if ((_a & 0x8000) == 0)
            {
                if (_a < qe)
                {
                    _a = qe;
                }
                else
                {
                    _c += qe;
                }

                _state[cx] = MqNmps[_state[cx]];
                Renorm();
            }
            else
            {
                _c += qe;
            }
        }

        private void CodeLps(int cx)
        {
            var qe = MqQe[_state[cx]];
            _a -= qe;
            if (_a < qe)
            {
                _c += qe;
            }
            else
            {
                _a = qe;
            }

            if (MqSwitch[_state[cx]] != 0)
            {
                _mps[cx] = 1 - _mps[cx];
            }

            _state[cx] = MqNlps[_state[cx]];
            Renorm();
        }

        private void Renorm()
        {
            do
            {
                _a <<= 1;
                _c <<= 1;
                _ct--;
                if (_ct == 0)
                {
                    ByteOut();
                }
            }
            while ((_a & 0x8000) == 0);
        }

        private void Put(long value)
        {
            _bp++;
            if (_bp >= _buf.Count)
            {
                _buf.Add(0);
            }

            _buf[_bp] = (byte)value;
        }

        private void ByteOut()
        {
            if (_buf[_bp] == 0xFF)
            {
                Put(_c >> 20);
                _c &= 0xFFFFF;
                _ct = 7;
            }
            else if ((_c & 0x8000000) == 0)
            {
                Put(_c >> 19);
                _c &= 0x7FFFF;
                _ct = 8;
            }
            else
            {
                _buf[_bp]++;
                if (_buf[_bp] == 0xFF)
                {
                    _c &= 0x7FFFFFF;
                    Put(_c >> 20);
                    _c &= 0xFFFFF;
                    _ct = 7;
                }
                else
                {
                    Put(_c >> 19);
                    _c &= 0x7FFFF;
                    _ct = 8;
                }
            }
        }

        private void SetBits()
        {
            var temp = _c + _a;
            _c |= 0xFFFF;
            if (_c >= temp)
            {
                _c -= 0x8000;
            }
        }
    }

    /// <summary>Writes raw (selective arithmetic coding bypass) bits with the 0xFF stuffing rule.</summary>
    internal sealed class RawBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _cur;
        private int _free = 8;

        /// <summary>Writes one bit.</summary>
        /// <param name="bit">The bit.</param>
        public void Write(int bit)
        {
            _free--;
            _cur |= bit << _free;
            if (_free == 0)
            {
                _bytes.Add((byte)_cur);
                _free = _cur == 0xFF ? 7 : 8;
                _cur = 0;
            }
        }

        /// <summary>Pads the last byte with zero bits, drops a trailing 0xFF and returns the segment.</summary>
        /// <returns>The bytes.</returns>
        public byte[] Flush()
        {
            var pending = _free != 8 && !(_free == 7 && _bytes.Count > 0 && _bytes[^1] == 0xFF);
            if (pending)
            {
                _bytes.Add((byte)_cur);
            }

            if (_bytes.Count > 0 && _bytes[^1] == 0xFF)
            {
                _bytes.RemoveAt(_bytes.Count - 1);
            }

            var result = _bytes.ToArray();
            _bytes.Clear();
            _cur = 0;
            _free = 8;
            return result;
        }
    }

    /// <summary>The coded form of one code-block: codeword segments with their pass counts.</summary>
    internal sealed class EncodedBlock
    {
        /// <summary>Gets or sets the number of coding passes.</summary>
        public int Passes { get; set; }

        /// <summary>Gets or sets the number of missing most significant bit-planes.</summary>
        public int ZeroBitPlanes { get; set; }

        /// <summary>Gets the codeword segments (pass count, bytes).</summary>
        public List<(int Passes, byte[] Data)> Segments { get; } = [];
    }

    /// <summary>The tier-1 (EBCOT) code-block encoder.</summary>
    internal sealed class Tier1Encoder
    {
        private const byte Sig = 1;
        private const byte Visited = 2;
        private const byte Refined = 4;

        private readonly MqEncoder _mq = new();
        private readonly RawBitWriter _raw = new();
        private byte[] _flags = [];
        private int[] _mag = [];
        private bool[] _neg = [];
        private int _w;
        private int _h;
        private int _stride;
        private int _orient;
        private bool _vcausal;

        /// <summary>Encodes a code-block.</summary>
        /// <param name="values">The signed coefficient values, row-major.</param>
        /// <param name="w">The width.</param>
        /// <param name="h">The height.</param>
        /// <param name="orient">The orientation: 0 LL, 1 HL, 2 LH, 3 HH.</param>
        /// <param name="style">The code-block style flags.</param>
        /// <param name="totalPlanes">The number of available magnitude bit-planes (Mb plus ROI shift).</param>
        /// <returns>The encoded block.</returns>
        public EncodedBlock Encode(int[] values, int w, int h, int orient, int style, int totalPlanes)
        {
            _w = w;
            _h = h;
            _orient = orient;
            _stride = w + 2;
            _vcausal = (style & 0x08) != 0;
            _flags = new byte[_stride * (h + 2)];
            _mag = new int[w * h];
            _neg = new bool[w * h];
            var max = 0;
            for (var i = 0; i < values.Length; i++)
            {
                _mag[i] = Math.Abs(values[i]);
                _neg[i] = values[i] < 0;
                max = Math.Max(max, _mag[i]);
            }

            var block = new EncodedBlock();
            var numBps = max == 0 ? 0 : 32 - int.LeadingZeroCount(max);
            if (numBps > totalPlanes)
            {
                throw new InvalidOperationException($"coefficient needs {numBps} bit-planes but only {totalPlanes} are available");
            }

            block.ZeroBitPlanes = totalPlanes - numBps;
            if (numBps == 0)
            {
                return block;
            }

            block.Passes = (3 * numBps) - 2;
            _mq.ResetContexts(true);
            RunPasses(block, numBps, style);
            return block;
        }

        private void RunPasses(EncodedBlock block, int numBps, int style)
        {
            var bypass = (style & 0x01) != 0;
            var termAll = (style & 0x04) != 0;
            var reset = (style & 0x02) != 0;
            var segSym = (style & 0x20) != 0;
            var bp = numBps - 1;
            var segPasses = 0;
            var open = false;
            var rawMode = false;
            for (var pass = 0; pass < block.Passes; pass++)
            {
                var type = (pass + 2) % 3;
                var raw = bypass && pass >= 10 && type != 2;
                if (!open)
                {
                    rawMode = raw;
                    if (rawMode)
                    {
                        _raw.Flush();
                    }
                    else
                    {
                        _mq.Start();
                    }

                    open = true;
                    segPasses = 0;
                }

                if (reset)
                {
                    _mq.ResetContexts(true);
                }

                switch (type)
                {
                    case 0:
                        SigProp(bp, raw);
                        break;
                    case 1:
                        MagRef(bp, raw);
                        break;
                    default:
                        Cleanup(bp);
                        if (segSym)
                        {
                            _mq.Encode(1, 18);
                            _mq.Encode(0, 18);
                            _mq.Encode(1, 18);
                            _mq.Encode(0, 18);
                        }

                        bp--;
                        break;
                }

                segPasses++;
                var last = pass == block.Passes - 1;
                var terminate = last || termAll || (bypass && (pass == 9 || (pass >= 10 && type != 0)));
                if (terminate)
                {
                    block.Segments.Add((segPasses, rawMode ? _raw.Flush() : _mq.Flush()));
                    open = false;
                }
            }
        }

        private int Idx(int x, int y) => ((y + 1) * _stride) + x + 1;

        private bool South(int y) => !(_vcausal && (y & 3) == 3);

        private int Neighbors(int idx, int y, out int h, out int v, out int d)
        {
            var f = _flags;
            h = (f[idx - 1] & Sig) + (f[idx + 1] & Sig);
            v = f[idx - _stride] & Sig;
            d = (f[idx - _stride - 1] & Sig) + (f[idx - _stride + 1] & Sig);
            if (South(y))
            {
                v += f[idx + _stride] & Sig;
                d += (f[idx + _stride - 1] & Sig) + (f[idx + _stride + 1] & Sig);
            }

            return h + v + d;
        }

        private int ZeroContext(int idx, int y)
        {
            Neighbors(idx, y, out var h, out var v, out var d);
            if (_orient == 3)
            {
                var hv = h + v;
                if (d >= 3)
                {
                    return 8;
                }

                if (d == 2)
                {
                    return hv >= 1 ? 7 : 6;
                }

                if (d == 1)
                {
                    return hv >= 2 ? 5 : (hv == 1 ? 4 : 3);
                }

                return hv >= 2 ? 2 : hv;
            }

            // HL swaps the roles of the horizontal and vertical neighbours.
            var a = _orient == 1 ? v : h;
            var b = _orient == 1 ? h : v;
            if (a == 2)
            {
                return 8;
            }

            if (a == 1)
            {
                return b >= 1 ? 7 : (d >= 1 ? 6 : 5);
            }

            if (b == 2)
            {
                return 4;
            }

            if (b == 1)
            {
                return 3;
            }

            return d >= 2 ? 2 : d;
        }

        private int SignContribution(int idx)
        {
            if ((_flags[idx] & Sig) == 0)
            {
                return 0;
            }

            var x = ((idx % _stride) - 1);
            var y = (idx / _stride) - 1;
            return _neg[(y * _w) + x] ? -1 : 1;
        }

        private void CodeSign(int x, int y, bool raw)
        {
            var negative = _neg[(y * _w) + x];
            if (raw)
            {
                _raw.Write(negative ? 1 : 0);
                return;
            }

            var idx = Idx(x, y);
            var hc = Math.Clamp(SignContribution(idx - 1) + SignContribution(idx + 1), -1, 1);
            var vsum = SignContribution(idx - _stride) + (South(y) ? SignContribution(idx + _stride) : 0);
            var vc = Math.Clamp(vsum, -1, 1);
            int cx;
            int xorBit;
            switch (hc, vc)
            {
                case (1, 1): cx = 13; xorBit = 0; break;
                case (1, 0): cx = 12; xorBit = 0; break;
                case (1, -1): cx = 11; xorBit = 0; break;
                case (0, 1): cx = 10; xorBit = 0; break;
                case (0, 0): cx = 9; xorBit = 0; break;
                case (0, -1): cx = 10; xorBit = 1; break;
                case (-1, 1): cx = 11; xorBit = 1; break;
                case (-1, 0): cx = 12; xorBit = 1; break;
                default: cx = 13; xorBit = 1; break;
            }

            _mq.Encode((negative ? 1 : 0) ^ xorBit, cx);
        }

        private void SigProp(int bp, bool raw)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                for (var x = 0; x < _w; x++)
                {
                    for (var y = y0; y < Math.Min(y0 + 4, _h); y++)
                    {
                        var idx = Idx(x, y);
                        if ((_flags[idx] & Sig) != 0)
                        {
                            continue;
                        }

                        var cx = ZeroContext(idx, y);
                        if (cx == 0)
                        {
                            continue;
                        }

                        var bit = (_mag[(y * _w) + x] >> bp) & 1;
                        if (raw)
                        {
                            _raw.Write(bit);
                        }
                        else
                        {
                            _mq.Encode(bit, cx);
                        }

                        _flags[idx] |= Visited;
                        if (bit != 0)
                        {
                            CodeSign(x, y, raw);
                            _flags[idx] |= Sig;
                        }
                    }
                }
            }
        }

        private void MagRef(int bp, bool raw)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                for (var x = 0; x < _w; x++)
                {
                    for (var y = y0; y < Math.Min(y0 + 4, _h); y++)
                    {
                        var idx = Idx(x, y);
                        if ((_flags[idx] & Sig) == 0 || (_flags[idx] & Visited) != 0)
                        {
                            continue;
                        }

                        var bit = (_mag[(y * _w) + x] >> bp) & 1;
                        if (raw)
                        {
                            _raw.Write(bit);
                        }
                        else
                        {
                            int cx;
                            if ((_flags[idx] & Refined) != 0)
                            {
                                cx = 16;
                            }
                            else
                            {
                                cx = Neighbors(idx, y, out _, out _, out _) > 0 ? 15 : 14;
                            }

                            _mq.Encode(bit, cx);
                        }

                        _flags[idx] |= Refined;
                    }
                }
            }
        }

        private void Cleanup(int bp)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                for (var x = 0; x < _w; x++)
                {
                    CleanupColumn(x, y0, bp);
                }
            }

            for (var i = 0; i < _flags.Length; i++)
            {
                _flags[i] &= unchecked((byte)~Visited);
            }
        }

        private void CleanupColumn(int x, int y0, int bp)
        {
            var start = y0;
            if (y0 + 4 <= _h && ColumnIsRunEligible(x, y0))
            {
                var first = -1;
                for (var k = 0; k < 4; k++)
                {
                    if (((_mag[((y0 + k) * _w) + x] >> bp) & 1) != 0)
                    {
                        first = k;
                        break;
                    }
                }

                if (first < 0)
                {
                    _mq.Encode(0, 17);
                    return;
                }

                _mq.Encode(1, 17);
                _mq.Encode(first >> 1, 18);
                _mq.Encode(first & 1, 18);
                CodeSign(x, y0 + first, false);
                _flags[Idx(x, y0 + first)] |= Sig;
                start = y0 + first + 1;
            }

            for (var y = start; y < Math.Min(y0 + 4, _h); y++)
            {
                var idx = Idx(x, y);
                if ((_flags[idx] & (Sig | Visited)) != 0)
                {
                    continue;
                }

                var bit = (_mag[(y * _w) + x] >> bp) & 1;
                _mq.Encode(bit, ZeroContext(idx, y));
                if (bit != 0)
                {
                    CodeSign(x, y, false);
                    _flags[idx] |= Sig;
                }
            }
        }

        private bool ColumnIsRunEligible(int x, int y0)
        {
            for (var k = 0; k < 4; k++)
            {
                var idx = Idx(x, y0 + k);
                if ((_flags[idx] & (Sig | Visited)) != 0 || Neighbors(idx, y0 + k, out _, out _, out _) != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Writes packet header bits with the 0xFF bit-stuffing rule.</summary>
    internal sealed class HeaderBitWriter
    {
        private readonly List<byte> _bytes = [];
        private int _cur;
        private int _free = 8;

        /// <summary>Writes one bit.</summary>
        /// <param name="bit">The bit.</param>
        public void Bit(int bit)
        {
            _free--;
            _cur |= (bit & 1) << _free;
            if (_free == 0)
            {
                _bytes.Add((byte)_cur);
                _free = _cur == 0xFF ? 7 : 8;
                _cur = 0;
            }
        }

        /// <summary>Writes <paramref name="count"/> bits, most significant first.</summary>
        /// <param name="value">The value.</param>
        /// <param name="count">The number of bits.</param>
        public void Bits(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                Bit((value >> i) & 1);
            }
        }

        /// <summary>Pads to a byte boundary and returns the header bytes.</summary>
        /// <returns>The bytes.</returns>
        public byte[] Finish()
        {
            if (_free != 8 && !(_free == 7 && _bytes.Count > 0 && _bytes[^1] == 0xFF && _cur == 0))
            {
                _bytes.Add((byte)_cur);
            }

            if (_bytes.Count > 0 && _bytes[^1] == 0xFF)
            {
                _bytes.Add(0);
            }

            return _bytes.ToArray();
        }
    }

    /// <summary>A tag tree encoder (ITU-T T.800 B.10.2).</summary>
    internal sealed class TagTreeEncoder
    {
        private readonly int[] _value;
        private readonly int[] _low;
        private readonly bool[] _known;
        private readonly int[] _offset;
        private readonly int[] _width;

        /// <summary>Initializes a tree over a <paramref name="w"/> by <paramref name="h"/> leaf grid.</summary>
        /// <param name="w">The number of leaf columns.</param>
        /// <param name="h">The number of leaf rows.</param>
        /// <param name="leaves">The leaf values in raster order.</param>
        public TagTreeEncoder(int w, int h, int[] leaves)
        {
            var offsets = new List<int>();
            var widths = new List<int>();
            var total = 0;
            var cw = w;
            var ch = h;
            while (true)
            {
                offsets.Add(total);
                widths.Add(cw);
                total += cw * ch;
                if (cw == 1 && ch == 1)
                {
                    break;
                }

                cw = (cw + 1) / 2;
                ch = (ch + 1) / 2;
            }

            _offset = [.. offsets];
            _width = [.. widths];
            _value = new int[total];
            _low = new int[total];
            _known = new bool[total];
            Array.Fill(_value, int.MaxValue);
            Array.Copy(leaves, _value, leaves.Length);
            var levelW = w;
            var levelH = h;
            for (var level = 1; level < _offset.Length; level++)
            {
                var pw = (levelW + 1) / 2;
                var ph = (levelH + 1) / 2;
                for (var y = 0; y < levelH; y++)
                {
                    for (var x = 0; x < levelW; x++)
                    {
                        var child = _value[_offset[level - 1] + (y * levelW) + x];
                        var parent = _offset[level] + ((y / 2) * pw) + (x / 2);
                        _value[parent] = Math.Min(_value[parent], child);
                    }
                }

                levelW = pw;
                levelH = ph;
            }
        }

        /// <summary>Emits the bits that tell a decoder whether the leaf value is below <paramref name="threshold"/>.</summary>
        /// <param name="bw">The bit writer.</param>
        /// <param name="x">The leaf column.</param>
        /// <param name="y">The leaf row.</param>
        /// <param name="threshold">The threshold.</param>
        public void Encode(HeaderBitWriter bw, int x, int y, int threshold)
        {
            var low = 0;
            for (var level = _offset.Length - 1; level >= 0; level--)
            {
                var node = _offset[level] + ((y >> level) * _width[level]) + (x >> level);
                if (low > _low[node])
                {
                    _low[node] = low;
                }
                else
                {
                    low = _low[node];
                }

                while (low < threshold)
                {
                    if (low >= _value[node])
                    {
                        if (!_known[node])
                        {
                            bw.Bit(1);
                            _known[node] = true;
                        }

                        break;
                    }

                    bw.Bit(0);
                    low++;
                }

                _low[node] = low;
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Forward wavelet transforms (ITU-T T.800 Annex F), operating on a Mallat-layout plane
    // ------------------------------------------------------------------------------------------

    private const double Alpha = -1.586134342059924;
    private const double Beta = -0.052980118572961;
    private const double Gamma = 0.882911075530934;
    private const double Delta = 0.443506852043971;
    private const double K = 1.230174104914001;

    /// <summary>Applies the forward 5/3 transform of one line in place (interleaved, absolute parity <paramref name="parity"/>).</summary>
    private static void Forward53(int[] x, int n, int parity)
    {
        if (n == 1)
        {
            if (parity == 1)
            {
                x[0] *= 2;
            }

            return;
        }

        for (var i = 1 - parity; i < n; i += 2)
        {
            x[i] -= (x[Mirror(i - 1, n)] + x[Mirror(i + 1, n)]) >> 1;
        }

        for (var i = parity; i < n; i += 2)
        {
            x[i] += (x[Mirror(i - 1, n)] + x[Mirror(i + 1, n)] + 2) >> 2;
        }
    }

    /// <summary>Applies the forward 9/7 transform of one line in place.</summary>
    private static void Forward97(double[] x, int n, int parity)
    {
        if (n == 1)
        {
            if (parity == 1)
            {
                x[0] *= 2;
            }

            return;
        }

        Lift(x, n, 1 - parity, Alpha);
        Lift(x, n, parity, Beta);
        Lift(x, n, 1 - parity, Gamma);
        Lift(x, n, parity, Delta);
        for (var i = 0; i < n; i++)
        {
            x[i] *= ((i + parity) & 1) == 0 ? 1.0 / K : K;
        }
    }

    private static void Lift(double[] x, int n, int first, double coefficient)
    {
        for (var i = first; i < n; i += 2)
        {
            x[i] += coefficient * (x[Mirror(i - 1, n)] + x[Mirror(i + 1, n)]);
        }
    }

    private static int Mirror(int i, int n)
    {
        if (i < 0)
        {
            return -i;
        }

        return i >= n ? (2 * (n - 1)) - i : i;
    }

    /// <summary>Runs the forward 2-D transform over all decomposition levels of a tile-component plane.</summary>
    /// <typeparam name="T">The sample type (<see cref="int"/> or <see cref="double"/>).</typeparam>
    /// <param name="plane">The plane, row stride <paramref name="stride"/>.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="rects">Per resolution (lowest first): x0, y0, x1, y1 in resolution coordinates.</param>
    /// <param name="line">The 1-D transform.</param>
    private static void ForwardDwt<T>(T[] plane, int stride, (long X0, long Y0, long X1, long Y1)[] rects, Action<T[], int, int> line)
    {
        for (var r = rects.Length - 1; r >= 1; r--)
        {
            var rc = rects[r];
            var w = (int)(rc.X1 - rc.X0);
            var h = (int)(rc.Y1 - rc.Y0);
            var px = (int)(rc.X0 & 1);
            var py = (int)(rc.Y0 & 1);
            var buf = new T[Math.Max(w, h)];
            for (var x = 0; x < w; x++)
            {
                for (var y = 0; y < h; y++)
                {
                    buf[y] = plane[(y * stride) + x];
                }

                line(buf, h, py);
                Deinterleave(buf, h, py, plane, x, stride);
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    buf[x] = plane[(y * stride) + x];
                }

                line(buf, w, px);
                Deinterleave(buf, w, px, plane, y * stride, 1);
            }
        }
    }

    private static void Deinterleave<T>(T[] buf, int n, int parity, T[] plane, int start, int step)
    {
        var lowCount = 0;
        for (var i = 0; i < n; i++)
        {
            if (((i + parity) & 1) == 0)
            {
                lowCount++;
            }
        }

        var lo = 0;
        var hi = lowCount;
        for (var i = 0; i < n; i++)
        {
            if (((i + parity) & 1) == 0)
            {
                plane[start + (lo++ * step)] = buf[i];
            }
            else
            {
                plane[start + (hi++ * step)] = buf[i];
            }
        }
    }
}
