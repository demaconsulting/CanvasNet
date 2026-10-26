// cspell:ignore exif jpeg jfif
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ExifOrientation class (part of the SourceLoader unit).
/// </summary>
public class ExifOrientationTests
{
    /// <summary>
    ///     Creates the bytes of a small JPEG without any EXIF segment.
    /// </summary>
    /// <returns>The bytes.</returns>
    private static byte[] CreatePlainJpeg()
    {
        using var surface = FixtureFactory.CreateGradient();
        using var stream = new MemoryStream();
        JpegCodec.Save(surface, stream);
        return stream.ToArray();
    }

    /// <summary>
    ///     Creates the 3x2 image whose pixels are numbered <c>a</c> (0) to <c>f</c> (5) in reading order.
    /// </summary>
    /// <returns>A new surface.</returns>
    private static Surface CreateNumbered()
    {
        var surface = new Surface(3, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                surface[x, y] = new Rgba32((byte)(((y * 3) + x) * 10), 0, 0, 255);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Renders a surface as the letters of its numbered pixels, one line per row, separated by slashes.
    /// </summary>
    /// <param name="surface">The surface made of the numbered pixels.</param>
    /// <returns>The text, for example <c>abc/def</c>.</returns>
    private static string Letters(Surface surface)
    {
        var rows = new List<string>();
        for (var y = 0; y < surface.Height; y++)
        {
            var letters = new char[surface.Width];
            for (var x = 0; x < surface.Width; x++)
            {
                letters[x] = (char)('a' + (surface[x, y].R / 10));
            }

            rows.Add(new string(letters));
        }

        return string.Join('/', rows);
    }

    /// <summary>
    ///     Test that every orientation is read from a little-endian and a big-endian EXIF segment.
    /// </summary>
    /// <param name="orientation">The orientation to write.</param>
    /// <param name="littleEndian">The byte order to write it in.</param>
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    public void ExifOrientation_Read_ExifSegment_ReturnsOrientation(int orientation, bool littleEndian)
    {
        // Arrange: a JPEG with an EXIF segment holding the orientation
        var jpeg = FixtureFactory.InsertExifOrientation(CreatePlainJpeg(), orientation, littleEndian);
        using var stream = new MemoryStream(jpeg);

        // Act: read the orientation
        var result = ExifOrientation.Read(stream);

        // Assert: the orientation that was written
        Assert.Equal(orientation, result);
    }

    /// <summary>
    ///     Test that a JPEG without an EXIF segment has no orientation.
    /// </summary>
    [Fact]
    public void ExifOrientation_Read_NoExifSegment_ReturnsNull()
    {
        // Arrange: a JPEG written by the codec, which writes no EXIF
        using var stream = new MemoryStream(CreatePlainJpeg());

        // Act / Assert: no orientation
        Assert.Null(ExifOrientation.Read(stream));
    }

    /// <summary>
    ///     Test that data that is not a JPEG has no orientation.
    /// </summary>
    [Fact]
    public void ExifOrientation_Read_NotJpeg_ReturnsNull()
    {
        // Arrange: garbage, and an empty stream
        using var garbage = new MemoryStream(FixtureFactory.CreateGarbage(64));
        using var empty = new MemoryStream();

        // Act / Assert: no orientation, and no exception
        Assert.Null(ExifOrientation.Read(garbage));
        Assert.Null(ExifOrientation.Read(empty));
    }

    /// <summary>
    ///     Test that a value that is not an orientation is ignored.
    /// </summary>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(255)]
    public void ExifOrientation_Read_ValueOutOfRange_ReturnsNull(int value)
    {
        // Arrange: a JPEG whose EXIF orientation is not 1 to 8
        using var stream = new MemoryStream(FixtureFactory.InsertExifOrientation(CreatePlainJpeg(), value, true));

        // Act / Assert: no orientation
        Assert.Null(ExifOrientation.Read(stream));
    }

    /// <summary>
    ///     Test that truncated data never throws.
    /// </summary>
    [Fact]
    public void ExifOrientation_Read_TruncatedAtEveryLength_NeverThrows()
    {
        // Arrange: a JPEG with an EXIF segment
        var jpeg = FixtureFactory.InsertExifOrientation(CreatePlainJpeg(), 6, true);

        // Act / Assert: every prefix of the first 40 bytes is read without an exception, and the full segment is found
        for (var length = 0; length < 40; length++)
        {
            using var stream = new MemoryStream(jpeg, 0, length);
            var result = ExifOrientation.Read(stream);
            Assert.True(result is null or 6);
        }

        using var whole = new MemoryStream(jpeg);
        Assert.Equal(6, ExifOrientation.Read(whole));
    }

    /// <summary>
    ///     Test that the segment is found after other segments, and in a stream that cannot seek.
    /// </summary>
    [Fact]
    public void ExifOrientation_Read_AfterOtherSegmentsInForwardOnlyStream_ReturnsOrientation()
    {
        // Arrange: SOI, a JFIF-style APP0 segment, an APP1 that is not EXIF, then the EXIF segment
        var exif = FixtureFactory.InsertExifOrientation(CreatePlainJpeg(), 3, false);
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x08, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 0]);
        bytes.AddRange([0xFF, 0xE1, 0x00, 0x0A, (byte)'h', (byte)'t', (byte)'t', (byte)'p', 0, 0, 0, 0]);
        bytes.AddRange(exif.Skip(2));
        using var stream = new ForwardOnlyStream(bytes.ToArray());

        // Act: read the orientation
        var result = ExifOrientation.Read(stream);

        // Assert: the orientation of the EXIF segment
        Assert.Equal(3, result);
    }

    /// <summary>
    ///     Test that the path overload reads the file.
    /// </summary>
    [Fact]
    public void ExifOrientation_Read_Path_ReadsFile()
    {
        // Arrange: a JPEG file with an orientation
        using var directory = new TempDirectory();
        var path = directory.Combine("exif.jpg");
        File.WriteAllBytes(path, FixtureFactory.InsertExifOrientation(CreatePlainJpeg(), 8, true));

        // Act / Assert: the orientation is read; a missing file is an I/O failure
        Assert.Equal(8, ExifOrientation.Read(path));
        Assert.Throws<FileNotFoundException>(() => ExifOrientation.Read(directory.Combine("missing.jpg")));
    }

    /// <summary>
    ///     Test that a null stream is rejected.
    /// </summary>
    [Fact]
    public void ExifOrientation_Read_NullStream_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a null stream is rejected
        Assert.Throws<ArgumentNullException>(() => ExifOrientation.Read((Stream)null!));
    }

    /// <summary>
    ///     Test that each orientation turns the numbered image into the expected upright image.
    /// </summary>
    /// <param name="orientation">The orientation to apply.</param>
    /// <param name="expected">The expected letters, row by row.</param>
    [Theory]
    [InlineData(1, "abc/def")]
    [InlineData(2, "cba/fed")]
    [InlineData(3, "fed/cba")]
    [InlineData(4, "def/abc")]
    [InlineData(5, "ad/be/cf")]
    [InlineData(6, "da/eb/fc")]
    [InlineData(7, "fc/eb/da")]
    [InlineData(8, "cf/be/ad")]
    public void ExifOrientation_Apply_Orientation_MapsPixels(int orientation, string expected)
    {
        // Arrange: the numbered 3x2 image
        using var source = CreateNumbered();

        // Act: apply the orientation
        using var result = ExifOrientation.Apply(source, orientation);

        // Assert: the pixels are where the EXIF specification puts them, and the source is unchanged
        Assert.Equal(expected, Letters(result));
        Assert.Equal("abc/def", Letters(source));
    }

    /// <summary>
    ///     Test that an orientation outside 1 to 8 and a null surface are rejected.
    /// </summary>
    [Fact]
    public void ExifOrientation_Apply_InvalidArguments_Throw()
    {
        // Arrange: a surface
        using var source = CreateNumbered();

        // Act / Assert: the range and null checks
        Assert.Throws<ArgumentOutOfRangeException>(() => ExifOrientation.Apply(source, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExifOrientation.Apply(source, 9));
        Assert.Throws<ArgumentNullException>(() => ExifOrientation.Apply(null!, 1));
    }

    /// <summary>
    ///     A stream that can only be read forward, to exercise the reading path that cannot seek.
    /// </summary>
    private sealed class ForwardOnlyStream(byte[] data) : MemoryStream(data)
    {
        /// <inheritdoc />
        public override bool CanSeek => false;
    }
}
