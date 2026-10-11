using System.Text;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     End-to-end, pixel-exact tests of the per-pixel image color conversion (<c>DeviceGray</c>,
///     <c>DeviceRGB</c>, <c>DeviceCMYK</c> and <c>/Indexed</c> samples): a raster image drawn 1:1 is an
///     exact copy of its converted samples, so every expected pixel below is checked exactly.
/// </summary>
public class PdfDocumentImageColorConversionTests
{
    private const int ImageWidth = 4;

    private static readonly PdfRenderOptions Opaque = new() { BackgroundColor = new(1, 2, 3, 255) };

    /// <summary>Builds a one-page PDF (4 x 1 points) drawing a 4 x 1 8-bit image 1:1.</summary>
    private static byte[] BuildPdf(string colorSpace, byte[] samples)
    {
        var content = Encoding.ASCII.GetBytes("4 0 0 1 0 0 cm /Im0 Do");
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 4 1] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >>"u8.ToArray(),
            PdfDocumentImageDownscaleTests.Stream("<< /Length " + content.Length + " >>", content),
            PdfDocumentImageDownscaleTests.Stream(
                $"<< /Type /XObject /Subtype /Image /Width {ImageWidth} /Height 1 /ColorSpace {colorSpace} /BitsPerComponent 8 /Length {samples.Length} >>",
                samples),
        };

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

    /// <summary>Renders the image and returns its four pixels as <c>(R, G, B)</c> triples.</summary>
    private static (int R, int G, int B)[] RenderPixels(string colorSpace, byte[] samples)
    {
        using var document = PdfDocument.Open(new MemoryStream(BuildPdf(colorSpace, samples)));
        using var surface = document.Render(0, ImageWidth, 1, Opaque);
        var pixels = new (int, int, int)[ImageWidth];
        for (var x = 0; x < ImageWidth; x++)
        {
            var p = surface[x, 0];
            Assert.Equal(255, p.A);
            pixels[x] = (p.R, p.G, p.B);
        }

        return pixels;
    }

    /// <summary>Proves that 8-bit <c>DeviceGray</c> samples become opaque gray pixels.</summary>
    [Fact]
    public void PdfDocument_ImageColor_DeviceGray_ConvertsEachSampleExactly()
    {
        // Arrange / Act: four gray samples drawn 1:1
        var pixels = RenderPixels("/DeviceGray", [0, 64, 128, 255]);

        // Assert: each pixel is the sample replicated into R, G and B
        (int, int, int)[] expected = [(0, 0, 0), (64, 64, 64), (128, 128, 128), (255, 255, 255)];
        Assert.Equal(expected, pixels);
    }

    /// <summary>Proves that 8-bit <c>DeviceRGB</c> samples are copied unchanged.</summary>
    [Fact]
    public void PdfDocument_ImageColor_DeviceRgb_ConvertsEachSampleExactly()
    {
        // Arrange / Act
        var pixels = RenderPixels("/DeviceRGB", [255, 0, 0, 10, 20, 30, 0, 255, 128, 7, 8, 9]);

        // Assert
        (int, int, int)[] expected = [(255, 0, 0), (10, 20, 30), (0, 255, 128), (7, 8, 9)];
        Assert.Equal(expected, pixels);
    }

    /// <summary>Proves that 8-bit <c>DeviceCMYK</c> samples use the naive <c>255 * (1 - c) * (1 - k)</c> conversion.</summary>
    [Fact]
    public void PdfDocument_ImageColor_DeviceCmyk_ConvertsEachSampleExactly()
    {
        // Arrange / Act: white, pure cyan, pure black key, and pure magenta
        var pixels = RenderPixels("/DeviceCMYK", [0, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 255, 0, 255, 0, 0]);

        // Assert
        (int, int, int)[] expected = [(255, 255, 255), (0, 255, 255), (0, 0, 0), (255, 0, 255)];
        Assert.Equal(expected, pixels);
    }

    /// <summary>Proves that <c>/Indexed</c> samples look up their palette entries exactly.</summary>
    [Fact]
    public void PdfDocument_ImageColor_Indexed_LooksUpEachPaletteEntryExactly()
    {
        // Arrange / Act: a four-entry RGB palette, sampled out of order
        var pixels = RenderPixels(
            "[/Indexed /DeviceRGB 3 <FF000000FF000000FF102030>]",
            [3, 0, 2, 1]);

        // Assert
        (int, int, int)[] expected = [(16, 32, 48), (255, 0, 0), (0, 0, 255), (0, 255, 0)];
        Assert.Equal(expected, pixels);
    }
}
