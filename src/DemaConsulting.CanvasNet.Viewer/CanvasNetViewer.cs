using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     Renders any file the CanvasNet format packages can read, and creates the image resolver that lets SVG documents
///     embed all of them.
/// </summary>
/// <remarks>
///     <para>
///         <b>The built-in sources.</b> The <see cref="ImageSources"/> property lists one <see cref="IImageSource"/> per format:
///         raster (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000), SVG, PDF, PPTX, VSDX, OpenSCAD and Vega-Lite. The list is fixed
///         and explicit: there is no discovery, no reflection and no global registry that can be changed. To support
///         another format, or to replace how a built-in one is drawn, implement <see cref="IImageSource"/> and pass it
///         as an extra source; <b>an extra source that claims the same extension or URI scheme as a built-in one wins</b>
///         (the later source in the sequence takes precedence). The built-in sources draw with the same
///         <see cref="Imaging.SourceLoader"/> as the <c>canvasnet</c> tool, so a file embedded in an SVG document and a file
///         passed to <see cref="Render(string, ViewerRenderOptions?)"/> are drawn by one implementation; the instances in
///         <see cref="ImageSources"/> are internal adapters (their names, extensions and URI schemes are those of the
///         sources of the format packages, which remain available stand-alone).
///     </para>
///     <para>
///         <b>Pages and views.</b> A link inside an SVG document chooses the page of a document, or the viewpoint of a
///         model, with the fragment of the link: <c>report.pdf#page=3</c>, <c>deck.pptx#slide=2</c> (or <c>#page=2</c>),
///         <c>drawing.vsdx#page=2</c>, <c>part.scad#view=front</c>. Pages and slides count from 1 and are page 1 without a
///         fragment. A parameter that the source does not use is the warning
///         <see cref="FolderImageResolver.FragmentUnknownParameterCode"/>, a page that does not exist is the unsupported
///         note <see cref="FolderImageResolver.PageOutOfRangeCode"/>, and an unusable value is
///         <see cref="FolderImageResolver.FragmentInvalidCode"/>. In an XML attribute write <c>&amp;</c> as
///         <c>&amp;amp;</c> to give more than one parameter. <see cref="ViewerRenderOptions.Page"/> is the same choice for
///         the file passed to <c>Render</c> itself.
///     </para>
///     <para>
///         <b>Embedding.</b> The overload <see cref="CreateImageResolver(string, FolderImageResolverOptions?)"/> returns a
///         <see cref="FolderImageResolver"/> for <c>SvgLoadOptions.ImageResolver</c>, for a render that you drive
///         yourself. Create one for <b>every</b> top-level render (it is cheap): it counts the images of one render,
///         accumulates the diagnostics, and never caches a picture, so a live source is asked again every time. Your
///         own sources go in the overload with <c>extraSources</c>; the <c>Render</c> methods below need no resolver
///         from you and take your sources from <see cref="ViewerRenderOptions.ExtraSources"/>.
///     </para>
///     <para>
///         <b>Rendering.</b> The methods <see cref="Render(string, ViewerRenderOptions?)"/> and
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
    /// <remarks>
    ///     Each one claims what the source of the matching format package claims, and draws with
    ///     <see cref="Imaging.SourceLoader"/> (see <see cref="LoaderImageSource"/>), so that an embedded file and a file
    ///     rendered by the <c>canvasnet</c> tool are drawn by the same code.
    /// </remarks>
    private static readonly ReadOnlyCollection<IImageSource> BuiltInSources = new(
    [
        new LoaderImageSource(
            new RasterImageSource(),
            [SourceKind.Png, SourceKind.Jpeg, SourceKind.Bmp, SourceKind.Tiff, SourceKind.Gif, SourceKind.Jpeg2000],
            [],
            false),
        new LoaderImageSource(new SvgImageSource(), [SourceKind.Svg], [], false),
        new LoaderImageSource(new PdfImageSource(), [SourceKind.Pdf], ["page"], false),
        new LoaderImageSource(new PptxImageSource(), [SourceKind.Pptx], ["page", "slide"], false),
        new LoaderImageSource(new VsdxImageSource(), [SourceKind.Vsdx], ["page"], false),
        new LoaderImageSource(new ScadImageSource(), [SourceKind.Scad], [], true),
        new LoaderImageSource(new VegaLiteImageSource(), [SourceKind.VegaLite], [], false)
    ]);

    /// <summary>
    ///     The extensions of the built-in sources, sorted.
    /// </summary>
    private static readonly IReadOnlyList<string> BuiltInExtensions = new ImageSourceSet(BuiltInSources).Extensions;

    /// <summary>
    ///     Finds the built-in source that draws a kind of file.
    /// </summary>
    /// <param name="kind">The kind of the file.</param>
    /// <returns>The source; its fragment declaration says which parameters the kind understands.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no built-in source draws the kind (a defect: every kind has one).</exception>
    internal static LoaderImageSource FindBuiltIn(SourceKind kind)
    {
        return BuiltInSources.OfType<LoaderImageSource>().FirstOrDefault(source => source.Draws(kind))
               ?? throw new InvalidOperationException($"No built-in image source draws the kind {kind}.");
    }

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
    ///     The folder the links are confined to, normally the folder of the document that is rendered. Must not be null,
    ///     empty or only white space; it may be relative.
    /// </param>
    /// <param name="options">The limits and switches, or <see langword="null"/> for the defaults.</param>
    /// <returns>
    ///     A new resolver. It serves one top-level render and is not thread-safe; create a new one for the next render.
    ///     Read its <see cref="FolderImageResolver.Diagnostics"/>, <see cref="FolderImageResolver.Echo"/> and
    ///     <see cref="FolderImageResolver.Embedded"/> after the render. It is not <see cref="IDisposable"/> and holds no
    ///     open files; there is nothing to dispose.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="folder"/> is null, empty or only white space.</exception>
    public static FolderImageResolver CreateImageResolver(string folder, FolderImageResolverOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        return new FolderImageResolver(folder, BuiltInSources, options);
    }

    /// <summary>
    ///     Creates a resolver that embeds every built-in format and your own sources from a folder.
    /// </summary>
    /// <remarks>
    ///     Use this for a render that you drive yourself (for example <c>SvgCodec.Load</c> with
    ///     <c>SvgLoadOptions.ImageResolver</c>). <see cref="Render(string, ViewerRenderOptions?)"/> does not need it: it
    ///     builds its own resolver from the built-in sources plus <see cref="ViewerRenderOptions.ExtraSources"/>, so pass
    ///     your sources there instead. The precedence is the same in both: a later source wins.
    /// </remarks>
    /// <param name="folder">The folder the links are confined to. Must not be null, empty or only white space.</param>
    /// <param name="extraSources">
    ///     Your own sources. They are placed <b>after</b> the built-in ones, so one that claims the same extension or URI
    ///     scheme as a built-in source (or as an earlier extra source) wins. Must not be null.
    /// </param>
    /// <param name="options">The limits and switches, or <see langword="null"/> for the defaults.</param>
    /// <returns>
    ///     A new resolver, as for <see cref="CreateImageResolver(string, FolderImageResolverOptions?)"/>; it is not
    ///     <see cref="IDisposable"/> and holds no open files, so there is nothing to dispose.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extraSources"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="folder"/> is null, empty or only white space, or a source makes an invalid claim (see
    ///     <see cref="ImageSourceSet"/>).
    /// </exception>
    public static FolderImageResolver CreateImageResolver(
        string folder,
        IEnumerable<IImageSource> extraSources,
        FolderImageResolverOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(extraSources);
        return new FolderImageResolver(folder, [.. BuiltInSources, .. extraSources], options);
    }

    /// <summary>
    ///     Renders a file, whatever its format, as a picture.
    /// </summary>
    /// <param name="path">The path of the file; it must exist and its extension must be one of <see cref="Extensions"/> (or claimed by an extra source).</param>
    /// <param name="options">What to render and how, or <see langword="null"/> for the natural size.</param>
    /// <returns>
    ///     The picture with its notes, text, format and embedded images; the caller disposes it. It writes no file; to
    ///     save the picture with a format or quality use <c>ImageWriter.Write</c> of the
    ///     <c>DemaConsulting.CanvasNet.Viewer.Imaging</c> namespace. A document that embeds
    ///     other files (an SVG file, a model with includes) finds them in the sandbox folder
    ///     (<see cref="ViewerRenderOptions.Folder"/>, by default the folder of the file).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before the file is read, when <see cref="ViewerRenderOptions.Width"/> or
    ///     <see cref="ViewerRenderOptions.Height"/> is given and is not from 1 to <see cref="Surface.MaxDimension"/>, when
    ///     <see cref="ViewerRenderOptions.Page"/> is less than 1, when <see cref="ViewerRenderOptions.Dpi"/> is not a
    ///     finite number greater than 0 and at most <see cref="SourceLoader.MaxDpi"/>, or when
    ///     <see cref="ViewerRenderOptions.Scad"/> is not usable (an unknown view, an elevation outside -90 to 90, a time
    ///     outside the limit, a triangle limit below 1, a definition the library rejects), or when
    ///     <see cref="ViewerRenderOptions.VegaLite"/> is not usable (a parameter value the library rejects).
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the file is not inside <see cref="ViewerRenderOptions.Folder"/>.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension; the message lists the extensions.</exception>
    /// <exception cref="DemaConsulting.CanvasNet.Files.SandboxException">
    ///     Thrown when the sandbox refuses the file: it is, or is reached through, a symbolic link, or is larger than 16 MiB.
    /// </exception>
    /// <exception cref="ViewerException">
    ///     Thrown, with the code <c>page-out-of-range</c>, when <see cref="ViewerRenderOptions.Page"/> is not a page of the
    ///     file (a single-image file has only page 1); and with the code of the failure (<c>size-limit</c>,
    ///     <c>usage-error</c>, <c>scad-error</c> for a model whose include is missing) for a mistake or limit of the
    ///     built-in sources that has no library exception behind it; with <see cref="ViewerRenderOptions.ThrowOnUnsupportedFeature"/>,
    ///     with the code <c>unsupported-feature</c> for a link of an SVG file that is not drawn.
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
        ValidateOptions(settings);

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
        var direct = CreateDirectOptions(settings);
        var result = RenderDirect(() => resolver.Render(relative, settings.Width, settings.Height, direct));
        return CreateImage(resolver, result, settings, direct, Path.GetFileName(fullPath));
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
    /// <returns>The picture with its notes, text, format, page and embedded images; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before the stream is read, for the options that <see cref="Render(string, ViewerRenderOptions?)"/>
    ///     refuses: a width or height outside 1 to <see cref="Surface.MaxDimension"/>, a page below 1, a resolution that
    ///     is not usable, or model options that are not usable.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension of <paramref name="fileName"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than 16 MiB.</exception>
    /// <exception cref="ViewerException">See <see cref="Render(string, ViewerRenderOptions?)"/>.</exception>
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
        ValidateOptions(settings);

        var resolver = CreateResolver(settings.Folder, settings);
        var direct = CreateDirectOptions(settings);
        var result = RenderDirect(() => resolver.Render(content, fileName, settings.Width, settings.Height, direct));
        return CreateImage(resolver, result, settings, direct, fileName);
    }

    /// <summary>
    ///     Creates the options of the built-in sources for a direct render.
    /// </summary>
    /// <param name="settings">The render options.</param>
    /// <returns>The options; the source fills in the page that was drawn and the page count.</returns>
    private static DirectRenderOptions CreateDirectOptions(ViewerRenderOptions settings)
    {
        return new DirectRenderOptions
        {
            Page = settings.Page,
            Dpi = settings.Dpi,
            Background = settings.Background,
            Scad = settings.Scad,
            VegaLite = settings.VegaLite
        };
    }

    /// <summary>
    ///     Runs the direct render of a resolver and gives its failures the shape <c>Render</c> documents.
    /// </summary>
    /// <param name="render">The render.</param>
    /// <returns>What the source returned.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown for a page that does not exist (<c>page-out-of-range</c>) and for a failure of the built-in sources that
    ///     the loader reports as a <see cref="ViewerException"/> without a library exception behind it.
    /// </exception>
    private static ImageSourceResult RenderDirect(Func<ImageSourceResult> render)
    {
        try
        {
            return render();
        }
        catch (ImageSourceRefusedException ex)
        {
            // Only a page that does not exist can be refused here (the options have no fragment)
            var code = ex.Code == FolderImageResolver.PageOutOfRangeCode ? ErrorCodes.PageOutOfRange : ErrorCodes.UsageError;
            throw new ViewerException(code, ex.Message, "Pages are numbered from 1.", ex);
        }
        catch (ImageSourceException ex) when (ex.InnerException is ViewerException loaderFailure)
        {
            // A mistake of the caller or a limit is not a failure of the file: it keeps its own code
            ExceptionDispatchInfo.Capture(loaderFailure).Throw();
            throw;
        }
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
    /// <param name="direct">The options of the render, in which a built-in source reported the page and the anchors.</param>
    /// <param name="fileName">The name of the rendered file, for the message of a strict failure.</param>
    /// <returns>The viewer image; it owns the surface.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>unsupported-feature</c>, when <see cref="ViewerRenderOptions.ThrowOnUnsupportedFeature"/> is
    ///     set, for a link of the document (at any level) that was not drawn, whose picture did not fit in the budget,
    ///     whose fragment was not usable, or that named a page the file does not have.
    /// </exception>
    private static ViewerImage CreateImage(
        FolderImageResolver resolver,
        ImageSourceResult result,
        ViewerRenderOptions settings,
        DirectRenderOptions direct,
        string fileName)
    {
        // A nested SVG file is drawn through the resolver of this render, which only records the links it could not draw;
        // the strict policy of the tool (a link that was not drawn is an error) is applied here, once, to all of them
        var surface = result.Surface;
        var anchors = direct.Anchors;
        try
        {
            if (settings.ThrowOnUnsupportedFeature)
            {
                SourceLoader.SvgImages.ThrowForUnresolved(resolver.Diagnostics, fileName);
            }

            // A source may return another size than the one asked for (a raster is always natural); the caller gets the size it
            // asked for: the box when both dimensions are given (the picture fitted inside it), or the aspect-preserving size.
            // The final buffer exists next to the surface of the source (already charged), so it must fit in what is left of
            // the live budget; nothing is allocated after it, so it only needs checking, not charging. The anchors follow the
            // same scale and offset as the pixels, and the margin of a fit has the background the caller chose
            var steps = new List<AppliedOperation>();
            var (sourceWidth, sourceHeight) = (surface.Width, surface.Height);
            if (settings.Width is { } width && settings.Height is { } height)
            {
                if (surface.Width != width || surface.Height != height)
                {
                    EnsureFinalBufferFits(resolver, width, height);
                    var fitted = FitIntoBox(surface, width, height, settings.Background ?? new Rgba32(0, 0, 0, 0), steps);
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
                    steps.Add(SourceLoader.Operation("resize", ("width", target.Width), ("height", target.Height)));
                }
            }

            if (steps.Count > 0 && anchors.Count > 0)
            {
                anchors = ImagePipeline.MapAnchors(anchors, sourceWidth, sourceHeight, steps);
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
            resolver.Embedded,
            direct.DrawnPage,
            direct.PageCount,
            anchors);
    }

    /// <summary>
    ///     Fits a surface into a box, keeping its aspect ratio and centering it, and describes the change of scale and
    ///     offset so that positions in the surface can follow.
    /// </summary>
    /// <param name="surface">The surface; it is not disposed.</param>
    /// <param name="width">The width of the box.</param>
    /// <param name="height">The height of the box.</param>
    /// <param name="margin">The color of the margin that the fit leaves around the picture.</param>
    /// <param name="steps">Receives the <c>resize</c> and <c>pad</c> steps that were applied, as the imaging pipeline reports them.</param>
    /// <returns>A new surface of exactly the box size.</returns>
    internal static Surface FitIntoBox(Surface surface, int width, int height, Rgba32 margin, List<AppliedOperation> steps)
    {
        var (fittedWidth, fittedHeight) = Surface.FitInside(surface.Width, surface.Height, width, height);
        var fitted = surface.FitInto(width, height, FitMode.Contain, margin);
        if (fittedWidth != surface.Width || fittedHeight != surface.Height)
        {
            steps.Add(SourceLoader.Operation("resize", ("width", fittedWidth), ("height", fittedHeight)));
        }

        if (fittedWidth != width || fittedHeight != height)
        {
            steps.Add(SourceLoader.Operation(
                "pad",
                ("left", (width - fittedWidth) / 2),
                ("top", (height - fittedHeight) / 2),
                ("width", width),
                ("height", height)));
        }

        return fitted;
    }

    /// <summary>
    ///     Checks that the options are usable, before anything is read.
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the width or the height is given and is not from 1 to <see cref="Surface.MaxDimension"/>, when the
    ///     page is less than 1, or when the resolution or the model options are not usable (the same rules as
    ///     <see cref="SourceLoader.Load(string, SourceOptions)"/> applies).
    /// </exception>
    private static void ValidateOptions(ViewerRenderOptions options)
    {
        ValidateSize(options);
        if (options.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Page, "ViewerRenderOptions.Page must be at least 1.");
        }

        // The loader owns the ranges of the resolution and of the model options, so every route gives the same answer
        if (SourceLoader.FindOptionProblem(new SourceOptions { Dpi = options.Dpi, Scad = options.Scad, VegaLite = options.VegaLite }) is { } problem)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options, problem.Message);
        }
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
