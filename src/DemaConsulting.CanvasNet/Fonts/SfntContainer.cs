namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses the SFNT offset table and table directory shared by TrueType (<c>glyf</c>-based) and
///     OpenType (CFF-based) font files, and hosts the shared big-endian primitive readers used
///     throughout the <see cref="Fonts"/> namespace.
/// </summary>
/// <remarks>
///     <para>
///     Only the TrueType outline flavor is accepted: <c>sfntVersion</c> must be
///     <c>0x00010000</c> or the classic Mac <c>'true'</c> tag; the CFF/OpenType tag
///     <c>'OTTO'</c> is explicitly rejected with <see cref="InvalidDataException"/>, since CFF
///     outlines are out of scope for this namespace.
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
    ///     The CFF/OpenType <c>sfntVersion</c> tag ('OTTO'), explicitly rejected since CFF
    ///     outlines are out of scope for this namespace.
    /// </summary>
    private const uint OttoVersion = 0x4F54544F;

    /// <summary>
    ///     The size, in bytes, of the 12-byte SFNT offset table.
    /// </summary>
    private const int OffsetTableSize = 12;

    /// <summary>
    ///     The size, in bytes, of a single table directory entry.
    /// </summary>
    private const int TableDirectoryEntrySize = 16;

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
    ///     Parses the SFNT offset table and table directory from <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The complete, in-memory font file contents.</param>
    /// <returns>A new <see cref="SfntContainer"/> describing every table's byte range.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="data"/> is too short to contain a 12-byte offset table or
    ///     its declared table directory, when <c>sfntVersion</c> is <c>'OTTO'</c> (CFF/OpenType)
    ///     or any other value not recognized as TrueType, or when any table directory entry's
    ///     <c>offset</c>/<c>length</c> falls outside the bounds of <paramref name="data"/>.
    /// </exception>
    public static SfntContainer Parse(byte[] data)
    {
        if (data.Length < OffsetTableSize)
        {
            throw new InvalidDataException("Font data is too short to contain an SFNT offset table.");
        }

        var version = ReadUInt32(data, 0);
        if (version == OttoVersion)
        {
            throw new InvalidDataException(
                "Font uses CFF/OpenType ('OTTO') outlines, which are not supported; only TrueType ('glyf') outlines are supported.");
        }

        if (version != TrueTypeVersion && version != MacTrueVersion)
        {
            throw new InvalidDataException($"Unrecognized SFNT version 0x{version:X8}.");
        }

        var numTables = ReadUInt16(data, 4);
        var directoryEnd = checked((long)OffsetTableSize + (long)numTables * TableDirectoryEntrySize);
        if (directoryEnd > data.Length)
        {
            throw new InvalidDataException("Font data is too short to contain its declared table directory.");
        }

        var tables = new Dictionary<string, (int Offset, int Length)>(numTables, StringComparer.Ordinal);
        for (var i = 0; i < numTables; i++)
        {
            var entryOffset = OffsetTableSize + i * TableDirectoryEntrySize;
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

        return new SfntContainer(tables);
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
