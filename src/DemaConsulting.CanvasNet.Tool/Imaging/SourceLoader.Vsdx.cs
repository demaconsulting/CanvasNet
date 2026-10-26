// cspell:ignore vsdx Visio
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     VSDX sources of <see cref="SourceLoader"/>: the pages of a Visio drawing.
/// </summary>
/// <remarks>
///     A construct the library cannot draw exactly is approximated and reported as a diagnostic, and with
///     <see cref="SourceOptions.Strict"/> the first one is an <c>unsupported-feature</c> error instead. The library
///     takes a whole number of dots per inch for a page, so a fractional <see cref="SourceOptions.Dpi"/> is refused.
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     Reads the pages of a drawing without rendering them.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file, with the name and size of every page.</returns>
    private static SourceInfo ProbeVsdx(string path, long bytes)
    {
        using var document = Read(path, SourceKind.Vsdx, () => VsdxDocument.Open(path));
        var pages = Read(
            path,
            SourceKind.Vsdx,
            () => Enumerable.Range(0, document.PageCount)
                .Select(i =>
                {
                    var page = document.GetPageSize(i);
                    return CreatePage(i + 1, page.WidthEmu / EmuPerPoint, page.HeightEmu / EmuPerPoint, page.Name, null);
                })
                .ToList());
        return CreateDocumentInfo(path, SourceKind.Vsdx, bytes, pages);
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
        private VsdxPageSource(VsdxDocument document, SourceInfo info, SourceOptions options)
            : base(info, options)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a drawing.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ToolException">Thrown when the file cannot be opened or the resolution is not usable.</exception>
        public static VsdxPageSource Open(SourceInfo info, SourceOptions options)
        {
            ValidateDpi(options, true);
            var document = Read(info.Path, SourceKind.Vsdx, () => VsdxDocument.Open(info.Path));
            return new VsdxPageSource(document, info, options);
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
