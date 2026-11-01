using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for <see cref="ImageDecoder"/>: signature-driven and explicit-format decoding.
/// </summary>
public class ImageDecoderTests
{
    /// <summary>A minimal hand-built 1x1 GIF89a whose single pixel is rgb(11, 22, 33).</summary>
    private static readonly byte[] TinyGif =
    [
        (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x0B, 0x16, 0x21,
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x4C, 0x01, 0x00, 0x3B
    ];

    /// <summary>A stream wrapper that reports itself as non-seekable.</summary>
    private sealed class TrickleStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 5));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Builds a small, varied surface.</summary>
    private static Surface BuildSurface()
    {
        var surface = new Surface(4, 3);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 60), (byte)(y * 80), (byte)(x * y * 20), 255);
            }
        }

        return surface;
    }

    /// <summary>Proves a lossless format is decoded through signature detection to exactly the source pixels.</summary>
    [Fact]
    public void ImageDecoder_LoadBytes_PngBmpTiff_DecodesExactPixels()
    {
        var source = BuildSurface();
        foreach (var save in new Action<Stream>[]
                 {
                     s => PngCodec.Save(source, s),
                     s => BmpCodec.Save(source, s, BmpBitDepth.Bit32),
                     s => TiffCodec.Save(source, s)
                 })
        {
            using var stream = new MemoryStream();
            save(stream);

            var loaded = ImageDecoder.Load(stream.ToArray());

            Assert.Equal(source.Width, loaded.Width);
            Assert.Equal(source.Height, loaded.Height);
            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    Assert.Equal(source[x, y], loaded[x, y]);
                }
            }
        }
    }

    /// <summary>Proves JPEG, GIF, and JPEG 2000 content is dispatched to the matching codec.</summary>
    [Fact]
    public void ImageDecoder_LoadBytes_JpegGifJpeg2000_DispatchesToMatchingCodec()
    {
        using var jpeg = new MemoryStream();
        JpegCodec.Save(BuildSurface(), jpeg);
        var jp2 = File.ReadAllBytes(Path.Join(AppContext.BaseDirectory, "Jpeg2000Fixtures", "gray.jp2"));

        var fromJpeg = ImageDecoder.Load(jpeg.ToArray());
        var fromGif = ImageDecoder.Load(TinyGif);
        var fromJp2 = ImageDecoder.Load(jp2);

        Assert.Equal((4, 3), (fromJpeg.Width, fromJpeg.Height));
        Assert.Equal(new Rgba32(11, 22, 33, 255), fromGif[0, 0]);
        using var jp2Stream = new MemoryStream(jp2);
        var expected = Jpeg2000Codec.Load(jp2Stream);
        Assert.Equal((expected.Width, expected.Height), (fromJp2.Width, fromJp2.Height));
    }

    /// <summary>Proves a seekable stream is rewound to where it started, so a non-zero start offset is honored.</summary>
    [Fact]
    public void ImageDecoder_LoadStream_SeekableStreamAtOffset_DecodesFromStartPosition()
    {
        using var png = new MemoryStream();
        PngCodec.Save(BuildSurface(), png);
        var prefixed = new byte[] { 1, 2, 3 }.Concat(png.ToArray()).ToArray();
        using var stream = new MemoryStream(prefixed) { Position = 3 };

        var loaded = ImageDecoder.Load(stream);

        Assert.Equal((4, 3), (loaded.Width, loaded.Height));
    }

    /// <summary>Proves a non-seekable stream (read in tiny chunks) is decoded without being buffered or rewound.</summary>
    [Fact]
    public void ImageDecoder_LoadStream_NonSeekableStream_DecodesWholeImage()
    {
        var source = BuildSurface();
        using var png = new MemoryStream();
        PngCodec.Save(source, png);
        using var stream = new TrickleStream(new MemoryStream(png.ToArray()));

        var loaded = ImageDecoder.Load(stream);

        Assert.Equal(source[3, 2], loaded[3, 2]);
        Assert.Equal((4, 3), (loaded.Width, loaded.Height));
    }

    /// <summary>Proves data with no recognized signature is rejected with <see cref="InvalidDataException"/> on every overload.</summary>
    [Fact]
    public void ImageDecoder_Load_UnrecognizedData_ThrowsInvalidDataException()
    {
        byte[] garbage = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(garbage));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(Array.Empty<byte>()));
        using var stream = new MemoryStream(garbage);
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(stream));
        using var nonSeekable = new TrickleStream(new MemoryStream(garbage));
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(nonSeekable));
    }

    /// <summary>Proves an explicit format is decoded without sniffing, and the codec rejects data of another format.</summary>
    [Fact]
    public void ImageDecoder_LoadStreamWithFormat_UsesGivenFormat()
    {
        using var png = new MemoryStream();
        PngCodec.Save(BuildSurface(), png);
        png.Position = 0;

        var loaded = ImageDecoder.Load(png, ImageFormat.Png);

        Assert.Equal((4, 3), (loaded.Width, loaded.Height));
        png.Position = 0;
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Load(png, ImageFormat.Bmp));
    }

    /// <summary>Proves null and undefined-format arguments are rejected.</summary>
    [Fact]
    public void ImageDecoder_Load_InvalidArguments_Throw()
    {
        using var stream = new MemoryStream(TinyGif);

        Assert.Throws<ArgumentNullException>(() => ImageDecoder.Load((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => ImageDecoder.Load(null!, ImageFormat.Png));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageDecoder.Load(stream, (ImageFormat)99));
    }
}
