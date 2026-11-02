// cspell:ignore exif jpeg jfif
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the EXIF orientation reader of the JpegCodec class (part of the JpegCodec unit).
/// </summary>
public class JpegCodecExifTests
{
    /// <summary>
    ///     Creates the bytes of a small JPEG without any EXIF segment.
    /// </summary>
    /// <returns>The bytes.</returns>
    private static byte[] CreatePlainJpeg()
    {
        using var surface = ImageFixtures.CreateGradient();
        using var stream = new MemoryStream();
        JpegCodec.Save(surface, stream);
        return stream.ToArray();
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
    public void JpegCodec_ReadExifOrientation_ExifSegment_ReturnsOrientation(int orientation, bool littleEndian)
    {
        // Arrange: a JPEG with an EXIF segment holding the orientation
        var jpeg = ImageFixtures.InsertExifOrientation(CreatePlainJpeg(), orientation, littleEndian);
        using var stream = new MemoryStream(jpeg);

        // Act: read the orientation
        var result = JpegCodec.ReadExifOrientation(stream);

        // Assert: the orientation that was written
        Assert.Equal(orientation, result);
    }

    /// <summary>
    ///     Test that a JPEG without an EXIF segment has no orientation.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_NoExifSegment_ReturnsNull()
    {
        // Arrange: a JPEG written by the codec, which writes no EXIF
        using var stream = new MemoryStream(CreatePlainJpeg());

        // Act / Assert: no orientation
        Assert.Null(JpegCodec.ReadExifOrientation(stream));
    }

    /// <summary>
    ///     Test that data that is not a JPEG has no orientation.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_NotJpeg_ReturnsNull()
    {
        // Arrange: garbage, and an empty stream
        using var garbage = new MemoryStream(ImageFixtures.CreateGarbage(64));
        using var empty = new MemoryStream();

        // Act / Assert: no orientation, and no exception
        Assert.Null(JpegCodec.ReadExifOrientation(garbage));
        Assert.Null(JpegCodec.ReadExifOrientation(empty));
    }

    /// <summary>
    ///     Test that a value that is not an orientation is ignored.
    /// </summary>
    /// <param name="value">The invalid value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(255)]
    public void JpegCodec_ReadExifOrientation_ValueOutOfRange_ReturnsNull(int value)
    {
        // Arrange: a JPEG whose EXIF orientation is not 1 to 8
        using var stream = new MemoryStream(ImageFixtures.InsertExifOrientation(CreatePlainJpeg(), value, true));

        // Act / Assert: no orientation
        Assert.Null(JpegCodec.ReadExifOrientation(stream));
    }

    /// <summary>
    ///     Test that truncated data never throws.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_TruncatedAtEveryLength_NeverThrows()
    {
        // Arrange: a JPEG with an EXIF segment
        var jpeg = ImageFixtures.InsertExifOrientation(CreatePlainJpeg(), 6, true);

        // Act / Assert: every prefix of the first 40 bytes is read without an exception, and the full segment is found
        for (var length = 0; length < 40; length++)
        {
            using var stream = new MemoryStream(jpeg, 0, length);
            var result = JpegCodec.ReadExifOrientation(stream);
            Assert.True(result is null or 6);
        }

        using var whole = new MemoryStream(jpeg);
        Assert.Equal(6, JpegCodec.ReadExifOrientation(whole));
    }

    /// <summary>
    ///     Test that the segment is found after other segments, and in a stream that cannot seek.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_AfterOtherSegmentsInForwardOnlyStream_ReturnsOrientation()
    {
        // Arrange: SOI, a JFIF-style APP0 segment, an APP1 that is not EXIF, then the EXIF segment
        var exif = ImageFixtures.InsertExifOrientation(CreatePlainJpeg(), 3, false);
        var bytes = new List<byte> { 0xFF, 0xD8 };
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x08, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 0]);
        bytes.AddRange([0xFF, 0xE1, 0x00, 0x0A, (byte)'h', (byte)'t', (byte)'t', (byte)'p', 0, 0, 0, 0]);
        bytes.AddRange(exif.Skip(2));
        using var stream = new ForwardOnlyStream(bytes.ToArray());

        // Act: read the orientation
        var result = JpegCodec.ReadExifOrientation(stream);

        // Assert: the orientation of the EXIF segment
        Assert.Equal(3, result);
    }

    /// <summary>
    ///     Test that the path overload reads the file.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_Path_ReadsFile()
    {
        // Arrange: a JPEG file with an orientation, in a folder that is removed afterwards
        var directory = Path.Combine(Path.GetTempPath(), "canvasnet-exif-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "exif.jpg");
            File.WriteAllBytes(path, ImageFixtures.InsertExifOrientation(CreatePlainJpeg(), 8, true));

            // Act / Assert: the orientation is read; a missing file is an I/O failure
            Assert.Equal(8, JpegCodec.ReadExifOrientation(path));
            Assert.Throws<FileNotFoundException>(() => JpegCodec.ReadExifOrientation(Path.Combine(directory, "missing.jpg")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a stream of fill bytes without end is abandoned within the byte budget.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_EndlessFillBytes_ReturnsNullWithinBudget()
    {
        // Arrange: SOI followed by 0xFF forever, generated without allocating
        using var stream = new EndlessFillStream();

        // Act
        var result = JpegCodec.ReadExifOrientation(stream);

        // Assert: no orientation, and no more than the 1 MiB budget (plus the signature) was read
        Assert.Null(result);
        Assert.InRange(stream.BytesRead, 1, (1 << 20) + 8);
    }

    /// <summary>
    ///     Test that a null stream or path is rejected.
    /// </summary>
    [Fact]
    public void JpegCodec_ReadExifOrientation_NullArgument_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: a null stream and a null path are rejected
        Assert.Throws<ArgumentNullException>(() => JpegCodec.ReadExifOrientation((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => JpegCodec.ReadExifOrientation((string)null!));
    }

    /// <summary>
    ///     A forward-only stream that holds a JPEG start-of-image marker and then an endless run of 0xFF bytes.
    /// </summary>
    private sealed class EndlessFillStream : Stream
    {
        /// <summary>The number of bytes handed out so far.</summary>
        private long _position;

        /// <summary>Gets the number of bytes read from the stream.</summary>
        public long BytesRead => _position;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc />
        public override int Read(Span<byte> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = _position + i == 1 ? (byte)0xD8 : (byte)0xFF;
            }

            _position += buffer.Length;
            return buffer.Length;
        }

        /// <inheritdoc />
        public override int ReadByte()
        {
            return _position++ == 1 ? 0xD8 : 0xFF;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
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
