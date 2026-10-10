namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Tier-1 (EBCOT) code-block decoding (ITU-T T.800 Annex D) and de-quantization (Annex E)
    // ================================================================================================

    /// <summary>Flag bit: the coefficient is significant.</summary>
    private const byte FlagSig = 1;

    /// <summary>Flag bit: the coefficient was coded in the significance propagation pass of the current bit-plane.</summary>
    private const byte FlagVisit = 2;

    /// <summary>Flag bit: the coefficient has been refined at least once.</summary>
    private const byte FlagRefined = 4;

    /// <summary>Flag bit: the coefficient is negative.</summary>
    private const byte FlagNeg = 8;

    /// <summary>Code-block style bit: selective arithmetic coding bypass.</summary>
    private const int StyleBypass = 0x01;

    /// <summary>Code-block style bit: reset context probabilities on each pass.</summary>
    private const int StyleReset = 0x02;

    /// <summary>Code-block style bit: termination on each coding pass.</summary>
    internal const int StyleTermAll = 0x04;

    /// <summary>Code-block style bit: vertically causal context.</summary>
    private const int StyleVCausal = 0x08;

    /// <summary>Code-block style bit: segmentation symbols.</summary>
    private const int StyleSegSym = 0x20;

    /// <summary>A shared work counter that bounds the total tier-1 effort of one decode.</summary>
    internal sealed class WorkBudget
    {
        private long _used;

        /// <summary>Charges <paramref name="amount"/> units of work and fails when the cap is exceeded.</summary>
        /// <param name="amount">The work to charge.</param>
        public void Charge(long amount)
        {
            _used += amount;
            if (_used > MaxTier1Work)
            {
                throw Malformed("image requires too much entropy-decoding work.");
            }
        }
    }

    /// <summary>Decodes code-blocks; one instance owns the scratch buffers and is reused across blocks.</summary>
    internal sealed class Tier1Decoder
    {
        private readonly MqDecoder _mq = new();
        private readonly RawBitReader _raw = new();
        private byte[] _flags = [];
        private int[] _mag = [];
        private byte[] _low = [];
        private int _w;
        private int _h;
        private int _stride;
        private int _orient;
        private bool _vcausal;

        /// <summary>Decodes one code-block and stores its de-quantized coefficients in a component plane.</summary>
        /// <param name="block">The code-block with its gathered codeword segments.</param>
        /// <param name="band">The owning sub-band.</param>
        /// <param name="style">The code-block style flags.</param>
        /// <param name="roiShift">The region-of-interest shift.</param>
        /// <param name="reversible">Whether the 5/3 reversible path is in use.</param>
        /// <param name="ints">The integer plane (reversible path) or <see langword="null"/>.</param>
        /// <param name="floats">The float plane (irreversible path) or <see langword="null"/>.</param>
        /// <param name="planeStride">The row stride of the plane.</param>
        /// <param name="work">The work budget.</param>
        public void DecodeBlock(
            CodeBlock block, Band band, int style, int roiShift, bool reversible, int[]? ints, float[]? floats, int planeStride,
            WorkBudget work)
        {
            if (block.Segments.Count == 0 || block.TotalPasses == 0)
            {
                return;
            }

            _w = block.X1 - block.X0;
            _h = block.Y1 - block.Y0;
            var numPlanes = band.Mb + roiShift - block.ZeroBitPlanes;
            if (numPlanes <= 0)
            {
                return;
            }

            if (numPlanes > 30)
            {
                throw Unsupported("jpeg2000-bit-depth", "JPEG 2000 code-block has more than 30 magnitude bit-planes.");
            }

            work.Charge((long)_w * _h * block.TotalPasses);
            Prepare(style, band.Orient);
            RunPasses(block, style, numPlanes);
            Store(block, band, roiShift, reversible, ints, floats, planeStride);
        }

        private void Prepare(int style, int orient)
        {
            _stride = _w + 2;
            var flagCount = _stride * (_h + 2);
            if (_flags.Length < flagCount)
            {
                _flags = new byte[flagCount];
            }
            else
            {
                Array.Clear(_flags, 0, flagCount);
            }

            var n = _w * _h;
            if (_mag.Length < n)
            {
                _mag = new int[n];
                _low = new byte[n];
            }
            else
            {
                Array.Clear(_mag, 0, n);
                Array.Clear(_low, 0, n);
            }

            _orient = orient;
            _vcausal = (style & StyleVCausal) != 0;
            _mq.ResetContexts();
        }

        private void RunPasses(CodeBlock block, int style, int numPlanes)
        {
            var passIndex = 0;
            var bp = numPlanes - 1;
            var offset = 0;
            foreach (var seg in block.Segments)
            {
                if (seg.Passes == 0)
                {
                    continue;
                }

                var firstType = (passIndex + 2) % 3;
                var raw = (style & StyleBypass) != 0 && passIndex >= 10 && firstType != 2;
                var decode = seg.Length > 0 && offset + seg.Length <= block.DataLength;
                if (decode)
                {
                    if (raw)
                    {
                        _raw.Init(block.Data, offset, seg.Length);
                    }
                    else
                    {
                        _mq.Init(block.Data, offset, seg.Length);
                    }
                }

                for (var k = 0; k < seg.Passes; k++)
                {
                    if (bp < 0)
                    {
                        throw Malformed("code-block has more coding passes than bit-planes.");
                    }

                    var type = (passIndex + 2) % 3;
                    if (decode)
                    {
                        RunPass(type, bp, raw, style);
                    }

                    if (type == 2)
                    {
                        bp--;
                    }

                    passIndex++;
                }

                offset += seg.Length;
            }
        }

        private void RunPass(int type, int bp, bool raw, int style)
        {
            if ((style & StyleReset) != 0)
            {
                _mq.ResetContexts();
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
                    if ((style & StyleSegSym) != 0)
                    {
                        for (var i = 0; i < 4; i++)
                        {
                            _mq.DecodeBit(18);
                        }
                    }

                    break;
            }
        }

        private void SigProp(int bp, bool raw)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                var y1 = Math.Min(y0 + 4, _h);
                for (var x = 0; x < _w; x++)
                {
                    for (var y = y0; y < y1; y++)
                    {
                        var idx = ((y + 1) * _stride) + x + 1;
                        var f = _flags[idx];
                        if ((f & FlagSig) != 0)
                        {
                            continue;
                        }

                        var ctx = ZeroContext(idx, y);
                        if (ctx == 0)
                        {
                            continue;
                        }

                        int bit = raw ? _raw.ReadBit() : _mq.DecodeBit(ctx);
                        _flags[idx] |= FlagVisit;
                        if (bit != 0)
                        {
                            BecomeSignificant(idx, x, y, bp, raw);
                        }
                    }
                }
            }
        }

        private void MagRef(int bp, bool raw)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                var y1 = Math.Min(y0 + 4, _h);
                for (var x = 0; x < _w; x++)
                {
                    for (var y = y0; y < y1; y++)
                    {
                        var idx = ((y + 1) * _stride) + x + 1;
                        var f = _flags[idx];
                        if ((f & FlagSig) == 0 || (f & FlagVisit) != 0)
                        {
                            continue;
                        }

                        int bit;
                        if (raw)
                        {
                            bit = _raw.ReadBit();
                        }
                        else
                        {
                            int ctx;
                            if ((f & FlagRefined) != 0)
                            {
                                ctx = 16;
                            }
                            else
                            {
                                ctx = HasSignificantNeighbor(idx, y) ? 15 : 14;
                            }

                            bit = _mq.DecodeBit(ctx);
                        }

                        var m = (y * _w) + x;
                        _mag[m] |= bit << bp;
                        _low[m] = (byte)bp;
                        _flags[idx] |= FlagRefined;
                    }
                }
            }
        }

        private void Cleanup(int bp)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                var full = y0 + 4 <= _h;
                for (var x = 0; x < _w; x++)
                {
                    var start = y0;
                    if (full && RunLengthEligible(x, y0))
                    {
                        if (_mq.DecodeBit(17) == 0)
                        {
                            continue;
                        }

                        var run = (_mq.DecodeBit(18) << 1) | _mq.DecodeBit(18);
                        var y = y0 + run;
                        BecomeSignificant(((y + 1) * _stride) + x + 1, x, y, bp, false);
                        start = y + 1;
                    }

                    for (var y = start; y < Math.Min(y0 + 4, _h); y++)
                    {
                        var idx = ((y + 1) * _stride) + x + 1;
                        if ((_flags[idx] & (FlagSig | FlagVisit)) != 0)
                        {
                            continue;
                        }

                        if (_mq.DecodeBit(ZeroContext(idx, y)) != 0)
                        {
                            BecomeSignificant(idx, x, y, bp, false);
                        }
                    }
                }
            }

            // The visit marks only live for one bit-plane.
            for (var y = 0; y < _h; y++)
            {
                var row = ((y + 1) * _stride) + 1;
                for (var x = 0; x < _w; x++)
                {
                    _flags[row + x] &= unchecked((byte)~FlagVisit);
                }
            }
        }

        private bool RunLengthEligible(int x, int y0)
        {
            for (var k = 0; k < 4; k++)
            {
                var y = y0 + k;
                var idx = ((y + 1) * _stride) + x + 1;
                if ((_flags[idx] & (FlagSig | FlagVisit)) != 0 || ZeroContext(idx, y) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void BecomeSignificant(int idx, int x, int y, int bp, bool raw)
        {
            int sign;
            if (raw)
            {
                sign = _raw.ReadBit();
            }
            else
            {
                sign = SignBit(idx, y);
            }

            _flags[idx] |= (byte)(FlagSig | (sign != 0 ? FlagNeg : 0));
            var m = (y * _w) + x;
            _mag[m] |= 1 << bp;
            _low[m] = (byte)bp;
        }

        private bool NoSouth(int y) => _vcausal && (y & 3) == 3;

        private bool HasSignificantNeighbor(int idx, int y) => ZeroContext(idx, y) != 0;

        private int ZeroContext(int idx, int y)
        {
            var f = _flags;
            var s = _stride;
            var h = (f[idx - 1] & FlagSig) + (f[idx + 1] & FlagSig);
            var v = f[idx - s] & FlagSig;
            var d = (f[idx - s - 1] & FlagSig) + (f[idx - s + 1] & FlagSig);
            if (!NoSouth(y))
            {
                v += f[idx + s] & FlagSig;
                d += (f[idx + s - 1] & FlagSig) + (f[idx + s + 1] & FlagSig);
            }

            return _orient switch
            {
                3 => HighHighContext(h + v, d),
                1 => LowHighContext(v, h, d),
                _ => LowHighContext(h, v, d),
            };
        }

        private static int LowHighContext(int primary, int secondary, int d)
        {
            if (primary == 2)
            {
                return 8;
            }

            if (primary == 1)
            {
                if (secondary >= 1)
                {
                    return 7;
                }

                return d >= 1 ? 6 : 5;
            }

            if (secondary == 2)
            {
                return 4;
            }

            if (secondary == 1)
            {
                return 3;
            }

            return d >= 2 ? 2 : d;
        }

        private static int HighHighContext(int hv, int d)
        {
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
                if (hv >= 2)
                {
                    return 5;
                }

                return hv == 1 ? 4 : 3;
            }

            return hv >= 2 ? 2 : hv;
        }

        private int Contribution(int idx)
        {
            var f = _flags[idx];
            if ((f & FlagSig) == 0)
            {
                return 0;
            }

            return (f & FlagNeg) != 0 ? -1 : 1;
        }

        private int SignBit(int idx, int y)
        {
            var hc = Math.Clamp(Contribution(idx - 1) + Contribution(idx + 1), -1, 1);
            var vcSum = Contribution(idx - _stride);
            if (!NoSouth(y))
            {
                vcSum += Contribution(idx + _stride);
            }

            var vc = Math.Clamp(vcSum, -1, 1);
            int ctx;
            int xor = 0;
            if (hc == 1)
            {
                ctx = 12 + vc;
            }
            else if (hc == 0)
            {
                if (vc == 0)
                {
                    ctx = 9;
                }
                else
                {
                    ctx = 10;
                    xor = vc == -1 ? 1 : 0;
                }
            }
            else
            {
                ctx = 12 - vc;
                xor = 1;
            }

            return _mq.DecodeBit(ctx) ^ xor;
        }

        private void Store(CodeBlock block, Band band, int roiShift, bool reversible, int[]? ints, float[]? floats, int planeStride)
        {
            for (var y = 0; y < _h; y++)
            {
                var rowBase = ((band.OffY + block.Y0 + y) * planeStride) + band.OffX + block.X0;
                for (var x = 0; x < _w; x++)
                {
                    var m = (y * _w) + x;
                    var mag = _mag[m];
                    if (mag == 0)
                    {
                        continue;
                    }

                    var low = (int)_low[m];
                    if (roiShift > 0 && mag >= (1 << Math.Min(roiShift, 30)))
                    {
                        mag >>= Math.Min(roiShift, 31);
                        low = Math.Max(0, low - Math.Min(roiShift, 31));
                    }

                    var neg = (_flags[((y + 1) * _stride) + x + 1] & FlagNeg) != 0;
                    if (reversible)
                    {
                        var v = low > 0 ? mag + (1 << (low - 1)) : mag;
                        ints![rowBase + x] = neg ? -v : v;
                    }
                    else
                    {
                        var v = (mag + (0.5f * (1 << low))) * band.Step;
                        floats![rowBase + x] = neg ? -v : v;
                    }
                }
            }
        }
    }
}
