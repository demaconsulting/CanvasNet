namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // JP2 file format boxes (ITU-T T.800 Annex I) and channel layout resolution
    // ================================================================================================

    private const long BoxJp2Signature = 0x6A502020;
    private const long BoxJp2Header = 0x6A703268;
    private const long BoxColr = 0x636F6C72;
    private const long BoxPclr = 0x70636C72;
    private const long BoxCmap = 0x636D6170;
    private const long BoxCdef = 0x63646566;
    private const long BoxCodestream = 0x6A703263;

    /// <summary>The maximum number of boxes examined at one nesting level.</summary>
    private const int MaxBoxes = 4096;

    /// <summary>The maximum number of channels (after palette mapping) the decoder handles.</summary>
    private const int MaxChannels = 16;

    /// <summary>Palette data from the <c>pclr</c> box.</summary>
    internal sealed class PaletteInfo
    {
        /// <summary>Gets the number of palette entries.</summary>
        public int Entries { get; init; }

        /// <summary>Gets the number of palette columns.</summary>
        public int Columns { get; init; }

        /// <summary>Gets the bit depth of each column.</summary>
        public required int[] Depth { get; init; }

        /// <summary>Gets the entry values, row-major (entry * Columns + column).</summary>
        public required int[] Values { get; init; }
    }

    /// <summary>One component mapping record from the <c>cmap</c> box.</summary>
    internal readonly record struct ComponentMapping(int Component, int Type, int Column);

    /// <summary>One channel definition record from the <c>cdef</c> box.</summary>
    internal readonly record struct ChannelDefinition(int Channel, int Type, int Association);

    /// <summary>The parsed JP2 wrapper: where the codestream is and how to interpret it.</summary>
    internal sealed class Jp2Info
    {
        /// <summary>Gets or sets the offset of the codestream.</summary>
        public int CodestreamStart { get; set; }

        /// <summary>Gets or sets the exclusive end of the codestream.</summary>
        public int CodestreamEnd { get; set; }

        /// <summary>Gets or sets the enumerated color space, or -1 when none was specified.</summary>
        public int EnumCs { get; set; } = -1;

        /// <summary>Gets or sets the embedded ICC profile, if any.</summary>
        public byte[]? IccProfile { get; set; }

        /// <summary>Gets or sets the palette, if any.</summary>
        public PaletteInfo? Palette { get; set; }

        /// <summary>Gets the component mappings.</summary>
        public List<ComponentMapping> Mappings { get; } = [];

        /// <summary>Gets the channel definitions.</summary>
        public List<ChannelDefinition> Definitions { get; } = [];
    }

    /// <summary>Where a channel's samples come from: a component, optionally mapped through a palette column.</summary>
    internal readonly record struct ChannelSource(int Component, int PaletteColumn);

    /// <summary>The resolved interpretation of the channels of an image.</summary>
    internal sealed class ChannelLayout
    {
        /// <summary>Gets the color space.</summary>
        public Jpeg2000ColorSpace ColorSpace { get; init; }

        /// <summary>Gets a value indicating whether the color channels are sYCC and must be converted.</summary>
        public bool IsSycc { get; init; }

        /// <summary>Gets the color channel sources in order.</summary>
        public required ChannelSource[] Color { get; init; }

        /// <summary>Gets the alpha channel source, or <see langword="null"/>.</summary>
        public ChannelSource? Alpha { get; init; }

        /// <summary>Gets a value indicating whether the alpha channel is premultiplied.</summary>
        public bool Premultiplied { get; init; }
    }

    /// <summary>Detects whether the data is a raw codestream or a JP2 file and locates the codestream.</summary>
    /// <param name="data">The input bytes.</param>
    /// <returns>The wrapper information.</returns>
    private static Jp2Info ParseContainer(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0x4F)
        {
            return new Jp2Info { CodestreamStart = 0, CodestreamEnd = data.Length };
        }

        if (data.Length < 12 || ReadBe32(data, 0) != 12 || ReadBe32(data, 4) != BoxJp2Signature)
        {
            throw Malformed("data is neither a JPEG 2000 codestream nor a JP2 file.");
        }

        if (ReadBe32(data, 8) != 0x0D0A870A)
        {
            throw Malformed("invalid JP2 signature box content.");
        }

        var info = new Jp2Info();
        var pos = 0;
        var found = false;
        for (var count = 0; count < MaxBoxes && pos < data.Length; count++)
        {
            var box = ReadBoxHeader(data, pos, data.Length);
            if (box.Type == BoxJp2Header)
            {
                ParseHeaderBoxes(data, box.ContentStart, box.End, info);
            }
            else if (box.Type == BoxCodestream)
            {
                info.CodestreamStart = box.ContentStart;
                info.CodestreamEnd = box.End;
                found = true;
                break;
            }

            pos = box.End;
        }

        if (!found)
        {
            throw Malformed("JP2 file has no codestream box.");
        }

        return info;
    }

    private static long ReadBe32(byte[] data, int pos) =>
        ((long)data[pos] << 24) | ((long)data[pos + 1] << 16) | ((long)data[pos + 2] << 8) | data[pos + 3];

    private readonly record struct BoxHeader(long Type, int ContentStart, int End);

    private static BoxHeader ReadBoxHeader(byte[] data, int pos, int limit)
    {
        if (limit - pos < 8)
        {
            throw Malformed("truncated box header.");
        }

        var length = ReadBe32(data, pos);
        var type = ReadBe32(data, pos + 4);
        var header = 8;
        if (length == 1)
        {
            if (limit - pos < 16)
            {
                throw Malformed("truncated extended box header.");
            }

            var hi = ReadBe32(data, pos + 8);
            var lo = ReadBe32(data, pos + 12);
            length = (hi << 32) | lo;
            header = 16;
            if (length < 0)
            {
                throw Malformed("box length is out of range.");
            }
        }
        else if (length == 0)
        {
            length = limit - pos;
        }

        if (length < header || length > limit - pos)
        {
            throw Malformed("invalid box length.");
        }

        return new BoxHeader(type, pos + header, pos + (int)length);
    }

    private static void ParseHeaderBoxes(byte[] data, int start, int end, Jp2Info info)
    {
        var pos = start;
        for (var count = 0; count < MaxBoxes && pos < end; count++)
        {
            var box = ReadBoxHeader(data, pos, end);
            switch (box.Type)
            {
                case BoxColr:
                    ParseColr(data, box, info);
                    break;
                case BoxPclr:
                    info.Palette ??= ParsePalette(data, box);
                    break;
                case BoxCmap:
                    if (info.Mappings.Count == 0)
                    {
                        ParseCmap(data, box, info);
                    }

                    break;
                case BoxCdef:
                    if (info.Definitions.Count == 0)
                    {
                        ParseCdef(data, box, info);
                    }

                    break;
                default:
                    break;
            }

            pos = box.End;
        }
    }

    private static void ParseColr(byte[] data, BoxHeader box, Jp2Info info)
    {
        if (box.End - box.ContentStart < 3)
        {
            throw Malformed("truncated color specification box.");
        }

        if (info.EnumCs >= 0 || info.IccProfile is not null)
        {
            return;
        }

        var method = data[box.ContentStart];
        var body = box.ContentStart + 3;
        if (method == 1)
        {
            var length = box.End - body;
            var enumCs = length >= 4 ? ReadBe32(data, body) : -1;

            // EnumCS 14 (CIELab) and 19 (CIEJab) carry extra parameter bytes; every other enumerated space is exactly 4.
            if (length < 4 || (length != 4 && enumCs != 14 && enumCs != 19))
            {
                throw Malformed("color specification box has an invalid length.");
            }

            info.EnumCs = (int)Math.Min(enumCs, int.MaxValue);
        }
        else if (method == 2)
        {
            var profile = new byte[box.End - body];
            Array.Copy(data, body, profile, 0, profile.Length);
            info.IccProfile = profile;
        }
    }

    private static PaletteInfo ParsePalette(byte[] data, BoxHeader box)
    {
        var r = new ByteReader(data, box.ContentStart, box.End);
        var entries = r.ReadU16();
        var columns = r.ReadU8();
        if (entries < 1 || entries > 1024 || columns < 1)
        {
            throw Malformed("invalid palette dimensions.");
        }

        var depth = new int[columns];
        for (var i = 0; i < columns; i++)
        {
            var depthByte = r.ReadU8();
            if ((depthByte & 0x80) != 0)
            {
                throw Unsupported("jpeg2000-signed-palette", "palette columns with signed entries.");
            }

            depth[i] = (depthByte & 0x7F) + 1;
            if (depth[i] > 16)
            {
                throw Unsupported("jpeg2000-bit-depth", "palette entries deeper than 16 bits.");
            }
        }

        var values = new int[entries * columns];
        for (var e = 0; e < entries; e++)
        {
            for (var c = 0; c < columns; c++)
            {
                values[(e * columns) + c] = depth[c] > 8 ? r.ReadU16() : r.ReadU8();
            }
        }

        r.RequireEnd("palette box");
        return new PaletteInfo { Entries = entries, Columns = columns, Depth = depth, Values = values };
    }

    private static void ParseCmap(byte[] data, BoxHeader box, Jp2Info info)
    {
        var r = new ByteReader(data, box.ContentStart, box.End);
        if (r.Remaining % 4 != 0 || r.Remaining / 4 > MaxChannels)
        {
            throw Malformed("invalid component mapping box.");
        }

        while (r.Remaining > 0)
        {
            var comp = r.ReadU16();
            var type = r.ReadU8();
            var column = r.ReadU8();
            if (type > 1)
            {
                throw Malformed("invalid component mapping type.");
            }

            info.Mappings.Add(new ComponentMapping(comp, type, column));
        }
    }

    private static void ParseCdef(byte[] data, BoxHeader box, Jp2Info info)
    {
        var r = new ByteReader(data, box.ContentStart, box.End);
        var n = r.ReadU16();
        if (n > MaxChannels || r.Remaining != n * 6)
        {
            throw Malformed("invalid channel definition box.");
        }

        for (var i = 0; i < n; i++)
        {
            info.Definitions.Add(new ChannelDefinition(r.ReadU16(), r.ReadU16(), r.ReadU16()));
        }
    }

    /// <summary>Works out which channels are color and alpha, and in which color space.</summary>
    /// <param name="jp2">The wrapper information (an empty one for raw codestreams).</param>
    /// <param name="componentCount">The number of codestream components.</param>
    /// <returns>The resolved layout.</returns>
    private static ChannelLayout ResolveLayout(Jp2Info jp2, int componentCount)
    {
        var channels = BuildChannels(jp2, componentCount);
        var space = ColorSpaceOf(jp2);
        var sycc = jp2.EnumCs == 18;
        var expected = space switch
        {
            Jpeg2000ColorSpace.Gray => 1,
            Jpeg2000ColorSpace.Srgb => 3,
            Jpeg2000ColorSpace.Cmyk => 4,
            _ => 0,
        };

        var color = new List<ChannelSource>();
        ChannelSource? alpha = null;
        var premultiplied = false;
        if (jp2.Definitions.Count > 0)
        {
            var byAssoc = new SortedDictionary<int, ChannelSource>();
            foreach (var def in jp2.Definitions)
            {
                if (def.Channel >= channels.Length)
                {
                    throw Malformed("channel definition refers to a missing channel.");
                }

                if (def.Type == 0 && def.Association >= 1 && def.Association <= MaxChannels)
                {
                    byAssoc[def.Association] = channels[def.Channel];
                }
                else if ((def.Type == 1 || def.Type == 2) && alpha is null)
                {
                    alpha = channels[def.Channel];
                    premultiplied = def.Type == 2;
                }
            }

            var expectedAssoc = 1;
            foreach (var (assoc, source) in byAssoc)
            {
                if (assoc != expectedAssoc++)
                {
                    throw Malformed("channel definitions leave a gap in the color channels.");
                }

                color.Add(source);
            }
        }
        else
        {
            var take = expected > 0 ? expected : DefaultColorCount(channels.Length);
            if (channels.Length < take)
            {
                throw Malformed("image has fewer channels than its color space requires.");
            }

            color.AddRange(channels.Take(take));
        }

        if (expected > 0 && color.Count != expected)
        {
            throw Malformed("channel count does not match the color space.");
        }

        if (color.Count == 0)
        {
            throw Malformed("image has no color channels.");
        }

        // Heuristic, documented: only when the file carries no color specification at all (a raw codestream, or a
        // JP2 file without a usable colr box) are one, three and four color channels taken as grey, RGB and CMYK.
        if (space == Jpeg2000ColorSpace.Unknown)
        {
            space = color.Count switch { 1 => Jpeg2000ColorSpace.Gray, 3 => Jpeg2000ColorSpace.Srgb, 4 => Jpeg2000ColorSpace.Cmyk, _ => Jpeg2000ColorSpace.Unknown };
            if (jp2.EnumCs >= 0 || jp2.IccProfile is not null)
            {
                space = Jpeg2000ColorSpace.Unknown;
            }
        }

        if (color.Count is not (1 or 3 or 4))
        {
            throw Unsupported("jpeg2000-channels", $"{color.Count} color channels are not supported.");
        }

        // Four color channels are converted as CMYK, so they must be CMYK: either declared, or (above) the
        // heuristic for unspecified color. Any other declared color space with four channels is not guessed at.
        if (color.Count == 4 && space != Jpeg2000ColorSpace.Cmyk)
        {
            throw Unsupported("jpeg2000-color-space", "four color channels in a color space other than CMYK.");
        }

        return new ChannelLayout
        {
            ColorSpace = space,
            IsSycc = sycc && color.Count == 3,
            Color = [.. color],
            Alpha = alpha,
            Premultiplied = premultiplied,
        };
    }

    private static int DefaultColorCount(int channelCount) => channelCount switch
    {
        <= 2 => 1,
        3 => 3,
        4 => 4,
        _ => throw Unsupported("jpeg2000-channels", "more than four channels without a channel definition."),
    };

    private static ChannelSource[] BuildChannels(Jp2Info jp2, int componentCount)
    {
        if (jp2.Palette is null || jp2.Mappings.Count == 0)
        {
            if (jp2.Palette is not null)
            {
                throw Malformed("palette without a component mapping.");
            }

            return [.. Enumerable.Range(0, componentCount).Select(c => new ChannelSource(c, -1))];
        }

        var list = new ChannelSource[jp2.Mappings.Count];
        for (var i = 0; i < list.Length; i++)
        {
            var m = jp2.Mappings[i];
            if (m.Component >= componentCount || (m.Type == 1 && m.Column >= jp2.Palette.Columns))
            {
                throw Malformed("component mapping refers to a missing component or palette column.");
            }

            list[i] = new ChannelSource(m.Component, m.Type == 1 ? m.Column : -1);
        }

        return list;
    }

    private static Jpeg2000ColorSpace ColorSpaceOf(Jp2Info jp2)
    {
        if (jp2.EnumCs >= 0)
        {
            return jp2.EnumCs switch
            {
                16 or 18 => Jpeg2000ColorSpace.Srgb,
                17 => Jpeg2000ColorSpace.Gray,
                12 => Jpeg2000ColorSpace.Cmyk,
                _ => Jpeg2000ColorSpace.Unknown,
            };
        }

        if (jp2.IccProfile is { Length: >= 20 } icc)
        {
            return System.Text.Encoding.ASCII.GetString(icc, 16, 4) switch
            {
                "GRAY" => Jpeg2000ColorSpace.Gray,
                "RGB " => Jpeg2000ColorSpace.Srgb,
                "CMYK" => Jpeg2000ColorSpace.Cmyk,
                _ => Jpeg2000ColorSpace.Unknown,
            };
        }

        return Jpeg2000ColorSpace.Unknown;
    }
}
