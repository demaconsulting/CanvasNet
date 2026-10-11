using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Deterministic ink-fidelity tests for thin rules (borders, double rules, footer rules and
///     thin strokes) drawn the way HTML-to-PDF engines emit them: a <c>1 0 0 -1 0 792 cm
///     0.75 0 0 0.75 0 0 cm</c> CTM, thin rectangular clip bands, and even-odd fills of
///     nested rectangles. Each test renders at many page sizes (so rule edges land at many
///     sub-pixel phases) and requires the summed painted coverage ("ink") to match the
///     geometric area within a tight tolerance. No timing is asserted.
/// </summary>
public class PdfDocumentThinRuleInkTests
{
    /// <summary>The page width in points.</summary>
    private const double PageWidth = 612;

    /// <summary>The page height in points.</summary>
    private const double PageHeight = 792;

    /// <summary>The WeasyPrint-style CTM prefix: flip to y-down, then scale CSS px to points.</summary>
    private const string Ctm = "1 0 0 -1 0 792 cm 0.75 0 0 0.75 0 0 cm\n";

    /// <summary>Square points per square CSS-pixel unit under <see cref="Ctm"/>.</summary>
    private const double UnitAreaPoints = 0.75 * 0.75;

    /// <summary>The device sizes (width, height) rendered by every sweep, including common page sizes.</summary>
    private static readonly (int Width, int Height)[] Sizes = BuildSizes();

    private static (int, int)[] BuildSizes()
    {
        var sizes = new List<(int, int)> { (765, 990), (1040, 1345), (1303, 1686) };
        for (var width = 400; width <= 1500; width += 97)
        {
            sizes.Add((width, (int)Math.Round(width * PageHeight / PageWidth)));
        }

        return [.. sizes];
    }

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

    /// <summary>Renders the content at the given size on a transparent page and returns its summed alpha ("ink") in pixels.</summary>
    private static double RenderInk(string content, int width, int height)
    {
        using var document = PdfDocument.Open(new MemoryStream(BuildPdf(content)));
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

    /// <summary>Asserts the ink at every sweep size is within <paramref name="tolerance"/> (relative) of the geometric area.</summary>
    private static void AssertInkMatchesGeometry(string content, double areaUnits, double tolerance)
    {
        foreach (var (width, height) in Sizes)
        {
            var expected = areaUnits * UnitAreaPoints * (width / PageWidth) * (height / PageHeight);
            var ink = RenderInk(content, width, height);
            Assert.True(
                ink / expected >= 1 - tolerance && ink / expected <= 1 + tolerance,
                $"{width}x{height}: ink/geometry = {ink / expected:F4} (tolerance {tolerance})");
        }
    }

    private static double Overlap(
        (double L, double T, double R, double B) a, (double L, double T, double R, double B) b) =>
        Math.Max(0, Math.Min(a.R, b.R) - Math.Max(a.L, b.L)) * Math.Max(0, Math.Min(a.B, b.B) - Math.Max(a.T, b.T));

    private static string Band(double l, double t, double r, double b) =>
        $"{F(r)} {F(b)} m {F(l)} {F(b)} l {F(l)} {F(t)} l {F(r)} {F(t)} l W* n\n";

    private static string Re(double l, double t, double r, double b) => $"{F(l)} {F(t)} {F(r - l)} {F(b - t)} re ";

    /// <summary>
    ///     Proves a WeasyPrint-style <c>border: double</c> top rule - a thin rectangular clip band
    ///     whose edges coincide with the even-odd ring rectangles' edges - paints its full ink at
    ///     every sub-pixel phase (previously 10-40 % too light).
    /// </summary>
    [Fact]
    public void PdfDocument_ThinRules_DoubleRuleWithCoincidentClip_InkMatchesGeometry()
    {
        // Two nested rings (even-odd over four rectangles), clipped to the top 5-unit band
        (double L, double T, double R, double B)[] rects =
        [
            (96, 60, 720, 996), (96, 61.667, 720, 994.333), (96, 63.333, 720, 992.667), (96, 65, 720, 991),
        ];
        var clip = (L: 96.0, T: 991.0, R: 720.0, B: 996.0);
        var content = Ctm + "0 g\n" + Band(clip.L, clip.T, clip.R, clip.B)
            + string.Concat(rects.Select(r => Re(r.L, r.T, r.R, r.B))) + "f*\n";
        var area = Overlap(rects[0], clip) - Overlap(rects[1], clip) + Overlap(rects[2], clip) - Overlap(rects[3], clip);

        AssertInkMatchesGeometry(content, area, 0.02);
    }

    /// <summary>Proves the same double rule under a clip wider than the band is also ink-exact (within 1 %).</summary>
    [Fact]
    public void PdfDocument_ThinRules_DoubleRuleWithWiderClip_InkMatchesGeometry()
    {
        (double L, double T, double R, double B)[] rects =
        [
            (96, 60, 720, 996), (96, 61.667, 720, 994.333), (96, 63.333, 720, 992.667), (96, 65, 720, 991),
        ];
        var clip = (L: 90.0, T: 985.0, R: 730.0, B: 1000.0);
        var content = Ctm + "0 g\n" + Band(clip.L, clip.T, clip.R, clip.B)
            + string.Concat(rects.Select(r => Re(r.L, r.T, r.R, r.B))) + "f*\n";
        var area = Overlap(rects[0], clip) - Overlap(rects[1], clip) + Overlap(rects[2], clip) - Overlap(rects[3], clip);

        AssertInkMatchesGeometry(content, area, 0.01);
    }

    /// <summary>Proves the gap between the double rule's two bars stays unpainted (the rule is not turned solid).</summary>
    [Fact]
    public void PdfDocument_ThinRules_DoubleRule_GapBetweenBarsRemainsTransparent()
    {
        var content = Ctm + "0 g\n" + Band(96, 991, 720, 996)
            + Re(96, 60, 720, 996) + Re(96, 61.667, 720, 994.333) + Re(96, 63.333, 720, 992.667) + Re(96, 65, 720, 991) + "f*\n";
        using var document = PdfDocument.Open(new MemoryStream(BuildPdf(content)));
        using var surface = document.Render(0, 1303, 1686, new PdfRenderOptions { BackgroundColor = new(0, 0, 0, 0) });

        // Gap spans y = 992.667..994.333 units -> points 0.75x -> pixels (x 1303/612); the page is y-down
        var scale = 1686 / PageHeight;
        var gapMid = (int)(0.75 * 993.5 * scale);
        Assert.Equal(0, surface[(int)(0.75 * 400 * 1303 / PageWidth), gapMid].A);
    }

    /// <summary>Proves a footer-style 1-unit rule (clip band on the outer rectangle's edge plus an even-odd ring) keeps its ink.</summary>
    [Fact]
    public void PdfDocument_ThinRules_FooterRule_InkMatchesGeometry()
    {
        var content = Ctm + "0.6 g\n" + Band(96, 995, 720, 996) + Re(96, 65, 720, 995) + Re(96, 60, 720, 996) + "f*\n";

        AssertInkMatchesGeometry(content, 624.0 * 1.0, 0.02);
    }

    /// <summary>Proves thin stroked rules (0.75 and 1 unit wide) at fractional positions keep their ink within 2 %.</summary>
    [Fact]
    public void PdfDocument_ThinRules_ThinStrokedRules_InkMatchesGeometry()
    {
        foreach (var (width, y) in new[] { (0.75, 500.0), (0.75, 500.31), (1.0, 733.77), (1.0, 100.5) })
        {
            var content = Ctm + $"0 g {F(width)} w 96 {F(y)} m 720 {F(y)} l S\n";

            AssertInkMatchesGeometry(content, 624.0 * width, 0.02);
        }
    }

    /// <summary>Proves a thin filled bar (0.75 unit tall) keeps its ink within 1 %.</summary>
    [Fact]
    public void PdfDocument_ThinRules_ThinFilledBar_InkMatchesGeometry()
    {
        var content = Ctm + "0 g " + Re(96, 500.13, 720, 500.88) + "f\n";

        AssertInkMatchesGeometry(content, 624.0 * 0.75, 0.01);
    }

    /// <summary>Proves a zero-width stroke (hairline) still paints at least a full device pixel per column.</summary>
    [Fact]
    public void PdfDocument_ThinRules_ZeroWidthStroke_PaintsHairline()
    {
        var content = Ctm + "0 g 0 w 96 500 m 720 500 l S\n";

        foreach (var (width, height) in Sizes)
        {
            var columns = 624.0 * 0.75 * (width / PageWidth);
            var ink = RenderInk(content, width, height);
            Assert.InRange(ink / columns, 0.95, 1.4);
        }
    }
}


