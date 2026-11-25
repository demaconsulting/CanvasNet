using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.MathMl;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.PlantUml;
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
///         raster (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000), SVG, PDF, PPTX, VSDX, OpenSCAD, Vega-Lite, Mermaid, PlantUML and MathML. The list is fixed
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
///         <c>drawing.vsdx#page=2</c>, <c>part.scad#view=front</c>, and a Mermaid diagram takes its theme the same way
///         (<c>flow.mmd#theme=dark</c>). Pages and slides count from 1 and are page 1 without a
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
///         file-access policy (<see cref="ViewerRenderOptions.FileAccess"/>) and the same limits as embedding does. The
///         file passed to <c>Render</c> is the caller's choice and is never judged by the policy; every file it
///         references is, at every level. Every failure of the input is a <see cref="ViewerException"/> with the stable
///         code of the <c>canvasnet</c> tool and, for an error in the source, its location and the code of the format
///         library. The class is static and stateless: the shared source
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
        new LoaderImageSource(new VegaLiteImageSource(), [SourceKind.VegaLite], [], false),
        new LoaderImageSource(new MermaidImageSource(), [SourceKind.Mermaid], [], false),
        new LoaderImageSource(new PlantUmlImageSource(), [SourceKind.PlantUml], [], false),
        new LoaderImageSource(new MathMlImageSource(), [SourceKind.MathMl], [], false)
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
    ///     Gets the built-in image sources: raster, SVG, PDF, PPTX, VSDX, OpenSCAD, Vega-Lite, Mermaid, PlantUML and MathML. The instances are shared,
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
    ///     The folder relative links are resolved against, normally the folder of the document that is rendered (the
    ///     base, not a boundary: <see cref="FolderImageResolverOptions.FileAccess"/> decides which files may be read).
    ///     Must not be null, empty or only white space; it may be relative.
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
    /// <param name="folder">The folder relative links are resolved against. Must not be null, empty or only white space.</param>
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
    ///     The picture with its notes, text, format and embedded images; the caller disposes it. A vector picture comes back
    ///     over opaque white unless <see cref="ViewerRenderOptions.Background"/> sets another color (alpha 0 keeps it
    ///     transparent), as the <c>canvasnet</c> tool draws it; a raster keeps its transparency. It writes no file; to
    ///     save the picture with a format or quality use <c>ImageWriter.Write</c> of the
    ///     <c>DemaConsulting.CanvasNet.Viewer.Imaging</c> namespace. A document that embeds
    ///     other files (an SVG file, a model with includes) finds them relative to its folder (the links of the file
    ///     itself relative to <see cref="ViewerRenderOptions.Folder"/>, by default the folder of the file), under
    ///     <see cref="ViewerRenderOptions.FileAccess"/>. The file itself may lie anywhere, also outside the folder and
    ///     behind a link: it is the caller's choice and is not judged by the policy.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before the file is read, when <see cref="ViewerRenderOptions.Width"/> or
    ///     <see cref="ViewerRenderOptions.Height"/> is given and is not from 1 to <see cref="Surface.MaxDimension"/>, when
    ///     <see cref="ViewerRenderOptions.Page"/> is less than 1, when <see cref="ViewerRenderOptions.Dpi"/> is not a
    ///     finite number greater than 0 and at most <see cref="SourceLoader.MaxDpi"/>, or when
    ///     <see cref="ViewerRenderOptions.Scad"/> is not usable (an unknown view, an elevation outside -90 to 90, a time
    ///     outside the limit, a triangle limit below 1, a definition the library rejects), or when
    ///     <see cref="ViewerRenderOptions.VegaLite"/> is not usable (a parameter value that the library rejects).
    /// </exception>
    /// <exception cref="ViewerException">
    ///     Thrown for every failure of the file and of the files it references, with a stable <see cref="ViewerException.Code"/>
    ///     (one of <see cref="ErrorCodes"/>, the same code the <c>canvasnet</c> tool reports for the same file):
    ///     <c>input-not-found</c> when the file does not exist; <c>unsupported-format</c> when no source claims the
    ///     extension (the message lists the extensions); <c>input-unreadable</c> when the file is not a regular file, is
    ///     larger than 16 MiB, or cannot be read; <c>corrupt-input</c> for a damaged file (malformed SVG markup with its
    ///     line and column in <see cref="ViewerException.Location"/>); <c>scad-error</c>, <c>vega-lite-error</c>,
    ///     <c>mermaid-error</c>, <c>plantuml-error</c> and <c>mathml-error</c> for an error in the source of a model, chart,
    ///     diagram or equation, with
    ///     <see cref="ViewerException.Location"/> (file, line, column, and for a chart the JSON pointer) and
    ///     <see cref="ViewerException.DetailCode"/> (the code of the library, such as <c>mermaid-syntax-error</c>; none for
    ///     a Vega-Lite specification or render error);
    ///     <c>file-access-denied</c> when <see cref="ViewerRenderOptions.FileAccess"/> denied an include or a data file
    ///     (and, with <see cref="ViewerRenderOptions.ThrowOnUnsupportedFeature"/>, a picture); <c>budget-exceeded</c> when
    ///     a limit of a format library is exceeded (see <see cref="ViewerRenderOptions.VegaLiteLimits"/>,
    ///     <see cref="ViewerRenderOptions.MermaidLimits"/>, <see cref="ViewerRenderOptions.PlantUmlLimits"/> and
    ///     <see cref="ViewerRenderOptions.MathMlLimits"/>) or the
    ///     picture, or the final picture of a render that is resized to the
    ///     requested size, does not fit in <see cref="ViewerRenderOptions.MaxDecodedPixels"/>; <c>page-out-of-range</c>
    ///     when <see cref="ViewerRenderOptions.Page"/> is not a page of the file (a single-image file has only page 1);
    ///     <c>size-limit</c> or <c>usage-error</c> for a size or option the source cannot use; with
    ///     <see cref="ViewerRenderOptions.ThrowOnUnsupportedFeature"/>, <c>unsupported-feature</c> for a feature that is
    ///     not drawn or a link of an SVG file that is not drawn, <c>positions-unmatched</c> for a selector of
    ///     <see cref="ViewerRenderOptions.Positions"/> that matched nothing, and <c>svg-binding-failed</c> for a position
    ///     binding of an SVG file that is not resolved; and <c>operation-failed</c> for any other failure of a
    ///     source. The original exception (the library exception, or the <see cref="ImageSourceBudgetException"/> with its
    ///     pixel counts) is the <see cref="Exception.InnerException"/>. Neither the message nor the
    ///     <see cref="ViewerException.Hint"/> names an option of the command-line tool.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="ViewerRenderOptions.CancellationToken"/> is canceled.</exception>
    public static ViewerImage Render(string path, ViewerRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        var settings = options ?? new ViewerRenderOptions();
        ValidateOptions(settings);

        // The file is the caller's choice: it is read wherever it lies, through the same engine (size limit, regular
        // file), and only the files it references are judged by the policy, from its own folder
        var fullPath = Path.GetFullPath(path);
        var folder = settings.Folder is null
            ? Path.GetDirectoryName(fullPath) ?? Path.GetPathRoot(fullPath) ?? fullPath
            : Path.GetFullPath(settings.Folder);
        var resolver = CreateResolver(folder, settings);
        var direct = CreateDirectOptions(settings);
        var result = RenderDirect(
            () => resolver.Render(fullPath, settings.Width, settings.Height, direct),
            path,
            resolver,
            readsStream: false);
        return CreateImage(resolver, result, settings, direct, Path.GetFileName(fullPath));
    }

    /// <summary>
    ///     Renders a file that is given as a stream, whatever its format, as a picture.
    /// </summary>
    /// <param name="content">The bytes of the file, read to the end (at most 16 MiB); not closed.</param>
    /// <param name="fileName">The name of the file; its extension chooses the source and it names the file in messages.</param>
    /// <param name="options">
    ///     What to render and how, or <see langword="null"/> for the natural size. <see cref="ViewerRenderOptions.Folder"/>
    ///     (optional) is the base of the files the document itself includes or embeds, read under
    ///     <see cref="ViewerRenderOptions.FileAccess"/>; without it none are resolved.
    /// </param>
    /// <returns>
    ///     The picture with its notes, text, format, page and embedded images, over the same default background as
    ///     <see cref="Render(string, ViewerRenderOptions?)"/>; the caller disposes it.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown, before anything is read, when <paramref name="content"/> cannot be read.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown, before the stream is read, for the options that <see cref="Render(string, ViewerRenderOptions?)"/>
    ///     refuses: a width or height outside 1 to <see cref="Surface.MaxDimension"/>, a page below 1, a resolution that
    ///     is not usable, or model options that are not usable.
    /// </exception>
    /// <exception cref="ViewerException">
    ///     See <see cref="Render(string, ViewerRenderOptions?)"/>; a stream longer than 16 MiB, or whose reading fails, is
    ///     <c>input-unreadable</c>, and an extension of <paramref name="fileName"/> that no source claims is
    ///     <c>unsupported-format</c>.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="ViewerRenderOptions.CancellationToken"/> is canceled.</exception>
    public static ViewerImage Render(Stream content, string fileName, ViewerRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        if (!content.CanRead)
        {
            throw new ArgumentException("The stream cannot be read.", nameof(content));
        }

        var settings = options ?? new ViewerRenderOptions();
        ValidateOptions(settings);

        var resolver = CreateResolver(settings.Folder, settings);
        var direct = CreateDirectOptions(settings);
        var result = RenderDirect(
            () => resolver.Render(content, fileName, settings.Width, settings.Height, direct),
            fileName,
            resolver,
            readsStream: true);
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
            VegaLite = settings.VegaLite,
            Positions = settings.Positions
        };
    }

    /// <summary>
    ///     Runs the direct render of a resolver and gives its failures the shape <c>Render</c> documents: every failure of
    ///     the input is a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="render">The render.</param>
    /// <param name="name">The path or file name the caller gave, for messages.</param>
    /// <param name="resolver">The resolver of the render, which knows the extensions its sources claim.</param>
    /// <param name="readsStream">Whether the render reads a stream the caller gave.</param>
    /// <returns>What the source returned.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown for a page that does not exist (<c>page-out-of-range</c>), a file that does not exist
    ///     (<c>input-not-found</c>), an extension no source claims (<c>unsupported-format</c>), a file that is not a regular
    ///     file, is larger than 16 MiB or cannot be read, or a stream of the caller whose reading fails
    ///     (<c>input-unreadable</c>), a picture over the pixel budget (<c>budget-exceeded</c>), and a failure of a source:
    ///     the <see cref="ViewerException"/> of a built-in source as it is, the library exception of a nested picture or
    ///     an extra source mapped by the shared mapping of the loader (<see cref="SourceLoader.MapFailure"/>), and
    ///     anything else as <c>operation-failed</c>; the original exception is always the inner exception.
    /// </exception>
    /// <remarks>
    ///     The command-line tool reads its files through the same loader mapping; the resolver failures mapped here (the
    ///     budget, an extension no source claims, the top-level file reference, the read of the caller's stream) are those
    ///     of the resolver only the viewer uses. A failure inside a source reaches here wrapped in an
    ///     <see cref="ImageSourceException"/>, so a bare <see cref="NotSupportedException"/> is either the refusal of an
    ///     extension no source claims or, for a stream whose extension is claimed, the stream refusing to be read.
    ///     Cancellation passes through unchanged.
    /// </remarks>
    private static ImageSourceResult RenderDirect(
        Func<ImageSourceResult> render,
        string name,
        FolderImageResolver resolver,
        bool readsStream)
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
            // The built-in sources already mapped the failure: it keeps its code, location and detail code
            ExceptionDispatchInfo.Capture(loaderFailure).Throw();
            throw;
        }
        catch (ImageSourceException ex)
        {
            // A nested picture or an extra source failed with a library exception: the shared mapping gives it its code
            var inner = ex.InnerException ?? ex;
            var mapped = ex.Embedder is null
                ? SourceLoader.MapFailure(inner, name, null)
                : SourceLoader.SvgImages.MapPicture(ex, name);
            throw mapped ?? new ViewerException(ErrorCodes.OperationFailed, ex.Message, null, inner);
        }
        catch (ImageSourceBudgetException ex)
        {
            throw BudgetExceeded(ex);
        }
        catch (FileNotFoundException ex) when (SourceLoader.MapFailure(ex, name, null) is { } missing)
        {
            throw missing;
        }
        catch (NotSupportedException ex) when (!readsStream || !ClaimsExtension(resolver, name))
        {
            throw new ViewerException(
                ErrorCodes.UnsupportedFormat,
                ex.Message,
                "Render a file whose extension is one of CanvasNetViewer.Extensions, or add an image source that claims it.",
                ex);
        }
        catch (NotSupportedException ex)
        {
            // The extension is claimed, so the caller's stream refused to be read
            throw new ViewerException(ErrorCodes.InputUnreadable, $"'{name}' cannot be read: {ex.Message}", null, ex);
        }
        catch (FileReferenceException ex)
        {
            throw new ViewerException(ErrorCodes.InputUnreadable, ex.Message, null, ex);
        }
        catch (InvalidDataException ex)
        {
            // Only the stream of a render by stream is checked outside a source: it is longer than 16 MiB
            throw new ViewerException(ErrorCodes.InputUnreadable, ex.Message, null, ex);
        }
        catch (IOException ex) when (SourceLoader.MapFailure(ex, name, null) is { } unreadable)
        {
            // The read of the caller's stream failed
            throw unreadable;
        }
    }

    /// <summary>
    ///     Tells whether a source of a resolver claims the extension of a file name, by the rule the resolver uses.
    /// </summary>
    /// <param name="resolver">The resolver.</param>
    /// <param name="name">A file name or a path; only the part after the last slash or backslash is looked at.</param>
    /// <returns><see langword="true"/> when the name ends with a claimed extension (case-insensitively).</returns>
    private static bool ClaimsExtension(FolderImageResolver resolver, string name)
    {
        var fileName = name[(name.AsSpan().LastIndexOfAny('/', '\\') + 1)..];
        return resolver.Extensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Creates the failure of a render that does not fit in the pixel budget.
    /// </summary>
    /// <param name="refusal">The refusal of the budget, with the requested and the remaining pixels; it becomes the inner exception.</param>
    /// <returns>The <c>budget-exceeded</c> exception to throw.</returns>
    private static ViewerException BudgetExceeded(ImageSourceBudgetException refusal)
    {
        return new ViewerException(
            ErrorCodes.BudgetExceeded,
            refusal.Message,
            "Raise ViewerRenderOptions.MaxDecodedPixels to render a larger picture, or ask for a smaller size.",
            refusal);
    }

    /// <summary>
    ///     Creates the resolver of one render: the built-in sources, then the extra ones.
    /// </summary>
    /// <param name="folder">The base folder of the links, or <see langword="null"/> for none.</param>
    /// <param name="settings">The render options.</param>
    /// <returns>A new resolver.</returns>
    /// <remarks>
    ///     When the options set a limit of a chart or a diagram, the built-in sources of this render are new instances that
    ///     carry the limits, so the file and every file drawn through the resolver use them; otherwise the shared sources
    ///     are used.
    /// </remarks>
    private static FolderImageResolver CreateResolver(string? folder, ViewerRenderOptions settings)
    {
        var limits = new FolderImageResolverOptions
        {
            ThrowOnUnsupportedFeature = settings.ThrowOnUnsupportedFeature,
            CancellationToken = settings.CancellationToken,
            FileAccess = settings.FileAccess
        };

        // Null keeps the resolver default; a value lets a direct render of one large image raise the cap
        if (settings.MaxDecodedPixels is { } maxDecodedPixels)
        {
            limits = limits with { MaxDecodedPixels = maxDecodedPixels };
        }

        IEnumerable<IImageSource> builtIn = BuiltInSources;
        if (settings.VegaLiteLimits is not null || settings.MermaidLimits is not null || settings.PlantUmlLimits is not null
            || settings.MathMlLimits is not null)
        {
            var sourceLimits = new SourceLoader.SourceLimits(settings.VegaLiteLimits, settings.MermaidLimits, settings.PlantUmlLimits, settings.MathMlLimits);
            builtIn = BuiltInSources.Cast<LoaderImageSource>().Select(source => source.WithLimits(sourceLimits));
        }

        return new FolderImageResolver(folder, [.. builtIn, .. settings.ExtraSources ?? []], limits);
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
    ///     whose fragment was not usable, or that named a page the file does not have; and with code
    ///     <c>svg-binding-failed</c>, after those, for an SVG position binding (at any level) that was not resolved.
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
        var positions = direct.ReportedPositions;
        try
        {
            if (settings.ThrowOnUnsupportedFeature)
            {
                SourceLoader.SvgImages.ThrowForUnresolved(resolver.Diagnostics, fileName);
                SourceLoader.ThrowForFailedBindings(result.Diagnostics.Select(SourceLoader.SvgImages.ToImageDiagnostic), fileName);
            }

            // A source may return another size than the one asked for (a raster is always natural); the caller gets the size it
            // asked for: the box when both dimensions are given (the picture fitted inside it), or the aspect-preserving size.
            // The final buffer exists next to the surface of the source (already charged), so it must fit in what is left of
            // the live budget; nothing is allocated after it, so it only needs checking, not charging. The anchors follow the
            // same scale and offset as the pixels, and the margin of a fit has the background the caller chose, else the
            // default of the picture (white for a vector source, as the picture itself; transparent for a raster)
            var steps = new List<AppliedOperation>();
            var (sourceWidth, sourceHeight) = (surface.Width, surface.Height);
            if (settings.Width is { } width && settings.Height is { } height)
            {
                if (surface.Width != width || surface.Height != height)
                {
                    EnsureFinalBufferFits(resolver, width, height);
                    var margin = settings.Background ?? (direct.IsVector ? SourceLoader.DefaultVectorBackground : new Rgba32(0, 0, 0, 0));
                    var fitted = FitIntoBox(surface, width, height, margin, steps);
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

            // The feature positions of a diagram or a chart follow the same steps
            if (steps.Count > 0 && positions.Count > 0)
            {
                positions = ImagePipeline.MapPositions(positions, sourceWidth, sourceHeight, steps);
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
            anchors,
            positions,
            direct.PositionsTruncated);
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
    /// <exception cref="ViewerException">Thrown with code <c>budget-exceeded</c> when the final picture does not fit in the budget that is left.</exception>
    private static void EnsureFinalBufferFits(FolderImageResolver resolver, int width, int height)
    {
        var pixels = (long)width * height;
        if (pixels > resolver.RemainingPixels)
        {
            throw BudgetExceeded(new ImageSourceBudgetException(pixels, resolver.RemainingPixels));
        }
    }
}
