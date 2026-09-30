// cspell:ignore eexec sniff
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Reads the classic MS-DOS/Windows "PFB" binary framing of a Type 1 font program - a
///     sequence of <c>0x80</c>-prefixed segment headers, each declaring a segment type and a
///     little-endian 32-bit payload length - and reassembles the underlying cleartext and
///     <c>eexec</c>-encrypted byte regions that <see cref="Type1Table.Parse"/> expects.
/// </summary>
/// <remarks>
///     Each segment header is exactly six bytes: <c>0x80</c>, a one-byte segment type
///     (<c>0x01</c> = ASCII/cleartext, <c>0x02</c> = binary/encrypted, <c>0x03</c> = end of
///     file - no payload follows type <c>0x03</c>), then a 4-byte little-endian payload length.
///     Real-world PFB files conventionally contain exactly three segments (cleartext, encrypted,
///     trailer cleartext) but this reader does not assume that: it loops generically over
///     however many segments are present, concatenating every <c>0x01</c> segment seen before the
///     first <c>0x02</c> segment into the cleartext region, every <c>0x02</c> segment into the
///     encrypted region (also generically, in case more than one is present), and discarding any
///     <c>0x01</c> segment seen after the first <c>0x02</c> segment (the conventional trailing
///     "512 zeros + cleartomark" footer, irrelevant to glyph decoding).
/// </remarks>
internal static class Type1PfbReader
{
    private const byte SegmentMarker = 0x80;
    private const byte TypeAscii = 0x01;
    private const byte TypeBinary = 0x02;
    private const byte TypeEndOfFile = 0x03;
    private const int HeaderSize = 6;

    /// <summary>
    ///     Determines whether <paramref name="data"/> begins with a PFB segment header, without
    ///     fully parsing the file.
    /// </summary>
    /// <param name="data">The candidate font file bytes.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> looks like a PFB file.</returns>
    public static bool TrySniff(byte[] data) =>
        data.Length >= 2 && data[0] == SegmentMarker && data[1] is TypeAscii or TypeBinary;

    /// <summary>
    ///     Reassembles a PFB file's cleartext and encrypted regions into the single contiguous
    ///     byte layout - <c>length1</c> cleartext bytes immediately followed by <c>length2</c>
    ///     encrypted bytes - that <see cref="Type1Table.Parse"/> expects.
    /// </summary>
    /// <param name="data">The complete PFB file bytes.</param>
    /// <returns>
    ///     The reassembled font file bytes, along with the cleartext and encrypted region
    ///     lengths.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a segment header or its declared payload is truncated, an unrecognized
    ///     segment type byte is encountered, or the file contains no <c>0x02</c> (binary/encrypted)
    ///     segment.
    /// </exception>
    public static (byte[] FontFileBytes, int Length1, int Length2) Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var cleartext = new List<byte>();
        var encrypted = new List<byte>();
        var sawBinarySegment = false;

        var pos = 0;
        while (true)
        {
            if (pos >= data.Length)
            {
                throw new InvalidDataException("Type 1 PFB file is missing its terminating end-of-file segment.");
            }

            if (data[pos] != SegmentMarker)
            {
                throw new InvalidDataException("Type 1 PFB file segment header does not start with the expected 0x80 marker byte.");
            }

            if (pos + 1 >= data.Length)
            {
                throw new InvalidDataException("Type 1 PFB file segment header is truncated.");
            }

            var segmentType = data[pos + 1];
            if (segmentType == TypeEndOfFile)
            {
                break;
            }

            if (segmentType != TypeAscii && segmentType != TypeBinary)
            {
                throw new InvalidDataException(
                    $"Type 1 PFB file contains an unrecognized segment type byte (0x{segmentType:X2}).");
            }

            if (pos + HeaderSize > data.Length)
            {
                throw new InvalidDataException("Type 1 PFB file segment header is truncated.");
            }

            var length = ReadLengthLittleEndian(data, pos + 2);
            var payloadStart = pos + HeaderSize;
            if (length < 0 || checked((long)payloadStart + length) > data.Length)
            {
                throw new InvalidDataException("Type 1 PFB file segment payload is truncated.");
            }

            if (segmentType == TypeAscii)
            {
                if (!sawBinarySegment)
                {
                    cleartext.AddRange(new ArraySegment<byte>(data, payloadStart, length));
                }

                // An ASCII segment seen after the first binary segment is the conventional
                // trailer (padding zeros + 'cleartomark') and is intentionally discarded.
            }
            else
            {
                sawBinarySegment = true;
                encrypted.AddRange(new ArraySegment<byte>(data, payloadStart, length));
            }

            pos = payloadStart + length;
        }

        if (!sawBinarySegment)
        {
            throw new InvalidDataException("Type 1 PFB file contains no binary (encrypted) segment.");
        }

        var fontFileBytes = new byte[cleartext.Count + encrypted.Count];
        cleartext.CopyTo(fontFileBytes, 0);
        encrypted.CopyTo(fontFileBytes, cleartext.Count);

        return (fontFileBytes, cleartext.Count, encrypted.Count);
    }

    private static int ReadLengthLittleEndian(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
}
