// cspell:ignore canvasnet psnr rmse ssim
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     One input of a comparison: which file, which page of it, and how it is loaded.
/// </summary>
/// <param name="Label">The name of the side, <c>a</c> or <c>b</c>, used in messages and results.</param>
/// <param name="Info">The facts about the file, from <see cref="SourceLoader.Probe"/>.</param>
/// <param name="Page">The 1-based page that is compared (1 for a single-image source); it must exist in <paramref name="Info"/>.</param>
/// <param name="Source">
///     How the input is loaded, without a target size: the size is set by <see cref="CompareRequest.Resize"/>. The
///     caller leaves out what does not concern the kind of the input (for example a model view for a raster).
/// </param>
public sealed record CompareInput(string Label, SourceInfo Info, int Page, SourceOptions Source);

/// <summary>
///     How the two inputs of a comparison are brought to one size.
/// </summary>
/// <param name="Side">The side whose size is the target (<c>a</c> or <c>b</c>), or <see langword="null"/> for an explicit size.</param>
/// <param name="Width">The target width in pixels for an explicit size; 0 otherwise.</param>
/// <param name="Height">The target height in pixels for an explicit size; 0 otherwise.</param>
public sealed record ResizeTarget(string? Side, int Width, int Height);

/// <summary>
///     What to compare: the two inputs, how they are brought to one size, what is measured and drawn, and where the
///     pictures are written.
/// </summary>
/// <param name="A">The first input.</param>
/// <param name="B">The second input.</param>
/// <param name="Resize">How the inputs are brought to one size, or <see langword="null"/> to compare them at their own sizes (which must then be equal).</param>
/// <param name="Fit">How a raster input is fitted to <paramref name="Resize"/>.</param>
/// <param name="Filter">The resampling filter of a fit.</param>
/// <param name="Background">The opaque color transparency is flattened over, and the margin of a contained raster.</param>
/// <param name="Tolerance">The largest channel difference that does not make a pixel differ, 0 to 255.</param>
/// <param name="GridColumns">The number of grid columns, 1 to <see cref="ComparisonResult.MaxGridSide"/>.</param>
/// <param name="GridRows">The number of grid rows, 1 to <see cref="ComparisonResult.MaxGridSide"/>.</param>
/// <param name="Thresholds">The requirements the result is checked against, in the order given; empty for none.</param>
/// <param name="DiffPath">The output of the difference picture, or <see langword="null"/> for none.</param>
/// <param name="SideBySidePath">The output of the side-by-side picture, or <see langword="null"/> for none.</param>
/// <param name="OverlayPath">The output of the overlay, or <see langword="null"/> for none.</param>
/// <param name="DiffStyle">How a difference is drawn in the difference picture.</param>
/// <param name="Gain">The amplification of a difference in the pictures, 0.1 to 255.</param>
/// <param name="OverlayWeight">The weight of the second input in the overlay, 0 to 1.</param>
/// <param name="Write">How the pictures are written.</param>
public sealed record CompareRequest(
    CompareInput A,
    CompareInput B,
    ResizeTarget? Resize,
    FitMode Fit,
    ResamplingMode Filter,
    Rgba32 Background,
    int Tolerance,
    int GridColumns,
    int GridRows,
    IReadOnlyList<ComparisonThreshold> Thresholds,
    string? DiffPath,
    string? SideBySidePath,
    string? OverlayPath,
    DiffStyle DiffStyle,
    double Gain,
    double OverlayWeight,
    WriteOptions Write);

/// <summary>
///     One input after it was loaded, with what is reported about it.
/// </summary>
/// <param name="Label">The name of the side: <c>a</c> or <c>b</c>.</param>
/// <param name="Info">The facts about the file.</param>
/// <param name="Page">The 1-based page that was loaded.</param>
/// <param name="LoadedWidth">The width of the input as it was loaded, before it was fitted to the target size.</param>
/// <param name="LoadedHeight">The height of the input as it was loaded, before it was fitted to the target size.</param>
/// <param name="Width">The width of the pixels that were compared.</param>
/// <param name="Height">The height of the pixels that were compared.</param>
/// <param name="Operations">What was done to the input to produce the compared pixels.</param>
/// <param name="Diagnostics">The notes reported while loading.</param>
/// <param name="Echo">The text a model printed while it was evaluated.</param>
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
/// <param name="A">The first input as it was compared.</param>
/// <param name="B">The second input as it was compared.</param>
/// <param name="Metrics">The measures, the grid, and the tolerance, from <see cref="Surface.Compare"/>.</param>
/// <param name="Checks">
///     The outcome of each requested threshold, in the order given. A threshold that is not met is data here
///     (<see cref="ThresholdCheck.Passed"/> is <see langword="false"/>), not an exception: the pictures were written
///     and the caller decides what a miss means.
/// </param>
/// <param name="Pictures">The pictures that were written, in the order diff, side-by-side, overlay.</param>
public sealed record CompareResult(
    CompareSide A,
    CompareSide B,
    ComparisonResult Metrics,
    IReadOnlyList<ThresholdCheck> Checks,
    IReadOnlyList<ComparedPicture> Pictures);

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when an output format cannot be told, two outputs are the same file, or the
    ///     grid is larger than the images; with code <c>size-mismatch</c> when the inputs have different sizes and no
    ///     resize was requested; with codes <c>same-input-output</c>, <c>output-exists</c>, or <c>output-write-failed</c>
    ///     when a picture cannot be written; and with the codes of
    ///     <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> when an input cannot be loaded.
    /// </exception>
    public static CompareResult Execute(CompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        foreach (var (_, path) in requested)
        {
            ImageWriter.ResolveFormat(path, null);
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
            Page = page;
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
