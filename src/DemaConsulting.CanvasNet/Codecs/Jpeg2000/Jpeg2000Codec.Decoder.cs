using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Decoding orchestration: tiles -> tier-2 -> tier-1 -> inverse DWT -> inverse MCT -> sample planes -> 8-bit image
    // ================================================================================================

    /// <summary>Decodes a complete image from a raw codestream or JP2 file held in memory.</summary>
    /// <param name="data">The input bytes.</param>
    /// <returns>The decoded image.</returns>
    private static Jpeg2000Image DecodeImage(byte[] data)
    {
        var jp2 = ParseContainer(data);
        var cs = Codestream.Parse(data, jp2.CodestreamStart, jp2.CodestreamEnd);
        var siz = cs.Siz;
        if (siz.Width > Surface.MaxDimension || siz.Height > Surface.MaxDimension)
        {
            throw Malformed($"image dimensions {siz.Width}x{siz.Height} exceed the {Surface.MaxDimension} pixel limit.");
        }

        var layout = ResolveLayout(jp2, siz.Csiz);
        var planes = AllocatePlanes(siz);
        var work = new WorkBudget();
        var tier1 = new Tier1Decoder();
        for (var i = 0; i < cs.Tiles.Length; i++)
        {
            var td = cs.Tiles[i];
            if (td?.State is null)
            {
                continue;
            }

            DecodeTile(siz, td, i, planes, tier1, work);
            td.Body.SetLength(0);
        }

        return Assemble(siz, jp2, layout, planes);
    }

    private static ushort[][] AllocatePlanes(SizInfo siz)
    {
        var planes = new ushort[siz.Csiz][];
        long total = 0;
        for (var c = 0; c < siz.Csiz; c++)
        {
            total += (siz.CompX1(c) - siz.CompX0(c)) * (siz.CompY1(c) - siz.CompY0(c));
            if (total > MaxTotalSamples)
            {
                throw Malformed("image exceeds the decoder sample limit.");
            }
        }

        for (var c = 0; c < siz.Csiz; c++)
        {
            var size = (int)((siz.CompX1(c) - siz.CompX0(c)) * (siz.CompY1(c) - siz.CompY0(c)));
            var plane = new ushort[size];

            // Tiles that carry no data decode as zero coefficients, which is mid-range after the DC shift.
            Array.Fill(plane, (ushort)(1 << (siz.Depth[c] - 1)));
            planes[c] = plane;
        }

        return planes;
    }

    private static void DecodeTile(SizInfo siz, TileData td, int index, ushort[][] planes, Tier1Decoder tier1, WorkBudget work)
    {
        var st = td.State!;
        var tile = BuildTile(siz, st, index);
        ReadTilePackets(tile, st, siz, td);

        var count = tile.Comps.Length;
        var ints = new int[count][];
        var floats = new float[count][];
        for (var c = 0; c < count; c++)
        {
            var tc = tile.Comps[c];
            var size = tc.Res[^1].Width * tc.Res[^1].Height;
            if (tc.Coding.Reversible)
            {
                ints[c] = new int[size];
            }
            else
            {
                floats[c] = new float[size];
            }

            DecodeBlocks(tc, ints[c], floats[c], tier1, work);
            var stride = tc.Res[^1].Width;
            if (tc.Coding.Reversible)
            {
                InverseDwt53(ints[c], stride, tc.Res);
            }
            else
            {
                InverseDwt97(floats[c], stride, tc.Res);
            }
        }

        if (st.Mct)
        {
            InverseMct(tile, ints, floats);
        }

        for (var c = 0; c < count; c++)
        {
            StoreComponent(siz, tile.Comps[c], c, ints[c], floats[c], planes[c]);
        }
    }

    private static void DecodeBlocks(TileComp tc, int[]? ints, float[]? floats, Tier1Decoder tier1, WorkBudget work)
    {
        var stride = tc.Res[^1].Width;
        foreach (var res in tc.Res)
        {
            foreach (var precinct in res.Precincts)
            {
                foreach (var pb in precinct.Bands)
                {
                    foreach (var block in pb.Blocks)
                    {
                        tier1.DecodeBlock(block, pb.Band, tc.Coding.Style, tc.RoiShift, tc.Coding.Reversible, ints, floats, stride, work);
                        block.Data = [];
                    }
                }
            }
        }
    }

    private static void InverseMct(Tile tile, int[][] ints, float[][] floats)
    {
        if (tile.Comps.Length < 3)
        {
            throw Malformed("multiple component transform needs at least three components.");
        }

        var a = tile.Comps[0];
        var b = tile.Comps[1];
        var c = tile.Comps[2];
        if (a.Res[^1].Width != b.Res[^1].Width || a.Res[^1].Width != c.Res[^1].Width
            || a.Res[^1].Height != b.Res[^1].Height || a.Res[^1].Height != c.Res[^1].Height
            || a.Coding.Reversible != b.Coding.Reversible || a.Coding.Reversible != c.Coding.Reversible)
        {
            throw Malformed("multiple component transform applied to incompatible components.");
        }

        if (a.Coding.Reversible)
        {
            var y0 = ints[0];
            var y1 = ints[1];
            var y2 = ints[2];
            for (var i = 0; i < y0.Length; i++)
            {
                var g = y0[i] - ((y2[i] + y1[i]) >> 2);
                var r = y2[i] + g;
                var bl = y1[i] + g;
                y0[i] = r;
                y1[i] = g;
                y2[i] = bl;
            }
        }
        else
        {
            var y0 = floats[0];
            var y1 = floats[1];
            var y2 = floats[2];
            for (var i = 0; i < y0.Length; i++)
            {
                var y = y0[i];
                var cb = y1[i];
                var cr = y2[i];
                y0[i] = y + (1.402f * cr);
                y1[i] = y - (0.34413f * cb) - (0.71414f * cr);
                y2[i] = y + (1.772f * cb);
            }
        }
    }

    private static void StoreComponent(SizInfo siz, TileComp tc, int c, int[]? ints, float[]? floats, ushort[] plane)
    {
        var w = tc.Res[^1].Width;
        var h = tc.Res[^1].Height;
        var depth = siz.Depth[c];
        var shift = 1 << (depth - 1);
        var max = (1 << depth) - 1;
        var ox = (int)(tc.X0 - siz.CompX0(c));
        var oy = (int)(tc.Y0 - siz.CompY0(c));
        var planeWidth = (int)(siz.CompX1(c) - siz.CompX0(c));
        for (var y = 0; y < h; y++)
        {
            var src = y * w;
            var dst = ((oy + y) * planeWidth) + ox;
            for (var x = 0; x < w; x++)
            {
                var v = ints is not null ? ints[src + x] : (int)MathF.Floor(floats![src + x] + 0.5f);
                plane[dst + x] = (ushort)Math.Clamp(v + shift, 0, max);
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Final assembly: upsampling, palette, 8-bit scaling, color conversion
    // ------------------------------------------------------------------------------------------

    private static Jpeg2000Image Assemble(SizInfo siz, Jp2Info jp2, ChannelLayout layout, ushort[][] planes)
    {
        var width = (int)siz.Width;
        var height = (int)siz.Height;
        var nColor = layout.Color.Length;
        var color = new byte[checked(width * height * nColor)];
        var alpha = layout.Alpha is null ? null : new byte[checked(width * height)];

        var sources = new List<ChannelSource>(layout.Color);
        if (layout.Alpha is { } a)
        {
            sources.Add(a);
        }

        var luts = sources.Select(s => BuildLut(siz, jp2.Palette, s)).ToArray();
        var xmaps = sources.Select(s => BuildAxisMap(siz.XOsiz, width, siz.XR[s.Component], siz.CompX0(s.Component), siz.CompX1(s.Component))).ToArray();
        var compWidths = sources.Select(s => (int)(siz.CompX1(s.Component) - siz.CompX0(s.Component))).ToArray();
        var ymaps = sources.Select(s => BuildAxisMap(siz.YOsiz, height, siz.YR[s.Component], siz.CompY0(s.Component), siz.CompY1(s.Component))).ToArray();

        for (var y = 0; y < height; y++)
        {
            for (var k = 0; k < sources.Count; k++)
            {
                var plane = planes[sources[k].Component];
                var row = ymaps[k][y] * compWidths[k];
                var lut = luts[k];
                var xmap = xmaps[k];
                if (k < nColor)
                {
                    var o = (y * width * nColor) + k;
                    for (var x = 0; x < width; x++)
                    {
                        color[o] = lut[plane[row + xmap[x]]];
                        o += nColor;
                    }
                }
                else
                {
                    var o = y * width;
                    for (var x = 0; x < width; x++)
                    {
                        alpha![o + x] = lut[plane[row + xmap[x]]];
                    }
                }
            }
        }

        if (layout.IsSycc)
        {
            ConvertSyccToRgb(color);
        }

        return new Jpeg2000Image(width, height, layout.ColorSpace, nColor, color, alpha, layout.Premultiplied, jp2.IccProfile)
        {
            BitDepth = siz.Depth[layout.Color[0].Component],
            HasPalette = layout.Color.Any(s => s.PaletteColumn >= 0) && jp2.Palette is not null,
        };
    }

    private static int[] BuildAxisMap(long origin, int length, int subsampling, long compStart, long compEnd)
    {
        var map = new int[length];
        var last = (int)(compEnd - compStart - 1);
        if (last < 0)
        {
            throw Malformed("component has no samples.");
        }
        for (var i = 0; i < length; i++)
        {
            var v = (int)(((origin + i) / subsampling) - compStart);
            map[i] = Math.Clamp(v, 0, last);
        }

        return map;
    }

    private static byte[] BuildLut(SizInfo siz, PaletteInfo? palette, ChannelSource source)
    {
        var depth = siz.Depth[source.Component];
        var lut = new byte[1 << depth];
        for (var v = 0; v < lut.Length; v++)
        {
            if (source.PaletteColumn >= 0 && palette is not null)
            {
                var entry = Math.Min(v, palette.Entries - 1);
                var bits = palette.Depth[source.PaletteColumn];
                lut[v] = ScaleTo8(palette.Values[(entry * palette.Columns) + source.PaletteColumn], bits);
            }
            else
            {
                lut[v] = ScaleTo8(v, depth);
            }
        }

        return lut;
    }

    private static byte ScaleTo8(int value, int bits)
    {
        if (bits == 8)
        {
            return (byte)value;
        }

        long max = (1L << bits) - 1;
        return (byte)Math.Min(255L, ((Math.Min(value, max) * 255) + (max / 2)) / max);
    }

    private static void ConvertSyccToRgb(byte[] samples)
    {
        for (var i = 0; i + 2 < samples.Length; i += 3)
        {
            var y = (float)samples[i];
            var cb = samples[i + 1] - 128f;
            var cr = samples[i + 2] - 128f;
            samples[i] = ClampByte(y + (1.402f * cr));
            samples[i + 1] = ClampByte(y - (0.344136f * cb) - (0.714136f * cr));
            samples[i + 2] = ClampByte(y + (1.772f * cb));
        }
    }

    private static byte ClampByte(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);
}
