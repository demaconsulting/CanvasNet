using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Tests of axial/radial shading fidelity: <c>/Extend</c> on radial shadings with a shrinking
///     radius, exact hard stops at stitching-function boundaries regardless of the uniform sample
///     spacing, and independent per-element <c>/Domain</c> clamping in a <c>/Function</c> array.
///     Expected values come from the specification formulas; no other viewer's output is used.
/// </summary>
public class PdfDocumentShadingFidelityTests
{
    private static readonly PdfRenderOptions Transparent = new() { BackgroundColor = new(0, 0, 0, 0) };

    private static byte[] Stream(string dictionary, byte[] data) =>
    [
        .. Encoding.ASCII.GetBytes(dictionary + "\nstream\n"),
        .. data,
        .. "\nendstream"u8.ToArray(),
    ];

    private static byte[] FunctionStream(string entries) => Stream($"<< {entries} /Length 0 >>", []);

    /// <summary>
    ///     Builds a one-page PDF (<paramref name="width"/> x 100 points) that fills the whole page with
    ///     shading pattern <c>/P1</c>; object 6 is the shading dictionary and objects from 7 are
    ///     <paramref name="functions"/>.
    /// </summary>
    private static byte[] BuildPatternPdf(int width, string shadingEntries, params byte[][] functions)
    {
        var content = Encoding.ASCII.GetBytes($"/Pattern cs /P1 scn 0 0 {width} 100 re f");
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"u8.ToArray(),
            Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} 100] /Contents 4 0 R /Resources << /Pattern << /P1 5 0 R >> >> >>"),
            Stream($"<< /Length {content.Length} >>", content),
            "<< /PatternType 2 /Shading 6 0 R >>"u8.ToArray(),
            Encoding.ASCII.GetBytes($"<< {shadingEntries} >>"),
        };
        bodies.AddRange(functions);

        using var buffer = new MemoryStream();
        void Write(byte[] b) => buffer.Write(b, 0, b.Length);
        Write("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<long>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Position);
            Write(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            Write(bodies[i]);
            Write("\nendobj\n"u8.ToArray());
        }

        var xref = buffer.Position;
        Write(Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n0000000000 65535 f \n"));
        foreach (var o in offsets)
        {
            Write(Encoding.ASCII.GetBytes($"{o:D10} 00000 n \n"));
        }

        Write(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"));
        return buffer.ToArray();
    }

    private static Surface Render(byte[] pdf, int width)
    {
        using var document = PdfDocument.Open(new MemoryStream(pdf));
        return document.Render(0, width, 100, Transparent);
    }

    private static byte[] BlackToWhite() =>
        FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [1 1 1] /N 1");

    /// <summary>Proves that a radial shading whose radius shrinks (r0 &gt; r1) extends the start outward and the end inward, each only when requested.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PdfDocument_Shading_RadialShrinkingRadius_ExtendsPerEnd(bool extendStart, bool extendEnd)
    {
        // Arrange: concentric circles, r0 = 20 (black) shrinking to r1 = 10 (white).
        var pdf = BuildPatternPdf(
            100,
            $"/ShadingType 3 /ColorSpace /DeviceRGB /Coords [50 50 20 50 50 10] /Extend [{(extendStart ? "true" : "false")} {(extendEnd ? "true" : "false")}] /Function 7 0 R",
            BlackToWhite());

        // Act
        using var surface = Render(pdf, 100);

        // Assert: the corner is outside r0 (extended start); the center is inside r1 (extended end).
        Assert.Equal(extendStart ? 255 : 0, surface[2, 2].A);
        if (extendStart)
        {
            Assert.True(surface[2, 2].R < 5);
        }

        Assert.Equal(extendEnd ? 255 : 0, surface[50, 50].A);
        if (extendEnd)
        {
            Assert.True(surface[50, 50].R > 250);
        }

        // The band between the circles is always painted.
        Assert.Equal(255, surface[65, 50].A);
    }

    /// <summary>
    ///     Proves that a stitching function's <c>/Bounds</c> become exact hard transitions in the
    ///     gradient even when the uniform sample spacing (here about 3.9 device pixels) is much coarser.
    /// </summary>
    [Fact]
    public void PdfDocument_Shading_StitchingBound_IsAnExactHardTransition()
    {
        // Arrange: a 1000-point axial ramp is sampled uniformly at 256 points (spacing ~3.9 px);
        // the black -> white switch at t = 0.5 falls between two uniform samples.
        var pdf = BuildPatternPdf(
            1000,
            "/ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 1000 0] /Function 7 0 R",
            FunctionStream("/FunctionType 3 /Domain [0 1] /Bounds [0.5] /Encode [0 1 0 1] /Functions [8 0 R 9 0 R]"),
            FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [0 0 0] /N 1"),
            FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [1 1 1] /N 1"));

        // Act
        using var surface = Render(pdf, 1000);

        // Assert: the last pixel before the bound is pure black, the first after is pure white.
        Assert.True(surface[499, 50].R < 3);
        Assert.True(surface[500, 50].R > 252);
        Assert.True(surface[490, 50].R < 3);
        Assert.True(surface[510, 50].R > 252);
    }

    /// <summary>Proves that a nested stitching function's inner bounds are also exact hard transitions.</summary>
    [Fact]
    public void PdfDocument_Shading_NestedStitchingBound_IsAnExactHardTransition()
    {
        // Arrange: outer bound at 0.5; the second half is itself stitched at its own 0.5 (t = 0.75).
        var pdf = BuildPatternPdf(
            1000,
            "/ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 1000 0] /Function 7 0 R",
            FunctionStream("/FunctionType 3 /Domain [0 1] /Bounds [0.5] /Encode [0 1 0 1] /Functions [8 0 R 9 0 R]"),
            FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [0 0 0] /N 1"),
            FunctionStream("/FunctionType 3 /Domain [0 1] /Bounds [0.5] /Encode [0 1 0 1] /Functions [10 0 R 11 0 R]"),
            FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [1 1 1] /N 1"),
            FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [0 0 0] /C1 [0 0 0] /N 1"));

        // Act
        using var surface = Render(pdf, 1000);

        // Assert: black | white | black with sharp edges at x = 500 and x = 750.
        Assert.True(surface[499, 50].R < 3);
        Assert.True(surface[500, 50].R > 252);
        Assert.True(surface[749, 50].R > 252);
        Assert.True(surface[750, 50].R < 3);
    }

    /// <summary>
    ///     Proves that each element of a <c>/Function</c> array clamps the shared parameter to its
    ///     own <c>/Domain</c> independently (PDF 32000-1 &#xA7;7.10.2).
    /// </summary>
    [Fact]
    public void PdfDocument_Shading_FunctionArrayWithDifferentDomains_ClampsEachElementIndependently()
    {
        // Arrange: R(t) = clamp(t, 0, 0.5), G(t) = clamp(t, 0.5, 1), B = 0 (each is a linear type 2 function).
        var pdf = BuildPatternPdf(
            100,
            "/ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] /Domain [0 1] /Function [7 0 R 8 0 R 9 0 R]",
            FunctionStream("/FunctionType 2 /Domain [0 0.5] /C0 [0] /C1 [1] /N 1"),
            FunctionStream("/FunctionType 2 /Domain [0.5 1] /C0 [0] /C1 [1] /N 1"),
            FunctionStream("/FunctionType 2 /Domain [0 1] /C0 [0] /C1 [0] /N 1"));

        // Act
        using var surface = Render(pdf, 100);

        // Assert: pixel centers x + 0.5 give t = 0.255 and 0.755.
        var left = surface[25, 50];
        Assert.InRange(left.R, 65 - 6, 65 + 6);
        Assert.InRange(left.G, 128 - 6, 128 + 6);
        Assert.Equal(0, left.B);

        var right = surface[75, 50];
        Assert.InRange(right.R, 128 - 6, 128 + 6);
        Assert.InRange(right.G, 192 - 6, 192 + 6);
        Assert.Equal(0, right.B);
    }
}
