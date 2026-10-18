using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Grid-line regression tests for PDF raster images drawn at many reduction ratios (the "nine sizes"
///     scenario): no one-pixel line may disappear and the darkness must stay consistent. Timing is never asserted.
/// </summary>
public class PdfImageResamplingTests
{
    private const int SourceWidth = 1940;
    private const int SourceHeight = 40;
    private const int FirstLine = 15;
    private const int LineSpacing = 24;
    private const int PageWidth = 612;

    private static readonly PdfRenderOptions White = new() { BackgroundColor = new(255, 255, 255, 255) };

    /// <summary>Renders the synthetic grid image at <paramref name="pixelWidth"/> and returns the minimum and maximum per-line ink.</summary>
    private static (int Missing, double MinInk, double MaxInk) Measure(int pixelWidth)
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

        var pageHeight = 792;
        var pdf = PdfDocumentImageDownscaleTests.BuildPdf(PageWidth, pageHeight, "468 0 0 40 72 700 cm", SourceWidth, SourceHeight, gray);
        using var stream = new MemoryStream(pdf);
        using var document = PdfDocument.Open(stream);
        var pixelHeight = pixelWidth * pageHeight / PageWidth;
        using var surface = document.Render(0, pixelWidth, pixelHeight, White);

        var scale = (double)pixelWidth / PageWidth;
        var imageLeft = 72 * scale;
        var pixelsPerSourceColumn = 468.0 * scale / SourceWidth;
        var y0 = (int)((792 - 720) * scale);
        var missing = 0;
        double min = double.MaxValue;
        double max = 0;
        for (var c = FirstLine; c < SourceWidth; c += LineSpacing)
        {
            var center = imageLeft + ((c + 0.5) * pixelsPerSourceColumn);
            var darkest = 0;
            double ink = 0;
            for (var x = (int)Math.Floor(center - 2); x <= (int)Math.Ceiling(center + 2); x++)
            {
                var v = 255 - surface[x, y0].R;
                darkest = Math.Max(darkest, v);
                ink += v;
            }

            if (darkest < 16)
            {
                missing++;
            }

            min = Math.Min(min, ink);
            max = Math.Max(max, ink);
        }

        return (missing, min, max);
    }

    /// <summary>No grid line goes missing at any of nine output sizes and the ink per line tracks the scale.</summary>
    [Theory]
    [InlineData(765)]
    [InlineData(900)]
    [InlineData(1020)]
    [InlineData(1275)]
    [InlineData(1530)]
    [InlineData(1836)]
    [InlineData(2448)]
    [InlineData(3060)]
    [InlineData(3672)]
    public void Render_GridImage_NoLineMissingAtAnySize(int pixelWidth)
    {
        var (missing, min, max) = Measure(pixelWidth);
        Assert.Equal(0, missing);

        // Each source line covers (468 * scale / 1940) device pixels of full black
        var expected = 255.0 * 468.0 * pixelWidth / PageWidth / SourceWidth;
        Assert.True(min >= 0.6 * expected, $"min ink {min:F1} below 0.6 * {expected:F1}");
        Assert.True(max <= 1.6 * expected + 8, $"max ink {max:F1} above 1.6 * {expected:F1}");
    }
}
