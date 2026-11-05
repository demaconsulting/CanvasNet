using System.Buffers.Binary;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Builds files that only <i>declare</i> a picture size, to prove a huge picture is refused from its header and never
///     decoded: such a file is a few dozen bytes, and decoding it would fail (there is no pixel data), so an attempt to
///     decode is visible as an exception and as a large allocation.
/// </summary>
internal static class PngHeaderFixtures
{
    /// <summary>The signature of a PNG file.</summary>
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Creates a PNG file that consists of the signature and a valid IHDR chunk that declares a size.
    /// </summary>
    /// <param name="width">The declared width.</param>
    /// <param name="height">The declared height.</param>
    /// <returns>The bytes: 33, with no pixel data.</returns>
    public static byte[] Declaring(int width, int height)
    {
        var bytes = new byte[Signature.Length + 25];
        Signature.CopyTo(bytes, 0);
        var chunk = bytes.AsSpan(Signature.Length);
        BinaryPrimitives.WriteUInt32BigEndian(chunk, 13);
        "IHDR"u8.CopyTo(chunk[4..]);
        BinaryPrimitives.WriteInt32BigEndian(chunk[8..], width);
        BinaryPrimitives.WriteInt32BigEndian(chunk[12..], height);
        chunk[16] = 8; // bit depth
        chunk[17] = 6; // truecolor with alpha
        BinaryPrimitives.WriteUInt32BigEndian(chunk[21..], Crc32(chunk.Slice(4, 17)));
        return bytes;
    }

    /// <summary>
    ///     Computes the CRC-32 of a PNG chunk (its type and data).
    /// </summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The checksum.</returns>
    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
