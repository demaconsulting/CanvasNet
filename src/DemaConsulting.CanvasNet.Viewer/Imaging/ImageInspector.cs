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
/// <param name="Source">How the source is loaded (page, resolution, size, background, model view, and so on).</param>
/// <param name="Operations">The image pipeline: the operations in the order they are applied before the analysis; empty for none.</param>
/// <param name="Kind">What is reported.</param>
/// <param name="Count">For <see cref="InspectKind.Colors"/>, the number of dominant colors, <see cref="SurfaceStatistics.MinColors"/> to <see cref="SurfaceStatistics.MaxColors"/>.</param>
/// <param name="Radius">For <see cref="InspectKind.Pixel"/>, the radius of the window around the point, 0 to <see cref="PixelSample.MaxRadius"/>; 0 reports the pixel only.</param>
/// <param name="Point">For <see cref="InspectKind.Pixel"/>, the point, in the coordinates of the image after the operations; it is required there and ignored for colors.</param>
public sealed record InspectRequest(
    SourceOptions Source,
    IReadOnlyList<IImageOperation> Operations,
    InspectKind Kind,
    int Count,
    int Radius,
    PixelPoint? Point);

/// <summary>
///     What an inspection found.
/// </summary>
/// <param name="Info">The facts about the source file.</param>
/// <param name="Page">The 1-based page that was loaded (1 for a single-image source).</param>
/// <param name="Operations">What was done to the source to produce the analyzed pixels.</param>
/// <param name="Diagnostics">The notes reported while loading.</param>
/// <param name="Echo">The text a model printed while it was evaluated.</param>
/// <param name="Width">The width in pixels of the analyzed image (after the operations).</param>
/// <param name="Height">The height in pixels of the analyzed image (after the operations).</param>
/// <param name="Background">The opaque color a translucent <see cref="SourceOptions.Background"/> was reduced to (white when none was given); it is what the report names as the background.</param>
/// <param name="Colors">The dominant colors and luma statistics for <see cref="InspectKind.Colors"/>; otherwise <see langword="null"/>.</param>
/// <param name="Pixel">The sampled pixel (and window) for <see cref="InspectKind.Pixel"/>; otherwise <see langword="null"/>.</param>
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
///         The source is probed by the caller (<see cref="SourceLoader.Probe"/>). The operation does file I/O, holds no
///         state, and is safe to call from several threads.
///     </para>
/// </remarks>
public static class ImageInspector
{
    /// <summary>
    ///     Inspects a source.
    /// </summary>
    /// <param name="info">The facts about the source, from <see cref="SourceLoader.Probe"/>.</param>
    /// <param name="request">What to inspect.</param>
    /// <returns>What was found; <see cref="InspectResult.Colors"/> or <see cref="InspectResult.Pixel"/> is set, by the kind.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the kind is <see cref="InspectKind.Pixel"/> and the request has no point.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>page-out-of-range</c> when the page does not exist, with code <c>usage-error</c> when the
    ///     point lies outside the image after the operations or an operation does not fit the image, and with the codes
    ///     of <see cref="SourceLoader.Load(SourceInfo, SourceOptions)"/> and <see cref="ImagePipeline.Apply"/> when the
    ///     source cannot be loaded or transformed.
    /// </exception>
    public static InspectResult Execute(SourceInfo info, InspectRequest request)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind == InspectKind.Pixel && request.Point == null)
        {
            throw new ArgumentException("A pixel inspection needs a point.", nameof(request));
        }

        SourceLoader.CheckPage(info, request.Source.Page);

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
