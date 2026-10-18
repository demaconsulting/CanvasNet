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

    /// <summary>Describes the component plane a decoded code-block is stored into.</summary>
    /// <param name="Reversible">Whether the 5/3 reversible path is in use.</param>
    /// <param name="Ints">The integer plane (reversible path) or <see langword="null"/>.</param>
    /// <param name="Floats">The float plane (irreversible path) or <see langword="null"/>.</param>
    /// <param name="PlaneStride">The row stride of the plane.</param>
    internal readonly record struct BlockOutput(bool Reversible, int[]? Ints, float[]? Floats, int PlaneStride);

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
        /// <param name="output">The destination plane description.</param>
        /// <param name="budget">The decode-wide budget.</param>
        public void DecodeBlock(CodeBlock block, Band band, int style, int roiShift, BlockOutput output, DecodeBudget budget)
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

            // numPlanes never exceeds MaxBitPlanes: geometry construction validated Mb plus the ROI shift.
            budget.ChargeTier1((long)_w * _h * block.TotalPasses);
            Prepare(style, band.Orient);
            RunPasses(block, style, numPlanes);
            Store(block, band, roiShift, output);
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
                    InitSegment(block, offset, seg.Length, raw);
                }

                RunSegmentPasses(seg.Passes, decode, raw, style, ref passIndex, ref bp);
                offset += seg.Length;
            }
        }

        private void InitSegment(CodeBlock block, int offset, int length, bool raw)
        {
            if (raw)
            {
                _raw.Init(block.Data, offset, length);
            }
            else
            {
                _mq.Init(block.Data, offset, length);
            }
        }

        private void RunSegmentPasses(int passes, bool decode, bool raw, int style, ref int passIndex, ref int bp)
        {
            for (var k = 0; k < passes; k++)
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
                    // The segmentation symbol is the four decisions 1010 on the uniform context.
                    if ((style & StyleSegSym) != 0
                        && (_mq.DecodeBit(18) != 1 || _mq.DecodeBit(18) != 0 || _mq.DecodeBit(18) != 1 || _mq.DecodeBit(18) != 0))
                    {
                        throw Malformed("segmentation symbol is wrong; the code-block is corrupt.");
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
                        SigPropSample(x, y, bp, raw);
                    }
                }
            }
        }

        private void SigPropSample(int x, int y, int bp, bool raw)
        {
            var idx = ((y + 1) * _stride) + x + 1;
            if ((_flags[idx] & FlagSig) != 0)
            {
                return;
            }

            var ctx = ZeroContext(idx, y);
            if (ctx == 0)
            {
                return;
            }

            int bit = raw ? _raw.ReadBit() : _mq.DecodeBit(ctx);
            _flags[idx] |= FlagVisit;
            if (bit != 0)
            {
                BecomeSignificant(idx, x, y, bp, raw);
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
                        MagRefSample(x, y, bp, raw);
                    }
                }
            }
        }

        private void MagRefSample(int x, int y, int bp, bool raw)
        {
            var idx = ((y + 1) * _stride) + x + 1;
            var f = _flags[idx];
            if ((f & FlagSig) == 0 || (f & FlagVisit) != 0)
            {
                return;
            }

            int bit;
            if (raw)
            {
                bit = _raw.ReadBit();
            }
            else if ((f & FlagRefined) != 0)
            {
                bit = _mq.DecodeBit(16);
            }
            else
            {
                bit = _mq.DecodeBit(HasSignificantNeighbor(idx, y) ? 15 : 14);
            }

            var m = (y * _w) + x;
            _mag[m] |= bit << bp;
            _low[m] = (byte)bp;
            _flags[idx] |= FlagRefined;
        }

        private void Cleanup(int bp)
        {
            for (var y0 = 0; y0 < _h; y0 += 4)
            {
                var full = y0 + 4 <= _h;
                for (var x = 0; x < _w; x++)
                {
                    CleanupColumn(x, y0, full, bp);
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

        private void CleanupColumn(int x, int y0, bool full, int bp)
        {
            var start = y0;
            if (full && RunLengthEligible(x, y0))
            {
                if (_mq.DecodeBit(17) == 0)
                {
                    return;
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
            var sign = raw ? _raw.ReadBit() : SignBit(idx, y);

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

        private void Store(CodeBlock block, Band band, int roiShift, BlockOutput output)
        {
            for (var y = 0; y < _h; y++)
            {
                var rowBase = ((band.OffY + block.Y0 + y) * output.PlaneStride) + band.OffX + block.X0;
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
                    StoreSample(output, band, rowBase + x, mag, low, neg);
                }
            }
        }

        private static void StoreSample(BlockOutput output, Band band, int position, int mag, int low, bool neg)
        {
            if (output.Reversible)
            {
                var v = low > 0 ? mag + (1 << (low - 1)) : mag;
                output.Ints![position] = neg ? -v : v;
            }
            else
            {
                var v = (mag + (0.5f * (1 << low))) * band.Step;
                output.Floats![position] = neg ? -v : v;
            }
        }
    }
}
