using System.IO.Compression;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Internal zlib (RFC 1950) wrapper shared by <see cref="PngCodec"/> and <see cref="TiffCodec"/>:
///     Adler-32 checksum computation, zlib-stream compression, and a hard-bounded zlib-stream decompression
///     that defends the decoders against "decompression bombs" (a tiny crafted compressed payload
///     that would otherwise inflate to gigabytes and exhaust memory).
/// </summary>
/// <remarks>
///     Both image decoders know exactly how many decompressed bytes a legitimate image of the
///     declared dimensions can occupy, so <see cref="Decompress"/> takes that figure as a mandatory
///     hard output limit rather than allowing unbounded expansion. This type is <c>internal</c>:
///     it is not part of the public API surface.
/// </remarks>
internal static class ZlibHelper
{
    /// <summary>
    ///     The modulus used by the Adler-32 checksum algorithm (the largest prime below 2^16).
    /// </summary>
    private const uint AdlerModulus = 65521;

    /// <summary>
    ///     The size, in bytes, of each read chunk pulled from the inflating stream. The output
    ///     total is checked after every chunk, so at most one chunk (and never more than one byte
    ///     past the limit, see <see cref="Decompress"/>) is ever produced beyond the limit.
    /// </summary>
    private const int ChunkSize = 81920;

    /// <summary>
    ///     Computes the Adler-32 checksum of a byte sequence, as used by the zlib stream format.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The 32-bit Adler-32 checksum.</returns>
    internal static uint ComputeAdler32(ReadOnlySpan<byte> data)
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
    /// <param name="rawData">The uncompressed bytes to compress.</param>
    /// <returns>The complete zlib-wrapped byte sequence.</returns>
    internal static byte[] Compress(byte[] rawData)
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
        var trailer = result.AsSpan(result.Length - 4);
        trailer[0] = (byte)(adler >> 24);
        trailer[1] = (byte)(adler >> 16);
        trailer[2] = (byte)(adler >> 8);
        trailer[3] = (byte)adler;
        return result;
    }

    /// <summary>
    ///     Decompresses a complete zlib stream (2-byte header, DEFLATE-compressed data, 4-byte
    ///     big-endian Adler-32 trailer) into its raw bytes, refusing to produce more than
    ///     <paramref name="maxOutputBytes"/> bytes of output.
    /// </summary>
    /// <param name="zlibData">The complete zlib-wrapped byte sequence to decompress.</param>
    /// <param name="maxOutputBytes">
    ///     The hard upper limit on the decompressed size, in bytes. Callers derive it from the
    ///     image's own declared dimensions (the most a legitimate image could need), never from an
    ///     attacker-controlled length field. Output is read in bounded chunks and the running total
    ///     is checked before each chunk is stored, so the memory allocated never exceeds this limit
    ///     (plus one fixed-size chunk buffer) regardless of how far the stream would expand.
    ///     Output of exactly this size is accepted; one byte more is rejected.
    /// </param>
    /// <param name="formatName">
    ///     A short format description (for example <c>PNG</c> or <c>TIFF Deflate</c>) used in error
    ///     messages.
    /// </param>
    /// <returns>The decompressed bytes, no more than <paramref name="maxOutputBytes"/> long.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the data is too short to be a valid zlib stream, the zlib header's
    ///     compression method is not DEFLATE, the header's FCHECK bits are invalid, the header
    ///     declares a preset dictionary (unsupported), the DEFLATE payload is corrupt, the
    ///     decompressed output exceeds <paramref name="maxOutputBytes"/> (a decompression bomb or
    ///     over-long data), <paramref name="maxOutputBytes"/> is negative or larger than the
    ///     maximum .NET array length, or the decompressed data's Adler-32 checksum does not match
    ///     the trailer.
    /// </exception>
    internal static byte[] Decompress(byte[] zlibData, long maxOutputBytes, string formatName)
    {
        if (maxOutputBytes < 0 || maxOutputBytes > Array.MaxLength)
        {
            throw new InvalidDataException(
                $"The expected {formatName} decompressed size {maxOutputBytes} is not representable.");
        }

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

        var expectedAdler =
            ((uint)zlibData[^4] << 24) | ((uint)zlibData[^3] << 16) | ((uint)zlibData[^2] << 8) | zlibData[^1];

        byte[] decompressed;
        using (var input = new MemoryStream(zlibData, 2, zlibData.Length - 2 - 4, false))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            var buffer = new byte[(int)Math.Min(ChunkSize, maxOutputBytes + 1)];
            long total = 0;
            int bytesRead;

            // Never request more than one byte past the limit: that single extra byte is enough
            // to prove the stream would exceed the limit, without ever materializing the excess.
            while ((bytesRead = deflate.Read(buffer, 0, (int)Math.Min(buffer.Length, maxOutputBytes - total + 1))) > 0)
            {
                total += bytesRead;
                if (total > maxOutputBytes)
                {
                    throw new InvalidDataException(
                        $"Decompressed {formatName} data exceeds the expected size of {maxOutputBytes} bytes " +
                        "(corrupt image data or a decompression bomb).");
                }

                output.Write(buffer, 0, bytesRead);
            }

            decompressed = output.ToArray();
        }

        if (ComputeAdler32(decompressed) != expectedAdler)
        {
            throw new InvalidDataException($"Zlib Adler-32 checksum mismatch (corrupt {formatName} data).");
        }

        return decompressed;
    }
}
