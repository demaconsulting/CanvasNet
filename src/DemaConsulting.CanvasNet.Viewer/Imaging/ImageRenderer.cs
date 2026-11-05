// cspell:ignore canvasnet jpeg tif tiff deskew
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     What to render: where to write, which pages, how to load the source and write the pictures, and which
///     operations to apply in between.
/// </summary>
/// <param name="Output">
///     The output path. With <paramref name="AllPages"/> it names a directory (an existing directory, or a path that ends
///     with a directory separator) or contains <c>{page}</c>, which is replaced by the page number.
/// </param>
/// <param name="AllPages"><see langword="true"/> to render every page of a document; otherwise only <see cref="SourceOptions.Page"/> of <paramref name="Source"/>.</param>
/// <param name="Source">How the source is loaded (page, resolution, size, background, model view, and so on).</param>
/// <param name="Write">How each picture is written (format, quality, overwrite policy, JPEG background).</param>
/// <param name="Operations">The image pipeline: the operations in the order they are applied; empty for none.</param>
public sealed record RenderRequest(
    string Output,
    bool AllPages,
    SourceOptions Source,
    WriteOptions Write,
    IReadOnlyList<IImageOperation> Operations);

/// <summary>
///     One page that was loaded and written, with what is reported about it.
/// </summary>
/// <param name="Page">The 1-based page number.</param>
/// <param name="Info">The facts about the source; the size is the natural size of the page.</param>
/// <param name="Diagnostics">The notes reported while loading.</param>
/// <param name="Echo">The text a model printed while it was evaluated.</param>
/// <param name="Operations">What was done to the source to produce the output.</param>
/// <param name="Result">What was written.</param>
/// <param name="Anchors">The anchors a model declared, with their pixel positions in the written image.</param>
public sealed record RenderedPage(
    int Page,
    SourceInfo Info,
    IReadOnlyList<ImageDiagnostic> Diagnostics,
    IReadOnlyList<string> Echo,
    IReadOnlyList<AppliedOperation> Operations,
    WriteResult Result,
    IReadOnlyList<SourceAnchor> Anchors)
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderedPage"/> record from a loaded page.
    /// </summary>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="operations">What was done to the source to produce the output.</param>
    /// <param name="result">What was written.</param>
    /// <param name="anchors">The anchors of the loaded page, already mapped through the pipeline.</param>
    internal RenderedPage(LoadedSource loaded, IReadOnlyList<AppliedOperation> operations, WriteResult result, IReadOnlyList<SourceAnchor> anchors)
        : this(loaded.Page, loaded.Info, loaded.Diagnostics, loaded.Echo, operations, result, anchors)
    {
    }
}

/// <summary>
///     What a render produced.
/// </summary>
/// <param name="Info">The facts about the source file.</param>
/// <param name="Pages">The pages that were written, in the order they were rendered.</param>
public sealed record RenderResult(SourceInfo Info, IReadOnlyList<RenderedPage> Pages);

/// <summary>
///     The render operation: loads an image, drawing, or document, runs it through the image pipeline, and writes it as
///     PNG, JPEG, BMP, or TIFF - the library form of <c>canvasnet render</c> and of its verb commands.
/// </summary>
/// <remarks>
///     <para>
///         The operation is one pass of load, transform, and write. Everything that can be checked before pixels are
///         decoded (the page, the format, and every output path) is checked first, in that order, and the document is
///         opened once for all pages. Each page is loaded, run through <see cref="ImagePipeline"/> (crop, resize, rotate,
///         and so on), and encoded to a temporary file next to its target. No target is touched until every page has
///         been rendered and encoded, so a render or encode failure on any page leaves no new or partial output and
///         every existing output untouched. The outputs are then published all or nothing: if a move fails (for
///         example a target is no longer writable), the targets already published are rolled back and the error is
///         reported.
///     </para>
///     <para>
///         The source is probed by the caller (<see cref="SourceLoader.Probe"/>) so that the caller can check the
///         request against the kind of the source before any work is done. The operation does file I/O, holds no state,
///         and is safe to call from several threads for different outputs.
///     </para>
/// </remarks>
public static class ImageRenderer
{
    /// <summary>
    ///     The text in an output name that is replaced by the page number.
    /// </summary>
    private const string PagePlaceholder = "{page}";

    /// <summary>
    ///     Renders a source.
    /// </summary>
    /// <param name="info">The facts about the source, from <see cref="SourceLoader.Probe"/>.</param>
    /// <param name="request">What to render.</param>
    /// <returns>What was written, page by page.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>page-out-of-range</c> when a page does not exist; with code <c>usage-error</c> when the
    ///     format cannot be told from the output, every page would be written to the same file, or an operation does not
    ///     fit the image; with codes <c>same-input-output</c>, <c>output-exists</c>, or <c>output-write-failed</c> when an
    ///     output cannot be written; and with the codes of <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> and
    ///     <see cref="ImagePipeline.Apply"/> when the source cannot be loaded or transformed.
    /// </exception>
    public static RenderResult Execute(SourceInfo info, RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(request);

        // Report every mistake about the pages and the outputs before any pixels are decoded
        var pages = request.AllPages ? Enumerable.Range(1, info.PageCount).ToList() : [request.Source.Page];
        SourceLoader.CheckPage(info, pages[0]);
        var targets = PlanTargets(request, info, pages);
        ImageWriter.ResolveFormat(targets[0].Path, request.Write.Format?.ToFormatName());
        foreach (var (_, target) in targets)
        {
            ImageWriter.CheckTarget(target, info.Path, request.Write.NoClobber);
        }

        // Each page is loaded, run through the image pipeline (crop, resize, rotate, and so on), and encoded to a
        // temporary file next to its target. No target is touched until every page has been rendered and encoded, so a
        // render or encode failure on any page leaves no new or partial output and every existing output untouched.
        // The outputs are then published all or nothing: if a move fails (for example a target is no longer
        // writable), the targets already published are rolled back and the error is reported.
        var outputs = new List<RenderedPage>();
        var staged = new List<ImageWriter.StagedOutput>();
        try
        {
            using (var source = SourceLoader.OpenPages(info, request.Source))
            {
                foreach (var (page, target) in targets)
                {
                    using var loaded = source.ReadPage(page);
                    var (output, pending) = TransformAndStage(loaded, target, request, info.Path, info.Format.IsDocument());
                    staged.Add(pending);
                    outputs.Add(output);
                }
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

        return new RenderResult(info, outputs);
    }

    /// <summary>
    ///     Decides which output file each page is written to.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="info">The facts about the input.</param>
    /// <param name="pages">The 1-based pages to render.</param>
    /// <returns>The page and the output path of each.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>usage-error</c> when every page is rendered but the output would be the same file for
    ///     each of them.
    /// </exception>
    /// <remarks>
    ///     <c>{page}</c> in the output is replaced by the page number. With all pages an output that is an
    ///     existing directory, or ends with a directory separator, names a directory: each page is written there as
    ///     the input's file name without extension, a dash, and the page number, with the extension of the format.
    /// </remarks>
    private static List<(int Page, string Path)> PlanTargets(RenderRequest request, SourceInfo info, IReadOnlyList<int> pages)
    {
        var template = request.Output;
        if (request.AllPages && IsDirectoryOutput(template))
        {
            var extension = (request.Write.Format ?? OutputFormat.Png) switch
            {
                OutputFormat.Jpeg => ".jpg",
                OutputFormat.Bmp => ".bmp",
                OutputFormat.Tiff => ".tif",
                _ => ".png"
            };
            template = Path.Combine(template, Path.GetFileNameWithoutExtension(info.Path) + "-{page}" + extension);
        }
        else if (request.AllPages && info.PageCount > 1 && !template.Contains(PagePlaceholder, StringComparison.Ordinal))
        {
            throw new ViewerException(
                ErrorCodes.UsageError,
                $"The output '{request.Output}' would be the same file for every page.",
                "With --all-pages, put {page} in the output name, for example out-{page}.png, or name a directory.");
        }

        return pages
            .Select(page => (page, template.Replace(PagePlaceholder, page.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)))
            .ToList();
    }

    /// <summary>
    ///     Determines whether an output names a directory.
    /// </summary>
    /// <param name="output">The output path.</param>
    /// <returns><see langword="true"/> when it is an existing directory or ends with a directory separator.</returns>
    private static bool IsDirectoryOutput(string output)
    {
        return Directory.Exists(output)
               || output.EndsWith('/')
               || output.EndsWith(Path.DirectorySeparatorChar)
               || output.EndsWith(Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    ///     Runs the image pipeline over a loaded page and stages the result next to its output.
    /// </summary>
    /// <param name="loaded">The loaded page; it is not disposed.</param>
    /// <param name="target">The output path of the page.</param>
    /// <param name="request">The request.</param>
    /// <param name="inputPath">The path of the input, which no output may be.</param>
    /// <param name="isDocument"><see langword="true"/> when the source is a document.</param>
    /// <returns>What was loaded, done to it, and will be written, and the staged output that publishes it.</returns>
    /// <remarks>
    ///     The pipeline makes a new surface for every step and disposes the previous one, so at most two full-size
    ///     surfaces exist at a time (plus the writer's flattened copy of a JPEG). A border that has no color of its
    ///     own is the background the user gave, else white for a vector source (which is flattened over white), else
    ///     transparent.
    /// </remarks>
    private static (RenderedPage Output, ImageWriter.StagedOutput Staged) TransformAndStage(
        LoadedSource loaded, string target, RenderRequest request, string inputPath, bool isDocument)
    {
        Surface? transformed = null;
        try
        {
            var surface = loaded.Surface;
            IReadOnlyList<AppliedOperation> applied = [];
            if (request.Operations.Count > 0)
            {
                var defaultPad = request.Source.Background ?? (loaded.IsVector ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 0));
                transformed = ImagePipeline.Apply(surface, request.Operations, defaultPad, out applied, request.Source.Background);
                surface = transformed;
            }

            var staged = ImageWriter.Stage(surface, target, inputPath, request.Write);
            var result = staged.Result;

            // The anchors of a model follow the pixels through the pipeline; the loaded image is the starting point
            var anchors = request.Operations.Count > 0
                ? ImagePipeline.MapAnchors(loaded.Anchors, loaded.Surface.Width, loaded.Surface.Height, applied)
                : loaded.Anchors;
            return (new RenderedPage(loaded, CollectOperations(loaded, applied, result, request, isDocument), result, anchors), staged);
        }
        finally
        {
            transformed?.Dispose();
        }
    }

    /// <summary>
    ///     Lists what was done to a loaded page to produce its output.
    /// </summary>
    /// <param name="loaded">The loaded page.</param>
    /// <param name="applied">What the image pipeline did, in order.</param>
    /// <param name="result">What was written.</param>
    /// <param name="request">The request.</param>
    /// <param name="isDocument"><see langword="true"/> when the source is a document.</param>
    /// <returns>
    ///     The operations of the loader, then those of the pipeline, then the flattening of a JPEG when the writer did
    ///     it; when every page of a document is rendered each operation names its page.
    /// </returns>
    private static List<AppliedOperation> CollectOperations(
        LoadedSource loaded,
        IReadOnlyList<AppliedOperation> applied,
        WriteResult result,
        RenderRequest request,
        bool isDocument)
    {
        var operations = loaded.Operations.Concat(applied).ToList();
        if (result.Flattened)
        {
            operations.Add(SourceLoader.Operation("flatten", ("background", SourceLoader.FormatColor(request.Write.FlattenBackground))));
        }

        if (request.AllPages && isDocument)
        {
            return operations
                .Select(o => o.Fields.Any(f => f.Key == "page")
                    ? o
                    : o with { Fields = [.. o.Fields, new KeyValuePair<string, object>("page", loaded.Page)] })
                .ToList();
        }

        return operations;
    }
}
