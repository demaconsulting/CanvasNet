using System.Globalization;
using System.IO.Compression;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Tests of the CIE-based color spaces (<c>CalGray</c>, <c>CalRGB</c>, <c>Lab</c>), the
///     <c>/ICCBased</c> fallback hardening (<c>/N</c> authoritative, no profile parsing), and their
///     use for operators, images, <c>/Indexed</c> palettes and shadings. Expected values are
///     computed in the tests with an independent double-precision reference (published Lindbloom
///     Bradford D50-to-D65 constants and the sRGB matrix/transfer function), never taken from
///     another viewer's output; the exact invariants (white, black, primaries) are asserted exactly.
/// </summary>
public class PdfDocumentColorSpaceTests
{
    private const string D50 = "[0.9642 1.0 0.8249]";
    private const string D65 = "[0.9505 1.0 1.089]";
    private const string SrgbMatrix = "[0.4124564 0.2126729 0.0193339 0.3575761 0.7151522 0.1191920 0.1804375 0.0721750 0.9503041]";

    private static readonly Rgba32 Background = new(1, 2, 3, 255);

    // ---- helpers ----

    /// <summary>Builds a one-page PDF; objects 5.. are <paramref name="extraBodies"/> in order.</summary>
    private static byte[] BuildPdf(int width, int height, string content, string resources, params byte[][] extraBodies)
    {
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"u8.ToArray(),
            Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Contents 4 0 R /Resources << {resources} >> >>"),
            Stream($"<< /Length {contentBytes.Length} >>", contentBytes),
        };
        bodies.AddRange(extraBodies);

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    private static byte[] Stream(string dictionary, byte[] data)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encoding.ASCII.GetBytes(dictionary + "\nstream\n"));
        buffer.AddRange(data);
        buffer.AddRange("\nendstream"u8.ToArray());
        return [.. buffer];
    }

    private static byte[] ImageObject(int width, int height, string colorSpace, byte[] samples, string extra = "")
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(samples);
        }

        var data = compressed.ToArray();
        return Stream(
            $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace {colorSpace} " +
            $"/BitsPerComponent 8 /Filter /FlateDecode {extra} /Length {data.Length} >>",
            data);
    }

    private static Surface Render(byte[] pdf, int width = 100, int height = 100)
    {
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        return document.Render(0, width, height, new PdfRenderOptions { BackgroundColor = Background });
    }

    /// <summary>Renders a 100 x 100 page that paints an 80 x 80 square with <paramref name="paint"/> and returns the center pixel.</summary>
    private static Rgba32 PaintCenter(string colorSpaceEntry, string paint, params byte[][] extra)
    {
        var pdf = BuildPdf(100, 100, paint + " 10 10 80 80 re f", $"/ColorSpace << /CS0 {colorSpaceEntry} >>", extra);
        using var surface = Render(pdf);
        return surface[50, 50];
    }

    private static void AssertNear(Rgba32 expected, Rgba32 actual, int tolerance)
    {
        Assert.InRange(actual.R, Math.Max(0, expected.R - tolerance), Math.Min(255, expected.R + tolerance));
        Assert.InRange(actual.G, Math.Max(0, expected.G - tolerance), Math.Min(255, expected.G + tolerance));
        Assert.InRange(actual.B, Math.Max(0, expected.B - tolerance), Math.Min(255, expected.B + tolerance));
        Assert.Equal(255, actual.A);
    }

    private static byte Encode(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        var v = c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055;
        return (byte)Math.Round(v * 255);
    }

    /// <summary>Independent reference: Lab (D50 white, Lindbloom Bradford D50-to-D65) to sRGB.</summary>
    private static Rgba32 ReferenceLabD50(double l, double a, double b)
    {
        double[] white = [0.96422, 1.0, 0.82521];
        var fy = (l + 16) / 116;
        var fx = fy + (a / 500);
        var fz = fy - (b / 200);
        static double Inv(double t) => t * t * t > 0.008856 ? t * t * t : (116 * t - 16) / 903.3;
        var x = white[0] * Inv(fx);
        var y = white[1] * (l > 8 ? fy * fy * fy : l / 903.3);
        var z = white[2] * Inv(fz);

        // Bradford D50 -> D65, then XYZ(D65) -> linear sRGB.
        var x65 = (0.9555766 * x) + (-0.0230393 * y) + (0.0631636 * z);
        var y65 = (-0.0282895 * x) + (1.0099416 * y) + (0.0210077 * z);
        var z65 = (0.0122982 * x) + (-0.0204830 * y) + (1.3299098 * z);
        var r = (3.2404542 * x65) + (-1.5371385 * y65) + (-0.4985314 * z65);
        var g = (-0.9692660 * x65) + (1.8760108 * y65) + (0.0415560 * z65);
        var bl = (0.0556434 * x65) + (-0.2040259 * y65) + (1.0572252 * z65);
        return new Rgba32(Encode(r), Encode(g), Encode(bl), 255);
    }

    // ---- operators ----

    /// <summary>Proves the exact Lab invariants: white maps to white (under D50 and D65) and black to black.</summary>
    [Theory]
    [InlineData(D50, "100 0 0", 255)]
    [InlineData(D65, "100 0 0", 255)]
    [InlineData(D50, "0 0 0", 0)]
    [InlineData(D65, "0 0 0", 0)]
    public void PdfDocument_ColorSpace_Lab_WhiteAndBlack_MapExactly(string white, string components, int expected)
    {
        // Arrange / Act
        var color = PaintCenter($"[/Lab << /WhitePoint {white} /Range [-128 127 -128 127] >>]", $"/CS0 cs {components} sc");

        // Assert
        Assert.Equal(new Rgba32((byte)expected, (byte)expected, (byte)expected, 255), color);
    }

    /// <summary>Proves a chromatic Lab color matches the independent reference within one level of rounding slack.</summary>
    [Theory]
    [InlineData(50, 40, -30)]
    [InlineData(70, -50, 60)]
    [InlineData(30, 20, 20)]
    public void PdfDocument_ColorSpace_Lab_ChromaticColor_MatchesReference(double l, double a, double b)
    {
        // Arrange / Act
        var color = PaintCenter(
            $"[/Lab << /WhitePoint {D50} /Range [-128 127 -128 127] >>]",
            string.Create(CultureInfo.InvariantCulture, $"/CS0 cs {l} {a} {b} sc"));

        // Assert
        AssertNear(ReferenceLabD50(l, a, b), color, 2);
    }

    /// <summary>Proves Lab a*/b* operands outside /Range are clamped to it.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_Lab_OutOfRangeAB_ClampsToRange()
    {
        // Arrange / Act: a* = 90 is clamped to the declared +20 maximum.
        var clamped = PaintCenter($"[/Lab << /WhitePoint {D50} /Range [-20 20 -20 20] >>]", "/CS0 cs 60 90 0 sc");
        var atMax = PaintCenter($"[/Lab << /WhitePoint {D50} /Range [-20 20 -20 20] >>]", "/CS0 cs 60 20 0 sc");

        // Assert
        Assert.Equal(atMax, clamped);
    }

    /// <summary>Proves CalGray 0 and 1 map exactly to black and white, and that a mid value follows its gamma.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_CalGray_EndpointsExactAndMidFollowsGamma()
    {
        // Arrange
        var space = $"[/CalGray << /WhitePoint {D65} /Gamma 2.2 >>]";

        // Act
        var black = PaintCenter(space, "/CS0 cs 0 sc");
        var white = PaintCenter(space, "/CS0 cs 1 sc");
        var mid = PaintCenter(space, "/CS0 cs 0.5 sc");

        // Assert: neutral axis, expected value from the spec formula Y = A^gamma then the sRGB curve
        Assert.Equal(new Rgba32(0, 0, 0, 255), black);
        Assert.Equal(new Rgba32(255, 255, 255, 255), white);
        var expected = Encode(Math.Pow(0.5, 2.2));
        AssertNear(new Rgba32(expected, expected, expected, 255), mid, 1);
    }

    /// <summary>Proves a CalRGB space with sRGB primaries and a D65 white maps its primaries and white exactly like DeviceRGB.</summary>
    [Theory]
    [InlineData("1 0 0", 255, 0, 0)]
    [InlineData("0 1 0", 0, 255, 0)]
    [InlineData("0 0 1", 0, 0, 255)]
    [InlineData("1 1 1", 255, 255, 255)]
    [InlineData("0 0 0", 0, 0, 0)]
    public void PdfDocument_ColorSpace_CalRgb_SrgbPrimaries_MatchDeviceRgb(string components, int r, int g, int b)
    {
        // Arrange / Act
        var color = PaintCenter(
            $"[/CalRGB << /WhitePoint [0.95047 1.0 1.08883] /Gamma [2.2 2.2 2.2] /Matrix {SrgbMatrix} >>]",
            $"/CS0 cs {components} sc");

        // Assert
        AssertNear(new Rgba32((byte)r, (byte)g, (byte)b, 255), color, 1);
    }

    /// <summary>Proves a CalRGB mid-gray with linear gamma is encoded by the sRGB transfer function (0.5 linear becomes 188).</summary>
    [Fact]
    public void PdfDocument_ColorSpace_CalRgb_LinearGamma_AppliesSrgbTransferFunction()
    {
        // Arrange / Act
        var color = PaintCenter(
            $"[/CalRGB << /WhitePoint [0.95047 1.0 1.08883] /Matrix {SrgbMatrix} >>]",
            "/CS0 cs 0.5 0.5 0.5 sc");

        // Assert
        AssertNear(new Rgba32(188, 188, 188, 255), color, 1);
    }

    /// <summary>Proves the stroke operators (CS/SC) use the CIE-based conversion too.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_CalGray_StrokeOperators_UseConversion()
    {
        // Arrange: a thick vertical stroke in CalGray 1 (white) on the background
        var pdf = BuildPdf(
            100,
            100,
            "/CS0 CS 1 SC 20 w 50 0 m 50 100 l S",
            $"/ColorSpace << /CS0 [/CalGray << /WhitePoint {D65} >>] >>");

        // Act
        using var surface = Render(pdf);

        // Assert
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[50, 50]);
        Assert.Equal(Background, surface[10, 50]);
    }

    /// <summary>Proves the initial color after cs is black for each CIE-based space.</summary>
    [Theory]
    [InlineData("[/CalGray << /WhitePoint [0.9505 1.0 1.089] >>]")]
    [InlineData("[/CalRGB << /WhitePoint [0.9505 1.0 1.089] >>]")]
    [InlineData("[/Lab << /WhitePoint [0.9505 1.0 1.089] >>]")]
    public void PdfDocument_ColorSpace_CalibratedSpaces_InitialColorIsBlack(string space)
    {
        // Arrange / Act
        var color = PaintCenter(space, "/CS0 cs");

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 255), color);
    }

    // ---- malformed dictionaries ----

    /// <summary>Proves malformed CIE-based dictionaries throw <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("[/Lab << >>]")]
    [InlineData("[/CalGray << >>]")]
    [InlineData("[/CalRGB << /WhitePoint [0.95 1.0] >>]")]
    [InlineData("[/CalRGB << /WhitePoint [0.95 1.0 1.09] /Gamma [-1 1 1] >>]")]
    [InlineData("[/CalRGB << /WhitePoint [0.95 1.0 1.09] /Matrix [1 0 0] >>]")]
    [InlineData("[/CalGray << /WhitePoint [0.95 1.0 1.09] /Gamma 0 >>]")]
    [InlineData("[/Lab << /WhitePoint [0.95 1.0 1.09] /Range [10 -10 0 0] >>]")]
    [InlineData("[/Lab]")]
    public void PdfDocument_ColorSpace_MalformedCalibratedDictionary_ThrowsInvalidDataException(string space)
    {
        // Arrange
        var pdf = BuildPdf(100, 100, "/CS0 cs", $"/ColorSpace << /CS0 {space} >>");

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(pdf));
    }

    // ---- images ----

    /// <summary>Proves a default-decode 8-bit Lab image maps its normalized samples across L 0..100 and the /Range.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_LabImage_DefaultDecode_MapsNormalizedSamples()
    {
        // Arrange: 2x1 image, drawn 1:1; samples (255,128,128) ~ white and (0,128,128) ~ black
        // (a*, b* of 128/255 across [-100, 100] is +0.39: essentially neutral).
        var samples = new byte[] { 255, 128, 128, 0, 128, 128 };
        var pdf = BuildPdf(
            2,
            1,
            "2 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(2, 1, $"[/Lab << /WhitePoint {D50} >>]", samples));

        // Act
        using var surface = Render(pdf, 2, 1);

        // Assert
        AssertNear(ReferenceLabD50(100, -100 + (128 / 255.0 * 200), -100 + (128 / 255.0 * 200)), surface[0, 0], 2);
        AssertNear(ReferenceLabD50(0, -100 + (128 / 255.0 * 200), -100 + (128 / 255.0 * 200)), surface[1, 0], 2);
    }

    /// <summary>Proves a chromatic Lab image sample matches the reference after rescaling to the /Range.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_LabImage_ChromaticSample_MatchesReference()
    {
        // Arrange: L = 200/255*100, a = 255 -> +127, b = 0 -> -128 with /Range [-128 127 -128 127]
        var pdf = BuildPdf(
            1,
            1,
            "1 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(1, 1, $"[/Lab << /WhitePoint {D50} /Range [-128 127 -128 127] >>]", [200, 255, 0]));

        // Act
        using var surface = Render(pdf, 1, 1);

        // Assert
        AssertNear(ReferenceLabD50(200 / 255.0 * 100, 127, -128), surface[0, 0], 2);
    }

    /// <summary>Proves an explicit /Decode array on a Lab image fails closed with feature pdf-image-lab-decode.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_LabImage_ExplicitDecode_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var pdf = BuildPdf(
            1,
            1,
            "1 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(1, 1, $"[/Lab << /WhitePoint {D50} >>]", [128, 128, 128], "/Decode [0 100 -100 100 -100 100]"));

        // Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => Render(pdf, 1, 1));
        Assert.Equal("pdf-image-lab-decode", exception.Feature);
    }

    /// <summary>Proves a 1-component CalGray image converts each sample exactly like the equivalent sc operand.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_CalGrayImage_ConvertsSamplesLikeOperator()
    {
        // Arrange
        var space = $"[/CalGray << /WhitePoint {D65} /Gamma 2.2 >>]";
        var samples = new byte[] { 0, 64, 128, 255 };
        var pdf = BuildPdf(
            4,
            1,
            "4 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(4, 1, space, samples));

        // Act
        using var surface = Render(pdf, 4, 1);

        // Assert: each pixel equals the operator-painted color for the normalized sample
        for (var x = 0; x < samples.Length; x++)
        {
            var expected = PaintCenter(space, string.Create(CultureInfo.InvariantCulture, $"/CS0 cs {samples[x] / 255.0:R} sc"));
            Assert.Equal(expected, surface[x, 0]);
        }
    }

    /// <summary>Proves a CalRGB image with sRGB primaries copies its samples (primaries/white exact).</summary>
    [Fact]
    public void PdfDocument_ColorSpace_CalRgbImage_SrgbPrimaries_MatchesDeviceRgbEndpoints()
    {
        // Arrange: red, green, blue, white; the repeated pixel exercises the previous-pixel memo
        var pdf = BuildPdf(
            5,
            1,
            "5 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(
                5,
                1,
                $"[/CalRGB << /WhitePoint [0.95047 1.0 1.08883] /Gamma [2.2 2.2 2.2] /Matrix {SrgbMatrix} >>]",
                [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255, 255, 255, 255]));

        // Act
        using var surface = Render(pdf, 5, 1);

        // Assert
        AssertNear(new Rgba32(255, 0, 0, 255), surface[0, 0], 1);
        AssertNear(new Rgba32(0, 255, 0, 255), surface[1, 0], 1);
        AssertNear(new Rgba32(0, 0, 255, 255), surface[2, 0], 1);
        AssertNear(new Rgba32(255, 255, 255, 255), surface[3, 0], 1);
        Assert.Equal(surface[3, 0], surface[4, 0]);
    }

    /// <summary>Proves a CalGray image is not accepted as an /SMask (only DeviceGray is).</summary>
    [Fact]
    public void PdfDocument_ColorSpace_CalGrayImage_AsSoftMask_ThrowsInvalidDataException()
    {
        // Arrange: object 5 = base image with /SMask 6 0 R; object 6 = CalGray mask
        var pdf = BuildPdf(
            1,
            1,
            "1 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(1, 1, "/DeviceRGB", [255, 0, 0], "/SMask 6 0 R"),
            ImageObject(1, 1, $"[/CalGray << /WhitePoint {D65} >>]", [255]));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(pdf, 1, 1));
    }

    /// <summary>Proves a DeviceGray image is still accepted as an /SMask.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_DeviceGrayImage_AsSoftMask_StillApplies()
    {
        // Arrange
        var pdf = BuildPdf(
            1,
            1,
            "1 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(1, 1, "/DeviceRGB", [255, 0, 0], "/SMask 6 0 R"),
            ImageObject(1, 1, "/DeviceGray", [0]));

        // Act
        using var surface = Render(pdf, 1, 1);

        // Assert: a fully transparent mask leaves the background
        Assert.Equal(Background, surface[0, 0]);
    }

    // ---- indexed ----

    /// <summary>Proves an /Indexed palette over Lab normalizes its bytes to the Lab ranges (via scn).</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IndexedOverLab_Scn_NormalizesPaletteBytes()
    {
        // Arrange: palette entry 1 = (255, 128, 128) -> L 100, a/b ~ +0.39
        var color = PaintCenter(
            $"[/Indexed [/Lab << /WhitePoint {D50} >>] 1 <000000FF8080>]",
            "/CS0 cs 1 sc");

        // Assert
        AssertNear(ReferenceLabD50(100, -100 + (128 / 255.0 * 200), -100 + (128 / 255.0 * 200)), color, 2);
    }

    /// <summary>Proves an /Indexed image over CalRGB looks up and converts each palette entry.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IndexedOverCalRgb_Image_ConvertsPalette()
    {
        // Arrange: 2-entry palette: red, blue; image indices [1, 0]
        var pdf = BuildPdf(
            2,
            1,
            "2 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            ImageObject(
                2,
                1,
                $"[/Indexed [/CalRGB << /WhitePoint [0.95047 1.0 1.08883] /Gamma [2.2 2.2 2.2] /Matrix {SrgbMatrix} >>] 1 <FF00000000FF>]",
                [1, 0]));

        // Act
        using var surface = Render(pdf, 2, 1);

        // Assert
        AssertNear(new Rgba32(0, 0, 255, 255), surface[0, 0], 1);
        AssertNear(new Rgba32(255, 0, 0, 255), surface[1, 0], 1);
    }

    // ---- ICC fallback ----

    private static byte[] Icc(string entries) => Stream($"<< {entries} /Length 1 >>", [0x00]);

    /// <summary>Proves /ICCBased with /N 1 and no /Alternate resolves as DeviceGray, and /N 4 as DeviceCMYK.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IccBased_N1AndN4_ResolveAsDeviceSpaces()
    {
        // Arrange / Act
        var gray = PaintCenter("[/ICCBased 5 0 R]", "/CS0 cs 0.5 sc", Icc("/N 1"));
        var cmykBlack = PaintCenter("[/ICCBased 5 0 R]", "/CS0 cs 0 0 0 1 sc", Icc("/N 4"));

        // Assert
        Assert.Equal(new Rgba32(128, 128, 128, 255), gray);
        Assert.Equal(new Rgba32(0, 0, 0, 255), cmykBlack);
    }

    /// <summary>Proves a fractional /N (1.5) is malformed and never truncated to 1: with no usable /Alternate it fails closed, and an integral real /N (3.0) is still accepted.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IccBased_FractionalN_RejectedAndIntegralRealAccepted()
    {
        // Arrange
        var pdf = BuildPdf(100, 100, "/CS0 cs", "/ColorSpace << /CS0 [/ICCBased 5 0 R] >>", Icc("/N 1.5"));

        // Act
        var rgb = PaintCenter("[/ICCBased 5 0 R]", "/CS0 cs 0 0 1 sc", Icc("/N 3.0"));

        // Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => Render(pdf));
        Assert.Equal(new Rgba32(0, 0, 255, 255), rgb);
    }

    /// <summary>Proves a fractional /Indexed /Hival (1.5) is rejected instead of being truncated to 1.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_Indexed_FractionalHival_ThrowsInvalidDataException()
    {
        // Arrange
        var pdf = BuildPdf(
            100, 100, "/CS0 cs 0 sc", "/ColorSpace << /CS0 [/Indexed /DeviceRGB 1.5 <FF000000FF00>] >>", []);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(pdf));
    }

    /// <summary>Proves a bogus /N with no usable /Alternate fails closed.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IccBased_BogusN_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var pdf = BuildPdf(100, 100, "/CS0 cs", "/ColorSpace << /CS0 [/ICCBased 5 0 R] >>", Icc("/N 2"));

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => Render(pdf));
    }

    /// <summary>Proves /N is authoritative: an /Alternate with a different component count is ignored.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IccBased_NMismatchesAlternate_PrefersN()
    {
        // Arrange: /N 3 but /Alternate /DeviceGray; 3 operands only work if /N won (DeviceRGB)
        var color = PaintCenter("[/ICCBased 5 0 R]", "/CS0 cs 0 0 1 sc", Icc("/N 3 /Alternate /DeviceGray"));

        // Assert
        Assert.Equal(new Rgba32(0, 0, 255, 255), color);
    }

    /// <summary>Proves a Lab /Alternate with /N 3 is used (the conversion is Lab, not DeviceRGB).</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IccBased_LabAlternateWithN3_UsesLab()
    {
        // Arrange: Lab L = 50 is a mid gray; DeviceRGB would clamp 50 to white
        var color = PaintCenter(
            "[/ICCBased 5 0 R]",
            "/CS0 cs 50 0 0 sc",
            Icc($"/N 3 /Alternate [/Lab << /WhitePoint {D50} /Range [-128 127 -128 127] >>]"));

        // Assert
        AssertNear(ReferenceLabD50(50, 0, 0), color, 2);
    }

    /// <summary>Proves a CalGray /Alternate with /N 1 is used.</summary>
    [Fact]
    public void PdfDocument_ColorSpace_IccBased_CalGrayAlternateWithN1_UsesCalGray()
    {
        // Arrange
        var color = PaintCenter(
            "[/ICCBased 5 0 R]",
            "/CS0 cs 0.5 sc",
            Icc($"/N 1 /Alternate [/CalGray << /WhitePoint {D65} /Gamma 2.2 >>]"));

        // Assert
        var expected = Encode(Math.Pow(0.5, 2.2));
        AssertNear(new Rgba32(expected, expected, expected, 255), color, 1);
    }

    // ---- shadings ----

    /// <summary>Proves an axial shading in a Lab color space interpolates in natural Lab units (dark to light gray).</summary>
    [Fact]
    public void PdfDocument_ColorSpace_AxialShadingInLab_InterpolatesNaturalUnits()
    {
        // Arrange: L* ramps 0 -> 100 along x; a*/b* stay 0
        var shading =
            $"<< /ShadingType 2 /ColorSpace [/Lab << /WhitePoint {D50} >>] /Coords [0 0 100 0] " +
            "/Function << /FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [100 0 0] /N 1 >> >>";
        var pdf = BuildPdf(100, 100, "/Sh0 sh", "/Shading << /Sh0 5 0 R >>", Encoding.ASCII.GetBytes(shading));

        // Act
        using var surface = Render(pdf);

        // Assert: monotonic lightening, neutral axis, near-white right end
        var left = surface[5, 50];
        var middle = surface[50, 50];
        var right = surface[95, 50];
        Assert.True(left.R < middle.R && middle.R < right.R);
        Assert.InRange(Math.Abs(middle.R - middle.B), 0, 2);
        Assert.True(right.R > 230);
        Assert.True(left.R < 40);
    }

    /// <summary>Proves the 256-sample axial ramp (adjacent converted samples are blended in sRGB) stays within a few levels of converting the exactly interpolated Lab components, on a long ramp with both lightness and chroma changing.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    [InlineData(800)]
    public void PdfDocument_ColorSpace_AxialShadingInLab_StaysNearDirectConversionOfInterpolatedComponents(int x)
    {
        // Arrange: a 1000-pixel ramp (the 256-sample maximum), L* 20 -> 90, a* -60 -> 60, b* 50 -> -50
        var shading =
            $"<< /ShadingType 2 /ColorSpace [/Lab << /WhitePoint {D50} /Range [-128 127 -128 127] >>] /Coords [0 0 1000 0] " +
            "/Function << /FunctionType 2 /Domain [0 1] /C0 [20 -60 50] /C1 [90 60 -50] /N 1 >> >>";
        var pdf = BuildPdf(1000, 100, "/Sh0 sh", "/Shading << /Sh0 5 0 R >>", Encoding.ASCII.GetBytes(shading));

        // Act
        using var surface = Render(pdf, 1000, 100);

        // Assert: the pixel center is at x + 0.5
        var t = (x + 0.5) / 1000.0;
        var expected = ReferenceLabD50(20 + (70 * t), -60 + (120 * t), 50 - (100 * t));
        AssertNear(expected, surface[x, 50], 3);
    }
}
