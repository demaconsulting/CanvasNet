namespace DemaConsulting.CanvasNet.Codecs;

public static partial class TiffCodec
{
    /// <summary>
    ///     Writes a complete little-endian TIFF file: the 8-byte header, a single Image File
    ///     Directory describing an 8-bit RGBA Chunky single-strip image, the externally stored
    ///     <c>BitsPerSample</c> array, and the strip data itself.
    /// </summary>
    private static void WriteTiffFile(
        Stream stream,
        int width,
        int height,
        TiffCompression compression,
        bool usePredictor,
        byte[] stripData)
    {
        var entryCount = usePredictor ? 12 : 11;
        var ifdSize = 2 + (entryCount * 12) + 4;
        var bitsPerSampleOffset = 8 + ifdSize;
        var bitsPerSampleSize = SavedSamplesPerPixel * 2;
        var stripDataOffset = bitsPerSampleOffset + bitsPerSampleSize;

        var header = new byte[8];
        header[0] = (byte)'I';
        header[1] = (byte)'I';
        WriteUInt16Le(header, 2, 42);
        WriteUInt32Le(header, 4, 8);
        stream.Write(header, 0, header.Length);

        var ifd = new byte[ifdSize];
        WriteUInt16Le(ifd, 0, (ushort)entryCount);
        var pos = 2;
        WriteIfdEntry(ifd, ref pos, TagImageWidth, TypeLong, 1, (uint)width);
        WriteIfdEntry(ifd, ref pos, TagImageLength, TypeLong, 1, (uint)height);
        WriteIfdEntry(ifd, ref pos, TagBitsPerSample, TypeShort, SavedSamplesPerPixel, (uint)bitsPerSampleOffset);
        WriteIfdEntry(ifd, ref pos, TagCompression, TypeShort, 1, (uint)compression);
        WriteIfdEntry(ifd, ref pos, TagPhotometricInterpretation, TypeShort, 1, PhotometricRgb);
        WriteIfdEntry(ifd, ref pos, TagStripOffsets, TypeLong, 1, (uint)stripDataOffset);
        WriteIfdEntry(ifd, ref pos, TagSamplesPerPixel, TypeShort, 1, SavedSamplesPerPixel);
        WriteIfdEntry(ifd, ref pos, TagRowsPerStrip, TypeLong, 1, (uint)height);
        WriteIfdEntry(ifd, ref pos, TagStripByteCounts, TypeLong, 1, (uint)stripData.Length);
        WriteIfdEntry(ifd, ref pos, TagPlanarConfiguration, TypeShort, 1, PlanarChunky);
        if (usePredictor)
        {
            WriteIfdEntry(ifd, ref pos, TagPredictor, TypeShort, 1, PredictorHorizontal);
        }

        WriteIfdEntry(ifd, ref pos, TagExtraSamples, TypeShort, 1, ExtraSamplesUnassociatedAlpha);
        WriteUInt32Le(ifd, pos, 0); // No next IFD (single page)
        stream.Write(ifd, 0, ifd.Length);

        var bitsPerSample = new byte[bitsPerSampleSize];
        for (var i = 0; i < SavedSamplesPerPixel; i++)
        {
            WriteUInt16Le(bitsPerSample, i * 2, 8);
        }

        stream.Write(bitsPerSample, 0, bitsPerSample.Length);

        stream.Write(stripData, 0, stripData.Length);
    }

    /// <summary>
    ///     Writes one 12-byte IFD entry (tag, type, count, and inline value or offset) at
    ///     <paramref name="pos"/>, then advances <paramref name="pos"/> past it.
    /// </summary>
    private static void WriteIfdEntry(byte[] buffer, ref int pos, ushort tag, ushort type, uint count, uint value)
    {
        WriteUInt16Le(buffer, pos, tag);
        WriteUInt16Le(buffer, pos + 2, type);
        WriteUInt32Le(buffer, pos + 4, count);
        if (type == TypeShort)
        {
            WriteUInt16Le(buffer, pos + 8, (ushort)value);
        }
        else
        {
            WriteUInt32Le(buffer, pos + 8, value);
        }

        pos += 12;
    }

    /// <summary>
    ///     Represents one parsed 12-byte TIFF IFD entry: its type, count, and the raw 4-byte
    ///     value/offset field exactly as stored in the file (interpreted later using the file's
    ///     own byte order).
    /// </summary>
    private sealed class IfdEntry
    {
        public required ushort Type { get; init; }

        public required uint Count { get; init; }

        public required byte[] ValueBytes { get; init; }
    }

    /// <summary>
    ///     Parses a TIFF Image File Directory at the given file offset into a lookup of tag number
    ///     to parsed entry. Only the first IFD is read; any subsequent IFD offset is ignored.
    /// </summary>
    private static Dictionary<ushort, IfdEntry> ParseIfd(ITiffDataSource source, uint ifdOffset, bool bigEndian)
    {
        var ifdOffsetInt = ToInt32Checked(ifdOffset, "IFD offset");
        var countBytes = source.ReadBytes(ifdOffsetInt, 2, "IFD entry count");
        var entryCount = ReadUInt16(countBytes, 0, bigEndian);

        var tags = new Dictionary<ushort, IfdEntry>();
        var pos = ToInt32Checked((long)ifdOffsetInt + 2, "IFD entries offset");
        for (var i = 0; i < entryCount; i++)
        {
            var entryBytes = source.ReadBytes(pos, 12, "IFD entry");
            var tag = ReadUInt16(entryBytes, 0, bigEndian);
            var type = ReadUInt16(entryBytes, 2, bigEndian);
            var count = ReadUInt32(entryBytes, 4, bigEndian);
            var valueBytes = entryBytes.AsSpan(8, 4).ToArray();
            tags[tag] = new IfdEntry { Type = type, Count = count, ValueBytes = valueBytes };
            pos += 12;
        }

        return tags;
    }

    /// <summary>
    ///     Resolves an IFD entry's values to an array of <see cref="uint"/>, reading them either
    ///     from the entry's inline 4-byte value field or, when they do not fit inline, from the
    ///     file offset stored in that field (fetched via <paramref name="source"/>).
    /// </summary>
    private static uint[] ReadTagValues(ITiffDataSource source, IfdEntry entry, bool bigEndian, int? maxCount = null)
    {
        if (entry.Count == 0)
        {
            throw new InvalidDataException("TIFF tag has a declared value count of 0, which is not valid.");
        }

        if (maxCount is { } cap && entry.Count > cap)
        {
            throw new InvalidDataException(
                $"TIFF tag has an implausibly large declared value count {entry.Count}; expected at most {cap}.");
        }

        var typeSize = entry.Type switch
        {
            TypeByte => 1,
            TypeShort => 2,
            TypeLong => 4,
            _ => throw new InvalidDataException($"Unsupported TIFF tag value type {entry.Type}.")
        };

        var totalSize = (long)typeSize * entry.Count;
        var valueBuffer = totalSize <= 4
            ? entry.ValueBytes
            : source.ReadBytes(
                ToInt32Checked(ReadUInt32(entry.ValueBytes, 0, bigEndian), "tag value offset"),
                ToInt32Checked(totalSize, "tag value array length"),
                "tag value array");

        var result = new uint[entry.Count];
        for (var i = 0; i < entry.Count; i++)
        {
            result[i] = entry.Type switch
            {
                TypeByte => valueBuffer[i],
                TypeShort => ReadUInt16(valueBuffer, i * 2, bigEndian),
                _ => ReadUInt32(valueBuffer, i * 4, bigEndian)
            };
        }

        return result;
    }

    /// <summary>
    ///     Converts a non-negative <see cref="long"/> to an <see cref="int"/>, throwing
    ///     <see cref="InvalidDataException"/> (rather than letting an <see cref="OverflowException"/>
    ///     escape) when the value is negative or exceeds <see cref="int.MaxValue"/>. Used for every
    ///     file-position/length value derived from attacker-controlled TIFF header/tag fields.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="what">A short description of the value, used in the error message.</param>
    private static int ToInt32Checked(long value, string what)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"TIFF {what} value {value} exceeds the supported range.");
        }

        return (int)value;
    }

    /// <summary>
    ///     Resolves a mandatory tag's values, throwing <see cref="InvalidDataException"/> if the
    ///     tag is not present in the directory.
    /// </summary>
    /// <param name="source">The abstracted, random-access-capable TIFF byte source.</param>
    /// <param name="tags">The parsed IFD tag lookup.</param>
    /// <param name="tag">The TIFF tag number to resolve.</param>
    /// <param name="tagName">The tag's human-readable name, used in the "missing tag" error message.</param>
    /// <param name="bigEndian">The file's detected byte order.</param>
    /// <param name="maxCount">
    ///     When non-null, the maximum declared value <c>Count</c> allowed for this tag; a larger
    ///     declared count is rejected with <see cref="InvalidDataException"/> before any
    ///     count-proportional allocation or read is performed.
    /// </param>
    private static uint[] RequireTagValues(
        ITiffDataSource source, Dictionary<ushort, IfdEntry> tags, ushort tag, string tagName, bool bigEndian, int? maxCount = null)
    {
        if (!tags.TryGetValue(tag, out var entry))
        {
            throw new InvalidDataException($"Missing mandatory TIFF tag {tagName}.");
        }

        return ReadTagValues(source, entry, bigEndian, maxCount);
    }

    /// <summary>
    ///     Resolves an optional tag's values, returning null if the tag is not present in the
    ///     directory.
    /// </summary>
    /// <param name="source">The abstracted, random-access-capable TIFF byte source.</param>
    /// <param name="tags">The parsed IFD tag lookup.</param>
    /// <param name="tag">The TIFF tag number to resolve.</param>
    /// <param name="bigEndian">The file's detected byte order.</param>
    /// <param name="maxCount">
    ///     When non-null, the maximum declared value <c>Count</c> allowed for this tag; see
    ///     <see cref="RequireTagValues"/>.
    /// </param>
    private static uint[]? TryGetTagValues(
        ITiffDataSource source, Dictionary<ushort, IfdEntry> tags, ushort tag, bool bigEndian, int? maxCount = null) =>
        tags.TryGetValue(tag, out var entry) ? ReadTagValues(source, entry, bigEndian, maxCount) : null;
}
