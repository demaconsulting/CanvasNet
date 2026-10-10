namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Tile / component / resolution / sub-band / precinct / code-block geometry (ITU-T T.800 Annex B)
    // ================================================================================================

    /// <summary>One codeword segment of a code-block: a run of coding passes sharing one termination.</summary>
    internal sealed class Segment
    {
        /// <summary>Gets or sets the number of coding passes contributed so far.</summary>
        public int Passes { get; set; }

        /// <summary>Gets the maximum number of passes the segment can hold.</summary>
        public int MaxPasses { get; init; }

        /// <summary>Gets or sets the number of coded bytes contributed so far.</summary>
        public int Length { get; set; }
    }

    /// <summary>
    ///     A code-block: a rectangle of sub-band coefficients coded independently. Coordinates are
    ///     relative to the origin of the owning sub-band so they always fit in an <see cref="int"/>.
    /// </summary>
    internal sealed class CodeBlock
    {
        /// <summary>Gets the left edge in sub-band coordinates.</summary>
        public int X0 { get; init; }

        /// <summary>Gets the top edge in sub-band coordinates.</summary>
        public int Y0 { get; init; }

        /// <summary>Gets the exclusive right edge in sub-band coordinates.</summary>
        public int X1 { get; init; }

        /// <summary>Gets the exclusive bottom edge in sub-band coordinates.</summary>
        public int Y1 { get; init; }

        /// <summary>Gets or sets a value indicating whether the block was included in an earlier packet.</summary>
        public bool Included { get; set; }

        /// <summary>Gets or sets the number of missing most significant bit-planes.</summary>
        public int ZeroBitPlanes { get; set; }

        /// <summary>Gets or sets the Lblock length-indicator state.</summary>
        public int Lblock { get; set; } = 3;

        /// <summary>Gets or sets the total number of coding passes signalled so far.</summary>
        public int TotalPasses { get; set; }

        /// <summary>Gets or sets the accumulated coded bytes of all segments.</summary>
        public byte[] Data { get; set; } = [];

        /// <summary>Gets or sets the number of valid bytes in <see cref="Data"/>.</summary>
        public int DataLength { get; set; }

        /// <summary>Gets the codeword segments in order.</summary>
        public List<Segment> Segments { get; } = [];
    }

    /// <summary>One sub-band of a resolution level.</summary>
    internal sealed class Band
    {
        /// <summary>Gets the orientation: 0 LL, 1 HL, 2 LH, 3 HH.</summary>
        public int Orient { get; init; }

        /// <summary>Gets the left edge in sub-band coordinates.</summary>
        public long X0 { get; init; }

        /// <summary>Gets the top edge in sub-band coordinates.</summary>
        public long Y0 { get; init; }

        /// <summary>Gets the exclusive right edge in sub-band coordinates.</summary>
        public long X1 { get; init; }

        /// <summary>Gets the exclusive bottom edge in sub-band coordinates.</summary>
        public long Y1 { get; init; }

        /// <summary>Gets the number of magnitude bit-planes Mb (guard bits plus exponent minus one).</summary>
        public int Mb { get; init; }

        /// <summary>Gets the quantization step size (1 for reversible coding).</summary>
        public float Step { get; init; }

        /// <summary>Gets the horizontal offset of the band in the in-place (Mallat) tile-component layout.</summary>
        public int OffX { get; init; }

        /// <summary>Gets the vertical offset of the band in the in-place (Mallat) tile-component layout.</summary>
        public int OffY { get; init; }
    }

    /// <summary>The part of a precinct that lies in one sub-band.</summary>
    internal sealed class PrecinctBand
    {
        /// <summary>Gets the sub-band the precinct part belongs to.</summary>
        public required Band Band { get; init; }

        /// <summary>Gets the number of code-block columns.</summary>
        public int Cw { get; init; }

        /// <summary>Gets the number of code-block rows.</summary>
        public int Ch { get; init; }

        /// <summary>Gets the code-blocks in raster order.</summary>
        public required CodeBlock[] Blocks { get; init; }

        /// <summary>Gets the inclusion tag tree (null when there are no code-blocks).</summary>
        public TagTree? Inclusion { get; init; }

        /// <summary>Gets the zero-bit-plane tag tree (null when there are no code-blocks).</summary>
        public TagTree? ZeroBits { get; init; }

        /// <summary>Gets the total number of bit-planes (Mb plus ROI shift) a block can use.</summary>
        public int TopPlanes { get; init; }
    }

    /// <summary>A precinct: the unit of packet formation.</summary>
    internal sealed class Precinct
    {
        /// <summary>Gets the per-sub-band parts of the precinct.</summary>
        public required PrecinctBand[] Bands { get; init; }
    }

    /// <summary>One resolution level of a tile-component.</summary>
    internal sealed class Resolution
    {
        /// <summary>Gets the left edge in resolution coordinates.</summary>
        public long X0 { get; init; }

        /// <summary>Gets the top edge in resolution coordinates.</summary>
        public long Y0 { get; init; }

        /// <summary>Gets the exclusive right edge in resolution coordinates.</summary>
        public long X1 { get; init; }

        /// <summary>Gets the exclusive bottom edge in resolution coordinates.</summary>
        public long Y1 { get; init; }

        /// <summary>Gets the precinct width exponent.</summary>
        public int PPx { get; init; }

        /// <summary>Gets the precinct height exponent.</summary>
        public int PPy { get; init; }

        /// <summary>Gets the number of precinct columns.</summary>
        public int Pw { get; init; }

        /// <summary>Gets the number of precinct rows.</summary>
        public int Ph { get; init; }

        /// <summary>Gets the sub-bands (one for resolution 0, otherwise HL, LH, HH).</summary>
        public required Band[] Bands { get; init; }

        /// <summary>Gets the precincts in raster order.</summary>
        public required Precinct[] Precincts { get; init; }

        /// <summary>Gets the width in samples.</summary>
        public int Width => (int)(X1 - X0);

        /// <summary>Gets the height in samples.</summary>
        public int Height => (int)(Y1 - Y0);
    }

    /// <summary>One component of one tile.</summary>
    internal sealed class TileComp
    {
        /// <summary>Gets the left edge in component samples.</summary>
        public long X0 { get; init; }

        /// <summary>Gets the top edge in component samples.</summary>
        public long Y0 { get; init; }

        /// <summary>Gets the exclusive right edge in component samples.</summary>
        public long X1 { get; init; }

        /// <summary>Gets the exclusive bottom edge in component samples.</summary>
        public long Y1 { get; init; }

        /// <summary>Gets the coding parameters.</summary>
        public required CodingParams Coding { get; init; }

        /// <summary>Gets the ROI max-shift value.</summary>
        public int RoiShift { get; init; }

        /// <summary>Gets the resolution levels, lowest first.</summary>
        public required Resolution[] Res { get; init; }

        /// <summary>Gets the width in samples.</summary>
        public int Width => (int)(X1 - X0);

        /// <summary>Gets the height in samples.</summary>
        public int Height => (int)(Y1 - Y0);
    }

    /// <summary>The fully built geometry of one tile.</summary>
    internal sealed class Tile
    {
        /// <summary>Gets the left edge on the reference grid.</summary>
        public long X0 { get; init; }

        /// <summary>Gets the top edge on the reference grid.</summary>
        public long Y0 { get; init; }

        /// <summary>Gets the exclusive right edge on the reference grid.</summary>
        public long X1 { get; init; }

        /// <summary>Gets the exclusive bottom edge on the reference grid.</summary>
        public long Y1 { get; init; }

        /// <summary>Gets the tile components.</summary>
        public required TileComp[] Comps { get; init; }

        /// <summary>Gets the total number of packets (precincts times layers).</summary>
        public long TotalPackets { get; init; }
    }

    /// <summary>Builds the tile geometry for tile <paramref name="index"/> with strict resource caps.</summary>
    /// <param name="siz">The image size information.</param>
    /// <param name="st">The coding state in effect for the tile.</param>
    /// <param name="index">The tile index in raster order.</param>
    /// <param name="limits">The resource limits.</param>
    /// <param name="packetBits">
    ///     The number of bits the tile-part data of the tile can hold. Every packet has a header of at
    ///     least one bit, so a tile cannot have more packets than this.
    /// </param>
    /// <returns>The built tile.</returns>
    private static Tile BuildTile(SizInfo siz, CodingState st, int index, Jpeg2000DecoderLimits limits, long packetBits)
    {
        var nx = siz.NumXTiles;
        var p = index % nx;
        var q = index / nx;
        var tx0 = Math.Max(siz.XTOsiz + (p * siz.XTsiz), siz.XOsiz);
        var tx1 = Math.Min(siz.XTOsiz + ((p + 1L) * siz.XTsiz), siz.Xsiz);
        var ty0 = Math.Max(siz.YTOsiz + (q * siz.YTsiz), siz.YOsiz);
        var ty1 = Math.Min(siz.YTOsiz + ((q + 1L) * siz.YTsiz), siz.Ysiz);

        // Every limit is checked before the structure it bounds is allocated. The packet bound is the tighter of the
        // packet limit and what the tile data could possibly carry.
        var budget = new GeometryBudget
        {
            MaxSamples = limits.MaxTileSamples,
            MaxBlocks = limits.MaxTileCodeBlocks,
            MaxPrecincts = limits.MaxTilePrecincts,
            MaxPackets = limits.MaxTilePackets,
            PacketBits = packetBits,
            Layers = st.Layers,
        };
        var comps = new TileComp[siz.Csiz];
        for (var c = 0; c < siz.Csiz; c++)
        {
            comps[c] = BuildTileComp(siz, st, c, new TileRegion(tx0, ty0, tx1, ty1), budget);
        }

        return new Tile { X0 = tx0, Y0 = ty0, X1 = tx1, Y1 = ty1, Comps = comps, TotalPackets = budget.Precincts * st.Layers };
    }

    /// <summary>Running resource counters used while building one tile.</summary>
    private sealed class GeometryBudget
    {
        /// <summary>Gets the maximum number of samples of the tile.</summary>
        public long MaxSamples { get; init; }

        /// <summary>Gets the maximum number of precincts of the tile.</summary>
        public long MaxPrecincts { get; init; }

        /// <summary>Gets the maximum number of packets (precincts times layers) of the tile.</summary>
        public long MaxPackets { get; init; }

        /// <summary>Gets the number of bits the tile data can hold; a packet needs at least one header bit.</summary>
        public long PacketBits { get; init; }

        /// <summary>Gets the number of layers.</summary>
        public int Layers { get; init; }

        /// <summary>Gets the maximum number of code-blocks of the tile.</summary>
        public long MaxBlocks { get; init; }

        /// <summary>Gets or sets the number of samples allocated so far.</summary>
        public long Samples { get; set; }

        /// <summary>Gets or sets the number of precincts created so far.</summary>
        public long Precincts { get; set; }

        /// <summary>Gets or sets the number of code-blocks created so far.</summary>
        public long Blocks { get; set; }

        /// <summary>Adds precincts to the running count and enforces the precinct and packet bounds before they are allocated.</summary>
        /// <param name="count">The number of precincts about to be created.</param>
        public void ChargePrecincts(long count)
        {
            Precincts += count;
            if (Precincts > MaxPrecincts)
            {
                throw Malformed("tile has more precincts than the decoder limit.");
            }

            // Precincts are within the int range here and Layers is at most 65535, so the product cannot overflow.
            var packets = Precincts * Layers;
            if (packets > MaxPackets)
            {
                throw Malformed("tile has more packets than the decoder limit.");
            }

            if (packets > PacketBits)
            {
                throw Malformed("tile has more packets than its data can carry.");
            }
        }
    }

    /// <summary>A rectangle in reference-grid or tile-component coordinates (exclusive upper bounds).</summary>
    private readonly record struct TileRegion(long X0, long Y0, long X1, long Y1);

    private static TileComp BuildTileComp(SizInfo siz, CodingState st, int c, TileRegion tile, GeometryBudget budget)
    {
        var coding = st.Coding[c]!;
        var quant = st.Quant[c]!;
        var levels = coding.Levels;

        // T.800 A.6.4: no-quantization (style 0) carries exactly one entry per sub-band, 3 * levels + 1. The decomposition
        // levels of the effective COD/COC are only known here, after any marker ordering (QCD before COD) has settled.
        if (quant.Style == 0 && quant.Exp.Length != (3 * levels) + 1)
        {
            throw Malformed($"quantization segment has {quant.Exp.Length} entries but {(3 * levels) + 1} sub-bands.");
        }

        var tcx0 = CeilDiv(tile.X0, siz.XR[c]);
        var tcy0 = CeilDiv(tile.Y0, siz.YR[c]);
        var tcx1 = CeilDiv(tile.X1, siz.XR[c]);
        var tcy1 = CeilDiv(tile.Y1, siz.YR[c]);

        // Memory cap on the decoded sample planes of the tile.
        budget.Samples += (tcx1 - tcx0) * (tcy1 - tcy0);
        if (budget.Samples > budget.MaxSamples)
        {
            throw Malformed("tile exceeds the decoder sample limit.");
        }

        var res = new Resolution[levels + 1];
        for (var r = 0; r <= levels; r++)
        {
            res[r] = BuildResolution(siz, st, c, r, new TileRegion(tcx0, tcy0, tcx1, tcy1), r > 0 ? res[r - 1] : null, budget);
        }

        return new TileComp { X0 = tcx0, Y0 = tcy0, X1 = tcx1, Y1 = tcy1, Coding = coding, RoiShift = st.RoiShift[c], Res = res };
    }

    private static Resolution BuildResolution(SizInfo siz, CodingState st, int c, int r, TileRegion tc, Resolution? lower, GeometryBudget budget)
    {
        var coding = st.Coding[c]!;
        var levels = coding.Levels;
        var lvl = levels - r;
        var rx0 = CeilDivPow2(tc.X0, lvl);
        var ry0 = CeilDivPow2(tc.Y0, lvl);
        var rx1 = CeilDivPow2(tc.X1, lvl);
        var ry1 = CeilDivPow2(tc.Y1, lvl);

        // Sub-bands (Annex B.5): LL for resolution 0, else HL, LH and HH at decomposition level nb.
        var bands = new Band[r == 0 ? 1 : 3];
        for (var b = 0; b < bands.Length; b++)
        {
            var orient = r == 0 ? 0 : b + 1;
            bands[b] = BuildBand(siz, st, c, r, orient, tc, lower);
        }

        // Precinct grid (anchored at the origin of the resolution coordinate system).
        var ppx = coding.PPx[r];
        var ppy = coding.PPy[r];
        var pwLong = rx1 > rx0 ? CeilDivPow2(rx1, ppx) - (rx0 >> ppx) : 0;
        var phLong = ry1 > ry0 ? CeilDivPow2(ry1, ppy) - (ry0 >> ppy) : 0;
        var count = pwLong * phLong;
        budget.ChargePrecincts(count);

        var pw = (int)pwLong;
        var ph = (int)phLong;

        var precincts = new Precinct[count];
        for (var j = 0; j < ph; j++)
        {
            for (var i = 0; i < pw; i++)
            {
                var pxAbs = (rx0 >> ppx) + i;
                var pyAbs = (ry0 >> ppy) + j;
                precincts[(j * pw) + i] = BuildPrecinct(coding, st.RoiShift[c], r, bands, pxAbs, pyAbs, budget);
            }
        }

        return new Resolution
        {
            X0 = rx0,
            Y0 = ry0,
            X1 = rx1,
            Y1 = ry1,
            PPx = ppx,
            PPy = ppy,
            Pw = pw,
            Ph = ph,
            Bands = bands,
            Precincts = precincts,
        };
    }

    private static Band BuildBand(SizInfo siz, CodingState st, int c, int r, int orient, TileRegion tc, Resolution? lower)
    {
        var coding = st.Coding[c]!;
        var quant = st.Quant[c]!;
        var levels = coding.Levels;
        var (x0, y0, x1, y1) = BandBounds(levels, r, orient, tc);
        var offX = 0;
        var offY = 0;
        if (r > 0)
        {
            offX = orient is 1 or 3 ? lower!.Width : 0;
            offY = orient is 2 or 3 ? lower!.Height : 0;
        }

        var (exp, mant) = BandQuantization(quant, r, orient);

        // The bit-plane count is validated once, here, so tier-2 and tier-1 can rely on it: the magnitude
        // bit-planes Mb must be positive and plausible, and Mb plus the ROI shift must fit the decoder.
        var mb = quant.GuardBits + exp - 1;
        if (exp < 0 || mb < 0 || mb > MaxBitPlanes)
        {
            throw Malformed("invalid quantization exponent.");
        }

        if (mb + st.RoiShift[c] > MaxBitPlanes)
        {
            throw Unsupported("jpeg2000-bitplanes", $"more than {MaxBitPlanes} coded bit-planes.");
        }

        var gain = orient switch { 0 => 0, 3 => 2, _ => 1 };
        var step = coding.Reversible ? 1f : (float)(Math.Pow(2, siz.Depth[c] + gain - exp) * (1.0 + (mant / 2048.0)));
        return new Band { Orient = orient, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Mb = mb, Step = step, OffX = offX, OffY = offY };
    }

    /// <summary>Computes the sub-band bounds from the tile-component bounds (Annex B.5).</summary>
    private static (long X0, long Y0, long X1, long Y1) BandBounds(int levels, int r, int orient, TileRegion tc)
    {
        if (r == 0)
        {
            return (CeilDivPow2(tc.X0, levels), CeilDivPow2(tc.Y0, levels), CeilDivPow2(tc.X1, levels), CeilDivPow2(tc.Y1, levels));
        }

        var nb = levels - r + 1;
        var xob = orient is 1 or 3 ? 1L : 0L;
        var yob = orient is 2 or 3 ? 1L : 0L;
        var shift = nb - 1;
        return (
            CeilDivPow2(tc.X0 - (xob << shift), nb),
            CeilDivPow2(tc.Y0 - (yob << shift), nb),
            CeilDivPow2(tc.X1 - (xob << shift), nb),
            CeilDivPow2(tc.Y1 - (yob << shift), nb));
    }

    /// <summary>Selects the quantization exponent and mantissa of a band (Annex E): derived style extrapolates from the LL entry.</summary>
    private static (int Exp, int Mant) BandQuantization(QuantParams quant, int r, int orient)
    {
        if (quant.Style == 1)
        {
            return (quant.Exp[0] - (r == 0 ? 0 : r - 1), quant.Mant[0]);
        }

        var bandIndex = r == 0 ? 0 : (3 * (r - 1)) + orient;
        if (bandIndex >= quant.Exp.Length)
        {
            throw Malformed("quantization segment has too few entries.");
        }

        return (quant.Exp[bandIndex], quant.Mant[bandIndex]);
    }
    private static Precinct BuildPrecinct(
        CodingParams coding, int roiShift, int r, Band[] bands, long pxAbs, long pyAbs, GeometryBudget budget)
    {
        var ppx = coding.PPx[r];
        var ppy = coding.PPy[r];
        var parts = new PrecinctBand[bands.Length];
        for (var b = 0; b < bands.Length; b++)
        {
            var band = bands[b];

            // Precinct region in sub-band coordinates: halved for resolutions above 0 (Annex B.6).
            var sx = r == 0 ? ppx : ppx - 1;
            var sy = r == 0 ? ppy : ppy - 1;
            var x0 = Math.Max(pxAbs << sx, band.X0);
            var x1 = Math.Min((pxAbs + 1) << sx, band.X1);
            var y0 = Math.Max(pyAbs << sy, band.Y0);
            var y1 = Math.Min((pyAbs + 1) << sy, band.Y1);
            var top = band.Mb + roiShift;
            if (x1 <= x0 || y1 <= y0)
            {
                parts[b] = new PrecinctBand { Band = band, Blocks = [], TopPlanes = top };
                continue;
            }

            // Code-block grid is anchored at the origin; its size is clamped to the precinct size.
            var xcb = Math.Min(coding.Xcb, sx);
            var ycb = Math.Min(coding.Ycb, sy);
            var cbx0 = x0 >> xcb;
            var cby0 = y0 >> ycb;
            var cwLong = CeilDivPow2(x1, xcb) - cbx0;
            var chLong = CeilDivPow2(y1, ycb) - cby0;
            budget.Blocks += cwLong * chLong;
            if (budget.Blocks > budget.MaxBlocks)
            {
                throw Malformed("tile has too many code-blocks.");
            }

            var cw = (int)cwLong;
            var ch = (int)chLong;
            var blocks = new CodeBlock[cw * ch];
            for (var j = 0; j < ch; j++)
            {
                for (var i = 0; i < cw; i++)
                {
                    blocks[(j * cw) + i] = new CodeBlock
                    {
                        X0 = (int)(Math.Max(x0, (cbx0 + i) << xcb) - band.X0),
                        Y0 = (int)(Math.Max(y0, (cby0 + j) << ycb) - band.Y0),
                        X1 = (int)(Math.Min(x1, (cbx0 + i + 1) << xcb) - band.X0),
                        Y1 = (int)(Math.Min(y1, (cby0 + j + 1) << ycb) - band.Y0),
                    };
                }
            }

            parts[b] = new PrecinctBand
            {
                Band = band,
                Cw = cw,
                Ch = ch,
                Blocks = blocks,
                TopPlanes = top,
                Inclusion = new TagTree(cw, ch),
                ZeroBits = new TagTree(cw, ch),
            };
        }

        return new Precinct { Bands = parts };
    }
}
