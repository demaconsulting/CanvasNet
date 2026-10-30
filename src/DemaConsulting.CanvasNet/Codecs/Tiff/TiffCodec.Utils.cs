using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class TiffCodec
{
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
