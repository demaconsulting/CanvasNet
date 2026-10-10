#pragma warning disable S3218, S3358, S1172, S107, S3776, S1541, S134, S1244
// cspell:ignore ppm ppt tlm plt crg rgn poc cprl rpcl pcrl rlcp lrcp sop eph xcb ycb
// cspell:ignore pclr cmap cdef bpcc colr ihdr ftyp jp2c jp2h
using System.Text;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>JP2 wrapper options of <see cref="Jpeg2000TestEncoder.WrapJp2"/>.</summary>
internal sealed class J2kJp2Options
{
    /// <summary>Gets or sets the enumerated color space (16 sRGB, 17 grayscale, 18 sYCC, 12 CMYK); ignored when an ICC profile is set.</summary>
    public int EnumCs { get; set; } = 16;

    /// <summary>Gets or sets an ICC profile written with METH 2, or <see langword="null"/>.</summary>
    public byte[]? Icc { get; set; }

    /// <summary>Gets or sets a value indicating whether a <c>bpcc</c> box is written.</summary>
    public bool WriteBpcc { get; set; }

    /// <summary>Gets or sets the palette entries (entries by columns), or <see langword="null"/>.</summary>
    public int[][]? Palette { get; set; }

    /// <summary>Gets or sets the palette column bit depths.</summary>
    public int[] PaletteDepths { get; set; } = [];

    /// <summary>Gets or sets the component mapping records, or <see langword="null"/>.</summary>
    public (int Component, int Type, int Column)[]? Cmap { get; set; }

    /// <summary>Gets or sets the channel definition records, or <see langword="null"/>.</summary>
    public (int Channel, int Type, int Association)[]? Cdef { get; set; }

    /// <summary>Gets or sets a value indicating whether the codestream box uses an extended (64-bit) length.</summary>
    public bool ExtendedLength { get; set; }

    /// <summary>Gets or sets a value indicating whether the codestream box length is written as zero (to end of file).</summary>
    public bool ZeroLength { get; set; }
}

internal static partial class Jpeg2000TestEncoder
{
    // ------------------------------------------------------------------------------------------
    // Marker writers
    // ------------------------------------------------------------------------------------------

    private static void WriteMarker(List<byte> dst, int marker)
    {
        dst.Add((byte)(marker >> 8));
        dst.Add((byte)marker);
    }

    private static void Put16(List<byte> dst, int v)
    {
        dst.Add((byte)(v >> 8));
        dst.Add((byte)v);
    }

    private static void Put32(List<byte> dst, long v)
    {
        dst.Add((byte)(v >> 24));
        dst.Add((byte)(v >> 16));
        dst.Add((byte)(v >> 8));
        dst.Add((byte)v);
    }

    private static void WriteSegment(List<byte> dst, int marker, List<byte> payload)
    {
        WriteMarker(dst, marker);
        Put16(dst, payload.Count + 2);
        dst.AddRange(payload);
    }

    private static void WriteSiz(List<byte> dst, J2kImage image, J2kOptions o, int tw, int th)
    {
        var p = new List<byte>();
        Put16(p, 0);
        Put32(p, image.Xsiz);
        Put32(p, image.Ysiz);
        Put32(p, image.XOsiz);
        Put32(p, image.YOsiz);
        Put32(p, tw);
        Put32(p, th);
        Put32(p, o.TileXOffset);
        Put32(p, o.TileYOffset);
        Put16(p, image.Components.Length);
        foreach (var c in image.Components)
        {
            p.Add((byte)((c.Depth - 1) | (c.Signed ? 0x80 : 0)));
            p.Add((byte)c.Dx);
            p.Add((byte)c.Dy);
        }

        WriteSegment(dst, 0xFF51, p);
    }

    private static void WriteSpParams(List<byte> p, J2kOptions o, int levels)
    {
        p.Add((byte)levels);
        p.Add((byte)(o.CodeBlockWidthExp - 2));
        p.Add((byte)(o.CodeBlockHeightExp - 2));
        p.Add((byte)o.CodeBlockStyle);
        p.Add((byte)(o.Reversible ? 1 : 0));
        if (o.Precincts is not null)
        {
            for (var r = 0; r <= levels; r++)
            {
                var (px, py) = PrecinctOf(o, r);
                p.Add((byte)((py << 4) | px));
            }
        }
    }

    private static List<byte> QuantBytes(J2kImage image, J2kOptions o, int c)
    {
        var levels = LevelsOf(o, c);
        var p = new List<byte>();
        if (o.Reversible)
        {
            p.Add((byte)(o.GuardBits << 5));
            for (var r = 0; r <= levels; r++)
            {
                foreach (var orient in r == 0 ? new[] { 0 } : new[] { 1, 2, 3 })
                {
                    p.Add((byte)(QuantFor(image, o, c, levels, r, orient).Eps << 3));
                }
            }

            return p;
        }

        if (o.QuantStyle == 1)
        {
            p.Add((byte)((o.GuardBits << 5) | 1));
            var (eps, mant) = QuantFor(image, o, c, levels, 0, 0);
            Put16(p, (eps << 11) | mant);
            return p;
        }

        p.Add((byte)((o.GuardBits << 5) | 2));
        for (var r = 0; r <= levels; r++)
        {
            foreach (var orient in r == 0 ? new[] { 0 } : new[] { 1, 2, 3 })
            {
                var (eps, mant) = QuantFor(image, o, c, levels, r, orient);
                Put16(p, (eps << 11) | mant);
            }
        }

        return p;
    }

    /// <summary>Writes COD, COC, QCD, QCC and RGN markers for <paramref name="o"/>.</summary>
    private static void WriteParamMarkers(List<byte> dst, J2kImage image, J2kOptions o)
    {
        var n = image.Components.Length;
        var cod = new List<byte>
        {
            (byte)((o.Precincts is not null ? 1 : 0) | (o.Sop ? 2 : 0) | (o.Eph ? 4 : 0)),
            (byte)o.Progression,
        };
        Put16(cod, o.Layers);
        cod.Add((byte)(o.Mct ? 1 : 0));
        WriteSpParams(cod, o, o.Levels);
        WriteSegment(dst, 0xFF52, cod);
        for (var c = 0; c < n; c++)
        {
            if (LevelsOf(o, c) != o.Levels)
            {
                var coc = new List<byte> { (byte)c, (byte)(o.Precincts is not null ? 1 : 0) };
                WriteSpParams(coc, o, LevelsOf(o, c));
                WriteSegment(dst, 0xFF53, coc);
            }
        }

        var qcd = QuantBytes(image, o, 0);
        WriteSegment(dst, 0xFF5C, qcd);
        for (var c = 1; c < n; c++)
        {
            var q = QuantBytes(image, o, c);
            if (!q.SequenceEqual(qcd))
            {
                var qcc = new List<byte> { (byte)c };
                qcc.AddRange(q);
                WriteSegment(dst, 0xFF5D, qcc);
            }
        }

        if (o.RoiShift > 0)
        {
            for (var c = 0; c < n; c++)
            {
                if (o.RoiComponents is null || o.RoiComponents.Contains(c))
                {
                    WriteSegment(dst, 0xFF5E, [(byte)c, 0, (byte)o.RoiShift]);
                }
            }
        }
    }

    private static void WritePocMarker(List<byte> dst, List<J2kPoc> poc)
    {
        var p = new List<byte>();
        foreach (var e in poc)
        {
            p.Add((byte)e.ResStart);
            p.Add((byte)e.CompStart);
            Put16(p, e.LayerEnd);
            p.Add((byte)e.ResEnd);
            p.Add((byte)(e.CompEnd & 0xFF));
            p.Add((byte)e.Order);
        }

        WriteSegment(dst, 0xFF5F, p);
    }

    private static void WriteCodingMarkers(List<byte> dst, J2kImage image, J2kOptions o, int tileParts)
    {
        _ = tileParts;
        WriteParamMarkers(dst, image, o);
        if (o.Poc is not null && !o.PocInTileHeader)
        {
            WritePocMarker(dst, o.Poc);
        }
    }

    private static void WriteExtraMainMarkers(List<byte> dst, J2kImage image, List<(int Tile, byte[] Bytes)> tileParts)
    {
        WriteMarker(dst, 0xFF30);
        var tlm = new List<byte> { 0, 0x60 };
        foreach (var (tile, bytes) in tileParts)
        {
            Put16(tlm, tile);
            Put32(tlm, bytes.Length);
        }

        WriteSegment(dst, 0xFF55, tlm);
        var crg = new List<byte>();
        for (var c = 0; c < image.Components.Length; c++)
        {
            Put16(crg, 0);
            Put16(crg, 0);
        }

        WriteSegment(dst, 0xFF63, crg);
        var com = new List<byte> { 0, 1 };
        com.AddRange(Encoding.ASCII.GetBytes("Jpeg2000TestEncoder"));
        WriteSegment(dst, 0xFF64, com);
    }

    private static void WritePpm(List<byte> dst, List<byte[]> chunks, int chunkSize)
    {
        var stream = new List<byte>();
        foreach (var c in chunks)
        {
            Put32(stream, c.Length);
            stream.AddRange(c);
        }

        var max = chunkSize > 0 ? chunkSize : 65535 - 3;
        var z = 0;
        for (var pos = 0; pos < stream.Count || z == 0; pos += max)
        {
            var len = Math.Min(max, stream.Count - pos);
            var seg = new List<byte> { (byte)z++ };
            seg.AddRange(stream.GetRange(pos, len));
            WriteSegment(dst, 0xFF60, seg);
            if (len == 0)
            {
                break;
            }
        }
    }

    private static void AppendTileParts(
        int tile, J2kOptions main, J2kOptions p, bool isOverride, List<Packet> packets, List<(int Tile, byte[] Bytes)> parts, List<byte[]> ppmChunks, J2kImage image)
    {
        var k = Math.Max(1, Math.Min(main.TileParts, packets.Count));
        for (var part = 0; part < k; part++)
        {
            var group = packets.Skip(packets.Count * part / k).Take((packets.Count * (part + 1) / k) - (packets.Count * part / k)).ToList();
            var body = new List<byte>();
            var headers = new List<byte>();
            foreach (var pk in group)
            {
                body.AddRange(main.HeaderMode == J2kHeaderMode.InBand ? pk.Sop.Concat(pk.Header).Concat(pk.Data) : pk.Sop.Concat(pk.Data));
                headers.AddRange(pk.Header);
            }

            var hdr = new List<byte>();
            if (part == 0)
            {
                if (isOverride)
                {
                    WriteParamMarkers(hdr, image, p);
                }

                if (p.Poc is not null && (isOverride || main.PocInTileHeader))
                {
                    WritePocMarker(hdr, p.Poc);
                }
            }

            if (main.ExtraMarkers)
            {
                var plt = new List<byte> { (byte)part };
                foreach (var pk in group)
                {
                    WriteVarInt(plt, pk.Sop.Length + pk.Header.Length + pk.Data.Length);
                }

                WriteSegment(hdr, 0xFF58, plt);
                WriteSegment(hdr, 0xFF64, [0, 1, (byte)'t']);
            }

            if (main.HeaderMode == J2kHeaderMode.Ppm)
            {
                ppmChunks.Add([.. headers]);
            }
            else if (main.HeaderMode == J2kHeaderMode.Ppt)
            {
                WritePpt(hdr, headers, main.PackedChunkSize);
            }

            var bytes = new List<byte>();
            WriteMarker(bytes, 0xFF90);
            Put16(bytes, 10);
            Put16(bytes, tile);
            Put32(bytes, 12 + hdr.Count + 2 + body.Count);
            bytes.Add((byte)part);
            bytes.Add((byte)k);
            bytes.AddRange(hdr);
            WriteMarker(bytes, 0xFF93);
            bytes.AddRange(body);
            parts.Add((tile, [.. bytes]));
        }
    }

    private static void WriteVarInt(List<byte> dst, int value)
    {
        var groups = new List<byte>();
        do
        {
            groups.Insert(0, (byte)(value & 0x7F));
            value >>= 7;
        }
        while (value > 0);
        for (var i = 0; i < groups.Count - 1; i++)
        {
            groups[i] |= 0x80;
        }

        dst.AddRange(groups);
    }

    private static void WritePpt(List<byte> dst, List<byte> headers, int chunkSize)
    {
        var max = chunkSize > 0 ? chunkSize : 65535 - 3;
        var z = 0;
        for (var pos = 0; pos < headers.Count || z == 0; pos += max)
        {
            var len = Math.Min(max, headers.Count - pos);
            var seg = new List<byte> { (byte)z++ };
            seg.AddRange(headers.GetRange(pos, len));
            WriteSegment(dst, 0xFF61, seg);
            if (len == 0)
            {
                break;
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // JP2 wrapper
    // ------------------------------------------------------------------------------------------

    private static void WriteBox(List<byte> dst, string type, IEnumerable<byte> payload)
    {
        var data = payload.ToArray();
        Put32(dst, data.Length + 8);
        dst.AddRange(Encoding.ASCII.GetBytes(type));
        dst.AddRange(data);
    }

    /// <summary>Wraps a codestream in a JP2 file.</summary>
    /// <param name="image">The image the codestream was made from.</param>
    /// <param name="codestream">The codestream.</param>
    /// <param name="o">The wrapper options.</param>
    /// <returns>The JP2 file bytes.</returns>
    public static byte[] WrapJp2(J2kImage image, byte[] codestream, J2kJp2Options o)
    {
        var file = new List<byte>();
        WriteBox(file, "jP  ", [0x0D, 0x0A, 0x87, 0x0A]);
        WriteBox(file, "ftyp", [.. Encoding.ASCII.GetBytes("jp2 "), 0, 0, 0, 0, .. Encoding.ASCII.GetBytes("jp2 ")]);
        var header = new List<byte>();
        var ihdr = new List<byte>();
        Put32(ihdr, image.Height);
        Put32(ihdr, image.Width);
        Put16(ihdr, image.Components.Length);
        var uniform = image.Components.All(c => c.Depth == image.Components[0].Depth && c.Signed == image.Components[0].Signed);
        ihdr.Add(uniform ? (byte)((image.Components[0].Depth - 1) | (image.Components[0].Signed ? 0x80 : 0)) : (byte)255);
        ihdr.AddRange([7, 0, 0]);
        WriteBox(header, "ihdr", ihdr);
        if (o.WriteBpcc)
        {
            WriteBox(header, "bpcc", image.Components.Select(c => (byte)((c.Depth - 1) | (c.Signed ? 0x80 : 0))));
        }

        var colr = new List<byte>();
        if (o.Icc is not null)
        {
            colr.AddRange([2, 0, 0]);
            colr.AddRange(o.Icc);
        }
        else
        {
            colr.AddRange([1, 0, 0]);
            Put32(colr, o.EnumCs);
        }

        WriteBox(header, "colr", colr);
        if (o.Palette is { } palette)
        {
            var pclr = new List<byte>();
            Put16(pclr, palette.Length);
            pclr.Add((byte)o.PaletteDepths.Length);
            pclr.AddRange(o.PaletteDepths.Select(d => (byte)(d - 1)));
            foreach (var entry in palette)
            {
                for (var col = 0; col < o.PaletteDepths.Length; col++)
                {
                    if (o.PaletteDepths[col] > 8)
                    {
                        Put16(pclr, entry[col]);
                    }
                    else
                    {
                        pclr.Add((byte)entry[col]);
                    }
                }
            }

            WriteBox(header, "pclr", pclr);
        }

        if (o.Cmap is { } cmap)
        {
            var b = new List<byte>();
            foreach (var (comp, type, col) in cmap)
            {
                Put16(b, comp);
                b.Add((byte)type);
                b.Add((byte)col);
            }

            WriteBox(header, "cmap", b);
        }

        if (o.Cdef is { } cdef)
        {
            var b = new List<byte>();
            Put16(b, cdef.Length);
            foreach (var (channel, type, assoc) in cdef)
            {
                Put16(b, channel);
                Put16(b, type);
                Put16(b, assoc);
            }

            WriteBox(header, "cdef", b);
        }

        WriteBox(file, "jp2h", header);
        if (o.ZeroLength)
        {
            Put32(file, 0);
            file.AddRange(Encoding.ASCII.GetBytes("jp2c"));
        }
        else if (o.ExtendedLength)
        {
            Put32(file, 1);
            file.AddRange(Encoding.ASCII.GetBytes("jp2c"));
            Put32(file, 0);
            Put32(file, codestream.Length + 16);
        }
        else
        {
            Put32(file, codestream.Length + 8);
            file.AddRange(Encoding.ASCII.GetBytes("jp2c"));
        }

        file.AddRange(codestream);
        return [.. file];
    }
}
