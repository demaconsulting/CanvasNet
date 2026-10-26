// cspell:ignore canvasnet
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Pdf;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     PDF sources of <see cref="SourceLoader"/>.
/// </summary>
/// <remarks>
///     The PDF library has no diagnostics channel: a feature it cannot draw makes the page fail with an
///     unsupported-feature error instead of being skipped, so a rendered PDF page never carries diagnostics and
///     <see cref="SourceOptions.Strict"/> has nothing to change.
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     Reads the pages of a PDF file without rendering them.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file, with the size (in points, rotation applied) and rotation of every page.</returns>
    private static SourceInfo ProbePdf(string path, long bytes)
    {
        using var document = Read(path, SourceKind.Pdf, () => PdfDocument.Open(path));
        var pages = Read(
            path,
            SourceKind.Pdf,
            () => Enumerable.Range(0, document.PageCount)
                .Select(i =>
                {
                    var page = document.GetPageInfo(i);
                    return CreatePage(i + 1, page.Width, page.Height, null, page.Rotation);
                })
                .ToList());
        return CreateDocumentInfo(path, SourceKind.Pdf, bytes, pages);
    }

    /// <summary>
    ///     A PDF file opened for rendering its pages.
    /// </summary>
    private sealed class PdfPageSource : DocumentPageSource
    {
        /// <summary>
        ///     The opened document.
        /// </summary>
        private readonly PdfDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PdfPageSource"/> class.
        /// </summary>
        /// <param name="document">The opened document; the page source owns it.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        private PdfPageSource(PdfDocument document, SourceInfo info, SourceOptions options)
            : base(info, options)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a PDF file.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ToolException">Thrown when the file cannot be opened or the resolution is not usable.</exception>
        public static PdfPageSource Open(SourceInfo info, SourceOptions options)
        {
            ValidateDpi(options, false);
            var document = Read(info.Path, SourceKind.Pdf, () => PdfDocument.Open(info.Path));
            return new PdfPageSource(document, info, options);
        }

        /// <inheritdoc />
        protected override Surface Render(int index, int width, int height, List<ImageDiagnostic> diagnostics)
        {
            return _document.Render(index, width, height, new PdfRenderOptions { BackgroundColor = Background });
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _document.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
