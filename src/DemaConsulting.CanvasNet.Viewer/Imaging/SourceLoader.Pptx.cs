// cspell:ignore pptx
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Pptx;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     PPTX sources of <see cref="SourceLoader"/>: the slides of a PowerPoint presentation.
/// </summary>
/// <remarks>
///     A picture the library cannot draw (an EMF or WMF metafile) is skipped and reported as a diagnostic, and with
///     <see cref="SourceOptions.Strict"/> it is an <c>unsupported-feature</c> error instead. A slide that uses a
///     construct the library does not implement (for example an unsupported preset shape) fails with
///     <c>unsupported-feature</c> either way.
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads the slides of a presentation without rendering them.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file; every slide has the size of the presentation.</returns>
    private static SourceInfo ProbePptx(SourceInput input, long bytes)
    {
        using var document = Read(input.Name, SourceKind.Pptx, () => OpenPptx(input));
        return DescribePptx(input.Name, document, bytes);
    }

    /// <summary>
    ///     Describes the slides of an opened presentation.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="document">The opened presentation.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo DescribePptx(string name, PptxDocument document, long bytes)
    {
        var size = Read(name, SourceKind.Pptx, () => document.SlideSize);
        var count = Read(name, SourceKind.Pptx, () => document.SlideCount);
        var pages = Enumerable.Range(1, count)
            .Select(number => CreatePage(number, size.WidthEmu / EmuPerPoint, size.HeightEmu / EmuPerPoint, null, null))
            .ToList();
        return CreateDocumentInfo(name, SourceKind.Pptx, bytes, pages);
    }

    /// <summary>
    ///     Opens a presentation from its source; the document holds no reference to the stream afterwards.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <returns>The opened document; the caller disposes it.</returns>
    private static PptxDocument OpenPptx(SourceInput input)
    {
        _documentParses++;
        using var stream = input.Open();
        return PptxDocument.Open(stream);
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
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private PptxPageSource(PptxDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a presentation.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <param name="opened">The source as it was opened for its facts, whose parsed document is reused, or <see langword="null"/> to parse the file.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be opened or the resolution is not usable.</exception>
        public static PptxPageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context, OpenedSource? opened = null)
        {
            ValidateDpi(options, false);
            var document = opened?.TakeDocument<PptxDocument>() ?? Read(info.Path, SourceKind.Pptx, () => OpenPptx(input));
            return new PptxPageSource(document, info, options, input, context);
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
