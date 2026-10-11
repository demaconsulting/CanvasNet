using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Rendering tests for hairline strokes of small closed outlines, exercised through a
///     synthetic in-memory PDF: a thin stroke on a closed path containing a tiny edge must stay a
///     thin outline instead of painting the whole interior solid.
/// </summary>
public class PdfDocumentHairlineStrokeTests
{
    private const int PageSize = 50;

    private const int RenderSize = 104;

    /// <summary>
    ///     A closed outline with a long body and two sub-stroke-width edges (a clip-art laptop
    ///     screen shape), filled pale blue and then hairline stroked black.
    /// </summary>
    private const string FillThenStrokeContent =
        "0.78 0.82 0.89 rg\n" +
        "0.93 19.86 m 8.27 44.63 l 7.55 45.06 l 18.48 38.94 29.33 32.68 40.1 26.27 c " +
        "32.61 0.78 l 32.75 1.5 l 22.06 7.46 11.45 13.58 0.93 19.86 c h f\n" +
        "0.72 w 0 G\n" +
        "0.93 19.86 m 8.27 44.63 l 7.55 45.06 l 18.48 38.94 29.33 32.68 40.1 26.27 c " +
        "32.61 0.78 l 32.75 1.5 l 22.06 7.46 11.45 13.58 0.93 19.86 c h S\n";

    /// <summary>
    ///     Proves that a hairline stroke on a closed outline containing tiny edges leaves the
    ///     interior untouched (still the fill color) while still inking the outline - regression
    ///     for a closed stroke whose inner offset ring was misclassified as collapsed and so was
    ///     painted solid black.
    /// </summary>
    [Fact]
    public void PdfDocument_Render_HairlineStrokeOfClosedOutlineWithTinyEdges_KeepsInteriorFilled()
    {
        // Arrange
        var pdf = BuildPdf(FillThenStrokeContent);

        // Act
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        using var surface = document.Render(0, RenderSize, RenderSize, null);

        // Assert: the interior keeps the pale-blue fill (the stroke must not flood it with black)
        var center = At(surface, 20, 22);
        Assert.InRange(center.R, 190, 215);
        Assert.InRange(center.G, 200, 220);
        Assert.InRange(center.B, 218, 238);

        // and the stroke is still inked along the outline: the lower-left edge midpoint
        // (0.93,19.86)-(11.45,13.58)... is part of the outline, so a pixel there is dark.
        var darkest = int.MaxValue;
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                var pixel = At(surface, 6 + dx * 0.5, 16.72 + dy * 0.5);
                darkest = Math.Min(darkest, pixel.R);
            }
        }

        Assert.True(darkest < 120, $"Expected a dark stroke pixel along the outline, found minimum R {darkest}.");
    }

    /// <summary>The pixel covering the PDF point (x, y) (PDF y points up, the surface's y points down).</summary>
    private static Rgba32 At(Surface surface, double x, double y) =>
        surface[(int)(x * RenderSize / PageSize), RenderSize - 1 - (int)(y * RenderSize / PageSize)];

    private static byte[] BuildPdf(string content)
    {
        var bodies = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 {PageSize} {PageSize}] >>",
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R /Resources << >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
        };

        var buffer = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Length);
            buffer.Append($"{i + 1} 0 obj\n{bodies[i]}\nendobj\n");
        }

        var xref = buffer.Length;
        buffer.Append($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            buffer.Append($"{offset:D10} 00000 n \n");
        }

        buffer.Append($"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(buffer.ToString());
    }
}
