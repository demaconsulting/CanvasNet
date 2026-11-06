// cspell:ignore pdf endobj endstream xref startxref
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Tests of <see cref="PdfImageSource"/>: the first page of a PDF file as an embedded picture - drawn at the size of
///     its placement (fitted without distortion) over a transparent background.
/// </summary>
public class PdfImageSourceTests
{
    /// <summary>
    ///     Creates a one-page PDF document of 144 by 96 points (192 by 128 pixels at 96 dpi) with a red rectangle in the
    ///     top-left quarter and a blue one in the bottom-right quarter, on a transparent page. There is no text and no
    ///     font, so the pixels do not depend on the fonts of the machine.
    /// </summary>
    /// <returns>The bytes of the document.</returns>
    private static byte[] CreatePdf()
    {
        const string content = "1 0 0 rg 0 48 72 48 re f 0 0 1 rg 72 0 72 48 re f";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 96] /Contents 4 0 R /Resources << >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append($"{offset:D10} 00000 n \n");
        }

        builder.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    ///     Creates a request for a PDF file.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="width">The requested width, or <see langword="null"/>.</param>
    /// <param name="height">The requested height, or <see langword="null"/>.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Request(byte[] bytes, int? width = null, int? height = null, long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "page.pdf",
            FileName = "page.pdf",
            Content = bytes,
            PixelWidth = width,
            PixelHeight = height,
            Budget = new ImageBudget(remaining)
        };
    }

    /// <summary>
    ///     Test that the page is drawn at its natural size at 96 dots per inch over a transparent background, with the
    ///     content where the page puts it.
    /// </summary>
    [Fact]
    public void PdfImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnTransparentBackground()
    {
        // Arrange
        var source = new PdfImageSource();

        // Act
        var result = source.Render(Request(CreatePdf()));
        using var surface = result.Surface;

        // Assert: 144 by 96 points is 192 by 128 pixels; red top-left, blue bottom-right, transparent elsewhere
        Assert.Equal((192, 128), (surface.Width, surface.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[20, 20]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[170, 110]);
        Assert.Equal(0, surface[170, 20].A);
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.Format);
    }

    /// <summary>
    ///     Test that the page is fitted inside the requested box without distortion, and that one requested dimension
    ///     keeps the aspect ratio.
    /// </summary>
    [Fact]
    public void PdfImageSource_Render_RequestedSize_FitsPageInsideBoxKeepingAspectRatio()
    {
        // Arrange
        var bytes = CreatePdf();
        var source = new PdfImageSource();

        // Act
        using var boxed = source.Render(Request(bytes, 96, 96)).Surface;
        using var widthOnly = source.Render(Request(bytes, 96)).Surface;

        // Assert: the 3 to 2 page fits a square box as 96 by 64, and one dimension scales the other
        Assert.Equal((96, 64), (boxed.Width, boxed.Height));
        Assert.Equal((96, 64), (widthOnly.Width, widthOnly.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), boxed[10, 10]);
    }

    /// <summary>
    ///     Test that a size beyond <see cref="ImageSourceRequest.RemainingPixels"/> is refused with
    ///     <see cref="ImageSourceBudgetException"/> before the page is drawn (the allocation stays far below the 64 MB
    ///     of the picture), and that a size that exactly fits is drawn.
    /// </summary>
    [Fact]
    public void PdfImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing()
    {
        // Arrange
        var bytes = CreatePdf();
        var source = new PdfImageSource();

        // Act: a 4000 by 4000 box (the page is fitted to 4000 by 2667) against a budget of 1000 pixels
        var before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<ImageSourceBudgetException>(() => source.Render(Request(bytes, 4000, 4000, remaining: 1000)));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        using var fits = source.Render(Request(bytes, 96, 96, remaining: 96 * 64)).Surface;

        // Assert
        Assert.Equal(1000L, ex.RemainingPixels);
        Assert.InRange(ex.RequestedPixels, 4000L * 2666, 4000L * 2667);
        Assert.True(allocated < 32 * 1024 * 1024, $"{allocated} bytes were allocated before the refusal.");
        Assert.Equal((96, 64), (fits.Width, fits.Height));
    }

    /// <summary>
    ///     Test that content that is not a PDF file, a request without file content, a null request and a canceled request
    ///     are rejected.
    /// </summary>
    [Fact]
    public void PdfImageSource_Render_InvalidInput_Throws()
    {
        // Arrange
        var source = new PdfImageSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => source.Render(Request([1, 2, 3, 4])));
        Assert.Throws<InvalidOperationException>(() => source.Render(new ImageSourceRequest { Href = "camera:front" }));
        Assert.Throws<ArgumentNullException>(() => source.Render(null!));
        Assert.Throws<OperationCanceledException>(() => source.Render(new ImageSourceRequest { Href = "a", FileName = "a.pdf", CancellationToken = cts.Token }));
    }

    /// <summary>
    ///     Creates a two-page PDF document: page 1 is 144 by 96 points with a red rectangle in the top-left quarter; page 2
    ///     is rotated by 90 degrees, so it is displayed 96 by 144 points (128 by 192 pixels), and is filled with green.
    /// </summary>
    /// <returns>The bytes of the document.</returns>
    private static byte[] CreateTwoPagePdf()
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 96] /Contents 4 0 R /Resources << >> >>",
            Stream("1 0 0 rg 0 48 72 48 re f"),
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 96] /Rotate 90 /Contents 6 0 R /Resources << >> >>",
            Stream("0 0.6 0 rg 0 0 144 96 re f")
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append($"{offset:D10} 00000 n \n");
        }

        builder.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());

        static string Stream(string content) => $"<< /Length {content.Length} >>\nstream\n{content}\nendstream";
    }

    /// <summary>
    ///     Creates a request for a link with a fragment into a PDF file.
    /// </summary>
    /// <param name="bytes">The bytes of the file.</param>
    /// <param name="fragment">The fragment text, without the hash.</param>
    /// <param name="budget">The budget, or <see langword="null"/> for none.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Linked(byte[] bytes, string fragment, ImageBudget? budget = null)
    {
        return new ImageSourceRequest
        {
            Href = "report.pdf#" + fragment,
            FileName = "report.pdf",
            Content = bytes,
            Fragment = ImageFragment.Parse(fragment),
            Budget = budget
        };
    }

    /// <summary>
    ///     Test that <c>#page=2</c> draws the second page, at the size of that page, and that <c>#page=1</c> and no
    ///     fragment draw the first.
    /// </summary>
    [Fact]
    public void PdfImageSource_Render_PageFragment_DrawsTheChosenPage()
    {
        // Arrange
        var bytes = CreateTwoPagePdf();
        var source = new PdfImageSource();

        // Act
        using var second = source.Render(Linked(bytes, "page=2")).Surface;
        using var first = source.Render(Linked(bytes, "page=1")).Surface;
        using var none = source.Render(Linked(bytes, string.Empty)).Surface;

        // Assert: page 2 is displayed rotated (128 by 192) and green; page 1 is 192 by 128 with red in its top-left
        Assert.Equal((128, 192), (second.Width, second.Height));
        Assert.Equal(new Rgba32(0, 153, 0, 255), second[64, 96]);
        Assert.Equal((192, 128), (first.Width, first.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), first[20, 20]);
        Assert.Equal((192, 128), (none.Width, none.Height));
    }

    /// <summary>
    ///     Test that a page that the document does not have is refused with the page-out-of-range code, naming the number
    ///     of pages, before anything is charged to the budget.
    /// </summary>
    [Fact]
    public void PdfImageSource_Render_PageBeyondTheLast_IsRefusedWithPageOutOfRange()
    {
        // Arrange
        var budget = new ImageBudget(1_000_000);

        // Act
        var ex = Assert.Throws<ImageSourceRefusedException>(() => new PdfImageSource().Render(Linked(CreateTwoPagePdf(), "page=3", budget)));

        // Assert
        Assert.Equal(FolderImageResolver.PageOutOfRangeCode, ex.Code);
        Assert.Equal("page 3 does not exist: the document has 2 pages", ex.Message);
        Assert.Equal(1_000_000L, budget.Remaining);
    }

    /// <summary>
    ///     Test that a page value that is not a whole number of 1 or more is refused with the invalid-fragment code and
    ///     that the file is not even read (a corrupt file is not reported).
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=two")]
    [InlineData("page=")]
    [InlineData("page=-1")]
    public void PdfImageSource_Render_InvalidPageValue_IsRefusedWithInvalidFragmentBeforeReading(string fragment)
    {
        // Act: the bytes are not a PDF, so reading them would throw InvalidDataException instead
        var ex = Assert.Throws<ImageSourceRefusedException>(() => new PdfImageSource().Render(Linked([1, 2, 3], fragment)));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
    }

    /// <summary>
    ///     Test that the source declares the <c>page</c> parameter and nothing else.
    /// </summary>
    [Fact]
    public void PdfImageSource_FragmentParameters_DeclaresPage()
    {
        // Arrange
        IFragmentImageSource source = new PdfImageSource();

        // Act / Assert
        Assert.Equal(["page"], source.FragmentParameters);
    }

    /// <summary>
    ///     Test the identity of the source: its name, its extension, and that it claims no scheme.
    /// </summary>
    [Fact]
    public void PdfImageSource_Properties_NameExtensionAndNoSchemes()
    {
        // Arrange
        var source = new PdfImageSource();

        // Act / Assert
        Assert.Equal("pdf", source.Name);
        Assert.Equal([".pdf"], source.Extensions);
        Assert.Empty(source.Schemes);
    }
}
