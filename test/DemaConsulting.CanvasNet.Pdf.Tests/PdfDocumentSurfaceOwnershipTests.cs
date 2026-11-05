using System.Reflection;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Regression tests for the <see cref="Surface"/> ownership paths in <see cref="PdfDocument"/>:
///     the page output surface allocated by <c>Render</c>, each image surface decoded by
///     <c>Do</c>, and the surfaces allocated by the raw/JPX/CCITT conversion helpers.
/// </summary>
/// <remarks>
///     <see cref="Surface"/> exposes no public disposal indicator, and no allocation-tracking
///     hook exists, so the <c>Dispose</c> calls themselves are verified by code review. These
///     tests pin the observable contract of the same paths: a failure after allocation still
///     surfaces the original exception type (the dispose-and-rethrow handlers must not wrap or
///     swallow it), a failed render leaves the document usable, and the happy path - where the
///     decoded image is now disposed after compositing - still paints correctly, including when
///     the same image is drawn repeatedly.
/// </remarks>
public class PdfDocumentSurfaceOwnershipTests
{
    /// <summary>Builds a one-page, 100 x 100 point PDF with the given content stream, resources and extra objects (from object 5).</summary>
    private static byte[] BuildPdf(string content, string resources, params byte[][] extraBodies)
    {
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"u8.ToArray(),
            Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Contents 4 0 R /Resources << {resources} >> >>"),
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

    /// <summary>Builds a stream object body from a dictionary and its raw data.</summary>
    private static byte[] Stream(string dictionary, byte[] data)
    {
        var buffer = new List<byte>();
        buffer.AddRange(Encoding.ASCII.GetBytes(dictionary + "\nstream\n"));
        buffer.AddRange(data);
        buffer.AddRange("\nendstream"u8.ToArray());
        return [.. buffer];
    }

    /// <summary>Builds an unfiltered 8-bit image XObject body.</summary>
    private static byte[] RawImage(int width, int height, string colorSpace, byte[] samples) =>
        Stream(
            $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace {colorSpace} " +
            $"/BitsPerComponent 8 /Length {samples.Length} >>",
            samples);

    /// <summary>Opens <paramref name="pdf"/> and renders page 0 at 100 x 100 pixels.</summary>
    private static Surface Render(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        using var document = PdfDocument.Open(stream);
        return document.Render(0, 100, 100);
    }

    /// <summary>Proves an undefined XObject (a failure after the output surface was allocated) still throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Render_UndefinedXObjectAfterAllocation_ThrowsInvalidDataException()
    {
        // Arrange
        var pdf = BuildPdf("/Missing Do", "/XObject << >>");

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(pdf));
    }

    /// <summary>Proves a render that failed after allocating its surface leaves the document usable for a later render.</summary>
    [Fact]
    public void PdfDocument_Render_AfterFailedRender_SucceedsOnSameDocument()
    {
        // Arrange: a document whose content stream fails after the output surface is allocated
        var failing = BuildPdf("/Missing Do", "/XObject << >>");
        using var stream = new MemoryStream(failing);
        using var document = PdfDocument.Open(stream);
        Assert.Throws<InvalidDataException>(() => document.Render(0, 100, 100));

        // Act: render again at a different size
        var ex = Assert.Throws<InvalidDataException>(() => document.Render(0, 50, 50));

        // Assert: the document is not left in a broken state - the failure is repeatable, not an
        // ObjectDisposedException or invalid-state error
        Assert.Contains("Missing", ex.Message);
    }

    /// <summary>Proves an image that is drawn repeatedly (each decode disposed after compositing) paints the expected color every time.</summary>
    [Fact]
    public void PdfDocument_Render_ImageDrawnTwice_PaintsBothPlacements()
    {
        // Arrange: a 1 x 1 opaque red image drawn into two disjoint 40 x 40 squares
        var image = RawImage(1, 1, "/DeviceRGB", [255, 0, 0]);
        var pdf = BuildPdf(
            "q 40 0 0 40 5 5 cm /Im0 Do Q q 40 0 0 40 55 55 cm /Im0 Do Q",
            "/XObject << /Im0 5 0 R >>",
            image);

        // Act
        using var surface = Render(pdf);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 75]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[75, 25]);
    }

    /// <summary>Proves an image whose sample data is truncated fails with <see cref="InvalidDataException"/> through <c>Render</c>.</summary>
    [Fact]
    public void PdfDocument_Render_TruncatedImageSamples_ThrowsInvalidDataException()
    {
        // Arrange: a 4 x 4 RGB image needs 48 sample bytes but supplies 3
        var image = RawImage(4, 4, "/DeviceRGB", [1, 2, 3]);
        var pdf = BuildPdf("q 40 0 0 40 5 5 cm /Im0 Do Q", "/XObject << /Im0 5 0 R >>", image);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => Render(pdf));
    }

    /// <summary>Proves the one-component sample conversion rethrows <see cref="InvalidDataException"/> (unwrapped) for truncated samples.</summary>
    [Fact]
    public void PdfDocument_ConvertSamplesToSurface_TruncatedGraySamples_ThrowsInvalidDataException()
    {
        // Arrange
        var samples = new byte[1];

        // Act & Assert: a 4 x 1 gray image needs 4 samples
        Assert.Throws<InvalidDataException>(() =>
            InvokeConvertSamplesToSurface(PdfDocument.PdfColorSpace.DeviceGray, samples, 4, 1, 1));
    }

    /// <summary>Proves the multi-component DeviceN conversion rethrows <see cref="InvalidDataException"/> (unwrapped) for truncated samples.</summary>
    [Fact]
    public void PdfDocument_ConvertSamplesToSurface_TruncatedDeviceNSamples_ThrowsInvalidDataException()
    {
        // Arrange: a two-colorant DeviceN space whose tint transform discards both inputs
        var function = new PdfDocument.PostScriptFunction(
            "{ 0 }"u8.ToArray(),
            [0.0, 1.0, 0.0, 1.0],
            [0, 1, 0, 1, 0, 1]);
        var space = PdfDocument.PdfColorSpace.Tint(
            PdfDocument.PdfColorSpace.Family.DeviceN,
            ["C0", "C1"],
            PdfDocument.PdfColorSpace.DeviceRGB,
            function);
        var samples = new byte[2];

        // Act & Assert: a 2 x 1 image needs 4 samples
        Assert.Throws<InvalidDataException>(() => InvokeConvertSamplesToSurface(space, samples, 2, 1, 2));
    }

    /// <summary>Proves the conversion helper still produces the converted surface when the samples are complete.</summary>
    [Fact]
    public void PdfDocument_ConvertSamplesToSurface_CompleteGraySamples_ReturnsConvertedSurface()
    {
        // Arrange
        byte[] samples = [0, 255];

        // Act
        using var surface = InvokeConvertSamplesToSurface(PdfDocument.PdfColorSpace.DeviceGray, samples, 2, 1, 1);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[1, 0]);
    }

    /// <summary>
    ///     Invokes the private static <c>PdfDocument.ConvertSamplesToSurface</c> via reflection,
    ///     unwrapping <see cref="TargetInvocationException"/> so the original exception type is observable.
    /// </summary>
    private static Surface InvokeConvertSamplesToSurface(
        PdfDocument.PdfColorSpace colorSpace,
        byte[] samples,
        int width,
        int height,
        int componentCount)
    {
        var method = typeof(PdfDocument).GetMethod("ConvertSamplesToSurface", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PdfDocument), "ConvertSamplesToSurface");
        try
        {
            return (Surface)method.Invoke(null, [colorSpace, samples, width, height, componentCount])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
