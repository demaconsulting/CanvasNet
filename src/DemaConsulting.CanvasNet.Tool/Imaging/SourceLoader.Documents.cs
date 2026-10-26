// cspell:ignore pptx vsdx Visio
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     What the page-based sources (PDF, PPTX, VSDX) of <see cref="SourceLoader"/> share: the page list, the size of a
///     page, and the rendering of a page to a size.
/// </summary>
internal static partial class SourceLoader
{
    /// <summary>
    ///     The resolution a page is sized at when none is given, in dots per inch.
    /// </summary>
    internal const double DefaultDpi = 96;

    /// <summary>
    ///     The number of points in an inch.
    /// </summary>
    private const double PointsPerInch = 72;

    /// <summary>
    ///     The number of English Metric Units (the unit of Office files) in a point.
    /// </summary>
    private const double EmuPerPoint = 12700;

    /// <summary>
    ///     Checks the resolution before a document is opened.
    /// </summary>
    /// <param name="options">The loading options.</param>
    /// <param name="wholeNumber"><see langword="true"/> when the source only takes a whole number of dots per inch.</param>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the resolution is not usable.</exception>
    private static void ValidateDpi(SourceOptions options, bool wholeNumber)
    {
        var dpi = options.Dpi ?? DefaultDpi;
        if (!double.IsFinite(dpi) || dpi <= 0)
        {
            throw new ToolException(ErrorCodes.UsageError, $"The resolution {dpi} is not valid: it must be greater than zero.");
        }

        if (wholeNumber && Math.Abs(dpi - Math.Round(dpi)) > 1e-9)
        {
            throw new ToolException(
                ErrorCodes.UsageError,
                $"The resolution {dpi} is not valid for this source: it needs a whole number of dots per inch.",
                "Use a whole number for --dpi, for example --dpi 150.");
        }
    }

    /// <summary>
    ///     Creates the facts about a document from its pages.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="kind">The kind of the document.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <param name="pages">The pages.</param>
    /// <returns>The facts, whose size is the size of the first page.</returns>
    /// <exception cref="ToolException">Thrown with code <c>corrupt-input</c> when the document has no pages.</exception>
    private static SourceInfo CreateDocumentInfo(string path, SourceKind kind, long bytes, IReadOnlyList<SourcePage> pages)
    {
        if (pages.Count == 0)
        {
            throw new ToolException(ErrorCodes.CorruptInput, $"'{path}' is not a valid {kind.ToFormatName()} file: it has no pages.");
        }

        var first = pages[0];
        return new SourceInfo(path, kind, bytes, first.Width, first.Height, 3, false, true, 1, pages.Count, null, pages, [], []);
    }

    /// <summary>
    ///     Creates a page record from its size in points.
    /// </summary>
    /// <param name="number">The 1-based page number.</param>
    /// <param name="widthPoints">The width in points.</param>
    /// <param name="heightPoints">The height in points.</param>
    /// <param name="name">The page name, or <see langword="null"/>.</param>
    /// <param name="rotation">The rotation of a PDF page, or <see langword="null"/>.</param>
    /// <returns>The page, with its size in pixels at 96 dots per inch.</returns>
    private static SourcePage CreatePage(int number, double widthPoints, double heightPoints, string? name, int? rotation)
    {
        return new SourcePage(
            number,
            ToPixels(widthPoints, DefaultDpi),
            ToPixels(heightPoints, DefaultDpi),
            name,
            widthPoints,
            heightPoints,
            rotation);
    }

    /// <summary>
    ///     Converts a length in points to pixels at a resolution.
    /// </summary>
    /// <param name="points">The length in points.</param>
    /// <param name="dpi">The resolution in dots per inch.</param>
    /// <returns>The length in pixels, rounded to the nearest pixel (half away from zero), at least 1.</returns>
    private static int ToPixels(double points, double dpi)
    {
        var pixels = Math.Round(points * dpi / PointsPerInch, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(pixels, 1, int.MaxValue);
    }

    /// <summary>
    ///     A page-based source: a document opened once, whose pages are rendered on request.
    /// </summary>
    private abstract class DocumentPageSource : PageSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="DocumentPageSource"/> class.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        protected DocumentPageSource(SourceInfo info, SourceOptions options)
            : base(info, options)
        {
        }

        /// <summary>
        ///     Gets the resolution pages are sized at, in dots per inch.
        /// </summary>
        protected double Dpi => Options.Dpi ?? DefaultDpi;

        /// <summary>
        ///     Gets the color the page is rendered over: white unless the options say otherwise.
        /// </summary>
        protected Rgba32 Background => Options.Background ?? new Rgba32(255, 255, 255, 255);

        /// <summary>
        ///     Renders a page to a surface of a given size.
        /// </summary>
        /// <param name="index">The 0-based index of the page.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <param name="diagnostics">The list to add the notes about unsupported features to.</param>
        /// <returns>A new surface, cleared to <see cref="Background"/> and drawn on.</returns>
        protected abstract Surface Render(int index, int width, int height, List<ImageDiagnostic> diagnostics);

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var sourcePage = Info.Pages[page - 1];
            var naturalWidth = ToPixels(sourcePage.WidthPoints, Dpi);
            var naturalHeight = ToPixels(sourcePage.HeightPoints, Dpi);

            // The sizing options act on the natural size; two sides given together name a box that the page is fitted
            // inside (the same rule as for an SVG), so that the page is never stretched
            var (boxWidth, boxHeight) = ComputeTargetSize(Info.Path, naturalWidth, naturalHeight, Options);
            var (width, height) = Options.Width != null && Options.Height != null
                ? FitInside(naturalWidth, naturalHeight, boxWidth, boxHeight)
                : (boxWidth, boxHeight);

            var operations = new List<AppliedOperation>
            {
                Operation(
                    "render",
                    ("page", page),
                    ("dpi", Dpi),
                    ("width", width),
                    ("height", height),
                    ("background", FormatColor(Background)))
            };
            var diagnostics = new List<ImageDiagnostic>();
            var surface = Read(Info.Path, Info.Format, () => Render(page - 1, width, height, diagnostics));
            try
            {
                if (width != boxWidth || height != boxHeight)
                {
                    surface = Replace(surface, Center(surface, boxWidth, boxHeight, Background));
                    operations.Add(Operation("fit", ("width", boxWidth), ("height", boxHeight)));
                }

                var notes = diagnostics.Select(d => d with { Page = page }).ToList();
                return new LoadedSource(surface, Info with { Width = naturalWidth, Height = naturalHeight }, page, true, notes, [], operations);
            }
            catch
            {
                surface.Dispose();
                throw;
            }
        }

        /// <summary>
        ///     Finds the largest size with the aspect ratio of a page that fits inside a box.
        /// </summary>
        /// <param name="width">The width of the page.</param>
        /// <param name="height">The height of the page.</param>
        /// <param name="boxWidth">The width of the box.</param>
        /// <param name="boxHeight">The height of the box.</param>
        /// <returns>The width and height of the fitted page, each at least 1 and at most the box.</returns>
        private static (int Width, int Height) FitInside(int width, int height, int boxWidth, int boxHeight)
        {
            var factor = Math.Min((double)boxWidth / width, (double)boxHeight / height);
            var fittedWidth = (int)Math.Clamp(Math.Round(width * factor, MidpointRounding.AwayFromZero), 1, boxWidth);
            var fittedHeight = (int)Math.Clamp(Math.Round(height * factor, MidpointRounding.AwayFromZero), 1, boxHeight);
            return (fittedWidth, fittedHeight);
        }

        /// <summary>
        ///     Places a surface in the middle of a larger one that is filled with a color.
        /// </summary>
        /// <param name="surface">The surface to place; it is not disposed.</param>
        /// <param name="width">The width of the new surface.</param>
        /// <param name="height">The height of the new surface.</param>
        /// <param name="fill">The color of the margin.</param>
        /// <returns>A new surface.</returns>
        private static Surface Center(Surface surface, int width, int height, Rgba32 fill)
        {
            var result = new Surface(width, height);
            try
            {
                result.Clear(fill);
                var left = (width - surface.Width) / 2;
                var top = (height - surface.Height) / 2;
                for (var y = 0; y < surface.Height; y++)
                {
                    surface.GetRowSpan(y).CopyTo(result.GetRowSpan(top + y)[left..]);
                }

                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
    }
}
