// cspell:ignore canvasnet luma otsu deskew
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     What an inspection reports.
/// </summary>
public enum InspectKind
{
    /// <summary>The dominant colors and the luma statistics of the image.</summary>
    Colors,

    /// <summary>The pixel at a point and, optionally, the pixels around it.</summary>
    Pixel
}

/// <summary>
///     A position in an image, in pixels from the top left.
/// </summary>
/// <param name="X">The column, from 0.</param>
/// <param name="Y">The row, from 0.</param>
public readonly record struct PixelPoint(int X, int Y);

/// <summary>
///     What to inspect: how the source is loaded, which operations run first, and what is reported.
/// </summary>
/// <remarks>
///     The kind is the only required member; the other members are optional init properties whose defaults are what the
///     <c>canvasnet inspect</c> command uses. Prefer <see cref="ForColors"/> and <see cref="ForPixel"/>: a pixel
///     inspection needs a point, which the factory requires and the constructor cannot. The result of a factory is a
///     record, so set the other members with a <see langword="with"/> expression, for example
///     <c>InspectRequest.ForPixel(new PixelPoint(10, 20), radius: 1) with { Operations = [new TrimOperation(8)] }</c>;
///     an object initializer cannot follow a method call (it can follow
///     <c>new InspectRequest(InspectKind.Colors)</c>).
/// </remarks>
/// <param name="Kind">What is reported.</param>
public sealed record InspectRequest(InspectKind Kind)
{
    /// <summary>
    ///     Gets how the source is loaded (page, resolution, size, background, model view, and so on); the library
    ///     defaults unless set.
    /// </summary>
    public SourceOptions Source { get; init; } = new();

    /// <summary>
    ///     Gets the image pipeline: the operations in the order they are applied before the analysis; empty (the
    ///     default) for none.
    /// </summary>
    public IReadOnlyList<IImageOperation> Operations { get; init; } = [];

    /// <summary>
    ///     Gets, for <see cref="InspectKind.Colors"/>, the number of dominant colors,
    ///     <see cref="SurfaceStatistics.MinColors"/> to <see cref="SurfaceStatistics.MaxColors"/>; the default is
    ///     <see cref="ImageInspector.DefaultColors"/>.
    /// </summary>
    public int Count { get; init; } = ImageInspector.DefaultColors;

    /// <summary>
    ///     Gets, for <see cref="InspectKind.Pixel"/>, the radius of the window around the point, 0 to
    ///     <see cref="PixelSample.MaxRadius"/>; 0 (the default) reports the pixel only.
    /// </summary>
    public int Radius { get; init; }

    /// <summary>
    ///     Gets, for <see cref="InspectKind.Pixel"/>, the point, in the coordinates of the image after the operations; it
    ///     is required there and ignored for colors. <see langword="null"/> by default.
    /// </summary>
    public PixelPoint? Point { get; init; }

    /// <summary>
    ///     Creates a request for the dominant colors and the luma statistics of an image.
    /// </summary>
    /// <param name="count">The number of dominant colors; <see cref="ImageInspector.DefaultColors"/> by default.</param>
    /// <returns>
    ///     The request, with every other member at its default; add more with a <see langword="with"/> expression.
    /// </returns>
    /// <example>
    ///     <code language="csharp">
    ///     var request = InspectRequest.ForColors(5) with { Operations = [new TrimOperation(8)] };
    ///     </code>
    /// </example>
    public static InspectRequest ForColors(int count = ImageInspector.DefaultColors) =>
        new(InspectKind.Colors) { Count = count };

    /// <summary>
    ///     Creates a request for the pixel at a point and, optionally, the pixels around it.
    /// </summary>
    /// <param name="point">The point, in the coordinates of the image after the operations.</param>
    /// <param name="radius">The radius of the window around the point; 0 (the default) reports the pixel only.</param>
    /// <returns>
    ///     The request, with every other member at its default; add more with a <see langword="with"/> expression.
    /// </returns>
    /// <example>
    ///     <code language="csharp">
    ///     var request = InspectRequest.ForPixel(new PixelPoint(10, 20), radius: 1) with
    ///     {
    ///         Operations = [new TrimOperation(8)]
    ///     };
    ///     </code>
    /// </example>
    public static InspectRequest ForPixel(PixelPoint point, int radius = 0) =>
        new(InspectKind.Pixel) { Point = point, Radius = radius };
}

/// <summary>
///     What an inspection found.
/// </summary>
/// <param name="Info">The facts about the source file.</param>
/// <param name="Page">The 1-based page that was loaded (the first page is 1; 1 for a single-image source).</param>
/// <param name="Operations">What was done to the source to produce the analyzed pixels.</param>
/// <param name="Diagnostics">The notes reported while loading.</param>
/// <param name="Echo">The text a model printed while it was evaluated.</param>
/// <param name="Width">The width in pixels of the analyzed image (after the operations).</param>
/// <param name="Height">The height in pixels of the analyzed image (after the operations).</param>
/// <param name="Background">The opaque color a translucent <see cref="SourceOptions.Background"/> was reduced to (white when none was given); it is what the report names as the background.</param>
/// <param name="Colors">The dominant colors and luma statistics for <see cref="InspectKind.Colors"/>; otherwise <see langword="null"/>.</param>
/// <param name="Pixel">The sampled pixel (and window) for <see cref="InspectKind.Pixel"/>; otherwise <see langword="null"/>.</param>
/// <remarks>
///     The result reports the <paramref name="Diagnostics"/> and the <paramref name="Echo"/> of the loaded source but not
///     the anchors of a model; only <see cref="ImageRenderer"/> and <see cref="SourceLoader"/> report anchors. The result
///     is a plain value: it holds no pixels and has nothing to dispose.
/// </remarks>
public sealed record InspectResult(
    SourceInfo Info,
    int Page,
    IReadOnlyList<AppliedOperation> Operations,
    IReadOnlyList<ImageDiagnostic> Diagnostics,
    IReadOnlyList<string> Echo,
    int Width,
    int Height,
    Rgba32 Background,
    SurfaceStatistics? Colors,
    PixelSample? Pixel);

/// <summary>
///     The inspect operation: reports what an image contains - its dominant colors and luma statistics, or the pixel at a
///     point - the library form of <c>canvasnet inspect</c>.
/// </summary>
/// <remarks>
///     <para>
///         The operation is read-only: it writes no file. It loads one page of the source exactly as
///         <see cref="ImageRenderer"/> does (the same <see cref="SourceOptions"/>, so an EXIF orientation, a document
///         page, a view of a model, a size, and a background all work), runs the same image pipeline (every operation of
///         <see cref="ImagePipeline"/>, so the coordinates of a pixel are those of the image after the pipeline), and
///         finally analyzes the pixels with <see cref="Surface.Analyze"/> or <see cref="Surface.Sample"/>.
///     </para>
///     <para>
///         The source is probed by the caller (<see cref="SourceLoader.Probe(string)"/>, or <see cref="SourceLoader.Probe(InputReference)"/> for an input with a fragment). The operation does file I/O, holds no
///         state, and is safe to call from several threads.
///     </para>
/// </remarks>
public static class ImageInspector
{
    /// <summary>
    ///     The default number of dominant colors an inspection of <see cref="InspectKind.Colors"/> reports: 8.
    /// </summary>
    public const int DefaultColors = 8;
    /// <summary>
    ///     Inspects a source.
    /// </summary>
    /// <param name="info">The facts about the source, from <see cref="SourceLoader.Probe(string)"/> or <see cref="SourceLoader.Probe(InputReference)"/>; a page the fragment of the input names replaces <see cref="SourceOptions.Page"/>.</param>
    /// <param name="request">What to inspect.</param>
    /// <returns>What was found; <see cref="InspectResult.Colors"/> or <see cref="InspectResult.Pixel"/> is set, by the kind.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when an argument is <see langword="null"/>, when <see cref="InspectRequest.Source"/> or
    ///     <see cref="InspectRequest.Operations"/> is <see langword="null"/>, or when an element of the operations is
    ///     <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the kind is <see cref="InspectKind.Pixel"/> and the request has no point.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before anything is loaded, when the kind is not defined, or when the count of a color inspection is
    ///     outside <see cref="SurfaceStatistics.MinColors"/> to <see cref="SurfaceStatistics.MaxColors"/> or the radius of
    ///     a pixel inspection is outside 0 to <see cref="PixelSample.MaxRadius"/>.
    /// </exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>page-out-of-range</c> when the page does not exist, with code <c>usage-error</c> when the
    ///     point lies outside the image after the operations, a value of an operation is out of range, or an operation
    ///     does not fit the image, with code <c>size-limit</c> when a size or border of an operation is larger than the
    ///     maximum, and with the codes of <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> and
    ///     <see cref="ImagePipeline.Apply"/> when the source cannot be loaded or transformed.
    /// </exception>
    public static InspectResult Execute(SourceInfo info, InspectRequest request)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(request);

        // Refuse a request with a missing member, and an operation with an invalid value, before anything is loaded
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Operations);
        ImagePipeline.Validate(request.Operations);
        if (!Enum.IsDefined(request.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "The inspection kind is not defined.");
        }

        if (request.Kind == InspectKind.Colors)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(request.Count, SurfaceStatistics.MinColors);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Count, SurfaceStatistics.MaxColors);
        }
        else
        {
            ArgumentOutOfRangeException.ThrowIfNegative(request.Radius);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Radius, PixelSample.MaxRadius);
        }

        if (request.Kind == InspectKind.Pixel && request.Point == null)
        {
            throw new ArgumentException("A pixel inspection needs a point.", nameof(request));
        }

        SourceLoader.CheckPage(info, info.Selection?.Page ?? request.Source.Page);

        using var loaded = SourceLoader.Load(info, request.Source);
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

            var operations = loaded.Operations.Concat(applied).ToList();
            var background = AnalysisBackground(request.Source.Background);
            SurfaceStatistics? colors = null;
            PixelSample? pixel = null;
            if (request.Kind == InspectKind.Colors)
            {
                // The loader and the pipeline have already composited the picture over the background (a raster source is
                // flattened over it, a drawing or a model is rendered onto it, and a rotation or a border is filled with
                // it), so what is still translucent here is analyzed over white: the reported background (a translucent one
                // flattened over white) is then exactly what a fully transparent pixel of the surface counts as, and no
                // pixel is composited twice.
                colors = surface.Analyze(new Rgba32(255, 255, 255, 255), request.Count);
            }
            else
            {
                var (x, y) = (request.Point!.Value.X, request.Point.Value.Y);

                // A point outside the image is a usage error with its own code and hint, so it is checked here, before the
                // library would refuse it with an ArgumentOutOfRangeException
                if (x < 0 || y < 0 || x >= surface.Width || y >= surface.Height)
                {
                    throw new ViewerException(
                        ErrorCodes.UsageError,
                        $"The point {x},{y} is outside the {surface.Width}x{surface.Height} image.",
                        $"Use a point from 0,0 to {surface.Width - 1},{surface.Height - 1}.");
                }

                pixel = surface.Sample(x, y, request.Radius);
            }

            return new InspectResult(
                loaded.Info,
                loaded.Page,
                operations,
                loaded.Diagnostics,
                loaded.Echo,
                surface.Width,
                surface.Height,
                background,
                colors,
                pixel);
        }
        finally
        {
            transformed?.Dispose();
        }
    }

    /// <summary>
    ///     Gets the color that translucent pixels are flattened over before they are analyzed.
    /// </summary>
    /// <param name="background">The background color, or <see langword="null"/> when none was given.</param>
    /// <returns>
    ///     The background when it is opaque; white when there is none; and a translucent background flattened over white
    ///     (<c>(c * a + 255 * (255 - a) + 127) / 255</c> for each channel, so a transparent background is white).
    /// </returns>
    private static Rgba32 AnalysisBackground(Rgba32? background)
    {
        if (background is not { } color)
        {
            return new Rgba32(255, 255, 255, 255);
        }

        if (color.A == 255)
        {
            return color;
        }

        return new Rgba32(
            FlattenOverWhite(color.R, color.A),
            FlattenOverWhite(color.G, color.A),
            FlattenOverWhite(color.B, color.A),
            255);
    }

    /// <summary>
    ///     Flattens one channel over white.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The channel of the flattened color.</returns>
    private static byte FlattenOverWhite(int channel, int alpha)
    {
        return (byte)(((channel * alpha) + (255 * (255 - alpha)) + 127) / 255);
    }
}
