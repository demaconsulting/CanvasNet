// cspell:ignore exif jpeg transversed transverse
using System.Buffers.Binary;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Reads the EXIF orientation of a JPEG file and applies an orientation to pixels.
/// </summary>
/// <remarks>
///     <para>
///         A camera stores the pixels as the sensor delivered them and records in the EXIF <c>Orientation</c> tag
///         (<c>0x0112</c>) how the image has to be turned to be viewed upright; the JPEG codec ignores the tag.
///         The values are the eight combinations of rotation and mirroring defined by the EXIF specification:
///         <c>1</c> upright, <c>2</c> mirrored left to right, <c>3</c> rotated 180 degrees, <c>4</c> mirrored top to
///         bottom, <c>5</c> transposed (mirrored about the main diagonal), <c>6</c> to be rotated 90 degrees
///         clockwise, <c>7</c> transversed (mirrored about the anti-diagonal), and <c>8</c> to be rotated 90
///         degrees counter-clockwise.
///     </para>
///     <para>
///         <see cref="Read(Stream)"/> never throws for malformed data: a file without a usable orientation simply
///         has none. It reads only the marker segments in front of the image data.
///     </para>
/// </remarks>
internal static class ExifOrientation
{
    /// <summary>
    ///     The most marker segments examined before giving up on finding the EXIF segment.
    /// </summary>
    private const int MaxSegments = 64;

    /// <summary>
    ///     The JPEG start-of-image marker, the second byte of the signature.
    /// </summary>
    private const byte MarkerSoi = 0xD8;

    /// <summary>
    ///     The JPEG end-of-image marker.
    /// </summary>
    private const byte MarkerEoi = 0xD9;

    /// <summary>
    ///     The JPEG start-of-scan marker, after which no more header segments follow.
    /// </summary>
    private const byte MarkerSos = 0xDA;

    /// <summary>
    ///     The JPEG application-1 marker, which carries the EXIF data.
    /// </summary>
    private const byte MarkerApp1 = 0xE1;

    /// <summary>
    ///     The size of the six-byte <c>Exif\0\0</c> identifier that starts an EXIF segment.
    /// </summary>
    private const int ExifHeaderLength = 6;

    /// <summary>
    ///     The size of one IFD entry in bytes.
    /// </summary>
    private const int EntryLength = 12;

    /// <summary>
    ///     The tag number of the orientation entry.
    /// </summary>
    private const int OrientationTag = 0x0112;

    /// <summary>
    ///     The TIFF field type SHORT, the type of the orientation entry.
    /// </summary>
    private const int TypeShort = 3;

    /// <summary>
    ///     Reads the EXIF orientation from a JPEG stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the JPEG data.</param>
    /// <returns>The orientation, 1 to 8, or <see langword="null"/> when the data has none or is not a JPEG.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    public static int? Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Span<byte> pair = stackalloc byte[2];
        if (!TryFill(stream, pair) || pair[0] != 0xFF || pair[1] != MarkerSoi)
        {
            return null;
        }

        for (var segment = 0; segment < MaxSegments; segment++)
        {
            var marker = ReadMarker(stream);
            if (marker is null or MarkerEoi or MarkerSos)
            {
                return null;
            }

            // Markers without a length: TEM (0x01) and the restart markers (0xD0-0xD7)
            if (marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            if (!TryFill(stream, pair))
            {
                return null;
            }

            var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(pair) - 2;
            if (payloadLength < 0)
            {
                return null;
            }

            if (marker != MarkerApp1 || payloadLength < ExifHeaderLength)
            {
                if (!TrySkip(stream, payloadLength))
                {
                    return null;
                }

                continue;
            }

            var payload = new byte[payloadLength];
            if (!TryFill(stream, payload))
            {
                return null;
            }

            // An APP1 segment that is not EXIF (for example XMP) is skipped; the EXIF one is the answer either way
            if (payload.AsSpan(0, ExifHeaderLength).SequenceEqual("Exif\0\0"u8))
            {
                return ParseTiff(payload.AsSpan(ExifHeaderLength));
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the EXIF orientation from a JPEG file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The orientation, 1 to 8, or <see langword="null"/> when the file has none or is not a JPEG.</returns>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    public static int? Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Read(stream);
    }

    /// <summary>
    ///     Applies an EXIF orientation to a surface, producing the upright image.
    /// </summary>
    /// <param name="source">The surface as stored; it is not modified or disposed.</param>
    /// <param name="orientation">The orientation, 1 to 8; <c>1</c> returns an independent copy.</param>
    /// <returns>A new surface; for the orientations 5 to 8 its width and height are swapped.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="orientation"/> is not 1 to 8.</exception>
    public static Surface Apply(Surface source, int orientation)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(orientation, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(orientation, 8);

        var width = source.Width;
        var height = source.Height;
        var swap = orientation >= 5;
        var result = new Surface(swap ? height : width, swap ? width : height);

        // Visit every pixel of the result and fetch the source pixel it shows
        for (var y = 0; y < result.Height; y++)
        {
            var row = result.GetRowSpan(y);
            for (var x = 0; x < result.Width; x++)
            {
                var (sx, sy) = orientation switch
                {
                    1 => (x, y),
                    2 => (width - 1 - x, y),
                    3 => (width - 1 - x, height - 1 - y),
                    4 => (x, height - 1 - y),
                    5 => (y, x),
                    6 => (y, height - 1 - x),
                    7 => (width - 1 - y, height - 1 - x),
                    _ => (width - 1 - y, x)
                };

                row[x] = source[sx, sy];
            }
        }

        return result;
    }

    /// <summary>
    ///     Finds the orientation in the TIFF structure that an EXIF segment contains.
    /// </summary>
    /// <param name="tiff">The TIFF data: the byte-order mark, the header, and the directories.</param>
    /// <returns>The orientation, or <see langword="null"/> when there is none or the structure is malformed.</returns>
    private static int? ParseTiff(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return null;
        }

        bool littleEndian;
        if (tiff[0] == 'I' && tiff[1] == 'I')
        {
            littleEndian = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M')
        {
            littleEndian = false;
        }
        else
        {
            return null;
        }

        if (ReadUInt16(tiff, 2, littleEndian) != 42)
        {
            return null;
        }

        // The first directory (the image's own tags) holds the orientation
        var directory = (long)ReadUInt32(tiff, 4, littleEndian);
        if (directory + 2 > tiff.Length)
        {
            return null;
        }

        var count = ReadUInt16(tiff, (int)directory, littleEndian);
        for (var i = 0; i < count; i++)
        {
            var entry = directory + 2 + (i * (long)EntryLength);
            if (entry + EntryLength > tiff.Length)
            {
                return null;
            }

            if (ReadUInt16(tiff, (int)entry, littleEndian) != OrientationTag)
            {
                continue;
            }

            // A one-element SHORT is stored in the first two bytes of the value field
            if (ReadUInt16(tiff, (int)entry + 2, littleEndian) != TypeShort || ReadUInt32(tiff, (int)entry + 4, littleEndian) != 1)
            {
                return null;
            }

            var value = ReadUInt16(tiff, (int)entry + 8, littleEndian);
            return value is >= 1 and <= 8 ? value : null;
        }

        return null;
    }

    /// <summary>
    ///     Reads a 16-bit unsigned integer.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>The value.</returns>
    private static int ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
    {
        var slice = data.Slice(offset, 2);
        return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(slice) : BinaryPrimitives.ReadUInt16BigEndian(slice);
    }

    /// <summary>
    ///     Reads a 32-bit unsigned integer.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    /// <returns>The value.</returns>
    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian)
    {
        var slice = data.Slice(offset, 4);
        return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(slice) : BinaryPrimitives.ReadUInt32BigEndian(slice);
    }

    /// <summary>
    ///     Reads the next marker: skips to the next <c>0xFF</c> byte and over any fill bytes.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The marker code, or <see langword="null"/> at the end of the data.</returns>
    private static byte? ReadMarker(Stream stream)
    {
        var next = stream.ReadByte();
        if (next != 0xFF)
        {
            return null;
        }

        // Any number of 0xFF fill bytes may precede the marker code
        do
        {
            next = stream.ReadByte();
        }
        while (next == 0xFF);

        return next < 0 ? null : (byte)next;
    }

    /// <summary>
    ///     Reads exactly as many bytes as the buffer holds.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns><see langword="true"/> when the buffer was filled; <see langword="false"/> at the end of the data.</returns>
    private static bool TryFill(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }

    /// <summary>
    ///     Skips bytes of the stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="count">The number of bytes to skip.</param>
    /// <returns><see langword="true"/> when the bytes were skipped; <see langword="false"/> at the end of the data.</returns>
    private static bool TrySkip(Stream stream, int count)
    {
        if (stream.CanSeek)
        {
            if (stream.Position + count > stream.Length)
            {
                return false;
            }

            stream.Seek(count, SeekOrigin.Current);
            return true;
        }

        Span<byte> scratch = stackalloc byte[256];
        while (count > 0)
        {
            var read = stream.Read(scratch[..Math.Min(count, scratch.Length)]);
            if (read == 0)
            {
                return false;
            }

            count -= read;
        }

        return true;
    }
}
