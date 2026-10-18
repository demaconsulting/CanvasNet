// cspell:ignore macStyle unitsPerEm numTables hmtx hhea maxp loca glyf
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     The catalog-relevant metadata of a single font face, as read by <see cref="SfntHeaderReader"/>.
/// </summary>
/// <param name="FamilyName">The <c>name</c>-table family name, or <see langword="null"/> when absent.</param>
/// <param name="SubfamilyName">The <c>name</c>-table subfamily name, or <see langword="null"/> when absent.</param>
/// <param name="Bold">The face's bold flag (same derivation as <see cref="TrueTypeFont.IsBold"/>).</param>
/// <param name="Italic">The face's italic flag (same derivation as <see cref="TrueTypeFont.IsItalic"/>).</param>
/// <param name="FixedPitch">The face's fixed-pitch flag (same derivation as <see cref="TrueTypeFont.IsFixedPitch"/>).</param>
internal readonly record struct SfntFaceInfo(
    string? FamilyName,
    string? SubfamilyName,
    bool Bold,
    bool Italic,
    bool FixedPitch);

/// <summary>
///     Reads only the header-level metadata <see cref="SystemFontCatalog"/> needs (face count,
///     family/subfamily names, bold/italic/fixed-pitch style) from a seekable font stream,
///     without loading the whole font file the way <see cref="TrueTypeFont.Load(Stream, int)"/>
///     does.
/// </summary>
/// <remarks>
///     <para>
///     Only the 12-byte offset table, the table directory, the <c>head</c> table's fixed prefix,
///     the first 6 bytes of <c>maxp</c>, the <c>name</c> table, and the leading 64 bytes of
///     <c>OS/2</c> and 16 bytes of <c>post</c> are read - never the glyph, outline, or metrics
///     data. The structural checks mirror <see cref="SfntContainer.Parse(byte[], int)"/> (every
///     table directory entry must lie within the file; <c>OTTO</c> requires <c>CFF </c>), plus
///     presence-only checks for the tables <see cref="TrueTypeFont"/> requires
///     (<c>head</c>/<c>maxp</c>/<c>hhea</c>/<c>hmtx</c> and <c>loca</c>+<c>glyf</c> or
///     <c>CFF </c>), a non-zero <c>head.unitsPerEm</c>, a valid <c>head.indexToLocFormat</c> for
///     glyf-flavored faces, and a <c>maxp</c> version consistent with the outline flavor. The
///     glyph data itself is not validated, so a face whose headers are well
///     formed but whose outline data is corrupt is catalogued here and only fails when it is
///     actually loaded.
///     </para>
///     <para>
///     Name and style parsing is delegated to the same <see cref="NameTable"/>/<see cref="StyleTable"/>
///     parsers <see cref="TrueTypeFont"/> uses, so a valid font yields identical metadata.
///     </para>
/// </remarks>
internal static class SfntHeaderReader
{
    private const uint TtcTag = 0x74746366; // 'ttcf'
    private static readonly string[] RequiredTables = ["head", "maxp", "hhea", "hmtx"];
    private const uint TrueTypeVersion = 0x00010000;
    private const uint MacTrueVersion = 0x74727565; // 'true'
    private const uint OttoVersion = 0x4F54544F; // 'OTTO'
    private const int OffsetTableSize = 12;
    private const int DirectoryEntrySize = 16;
    private const int TtcHeaderPrefixSize = 12;
    private const int HeadSize = 54;
    private const int Os2Prefix = 64;
    private const int PostPrefix = 16;
    private const int MaxpPrefix = 6;

    /// <summary>
    ///     Reports how many faces <paramref name="stream"/> holds: the <c>ttcf</c> face count, or
    ///     <c>1</c> for an ordinary (non-collection) file.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned anywhere; its position is changed.</param>
    /// <exception cref="InvalidDataException">Thrown when a <c>ttcf</c> header is truncated or declares zero faces.</exception>
    public static int GetFaceCount(Stream stream)
    {
        var length = stream.Length;
        if (length < 4)
        {
            return 1;
        }

        var tag = ReadAt(stream, 0, 4);
        if (SfntContainer.ReadUInt32(tag, 0) != TtcTag)
        {
            return 1;
        }

        return ReadFaceOffsets(stream, length).Length;
    }

    /// <summary>
    ///     Reads the catalog metadata of face <paramref name="faceIndex"/> of <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">A readable, seekable stream; its position is changed.</param>
    /// <param name="faceIndex">The zero-based face index (must be <c>0</c> for a non-collection file).</param>
    /// <returns>The face's metadata.</returns>
    /// <exception cref="InvalidDataException">Thrown when the headers are malformed or required tables are missing.</exception>
    public static SfntFaceInfo ReadFace(Stream stream, int faceIndex)
    {
        var length = stream.Length;
        var offsetTableStart = ResolveOffsetTableStart(stream, length, faceIndex);
        var tables = ReadTableDirectory(stream, offsetTableStart, length, out var version);

        var isCff = tables.ContainsKey("CFF ");
        ValidateRequiredTables(tables, version, isCff);

        var macStyle = ValidateHeadAndMaxp(stream, tables, isCff);

        var name = tables.TryGetValue("name", out var nameRange)
            ? NameTable.Parse(ReadAt(stream, nameRange.Offset, checked((int)nameRange.Length)), 0, (int)nameRange.Length)
            : NameTable.Empty;

        // OS/2 and post are packed (truncated to the bytes StyleTable reads) into one small buffer.
        var os2Length = tables.TryGetValue("OS/2", out var os2Range) ? (int)Math.Min(os2Range.Length, Os2Prefix) : -1;
        var postLength = tables.TryGetValue("post", out var postRange) ? (int)Math.Min(postRange.Length, PostPrefix) : -1;
        var packed = new byte[Math.Max(os2Length, 0) + Math.Max(postLength, 0)];
        if (os2Length >= 0)
        {
            ReadAt(stream, os2Range.Offset, os2Length).CopyTo(packed, 0);
        }

        if (postLength >= 0)
        {
            ReadAt(stream, postRange.Offset, postLength).CopyTo(packed, Math.Max(os2Length, 0));
        }

        (int, int)? os2 = os2Length >= 0 ? (0, os2Length) : null;
        (int, int)? post = postLength >= 0 ? (Math.Max(os2Length, 0), postLength) : null;
        var style = StyleTable.Parse(packed, macStyle, os2, post);

        return new SfntFaceInfo(name.FamilyName, name.SubfamilyName, style.IsBold, style.IsItalic, style.IsFixedPitch);
    }

    private static long ResolveOffsetTableStart(Stream stream, long length, int faceIndex)
    {
        if (length >= 4 && SfntContainer.ReadUInt32(ReadAt(stream, 0, 4), 0) == TtcTag)
        {
            var offsets = ReadFaceOffsets(stream, length);
            if ((uint)faceIndex >= (uint)offsets.Length)
            {
                throw new InvalidDataException("Face index is outside the collection.");
            }

            return offsets[faceIndex];
        }

        if (faceIndex != 0)
        {
            throw new InvalidDataException("Face index must be 0 for a non-collection font file.");
        }

        return 0L;
    }

    private static Dictionary<string, (long Offset, long Length)> ReadTableDirectory(
        Stream stream, long offsetTableStart, long length, out uint version)
    {
        if (offsetTableStart + OffsetTableSize > length)
        {
            throw new InvalidDataException("Font data is too short to contain an SFNT offset table.");
        }

        var header = ReadAt(stream, offsetTableStart, OffsetTableSize);
        version = SfntContainer.ReadUInt32(header, 0);
        if (version != TrueTypeVersion && version != MacTrueVersion && version != OttoVersion)
        {
            throw new InvalidDataException($"Unrecognized SFNT version 0x{version:X8}.");
        }

        var numTables = SfntContainer.ReadUInt16(header, 4);
        var directoryBytes = (long)numTables * DirectoryEntrySize;
        if (offsetTableStart + OffsetTableSize + directoryBytes > length)
        {
            throw new InvalidDataException("Font data is too short to contain its declared table directory.");
        }

        var directory = ReadAt(stream, offsetTableStart + OffsetTableSize, (int)directoryBytes);
        var tables = new Dictionary<string, (long Offset, long Length)>(numTables, StringComparer.Ordinal);
        for (var i = 0; i < numTables; i++)
        {
            var entry = i * DirectoryEntrySize;
            var tag = System.Text.Encoding.Latin1.GetString(directory, entry, 4);
            var tableOffset = SfntContainer.ReadUInt32(directory, entry + 8);
            var tableLength = SfntContainer.ReadUInt32(directory, entry + 12);
            if ((long)tableOffset + tableLength > length)
            {
                throw new InvalidDataException(
                    $"Table '{tag}' offset/length ({tableOffset}/{tableLength}) exceeds the font data bounds.");
            }

            tables[tag] = (tableOffset, tableLength);
        }

        return tables;
    }

    private static void ValidateRequiredTables(Dictionary<string, (long Offset, long Length)> tables, uint version, bool isCff)
    {
        if (version == OttoVersion && !isCff)
        {
            throw new InvalidDataException("Font uses CFF/OpenType ('OTTO') outlines but has no 'CFF ' table.");
        }

        var missingTable = RequiredTables.FirstOrDefault(t => !tables.ContainsKey(t));
        if (missingTable is not null)
        {
            throw new InvalidDataException($"Required table '{missingTable}' is missing from the font.");
        }

        if (!isCff && (!tables.ContainsKey("loca") || !tables.ContainsKey("glyf")))
        {
            throw new InvalidDataException("Required table 'loca' or 'glyf' is missing from the font.");
        }
    }

    private static ushort ValidateHeadAndMaxp(Stream stream, Dictionary<string, (long Offset, long Length)> tables, bool isCff)
    {
        var head = tables["head"];
        if (head.Length < HeadSize)
        {
            throw new InvalidDataException("The 'head' table is too short.");
        }

        var headBytes = ReadAt(stream, head.Offset, HeadSize);
        if (SfntContainer.ReadUInt16(headBytes, 18) == 0)
        {
            throw new InvalidDataException("'head.unitsPerEm' must not be zero.");
        }

        // Mirrors TrueTypeFont.Load: a glyf-flavored face needs a valid loca format selector.
        if (!isCff)
        {
            var indexToLocFormat = SfntContainer.ReadInt16(headBytes, 50);
            if (indexToLocFormat != 0 && indexToLocFormat != 1)
            {
                throw new InvalidDataException($"'head.indexToLocFormat' has an unsupported value ({indexToLocFormat}).");
            }
        }

        var macStyle = SfntContainer.ReadUInt16(headBytes, 44);

        var maxp = tables["maxp"];
        if (maxp.Length < MaxpPrefix)
        {
            throw new InvalidDataException("The 'maxp' table is too short.");
        }

        var maxpVersion = SfntContainer.ReadUInt32(ReadAt(stream, maxp.Offset, MaxpPrefix), 0);
        if (isCff ? maxpVersion != 0x00005000 : maxpVersion != TrueTypeVersion || maxp.Length < 32)
        {
            throw new InvalidDataException("The 'maxp' table is inconsistent with the font's outline flavor.");
        }

        return macStyle;
    }

    private static long[] ReadFaceOffsets(Stream stream, long length)
    {
        if (length < TtcHeaderPrefixSize)
        {
            throw new InvalidDataException("Font data is too short to contain a 'ttcf' header.");
        }

        var prefix = ReadAt(stream, 0, TtcHeaderPrefixSize);
        var numFonts = SfntContainer.ReadUInt32(prefix, 8);
        if (numFonts == 0)
        {
            throw new InvalidDataException("A 'ttcf' container must declare at least one font.");
        }

        if (TtcHeaderPrefixSize + ((long)numFonts * 4) > length)
        {
            throw new InvalidDataException("Font data is too short to contain its declared 'ttcf' face offset table.");
        }

        var table = ReadAt(stream, TtcHeaderPrefixSize, checked((int)numFonts * 4));
        var offsets = new long[numFonts];
        for (var i = 0; i < offsets.Length; i++)
        {
            var offset = SfntContainer.ReadUInt32(table, i * 4);
            if (offset > length)
            {
                throw new InvalidDataException("A 'ttcf' face offset exceeds the font data bounds.");
            }

            offsets[i] = offset;
        }

        return offsets;
    }

    private static byte[] ReadAt(Stream stream, long position, int count)
    {
        var buffer = new byte[count];
        stream.Seek(position, SeekOrigin.Begin);
        stream.ReadExactly(buffer, 0, count);
        return buffer;
    }
}
