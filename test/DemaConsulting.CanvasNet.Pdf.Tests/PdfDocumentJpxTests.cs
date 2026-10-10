// cspell:ignore JPXDecode SMaskInData cdef
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.Codecs;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Tests for <c>/Filter /JPXDecode</c> (JPEG 2000) image XObjects and the <c>/SMask</c>
///     soft-mask path in <see cref="PdfDocument"/>. The JPEG 2000 streams are produced by the
///     test-only <c>Jpeg2000TestEncoder</c> (linked from the core test project).
/// </summary>
public class PdfDocumentJpxTests
{
    private static readonly PdfRenderOptions Transparent = new() { BackgroundColor = new(0, 0, 0, 0) };

    private static J2kOptions Lossless(int levels = 1) => new() { Levels = levels };

    /// <summary>Builds an image whose component <c>c</c> is filled with <c>values[c]</c>.</summary>
    private static J2kImage Flat(int width, int height, params int[] values)
    {
        var image = Jpeg2000TestEncoder.MakeImage(width, height, values.Length);
        for (var c = 0; c < values.Length; c++)
        {
            Array.Fill(image.Components[c].Samples, values[c]);
        }

        return image;
    }

    private static byte[] Jp2(J2kImage image, J2kJp2Options? options = null, int levels = 1) =>
        Jpeg2000TestEncoder.WrapJp2(image, Jpeg2000TestEncoder.Encode(image, Lossless(levels)), options ?? new J2kJp2Options());

    private static byte[] StreamObject(string entries, byte[] data)
    {
        var header = Encoding.ASCII.GetBytes($"<< {entries} /Length {data.Length} >>\nstream\n");
        return [.. header, .. data, .. "\nendstream"u8.ToArray()];
    }

    private static byte[] ImageObject(string entries, byte[] jpx) =>
        StreamObject($"/Type /XObject /Subtype /Image /Width 8 /Height 8 /BitsPerComponent 8 /Filter /JPXDecode {entries}", jpx);

    /// <summary>Builds a one-page 100x100 PDF placing /Im0 (object 5) over the whole page.</summary>
    private static byte[] BuildPdf(params byte[][] extraObjects) =>
        BuildPdf("100 0 0 100 0 0 cm /Im0 Do", extraObjects);

    /// <summary>Builds a one-page 100x100 PDF with the given page content stream and extra objects from object 5.</summary>
    private static byte[] BuildPdf(string contentText, byte[][] extraObjects)
    {
        var content = Encoding.ASCII.GetBytes(contentText);
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >>"u8.ToArray(),
            StreamObject(string.Empty, content),
        };
        bodies.AddRange(extraObjects);

        var buffer = new List<byte>(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xref = buffer.Count;
        buffer.AddRange(Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return [.. buffer];
    }

    private static Surface Render(params byte[][] extraObjects)
    {
        using var document = PdfDocument.Open(new MemoryStream(BuildPdf(extraObjects)));
        return document.Render(0, 100, 100, Transparent);
    }

    private static void AssertNear(Rgba32 expected, Rgba32 actual, int tolerance = 2)
    {
        Assert.True(Math.Abs(expected.R - actual.R) <= tolerance, $"R expected {expected.R} actual {actual.R}");
        Assert.True(Math.Abs(expected.G - actual.G) <= tolerance, $"G expected {expected.G} actual {actual.G}");
        Assert.True(Math.Abs(expected.B - actual.B) <= tolerance, $"B expected {expected.B} actual {actual.B}");
        Assert.True(Math.Abs(expected.A - actual.A) <= tolerance, $"A expected {expected.A} actual {actual.A}");
    }

    /// <summary>
    ///     Proves a JPX image produced by a real encoder (ImageMagick/OpenJPEG, not the test-only
    ///     encoder) embedded as /JPXDecode renders close to the source pixels when placed 1:1 on the page.
    /// </summary>
    [Fact]
    public void PdfDocument_Images_Jpx_RealEncoderFixture_RendersCloseToSourcePixels()
    {
        // Arrange: a 100x70 real-encoder gray JP2 placed 1:1 at the top of a 100x100 page (PDF y 30..100)
        var directory = Path.Join(AppContext.BaseDirectory, "Jpeg2000Fixtures");
        var jpx = File.ReadAllBytes(Path.Join(directory, "gray.jp2"));
        using var source = PngCodec.Load(Path.Join(directory, "source_gray.png"));
        var pdf = BuildPdf("100 0 0 70 0 30 cm /Im0 Do", [ImageObject(string.Empty, jpx)]);

        // Act
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        using var surface = document.Render(0, 100, 100, Transparent);

        // Assert: the image fills the top 70 pixel rows (PDF y 30..100). The renderer resamples
        // the (noisy) image, so compare the mean absolute difference rather than exact pixels
        double total = 0;
        var count = 0;
        for (var y = 1; y < 69; y++)
        {
            for (var x = 1; x < 99; x++)
            {
                total += Math.Abs(source[x, y].R - surface[x, y].R);
                count++;
            }
        }

        Assert.True(total / count < 8, $"mean absolute difference {total / count:F2} too large");

        // The area below the image stays transparent
        Assert.Equal(0, surface[50, 90].A);
    }

    /// <summary>Proves a JPX image without /ColorSpace uses the JP2's own gray color space.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_GrayWithoutColorSpace_UsesJp2ColorSpace()
    {
        var jpx = Jp2(Flat(8, 8, 100), new J2kJp2Options { EnumCs = 17 });
        using var surface = Render(ImageObject(string.Empty, jpx));
        AssertNear(new Rgba32(100, 100, 100, 255), surface[50, 50], 0);
    }

    /// <summary>Proves a JPX image without /ColorSpace uses the JP2's own sRGB color space.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_RgbWithoutColorSpace_UsesJp2ColorSpace()
    {
        var jpx = Jp2(Flat(8, 8, 200, 50, 10), new J2kJp2Options { EnumCs = 16 }, levels: 2);
        using var surface = Render(ImageObject(string.Empty, jpx));
        AssertNear(new Rgba32(200, 50, 10, 255), surface[50, 50], 0);
    }

    /// <summary>Proves a JPX CMYK image decodes through the naive CMYK conversion.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_CmykWithoutColorSpace_UsesJp2ColorSpace()
    {
        var jpx = Jp2(Flat(8, 8, 255, 0, 0, 0), new J2kJp2Options { EnumCs = 12 });
        using var surface = Render(ImageObject(string.Empty, jpx));
        AssertNear(new Rgba32(0, 255, 255, 255), surface[50, 50], 0);
    }

    /// <summary>Proves an explicit /DeviceRGB color space is honoured when it matches the data.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_ExplicitDeviceRgb_HonorsColorSpace()
    {
        var jpx = Jp2(Flat(8, 8, 10, 20, 30), new J2kJp2Options { EnumCs = 16 });
        using var surface = Render(ImageObject("/ColorSpace /DeviceRGB", jpx));
        AssertNear(new Rgba32(10, 20, 30, 255), surface[50, 50], 0);
    }

    /// <summary>Proves an explicit /DeviceCMYK color space is honoured for four-channel data.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_ExplicitDeviceCmyk_HonorsColorSpace()
    {
        var jpx = Jp2(Flat(8, 8, 0, 255, 0, 0), new J2kJp2Options { EnumCs = 12 });
        using var surface = Render(ImageObject("/ColorSpace /DeviceCMYK", jpx));
        AssertNear(new Rgba32(255, 0, 255, 255), surface[50, 50], 0);
    }

    /// <summary>Proves /ICCBased is resolved by component count.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_IccBasedColorSpace_ResolvesByComponentCount()
    {
        var jpx = Jp2(Flat(8, 8, 90, 80, 70), new J2kJp2Options { EnumCs = 16 });
        var icc = StreamObject("/N 3 /Alternate /DeviceRGB", [0, 1, 2, 3]);
        using var surface = Render(ImageObject("/ColorSpace [/ICCBased 6 0 R]", jpx), icc);
        AssertNear(new Rgba32(90, 80, 70, 255), surface[50, 50], 0);
    }

    /// <summary>Proves an /Indexed color space overrides the JP2 color space: samples are palette indices.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_IndexedColorSpace_OverridesJp2ColorSpace()
    {
        var jpx = Jp2(Flat(8, 8, 1), new J2kJp2Options { EnumCs = 17 });
        using var surface = Render(ImageObject("/ColorSpace [/Indexed /DeviceRGB 1 <FF000000FF00>]", jpx));
        AssertNear(new Rgba32(0, 255, 0, 255), surface[50, 50], 0);
    }

    /// <summary>Proves an /Indexed color space over non-8-bit JPX samples fails closed (samples are scaled to 8 bits).</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_IndexedColorSpaceNonEightBit_ThrowsUnsupportedImageFeatureException()
    {
        var jpx = Jp2(Jpeg2000TestEncoder.MakeImage(8, 8, 1, 4, 1, 0), new J2kJp2Options { EnumCs = 17 });
        Assert.Throws<UnsupportedImageFeatureException>(
            () => Render(ImageObject("/ColorSpace [/Indexed /DeviceRGB 1 <FF000000FF00>]", jpx)));
    }

    /// <summary>Proves a PDF /ColorSpace override on JP2 data that carries its own palette fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_ColorSpaceOverJp2Palette_ThrowsUnsupportedImageFeatureException()
    {
        var image = Flat(8, 8, 1);
        int[][] palette = [[255, 0, 0], [0, 255, 0]];
        var jpx = Jp2(image, new J2kJp2Options { Palette = palette, PaletteDepths = [8, 8, 8], Cmap = [(0, 1, 0), (0, 1, 1), (0, 1, 2)] });
        Assert.Throws<UnsupportedImageFeatureException>(() => Render(ImageObject("/ColorSpace /DeviceRGB", jpx)));

        // Without an override the JP2's own palette is honored
        using var surface = Render(ImageObject(string.Empty, jpx));
        AssertNear(new Rgba32(0, 255, 0, 255), surface[50, 50], 0);
    }

    /// <summary>Proves /Decode [1 0] inverts a gray JPX image.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_DecodeArray_InvertsSamples()
    {
        var jpx = Jp2(Flat(8, 8, 50), new J2kJp2Options { EnumCs = 17 });
        using var surface = Render(ImageObject("/Decode [1 0]", jpx));
        AssertNear(new Rgba32(205, 205, 205, 255), surface[50, 50], 0);
    }

    /// <summary>Proves /SMaskInData 0 (the default) ignores the codestream's opacity channel.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_SMaskInDataZero_IgnoresAlphaChannel()
    {
        var jpx = AlphaJp2(200, 50, 10, 128, type: 1);
        using var surface = Render(ImageObject("/ColorSpace /DeviceRGB /SMaskInData 0", jpx));
        AssertNear(new Rgba32(200, 50, 10, 255), surface[50, 50], 0);
    }

    /// <summary>Proves /SMaskInData 1 applies the opacity channel as straight alpha.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_SMaskInDataOne_AppliesAlphaChannel()
    {
        var jpx = AlphaJp2(200, 50, 10, 128, type: 1);
        using var surface = Render(ImageObject("/ColorSpace /DeviceRGB /SMaskInData 1", jpx));
        var pixel = surface[50, 50];
        Assert.Equal(128, pixel.A);
        AssertNear(new Rgba32(200, 50, 10, 128), pixel, 3);
    }

    /// <summary>Proves /SMaskInData 2 un-premultiplies the color samples.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_SMaskInDataTwo_UnpremultipliesColor()
    {
        var jpx = AlphaJp2(100, 25, 5, 128, type: 2);
        using var surface = Render(ImageObject("/ColorSpace /DeviceRGB /SMaskInData 2", jpx));
        var pixel = surface[50, 50];
        Assert.Equal(128, pixel.A);
        AssertNear(new Rgba32(199, 50, 10, 128), pixel, 4);
    }

    /// <summary>Proves a JPX /SMask image (of different dimensions) supplies the alpha channel.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_JpxSMask_AppliesLuminanceAsAlpha()
    {
        var baseJpx = Jp2(Flat(8, 8, 200, 50, 10), new J2kJp2Options { EnumCs = 16 });
        var mask = Flat(2, 1, 0);
        mask.Components[0].Samples[1] = 255;
        var maskJpx = Jp2(mask, new J2kJp2Options { EnumCs = 17 }, levels: 0);
        var maskObject = StreamObject(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceGray /Filter /JPXDecode",
            maskJpx);
        using var surface = Render(ImageObject("/SMask 6 0 R", baseJpx), maskObject);
        Assert.Equal(0, surface[25, 50].A);
        AssertNear(new Rgba32(200, 50, 10, 255), surface[75, 50], 0);
    }

    /// <summary>Proves a JPX /SMask honours its own /Decode inversion.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_JpxSMaskWithDecode_InvertsMask()
    {
        var baseJpx = Jp2(Flat(8, 8, 200, 50, 10), new J2kJp2Options { EnumCs = 16 });
        var maskJpx = Jp2(Flat(8, 8, 255), new J2kJp2Options { EnumCs = 17 });
        var maskObject = StreamObject(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /BitsPerComponent 8 /ColorSpace /DeviceGray /Filter /JPXDecode /Decode [1 0]",
            maskJpx);
        using var surface = Render(ImageObject("/SMask 6 0 R", baseJpx), maskObject);
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>Proves an /SMask on a non-JPX (raw sample) image is applied too.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskOnRawImage_AppliesLuminanceAsAlpha()
    {
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /SMask 6 0 R",
            [255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8",
            [64]);
        using var surface = Render(image, mask);
        Assert.Equal(64, surface[50, 50].A);
    }

    /// <summary>Proves an explicit /SMask takes precedence over /SMaskInData.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_SMaskAndSMaskInData_SMaskWins()
    {
        var jpx = AlphaJp2(200, 50, 10, 128, type: 1);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8",
            [255]);
        using var surface = Render(ImageObject("/ColorSpace /DeviceRGB /SMaskInData 1 /SMask 6 0 R", jpx), mask);
        Assert.Equal(255, surface[50, 50].A);
    }

    /// <summary>Proves a /ColorSpace whose component count disagrees with the data fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_ColorSpaceComponentMismatch_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 1, 2, 3), new J2kJp2Options { EnumCs = 16 });
        Assert.Throws<InvalidDataException>(() => Render(ImageObject("/ColorSpace /DeviceGray", jpx)));
    }

    /// <summary>Proves an out-of-range /SMaskInData fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_InvalidSMaskInData_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 1), new J2kJp2Options { EnumCs = 17 });
        Assert.Throws<InvalidDataException>(() => Render(ImageObject("/SMaskInData 3", jpx)));
    }

    /// <summary>Proves a malformed /Decode array fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_DecodeArrayWrongLength_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 1), new J2kJp2Options { EnumCs = 17 });
        Assert.Throws<InvalidDataException>(() => Render(ImageObject("/Decode [0 1 0 1]", jpx)));
    }

    /// <summary>Proves garbage JPX data fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_GarbageData_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => Render(ImageObject(string.Empty, [1, 2, 3, 4, 5, 6, 7, 8])));
    }

    /// <summary>Proves a truncated JPX stream fails closed with <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_TruncatedData_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 200, 50, 10), new J2kJp2Options { EnumCs = 16 });
        var truncated = jpx[..(jpx.Length / 2)];
        Assert.Throws<InvalidDataException>(() => Render(ImageObject(string.Empty, truncated)));
    }

    /// <summary>Proves a codestream header declaring huge dimensions is rejected without decoding.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_OversizedHeader_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 1), new J2kJp2Options { EnumCs = 17 });
        var siz = jpx.AsSpan().IndexOf(new byte[] { 0xFF, 0x51 });
        Assert.True(siz > 0);

        // Xsiz and Ysiz follow the marker, Lsiz and Rsiz (big-endian): set both to 100000
        foreach (var offset in new[] { siz + 6, siz + 10 })
        {
            jpx[offset] = 0;
            jpx[offset + 1] = 1;
            jpx[offset + 2] = 0x86;
            jpx[offset + 3] = 0xA0;
        }

        Assert.Throws<InvalidDataException>(() => Render(ImageObject(string.Empty, jpx)));
    }

    /// <summary>Proves a /Pattern /ColorSpace on a JPX image is rejected.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_PatternColorSpace_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 1), new J2kJp2Options { EnumCs = 17 });
        Assert.Throws<InvalidDataException>(() => Render(ImageObject("/ColorSpace /Pattern", jpx)));
    }

    /// <summary>Proves /SMaskInData 2 un-premultiplies CMYK component samples before the color conversion.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_SMaskInDataTwoCmyk_UnpremultipliesBeforeConversion()
    {
        // Premultiplied C = 128 at alpha 128 is a full-strength cyan: naive CMYK->RGB gives (0, 255, 255)
        var jpx = Jp2(
            Flat(8, 8, 128, 0, 0, 0, 128),
            new J2kJp2Options { EnumCs = 12, Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3), (3, 0, 4), (4, 1, 0)] });
        using var surface = Render(ImageObject("/ColorSpace /DeviceCMYK /SMaskInData 2", jpx));
        var pixel = surface[50, 50];
        Assert.Equal(128, pixel.A);
        AssertNear(new Rgba32(0, 255, 255, 128), pixel, 1);
    }

    /// <summary>Proves /SMaskInData 2 over an /Indexed color space fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_SMaskInDataTwoIndexed_ThrowsUnsupportedImageFeatureException()
    {
        var jpx = Jp2(Flat(8, 8, 1, 128), new J2kJp2Options { EnumCs = 17, Cdef = [(0, 0, 1), (1, 1, 0)] });
        Assert.Throws<UnsupportedImageFeatureException>(
            () => Render(ImageObject("/ColorSpace [/Indexed /DeviceRGB 1 <FF000000FF00>] /SMaskInData 2", jpx)));
    }

    /// <summary>Proves a non-stream /SMask fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskNotAStream_ThrowsInvalidDataException()
    {
        var image = RawGrayImage("/SMask 6 0 R", [255]);
        Assert.Throws<InvalidDataException>(() => Render(image, "<< /Type /XObject >>"u8.ToArray()));
    }

    /// <summary>Proves an /SMask stream lacking /Subtype /Image fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskNotImageSubtype_ThrowsInvalidDataException()
    {
        var image = RawGrayImage("/SMask 6 0 R", [255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Form /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8",
            [64]);
        Assert.Throws<InvalidDataException>(() => Render(image, mask));
    }

    /// <summary>Proves a non-gray /SMask image fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskNotGray_ThrowsInvalidDataException()
    {
        var image = RawGrayImage("/SMask 6 0 R", [255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8",
            [64, 64, 64]);
        Assert.Throws<InvalidDataException>(() => Render(image, mask));
    }

    /// <summary>Proves a raw soft-mask image honours its own /Decode inversion.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskRawWithDecode_InvertsMask()
    {
        var image = RawGrayImage("/SMask 6 0 R", [255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Decode [1 0]",
            [64]);
        using var surface = Render(image, mask);
        Assert.Equal(191, surface[50, 50].A);
    }

    /// <summary>Proves /Decode [1 0] inverts a raw gray image (the shared sample path).</summary>
    [Fact]
    public void PdfDocument_Images_RawWithDecode_InvertsSamples()
    {
        using var surface = Render(RawGrayImage("/Decode [1 0]", [55]));
        AssertNear(new Rgba32(200, 200, 200, 255), surface[50, 50], 0);
    }

    /// <summary>Proves a malformed /Decode array on a raw image fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_RawWithWrongLengthDecode_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => Render(RawGrayImage("/Decode [1 0 1 0]", [55])));
    }

    /// <summary>Proves an /SMask is applied to a DCTDecode image.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskOnDctImage_AppliesLuminanceAsAlpha()
    {
        using var source = new Surface(8, 8);
        source.Clear(new Rgba32(200, 200, 200, 255));
        using var jpegStream = new MemoryStream();
        JpegCodec.Save(source, jpegStream, quality: 100);
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /SMask 6 0 R",
            jpegStream.ToArray());
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8",
            [128]);
        using var surface = Render(image, mask);
        Assert.Equal(128, surface[50, 50].A);
    }

    /// <summary>Proves a non-identity /Decode is applied exactly to a 3-channel DCTDecode image.</summary>
    [Fact]
    public void PdfDocument_Images_DctRgbWithDecode_InvertsSamples()
    {
        using var source = new Surface(8, 8);
        source.Clear(new Rgba32(200, 100, 50, 255));
        using var jpegStream = new MemoryStream();
        JpegCodec.Save(source, jpegStream, quality: 100);
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Decode [1 0 1 0 1 0]",
            jpegStream.ToArray());
        using var inverted = Render(image);
        var plain = StreamObject(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
            jpegStream.ToArray());
        using var reference = Render(plain);
        var expected = reference[50, 50];
        AssertNear(new Rgba32((byte)(255 - expected.R), (byte)(255 - expected.G), (byte)(255 - expected.B), 255), inverted[50, 50], 1);
        AssertNear(new Rgba32(55, 155, 205, 255), inverted[50, 50], 3);
    }

    /// <summary>Proves a non-identity /Decode is applied exactly to a 1-channel (gray) DCTDecode image.</summary>
    [Fact]
    public void PdfDocument_Images_DctGrayWithDecode_InvertsSamples()
    {
        var jpeg = File.ReadAllBytes(Path.Join(AppContext.BaseDirectory, "JpegFixtures", "grayscale_baseline.jpg"));
        var info = JpegCodec.GetInfo(new MemoryStream(jpeg));
        Assert.Equal(1, info.Channels);
        var dictionary = $"/Type /XObject /Subtype /Image /Width {info.Width} /Height {info.Height} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /DCTDecode";
        using var reference = Render(StreamObject(dictionary, jpeg));
        using var inverted = Render(StreamObject(dictionary + " /Decode [1 0]", jpeg));
        foreach (var (x, y) in new[] { (10, 10), (50, 50), (90, 30) })
        {
            AssertNear(new Rgba32((byte)(255 - reference[x, y].R), (byte)(255 - reference[x, y].G), (byte)(255 - reference[x, y].B), 255), inverted[x, y], 1);
        }
    }

    /// <summary>Proves a 4-component (CMYK) JPEG with a non-identity /Decode still fails closed (the JPEG codec rejects it).</summary>
    [Fact]
    public void PdfDocument_Images_DctFourChannelWithDecode_ThrowsInvalidDataException()
    {
        // SOI, SOF0 (8-bit, 8x8, 4 components), EOI: rejected before any pixel decoding.
        byte[] jpeg =
        [
            0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x14, 0x08, 0x00, 0x08, 0x00, 0x08, 0x04,
            0x01, 0x11, 0x00, 0x02, 0x11, 0x00, 0x03, 0x11, 0x00, 0x04, 0x11, 0x00,
            0xFF, 0xD9,
        ];
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceCMYK /BitsPerComponent 8 /Filter /DCTDecode /Decode [1 0 1 0 1 0 1 0]",
            jpeg);
        Assert.Throws<InvalidDataException>(() => Render(image));
    }

    /// <summary>Proves a 1-bit /SMask supplies a fully transparent and a fully opaque pixel.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskOneBit_AppliesBinaryAlpha()
    {
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /SMask 6 0 R",
            [255, 255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 1",
            [0b0100_0000]);
        using var surface = Render(image, mask);
        Assert.Equal(0, surface[25, 50].A);
        Assert.Equal(255, surface[75, 50].A);
    }

    /// <summary>Proves a 16-bit /SMask is applied through its high byte.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskSixteenBit_AppliesHighByteAsAlpha()
    {
        var image = RawGrayImage("/SMask 6 0 R", [255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 16",
            [0x80, 0x00]);
        using var surface = Render(image, mask);
        Assert.Equal(128, surface[50, 50].A);
    }

    /// <summary>Proves a 4-bit /SMask honours /Decode and row padding.</summary>
    [Fact]
    public void PdfDocument_Images_SMaskFourBitWithDecode_InvertsMask()
    {
        var image = RawGrayImage("/SMask 6 0 R", [255]);
        var mask = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 4 /Decode [1 0]",
            [0x30]);
        using var surface = Render(image, mask);
        Assert.Equal(204, surface[50, 50].A);
    }

    /// <summary>Proves a 1-bit base image is unpacked MSB first and scaled to full range.</summary>
    [Fact]
    public void PdfDocument_Images_RawOneBitGray_UnpacksSamples()
    {
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 1",
            [0b0100_0000]);
        using var surface = Render(image);
        AssertNear(new Rgba32(0, 0, 0, 255), surface[25, 50], 0);
        AssertNear(new Rgba32(255, 255, 255, 255), surface[75, 50], 0);
    }

    /// <summary>Proves rows of a sub-byte image are padded to whole bytes independently (width 9, height 2).</summary>
    [Fact]
    public void PdfDocument_Images_RawOneBitMultiRowPadding_UnpacksEachRowFromItsOwnByte()
    {
        // Row 0: sample 0 white; row 1: sample 8 white (first bit of its second byte). Padding bits are ignored.
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 9 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 1",
            [0x80, 0x00, 0x00, 0x80]);
        using var surface = Render(image);
        var black = new Rgba32(0, 0, 0, 255);
        var white = new Rgba32(255, 255, 255, 255);
        AssertNear(white, surface[5, 25], 0);
        AssertNear(black, surface[16, 25], 0);
        AssertNear(black, surface[94, 25], 0);
        AssertNear(black, surface[5, 75], 0);
        AssertNear(black, surface[83, 75], 0);
        AssertNear(white, surface[94, 75], 0);
    }

    /// <summary>Proves a 4-bit DeviceRGB base image keeps its three components per pixel.</summary>
    [Fact]
    public void PdfDocument_Images_RawFourBitRgb_UnpacksComponents()
    {
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 4",
            [0xF0, 0x00, 0xFF]);
        using var surface = Render(image);
        AssertNear(new Rgba32(255, 0, 0, 255), surface[25, 50], 0);
        AssertNear(new Rgba32(0, 255, 255, 255), surface[75, 50], 0);
    }

    /// <summary>Proves a 2-bit DeviceRGB base image is padded per row (3 samples of 2 bits fit in one byte, 2 rows).</summary>
    [Fact]
    public void PdfDocument_Images_RawTwoBitRgbTwoRows_PadsEachRow()
    {
        // 1 pixel wide: 3 samples x 2 bits = 6 bits, padded to one byte per row (the two padding bits are ignored).
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 2",
            [0b1100_0011, 0b0011_1100]);
        using var surface = Render(image);
        AssertNear(new Rgba32(255, 0, 0, 255), surface[50, 25], 0);
        AssertNear(new Rgba32(0, 255, 255, 255), surface[50, 75], 0);
    }

    /// <summary>Proves 16-bit gray and RGB base images keep the high byte of each sample.</summary>
    [Fact]
    public void PdfDocument_Images_RawSixteenBit_UsesHighBytes()
    {
        var gray = StreamObject(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 16",
            [0x80, 0xFF, 0x40, 0x00]);
        using (var surface = Render(gray))
        {
            AssertNear(new Rgba32(128, 128, 128, 255), surface[25, 50], 0);
            AssertNear(new Rgba32(64, 64, 64, 255), surface[75, 50], 0);
        }

        var rgb = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 16",
            [0xFF, 0x00, 0x80, 0xAA, 0x00, 0xFF]);
        using var rgbSurface = Render(rgb);
        AssertNear(new Rgba32(255, 128, 0, 255), rgbSurface[50, 50], 0);
    }

    /// <summary>Proves a 4-bit /Indexed image looks up raw nibble indices and pads its row.</summary>
    [Fact]
    public void PdfDocument_Images_RawFourBitIndexed_LooksUpPalette()
    {
        // Three 4-bit indices (0, 1, 2) in two bytes; the last nibble is row padding.
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 3 /Height 1 "
            + "/ColorSpace [/Indexed /DeviceRGB 2 <FF000000FF000000FF>] /BitsPerComponent 4",
            [0x01, 0x20]);
        using var surface = Render(image);
        AssertNear(new Rgba32(255, 0, 0, 255), surface[16, 50], 0);
        AssertNear(new Rgba32(0, 255, 0, 255), surface[50, 50], 0);
        AssertNear(new Rgba32(0, 0, 255, 255), surface[83, 50], 0);
    }

    /// <summary>Proves a truncated sub-byte image fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_RawOneBitTruncated_ThrowsInvalidDataException()
    {
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 9 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 1",
            [0xFF, 0x80, 0xFF]);
        Assert.Throws<InvalidDataException>(() => Render(image));
    }

    /// <summary>Proves a 16-bit /Indexed image is rejected.</summary>
    [Fact]
    public void PdfDocument_Images_IndexedSixteenBit_ThrowsUnsupportedImageFeatureException()
    {
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace [/Indexed /DeviceRGB 1 <FF000000FF00>] /BitsPerComponent 16",
            [0, 0]);
        Assert.Throws<UnsupportedImageFeatureException>(() => Render(image));
    }

    /// <summary>Builds a 1x1 raw DeviceGray image object (object 5) with extra dictionary entries.</summary>
    private static byte[] RawGrayImage(string entries, byte[] samples) =>
        StreamObject(
            $"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 {entries}",
            samples);

    /// <summary>Proves JPXDecode combined with another filter fails closed.</summary>
    [Fact]
    public void PdfDocument_Images_Jpx_CombinedWithAnotherFilter_ThrowsInvalidDataException()
    {
        var jpx = Jp2(Flat(8, 8, 1), new J2kJp2Options { EnumCs = 17 });
        var image = StreamObject(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /BitsPerComponent 8 /Filter [/ASCIIHexDecode /JPXDecode]",
            Encoding.ASCII.GetBytes(Convert.ToHexString(jpx) + ">"));
        Assert.Throws<InvalidDataException>(() => Render(image));
    }

    /// <summary>Builds a four-component JP2 (color + opacity via cdef) with flat component values.</summary>
    private static byte[] AlphaJp2(int r, int g, int b, int a, int type) =>
        Jp2(
            Flat(8, 8, r, g, b, a),
            new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3), (3, type, 0)] });
}
