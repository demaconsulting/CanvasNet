// cspell:ignore pdf endobj endstream xref startxref
using System.Text;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     The original PDF test document.
/// </summary>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     The width in points of the first page of <see cref="CreatePdf"/>.
    /// </summary>
    public const int PdfPageWidth = 144;

    /// <summary>
    ///     The height in points of the first page of <see cref="CreatePdf"/>.
    /// </summary>
    public const int PdfPageHeight = 96;

    /// <summary>
    ///     Creates a small two-page PDF document, written by hand for these tests.
    /// </summary>
    /// <param name="page1Content">The content stream of page 1, or <see langword="null"/> for the two rectangles.</param>
    /// <param name="encrypted">
    ///     <see langword="true"/> to declare an encryption handler that does not exist, which the library refuses.
    /// </param>
    /// <returns>The bytes of the document.</returns>
    /// <remarks>
    ///     <para>
    ///         Page 1 is 144x96 points (192x128 pixels at 96 dpi): a red rectangle in the top-left quarter and a blue
    ///         one in the bottom-right quarter, on white. Page 2 has the same media box and a rotation of 90 degrees,
    ///         so that it is displayed 96x144 points: it is green, with a yellow square at the bottom-left of the
    ///         unrotated page, which is the top-left of the displayed page.
    ///     </para>
    ///     <para>
    ///         There is no text and no font, so that the pixels do not depend on the fonts of the machine.
    ///     </para>
    /// </remarks>
    public static byte[] CreatePdf(string? page1Content = null, bool encrypted = false)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 96] /Contents 4 0 R /Resources << >> >>",
            PdfStream(page1Content ?? "1 0 0 rg 0 48 72 48 re f 0 0 1 rg 72 0 72 48 re f"),
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 96] /Rotate 90 /Contents 6 0 R /Resources << >> >>",
            PdfStream("0 0.6 0 rg 0 0 144 96 re f 1 1 0 rg 0 0 48 48 re f")
        };
        if (encrypted)
        {
            objects.Add("<< /Filter /NotTheStandardHandler /V 1 >>");
        }

        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append($"{offset:D10} 00000 n \n");
        }

        var encrypt = encrypted ? " /Encrypt 7 0 R" : string.Empty;
        builder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{encrypt} >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    ///     Creates the object text of a PDF stream.
    /// </summary>
    /// <param name="content">The content of the stream, in ASCII.</param>
    /// <returns>The stream object, without the object header.</returns>
    private static string PdfStream(string content)
    {
        return $"<< /Length {content.Length} >>\nstream\n{content}\nendstream";
    }
}
