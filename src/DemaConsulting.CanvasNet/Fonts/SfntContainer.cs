// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO ttcf
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses the SFNT offset table and table directory shared by TrueType (<c>glyf</c>-based) and
///     OpenType (CFF-based) font files, parses the leading <c>ttcf</c> TrueType Collection header
///     when present, and hosts the shared big-endian primitive readers used throughout the
///     <see cref="Fonts"/> namespace.
/// </summary>
/// <remarks>
///     <para>
///     <c>sfntVersion</c> must be <c>0x00010000</c>, the classic Mac <c>'true'</c> tag, or the
///     CFF/OpenType tag <c>'OTTO'</c>; any other value is rejected with
///     <see cref="InvalidDataException"/>. An <c>'OTTO'</c>-tagged font whose table directory has
///     no <c>CFF </c> table is itself structurally inconsistent (it claims CFF outlines but
///     supplies none) and is also rejected here, at the container level, rather than left for
///     <see cref="TrueTypeFont"/> to discover later - this is a fact available directly from the
///     table directory this type already parses, not a decision that depends on any
///     <see cref="TrueTypeFont"/>-specific semantics. Whether a <c>'true'</c>/<c>0x00010000</c>-tagged
///     font actually supplies its own required TrueType tables (<c>glyf</c>/<c>loca</c>) is left to
///     <see cref="TrueTypeFont"/>, since that decision does depend on which outline flavor the font
///     declares.
///     </para>
///     <para>
///     Every table directory entry's <c>offset</c>/<c>length</c> pair is validated against the
///     overall file length using <see langword="long"/> arithmetic (never raw <see langword="uint"/>
///     addition), so a maliciously large offset or length can never overflow the bounds check
///     itself.
///     </para>
///     <para>
///     SFNT multi-byte fields are always big-endian regardless of host CPU/OS endianness; every
///     primitive reader here composes its result from individual bytes (never <see cref="BitConverter"/>
///     or <see cref="System.Buffers.Binary.BinaryPrimitives"/>), matching the convention already
///     established by <see cref="Codecs.PngCodec"/>/<see cref="Codecs.TiffCodec"/>.
///     </para>
/// </remarks>
internal sealed class SfntContainer
{
    /// <summary>
    ///     The <c>sfntVersion</c> value used by TrueType-outline fonts.
    /// </summary>
    private const uint TrueTypeVersion = 0x00010000;

    /// <summary>
    ///     The classic Mac OS <c>sfntVersion</c> tag ('true') used by some TrueType-outline fonts.
    /// </summary>
    private const uint MacTrueVersion = 0x74727565;

    /// <summary>
    ///     The CFF/OpenType <c>sfntVersion</c> tag ('OTTO'). Accepted only when the table
    ///     directory also declares a <c>CFF </c> table (see this class's remarks).
    /// </summary>
    private const uint OttoVersion = 0x4F54544F;

    /// <summary>
    ///     The TrueType Collection <c>ttcTag</c> value ('ttcf'), identifying a multi-face font
    ///     collection container rather than a bare SFNT offset table.
    /// </summary>
    private const uint TtcTag = 0x74746366;

    /// <summary>
    ///     The size, in bytes, of the 12-byte SFNT offset table.
    /// </summary>
    private const int OffsetTableSize = 12;

    /// <summary>
    ///     The size, in bytes, of a single table directory entry.
    /// </summary>
    private const int TableDirectoryEntrySize = 16;

    /// <summary>
    ///     The size, in bytes, of the <c>ttcf</c> header prefix (<c>ttcTag</c>, <c>majorVersion</c>,
    ///     <c>minorVersion</c>, <c>numFonts</c>) preceding the per-face offset table.
    /// </summary>
    private const int TtcHeaderPrefixSize = 12;

    /// <summary>
    ///     Every table's raw byte range, keyed by its 4-character tag.
    /// </summary>
    private readonly Dictionary<string, (int Offset, int Length)> _tables;

    /// <summary>
    ///     Initializes a new container over an already-validated table directory.
    /// </summary>
    private SfntContainer(Dictionary<string, (int Offset, int Length)> tables)
    {
        _tables = tables;
    }

    /// <summary>
    ///     Parses the SFNT offset table and table directory starting at the beginning of
    ///     <paramref name="data"/> (offset table start <c>0</c>).
    /// </summary>
    /// <param name="data">The complete, in-memory font file contents.</param>
    /// <returns>A new <see cref="SfntContainer"/> describing every table's byte range.</returns>
    /// <exception cref="InvalidDataException">
    ///     See <see cref="Parse(byte[], int)"/>.
    /// </exception>
    public static SfntContainer Parse(byte[] data) => Parse(data, 0);

    /// <summary>
    ///     Parses a single face's SFNT offset table and table directory starting at
    ///     <paramref name="offsetTableStart"/> within <paramref name="data"/>.
    /// </summary>
    /// <param name="data">
    ///     The complete, in-memory font file contents - for a <c>ttcf</c> TrueType Collection,
    ///     this is the whole container (every face's table directory entry offset/length is
    ///     already absolute from the start of this array, per the <c>ttcf</c> format, regardless
    ///     of which face's offset table <paramref name="offsetTableStart"/> selects).
    /// </param>
    /// <param name="offsetTableStart">
    ///     The byte offset within <paramref name="data"/> where this face's 12-byte SFNT offset
    ///     table begins - <c>0</c> for an ordinary (non-collection) SFNT file, or one of the
    ///     face offsets returned by <see cref="TryReadTtcHeader"/> for a <c>ttcf</c> container.
    /// </param>
    /// <returns>A new <see cref="SfntContainer"/> describing every table's byte range.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="data"/> is too short to contain a 12-byte offset table (at
    ///     <paramref name="offsetTableStart"/>) or its declared table directory, when
    ///     <c>sfntVersion</c> is <c>'OTTO'</c> without a <c>CFF </c> table present, or any other
    ///     value not recognized as TrueType or CFF/OpenType, or when any table directory entry's
    ///     <c>offset</c>/<c>length</c> falls outside the bounds of <paramref name="data"/>.
    /// </exception>
    public static SfntContainer Parse(byte[] data, int offsetTableStart)
    {
        if (offsetTableStart < 0 || checked((long)offsetTableStart + OffsetTableSize) > data.Length)
        {
            throw new InvalidDataException("Font data is too short to contain an SFNT offset table.");
        }

        var version = ReadUInt32(data, offsetTableStart);
        if (version != TrueTypeVersion && version != MacTrueVersion && version != OttoVersion)
        {
            throw new InvalidDataException($"Unrecognized SFNT version 0x{version:X8}.");
        }

        var numTables = ReadUInt16(data, offsetTableStart + 4);
        var directoryEnd = checked((long)offsetTableStart + OffsetTableSize + (long)numTables * TableDirectoryEntrySize);
        if (directoryEnd > data.Length)
        {
            throw new InvalidDataException("Font data is too short to contain its declared table directory.");
        }

        var tables = new Dictionary<string, (int Offset, int Length)>(numTables, StringComparer.Ordinal);
        for (var i = 0; i < numTables; i++)
        {
            var entryOffset = offsetTableStart + OffsetTableSize + i * TableDirectoryEntrySize;
            var tag = ReadTag(data, entryOffset);
            var tableOffset = ReadUInt32(data, entryOffset + 8);
            var tableLength = ReadUInt32(data, entryOffset + 12);

            var end = checked((long)tableOffset + tableLength);
            if (end > data.Length)
            {
                throw new InvalidDataException(
                    $"Table '{tag}' offset/length ({tableOffset}/{tableLength}) exceeds the font data bounds.");
            }

            tables[tag] = ((int)tableOffset, (int)tableLength);
        }

        if (version == OttoVersion && !tables.ContainsKey("CFF "))
        {
            throw new InvalidDataException(
                "Font uses CFF/OpenType ('OTTO') outlines but has no 'CFF ' table.");
        }

        return new SfntContainer(tables);
    }

    /// <summary>
    ///     Reports whether <paramref name="data"/>'s leading 4 bytes match one of the recognized
    ///     SFNT <c>sfntVersion</c>/<c>ttcTag</c> values (<see cref="TrueTypeVersion"/>,
    ///     <see cref="MacTrueVersion"/>, <see cref="OttoVersion"/>, or <see cref="TtcTag"/>) - a
    ///     lightweight, non-throwing shape sniff distinguishing an SFNT-wrapped font program from
    ///     a bare (standalone) CFF program (see <see cref="CffTable.LooksLikeCffHeader"/>). Used
    ///     by <c>Pdf.PdfDocument</c>'s own <c>/FontFile3</c> shape-sniffing dispatch, since the
    ///     PDF specification permits (and real-world producers sometimes emit) a mismatch
    ///     between a <c>/FontFile3</c> stream's declared <c>/Subtype</c> name and its actual byte
    ///     container shape.
    /// </summary>
    /// <param name="data">The candidate font program bytes.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="data"/> is at least 4 bytes long and its
    ///     leading 4 bytes match a recognized SFNT version or collection tag; otherwise,
    ///     <see langword="false"/>. Deliberately shallow - this method does not otherwise
    ///     validate the table directory or any table's own structure, unlike
    ///     <see cref="Parse(byte[], int)"/>: a value recognized here can still fail that
    ///     method's own, stricter validation.
    /// </returns>
    public static bool LooksLikeSfnt(byte[] data) =>
        data.Length >= 4 &&
        ReadUInt32(data, 0) is TrueTypeVersion or MacTrueVersion or OttoVersion or TtcTag;

    /// <summary>
    ///     Attempts to parse a leading <c>ttcf</c> TrueType Collection header from
    ///     <paramref name="data"/>, returning every face's SFNT offset table start position.
    /// </summary>
    /// <param name="data">The complete, in-memory font file contents.</param>
    /// <param name="faceOffsets">
    ///     Every face's SFNT offset table start position within <paramref name="data"/>, in
    ///     declaration order, if this is a <c>ttcf</c> container; otherwise an empty list.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="data"/> begins with the <c>ttcf</c> tag;
    ///     otherwise, <see langword="false"/> (an ordinary, non-collection SFNT file, which this
    ///     method does not otherwise validate).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="data"/> begins with the <c>ttcf</c> tag but is too short to
    ///     contain the fixed 12-byte header prefix or its declared face offset table, or declares
    ///     zero faces.
    /// </exception>
    /// <remarks>
    ///     Deliberately lightweight: only the fixed header prefix and the face offset table are
    ///     read here - no face's own SFNT offset table or table directory is parsed. Callers parse
    ///     an individual face on demand via <see cref="Parse(byte[], int)"/>.
    /// </remarks>
    public static bool TryReadTtcHeader(byte[] data, out IReadOnlyList<int> faceOffsets)
    {
        faceOffsets = [];
        if (data.Length < 4 || ReadUInt32(data, 0) != TtcTag)
        {
            return false;
        }

        if (data.Length < TtcHeaderPrefixSize)
        {
            throw new InvalidDataException("Font data is too short to contain a 'ttcf' header.");
        }

        var numFonts = ReadUInt32(data, 8);
        if (numFonts == 0)
        {
            throw new InvalidDataException("A 'ttcf' container must declare at least one font.");
        }

        var directoryEnd = checked((long)TtcHeaderPrefixSize + (long)numFonts * 4);
        if (directoryEnd > data.Length)
        {
            throw new InvalidDataException("Font data is too short to contain its declared 'ttcf' face offset table.");
        }

        var offsets = new int[numFonts];
        for (var i = 0; i < numFonts; i++)
        {
            var offset = ReadUInt32(data, TtcHeaderPrefixSize + i * 4);
            if (offset > data.Length)
            {
                throw new InvalidDataException("A 'ttcf' face offset exceeds the font data bounds.");
            }

            offsets[i] = (int)offset;
        }

        faceOffsets = offsets;
        return true;
    }

    /// <summary>
    ///     Attempts to look up a table's byte range by tag.
    /// </summary>
    /// <param name="tag">The 4-character table tag, for example <c>"glyf"</c>.</param>
    /// <param name="range">The table's offset and length within the font data, if found.</param>
    /// <returns><see langword="true"/> if the table is present; otherwise, <see langword="false"/>.</returns>
    public bool TryGetTable(string tag, out (int Offset, int Length) range) => _tables.TryGetValue(tag, out range);

    /// <summary>
    ///     Looks up a required table's byte range by tag, throwing if it is absent.
    /// </summary>
    /// <param name="tag">The 4-character table tag, for example <c>"glyf"</c>.</param>
    /// <returns>The table's offset and length within the font data.</returns>
    /// <exception cref="InvalidDataException">Thrown when the table is not present.</exception>
    public (int Offset, int Length) RequireTable(string tag)
    {
        if (!TryGetTable(tag, out var range))
        {
            throw new InvalidDataException($"Required table '{tag}' is missing from the font.");
        }

        return range;
    }

    /// <summary>
    ///     Reads a 4-character ASCII table tag at the given offset.
    /// </summary>
    private static string ReadTag(byte[] data, int offset) =>
        string.Create(4, (data, offset), static (span, state) =>
        {
            for (var i = 0; i < 4; i++)
            {
                span[i] = (char)state.data[state.offset + i];
            }
        });

    /// <summary>
    ///     Reads a big-endian, unsigned 16-bit integer at the given offset.
    /// </summary>
    public static ushort ReadUInt16(byte[] data, int offset) =>
        (ushort)((data[offset] << 8) | data[offset + 1]);

    /// <summary>
    ///     Reads a big-endian, signed 16-bit integer at the given offset.
    /// </summary>
    public static short ReadInt16(byte[] data, int offset) => (short)ReadUInt16(data, offset);

    /// <summary>
    ///     Reads a big-endian, unsigned 32-bit integer at the given offset.
    /// </summary>
    public static uint ReadUInt32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) |
        ((uint)data[offset + 1] << 16) |
        ((uint)data[offset + 2] << 8) |
        data[offset + 3];

    /// <summary>
    ///     Reads a big-endian, signed 32-bit integer at the given offset.
    /// </summary>
    public static int ReadInt32(byte[] data, int offset) => (int)ReadUInt32(data, offset);

    /// <summary>
    ///     Reads a big-endian 2.14 fixed-point value (<c>F2Dot14</c>) at the given offset, as used
    ///     by composite glyph component transforms.
    /// </summary>
    public static float ReadF2Dot14(byte[] data, int offset) => ReadInt16(data, offset) / 16384f;
}
