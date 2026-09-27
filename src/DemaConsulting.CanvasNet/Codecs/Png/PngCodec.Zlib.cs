using System.IO.Compression;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class PngCodec
{
    /// <summary>
    ///     Builds the 256-entry CRC-32 lookup table using the standard IEEE 802.3 polynomial
    ///     (0xEDB88320, reflected), as required by the PNG specification.
    /// </summary>
    /// <returns>A 256-entry lookup table for <see cref="ComputeCrc32"/>.</returns>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>
    ///     The initial (pre-first-update) running CRC-32 state, as required by the PNG/zlib
    ///     CRC-32 algorithm's bit-inversion convention. Callers that need to fold data into a
    ///     CRC-32 across multiple calls - for example <see cref="StreamDiscardChunkPayload"/>,
    ///     which folds in a chunk's type and then its payload in bounded pieces rather than in one
    ///     buffered call - start from this value and pass the running result to
    ///     <see cref="UpdateCrc32"/> for each subsequent piece, then to <see cref="FinalizeCrc32"/>
    ///     once all pieces have been folded in.
    /// </summary>
    private const uint Crc32InitialState = 0xFFFFFFFFu;

    /// <summary>
    ///     Folds a piece of data into a running CRC-32 state, without finalizing it. Splitting the
    ///     PNG/zlib CRC-32 algorithm into <see cref="Crc32InitialState"/>/<see cref="UpdateCrc32"/>/
    ///     <see cref="FinalizeCrc32"/> steps (rather than only exposing the one-shot
    ///     <see cref="ComputeCrc32"/>) lets a caller checksum data that arrives in several pieces -
    ///     for example a chunk's type bytes followed by its payload read in bounded chunks - without
    ///     ever needing to buffer all of it in one array first.
    /// </summary>
    /// <param name="crc">
    ///     The running CRC-32 state: <see cref="Crc32InitialState"/> for the first piece, or the
    ///     previous call's return value for every subsequent piece.
    /// </param>
    /// <param name="data">The next piece of data to fold into the running CRC-32 state.</param>
    /// <returns>The updated running CRC-32 state, not yet finalized.</returns>
    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    /// <summary>
    ///     Finalizes a running CRC-32 state produced by <see cref="Crc32InitialState"/> and zero
    ///     or more <see cref="UpdateCrc32"/> calls into the actual CRC-32 checksum value, applying
    ///     the algorithm's final bit-inversion step.
    /// </summary>
    /// <param name="crc">The running CRC-32 state to finalize.</param>
    /// <returns>The final 32-bit CRC checksum.</returns>
    private static uint FinalizeCrc32(uint crc) => crc ^ 0xFFFFFFFFu;

    /// <summary>
    ///     Computes the PNG/zlib CRC-32 checksum of a byte sequence already fully available in
    ///     memory. Implemented as a thin wrapper over <see cref="Crc32InitialState"/>,
    ///     <see cref="UpdateCrc32"/>, and <see cref="FinalizeCrc32"/> so every one-shot call site
    ///     (the <c>IHDR</c>/<c>PLTE</c>/<c>tRNS</c>/<c>IDAT</c>/<c>IEND</c> buffered chunk-read
    ///     path, and the <c>Save</c> path) is unaffected by the incremental steps that
    ///     <see cref="StreamDiscardChunkPayload"/> uses instead.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The 32-bit CRC checksum.</returns>
    private static uint ComputeCrc32(ReadOnlySpan<byte> data) => FinalizeCrc32(UpdateCrc32(Crc32InitialState, data));

    /// <summary>
    ///     Computes the Adler-32 checksum of a byte sequence, as used by the zlib stream format
    ///     wrapping PNG's <c>IDAT</c> payload.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The 32-bit Adler-32 checksum.</returns>
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
    ///     Compresses raw scanline bytes into a complete zlib stream: a 2-byte zlib header,
    ///     DEFLATE-compressed data, and a 4-byte big-endian Adler-32 trailer.
    /// </summary>
    /// <param name="rawData">The uncompressed scanline bytes to compress.</param>
    /// <returns>The complete zlib-wrapped byte sequence.</returns>
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
    ///     big-endian Adler-32 trailer) into its raw scanline bytes.
    /// </summary>
    /// <param name="zlibData">The complete zlib-wrapped byte sequence to decompress.</param>
    /// <returns>The decompressed raw scanline bytes.</returns>
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
            throw new InvalidDataException("Zlib Adler-32 checksum mismatch (corrupt PNG data).");
        }

        return decompressed;
    }

    /// <summary>
    ///     Reads a big-endian, unsigned 32-bit integer from a byte buffer at the given offset.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The offset of the first (most significant) byte.</param>
    /// <returns>The decoded 32-bit value.</returns>
    private static uint ReadUInt32Be(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8) |
        buffer[offset + 3];

    /// <summary>
    ///     Writes a big-endian, unsigned 32-bit integer into a byte buffer at the given offset.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="offset">The offset of the first (most significant) byte to write.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>
    ///     Reads exactly <paramref name="count"/> bytes from a stream into a newly allocated buffer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="count">The exact number of bytes required.</param>
    /// <param name="what">A short description of the data being read, used in the error message.</param>
    /// <returns>A newly allocated buffer of length <paramref name="count"/>.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="count"/> bytes could be read.
    /// </exception>
    private static byte[] ReadExactly(Stream stream, int count, string what)
    {
        var buffer = new byte[count];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                throw new InvalidDataException($"Unexpected end of stream while reading {what}.");
            }

            totalRead += read;
        }

        return buffer;
    }
}
