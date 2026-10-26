// cspell:ignore pptx
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Pptx;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     PPTX sources of <see cref="SourceLoader"/>: the slides of a PowerPoint presentation.
/// </summary>
/// <remarks>
///     A picture the library cannot draw (an EMF or WMF metafile) is skipped and reported as a diagnostic, and with
///     <see cref="SourceOptions.Strict"/> it is an <c>unsupported-feature</c> error instead. A slide that uses a
///     construct the library does not implement (for example an unsupported preset shape) fails with
///     <c>unsupported-feature</c> either way.
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     Reads the slides of a presentation without rendering them.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file; every slide has the size of the presentation.</returns>
    private static SourceInfo ProbePptx(string path, long bytes)
    {
        using var document = Read(path, SourceKind.Pptx, () => PptxDocument.Open(path));
        var size = Read(path, SourceKind.Pptx, () => document.SlideSize);
        var count = Read(path, SourceKind.Pptx, () => document.SlideCount);
        var pages = Enumerable.Range(1, count)
            .Select(number => CreatePage(number, size.WidthEmu / EmuPerPoint, size.HeightEmu / EmuPerPoint, null, null))
            .ToList();
        return CreateDocumentInfo(path, SourceKind.Pptx, bytes, pages);
    }

    /// <summary>
    ///     A presentation opened for rendering its slides.
    /// </summary>
    private sealed class PptxPageSource : DocumentPageSource
    {
        /// <summary>
        ///     The opened document.
        /// </summary>
        private readonly PptxDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PptxPageSource"/> class.
        /// </summary>
        /// <param name="document">The opened document; the page source owns it.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        private PptxPageSource(PptxDocument document, SourceInfo info, SourceOptions options)
            : base(info, options)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a presentation.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ToolException">Thrown when the file cannot be opened or the resolution is not usable.</exception>
        public static PptxPageSource Open(SourceInfo info, SourceOptions options)
        {
            ValidateDpi(options, false);
            var document = Read(info.Path, SourceKind.Pptx, () => PptxDocument.Open(info.Path));
            return new PptxPageSource(document, info, options);
        }

        /// <inheritdoc />
        protected override Surface Render(int index, int width, int height, List<ImageDiagnostic> diagnostics)
        {
            var renderOptions = new PptxRenderOptions
            {
                BackgroundColor = Background,
                StrictUnsupportedPictures = Options.Strict,
                OnUnsupportedPictureSkipped = skipped => diagnostics.Add(
                    new ImageDiagnostic("pptx", "unsupported", skipped.Feature, OneLine(skipped.Message)))
            };
            return _document.Render(index, width, height, renderOptions);
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
