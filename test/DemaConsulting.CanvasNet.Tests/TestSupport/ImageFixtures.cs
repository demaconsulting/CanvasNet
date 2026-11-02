using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.TestSupport;

/// <summary>
///     Generated pictures and file bytes for the tests of the surface operations that were moved from the tool.
/// </summary>
internal static class ImageFixtures
{
    /// <summary>
    ///     Creates an opaque image whose pixels differ in position and in channel, so that any mirroring, rotation, or
    ///     offset changes it.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface.</returns>
    public static Surface CreateGradient(int width = 12, int height = 8)
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
    ///     Creates an opaque image of five flat colors.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new surface.</returns>
    public static Surface CreatePalette(int width = 12, int height = 8)
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
    ///     Creates the picture of a sheet of paper with tilted dark lines, like a photographed page of ruled notes.
    /// </summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="tiltDegrees">
    ///     The tilt of the lines in degrees; positive means the lines descend to the right (they are rotated clockwise).
    /// </param>
    /// <param name="lines">The number of lines, evenly spaced from top to bottom.</param>
    /// <param name="thickness">The thickness of a line measured vertically, in pixels.</param>
    /// <param name="paperLevel">The gray level of the paper at the middle of the picture.</param>
    /// <param name="inkContrast">How many levels darker than the paper at that place the ink is.</param>
    /// <param name="shade">
    ///     The change of the paper level from the left edge to the right edge, as uneven lighting; the ink follows the
    ///     paper so the contrast of the lines stays the same.
    /// </param>
    /// <returns>An opaque surface. The lines are drawn with plain arithmetic, independent of any rotation code.</returns>
    public static Surface SketchPhoto(
        int width,
        int height,
        double tiltDegrees,
        int lines = 6,
        int thickness = 2,
        int paperLevel = 200,
        int inkContrast = 90,
        int shade = 0)
    {
        byte PaperAt(int x)
        {
            var level = paperLevel + (int)Math.Round(shade * ((x / (double)Math.Max(1, width - 1)) - 0.5));
            return (byte)Math.Clamp(level, 0, 255);
        }

        var surface = new Surface(width, height);
        for (var x = 0; x < width; x++)
        {
            var paper = PaperAt(x);
            for (var y = 0; y < height; y++)
            {
                surface[x, y] = new Rgba32(paper, paper, paper, 255);
            }
        }

        var slope = Math.Tan(tiltDegrees * Math.PI / 180.0);
        for (var line = 1; line <= lines; line++)
        {
            var baseline = height * line / (double)(lines + 1);
            for (var x = 0; x < width; x++)
            {
                var ink = (byte)Math.Max(0, PaperAt(x) - inkContrast);
                var top = (int)Math.Round(baseline + ((x - (width / 2.0)) * slope) - (thickness / 2.0));
                for (var t = 0; t < thickness; t++)
                {
                    var y = top + t;
                    if (y >= 0 && y < height)
                    {
                        surface[x, y] = new Rgba32(ink, ink, ink, 255);
                    }
                }
            }
        }

        return surface;
    }

    /// <summary>
    ///     Inserts an EXIF segment that holds an orientation into a JPEG.
    /// </summary>
    /// <param name="jpeg">The bytes of a JPEG file.</param>
    /// <param name="orientation">The orientation value to write.</param>
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
    ///     Appends a 16-bit unsigned integer.
    /// </summary>
    /// <param name="bytes">The list to append to.</param>
    /// <param name="value">The value.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    private static void AddUInt16(List<byte> bytes, int value, bool littleEndian)
    {
        byte[] pair = [(byte)(value >> 8), (byte)(value & 0xFF)];
        bytes.AddRange(littleEndian ? Enumerable.Reverse(pair).ToArray() : pair);
    }

    /// <summary>
    ///     Appends a 32-bit unsigned integer.
    /// </summary>
    /// <param name="bytes">The list to append to.</param>
    /// <param name="value">The value.</param>
    /// <param name="littleEndian"><see langword="true"/> for little-endian byte order.</param>
    private static void AddUInt32(List<byte> bytes, int value, bool littleEndian)
    {
        byte[] quad = [(byte)(value >> 24), (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF)];
        bytes.AddRange(littleEndian ? Enumerable.Reverse(quad).ToArray() : quad);
    }
}
