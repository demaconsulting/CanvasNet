using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Synthetic ink-fidelity tests for ~0.33-unit bars (thin horizontal and vertical rectangle
///     fills, with and without a rectangle clip, and nested even-odd rings). Each case renders at
///     many device sizes so bar edges land at many sub-pixel phases and requires the summed
///     coverage to match the exact geometric area. No timing is asserted.
/// </summary>
public class PdfDocumentThinBarInkTests
{
    private const double PageWidth = 612;
    private const double PageHeight = 792;

    private static readonly (int Width, int Height)[] Sizes =
    [
        (1147, 1484), (1020, 1320), (1108, 1434), (765, 990), (612, 792), (919, 1189), (1303, 1686), (500, 647),
    ];

    private static byte[] BuildPdf(string content)
    {
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 612 792] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>"u8.ToArray(),
            Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream"),
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

    /// <summary>Renders black-on-transparent and returns the summed alpha in pixels.</summary>
    private static double RenderInk(string content, int width, int height)
    {
        using var stream = new MemoryStream(BuildPdf(content));
        using var document = PdfDocument.Open(stream);
        using var surface = document.Render(0, width, height, new PdfRenderOptions { BackgroundColor = new(0, 0, 0, 0) });
        double sum = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                sum += surface[x, y].A;
            }
        }

        return sum / 255.0;
    }

    private static void AssertInk(string content, double areaPoints, double tolerance) =>
        AssertInk(content, areaPoints, tolerance, tolerance);

    private static void AssertInk(string content, double areaPoints, double lowTolerance, double highTolerance)
    {
        foreach (var (width, height) in Sizes)
        {
            var expected = areaPoints * (width / PageWidth) * (height / PageHeight);
            var ink = RenderInk(content, width, height);
            Assert.True(
                ink / expected >= 1 - lowTolerance && ink / expected <= 1 + highTolerance,
                $"{width}x{height}: ink/geometry = {ink / expected:F4}");
        }
    }

    /// <summary>Proves a thin horizontal bar fill (with and without a rectangle clip) paints its exact area.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PdfDocument_ThinBar_HorizontalFill_InkMatchesGeometry(bool clipped)
    {
        var prefix = clipped ? "90 55 640 60 re W n\n" : string.Empty;
        AssertInk(prefix + "0 g 96.1 60.2 500.7 0.33 re f\n", 500.7 * 0.33, 0.02);
    }

    /// <summary>Proves a thin vertical bar fill (with and without a rectangle clip) paints its exact area.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PdfDocument_ThinBar_VerticalFill_InkMatchesGeometry(bool clipped)
    {
        var prefix = clipped ? "90 55 60 640 re W n\n" : string.Empty;
        AssertInk(prefix + "0 g 96.1 60.2 0.33 500.7 re f\n", 500.7 * 0.33, 0.02);
    }

    /// <summary>Proves the overlapping even-odd stacked rectangles paint their exact even-odd area.</summary>
    /// <remarks>
    ///     Four rectangles share one left edge and have bottoms at 65, 63.33, 61.67 and 60 while all
    ///     tops lie beyond the page, so even-odd leaves two 1.67-unit bands visible (63.33-65 and
    ///     60-61.67), each 516 units wide inside the page.
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PdfDocument_ThinBar_OverlappingEvenOddRects_InkMatchesEvenOddArea(bool clipped)
    {
        var prefix = clipped ? "0 0 612 792 re W n\n" : string.Empty;
        var content = prefix + "0 g 96 65 624 930 re 96 61.67 624 934 re 96 63.33 624 932 re 96 60 624 936 re f*\n";
        var area = ((65 - 63.33) + (61.67 - 60)) * (612 - 96);
        AssertInk(content, area, 0.015, 0.03);
    }

    /// <summary>Proves the same stacked rectangles measured as gray-over-white luminance match the exact area.</summary>
    [Fact]
    public void PdfDocument_ThinBar_OverlappingEvenOddRects_LuminanceInkMatchesEvenOddArea()
    {
        const string content = "0.6 g 96 65 624 930 re 96 61.67 624 934 re 96 63.33 624 932 re 96 60 624 936 re f*\n";
        var area = ((65 - 63.33) + (61.67 - 60)) * (612 - 96);
        foreach (var (width, height) in Sizes)
        {
            using var stream = new MemoryStream(BuildPdf(content));
            using var document = PdfDocument.Open(stream);
            using var surface = document.Render(0, width, height, new PdfRenderOptions { BackgroundColor = new(255, 255, 255, 255) });
            double sum = 0;
            for (var y = 0; y < surface.Height; y++)
            {
                for (var x = 0; x < surface.Width; x++)
                {
                    sum += (255 - surface[x, y].R) / (255.0 - 153.0);
                }
            }

            var expected = area * (width / PageWidth) * (height / PageHeight);
            var ratio = sum / expected;
            Assert.True(ratio >= 0.985 && ratio <= 1.03, $"{width}x{height}: luminance ink/geometry = {ratio:F4}");
        }
    }
}
