#pragma warning disable S3218, S3358, S1172, S107, S3776, S1541, S134, S1244
// cspell:ignore bypass ebcot lblock precinct precincts subband subbands tlm plt ppm ppt crg sop eph rgn poc cprl rpcl pcrl rlcp lrcp
// cspell:ignore pclr cmap cdef bpcc colr ihdr ftyp jp2c jp2h tilepart xcb ycb
using System.Text;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>One image component handed to <see cref="Jpeg2000TestEncoder"/>.</summary>
internal sealed class J2kComponent
{
    /// <summary>Gets or sets the bit depth (1 to 16).</summary>
    public int Depth { get; set; } = 8;

    /// <summary>Gets or sets a value indicating whether samples are signed.</summary>
    public bool Signed { get; set; }

    /// <summary>Gets or sets the horizontal subsampling factor.</summary>
    public int Dx { get; set; } = 1;

    /// <summary>Gets or sets the vertical subsampling factor.</summary>
    public int Dy { get; set; } = 1;

    /// <summary>Gets or sets the samples of the component grid, row-major, in natural (signed or unsigned) values.</summary>
    public int[] Samples { get; set; } = [];
}

/// <summary>One progression order change entry.</summary>
/// <param name="ResStart">The first resolution.</param>
/// <param name="CompStart">The first component.</param>
/// <param name="LayerEnd">The exclusive layer end.</param>
/// <param name="ResEnd">The exclusive resolution end.</param>
/// <param name="CompEnd">The exclusive component end.</param>
/// <param name="Order">The progression order (0 LRCP, 1 RLCP, 2 RPCL, 3 PCRL, 4 CPRL).</param>
internal readonly record struct J2kPoc(int ResStart, int CompStart, int LayerEnd, int ResEnd, int CompEnd, int Order);

/// <summary>An image on the reference grid.</summary>
internal sealed class J2kImage
{
    /// <summary>Gets or sets the right edge of the reference grid.</summary>
    public int Xsiz { get; set; }

    /// <summary>Gets or sets the bottom edge of the reference grid.</summary>
    public int Ysiz { get; set; }

    /// <summary>Gets or sets the horizontal image offset.</summary>
    public int XOsiz { get; set; }

    /// <summary>Gets or sets the vertical image offset.</summary>
    public int YOsiz { get; set; }

    /// <summary>Gets or sets the components.</summary>
    public J2kComponent[] Components { get; set; } = [];

    /// <summary>Gets the image width.</summary>
    public int Width => Xsiz - XOsiz;

    /// <summary>Gets the image height.</summary>
    public int Height => Ysiz - YOsiz;

    /// <summary>Gets the left edge of component <paramref name="c"/> in component samples.</summary>
    public int CompX0(int c) => (XOsiz + Components[c].Dx - 1) / Components[c].Dx;

    /// <summary>Gets the top edge of component <paramref name="c"/> in component samples.</summary>
    public int CompY0(int c) => (YOsiz + Components[c].Dy - 1) / Components[c].Dy;

    /// <summary>Gets the width of component <paramref name="c"/> in component samples.</summary>
    public int CompWidth(int c) => ((Xsiz + Components[c].Dx - 1) / Components[c].Dx) - CompX0(c);

    /// <summary>Gets the height of component <paramref name="c"/> in component samples.</summary>
    public int CompHeight(int c) => ((Ysiz + Components[c].Dy - 1) / Components[c].Dy) - CompY0(c);
}

/// <summary>Where packet headers are stored.</summary>
internal enum J2kHeaderMode
{
    /// <summary>In-line with the packet data.</summary>
    InBand,

    /// <summary>Packed in PPM marker segments of the main header.</summary>
    Ppm,

    /// <summary>Packed in PPT marker segments of the tile-part headers.</summary>
    Ppt,
}

/// <summary>Coding options of <see cref="Jpeg2000TestEncoder"/>.</summary>
internal sealed class J2kOptions
{
    /// <summary>Gets or sets the tile width (0 = one tile).</summary>
    public int TileWidth { get; set; }

    /// <summary>Gets or sets the tile height (0 = one tile).</summary>
    public int TileHeight { get; set; }

    /// <summary>Gets or sets the tile grid horizontal offset.</summary>
    public int TileXOffset { get; set; }

    /// <summary>Gets or sets the tile grid vertical offset.</summary>
    public int TileYOffset { get; set; }

    /// <summary>Gets or sets the number of decomposition levels.</summary>
    public int Levels { get; set; } = 3;

    /// <summary>Gets or sets per-component decomposition levels (emitted as COC/QCC), or <see langword="null"/>.</summary>
    public int[]? ComponentLevels { get; set; }

    /// <summary>Gets or sets a value indicating whether the reversible 5/3 transform is used.</summary>
    public bool Reversible { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the component transform (RCT/ICT) is used.</summary>
    public bool Mct { get; set; }

    /// <summary>Gets or sets the code-block width exponent.</summary>
    public int CodeBlockWidthExp { get; set; } = 5;

    /// <summary>Gets or sets the code-block height exponent.</summary>
    public int CodeBlockHeightExp { get; set; } = 5;

    /// <summary>Gets or sets the code-block style flags (0x01 bypass, 0x02 reset, 0x04 termall, 0x08 vcausal, 0x10 pterm, 0x20 segsym).</summary>
    public int CodeBlockStyle { get; set; }

    /// <summary>Gets or sets the progression order (0 LRCP, 1 RLCP, 2 RPCL, 3 PCRL, 4 CPRL).</summary>
    public int Progression { get; set; }

    /// <summary>Gets or sets the number of quality layers.</summary>
    public int Layers { get; set; } = 1;

    /// <summary>Gets or sets explicit precinct exponents (PPx, PPy) per resolution (lowest first; the last entry repeats), or <see langword="null"/>.</summary>
    public (int Ppx, int Ppy)[]? Precincts { get; set; }

    /// <summary>Gets or sets a value indicating whether SOP marker segments are written.</summary>
    public bool Sop { get; set; }

    /// <summary>Gets or sets a value indicating whether EPH markers are written.</summary>
    public bool Eph { get; set; }

    /// <summary>Gets or sets the maximum-shift region-of-interest value (0 = none).</summary>
    public int RoiShift { get; set; }

    /// <summary>Gets or sets the components that carry an ROI (null = all).</summary>
    public int[]? RoiComponents { get; set; }

    /// <summary>Gets or sets the number of guard bits.</summary>
    public int GuardBits { get; set; } = 2;

    /// <summary>Gets or sets the quantization style for irreversible coding: 1 derived, 2 expounded.</summary>
    public int QuantStyle { get; set; } = 2;

    /// <summary>Gets or sets a value indicating whether non-zero step size mantissas are used.</summary>
    public bool QuantMantissa { get; set; }

    /// <summary>Gets or sets progression order changes (written as a POC marker), or <see langword="null"/>.</summary>
    public List<J2kPoc>? Poc { get; set; }

    /// <summary>Gets or sets a value indicating whether the POC marker is placed in the tile-part headers instead of the main header.</summary>
    public bool PocInTileHeader { get; set; }

    /// <summary>Gets or sets a value indicating whether the tile-part POC entries are spread over the tile-part headers (first entry in the first, the rest in the second) instead of all being in the first.</summary>
    public bool PocSplitAcrossParts { get; set; }

    /// <summary>Gets or sets complete marker segments (marker included) appended to the header of every tile-part after the first.</summary>
    public byte[]? LaterPartSegments { get; set; }

    /// <summary>Gets or sets where packet headers are stored.</summary>
    public J2kHeaderMode HeaderMode { get; set; }

    /// <summary>Gets or sets the maximum payload of a PPM/PPT marker segment (0 = unlimited).</summary>
    public int PackedChunkSize { get; set; }

    /// <summary>Gets or sets the number of tile-parts per tile.</summary>
    public int TileParts { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether COM, CRG, TLM, PLT and reserved markers are written.</summary>
    public bool ExtraMarkers { get; set; }

    /// <summary>Gets or sets per-tile coding overrides written in the tile-part headers.</summary>
    public Dictionary<int, J2kOptions> TileOverrides { get; private set; } = [];

    /// <summary>Gets or sets a value indicating whether the last tile-part uses Psot = 0 and the EOC marker is omitted.</summary>
    public bool OmitEoc { get; set; }

    /// <summary>Gets or sets a value indicating whether Psot is written as zero for the final tile-part.</summary>
    public bool ZeroPsotOnLast { get; set; }

    /// <summary>Creates a shallow copy of the options (without tile overrides).</summary>
    /// <returns>The copy.</returns>
    public J2kOptions Clone()
    {
        var copy = (J2kOptions)MemberwiseClone();
        copy.TileOverrides = [];
        return copy;
    }
}

/// <summary>
///     A minimal, test-only JPEG 2000 Part 1 encoder. It exists so that the production decoder can be
///     exercised on every coding feature (5/3 and 9/7, RCT/ICT, tiles, precincts, all progression orders,
///     layers, every code-block style, ROI, packed headers, subsampling, JP2 boxes) without third-party files.
/// </summary>
internal static partial class Jpeg2000TestEncoder
{
    // ------------------------------------------------------------------------------------------
    // Geometry model
    // ------------------------------------------------------------------------------------------

    private sealed class EBand
    {
        public int Orient { get; init; }

        public long X0 { get; init; }

        public long Y0 { get; init; }

        public long X1 { get; init; }

        public long Y1 { get; init; }

        public int Eps { get; init; }

        public int Mant { get; init; }

        public int Mb { get; init; }

        public double Step { get; init; }

        public int OffX { get; init; }

        public int OffY { get; init; }
    }

    private sealed class EPrecBand
    {
        public required EBand Band { get; init; }

        public int Cw { get; init; }

        public int Ch { get; init; }

        public EncodedBlock[] Blocks { get; init; } = [];

        public int[][] Cum { get; set; } = [];

        public bool[] Included { get; set; } = [];

        public int[] Lblock { get; set; } = [];

        public TagTreeEncoder? Inclusion { get; set; }

        public TagTreeEncoder? ZeroBits { get; set; }
    }

    private sealed class EPrecinct
    {
        public required EPrecBand[] Bands { get; init; }
    }

    private sealed class ERes
    {
        public long X0 { get; init; }

        public long Y0 { get; init; }

        public long X1 { get; init; }

        public long Y1 { get; init; }

        public int PPx { get; init; }

        public int PPy { get; init; }

        public int Pw { get; init; }

        public int Ph { get; init; }

        public required EPrecinct[] Precincts { get; init; }
    }

    private sealed class ETileComp
    {
        public required ERes[] Res { get; init; }

        public int Levels { get; init; }
    }

    private sealed class Packet
    {
        public byte[] Sop { get; init; } = [];

        public byte[] Header { get; init; } = [];

        public byte[] Data { get; init; } = [];
    }

    // ------------------------------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------------------------------

    /// <summary>Encodes an image to a raw JPEG 2000 codestream.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">The coding options.</param>
    /// <returns>The codestream bytes.</returns>
    public static byte[] Encode(J2kImage image, J2kOptions options)
    {
        var tw = options.TileWidth == 0 ? image.Xsiz - options.TileXOffset : options.TileWidth;
        var th = options.TileHeight == 0 ? image.Ysiz - options.TileYOffset : options.TileHeight;
        var tilesX = (image.Xsiz - options.TileXOffset + tw - 1) / tw;
        var tilesY = (image.Ysiz - options.TileYOffset + th - 1) / th;

        var tileParts = new List<(int Tile, byte[] Bytes)>();
        var ppmChunks = new List<byte[]>();
        for (var t = 0; t < tilesX * tilesY; t++)
        {
            var p = options.TileOverrides.TryGetValue(t, out var o) ? o : options;
            var tx0 = Math.Max(options.TileXOffset + ((t % tilesX) * tw), image.XOsiz);
            var tx1 = Math.Min(options.TileXOffset + (((t % tilesX) + 1) * tw), image.Xsiz);
            var ty0 = Math.Max(options.TileYOffset + ((t / tilesX) * th), image.YOsiz);
            var ty1 = Math.Min(options.TileYOffset + (((t / tilesX) + 1) * th), image.Ysiz);
            var packets = BuildTilePackets(image, p, tx0, ty0, tx1, ty1);
            AppendTileParts(t, options, p, !ReferenceEquals(p, options), packets, tileParts, ppmChunks, image);
        }

        var output = new List<byte>();
        WriteMarker(output, 0xFF4F);
        WriteSiz(output, image, options, tw, th);
        WriteCodingMarkers(output, image, options, tileParts.Count);
        if (options.HeaderMode == J2kHeaderMode.Ppm)
        {
            WritePpm(output, ppmChunks, options.PackedChunkSize);
        }

        if (options.ExtraMarkers)
        {
            WriteExtraMainMarkers(output, image, tileParts);
        }

        for (var i = 0; i < tileParts.Count; i++)
        {
            var bytes = tileParts[i].Bytes;
            if (options.ZeroPsotOnLast && i == tileParts.Count - 1)
            {
                bytes = (byte[])bytes.Clone();
                bytes[6] = bytes[7] = bytes[8] = bytes[9] = 0;
            }

            output.AddRange(bytes);
        }

        if (!options.OmitEoc)
        {
            WriteMarker(output, 0xFFD9);
        }

        return [.. output];
    }

    /// <summary>Builds a deterministic test image.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="components">The component count.</param>
    /// <param name="depth">The bit depth.</param>
    /// <param name="seed">The pseudo-random seed.</param>
    /// <param name="noise">The noise amplitude (0 = smooth).</param>
    /// <param name="signed">Whether the samples are signed.</param>
    /// <returns>The image.</returns>
    public static J2kImage MakeImage(int width, int height, int components, int depth = 8, int seed = 1, int noise = 24, bool signed = false)
    {
        var image = new J2kImage { Xsiz = width, Ysiz = height, Components = new J2kComponent[components] };
        var rng = new Random(seed);
        var max = (1 << depth) - 1;
        for (var c = 0; c < components; c++)
        {
            var comp = new J2kComponent { Depth = depth, Signed = signed, Samples = new int[width * height] };
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var smooth = ((x * 3) + (y * 2) + (c * 40)) % (max + 1);
                    if (((x / 7) + (y / 5)) % 4 == 0)
                    {
                        smooth = max - smooth;
                    }

                    var v = Math.Clamp(smooth + (noise == 0 ? 0 : rng.Next(-noise, noise + 1)), 0, max);
                    comp.Samples[(y * width) + x] = signed ? v - (1 << (depth - 1)) : v;
                }
            }

            image.Components[c] = comp;
        }

        return image;
    }

    /// <summary>Computes the 8-bit value the decoder is expected to produce for a component sample.</summary>
    /// <param name="sample">The natural sample value.</param>
    /// <param name="depth">The bit depth.</param>
    /// <param name="signed">Whether the component is signed.</param>
    /// <returns>The expected byte.</returns>
    public static byte ExpectedByte(int sample, int depth, bool signed)
    {
        var unsigned = signed ? sample + (1 << (depth - 1)) : sample;
        if (depth == 8)
        {
            return (byte)unsigned;
        }

        long max = (1L << depth) - 1;
        return (byte)Math.Min(255L, ((unsigned * 255L) + (max / 2)) / max);
    }

    // ------------------------------------------------------------------------------------------
    // Tile coding
    // ------------------------------------------------------------------------------------------

    private static int LevelsOf(J2kOptions o, int c) => o.ComponentLevels is { } l ? l[c] : o.Levels;

    private static (int Ppx, int Ppy) PrecinctOf(J2kOptions o, int r)
    {
        if (o.Precincts is not { Length: > 0 } p)
        {
            return (15, 15);
        }

        return p[Math.Min(r, p.Length - 1)];
    }

    private static long CeilDiv(long a, long b) => (a + b - 1) / b;

    private static long CeilPow2(long a, int s) => (a + (1L << s) - 1) >> s;

    private static List<Packet> BuildTilePackets(J2kImage image, J2kOptions o, int tx0, int ty0, int tx1, int ty1)
    {
        var n = image.Components.Length;
        var planes = new int[n][];
        var dplanes = new double[n][];
        var dims = new (int X0, int Y0, int W, int H)[n];
        for (var c = 0; c < n; c++)
        {
            var comp = image.Components[c];
            var x0 = (int)CeilDiv(tx0, comp.Dx);
            var y0 = (int)CeilDiv(ty0, comp.Dy);
            var x1 = (int)CeilDiv(tx1, comp.Dx);
            var y1 = (int)CeilDiv(ty1, comp.Dy);
            dims[c] = (x0, y0, x1 - x0, y1 - y0);
            planes[c] = new int[Math.Max(0, (x1 - x0) * (y1 - y0))];
            var shift = comp.Signed ? 0 : 1 << (comp.Depth - 1);
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    planes[c][((y - y0) * (x1 - x0)) + x - x0] = comp.Samples[((y - image.CompY0(c)) * image.CompWidth(c)) + x - image.CompX0(c)] - shift;
                }
            }
        }

        if (o.Mct)
        {
            ForwardMct(o, planes, dplanes);
        }

        if (!o.Reversible)
        {
            for (var c = 0; c < n; c++)
            {
                dplanes[c] ??= planes[c].Select(v => (double)v).ToArray();
            }
        }

        var comps = new ETileComp[n];
        var blockCounter = 0;
        for (var c = 0; c < n; c++)
        {
            comps[c] = BuildTileComp(image, o, c, dims[c], planes[c], dplanes[c], ref blockCounter);
        }

        return BuildPackets(image, o, comps, tx0, ty0, tx1, ty1);
    }

    private static void ForwardMct(J2kOptions o, int[][] planes, double[][] dplanes)
    {
        var len = planes[0].Length;
        if (o.Reversible)
        {
            for (var i = 0; i < len; i++)
            {
                int r = planes[0][i], g = planes[1][i], b = planes[2][i];
                planes[0][i] = (r + (2 * g) + b) >> 2;
                planes[1][i] = b - g;
                planes[2][i] = r - g;
            }

            return;
        }

        var y = new double[len];
        var cb = new double[len];
        var cr = new double[len];
        for (var i = 0; i < len; i++)
        {
            double r = planes[0][i], g = planes[1][i], b = planes[2][i];
            y[i] = (0.299 * r) + (0.587 * g) + (0.114 * b);
            cb[i] = (-0.16875 * r) - (0.33126 * g) + (0.5 * b);
            cr[i] = (0.5 * r) - (0.41869 * g) - (0.08131 * b);
        }

        dplanes[0] = y;
        dplanes[1] = cb;
        dplanes[2] = cr;
    }

    private static ETileComp BuildTileComp(J2kImage image, J2kOptions o, int c, (int X0, int Y0, int W, int H) d, int[] iplane, double[]? fplane, ref int blockCounter)
    {
        var levels = LevelsOf(o, c);
        var rects = new (long X0, long Y0, long X1, long Y1)[levels + 1];
        for (var r = 0; r <= levels; r++)
        {
            var s = levels - r;
            rects[r] = (CeilPow2(d.X0, s), CeilPow2(d.Y0, s), CeilPow2(d.X0 + d.W, s), CeilPow2(d.Y0 + d.H, s));
        }

        if (o.Reversible)
        {
            ForwardDwt(iplane, d.W, rects, Forward53);
        }
        else
        {
            ForwardDwt(fplane!, d.W, rects, Forward97);
        }

        var res = new ERes[levels + 1];
        for (var r = 0; r <= levels; r++)
        {
            res[r] = BuildRes(image, o, c, levels, r, rects, d, iplane, fplane, ref blockCounter);
        }

        return new ETileComp { Res = res, Levels = levels };
    }

    private static EBand MakeBand(J2kImage image, J2kOptions o, int c, int levels, int r, int orient, (int X0, int Y0, int W, int H) d, (long X0, long Y0, long X1, long Y1)[] rects)
    {
        long x0, y0, x1, y1;
        int offX = 0, offY = 0;
        if (r == 0)
        {
            x0 = rects[0].X0;
            y0 = rects[0].Y0;
            x1 = rects[0].X1;
            y1 = rects[0].Y1;
        }
        else
        {
            var nb = levels - r + 1;
            var xob = orient is 1 or 3 ? 1L : 0L;
            var yob = orient is 2 or 3 ? 1L : 0L;
            var half = 1L << (nb - 1);
            x0 = CeilPow2(d.X0 - (xob * half), nb);
            y0 = CeilPow2(d.Y0 - (yob * half), nb);
            x1 = CeilPow2(d.X0 + d.W - (xob * half), nb);
            y1 = CeilPow2(d.Y0 + d.H - (yob * half), nb);
            offX = xob == 1 ? (int)(rects[r - 1].X1 - rects[r - 1].X0) : 0;
            offY = yob == 1 ? (int)(rects[r - 1].Y1 - rects[r - 1].Y0) : 0;
        }

        var (eps, mant) = QuantFor(image, o, c, levels, r, orient);
        var gain = orient switch { 0 => 0, 3 => 2, _ => 1 };
        var depth = image.Components[c].Depth;
        var step = o.Reversible ? 1.0 : Math.Pow(2, depth + gain - eps) * (1.0 + (mant / 2048.0));
        return new EBand { Orient = orient, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Eps = eps, Mant = mant, Mb = o.GuardBits + eps - 1, Step = step, OffX = offX, OffY = offY };
    }

    /// <summary>Gets the quantization exponent and mantissa of a band.</summary>
    private static (int Eps, int Mant) QuantFor(J2kImage image, J2kOptions o, int c, int levels, int r, int orient)
    {
        var depth = image.Components[c].Depth;
        var gain = orient switch { 0 => 0, 3 => 2, _ => 1 };
        var bandIndex = r == 0 ? 0 : (3 * (r - 1)) + orient;
        if (o.Reversible)
        {
            return (depth + gain + 1 + (o.Mct ? 1 : 0), 0);
        }

        var mant = o.QuantMantissa ? ((bandIndex * 371) + 97) % 2048 : 0;
        if (o.QuantStyle == 1)
        {
            var eps0 = depth + 2 + levels + 1;
            return (eps0 - (r == 0 ? 0 : r - 1), o.QuantMantissa ? 0x155 : 0);
        }

        return (depth + gain + 1, mant);
    }

    private static ERes BuildRes(J2kImage image, J2kOptions o, int c, int levels, int r, (long X0, long Y0, long X1, long Y1)[] rects, (int X0, int Y0, int W, int H) d, int[] iplane, double[]? fplane, ref int blockCounter)
    {
        var rc = rects[r];
        var (ppx, ppy) = PrecinctOf(o, r);
        var pw = rc.X1 > rc.X0 ? (int)(CeilPow2(rc.X1, ppx) - (rc.X0 >> ppx)) : 0;
        var ph = rc.Y1 > rc.Y0 ? (int)(CeilPow2(rc.Y1, ppy) - (rc.Y0 >> ppy)) : 0;
        var orients = r == 0 ? new[] { 0 } : new[] { 1, 2, 3 };
        var bands = orients.Select(or => MakeBand(image, o, c, levels, r, or, d, rects)).ToArray();
        var precincts = new EPrecinct[pw * ph];
        for (var j = 0; j < ph; j++)
        {
            for (var i = 0; i < pw; i++)
            {
                var parts = new EPrecBand[bands.Length];
                for (var b = 0; b < bands.Length; b++)
                {
                    parts[b] = BuildPrecBand(o, c, r, bands[b], ppx, ppy, (rc.X0 >> ppx) + i, (rc.Y0 >> ppy) + j, d.W, iplane, fplane, ref blockCounter);
                }

                precincts[(j * pw) + i] = new EPrecinct { Bands = parts };
            }
        }

        return new ERes { X0 = rc.X0, Y0 = rc.Y0, X1 = rc.X1, Y1 = rc.Y1, PPx = ppx, PPy = ppy, Pw = pw, Ph = ph, Precincts = precincts };
    }

    private static bool InRoi(J2kOptions o, int c, long u, long v)
    {
        if (o.RoiShift == 0 || (o.RoiComponents is { } rc && !rc.Contains(c)))
        {
            return false;
        }

        return (((u >> 1) + (v >> 1)) & 1) == 0;
    }

    private static EPrecBand BuildPrecBand(J2kOptions o, int c, int r, EBand band, int ppx, int ppy, long pxAbs, long pyAbs, int stride, int[] iplane, double[]? fplane, ref int blockCounter)
    {
        var sx = r == 0 ? ppx : ppx - 1;
        var sy = r == 0 ? ppy : ppy - 1;
        var x0 = Math.Max(pxAbs << sx, band.X0);
        var x1 = Math.Min((pxAbs + 1) << sx, band.X1);
        var y0 = Math.Max(pyAbs << sy, band.Y0);
        var y1 = Math.Min((pyAbs + 1) << sy, band.Y1);
        if (x1 <= x0 || y1 <= y0)
        {
            return new EPrecBand { Band = band };
        }

        var xcb = Math.Min(o.CodeBlockWidthExp, sx);
        var ycb = Math.Min(o.CodeBlockHeightExp, sy);
        var cbx0 = x0 >> xcb;
        var cby0 = y0 >> ycb;
        var cw = (int)(CeilPow2(x1, xcb) - cbx0);
        var ch = (int)(CeilPow2(y1, ycb) - cby0);
        var blocks = new EncodedBlock[cw * ch];
        var tier1 = new Tier1Encoder();
        var totalPlanes = band.Mb + (o.RoiComponents is { } roiComps && !roiComps.Contains(c) ? 0 : o.RoiShift);
        for (var j = 0; j < ch; j++)
        {
            for (var i = 0; i < cw; i++)
            {
                var bx0 = Math.Max(x0, (cbx0 + i) << xcb);
                var bx1 = Math.Min(x1, (cbx0 + i + 1) << xcb);
                var by0 = Math.Max(y0, (cby0 + j) << ycb);
                var by1 = Math.Min(y1, (cby0 + j + 1) << ycb);
                var w = (int)(bx1 - bx0);
                var h = (int)(by1 - by0);
                var values = new int[w * h];
                for (var yy = 0; yy < h; yy++)
                {
                    for (var xx = 0; xx < w; xx++)
                    {
                        var u = bx0 + xx;
                        var v = by0 + yy;
                        var pos = ((band.OffY + (int)(v - band.Y0)) * stride) + band.OffX + (int)(u - band.X0);
                        var q = o.Reversible ? iplane[pos] : Quantize(fplane![pos], band.Step);
                        if (q != 0 && InRoi(o, c, u, v))
                        {
                            q = q < 0 ? -((-q) << o.RoiShift) : q << o.RoiShift;
                        }

                        values[(yy * w) + xx] = q;
                    }
                }

                blocks[(j * cw) + i] = tier1.Encode(values, w, h, band.Orient, o.CodeBlockStyle, totalPlanes);
            }
        }

        var cum = new int[cw * ch][];
        for (var k = 0; k < blocks.Length; k++)
        {
            cum[k] = LayerSplit(blocks[k].Passes, o.Layers, blockCounter++);
        }

        var firstLayer = new int[blocks.Length];
        var zbp = new int[blocks.Length];
        for (var k = 0; k < blocks.Length; k++)
        {
            firstLayer[k] = blocks[k].Passes == 0 ? int.MaxValue : Array.FindIndex(cum[k], v => v > 0);
            zbp[k] = blocks[k].Passes == 0 ? int.MaxValue : blocks[k].ZeroBitPlanes;
        }

        return new EPrecBand
        {
            Band = band,
            Cw = cw,
            Ch = ch,
            Blocks = blocks,
            Cum = cum,
            Included = new bool[blocks.Length],
            Lblock = Enumerable.Repeat(3, blocks.Length).ToArray(),
            Inclusion = new TagTreeEncoder(cw, ch, firstLayer),
            ZeroBits = new TagTreeEncoder(cw, ch, zbp),
        };
    }

    private static int Quantize(double value, double step)
    {
        var q = (int)Math.Floor(Math.Abs(value) / step);
        return value < 0 ? -q : q;
    }

    private static int[] LayerSplit(int total, int layers, int counter)
    {
        var cum = new int[layers];
        for (var l = 0; l < layers; l++)
        {
            if (l == layers - 1)
            {
                cum[l] = total;
                continue;
            }

            cum[l] = (counter % 3) switch
            {
                0 => total * (l + 1) / layers,
                1 => total * l / layers,
                _ => 0,
            };
        }

        return cum;
    }

    // ------------------------------------------------------------------------------------------
    // Packets
    // ------------------------------------------------------------------------------------------

    private static List<Packet> BuildPackets(J2kImage image, J2kOptions o, ETileComp[] comps, int tx0, int ty0, int tx1, int ty1)
    {
        var sequence = new List<(int L, int R, int C, int K)>();
        var sent = new HashSet<(int, int, int, int)>();
        var volumes = o.Poc is { Count: > 0 } poc
            ? poc
            : [new J2kPoc(0, 0, o.Layers, comps.Max(t => t.Levels) + 1, comps.Length, o.Progression)];
        foreach (var v in volumes)
        {
            foreach (var id in Enumerate(image, o, comps, v, tx0, ty0, tx1, ty1))
            {
                if (sent.Add(id))
                {
                    sequence.Add(id);
                }
            }
        }

        var packets = new List<Packet>();
        var sopCounter = 0;
        foreach (var (l, r, c, k) in sequence)
        {
            packets.Add(WritePacket(o, comps[c].Res[r].Precincts[k], l, sopCounter++));
        }

        return packets;
    }

    private static IEnumerable<(int L, int R, int C, int K)> Enumerate(J2kImage image, J2kOptions o, ETileComp[] comps, J2kPoc v, int tx0, int ty0, int tx1, int ty1)
    {
        var layerEnd = Math.Min(v.LayerEnd, o.Layers);
        var compEnd = Math.Min(v.CompEnd, comps.Length);
        var resEnd = v.ResEnd;
        IEnumerable<(int, int, int, int)> Precincts(int l, int r, int c)
        {
            if (r > comps[c].Levels)
            {
                yield break;
            }

            for (var k = 0; k < comps[c].Res[r].Precincts.Length; k++)
            {
                yield return (l, r, c, k);
            }
        }

        switch (v.Order)
        {
            case 0:
                for (var l = 0; l < layerEnd; l++)
                {
                    for (var r = v.ResStart; r < resEnd; r++)
                    {
                        for (var c = v.CompStart; c < compEnd; c++)
                        {
                            foreach (var id in Precincts(l, r, c))
                            {
                                yield return id;
                            }
                        }
                    }
                }

                break;
            case 1:
                for (var r = v.ResStart; r < resEnd; r++)
                {
                    for (var l = 0; l < layerEnd; l++)
                    {
                        for (var c = v.CompStart; c < compEnd; c++)
                        {
                            foreach (var id in Precincts(l, r, c))
                            {
                                yield return id;
                            }
                        }
                    }
                }

                break;
            default:
                foreach (var id in EnumeratePositional(image, comps, v, layerEnd, resEnd, compEnd, tx0, ty0, tx1, ty1))
                {
                    yield return id;
                }

                break;
        }
    }

    private static bool PositionHit(J2kImage image, ETileComp[] comps, int c, int r, int x, int y, int tx0, int ty0, out int k)
    {
        k = 0;
        var t = comps[c];
        if (r > t.Levels)
        {
            return false;
        }

        var res = t.Res[r];
        if (res.Pw == 0 || res.Ph == 0)
        {
            return false;
        }

        var nl = t.Levels;
        var dxr = image.Components[c].Dx;
        var dyr = image.Components[c].Dy;
        var yOk = (y % ((long)dyr << (res.PPy + nl - r))) == 0
                  || (y == ty0 && ((res.Y0 << (nl - r)) % (1L << (res.PPy + nl - r))) != 0);
        var xOk = (x % ((long)dxr << (res.PPx + nl - r))) == 0
                  || (x == tx0 && ((res.X0 << (nl - r)) % (1L << (res.PPx + nl - r))) != 0);
        if (!xOk || !yOk)
        {
            return false;
        }

        var px = (CeilDiv(x, (long)dxr << (nl - r)) >> res.PPx) - (res.X0 >> res.PPx);
        var py = (CeilDiv(y, (long)dyr << (nl - r)) >> res.PPy) - (res.Y0 >> res.PPy);
        k = (int)(px + (res.Pw * py));
        return px >= 0 && py >= 0 && px < res.Pw && py < res.Ph;
    }

    private static IEnumerable<(int L, int R, int C, int K)> EnumeratePositional(J2kImage image, ETileComp[] comps, J2kPoc v, int layerEnd, int resEnd, int compEnd, int tx0, int ty0, int tx1, int ty1)
    {
        if (v.Order == 2)
        {
            for (var r = v.ResStart; r < resEnd; r++)
            {
                for (var y = ty0; y < ty1; y++)
                {
                    for (var x = tx0; x < tx1; x++)
                    {
                        for (var c = v.CompStart; c < compEnd; c++)
                        {
                            if (PositionHit(image, comps, c, r, x, y, tx0, ty0, out var k))
                            {
                                for (var l = 0; l < layerEnd; l++)
                                {
                                    yield return (l, r, c, k);
                                }
                            }
                        }
                    }
                }
            }
        }
        else if (v.Order == 3)
        {
            for (var y = ty0; y < ty1; y++)
            {
                for (var x = tx0; x < tx1; x++)
                {
                    for (var c = v.CompStart; c < compEnd; c++)
                    {
                        for (var r = v.ResStart; r < resEnd; r++)
                        {
                            if (PositionHit(image, comps, c, r, x, y, tx0, ty0, out var k))
                            {
                                for (var l = 0; l < layerEnd; l++)
                                {
                                    yield return (l, r, c, k);
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            for (var c = v.CompStart; c < compEnd; c++)
            {
                for (var y = ty0; y < ty1; y++)
                {
                    for (var x = tx0; x < tx1; x++)
                    {
                        for (var r = v.ResStart; r < resEnd; r++)
                        {
                            if (PositionHit(image, comps, c, r, x, y, tx0, ty0, out var k))
                            {
                                for (var l = 0; l < layerEnd; l++)
                                {
                                    yield return (l, r, c, k);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static int PassesInLayer(EPrecBand pb, int block, int layer) =>
        pb.Cum[block][layer] - (layer == 0 ? 0 : pb.Cum[block][layer - 1]);

    private static Packet WritePacket(J2kOptions o, EPrecinct precinct, int layer, int sequence)
    {
        var bw = new HeaderBitWriter();
        var body = new List<byte>();
        var any = precinct.Bands.Any(pb => Enumerable.Range(0, pb.Blocks.Length).Any(i => PassesInLayer(pb, i, layer) > 0));
        if (!any)
        {
            bw.Bit(0);
        }
        else
        {
            bw.Bit(1);
            foreach (var pb in precinct.Bands)
            {
                for (var i = 0; i < pb.Blocks.Length; i++)
                {
                    WriteBlockContribution(o, pb, i, layer, bw, body);
                }
            }
        }

        var header = new List<byte>(bw.Finish());
        if (o.Eph)
        {
            header.Add(0xFF);
            header.Add(0x92);
        }

        byte[] sop = o.Sop ? [0xFF, 0x91, 0x00, 0x04, (byte)(sequence >> 8), (byte)sequence] : [];
        return new Packet { Sop = sop, Header = [.. header], Data = [.. body] };
    }

    private static void WriteBlockContribution(J2kOptions o, EPrecBand pb, int i, int layer, HeaderBitWriter bw, List<byte> body)
    {
        var block = pb.Blocks[i];
        var n = PassesInLayer(pb, i, layer);
        var cx = i % pb.Cw;
        var cy = i / pb.Cw;
        if (pb.Included[i])
        {
            bw.Bit(n > 0 ? 1 : 0);
        }
        else
        {
            pb.Inclusion!.Encode(bw, cx, cy, layer + 1);
        }

        if (n == 0)
        {
            return;
        }

        if (!pb.Included[i])
        {
            pb.Included[i] = true;
            pb.ZeroBits!.Encode(bw, cx, cy, block.ZeroBitPlanes + 1);
        }

        WritePassCount(bw, n);
        var first = layer == 0 ? 0 : pb.Cum[i][layer - 1];
        var pieces = Pieces(block, first, first + n);
        var increment = 0;
        foreach (var (passes, data) in pieces)
        {
            var need = (32 - int.LeadingZeroCount(Math.Max(1, data.Length))) - (31 - int.LeadingZeroCount(passes));
            increment = Math.Max(increment, need - pb.Lblock[i]);
        }

        for (var k = 0; k < increment; k++)
        {
            bw.Bit(1);
        }

        bw.Bit(0);
        pb.Lblock[i] += increment;
        foreach (var (passes, data) in pieces)
        {
            bw.Bits(data.Length, pb.Lblock[i] + (31 - int.LeadingZeroCount(passes)));
            body.AddRange(data);
        }
    }

    private static List<(int Passes, byte[] Data)> Pieces(EncodedBlock block, int firstPass, int endPass)
    {
        var result = new List<(int, byte[])>();
        var start = 0;
        foreach (var (segPasses, data) in block.Segments)
        {
            var s0 = start;
            var s1 = start + segPasses;
            start = s1;
            var a = Math.Max(firstPass, s0);
            var b = Math.Min(endPass, s1);
            if (b <= a)
            {
                continue;
            }

            var from = data.Length * (a - s0) / segPasses;
            var to = data.Length * (b - s0) / segPasses;
            result.Add((b - a, data[from..to]));
        }

        return result;
    }

    private static void WritePassCount(HeaderBitWriter bw, int n)
    {
        if (n == 1)
        {
            bw.Bit(0);
        }
        else if (n == 2)
        {
            bw.Bits(2, 2);
        }
        else if (n <= 5)
        {
            bw.Bits(3, 2);
            bw.Bits(n - 3, 2);
        }
        else if (n <= 36)
        {
            bw.Bits(0xF, 4);
            bw.Bits(n - 6, 5);
        }
        else
        {
            bw.Bits(0x1FF, 9);
            bw.Bits(n - 37, 7);
        }
    }
}
