// cspell:ignore canvasnet jpeg tiff tif exif gif ihdr scad
using System.Buffers.Binary;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Creates the original test inputs: small rasters generated in memory with the CanvasNet codecs, byte
///     sequences that are valid or deliberately broken, and the paths of the fixture files.
/// </summary>
/// <remarks>
///     The fixture factory writes files only into directories the caller supplies, so every test cleans up by
///     deleting its own <see cref="TempDirectory"/>.
/// </remarks>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     The width of the generated test images.
    /// </summary>
    public const int Width = 12;

    /// <summary>
    ///     The height of the generated test images.
    /// </summary>
    public const int Height = 8;

    /// <summary>
    ///     The keys of the source kinds that <see cref="WriteSource"/> can create, with the extension of each.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Extension)> Sources =
    [
        ("png", ".png"),
        ("jpeg", ".jpg"),
        ("bmp", ".bmp"),
        ("tiff", ".tif"),
        ("gif", ".gif"),
        ("jpeg2000", ".jp2"),
        ("svg", ".svg"),
        ("pdf", ".pdf"),
        ("pptx", ".pptx"),
        ("vsdx", ".vsdx"),
        ("scad", ".scad")
    ];

    /// <summary>
    ///     Gets the full path of a fixture file.
    /// </summary>
    /// <param name="name">The file name within the <c>Fixtures</c> folder.</param>
    /// <returns>The path next to the test assembly.</returns>
    public static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    }

    /// <summary>
    ///     Creates an opaque image whose pixels differ in position and in channel, so that any mirroring, rotation, or
    ///     offset changes it.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface.</returns>
    public static Surface CreateGradient(int width = Width, int height = Height)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = new Rgba32(
                    (byte)(x * 255 / Math.Max(1, width - 1)),
                    (byte)(y * 255 / Math.Max(1, height - 1)),
                    (byte)(((x * 7) + (y * 13)) % 256),
                    255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Creates an image with transparency: the opaque gradient with an alpha that falls from left to right.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface.</returns>
    public static Surface CreateTranslucent(int width = Width, int height = Height)
    {
        var surface = CreateGradient(width, height);
        for (var y = 0; y < height; y++)
        {
            var row = surface.GetRowSpan(y);
            for (var x = 0; x < width; x++)
            {
                row[x] = new Rgba32(row[x].R, row[x].G, row[x].B, (byte)(255 - (x * 255 / Math.Max(1, width - 1))));
            }
        }

        return surface;
    }

    /// <summary>
    ///     Creates an opaque image of five flat colors, which a GIF can hold exactly.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface.</returns>
    public static Surface CreatePalette(int width = Width, int height = Height)
    {
        Rgba32[] colors =
        [
            new(255, 0, 0, 255),
            new(0, 255, 0, 255),
            new(0, 0, 255, 255),
            new(255, 255, 0, 255),
            new(0, 0, 0, 255)
        ];
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = colors[(x + (3 * y)) % colors.Length];
            }
        }

        return surface;
    }

    /// <summary>
    ///     Writes a source file of one kind into a directory.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="key">The kind: one of the keys of <see cref="Sources"/>.</param>
    /// <returns>The path of the file, named <c>source</c> with the kind's extension.</returns>
    public static string WriteSource(string directory, string key)
    {
        var extension = Sources.Single(s => s.Key == key).Extension;
        var path = Path.Combine(directory, "source" + extension);
        switch (key)
        {
            case "png":
                using (var surface = CreateGradient())
                {
                    PngCodec.Save(surface, path);
                }

                break;
            case "jpeg":
                using (var surface = CreateGradient())
                {
                    JpegCodec.Save(surface, path);
                }

                break;
            case "bmp":
                using (var surface = CreateGradient())
                {
                    BmpCodec.Save(surface, path);
                }

                break;
            case "tiff":
                using (var surface = CreateGradient())
                {
                    TiffCodec.Save(surface, path);
                }

                break;
            case "gif":
                using (var surface = CreatePalette())
                {
                    File.WriteAllBytes(path, CreateGif(surface));
                }

                break;
            case "jpeg2000":
                File.Copy(FixturePath("gradient-100x70.jp2"), path);
                break;
            case "pdf":
                File.WriteAllBytes(path, CreatePdf());
                break;
            case "pptx":
                File.WriteAllBytes(path, CreatePptx());
                break;
            case "vsdx":
                File.WriteAllBytes(path, CreateVsdx());
                break;
            case "scad":
                File.Copy(FixturePath("cube.scad"), path);
                break;
            default:
                File.Copy(FixturePath("shapes.svg"), path);
                break;
        }

        return path;
    }

    /// <summary>
    ///     Encodes an image as a GIF with a plain (uncompressed in effect) LZW stream.
    /// </summary>
    /// <param name="surface">The image; it must be opaque and have at most 256 distinct colors.</param>
    /// <returns>The bytes of the GIF.</returns>
    /// <remarks>
    ///     The CanvasNet libraries have no GIF encoder, so the tests write one: every pixel is emitted as a literal
    ///     9-bit code, and a clear code is emitted every 100 pixels so that the code table never grows past 9 bits.
    /// </remarks>
    public static byte[] CreateGif(Surface surface)
    {
        var palette = new List<Rgba32>();
        var indices = new List<int>();
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                var index = palette.IndexOf(pixel);
                if (index < 0)
                {
                    index = palette.Count;
                    palette.Add(pixel);
                }

                indices.Add(index);
            }
        }

        using var stream = new MemoryStream();
        stream.Write("GIF89a"u8);
        WriteUInt16(stream, surface.Width);
        WriteUInt16(stream, surface.Height);

        // A global color table of 256 entries, 8 bits per channel
        stream.WriteByte(0xF7);
        stream.WriteByte(0);
        stream.WriteByte(0);
        for (var i = 0; i < 256; i++)
        {
            var color = i < palette.Count ? palette[i] : default;
            stream.WriteByte(color.R);
            stream.WriteByte(color.G);
            stream.WriteByte(color.B);
        }

        // The image descriptor, the minimum code size, and the data in sub-blocks of at most 255 bytes
        stream.WriteByte(0x2C);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, surface.Width);
        WriteUInt16(stream, surface.Height);
        stream.WriteByte(0);
        stream.WriteByte(8);
        var data = PackCodes(indices);
        for (var offset = 0; offset < data.Length; offset += 255)
        {
            var count = Math.Min(255, data.Length - offset);
            stream.WriteByte((byte)count);
            stream.Write(data, offset, count);
        }

        stream.WriteByte(0);
        stream.WriteByte(0x3B);
        return stream.ToArray();
    }

    /// <summary>
    ///     Inserts an EXIF segment that holds an orientation into a JPEG.
    /// </summary>
    /// <param name="jpeg">The bytes of a JPEG file.</param>
    /// <param name="orientation">The orientation, 1 to 8.</param>
    /// <param name="littleEndian"><see langword="true"/> to write the EXIF data in little-endian byte order.</param>
    /// <returns>The bytes of the JPEG with the segment right after the start-of-image marker.</returns>
    public static byte[] InsertExifOrientation(byte[] jpeg, int orientation, bool littleEndian)
    {
        var tiff = new List<byte>();
        tiff.AddRange(littleEndian ? "II"u8.ToArray() : "MM"u8.ToArray());
        AddUInt16(tiff, 42, littleEndian);
        AddUInt32(tiff, 8, littleEndian);

        // One directory entry: tag 0x0112, type SHORT, count 1, the value in the first two bytes of the value field
        AddUInt16(tiff, 1, littleEndian);
        AddUInt16(tiff, 0x0112, littleEndian);
        AddUInt16(tiff, 3, littleEndian);
        AddUInt32(tiff, 1, littleEndian);
        AddUInt16(tiff, orientation, littleEndian);
        AddUInt16(tiff, 0, littleEndian);
        AddUInt32(tiff, 0, littleEndian);

        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(tiff);

        var result = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1 };
        result.Add((byte)((payload.Count + 2) >> 8));
        result.Add((byte)((payload.Count + 2) & 0xFF));
        result.AddRange(payload);
        result.AddRange(jpeg.Skip(2));
        return [.. result];
    }

    /// <summary>
    ///     Creates the bytes of a PNG that declares a size but holds no pixel data.
    /// </summary>
    /// <param name="width">The declared width.</param>
    /// <param name="height">The declared height.</param>
    /// <returns>The signature and a header chunk only.</returns>
    public static byte[] CreatePngHeaderOnly(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;

        var type = "IHDR"u8.ToArray();
        var crc = Crc32(type.Concat(header).ToArray());
        var result = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 };
        result.AddRange(type);
        result.AddRange(header);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        result.AddRange(crcBytes);
        return [.. result];
    }

    /// <summary>
    ///     Gets deterministic pseudo-random bytes, which are not a valid file of any format.
    /// </summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    public static byte[] CreateGarbage(int length)
    {
        var bytes = new byte[length];
        new Random(12345).NextBytes(bytes);
        return bytes;
    }

    /// <summary>
    ///     Packs the LZW codes of a GIF image into bytes, least significant bit first.
    /// </summary>
    /// <param name="indices">The palette index of each pixel.</param>
    /// <returns>The packed codes: clear codes, one literal code per pixel, and the end code.</returns>
    private static byte[] PackCodes(List<int> indices)
    {
        const int clear = 256;
        const int end = 257;
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;

        void Emit(int code)
        {
            buffer |= code << bits;
            bits += 9;
            while (bits >= 8)
            {
                bytes.Add((byte)(buffer & 0xFF));
                buffer >>= 8;
                bits -= 8;
            }
        }

        for (var i = 0; i < indices.Count; i++)
        {
            if (i % 100 == 0)
            {
                Emit(clear);
            }

            Emit(indices[i]);
        }

        Emit(end);
        if (bits > 0)
        {
            bytes.Add((byte)(buffer & 0xFF));
        }

        return [.. bytes];
    }

    /// <summary>
    ///     Writes a 16-bit little-endian value.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="value">The value.</param>
    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8));
    }

    /// <summary>
    ///     Appends a 16-bit value.
    /// </summary>
    /// <param name="bytes">The list to append to.</param>
    /// <param name="value">The value.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    private static void AddUInt16(List<byte> bytes, int value, bool littleEndian)
    {
        var buffer = new byte[2];
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)value);
        }

        bytes.AddRange(buffer);
    }

    /// <summary>
    ///     Appends a 32-bit value.
    /// </summary>
    /// <param name="bytes">The list to append to.</param>
    /// <param name="value">The value.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    private static void AddUInt32(List<byte> bytes, int value, bool littleEndian)
    {
        var buffer = new byte[4];
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, (uint)value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)value);
        }

        bytes.AddRange(buffer);
    }

    /// <summary>
    ///     Computes the CRC-32 that PNG chunks carry.
    /// </summary>
    /// <param name="data">The data: the chunk type and the chunk data.</param>
    /// <returns>The checksum.</returns>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}

/// <summary>
///     A temporary directory that is deleted, with its contents, when it is disposed.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TempDirectory"/> class and creates the directory.
    /// </summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"canvasnet_tool_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    ///     Gets the full path of the directory.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the path of a file or folder inside the directory.
    /// </summary>
    /// <param name="names">The names of the folders and the file, relative to the directory.</param>
    /// <returns>The full path.</returns>
    public string Combine(params string[] names)
    {
        return System.IO.Path.Combine([Path, .. names]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary directory must not fail a test
        }
    }
}
