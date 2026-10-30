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
///     declared dimensions can occupy, so <see cref="Decompress(byte[], long, string)"/> takes that figure as a mandatory
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
    ///     The smallest initial output-buffer size, in bytes, used by <see cref="Decompress(byte[], long, string)"/>.
    /// </summary>
    private const int InitialBufferBytes = 64 * 1024;

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
    ///     attacker-controlled length field. The output buffer is grown on demand, in proportion to
    ///     the output actually produced, and never beyond this limit: it starts at
    ///     <c>min(maxOutputBytes, max(64 KiB, 4 * zlibData.Length))</c> bytes and doubles (capped
    ///     at <paramref name="maxOutputBytes"/>) only when the inflater has filled it and more
    ///     output follows. The stream is inflated directly into the free tail of the buffer; at
    ///     most one byte beyond the limit is ever read, to prove excess. Consequently a truncated or
    ///     malformed stream allocates only in proportion to what it really inflates, while the
    ///     total output-buffer allocation (every buffer ever allocated, including the superseded
    ///     ones) is at most 2 x the final capacity, itself at most <paramref name="maxOutputBytes"/>
    ///     and less than 2 x the actual output (unless the initial size or the limit applies); the
    ///     live peak is at most 1.5 x the final capacity (old plus new buffer while growing). There
    ///     is no <see cref="MemoryStream"/> growth and no final copy; the inflater's own working
    ///     memory is fixed and size-independent. Output of exactly this size is accepted; one byte
    ///     more is rejected.
    /// </param>
    /// <param name="formatName">
    ///     A short format description (for example <c>PNG</c> or <c>TIFF Deflate</c>) used in error
    ///     messages.
    /// </param>
    /// <returns>
    ///     A view of the decompressed bytes, no more than <paramref name="maxOutputBytes"/> long. It
    ///     is a slice over the final output buffer, so a stream that legally inflates to fewer bytes
    ///     than the buffer's capacity yields a shorter view without any further copy.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the data is too short to be a valid zlib stream, the zlib header's
    ///     compression method is not DEFLATE, the header's FCHECK bits are invalid, the header
    ///     declares a preset dictionary (unsupported), the DEFLATE payload is corrupt, the
    ///     decompressed output exceeds <paramref name="maxOutputBytes"/> (a decompression bomb or
    ///     over-long data), <paramref name="maxOutputBytes"/> is negative or larger than the
    ///     maximum .NET array length, or the decompressed data's Adler-32 checksum does not match
    ///     the trailer.
    /// </exception>
    internal static Memory<byte> Decompress(byte[] zlibData, long maxOutputBytes, string formatName) =>
        Decompress(zlibData, maxOutputBytes, formatName, out _);

    /// <summary>
    ///     Decompresses a zlib stream exactly like <see cref="Decompress(byte[], long, string)"/> and
    ///     additionally reports how many bytes of output buffer it allocated (a test hook that makes
    ///     the documented allocation bound observable independently of the runtime's inflater
    ///     internals).
    /// </summary>
    /// <param name="zlibData">The complete zlib-wrapped byte sequence to decompress.</param>
    /// <param name="maxOutputBytes">The hard upper limit on the decompressed size, in bytes.</param>
    /// <param name="formatName">A short format description used in error messages.</param>
    /// <param name="outputBytesAllocated">
    ///     The cumulative size of every output buffer this call allocated (initial buffer plus each
    ///     growth step). It is assigned progressively, so it is valid even when the call throws.
    /// </param>
    /// <returns>A view of the decompressed bytes.</returns>
    /// <exception cref="InvalidDataException">See <see cref="Decompress(byte[], long, string)"/>.</exception>
    internal static Memory<byte> Decompress(
        byte[] zlibData,
        long maxOutputBytes,
        string formatName,
        out long outputBytesAllocated)
    {
        outputBytesAllocated = 0;
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

        // The buffer starts small and grows only when the inflater has really filled it, so the
        // allocation tracks the output actually produced (a tiny truncated stream on a huge
        // declared image never forces the full limit), and it never exceeds the caller's limit.
        var capacity = (int)Math.Min(maxOutputBytes, Math.Max(InitialBufferBytes, (long)zlibData.Length * 4));
        var buffer = new byte[capacity];
        outputBytesAllocated = capacity;
        var total = 0;
        using (var input = new MemoryStream(zlibData, 2, zlibData.Length - 2 - 4, false))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        {
            Span<byte> probe = stackalloc byte[1];
            while (true)
            {
                if (total < buffer.Length)
                {
                    var bytesRead = deflate.Read(buffer, total, buffer.Length - total);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    total += bytesRead;
                    continue;
                }

                // The buffer is full: a single further byte tells whether the stream ends here,
                // exceeds the limit, or needs a larger buffer, without storing any excess.
                if (deflate.Read(probe) == 0)
                {
                    break;
                }

                if (buffer.Length == maxOutputBytes)
                {
                    throw new InvalidDataException(
                        $"Decompressed {formatName} data exceeds the expected size of {maxOutputBytes} bytes " +
                        "(corrupt image data or a decompression bomb).");
                }

                capacity = (int)Math.Min(maxOutputBytes, (long)buffer.Length * 2);
                Array.Resize(ref buffer, capacity);
                outputBytesAllocated += capacity;
                buffer[total++] = probe[0];
            }
        }
        var decompressed = new Memory<byte>(buffer, 0, total);
        if (ComputeAdler32(decompressed.Span) != expectedAdler)
        {
            throw new InvalidDataException($"Zlib Adler-32 checksum mismatch (corrupt {formatName} data).");
        }

        return decompressed;
    }
}
