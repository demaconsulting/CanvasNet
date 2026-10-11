using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     End-to-end tests proving <see cref="PdfDocument"/> area-averages (rather than point-samples)
///     a raster image that is shrunk on the page, so one-pixel lines are never dropped, and that
///     enlarged images keep their exact nearest-neighbor blocks. Timing is deliberately not asserted.
/// </summary>
public class PdfDocumentImageDownscaleTests
{
    private const int SourceWidth = 1940;
    private const int SourceHeight = 4;
    private const int FirstLine = 100;
    private const int LineSpacing = 24;

    private static readonly PdfRenderOptions White = new() { BackgroundColor = new(255, 255, 255, 255) };

    /// <summary>Builds a one-page PDF drawing a raw 8-bit gray image through <paramref name="cm"/>.</summary>
    private static byte[] BuildPdf(int pageWidth, int pageHeight, string cm, int imageWidth, int imageHeight, byte[] gray)
    {
        var content = Encoding.ASCII.GetBytes($"{cm} /Im0 Do");
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"u8.ToArray(),
            Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >>"),
            Stream("<< /Length " + content.Length + " >>", content),
            Stream($"<< /Type /XObject /Subtype /Image /Width {imageWidth} /Height {imageHeight} /ColorSpace /DeviceGray /BitsPerComponent 8 /Length {gray.Length} >>", gray),
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

    /// <summary>Wraps a dictionary and payload as a stream object body.</summary>
    private static byte[] Stream(string dictionary, byte[] data)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes(dictionary + "\nstream\n"));
        bytes.AddRange(data);
        bytes.AddRange("\nendstream"u8.ToArray());
        return [.. bytes];
    }

    /// <summary>Creates the white gray image with one-pixel black vertical lines.</summary>
    private static byte[] LinesImage()
    {
        var gray = new byte[SourceWidth * SourceHeight];
        Array.Fill(gray, (byte)255);
        for (var c = FirstLine; c < SourceWidth; c += LineSpacing)
        {
            for (var y = 0; y < SourceHeight; y++)
            {
                gray[(y * SourceWidth) + c] = 0;
            }
        }

        return gray;
    }

    /// <summary>Renders the lines page at <paramref name="width"/> pixels wide.</summary>
    private static Surface RenderLines(string cm, int width)
    {
        var pdf = BuildPdf(SourceWidth, 20, cm, SourceWidth, SourceHeight, LinesImage());
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        return document.Render(0, width, Math.Max(8, width * 20 / SourceWidth), White);
    }

    /// <summary>Asserts every source line leaves roughly its area-proportional darkness near its mapped column.</summary>
    private static void AssertAllLinesPresent(Surface surface, int width, bool mirrored)
    {
        var y = surface.Height / 2;
        var scale = (double)width / SourceWidth;
        var expected = 255.0 * scale;
        for (var c = FirstLine; c < SourceWidth; c += LineSpacing)
        {
            var sourceColumn = mirrored ? SourceWidth - 1 - c : c;
            var center = (int)Math.Floor((sourceColumn + 0.5) * scale);
            double darkness = 0;
            for (var x = Math.Max(0, center - 2); x <= Math.Min(width - 1, center + 2); x++)
            {
                darkness += 255 - surface[x, y].R;
            }

            Assert.True(darkness >= 0.7 * expected, $"line at source column {c} lost at width {width}: darkness {darkness:F1}, expected about {expected:F1}");
        }
    }

    /// <summary>One-pixel lines survive reduction at several non-integer ratios.</summary>
    [Theory]
    [InlineData(765)]
    [InlineData(1020)]
    [InlineData(1108)]
    [InlineData(1303)]
    public void Render_ShrunkImageWithOnePixelLines_KeepsEveryLine(int width)
    {
        using var surface = RenderLines("1940 0 0 20 0 0 cm", width);
        AssertAllLinesPresent(surface, width, mirrored: false);
    }

    /// <summary>A horizontally flipped placement filters the same way.</summary>
    [Fact]
    public void Render_ShrunkFlippedImage_KeepsEveryLine()
    {
        using var surface = RenderLines("-1940 0 0 20 1940 0 cm", 1020);
        AssertAllLinesPresent(surface, 1020, mirrored: true);
    }

    /// <summary>A 90-degree rotated placement shrinks along the page's vertical axis without throwing and keeps the lines.</summary>
    [Fact]
    public void Render_ShrunkRotatedImage_KeepsEveryLine()
    {
        // Rotate 90 degrees: image x runs up the page. Page is 20 wide by 1940 tall.
        var pdf = BuildPdf(20, SourceWidth, $"0 {SourceWidth} -20 0 20 0 cm", SourceWidth, SourceHeight, LinesImage());
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        using var surface = document.Render(0, 8, 765, White);

        var scale = 765.0 / SourceWidth;
        var x = 4;
        for (var c = FirstLine; c < SourceWidth; c += LineSpacing)
        {
            // Image column c maps to page y = c, which is device y = (1940 - c) * scale.
            var center = (int)Math.Floor((SourceWidth - (c + 0.5)) * scale);
            double darkness = 0;
            for (var y = Math.Max(0, center - 2); y <= Math.Min(764, center + 2); y++)
            {
                darkness += 255 - surface[x, y].R;
            }

            Assert.True(darkness >= 0.7 * 255.0 * scale, $"rotated line at source column {c} lost");
        }
    }

    /// <summary>An enlarged image is still painted as exact nearest-neighbor blocks (no blending).</summary>
    [Fact]
    public void Render_EnlargedImage_RemainsExactBlocks()
    {
        var pdf = BuildPdf(400, 10, "400 0 0 10 0 0 cm", 4, 1, [0, 80, 160, 240]);
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        using var surface = document.Render(0, 400, 10, White);

        Assert.Equal(0, surface[99, 5].R);
        Assert.Equal(80, surface[100, 5].R);
        Assert.Equal(80, surface[199, 5].R);
        Assert.Equal(160, surface[200, 5].R);
        Assert.Equal(240, surface[399, 5].R);
    }
}
