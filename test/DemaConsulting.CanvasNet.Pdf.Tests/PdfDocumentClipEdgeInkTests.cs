using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Deterministic ink-fidelity tests for clip edges: a fill whose edge coincides with a clip
///     edge must keep its exact area coverage (not coverage times clip coverage), and nested
///     clips must intersect geometrically. All PDFs are synthetic; no timing is asserted.
/// </summary>
public class PdfDocumentClipEdgeInkTests
{
    /// <summary>The page width in points.</summary>
    private const double PageWidth = 612;

    /// <summary>The page height in points.</summary>
    private const double PageHeight = 792;

    private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

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

    /// <summary>Renders on white and returns the summed darkness (255 - gray) / 255 in pixels.</summary>
    private static double RenderDarkness(string content, int width, int height, double grayLevel)
    {
        using var document = PdfDocument.Open(new MemoryStream(BuildPdf(content)));
        using var surface = document.Render(0, width, height, new PdfRenderOptions { BackgroundColor = new(255, 255, 255, 255) });
        double sum = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var p = surface[x, y];
                sum += 255 - ((0.299 * p.R) + (0.587 * p.G) + (0.114 * p.B));
            }
        }

        return sum / (255.0 * (1 - grayLevel));
    }

    private static string Rect(double x, double y, double w, double h) => $"{F(x)} {F(y)} {F(w)} {F(h)} re ";

    /// <summary>The DocPilot header rule: even-odd ring of four rects, clip band touching the outer lower bar edge.</summary>
    private const string Ctm = "1 0 0 -1 0 792 cm 0.75 0 0 0.75 0 0 cm\n";

    private static string HeaderRule(string clipBand) =>
        Ctm + clipBand + "W* n\n0.6 0.6 0.6 rg " +
        Rect(96, 65, 624, 930) + Rect(96, 61.67, 624, 934) + Rect(96, 63.33, 624, 932) + Rect(96, 60, 624, 936) + "f*\n";

    private static readonly (int Width, int Height)[] ReportedSizes = [(1147, 1484), (1020, 1320), (1108, 1434), (612, 792), (765, 990)];

    private static double Exact(double c0, double c1)
    {
        double Ov(double a, double b) => Math.Max(0, Math.Min(b, c1) - Math.Max(a, c0));
        return 624 * (Ov(60, 996) - Ov(61.67, 995.67) + Ov(63.33, 995.33) - Ov(65, 995)) * 0.5625;
    }

    private static void AssertHeaderRuleInk(string clipBand, double c0, double c1)
    {
        foreach (var (w, h) in ReportedSizes)
        {
            var ratio = RenderDarkness(HeaderRule(clipBand), w, h, 0.6) / (Exact(c0, c1) * (w / PageWidth) * (h / PageHeight));
            Assert.True(ratio is >= 0.985 and <= 1.03, $"{w}x{h}: ink/exact = {ratio:F4}");
        }
    }

    /// <summary>
    ///     Proves the reported header rule - a <c>W*</c> clip built from several rectangles (so a
    ///     non-rectangular clip) whose edge coincides with the even-odd fill's outer lower bar edge -
    ///     keeps its ink (previously fill coverage was multiplied by the clip's own edge coverage).
    /// </summary>
    [Fact]
    public void PdfDocument_ClipEdge_RectangleUnionClipCoincidentWithFillEdge_InkMatchesGeometry()
    {
        AssertHeaderRuleInk(Rect(96, 995, 624, 1) + Rect(10, 10, 20, 20), 995, 996);
        AssertHeaderRuleInk(Rect(96, 990, 624, 6) + Rect(10, 10, 20, 20), 990, 996);
    }

    /// <summary>Proves the same clip as a single rectangle gives identical ink (the union path agrees with the analytic rectangle).</summary>
    [Fact]
    public void PdfDocument_ClipEdge_SingleRectangleClipCoincidentWithFillEdge_InkMatchesGeometry()
    {
        AssertHeaderRuleInk(Rect(96, 995, 624, 1), 995, 996);
        AssertHeaderRuleInk(Rect(96, 990, 624, 6), 990, 996);
    }

    /// <summary>Proves two nested identical rectangular clips at fractional edges leave the fill's exact area (c, not c squared).</summary>
    [Fact]
    public void PdfDocument_ClipEdge_NestedIdenticalFractionalRectClips_InkMatchesGeometry()
    {
        var clip = "10.3 20.6 20.4 30.2 re W n\n";
        var content = clip + clip + "0 g 0 0 612 792 re f\n";

        foreach (var (w, h) in ReportedSizes)
        {
            var ratio = RenderDarkness(content, w, h, 0) / (20.4 * 30.2 * (w / PageWidth) * (h / PageHeight));
            Assert.True(ratio is >= 0.995 and <= 1.005, $"{w}x{h}: ink/exact = {ratio:F4}");
        }
    }

    /// <summary>Proves two orthogonal fractional rectangular clips intersect to their exact overlap.</summary>
    [Fact]
    public void PdfDocument_ClipEdge_OrthogonalFractionalRectClips_InkMatchesGeometry()
    {
        var content = "10.3 20.6 100.4 30.2 re W n\n40.7 10.2 20.3 80.9 re W n\n0 g 0 0 612 792 re f\n";

        foreach (var (w, h) in ReportedSizes)
        {
            var ratio = RenderDarkness(content, w, h, 0) / (OrthogonalArea * (w / PageWidth) * (h / PageHeight));
            Assert.True(ratio is >= 0.995 and <= 1.005, $"{w}x{h}: ink/exact = {ratio:F4}");
        }
    }

    /// <summary>Overlap of x 10.3..110.7 / y 20.6..50.8 with x 40.7..61.0 / y 10.2..91.1 in points.</summary>
    private const double OrthogonalArea = (61.0 - 40.7) * (50.8 - 20.6);

    /// <summary>Proves a rectangular clip nested inside a non-rectangular (triangle) clip leaves only the triangle-restricted region and nothing outside the rectangle.</summary>
    [Fact]
    public void PdfDocument_ClipEdge_RectangleInsideTriangleClip_RestrictsToBoth()
    {
        // Triangle (0,0)-(300,0)-(0,300) in y-down device space via a flip CTM; rectangle x 100..400, y 0..100
        var content = "1 0 0 -1 0 792 cm 0 0 m 300 0 l 0 300 l h W n\n100 0 300 100 re W n\n0 g 0 0 612 792 re f\n";
        using var document = PdfDocument.Open(new MemoryStream(BuildPdf(content)));
        using var surface = document.Render(0, 612, 792, new PdfRenderOptions { BackgroundColor = new(255, 255, 255, 255) });

        // User-space (y-down) point (150, 50) is device pixel (150, 50): inside triangle (x + y < 300) and rectangle
        Assert.Equal(0, surface[150, 50].R);

        // (50, 50): inside the triangle but left of the rectangle -> unpainted
        Assert.Equal(255, surface[50, 50].R);

        // (250, 50): inside the rectangle but outside the triangle (x + y > 300) -> unpainted
        Assert.Equal(255, surface[250, 50].R);
    }
}
