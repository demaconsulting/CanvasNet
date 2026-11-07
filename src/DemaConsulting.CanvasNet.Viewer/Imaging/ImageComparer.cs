// cspell:ignore canvasnet psnr rmse ssim
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     One input of a comparison: which file, which page of it, and how it is loaded.
/// </summary>
/// <remarks>
///     The label and the probed file are required; the page and the load options default to what the
///     <c>canvasnet compare</c> command uses when the input has no options (page 1, default load options).
/// </remarks>
/// <param name="Label">The name of the side, <c>a</c> or <c>b</c>, used in messages and results.</param>
/// <param name="Info">The facts about the file, from <see cref="SourceLoader.Probe(string)"/> or <see cref="SourceLoader.Probe(InputReference)"/>.</param>
public sealed record CompareInput(string Label, SourceInfo Info)
{
    /// <summary>
    ///     Gets the 1-based page that is compared (the first page is 1, which is also the page of a single-image source
    ///     and the default); it must exist in <see cref="Info"/>. The format packages count pages from 0; do not pass them
    ///     this number unchanged.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    ///     Gets how the input is loaded, without a target size: the size is set by <see cref="CompareRequest.Resize"/>.
    ///     The default loads the input with the library defaults. The caller leaves out what does not concern the kind of
    ///     the input (for example a model view for a raster).
    /// </summary>
    public SourceOptions Source { get; init; } = new();
}

/// <summary>
///     How the two inputs of a comparison are brought to one size.
/// </summary>
/// <remarks>
///     <para>
///         Use <see cref="ToInputA"/>, <see cref="ToInputB"/> or <see cref="To(int, int)"/> instead of the constructor: they
///         name the three valid shapes. There are exactly two meanings:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <paramref name="Side"/> is <c>"a"</c> or <c>"b"</c> (lower case, compared exactly): the <b>other</b>
///                 input is loaded at the natural size of that input. <paramref name="Width"/> and
///                 <paramref name="Height"/> are then ignored; they are <c>0</c> by convention.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Any other <paramref name="Side"/>, normally <see langword="null"/>: <b>both</b> inputs are loaded at
///                 <paramref name="Width"/> x <paramref name="Height"/>, each 1 to <see cref="Surface.MaxDimension"/>
///                 pixels. A vector input (SVG, a document page, a model) is rendered at that size; a raster input is
///                 fitted into it with <see cref="CompareRequest.Fit"/> and <see cref="CompareRequest.Filter"/>.
///             </description>
///         </item>
///     </list>
///     <para>
///         The library does not check <paramref name="Side"/> or the size when the request is built: a misspelled side
///         such as <c>"A"</c> is taken for an explicit size, and a size outside the range makes the comparison fail
///         when it runs. The command line does check both.
///     </para>
/// </remarks>
/// <param name="Side">The side whose size is the target: <c>"a"</c> (bring B to the size of A) or <c>"b"</c> (bring A to the size of B), lower case; <see langword="null"/> for an explicit size.</param>
/// <param name="Width">The target width in pixels of an explicit size (1 to <see cref="Surface.MaxDimension"/>); 0, and ignored, when <paramref name="Side"/> is <c>"a"</c> or <c>"b"</c>.</param>
/// <param name="Height">The target height in pixels of an explicit size (1 to <see cref="Surface.MaxDimension"/>); 0, and ignored, when <paramref name="Side"/> is <c>"a"</c> or <c>"b"</c>.</param>
public sealed record ResizeTarget(string? Side, int Width, int Height)
{
    /// <summary>
    ///     Gets the target that brings input B to the size of input A.
    /// </summary>
    public static ResizeTarget ToInputA { get; } = new("a", 0, 0);

    /// <summary>
    ///     Gets the target that brings input A to the size of input B.
    /// </summary>
    public static ResizeTarget ToInputB { get; } = new("b", 0, 0);

    /// <summary>
    ///     Creates the target that brings both inputs to an explicit size.
    /// </summary>
    /// <param name="width">The width in pixels, 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height in pixels, 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <returns>The target; the size is checked when the comparison runs.</returns>
    public static ResizeTarget To(int width, int height) => new(null, width, height);
}

/// <summary>
///     What to compare: the two inputs, how they are brought to one size, what is measured and drawn, and where the
///     pictures are written.
/// </summary>
/// <remarks>
///     The two inputs are required; every other member is an optional init property whose default is the value the
///     <c>canvasnet compare</c> command uses when the option is not given.
/// </remarks>
/// <example>
///     Compare a reference picture with a redraw of it, bring the redraw to the size of the reference, require an RMSE of at
///     most 10 (on the 0 to 255 channel scale, so about 4% of full scale), and write the difference picture:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer;
///     using DemaConsulting.CanvasNet.Viewer.Imaging;
///
///     var request = new CompareRequest(
///         new CompareInput("a", SourceLoader.Probe("reference.png")),
///         new CompareInput("b", SourceLoader.Probe("redraw.svg")))
///     {
///         Resize = ResizeTarget.ToInputA,
///         Thresholds = [new ComparisonThreshold(ThresholdMetric.Rmse, 10)],
///         DiffPath = "diff.png"
///     };
///
///     var result = ImageComparer.Execute(request);
///     Console.WriteLine($"RMSE {result.Metrics.Rmse:F2}, passed: {result.Checks.All(check => check.Passed)}");
///     </code>
/// </example>
/// <param name="A">The first input.</param>
/// <param name="B">The second input.</param>
public sealed record CompareRequest(CompareInput A, CompareInput B)
{
    /// <summary>
    ///     Gets how the inputs are brought to one size, or <see langword="null"/> (the default) to compare them at their
    ///     own sizes, which must then be equal.
    /// </summary>
    public ResizeTarget? Resize { get; init; }

    /// <summary>
    ///     Gets how a raster input is fitted to <see cref="Resize"/>; <see cref="FitMode.Contain"/> by default.
    /// </summary>
    public FitMode Fit { get; init; } = FitMode.Contain;

    /// <summary>
    ///     Gets the resampling filter of a fit; <see cref="ResamplingMode.Auto"/> by default.
    /// </summary>
    public ResamplingMode Filter { get; init; } = ResamplingMode.Auto;

    /// <summary>
    ///     Gets the opaque color transparency is flattened over, and the margin of a contained raster; opaque white by
    ///     default.
    /// </summary>
    public Rgba32 Background { get; init; } = new(255, 255, 255, 255);

    /// <summary>
    ///     Gets the largest channel difference that does not make a pixel differ, 0 to 255; the default is
    ///     <see cref="ImageComparison.DefaultTolerance"/>.
    /// </summary>
    /// <remarks>
    ///     A pixel differs when any of its red, green, or blue channels differs by <b>more</b> than the tolerance. It
    ///     changes only the count and ratio of differing pixels (<see cref="ThresholdMetric.DiffRatio"/>, and the same
    ///     ratio of each grid cell); it does not change MAE, RMSE, PSNR, SSIM, or the maximum difference, which always
    ///     use every pixel.
    /// </remarks>
    public int Tolerance { get; init; } = ImageComparison.DefaultTolerance;

    /// <summary>
    ///     Gets the number of grid columns, 1 to <see cref="ComparisonResult.MaxGridSide"/>; the default is
    ///     <see cref="ImageComparison.DefaultGridColumns"/>.
    /// </summary>
    public int GridColumns { get; init; } = ImageComparison.DefaultGridColumns;

    /// <summary>
    ///     Gets the number of grid rows, 1 to <see cref="ComparisonResult.MaxGridSide"/>; the default is
    ///     <see cref="ImageComparison.DefaultGridRows"/>.
    /// </summary>
    public int GridRows { get; init; } = ImageComparison.DefaultGridRows;

    /// <summary>
    ///     Gets the requirements the result is checked against, in the order given; empty (the default) for none.
    /// </summary>
    public IReadOnlyList<ComparisonThreshold> Thresholds { get; init; } = [];

    /// <summary>
    ///     Gets the output of the difference picture, or <see langword="null"/> (the default) for none; the extension
    ///     chooses the format unless <see cref="Write"/> sets one.
    /// </summary>
    public string? DiffPath { get; init; }

    /// <summary>
    ///     Gets the output of the side-by-side picture, or <see langword="null"/> (the default) for none; the extension
    ///     chooses the format unless <see cref="Write"/> sets one.
    /// </summary>
    public string? SideBySidePath { get; init; }

    /// <summary>
    ///     Gets the output of the overlay, or <see langword="null"/> (the default) for none; the extension chooses the
    ///     format unless <see cref="Write"/> sets one.
    /// </summary>
    public string? OverlayPath { get; init; }

    /// <summary>
    ///     Gets how a difference is drawn in the difference picture; <see cref="DiffStyle.Heat"/> by default.
    /// </summary>
    public DiffStyle DiffStyle { get; init; } = DiffStyle.Heat;

    /// <summary>
    ///     Gets the amplification of a difference in the pictures, 0.1 to 255; the default is
    ///     <see cref="ImageComparison.DefaultGain"/>.
    /// </summary>
    public double Gain { get; init; } = ImageComparison.DefaultGain;

    /// <summary>
    ///     Gets the weight of the second input in the overlay, 0 to 1; the default is
    ///     <see cref="ImageComparison.DefaultOverlayWeight"/>.
    /// </summary>
    public double OverlayWeight { get; init; } = ImageComparison.DefaultOverlayWeight;

    /// <summary>
    ///     Gets how the difference, side-by-side, and overlay pictures are written
    ///     (<see cref="WriteOptions.Format"/>, JPEG <see cref="WriteOptions.Quality"/>,
    ///     <see cref="WriteOptions.NoClobber"/>, <see cref="WriteOptions.FlattenBackground"/>);
    ///     <see cref="WriteOptions.Default"/> by default, for example
    ///     <c>Write = WriteOptions.Default with { Quality = 85 }</c>.
    /// </summary>
    public WriteOptions Write { get; init; } = WriteOptions.Default;
}
/// <summary>
///     One input after it was loaded, with what is reported about it.
/// </summary>
/// <param name="Label">The name of the side: <c>a</c> or <c>b</c>.</param>
/// <param name="Info">The facts about the file.</param>
/// <param name="Page">The 1-based page that was loaded (the first page is 1).</param>
/// <param name="LoadedWidth">The width of the input as it was loaded, before it was fitted to the target size.</param>
/// <param name="LoadedHeight">The height of the input as it was loaded, before it was fitted to the target size.</param>
/// <param name="Width">The width of the pixels that were compared.</param>
/// <param name="Height">The height of the pixels that were compared.</param>
/// <param name="Operations">What was done to the input to produce the compared pixels.</param>
/// <param name="Diagnostics">The notes reported while loading.</param>
/// <param name="Echo">The text a model printed while it was evaluated.</param>
/// <remarks>
///     A side reports its <paramref name="Diagnostics"/> and <paramref name="Echo"/> but not the anchors of a model: the
///     anchors are reported only by <see cref="ImageRenderer"/> and <see cref="SourceLoader"/>.
/// </remarks>
public sealed record CompareSide(
    string Label,
    SourceInfo Info,
    int Page,
    int LoadedWidth,
    int LoadedHeight,
    int Width,
    int Height,
    IReadOnlyList<AppliedOperation> Operations,
    IReadOnlyList<ImageDiagnostic> Diagnostics,
    IReadOnlyList<string> Echo);

/// <summary>
///     One picture that was written by a comparison.
/// </summary>
/// <param name="Kind">The kind: <c>diff</c>, <c>sideBySide</c>, or <c>overlay</c>.</param>
/// <param name="Result">What was written.</param>
public sealed record ComparedPicture(string Kind, WriteResult Result);

/// <summary>
///     What a comparison found.
/// </summary>
/// <remarks>
///     Plain values: the result holds no pixels and has nothing to dispose; <paramref name="Pictures"/> lists where the
///     pictures were written.
/// </remarks>
/// <param name="A">The first input as it was compared.</param>
/// <param name="B">The second input as it was compared.</param>
/// <param name="Comparison">The measures, the grid, and the tolerance, from <see cref="Surface.Compare"/>; <see cref="CompareResult.Metrics"/> is a shortcut to its metrics.</param>
/// <param name="Checks">
///     The outcome of each requested threshold, in the order given. A threshold that is not met is data here
///     (<see cref="ThresholdCheck.Passed"/> is <see langword="false"/>), not an exception: the pictures were written
///     and the caller decides what a miss means.
/// </param>
/// <param name="Pictures">The pictures that were written, in the order diff, side-by-side, overlay.</param>
public sealed record CompareResult(
    CompareSide A,
    CompareSide B,
    ComparisonResult Comparison,
    IReadOnlyList<ThresholdCheck> Checks,
    IReadOnlyList<ComparedPicture> Pictures)
{
    /// <summary>
    ///     Gets the whole-image measures of <see cref="Comparison"/>: MAE, RMSE, maximum difference and the differing
    ///     pixels on the 0 to 255 channel scale (0 means identical), PSNR in decibels, and SSIM from -1 to 1.
    /// </summary>
    /// <remarks>
    ///     This is the shortcut for <c>Comparison.Metrics</c>, so the RMSE reads <c>result.Metrics.Rmse</c>. The grid
    ///     (<c>Cells</c>, <c>WorstCells</c>, <c>MaxCellRmse</c>) and the tolerance are on <see cref="Comparison"/>.
    /// </remarks>
    public ComparisonMetrics Metrics => Comparison.Metrics;
}

/// <summary>
///     The compare operation: measures how much two images differ and can write pictures of the differences - the
///     library form of <c>canvasnet compare</c>.
/// </summary>
/// <remarks>
///     <para>
///         Each input is loaded by <see cref="SourceLoader"/> like the input of <see cref="ImageRenderer"/>, and both are
///         flattened over the opaque background, so the metrics are defined on opaque RGB.
///     </para>
///     <para>
///         The inputs must have the same size. <see cref="CompareRequest.Resize"/> brings them to one size: <c>a</c> or
///         <c>b</c> is the size of that input, and an explicit size is used as given. A vector input (SVG, a document
///         page, a model) is rendered at that size, sharply; a raster input is fitted into it (<see cref="FitMode"/>).
///         Without a resize a difference in size is the error <c>size-mismatch</c>.
///     </para>
///     <para>
///         <see cref="Surface.Compare"/> computes the metrics and the per-cell grid and <see cref="Surface.CreateDiff"/>,
///         <see cref="Surface.CreateOverlay"/>, and <see cref="Surface.CreateSideBySide"/> the pictures;
///         <see cref="ImageComparison"/> checks the thresholds. The pictures are encoded to temporary files first and
///         moved into place only when every one was encoded, like the pages of <see cref="ImageRenderer"/>.
///     </para>
///     <para>
///         The inputs are probed, and their pages and options checked against their kinds, by the caller. The operation
///         does file I/O, holds no state, and is safe to call from several threads for different outputs.
///     </para>
/// </remarks>
public static class ImageComparer
{
    /// <summary>
    ///     Compares two inputs.
    /// </summary>
    /// <param name="request">What to compare.</param>
    /// <returns>The measures, the threshold checks, and the pictures that were written.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="request"/> is <see langword="null"/>, or when one of its members that the
    ///     operation needs (the inputs with their label, facts, and options, the thresholds, and the write options) is
    ///     <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before anything is probed, loaded, or written, when the fit mode, the filter, the difference style, or
    ///     a threshold metric is not defined, or when the tolerance (0 to 255), the grid (1 to
    ///     <see cref="ComparisonResult.MaxGridSide"/> each way), the gain (0.1 to 255), the overlay weight (0 to 1), or an
    ///     explicit resize size (1 to <see cref="Surface.MaxDimension"/>) is out of range, whatever outputs are requested.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the resize target names a side other than <c>"a"</c> or <c>"b"</c>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when an output format cannot be told (<see cref="WriteOptions.Format"/>
    ///     wins over the extension of the output), two outputs are the same file, or the
    ///     grid is larger than the images; with code <c>size-mismatch</c> when the inputs have different sizes and no
    ///     resize was requested; with codes <c>same-input-output</c>, <c>output-exists</c>, or <c>output-write-failed</c>
    ///     when a picture cannot be written; and with the codes of
    ///     <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> when an input cannot be loaded.
    /// </exception>
    public static CompareResult Execute(CompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Refuse a request with a missing member before anything is checked, loaded, or written
        ArgumentNullException.ThrowIfNull(request.A);
        ArgumentNullException.ThrowIfNull(request.B);
        ArgumentNullException.ThrowIfNull(request.A.Label);
        ArgumentNullException.ThrowIfNull(request.B.Label);
        ArgumentNullException.ThrowIfNull(request.A.Info);
        ArgumentNullException.ThrowIfNull(request.B.Info);
        ArgumentNullException.ThrowIfNull(request.A.Source);
        ArgumentNullException.ThrowIfNull(request.B.Source);
        ArgumentNullException.ThrowIfNull(request.Thresholds);
        ArgumentNullException.ThrowIfNull(request.Write);
        ValidateValues(request);

        // Report every mistake about the outputs before any pixels are decoded
        var requested = new List<(string Kind, string Path)>();
        if (request.DiffPath != null)
        {
            requested.Add(("diff", request.DiffPath));
        }

        if (request.SideBySidePath != null)
        {
            requested.Add(("sideBySide", request.SideBySidePath));
        }

        if (request.OverlayPath != null)
        {
            requested.Add(("overlay", request.OverlayPath));
        }

        // The format is the explicit one when given, else the one the extension says
        var format = request.Write.Format?.ToFormatName();
        foreach (var (_, path) in requested)
        {
            ImageWriter.ResolveFormat(path, format);
        }

        ImageWriter.CheckTargets(
            requested.Select(r => r.Path).ToList(), [request.A.Info.Path, request.B.Info.Path], request.Write.NoClobber);

        using var a = new Side(request.A.Label, request.A.Info, request.A.Page);
        using var b = new Side(request.B.Label, request.B.Info, request.B.Page);
        LoadSides(request, a, b);
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new ViewerException(
                ErrorCodes.SizeMismatch,
                $"The inputs have different sizes: '{a.Info.Path}' is {a.Width}x{a.Height} and '{b.Info.Path}' is {b.Width}x{b.Height}.",
                "Use --resize-to a (or b, or WxH) to bring them to one size.");
        }

        if (request.GridColumns > a.Width || request.GridRows > a.Height)
        {
            throw new ViewerException(
                ErrorCodes.UsageError,
                $"The grid {request.GridColumns}x{request.GridRows} is larger than the images ({a.Width}x{a.Height}).",
                "Use a grid with at most one cell per pixel.");
        }

        var metrics = a.Surface!.Compare(b.Surface!, request.Tolerance, request.GridColumns, request.GridRows);
        var checks = ImageComparison.Evaluate(metrics, request.Thresholds);
        var pictures = WriteOutputs(request, requested, a.Surface, b.Surface!);
        return new CompareResult(a.ToResult(), b.ToResult(), metrics, checks, pictures);
    }

    /// <summary>
    ///     One input while it is loaded: the pixels it owns and what is reported about it.
    /// </summary>
    private sealed class Side : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Side"/> class.
        /// </summary>
        /// <param name="label">The name of the side: <c>a</c> or <c>b</c>.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="page">The 1-based page that is loaded.</param>
        public Side(string label, SourceInfo info, int page)
        {
            Label = label;
            Info = info;

            // The page that the fragment of the input names replaces the page of the request
            Page = info.Selection?.Page ?? page;
        }

        /// <summary>Gets the name of the side: <c>a</c> or <c>b</c>.</summary>
        public string Label { get; }

        /// <summary>Gets the facts about the file.</summary>
        public SourceInfo Info { get; }

        /// <summary>Gets the 1-based page that is loaded.</summary>
        public int Page { get; }

        /// <summary>Gets or sets the pixels at the size that is compared; the side owns them.</summary>
        public Surface? Surface { get; set; }

        /// <summary>Gets or sets the width of <see cref="Surface"/>; it stays when the pixels are disposed.</summary>
        public int Width { get; set; }

        /// <summary>Gets or sets the height of <see cref="Surface"/>; it stays when the pixels are disposed.</summary>
        public int Height { get; set; }

        /// <summary>Gets or sets the width of the input as it was loaded, before it was fitted to the target size.</summary>
        public int LoadedWidth { get; set; }

        /// <summary>Gets or sets the height of the input as it was loaded, before it was fitted to the target size.</summary>
        public int LoadedHeight { get; set; }

        /// <summary>Gets or sets what was done to the input to produce the pixels.</summary>
        public IReadOnlyList<AppliedOperation> Operations { get; set; } = [];

        /// <summary>Gets or sets the notes reported while loading.</summary>
        public IReadOnlyList<ImageDiagnostic> Diagnostics { get; set; } = [];

        /// <summary>Gets or sets the text a model printed while it was evaluated.</summary>
        public IReadOnlyList<string> Echo { get; set; } = [];

        /// <summary>
        ///     Describes the side for the caller, without the pixels.
        /// </summary>
        /// <returns>The description.</returns>
        public CompareSide ToResult()
        {
            return new CompareSide(Label, Info, Page, LoadedWidth, LoadedHeight, Width, Height, Operations, Diagnostics, Echo);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Surface?.Dispose();
            Surface = null;
        }
    }

    /// <summary>
    ///     Refuses a request with an undefined enumeration value or a value out of range, whichever outputs it asks for, so
    ///     a malformed request fails the same way before anything is probed, loaded, or written. The ranges are the ones
    ///     the <c>canvasnet compare</c> options accept.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not defined or out of range.</exception>
    /// <exception cref="ArgumentException">Thrown when the resize target names an unknown side.</exception>
    private static void ValidateValues(CompareRequest request)
    {
        if (!Enum.IsDefined(request.Fit))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Fit, "The fit mode is not defined.");
        }

        if (!Enum.IsDefined(request.Filter))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Filter, "The resampling filter is not defined.");
        }

        if (!Enum.IsDefined(request.DiffStyle))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.DiffStyle, "The difference style is not defined.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(request.Tolerance);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Tolerance, 255);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.GridColumns, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.GridColumns, ComparisonResult.MaxGridSide);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.GridRows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.GridRows, ComparisonResult.MaxGridSide);
        if (!double.IsFinite(request.Gain) || request.Gain < 0.1 || request.Gain > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Gain, "The gain must be from 0.1 to 255.");
        }

        if (!double.IsFinite(request.OverlayWeight) || request.OverlayWeight < 0 || request.OverlayWeight > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.OverlayWeight, "The overlay weight must be from 0 to 1.");
        }

        foreach (var threshold in request.Thresholds)
        {
            if (threshold is { } given && !Enum.IsDefined(given.Metric))
            {
                throw new ArgumentOutOfRangeException(nameof(request), given.Metric, "The threshold metric is not defined.");
            }
        }

        var resize = request.Resize;
        if (resize == null)
        {
            return;
        }

        if (resize.Side is not (null or "a" or "b"))
        {
            throw new ArgumentException($"The resize side '{resize.Side}' is not 'a' or 'b'.", nameof(request));
        }

        if (resize.Side == null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(resize.Width, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(resize.Width, Surface.MaxDimension);
            ArgumentOutOfRangeException.ThrowIfLessThan(resize.Height, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(resize.Height, Surface.MaxDimension);
        }
    }

    /// <summary>
    ///     Loads both inputs at the size that is compared.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="a">The first input; its pixels are set.</param>
    /// <param name="b">The second input; its pixels are set.</param>
    /// <remarks>
    ///     Without a resize both are loaded at their natural size. With <c>a</c> or <c>b</c> the reference side is
    ///     loaded first, at its natural size, and the other is loaded into that size; with an explicit size both are
    ///     loaded into the size.
    /// </remarks>
    private static void LoadSides(CompareRequest request, Side a, Side b)
    {
        var resize = request.Resize;
        if (resize == null)
        {
            LoadSide(request, a, request.A.Source, null);
            LoadSide(request, b, request.B.Source, null);
        }
        else if (resize.Side == "a")
        {
            LoadSide(request, a, request.A.Source, null);
            LoadSide(request, b, request.B.Source, (a.Width, a.Height));
        }
        else if (resize.Side == "b")
        {
            LoadSide(request, b, request.B.Source, null);
            LoadSide(request, a, request.A.Source, (b.Width, b.Height));
        }
        else
        {
            LoadSide(request, a, request.A.Source, (resize.Width, resize.Height));
            LoadSide(request, b, request.B.Source, (resize.Width, resize.Height));
        }
    }

    /// <summary>
    ///     Loads one input, optionally into a box.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="side">The input; its pixels, sizes, operations, and notes are set.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="box">The size to bring the input to, or <see langword="null"/> to keep its size.</param>
    /// <remarks>
    ///     A vector input is rendered at the box (see <see cref="SourceLoader.BoxOptions"/>); a raster input is fitted into
    ///     it with <see cref="Surface.FitInto"/> unless it already has that size.
    /// </remarks>
    private static void LoadSide(CompareRequest request, Side side, SourceOptions options, (int Width, int Height)? box)
    {
        var effective = box is { } target ? SourceLoader.BoxOptions(side.Info, options, target.Width, target.Height) : options;
        using var pages = SourceLoader.OpenPages(side.Info, effective);
        var loaded = pages.ReadPage(side.Page);

        // The side takes the surface of the loaded source: it owns it from here on
        var surface = loaded.Surface;
        try
        {
            var operations = loaded.Operations.ToList();
            side.LoadedWidth = surface.Width;
            side.LoadedHeight = surface.Height;
            side.Diagnostics = loaded.Diagnostics;
            side.Echo = loaded.Echo;
            if (box is { } size && (surface.Width != size.Width || surface.Height != size.Height))
            {
                var fitted = surface.FitInto(size.Width, size.Height, request.Fit, request.Background, request.Filter);
                surface.Dispose();
                surface = fitted;
                operations.Add(request.Fit == FitMode.Contain
                    ? SourceLoader.Operation(
                        "fit",
                        ("mode", "contain"),
                        ("width", size.Width),
                        ("height", size.Height),
                        ("background", SourceLoader.FormatColor(request.Background)))
                    : SourceLoader.Operation(
                        "resize",
                        ("width", size.Width),
                        ("height", size.Height),
                        ("filter", ImagePipeline.FilterName(request.Filter))));
            }

            side.Operations = operations;
            side.Width = surface.Width;
            side.Height = surface.Height;
            side.Surface = surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Creates, stages, and publishes the requested pictures.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="requested">The kind and path of each requested picture, in the order they are written.</param>
    /// <param name="a">The first image.</param>
    /// <param name="b">The second image.</param>
    /// <returns>What was written, in the order of <paramref name="requested"/>.</returns>
    /// <remarks>
    ///     Every picture is encoded to a temporary file before any is moved into place, so a failure while creating or
    ///     encoding one leaves no new or partial output and every existing output untouched. Each picture is disposed as
    ///     soon as it is encoded.
    /// </remarks>
    private static List<ComparedPicture> WriteOutputs(CompareRequest request, List<(string Kind, string Path)> requested, Surface a, Surface b)
    {
        var outputs = new List<ComparedPicture>();
        var staged = new List<ImageWriter.StagedOutput>();
        try
        {
            foreach (var (kind, path) in requested)
            {
                using var picture = CreatePicture(request, kind, a, b);
                var pending = ImageWriter.Stage(picture, path, request.A.Info.Path, request.Write);
                staged.Add(pending);
                outputs.Add(new ComparedPicture(kind, pending.Result));
            }

            ImageWriter.PublishAll(staged);
        }
        finally
        {
            // Disposing removes the temporary file of every output that was not moved into place
            foreach (var pending in staged)
            {
                pending.Dispose();
            }
        }

        return outputs;
    }

    /// <summary>
    ///     Creates one picture.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="kind">The kind: <c>diff</c>, <c>sideBySide</c>, or <c>overlay</c>.</param>
    /// <param name="a">The first image.</param>
    /// <param name="b">The second image.</param>
    /// <returns>The picture; the caller disposes it.</returns>
    private static Surface CreatePicture(CompareRequest request, string kind, Surface a, Surface b)
    {
        switch (kind)
        {
            case "diff":
                return a.CreateDiff(b, request.DiffStyle, request.Gain);
            case "overlay":
                return a.CreateOverlay(b, request.OverlayWeight);
            default:
                // The third panel of a side-by-side picture is always the heat map
                using (var heat = a.CreateDiff(b, DiffStyle.Heat, request.Gain))
                {
                    return a.CreateSideBySide(b, heat);
                }
        }
    }
}
