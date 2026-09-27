using System.IO.Compression;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class TiffCodec
{
    /// <summary>
    ///     Computes the Adler-32 checksum of a byte sequence, as used by the zlib stream format
    ///     wrapping Deflate-compressed TIFF strip data.
    /// </summary>
    private static uint ComputeAdler32(ReadOnlySpan<byte> data)
    {
        var a = 1u;
        var b = 0u;
        foreach (var value in data)
        {
            a = (a + value) % AdlerModulus;
            b = (b + a) % AdlerModulus;
        }

        return (b << 16) | a;
    }

    /// <summary>
    ///     Compresses raw bytes into a complete zlib stream: a 2-byte zlib header,
    ///     DEFLATE-compressed data, and a 4-byte big-endian Adler-32 trailer.
    /// </summary>
    private static byte[] ZlibCompress(byte[] rawData)
    {
        byte[] deflateData;
        using (var output = new MemoryStream())
        {
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
            {
                deflate.Write(rawData, 0, rawData.Length);
            }

            deflateData = output.ToArray();
        }

        var adler = ComputeAdler32(rawData);
        var result = new byte[2 + deflateData.Length + 4];
        result[0] = 0x78;
        result[1] = 0x9C;
        deflateData.CopyTo(result, 2);
        WriteUInt32Be(result, result.Length - 4, adler);
        return result;
    }

    /// <summary>
    ///     Decompresses a complete zlib stream (2-byte header, DEFLATE-compressed data, 4-byte
    ///     big-endian Adler-32 trailer) into its raw bytes.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the data is too short to be a valid zlib stream, the zlib header's
    ///     compression method is not DEFLATE, the header's FCHECK bits are invalid, the header
    ///     declares a preset dictionary (unsupported), or the decompressed data's Adler-32
    ///     checksum does not match the trailer.
    /// </exception>
    private static byte[] ZlibDecompress(byte[] zlibData)
    {
        if (zlibData.Length < 6)
        {
            throw new InvalidDataException("Zlib stream is too short to be valid.");
        }

        var cmf = zlibData[0];
        var flg = zlibData[1];
        if ((cmf & 0x0F) != 8)
        {
            throw new InvalidDataException(
                $"Unsupported zlib compression method {cmf & 0x0F}; only DEFLATE (8) is supported.");
        }

        if (((cmf << 8) | flg) % 31 != 0)
        {
            throw new InvalidDataException("Invalid zlib header (FCHECK validation failed).");
        }

        if ((flg & 0x20) != 0)
        {
            throw new InvalidDataException("Zlib preset dictionaries are not supported.");
        }

        var deflateData = zlibData.AsSpan(2, zlibData.Length - 2 - 4).ToArray();
        var expectedAdler = ReadUInt32Be(zlibData, zlibData.Length - 4);

        byte[] decompressed;
        using (var input = new MemoryStream(deflateData))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            deflate.CopyTo(output);
            decompressed = output.ToArray();
        }

        var actualAdler = ComputeAdler32(decompressed);
        if (actualAdler != expectedAdler)
        {
            throw new InvalidDataException("Zlib Adler-32 checksum mismatch (corrupt TIFF Deflate data).");
        }

        return decompressed;
    }

    /// <summary>
    ///     Reads an unsigned 16-bit integer from a byte buffer at the given offset, using the
    ///     specified byte order.
    /// </summary>
    private static ushort ReadUInt16(byte[] buffer, int offset, bool bigEndian) =>
        bigEndian
            ? (ushort)((buffer[offset] << 8) | buffer[offset + 1])
            : (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    /// <summary>
    ///     Reads an unsigned 32-bit integer from a byte buffer at the given offset, using the
    ///     specified byte order.
    /// </summary>
    private static uint ReadUInt32(byte[] buffer, int offset, bool bigEndian) =>
        bigEndian
            ? ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3]
            : buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);

    /// <summary>
    ///     Reads a big-endian, unsigned 32-bit integer from a byte buffer at the given offset (used
    ///     only by the zlib wrapper, which is always big-endian regardless of the TIFF file's own
    ///     byte order).
    /// </summary>
    private static uint ReadUInt32Be(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];

    /// <summary>
    ///     Writes a big-endian, unsigned 32-bit integer into a byte buffer at the given offset
    ///     (used only by the zlib wrapper, which is always big-endian).
    /// </summary>
    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>
    ///     Writes a little-endian, unsigned 16-bit integer into a byte buffer at the given offset
    ///     (used only by <see cref="Save(Surface, Stream, TiffCompression)"/>, which always writes
    ///     little-endian files).
    /// </summary>
    private static void WriteUInt16Le(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    /// <summary>
    ///     Writes a little-endian, unsigned 32-bit integer into a byte buffer at the given offset
    ///     (used only by <see cref="Save(Surface, Stream, TiffCompression)"/>, which always writes
    ///     little-endian files).
    /// </summary>
    private static void WriteUInt32Le(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    /// <summary>
    ///     Verifies that a byte range lies fully within a buffer, throwing
    ///     <see cref="InvalidDataException"/> otherwise (indicating a truncated stream or an
    ///     invalid offset stored in the file).
    /// </summary>
    private static void CheckBounds(byte[] file, int offset, int length, string what)
    {
        if (offset < 0 || length < 0 || offset + (long)length > file.Length)
        {
            throw new InvalidDataException($"Unexpected end of TIFF data while reading {what}.");
        }
    }

    /// <summary>
    ///     Reads an entire stream into a newly allocated byte array. TIFF's Image File Directory
    ///     offset, and any tag value not fitting inline, may point anywhere in the file, so parsing
    ///     requires random access to the whole file rather than the sequential reads used by
    ///     <see cref="BmpCodec"/> and <see cref="PngCodec"/>.
    /// </summary>
    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
