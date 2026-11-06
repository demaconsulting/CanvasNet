// cspell:ignore vsdx Visio
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     VSDX sources of <see cref="SourceLoader"/>: the pages of a Visio drawing.
/// </summary>
/// <remarks>
///     A construct the library cannot draw exactly is approximated and reported as a diagnostic, and with
///     <see cref="SourceOptions.Strict"/> the first one is an <c>unsupported-feature</c> error instead. The library
///     takes a whole number of dots per inch for a page, so a fractional <see cref="SourceOptions.Dpi"/> is refused.
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads the pages of a drawing without rendering them.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file, with the name and size of every page.</returns>
    private static SourceInfo ProbeVsdx(SourceInput input, long bytes)
    {
        using var document = Read(input.Name, SourceKind.Vsdx, () => OpenVsdx(input));
        return DescribeVsdx(input.Name, document, bytes);
    }

    /// <summary>
    ///     Describes the pages of an opened drawing.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="document">The opened drawing.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo DescribeVsdx(string name, VsdxDocument document, long bytes)
    {
        var pages = Read(
            name,
            SourceKind.Vsdx,
            () => Enumerable.Range(0, document.PageCount)
                .Select(i =>
                {
                    var page = document.GetPageSize(i);
                    return CreatePage(i + 1, page.WidthEmu / EmuPerPoint, page.HeightEmu / EmuPerPoint, page.Name, null);
                })
                .ToList());
        return CreateDocumentInfo(name, SourceKind.Vsdx, bytes, pages);
    }

    /// <summary>
    ///     Opens a drawing from its source; the document holds no reference to the stream afterwards.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <returns>The opened document; the caller disposes it.</returns>
    private static VsdxDocument OpenVsdx(SourceInput input)
    {
        _documentParses++;
        using var stream = input.Open();
        return VsdxDocument.Open(stream);
    }

    /// <summary>
    ///     A drawing opened for rendering its pages.
    /// </summary>
    private sealed class VsdxPageSource : DocumentPageSource
    {
        /// <summary>
        ///     The opened document.
        /// </summary>
        private readonly VsdxDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="VsdxPageSource"/> class.
        /// </summary>
        /// <param name="document">The opened document; the page source owns it.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private VsdxPageSource(VsdxDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a drawing.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <param name="opened">The source as it was opened for its facts, whose parsed document is reused, or <see langword="null"/> to parse the file.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be opened or the resolution is not usable.</exception>
        public static VsdxPageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context, OpenedSource? opened = null)
        {
            ValidateDpi(options, true);
            var document = opened?.TakeDocument<VsdxDocument>() ?? Read(info.Path, SourceKind.Vsdx, () => OpenVsdx(input));
            return new VsdxPageSource(document, info, options, input, context);
        }

        /// <inheritdoc />
        protected override Surface Render(int index, int width, int height, List<ImageDiagnostic> diagnostics)
        {
            var renderOptions = new VsdxRenderOptions
            {
                BackgroundColor = Background,
                ThrowOnUnsupportedFeature = Options.Strict,
                UnsupportedFeatureHandler = feature => diagnostics.Add(
                    new ImageDiagnostic("vsdx", "unsupported", feature.Feature, OneLine(feature.Message)))
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
