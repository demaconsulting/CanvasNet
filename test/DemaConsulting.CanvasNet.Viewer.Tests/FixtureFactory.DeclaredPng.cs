using System.Buffers.Binary;

namespace DemaConsulting.CanvasNet.TestSupport;

/// <summary>
///     Fixture helpers for pictures that only declare a size.
/// </summary>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     Writes a PNG file that only declares a size: the signature and a valid IHDR chunk (33 bytes), with no pixel
    ///     data. A reader that checks the header refuses it without decoding, and decoding it would fail, so a test can
    ///     prove that a huge picture is never decoded.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="width">The declared width.</param>
    /// <param name="height">The declared height.</param>
    public static void WriteDeclaredPng(string path, int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        var chunk = bytes.AsSpan(8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk, 13);
        "IHDR"u8.CopyTo(chunk[4..]);
        BinaryPrimitives.WriteInt32BigEndian(chunk[8..], width);
        BinaryPrimitives.WriteInt32BigEndian(chunk[12..], height);
        chunk[16] = 8;
        chunk[17] = 6;
        var crc = 0xFFFFFFFFu;
        foreach (var b in chunk.Slice(4, 17))
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(chunk[21..], ~crc);
        File.WriteAllBytes(path, bytes);
    }
}
