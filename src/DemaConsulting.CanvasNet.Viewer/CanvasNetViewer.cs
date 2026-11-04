using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     Renders any file the CanvasNet format packages can read, and creates the image resolver that lets SVG documents
///     embed all of them.
/// </summary>
/// <remarks>
///     <para>
///         <b>The built-in sources.</b> <see cref="ImageSources"/> lists one <see cref="IImageSource"/> per format:
///         raster (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000), SVG, PDF, PPTX, VSDX, OpenSCAD and Vega-Lite. The list is fixed
///         and explicit: there is no discovery, no reflection and no global registry that can be changed. To support
///         another format, or to replace how a built-in one is drawn, implement <see cref="IImageSource"/> and pass it
///         as an extra source; <b>an extra source that claims the same extension or URI scheme as a built-in one wins</b>
///         (the later source in the sequence takes precedence).
///     </para>
///     <para>
///         <b>Embedding.</b> <see cref="CreateImageResolver(string, FolderImageResolverOptions?)"/> returns a
///         <see cref="FolderImageResolver"/> for <c>SvgLoadOptions.ImageResolver</c>. Create one for <b>every</b> top-level
///         render (it is cheap): it counts the images of one render, accumulates the diagnostics, and never caches a
///         picture, so a live source is asked again every time.
///     </para>
///     <para>
///         <b>Rendering.</b> <see cref="Render(string, ViewerRenderOptions?)"/> and
///         <see cref="Render(Stream, string, ViewerRenderOptions?)"/> draw one file with the same sources, the same
///         folder sandbox and the same limits as embedding does. The class is static and stateless: the shared source
///         instances are stateless and thread-safe, and every call builds its own resolver, so the methods are safe to
///         call concurrently.
///     </para>
/// </remarks>
public static class CanvasNetViewer
{
    /// <summary>
    ///     The built-in sources, in a fixed order. Their extensions do not overlap.
    /// </summary>
    private static readonly ReadOnlyCollection<IImageSource> BuiltInSources = new(
    [
        new RasterImageSource(),
        new SvgImageSource(),
        new PdfImageSource(),
        new PptxImageSource(),
        new VsdxImageSource(),
        new ScadImageSource(),
        new VegaLiteImageSource()
    ]);

    /// <summary>
    ///     The extensions of the built-in sources, sorted.
    /// </summary>
    private static readonly IReadOnlyList<string> BuiltInExtensions = new ImageSourceSet(BuiltInSources).Extensions;

    /// <summary>
    ///     Gets the built-in image sources: raster, SVG, PDF, PPTX, VSDX, OpenSCAD and Vega-Lite. The instances are shared,
    ///     stateless and thread-safe; the list cannot be changed. To add your own, compose a new sequence:
    ///     <c>new ImageSourceSet(CanvasNetViewer.ImageSources).Add(mySource)</c> (later sources win).
    /// </summary>
    public static IReadOnlyList<IImageSource> ImageSources => BuiltInSources;

    /// <summary>
    ///     Gets every file name extension the built-in sources claim (lower case, with the dot), sorted: the file types a
    ///     document can embed and <see cref="Render(string, ViewerRenderOptions?)"/> can draw.
    /// </summary>
    public static IReadOnlyList<string> Extensions => BuiltInExtensions;

    /// <summary>
    ///     Creates a resolver that embeds every built-in format from a folder.
    /// </summary>
    /// <param name="folder">
    ///     The folder the links are confined to, normally the folder of the document that is rendered. Must not be null
    ///     or empty; it may be relative.
    /// </param>
    /// <param name="options">The limits and switches, or <see langword="null"/> for the defaults.</param>
    /// <returns>
    ///     A new resolver. It serves one top-level render and is not thread-safe; create a new one for the next render.
    ///     Read its <see cref="FolderImageResolver.Diagnostics"/>, <see cref="FolderImageResolver.Echo"/> and
    ///     <see cref="FolderImageResolver.Embedded"/> after the render.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="folder"/> is null or empty.</exception>
    public static FolderImageResolver CreateImageResolver(string folder, FolderImageResolverOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        return new FolderImageResolver(folder, BuiltInSources, options);
    }

    /// <summary>
    ///     Creates a resolver that embeds every built-in format and your own sources from a folder.
    /// </summary>
    /// <param name="folder">The folder the links are confined to. Must not be null or empty.</param>
    /// <param name="extraSources">
    ///     Your own sources. They are placed <b>after</b> the built-in ones, so one that claims the same extension or URI
    ///     scheme as a built-in source (or as an earlier extra source) wins. Must not be null.
    /// </param>
    /// <param name="options">The limits and switches, or <see langword="null"/> for the defaults.</param>
    /// <returns>A new resolver, as for <see cref="CreateImageResolver(string, FolderImageResolverOptions?)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extraSources"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="folder"/> is null or empty, or a source makes an invalid claim (see
    ///     <see cref="ImageSourceSet"/>).
    /// </exception>
    public static FolderImageResolver CreateImageResolver(
        string folder,
        IEnumerable<IImageSource> extraSources,
        FolderImageResolverOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(extraSources);
        return new FolderImageResolver(folder, [.. BuiltInSources, .. extraSources], options);
    }

    /// <summary>
    ///     Renders a file, whatever its format, as a picture.
    /// </summary>
    /// <param name="path">The path of the file; it must exist and its extension must be one of <see cref="Extensions"/> (or claimed by an extra source).</param>
    /// <param name="options">What to render and how, or <see langword="null"/> for the natural size.</param>
    /// <returns>
    ///     The picture with its notes, text, format and embedded images; the caller disposes it. A document that embeds
    ///     other files (an SVG file, a model with includes) finds them in the sandbox folder
    ///     (<see cref="ViewerRenderOptions.Folder"/>, by default the folder of the file).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before the file is read, when <see cref="ViewerRenderOptions.Width"/> or
    ///     <see cref="ViewerRenderOptions.Height"/> is given and is not from 1 to <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the file is not inside <see cref="ViewerRenderOptions.Folder"/>.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension; the message lists the extensions.</exception>
    /// <exception cref="DemaConsulting.CanvasNet.Files.SandboxException">
    ///     Thrown when the sandbox refuses the file: it is, or is reached through, a symbolic link, or is larger than 16 MiB.
    /// </exception>
    /// <exception cref="ImageSourceException">Thrown when the file is found but cannot be drawn (the original failure is the inner exception).</exception>
    /// <exception cref="ImageSourceBudgetException">
    ///     Thrown when the picture, or the final picture of a render that is resized to the requested size, does not fit in
    ///     <see cref="ViewerRenderOptions.MaxDecodedPixels"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="ViewerRenderOptions.CancellationToken"/> is canceled.</exception>
    public static ViewerImage Render(string path, ViewerRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        var settings = options ?? new ViewerRenderOptions();
        ValidateSize(settings);

        // The file is addressed relative to the sandbox folder, so it goes through the same checks as an embedded link
        var fullPath = Path.GetFullPath(path);
        var folder = settings.Folder is null
            ? Path.GetDirectoryName(fullPath) ?? Path.GetPathRoot(fullPath) ?? fullPath
            : Path.GetFullPath(settings.Folder);
        var relative = Path.GetRelativePath(folder, fullPath);
        if (Path.IsPathRooted(relative)
            || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The file '{path}' is not inside the folder '{folder}'.", nameof(path));
        }

        var resolver = CreateResolver(folder, settings);
        var result = resolver.Render(relative, settings.Width, settings.Height);
        return CreateImage(resolver, result, settings);
    }

    /// <summary>
    ///     Renders a file that is given as a stream, whatever its format, as a picture.
    /// </summary>
    /// <param name="content">The bytes of the file, read to the end (at most 16 MiB); not closed.</param>
    /// <param name="fileName">The name of the file; its extension chooses the source and it names the file in messages.</param>
    /// <param name="options">
    ///     What to render and how, or <see langword="null"/> for the natural size. <see cref="ViewerRenderOptions.Folder"/>
    ///     (optional) is the sandbox for the files the document itself includes or embeds; without it none are resolved.
    /// </param>
    /// <returns>The picture with its notes, text, format and embedded images; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before the stream is read, when <see cref="ViewerRenderOptions.Width"/> or
    ///     <see cref="ViewerRenderOptions.Height"/> is given and is not from 1 to <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension of <paramref name="fileName"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than 16 MiB.</exception>
    /// <exception cref="ImageSourceException">Thrown when the content cannot be drawn (the original failure is the inner exception).</exception>
    /// <exception cref="ImageSourceBudgetException">
    ///     Thrown when the picture, or the final picture of a render that is resized to the requested size, does not fit in
    ///     <see cref="ViewerRenderOptions.MaxDecodedPixels"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="ViewerRenderOptions.CancellationToken"/> is canceled.</exception>
    public static ViewerImage Render(Stream content, string fileName, ViewerRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        var settings = options ?? new ViewerRenderOptions();
        ValidateSize(settings);

        var resolver = CreateResolver(settings.Folder, settings);
        var result = resolver.Render(content, fileName, settings.Width, settings.Height);
        return CreateImage(resolver, result, settings);
    }

    /// <summary>
    ///     Creates the resolver of one render: the built-in sources, then the extra ones.
    /// </summary>
    /// <param name="folder">The sandbox folder, or <see langword="null"/> for none.</param>
    /// <param name="settings">The render options.</param>
    /// <returns>A new resolver.</returns>
    private static FolderImageResolver CreateResolver(string? folder, ViewerRenderOptions settings)
    {
        var limits = new FolderImageResolverOptions
        {
            ThrowOnUnsupportedFeature = settings.ThrowOnUnsupportedFeature,
            CancellationToken = settings.CancellationToken
        };

        // Null keeps the resolver default; a value lets a direct render of one large image raise the cap
        if (settings.MaxDecodedPixels is { } maxDecodedPixels)
        {
            limits = limits with { MaxDecodedPixels = maxDecodedPixels };
        }

        return new FolderImageResolver(folder, [.. BuiltInSources, .. settings.ExtraSources ?? []], limits);
    }

    /// <summary>
    ///     Builds the result of a render from what the resolver returned and collected.
    /// </summary>
    /// <param name="resolver">The resolver of the render.</param>
    /// <param name="result">What the source of the file returned.</param>
    /// <param name="settings">The render options.</param>
    /// <returns>The viewer image; it owns the surface.</returns>
    private static ViewerImage CreateImage(FolderImageResolver resolver, ImageSourceResult result, ViewerRenderOptions settings)
    {
        // A source may return another size than the one asked for (a raster is always natural); the caller gets the size it
        // asked for: the box when both dimensions are given (the picture fitted inside it), or the aspect-preserving size.
        // The final buffer exists next to the surface of the source (already charged), so it must fit in what is left of
        // the live budget; nothing is allocated after it, so it only needs checking, not charging
        var surface = result.Surface;
        try
        {
            if (settings.Width is { } width && settings.Height is { } height)
            {
                if (surface.Width != width || surface.Height != height)
                {
                    EnsureFinalBufferFits(resolver, width, height);
                    var fitted = surface.FitInto(width, height, FitMode.Contain, new Rgba32(0, 0, 0, 0));
                    surface.Dispose();
                    surface = fitted;
                }
            }
            else if (settings.Width is not null || settings.Height is not null)
            {
                var target = new ImageSourceRequest { Href = string.Empty, PixelWidth = settings.Width, PixelHeight = settings.Height }
                    .SizeFor(surface.Width, surface.Height);
                if (surface.Width != target.Width || surface.Height != target.Height)
                {
                    EnsureFinalBufferFits(resolver, target.Width, target.Height);
                    var resized = surface.Resize(target.Width, target.Height);
                    surface.Dispose();
                    surface = resized;
                }
            }
        }
        catch
        {
            // Do not leak the surface the failed step was working on
            surface.Dispose();
            throw;
        }

        return new ViewerImage(
            surface,
            result.Format ?? "unknown",
            [.. result.Diagnostics, .. resolver.Diagnostics],
            [.. result.Echo, .. resolver.Echo],
            resolver.Embedded);
    }

    /// <summary>
    ///     Checks that the options ask for a size the viewer can render.
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width or the height is given and is not from 1 to <see cref="Surface.MaxDimension"/>.</exception>
    private static void ValidateSize(ViewerRenderOptions options)
    {
        foreach (var (name, value) in new[] { (nameof(ViewerRenderOptions.Width), options.Width), (nameof(ViewerRenderOptions.Height), options.Height) })
        {
            if (value is { } size && (size < 1 || size > Surface.MaxDimension))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    size,
                    $"ViewerRenderOptions.{name} must be from 1 to {Surface.MaxDimension} pixels.");
            }
        }
    }

    /// <summary>
    ///     Checks that the final buffer of a post-processed render fits in what is left of the live pixel budget.
    /// </summary>
    /// <param name="resolver">The resolver of the render, whose live budget already holds the surface of the source.</param>
    /// <param name="width">The width of the final picture.</param>
    /// <param name="height">The height of the final picture.</param>
    /// <exception cref="ImageSourceBudgetException">Thrown when the final picture does not fit in the budget that is left.</exception>
    private static void EnsureFinalBufferFits(FolderImageResolver resolver, int width, int height)
    {
        var pixels = (long)width * height;
        if (pixels > resolver.RemainingPixels)
        {
            throw new ImageSourceBudgetException(pixels, resolver.RemainingPixels);
        }
    }
}
