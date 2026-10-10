namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Tier-2: tag trees, packet header bit reader, progression orders and packet parsing
    // ================================================================================================

    /// <summary>Identifies one packet: a precinct of one resolution of one component in one layer.</summary>
    /// <param name="Layer">The layer index.</param>
    /// <param name="Res">The resolution index.</param>
    /// <param name="Comp">The component index.</param>
    /// <param name="Precinct">The precinct index within the resolution (raster order).</param>
    internal readonly record struct PacketId(int Layer, int Res, int Comp, int Precinct);

    /// <summary>A bounded forward cursor over a byte array.</summary>
    internal sealed class ByteCursor
    {
        /// <summary>Initializes a new instance of the <see cref="ByteCursor"/> class.</summary>
        /// <param name="data">The backing array.</param>
        public ByteCursor(byte[] data)
        {
            Data = data;
            End = data.Length;
        }

        /// <summary>Gets the backing array.</summary>
        public byte[] Data { get; }

        /// <summary>Gets the exclusive end offset.</summary>
        public int End { get; }

        /// <summary>Gets or sets the current offset.</summary>
        public int Pos { get; set; }

        /// <summary>Gets the number of unread bytes.</summary>
        public int Remaining => End - Pos;
    }

    /// <summary>Reads packet header bits with the 0xFF bit-stuffing rule of ITU-T T.800 B.10.1.</summary>
    internal sealed class PacketBitReader
    {
        private readonly ByteCursor _cursor;
        private int _current;
        private int _bitsLeft;
        private int _last;

        /// <summary>Initializes a new instance of the <see cref="PacketBitReader"/> class.</summary>
        /// <param name="cursor">The cursor to read header bytes from.</param>
        public PacketBitReader(ByteCursor cursor) => _cursor = cursor;

        /// <summary>Reads one bit.</summary>
        /// <returns>0 or 1.</returns>
        public int ReadBit()
        {
            if (_bitsLeft == 0)
            {
                if (_cursor.Pos >= _cursor.End)
                {
                    throw Malformed("packet header is truncated.");
                }

                // After a 0xFF byte the next byte carries only seven bits (its MSB is a stuffed zero).
                _bitsLeft = _last == 0xFF ? 7 : 8;
                _current = _cursor.Data[_cursor.Pos++];
                _last = _current;
            }

            _bitsLeft--;
            return (_current >> _bitsLeft) & 1;
        }

        /// <summary>Reads <paramref name="count"/> bits, most significant first.</summary>
        /// <param name="count">The number of bits, 0 to 31.</param>
        /// <returns>The value.</returns>
        public int ReadBits(int count)
        {
            var v = 0;
            for (var i = 0; i < count; i++)
            {
                v = (v << 1) | ReadBit();
            }

            return v;
        }

        /// <summary>Finishes the header: discards unread bits and consumes a stuffed byte after a trailing 0xFF.</summary>
        public void Align()
        {
            _bitsLeft = 0;
            if (_last == 0xFF)
            {
                if (_cursor.Pos >= _cursor.End)
                {
                    throw Malformed("packet header is truncated.");
                }

                _cursor.Pos++;
            }

            _last = 0;
        }
    }

    /// <summary>A tag tree decoder (ITU-T T.800 B.10.2).</summary>
    internal sealed class TagTree
    {
        private readonly int[] _value;
        private readonly int[] _low;
        private readonly int[] _levelOffset;
        private readonly int[] _levelWidth;

        /// <summary>Initializes a new instance of the <see cref="TagTree"/> class.</summary>
        /// <param name="width">The number of leaf columns.</param>
        /// <param name="height">The number of leaf rows.</param>
        public TagTree(int width, int height)
        {
            var offsets = new List<int>();
            var widths = new List<int>();
            var total = 0;
            var w = width;
            var h = height;
            while (true)
            {
                offsets.Add(total);
                widths.Add(w);
                total += w * h;
                if (w == 1 && h == 1)
                {
                    break;
                }

                w = (w + 1) / 2;
                h = (h + 1) / 2;
            }

            _levelOffset = [.. offsets];
            _levelWidth = [.. widths];
            _value = new int[total];
            _low = new int[total];
            Array.Fill(_value, int.MaxValue);
        }

        /// <summary>Decodes whether the leaf value is strictly below <paramref name="threshold"/>.</summary>
        /// <param name="br">The packet header bit reader.</param>
        /// <param name="x">The leaf column.</param>
        /// <param name="y">The leaf row.</param>
        /// <param name="threshold">The threshold.</param>
        /// <returns><see langword="true"/> when the leaf value is known to be below the threshold.</returns>
        public bool DecodeBelow(PacketBitReader br, int x, int y, int threshold)
        {
            var low = 0;
            var node = 0;
            for (var level = _levelOffset.Length - 1; level >= 0; level--)
            {
                node = _levelOffset[level] + ((y >> level) * _levelWidth[level]) + (x >> level);
                if (low > _low[node])
                {
                    _low[node] = low;
                }
                else
                {
                    low = _low[node];
                }

                while (low < threshold && low < _value[node])
                {
                    if (br.ReadBit() == 1)
                    {
                        _value[node] = low;
                    }
                    else
                    {
                        low++;
                    }
                }

                _low[node] = low;
            }

            return _value[node] < threshold;
        }
    }

    /// <summary>Counts progression iterator steps and aborts when the budget is exhausted.</summary>
    private sealed class StepBudget
    {
        private long _steps;

        /// <summary>Counts one step.</summary>
        public void Step()
        {
            if (++_steps > MaxProgressionSteps)
            {
                throw Malformed("progression order iteration exceeds the decoder limit.");
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Progression order iteration (Annex B.12)
    // ------------------------------------------------------------------------------------------

    private static IEnumerable<PacketId> EnumeratePackets(Tile tile, SizInfo siz, int layers, List<ProgressionChange> changes, StepBudget steps)
    {
        var maxRes = 0;
        foreach (var tc in tile.Comps)
        {
            maxRes = Math.Max(maxRes, tc.Res.Length);
        }

        // Smallest precinct step on the reference grid, used by the position-based progressions.
        var dx = long.MaxValue;
        var dy = long.MaxValue;
        for (var c = 0; c < tile.Comps.Length; c++)
        {
            var tc = tile.Comps[c];
            for (var r = 0; r < tc.Res.Length; r++)
            {
                var lvl = tc.Coding.Levels - r;
                dx = Math.Min(dx, (long)siz.XR[c] << (tc.Res[r].PPx + lvl));
                dy = Math.Min(dy, (long)siz.YR[c] << (tc.Res[r].PPy + lvl));
            }
        }

        foreach (var ch in changes)
        {
            var layerEnd = Math.Min(ch.LayerEnd, layers);
            var resEnd = Math.Min(ch.ResEnd, maxRes);
            var compEnd = Math.Min(ch.CompEnd, tile.Comps.Length);
            IEnumerable<PacketId> sequence = ch.Order switch
            {
                0 => EnumerateLrcp(tile, ch, layerEnd, resEnd, compEnd, steps),
                1 => EnumerateRlcp(tile, ch, layerEnd, resEnd, compEnd, steps),
                _ => EnumeratePositional(tile, siz, ch, layerEnd, resEnd, compEnd, dx, dy, steps),
            };
            foreach (var id in sequence)
            {
                yield return id;
            }
        }
    }

    private static IEnumerable<PacketId> EnumerateLrcp(Tile tile, ProgressionChange ch, int layerEnd, int resEnd, int compEnd, StepBudget steps)
    {
        for (var l = 0; l < layerEnd; l++)
        {
            for (var r = ch.ResStart; r < resEnd; r++)
            {
                for (var c = ch.CompStart; c < compEnd; c++)
                {
                    steps.Step();
                    if (r >= tile.Comps[c].Res.Length)
                    {
                        continue;
                    }

                    var n = tile.Comps[c].Res[r].Precincts.Length;
                    for (var k = 0; k < n; k++)
                    {
                        yield return new PacketId(l, r, c, k);
                    }
                }
            }
        }
    }

    private static IEnumerable<PacketId> EnumerateRlcp(Tile tile, ProgressionChange ch, int layerEnd, int resEnd, int compEnd, StepBudget steps)
    {
        for (var r = ch.ResStart; r < resEnd; r++)
        {
            for (var l = 0; l < layerEnd; l++)
            {
                for (var c = ch.CompStart; c < compEnd; c++)
                {
                    steps.Step();
                    if (r >= tile.Comps[c].Res.Length)
                    {
                        continue;
                    }

                    var n = tile.Comps[c].Res[r].Precincts.Length;
                    for (var k = 0; k < n; k++)
                    {
                        yield return new PacketId(l, r, c, k);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Finds the precinct of component <paramref name="c"/> resolution <paramref name="r"/> whose
    ///     reference-grid anchor is exactly the position (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    private static bool TryFindPrecinct(Tile tile, SizInfo siz, int c, int r, long x, long y, out int precinct)
    {
        precinct = 0;
        var tc = tile.Comps[c];
        if (r >= tc.Res.Length)
        {
            return false;
        }

        var res = tc.Res[r];
        if (res.Pw == 0 || res.Ph == 0)
        {
            return false;
        }

        var lvl = tc.Coding.Levels - r;
        long xr = siz.XR[c];
        long yr = siz.YR[c];
        var rpx = res.PPx + lvl;
        var rpy = res.PPy + lvl;
        var yOk = (y % (yr << rpy)) == 0 || (y == tile.Y0 && ((res.Y0 << lvl) % (1L << rpy)) != 0);
        var xOk = (x % (xr << rpx)) == 0 || (x == tile.X0 && ((res.X0 << lvl) % (1L << rpx)) != 0);
        if (!yOk || !xOk)
        {
            return false;
        }

        var pi = (CeilDiv(x, xr << lvl) >> res.PPx) - (res.X0 >> res.PPx);
        var pj = (CeilDiv(y, yr << lvl) >> res.PPy) - (res.Y0 >> res.PPy);
        if (pi < 0 || pi >= res.Pw || pj < 0 || pj >= res.Ph)
        {
            return false;
        }

        precinct = (int)(pi + (pj * res.Pw));
        return true;
    }

    private static IEnumerable<PacketId> EnumeratePositional(
        Tile tile, SizInfo siz, ProgressionChange ch, int layerEnd, int resEnd, int compEnd, long dx, long dy, StepBudget steps)
    {
        // RPCL = 2, PCRL = 3, CPRL = 4: the outer loops differ, the position loops are shared.
        if (ch.Order == 2)
        {
            for (var r = ch.ResStart; r < resEnd; r++)
            {
                for (var y = tile.Y0; y < tile.Y1; y += dy - (y % dy))
                {
                    for (var x = tile.X0; x < tile.X1; x += dx - (x % dx))
                    {
                        for (var c = ch.CompStart; c < compEnd; c++)
                        {
                            steps.Step();
                            if (!TryFindPrecinct(tile, siz, c, r, x, y, out var k))
                            {
                                continue;
                            }

                            for (var l = 0; l < layerEnd; l++)
                            {
                                yield return new PacketId(l, r, c, k);
                            }
                        }
                    }
                }
            }
        }
        else if (ch.Order == 3)
        {
            for (var y = tile.Y0; y < tile.Y1; y += dy - (y % dy))
            {
                for (var x = tile.X0; x < tile.X1; x += dx - (x % dx))
                {
                    for (var c = ch.CompStart; c < compEnd; c++)
                    {
                        for (var r = ch.ResStart; r < resEnd; r++)
                        {
                            steps.Step();
                            if (!TryFindPrecinct(tile, siz, c, r, x, y, out var k))
                            {
                                continue;
                            }

                            for (var l = 0; l < layerEnd; l++)
                            {
                                yield return new PacketId(l, r, c, k);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            for (var c = ch.CompStart; c < compEnd; c++)
            {
                for (var y = tile.Y0; y < tile.Y1; y += dy - (y % dy))
                {
                    for (var x = tile.X0; x < tile.X1; x += dx - (x % dx))
                    {
                        for (var r = ch.ResStart; r < resEnd; r++)
                        {
                            steps.Step();
                            if (!TryFindPrecinct(tile, siz, c, r, x, y, out var k))
                            {
                                continue;
                            }

                            for (var l = 0; l < layerEnd; l++)
                            {
                                yield return new PacketId(l, r, c, k);
                            }
                        }
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Packet parsing (Annex B.10)
    // ------------------------------------------------------------------------------------------

    /// <summary>Reads every packet of a tile in progression order, filling the code-block segment data.</summary>
    /// <param name="tile">The built tile geometry.</param>
    /// <param name="st">The coding state of the tile.</param>
    /// <param name="siz">The image size information.</param>
    /// <param name="data">The collected tile-part data.</param>
    private static void ReadTilePackets(Tile tile, CodingState st, SizInfo siz, TileData data)
    {
        var body = new ByteCursor(data.Body.ToArray());
        var headers = data.UsesPackedHeaders ? new ByteCursor(data.PackedHeaders.ToArray()) : body;

        // Per (component, resolution) base index so each packet has a unique slot in the "seen" bitmap.
        var bases = new long[tile.Comps.Length][];
        long running = 0;
        for (var c = 0; c < tile.Comps.Length; c++)
        {
            bases[c] = new long[tile.Comps[c].Res.Length];
            for (var r = 0; r < bases[c].Length; r++)
            {
                bases[c][r] = running;
                running += tile.Comps[c].Res[r].Precincts.Length;
            }
        }

        var layers = st.Layers;
        var seen = new bool[running * layers];
        var changes = st.Poc ?? [new ProgressionChange(0, 0, layers, 33, tile.Comps.Length, st.Progression)];
        var pending = new List<(CodeBlock Block, Segment Seg, int Length)>();
        foreach (var id in EnumeratePackets(tile, siz, layers, changes, new StepBudget()))
        {
            var slot = ((bases[id.Comp][id.Res] + id.Precinct) * layers) + id.Layer;
            if (seen[slot])
            {
                continue;
            }

            seen[slot] = true;
            var precinct = tile.Comps[id.Comp].Res[id.Res].Precincts[id.Precinct];
            ReadPacket(precinct, tile.Comps[id.Comp].Coding.Style, id.Layer, st, body, headers, pending);
        }
    }

    private static void ReadPacket(
        Precinct precinct, int style, int layer, CodingState st, ByteCursor body, ByteCursor headers,
        List<(CodeBlock Block, Segment Seg, int Length)> pending)
    {
        // Optional SOP marker segment in front of the packet (always in the body stream).
        if (st.Sop && body.Remaining >= 6 && body.Data[body.Pos] == 0xFF && body.Data[body.Pos + 1] == 0x91)
        {
            body.Pos += 6;
        }

        var br = new PacketBitReader(headers);
        pending.Clear();
        if (br.ReadBit() == 1)
        {
            foreach (var pb in precinct.Bands)
            {
                ReadBandContributions(pb, style, layer, br, pending);
            }
        }

        br.Align();
        if (st.Eph)
        {
            if (headers.Remaining < 2 || headers.Data[headers.Pos] != 0xFF || headers.Data[headers.Pos + 1] != 0x92)
            {
                throw Malformed("missing EPH marker.");
            }

            headers.Pos += 2;
        }

        // Packet body: the coded bytes of each contribution, in header order.
        foreach (var (block, seg, length) in pending)
        {
            if (length > body.Remaining)
            {
                throw Malformed("packet body extends beyond the tile data.");
            }

            if (length > 0)
            {
                if (block.DataLength + length > block.Data.Length)
                {
                    var grown = block.Data;
                    Array.Resize(ref grown, Math.Max(block.DataLength + length, grown.Length * 2));
                    block.Data = grown;
                }

                Array.Copy(body.Data, body.Pos, block.Data, block.DataLength, length);
                block.DataLength += length;
                body.Pos += length;
            }

            seg.Length += length;
        }
    }

    private static void ReadBandContributions(
        PrecinctBand pb, int style, int layer, PacketBitReader br, List<(CodeBlock Block, Segment Seg, int Length)> pending)
    {
        for (var idx = 0; idx < pb.Blocks.Length; idx++)
        {
            var cb = pb.Blocks[idx];
            var cx = idx % pb.Cw;
            var cy = idx / pb.Cw;

            // Inclusion: tag tree for blocks never included, a single bit afterwards.
            bool included;
            if (cb.Included)
            {
                included = br.ReadBit() == 1;
            }
            else
            {
                included = pb.Inclusion!.DecodeBelow(br, cx, cy, layer + 1);
            }

            if (!included)
            {
                continue;
            }

            if (!cb.Included)
            {
                // Number of missing most significant bit-planes: decode the leaf value fully.
                var i = 1;
                while (!pb.ZeroBits!.DecodeBelow(br, cx, cy, i))
                {
                    if (++i > 128)
                    {
                        throw Malformed("zero bit-plane count is out of range.");
                    }
                }

                cb.ZeroBitPlanes = i - 1;
                cb.Included = true;
                cb.Lblock = 3;
            }

            var passes = ReadPassCount(br);
            while (br.ReadBit() == 1)
            {
                if (++cb.Lblock > 32)
                {
                    throw Malformed("code-block length indicator is out of range.");
                }
            }

            // A block can never carry more coding passes than its bit-planes allow.
            var planes = pb.TopPlanes - cb.ZeroBitPlanes;
            if (planes > 31)
            {
                throw Unsupported("jpeg2000-bitplanes", "more than 31 coded bit-planes.");
            }

            if (planes <= 0 || cb.TotalPasses + passes > (3 * planes) - 2)
            {
                throw Malformed("code-block pass count exceeds its bit-planes.");
            }

            cb.TotalPasses += passes;
            SplitIntoSegments(cb, style, passes, br, pending);
        }
    }

    private static int ReadPassCount(PacketBitReader br)
    {
        if (br.ReadBit() == 0)
        {
            return 1;
        }

        if (br.ReadBit() == 0)
        {
            return 2;
        }

        var n = br.ReadBits(2);
        if (n != 3)
        {
            return 3 + n;
        }

        n = br.ReadBits(5);
        return n != 31 ? 6 + n : 37 + br.ReadBits(7);
    }

    private static Segment NewSegment(CodeBlock cb, int style)
    {
        // TERMALL: one pass per segment. LAZY (bypass): 10 passes, then alternating 2 (raw) and 1 (MQ).
        int max;
        if ((style & 0x04) != 0)
        {
            max = 1;
        }
        else if ((style & 0x01) != 0)
        {
            if (cb.Segments.Count == 0)
            {
                max = 10;
            }
            else
            {
                var prev = cb.Segments[^1].MaxPasses;
                max = prev == 1 || prev == 10 ? 2 : 1;
            }
        }
        else
        {
            max = 109;
        }

        var seg = new Segment { MaxPasses = max };
        cb.Segments.Add(seg);
        return seg;
    }

    private static void SplitIntoSegments(
        CodeBlock cb, int style, int passes, PacketBitReader br, List<(CodeBlock Block, Segment Seg, int Length)> pending)
    {
        var seg = cb.Segments.Count == 0 ? NewSegment(cb, style) : cb.Segments[^1];
        if (seg.Passes >= seg.MaxPasses)
        {
            seg = NewSegment(cb, style);
        }

        var remaining = passes;
        while (remaining > 0)
        {
            var take = Math.Min(seg.MaxPasses - seg.Passes, remaining);
            var bits = cb.Lblock + (31 - int.LeadingZeroCount(take));
            if (bits > 31)
            {
                throw Malformed("code-block length field is too wide.");
            }

            var length = br.ReadBits(bits);
            seg.Passes += take;
            pending.Add((cb, seg, length));
            remaining -= take;
            if (remaining > 0)
            {
                seg = NewSegment(cb, style);
            }
        }
    }
}
