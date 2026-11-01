using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for <see cref="ImageFormats"/>: signature detection and media-type recognition.
/// </summary>
public class ImageFormatsTests
{
    /// <summary>The 8-byte PNG signature.</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Builds a plausible 30-byte BMP header with the given bitmap-header size.</summary>
    private static byte[] BuildBmpHeader(int dibSize)
    {
        var header = new byte[30];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        header[14] = (byte)dibSize;
        return header;
    }

    /// <summary>Proves each supported signature is recognized as its own format.</summary>
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, ImageFormat.Png)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ImageFormat.Jpeg)]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'7', (byte)'a' }, ImageFormat.Gif)]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, ImageFormat.Gif)]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08 }, ImageFormat.Tiff)]
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2A, 0x00 }, ImageFormat.Tiff)]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A }, ImageFormat.Jpeg2000)]
    [InlineData(new byte[] { 0xFF, 0x4F, 0xFF, 0x51, 0x00 }, ImageFormat.Jpeg2000)]
    public void ImageFormats_Detect_KnownSignature_ReturnsFormat(byte[] header, ImageFormat expected)
    {
        Assert.Equal(expected, ImageFormats.Detect(header));
    }

    /// <summary>Proves a BMP is recognized for every accepted bitmap-header size.</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(40)]
    [InlineData(52)]
    [InlineData(56)]
    [InlineData(64)]
    [InlineData(108)]
    [InlineData(124)]
    public void ImageFormats_Detect_BmpWithPlausibleHeader_ReturnsBmp(int dibSize)
    {
        Assert.Equal(ImageFormat.Bmp, ImageFormats.Detect(BuildBmpHeader(dibSize)));
    }

    /// <summary>Proves text that merely starts with <c>BM</c>, or a truncated or odd BMP header, is not a BMP.</summary>
    [Fact]
    public void ImageFormats_Detect_ImplausibleBmpHeader_ReturnsNull()
    {
        var badSize = BuildBmpHeader(41);
        var reservedSet = BuildBmpHeader(40);
        reservedSet[6] = 1;

        Assert.Null(ImageFormats.Detect(badSize));
        Assert.Null(ImageFormats.Detect(reservedSet));
        Assert.Null(ImageFormats.Detect(BuildBmpHeader(40).AsSpan(0, 17)));
        Assert.Null(ImageFormats.Detect("BM - a text file that is long enough to read"u8));
    }

    /// <summary>Proves empty, short, and unrecognized input yields <see langword="null"/> without throwing.</summary>
    [Fact]
    public void ImageFormats_Detect_EmptyShortOrUnknown_ReturnsNull()
    {
        Assert.Null(ImageFormats.Detect([]));
        Assert.Null(ImageFormats.Detect(PngSignature.AsSpan(0, 7)));
        Assert.Null(ImageFormats.Detect("%PDF-1.7"u8));
        Assert.Null(ImageFormats.Detect("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8));
    }

    /// <summary>Proves every recognized media type maps to its format.</summary>
    [Theory]
    [InlineData("image/png", ImageFormat.Png)]
    [InlineData("image/jpeg", ImageFormat.Jpeg)]
    [InlineData("image/jpg", ImageFormat.Jpeg)]
    [InlineData("image/gif", ImageFormat.Gif)]
    [InlineData("image/bmp", ImageFormat.Bmp)]
    [InlineData("image/x-bmp", ImageFormat.Bmp)]
    [InlineData("image/x-ms-bmp", ImageFormat.Bmp)]
    [InlineData("image/tiff", ImageFormat.Tiff)]
    [InlineData("image/x-tiff", ImageFormat.Tiff)]
    [InlineData("image/jp2", ImageFormat.Jpeg2000)]
    public void ImageFormats_FromMediaType_KnownType_ReturnsFormat(string mediaType, ImageFormat expected)
    {
        Assert.Equal(expected, ImageFormats.FromMediaType(mediaType));
    }

    /// <summary>Proves case, surrounding whitespace, and parameters are ignored.</summary>
    [Theory]
    [InlineData("IMAGE/PNG", ImageFormat.Png)]
    [InlineData("  Image/Jpeg  ", ImageFormat.Jpeg)]
    [InlineData("image/png; charset=binary", ImageFormat.Png)]
    [InlineData("image/gif ;q=0.9", ImageFormat.Gif)]
    [InlineData("Image/X-BMP;foo=bar", ImageFormat.Bmp)]
    public void ImageFormats_FromMediaType_CaseWhitespaceAndParameters_AreNormalized(string mediaType, ImageFormat expected)
    {
        Assert.Equal(expected, ImageFormats.FromMediaType(mediaType));
    }

    /// <summary>Proves null, empty, and unsupported media types (including SVG and metafiles) yield <see langword="null"/>.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("image")]
    [InlineData("image/svg+xml")]
    [InlineData("image/x-emf")]
    [InlineData("image/webp")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public void ImageFormats_FromMediaType_UnsupportedOrMissing_ReturnsNull(string? mediaType)
    {
        Assert.Null(ImageFormats.FromMediaType(mediaType));
    }
}
