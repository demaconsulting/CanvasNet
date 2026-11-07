// cspell:ignore href hrefs embedder
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Files;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Loads the pictures a document links to from one folder, safely, through pluggable <see cref="IImageSource"/>
///     implementations. It is the <see cref="IImageResolver"/> to give to <c>SvgLoadOptions.ImageResolver</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Where a link may point.</b> A link without a URI scheme is a path relative to the folder the resolver was
///         created with (relative to the folder of the file that contains the link, for a link inside an embedded file).
///         It is resolved through a <see cref="SandboxedFileResolver"/>: an absolute path, a drive or UNC path, a path
///         that leaves the folder (<c>..</c>, also percent-encoded), a path through a symbolic link or junction, a
///         control character, an alternate data stream and a file larger than 16 MiB are all refused, and a refused link
///         is a warning with nothing drawn, never a read. A <c>file:</c> URI is accepted only when it names a file inside
///         the folder. The link is read as a URI reference (RFC 3986): <b>a query (<c>?…</c>) is not part of the file
///         name</b> and is ignored for a file link (so <c>image.png?cache=1</c> names <c>image.png</c>), and <b>a
///         fragment (<c>#…</c>) is not part of it either, but is parsed</b> (<see cref="ImageFragment"/>) and given to
///         the source in <see cref="ImageSourceRequest.Fragment"/>, which is how <c>report.pdf#page=3</c> chooses a page;
///         both are cut off before the percent-decoding (an encoded <c>%23</c> or <c>%3F</c> stays a literal <c>#</c> or
///         <c>?</c> in the file name); a link that is only a query or fragment is unresolved. A parameter that the source
///         does not declare (<see cref="IFragmentImageSource"/>) is a warning
///         (<see cref="FragmentUnknownParameterCode"/>) and the picture is drawn without it; a parameter given twice with
///         different values is a warning (<see cref="FragmentDuplicateParameterCode"/>) and the last one wins; a
///         fragment the source cannot use is an <see cref="ImageDiagnosticSeverity.Unsupported"/> note with
///         <see cref="FragmentInvalidCode"/>, and a page that does not exist one with <see cref="PageOutOfRangeCode"/>,
///         and in both cases nothing is drawn and the render carries on. The source still receives the link as written in
///         <see cref="ImageSourceRequest.Href"/>. A link with a URI scheme (<c>http:</c>, <c>ftp:</c>, <c>camera:</c>, ...) is never read from the
///         file system; it is drawn only when an <see cref="IImageSource"/> claims the scheme (see
///         <see cref="IImageSource.Schemes"/>), and then it is that source that owns access and security, and it gets the
///         link exactly as written (its query is not interpreted; its fragment is parsed into the request as well, and
///         only a fragment over the limits of <see cref="ImageFragment"/> is refused). No source in
///         this repository claims <c>http</c> or <c>https</c>, so by default such links are unresolved warnings.
///     </para>
///     <para>
///         <b>Which source draws a file.</b> The file name extension chooses the source (the longest claimed extension
///         wins, case-insensitively, so <c>.vl.json</c> beats <c>.json</c>); a file whose extension nobody claims is an
///         unresolved warning. <b>When several sources claim the same extension or scheme the later one in the sequence
///         wins</b>, so sources the caller adds after the built-in ones override them.
///     </para>
///     <para>
///         <b>Limits.</b> Nesting is limited to <see cref="FolderImageResolverOptions.MaxDepth"/> levels, a file that
///         embeds itself (directly or through others) is a cycle and stops, and at most
///         <see cref="FolderImageResolverOptions.MaxImages"/> images are loaded in total. Each limit that is hit is an
///         unresolved warning, and the render carries on.
///     </para>
///     <para>
///         <b>Memory.</b> A render may allocate at most <see cref="FolderImageResolverOptions.MaxDecodedPixels"/> pixels
///         in all, from one live budget (<see cref="ImageBudget"/>) that the resolver shares with every request, nested
///         ones included. The contract, for a source and for the resolver, is described once, in the remarks of
///         <see cref="ImageSourceRequest"/>; in short, a source charges each surface it allocates
///         (<see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/>), and the resolver refuses a link whose
///         requested size cannot fit, charges whatever a source returned beyond what it charged, and gives back what a
///         failed source charged. <see cref="RemainingPixels"/> is the live part of the budget not yet charged. A picture
///         that does not fit is not drawn and is a warning with the code <see cref="TooLargeCode"/> (a direct render
///         throws <see cref="ImageSourceBudgetException"/> instead). The requested size of every picture is clamped to
///         1 to <see cref="Surface.MaxDimension"/> per side.
///     </para>
///     <para>
///         <b>One resolver, one render; nothing is cached.</b> The resolver counts the images it loads, accumulates
///         <see cref="Diagnostics"/>, <see cref="Echo"/> and <see cref="Embedded"/>, and reads each file from disk once;
///         so create a new resolver for every top-level render (it is cheap), and do not reuse one across renders or
///         threads: it is <b>not thread-safe</b>. It never caches rendered pictures: every <see cref="Resolve(string, int, int)"/> call
///         invokes the source again, so a live source (a video frame) yields a fresh picture each time.
///     </para>
///     <para>
///         <b>Nothing to dispose.</b> The resolver is not <see cref="IDisposable"/>: it holds no open files, streams, or
///         other resources between calls (each file is read completely and closed), so it needs no <c>using</c> and
///         nothing to clean up after a render but reading <see cref="Diagnostics"/>, <see cref="Echo"/>, and
///         <see cref="Embedded"/>. The only disposable objects that pass through it are the <see cref="Surface"/>
///         objects it returns from <see cref="Resolve(string, int, int)"/> (the renderer disposes them) and from <c>Render</c> (the caller
///         disposes <c>result.Surface</c>).
///     </para>
///     <para>
///         <b>Failures.</b> A link that cannot be resolved is not an error (see <see cref="UnresolvedCode"/>). A file that
///         is found but cannot be drawn throws <see cref="ImageSourceException"/> (the original exception is the
///         <see cref="Exception.InnerException"/>), except <see cref="OperationCanceledException"/>, which passes through.
///     </para>
/// </remarks>
public sealed partial class FolderImageResolver : IImageNaturalSizeResolver
{
    /// <summary>
    ///     The code of the warning <see cref="Diagnostics"/> holds for a link that was not drawn (the name says "svg"
    ///     because the links of SVG <c>image</c> elements were the first use).
    /// </summary>
    public const string UnresolvedCode = "svg-image-unresolved";

    /// <summary>
    ///     The code of the warning <see cref="Diagnostics"/> holds for a link that was not drawn because the picture does
    ///     not fit in the pixel budget (<see cref="FolderImageResolverOptions.MaxDecodedPixels"/>): nothing was
    ///     allocated for it and nothing is drawn, and the render carries on.
    /// </summary>
    public const string TooLargeCode = "svg-image-too-large";

    /// <summary>
    ///     The code of the warning <see cref="Diagnostics"/> holds for a fragment parameter that the source of the link
    ///     does not declare (see <see cref="IFragmentImageSource"/>); the picture is drawn without it.
    /// </summary>
    public const string FragmentUnknownParameterCode = "image-fragment-unknown-parameter";

    /// <summary>
    ///     The code of the warning <see cref="Diagnostics"/> holds for a fragment that gives the same parameter more than
    ///     once with different values; the last one wins and the picture is drawn.
    /// </summary>
    public const string FragmentDuplicateParameterCode = "image-fragment-duplicate-parameter";

    /// <summary>
    ///     The code of the <see cref="ImageDiagnosticSeverity.Unsupported"/> note <see cref="Diagnostics"/> holds for a
    ///     link whose fragment cannot be used (it is over the limits of <see cref="ImageFragment"/>, or a source cannot
    ///     use the value of a parameter); nothing is drawn and the render carries on.
    /// </summary>
    public const string FragmentInvalidCode = "image-fragment-invalid";

    /// <summary>
    ///     The code of the <see cref="ImageDiagnosticSeverity.Unsupported"/> note <see cref="Diagnostics"/> holds for a
    ///     link that asks for a page, slide or sheet that the document does not have; nothing is drawn and the render
    ///     carries on.
    /// </summary>
    public const string PageOutOfRangeCode = "image-page-out-of-range";

    /// <summary>
    ///     The sources, with the precedence rule applied.
    /// </summary>
    private readonly ImageSourceSet _sources;

    /// <summary>
    ///     The limits and switches.
    /// </summary>
    private readonly FolderImageResolverOptions _options;

    /// <summary>
    ///     The full path of the folder, or <see langword="null"/> when there is none.
    /// </summary>
    private readonly string? _rootDirectory;

    /// <summary>
    ///     The sandbox that finds files in the folder; created at first use (see <see cref="GetSandbox(bool)"/>), and
    ///     never created when there is no folder.
    /// </summary>
    private SandboxedFileResolver? _sandbox;

    /// <summary>
    ///     Whether the picture of the latest source call was at its own pixel size (<see cref="ImageSourceResult.IsNaturalSize"/>);
    ///     read by <see cref="Resolve(string, int, int, out bool)"/> right after the call, which is safe because the resolver is not thread-safe.
    /// </summary>
    private bool _lastNaturalSize;

    /// <summary>
    ///     The notes of this render, in order.
    /// </summary>
    private readonly List<ImageSourceDiagnostic> _diagnostics = [];

    /// <summary>
    ///     The lines the files printed.
    /// </summary>
    private readonly List<string> _echo = [];

    /// <summary>
    ///     The images that were loaded.
    /// </summary>
    private readonly List<EmbeddedImage> _embedded = [];

    /// <summary>
    ///     The scope of the top-level document: no current file and an empty chain.
    /// </summary>
    private readonly Scope _rootScope;

    /// <summary>
    ///     The number of images that reached a source so far, shared by every level of the render.
    /// </summary>
    private int _loaded;

    /// <summary>
    ///     The live pixel budget of the render, shared by every level and given to every request.
    /// </summary>
    private readonly ImageBudget _budget;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FolderImageResolver"/> class.
    /// </summary>
    /// <param name="rootFolder">
    ///     The folder the links are confined to, normally the folder of the document that is being rendered. May be
    ///     relative (it is made absolute). It may be <see langword="null"/>: then no file can be loaded (every link
    ///     without a scheme is unresolved) and only sources that claim a scheme are used.
    /// </param>
    /// <param name="sources">
    ///     The sources, in order of increasing precedence: <b>when several sources claim the same extension or scheme, the
    ///     later one in the sequence wins</b>, so put your own sources after the built-in ones (for example
    ///     <c>[.. CanvasNetViewer.ImageSources, mine]</c>, or <c>new ImageSourceSet(builtIns).Add(mine)</c>) to override
    ///     them. An <see cref="ImageSourceSet"/> is used as is. Must not be null and must not contain null.
    /// </param>
    /// <param name="options">The limits and switches, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sources"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="rootFolder"/> is empty or only white space (null is allowed and means no folder),
    ///     or a source makes an invalid claim (a malformed extension,
    ///     a malformed scheme, or the reserved schemes <c>file</c> and <c>data</c>; see <see cref="ImageSourceSet"/>).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit in <paramref name="options"/> is negative.</exception>
    public FolderImageResolver(string? rootFolder, IEnumerable<IImageSource> sources, FolderImageResolverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (rootFolder is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);
        }

        _options = options ?? new FolderImageResolverOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(_options.MaxDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(_options.MaxImages);
        ArgumentOutOfRangeException.ThrowIfNegative(_options.MaxDecodedPixels);
        _sources = sources as ImageSourceSet ?? new ImageSourceSet(sources);
        _budget = new ImageBudget(_options.MaxDecodedPixels);
        _rootDirectory = rootFolder is null ? null : Path.GetFullPath(rootFolder);
        _rootScope = new Scope(this, null, [], 0, null, []);
    }

    /// <summary>
    ///     Gets the sandbox, creating it on first use, or <see langword="null"/> when the resolver has no folder.
    /// </summary>
    /// <param name="topLevelFile">
    ///     <see langword="true"/> for the read of the top-level file of a render by path, which takes a file slot of the
    ///     sandbox without counting as an embedded image.
    /// </param>
    /// <returns>The sandbox, or <see langword="null"/> when there is no folder.</returns>
    /// <remarks>
    ///     The sandbox may read as many distinct files as the resolver may load images
    ///     (<see cref="FolderImageResolverOptions.MaxImages"/>), plus one for the top-level file when the render is by
    ///     path (<see cref="Render(string, int?, int?)"/>; a render of a stream reads no top-level file). The limit is
    ///     fixed when the sandbox is created, so a render by path must be the first thing a resolver does; that is how a
    ///     resolver is meant to be used (one resolver, one render).
    /// </remarks>
    private SandboxedFileResolver? GetSandbox(bool topLevelFile = false)
    {
        if (_rootDirectory is null)
        {
            return null;
        }

        return _sandbox ??= new SandboxedFileResolver(
            new SandboxOptions
            {
                BaseDirectory = _rootDirectory,
                MaxFiles = (int)Math.Min((long)_options.MaxImages + (topLevelFile ? 1 : 0), int.MaxValue)
            },
            _options.CancellationToken);
    }

    /// <summary>
    ///     Gets every file name extension the sources claim (lower case, with the dot), sorted and without duplicates.
    /// </summary>
    public IReadOnlyList<string> Extensions => _sources.Extensions;

    /// <summary>
    ///     Gets every URI scheme the sources claim (lower case, without the colon), sorted and without duplicates.
    /// </summary>
    public IReadOnlyList<string> Schemes => _sources.Schemes;

    /// <summary>
    ///     Gets the notes of the render so far, in render order: a warning with the code <see cref="UnresolvedCode"/> for
    ///     every link that was not drawn, a warning with the code <see cref="TooLargeCode"/> for every picture that did
    ///     not fit in the pixel budget, and the notes the sources returned, with the embedding link in front of each
    ///     message (<c>"{href}: {message}"</c>). Notes of a nested file are listed before the note of the file that
    ///     embeds it, because the nested file is drawn first.
    /// </summary>
    public IReadOnlyList<ImageSourceDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    /// <summary>
    ///     Gets the lines the embedded files printed (the <c>echo</c> output of models), in render order.
    /// </summary>
    public IReadOnlyList<string> Echo => _echo.AsReadOnly();

    /// <summary>
    ///     Gets one entry for every image that was loaded and handed to the renderer, in completion order (a nested image
    ///     is listed before the image that embeds it; <see cref="EmbeddedImage.Depth"/> tells them apart).
    /// </summary>
    public IReadOnlyList<EmbeddedImage> Embedded => _embedded.AsReadOnly();

    /// <summary>
    ///     Gets the part of the pixel budget (<see cref="FolderImageResolverOptions.MaxDecodedPixels"/>) that is free
    ///     right now: the maximum minus the pixels charged so far for the pictures this resolver has drawn (see the
    ///     remarks of <see cref="ImageSourceRequest"/> for who charges what). A caller that allocates a further buffer of
    ///     its own after a render, such as a resized final picture, can compare its size with this value to stay inside
    ///     the same budget.
    /// </summary>
    public long RemainingPixels => _budget.Remaining;

    /// <summary>
    ///     Loads the picture a link names, for the top-level document.
    /// </summary>
    /// <param name="href">The link as written. Relative paths are relative to the folder of the resolver.</param>
    /// <param name="pixelWidth">The device-pixel width of the placement; passed to the source.</param>
    /// <param name="pixelHeight">The device-pixel height of the placement; passed to the source.</param>
    /// <returns>
    ///     The picture, which the caller owns and disposes, or <see langword="null"/> when the link was not drawn (a
    ///     warning is added to <see cref="Diagnostics"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="href"/> is null.</exception>
    /// <exception cref="ImageSourceException">Thrown when the file was found but its source failed to draw it.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="FolderImageResolverOptions.CancellationToken"/> is canceled.</exception>
    public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
    {
        return ResolveIn(_rootScope, href, pixelWidth, pixelHeight);
    }

    /// <inheritdoc />
    public Surface? Resolve(string href, int pixelWidth, int pixelHeight, out bool isNaturalSize)
    {
        return ResolveIn(_rootScope, href, pixelWidth, pixelHeight, out isNaturalSize);
    }

    /// <summary>
    ///     Draws a file of the folder directly, as the top-level picture, and fails loudly when it cannot.
    /// </summary>
    /// <param name="path">
    ///     The path of the file relative to the folder of the resolver, resolved by the same sandbox as a link (so an
    ///     absolute path, a path that leaves the folder, and a link are refused). A <c>file:</c> URI or percent-encoding is
    ///     not interpreted.
    /// </param>
    /// <param name="pixelWidth">The requested width, or <see langword="null"/> for the natural width.</param>
    /// <param name="pixelHeight">The requested height, or <see langword="null"/> for the natural height.</param>
    /// <returns>
    ///     What the source returned, with <see cref="ImageSourceResult.Format"/> always set. The caller owns the surface.
    ///     Notes of the files that this file embeds are in <see cref="Diagnostics"/>, <see cref="Echo"/> and
    ///     <see cref="Embedded"/>; the notes in the result are those of the file itself. The file counts against the depth
    ///     chain (so a file that embeds itself stops at once) but not against <see cref="FolderImageResolverOptions.MaxImages"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resolver was created without a folder.</exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension; the message lists the extensions.</exception>
    /// <exception cref="SandboxException">Thrown when the sandbox refuses the path.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="ImageSourceException">Thrown when the source fails to draw the file.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown when the picture does not fit in <see cref="FolderImageResolverOptions.MaxDecodedPixels"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="FolderImageResolverOptions.CancellationToken"/> is canceled.</exception>
    public ImageSourceResult Render(string path, int? pixelWidth = null, int? pixelHeight = null)
    {
        return Render(path, pixelWidth, pixelHeight, null);
    }

    /// <summary>
    ///     Draws a file of the folder directly, as the top-level picture, with caller-defined options for the source, and
    ///     fails loudly when it cannot.
    /// </summary>
    /// <param name="path">
    ///     The path of the file relative to the folder of the resolver, resolved by the same sandbox as a link. It is a
    ///     plain path: a <c>file:</c> URI, percent-encoding, a query and a fragment are not interpreted (the page to draw
    ///     is chosen with <paramref name="options"/>, not with a fragment).
    /// </param>
    /// <param name="pixelWidth">The requested width, or <see langword="null"/> for the natural width.</param>
    /// <param name="pixelHeight">The requested height, or <see langword="null"/> for the natural height.</param>
    /// <param name="options">
    ///     The options the source understands, passed untouched in <see cref="ImageSourceRequest.Options"/>; the resolver
    ///     never reads them. Files this file embeds never get them.
    /// </param>
    /// <returns>See <see cref="Render(string, int?, int?)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resolver was created without a folder.</exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension; the message lists the extensions.</exception>
    /// <exception cref="SandboxException">Thrown when the sandbox refuses the path.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="ImageSourceException">Thrown when the source fails to draw the file.</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown, as the source threw it, when the source declines the picture (for example a page that does not exist).</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown when the picture does not fit in <see cref="FolderImageResolverOptions.MaxDecodedPixels"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="FolderImageResolverOptions.CancellationToken"/> is canceled.</exception>
    public ImageSourceResult Render(string path, int? pixelWidth, int? pixelHeight, IImageSourceOptions? options)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (_rootDirectory is null)
        {
            throw new InvalidOperationException("The resolver was created without a folder, so it cannot render a file by path; use Render(Stream, string).");
        }

        _options.CancellationToken.ThrowIfCancellationRequested();

        // The extension is checked first so a file nobody can draw is never read
        var source = _sources.FindByExtension(path) ?? throw NoSource(path);
        var sandbox = GetSandbox(topLevelFile: true)!;
        var file = sandbox.Resolve(path)
                   ?? throw new FileNotFoundException($"The file '{path}' was not found in the folder.", path);
        if (!ReferenceEquals(_sources.FindByExtension(file.Key), source))
        {
            throw NoSource(path);
        }

        var name = RelativeName(file);
        var (width, height) = PrepareDirect(pixelWidth, pixelHeight);
        var request = new ImageSourceRequest
        {
            Href = path,
            Content = file.Content,
            FileName = name,
            Directory = file.Directory,
            PixelWidth = width,
            PixelHeight = height,
            Budget = _budget,
            Resolver = new Scope(this, file, [file.Key], 0, name, []),
            Options = options,
            ThrowOnUnsupportedFeature = _options.ThrowOnUnsupportedFeature,
            CancellationToken = _options.CancellationToken
        };
        return RenderDirect(source, request);
    }

    /// <summary>
    ///     Draws a file given as a stream, as the top-level picture, and fails loudly when it cannot.
    /// </summary>
    /// <param name="content">The bytes of the file; read to the end, at most 16 MiB. It is not closed.</param>
    /// <param name="fileName">
    ///     The name of the file; its extension chooses the source and it is the source name in messages. It need not exist.
    /// </param>
    /// <param name="pixelWidth">The requested width, or <see langword="null"/> for the natural width.</param>
    /// <param name="pixelHeight">The requested height, or <see langword="null"/> for the natural height.</param>
    /// <returns>
    ///     What the source returned, with <see cref="ImageSourceResult.Format"/> always set. The caller owns the surface.
    ///     The files the document links to are resolved against the folder of the resolver (none when it has no folder),
    ///     and <see cref="ImageSourceRequest.Directory"/> is that folder.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension of <paramref name="fileName"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than 16 MiB.</exception>
    /// <exception cref="ImageSourceException">Thrown when the source fails to draw the file.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown when the picture does not fit in <see cref="FolderImageResolverOptions.MaxDecodedPixels"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="FolderImageResolverOptions.CancellationToken"/> is canceled.</exception>
    public ImageSourceResult Render(Stream content, string fileName, int? pixelWidth = null, int? pixelHeight = null)
    {
        return Render(content, fileName, pixelWidth, pixelHeight, null);
    }

    /// <summary>
    ///     Draws a file given as a stream, as the top-level picture, with caller-defined options for the source, and
    ///     fails loudly when it cannot.
    /// </summary>
    /// <param name="content">The bytes of the file; read to the end, at most 16 MiB. It is not closed.</param>
    /// <param name="fileName">The name of the file; its extension chooses the source. It need not exist.</param>
    /// <param name="pixelWidth">The requested width, or <see langword="null"/> for the natural width.</param>
    /// <param name="pixelHeight">The requested height, or <see langword="null"/> for the natural height.</param>
    /// <param name="options">
    ///     The options the source understands, passed untouched in <see cref="ImageSourceRequest.Options"/>; the resolver
    ///     never reads them. Files this file embeds never get them.
    /// </param>
    /// <returns>See <see cref="Render(Stream, string, int?, int?)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="options"/> is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when no source claims the extension of <paramref name="fileName"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than 16 MiB.</exception>
    /// <exception cref="ImageSourceException">Thrown when the source fails to draw the file.</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown, as the source threw it, when the source declines the picture (for example a page that does not exist).</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown when the picture does not fit in <see cref="FolderImageResolverOptions.MaxDecodedPixels"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <see cref="FolderImageResolverOptions.CancellationToken"/> is canceled.</exception>
    public ImageSourceResult Render(Stream content, string fileName, int? pixelWidth, int? pixelHeight, IImageSourceOptions? options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(fileName);
        _options.CancellationToken.ThrowIfCancellationRequested();

        var source = _sources.FindByExtension(fileName) ?? throw NoSource(fileName);
        var (width, height) = PrepareDirect(pixelWidth, pixelHeight);
        var request = new ImageSourceRequest
        {
            Href = fileName,
            Content = ReadBounded(content),
            FileName = fileName,
            Directory = _rootDirectory,
            PixelWidth = width,
            PixelHeight = height,
            Budget = _budget,
            Resolver = new Scope(this, null, [], 0, fileName, []),
            Options = options,
            ThrowOnUnsupportedFeature = _options.ThrowOnUnsupportedFeature,
            CancellationToken = _options.CancellationToken
        };
        return RenderDirect(source, request);
    }

    /// <summary>
    ///     Resolves a link inside a scope and tells whether the picture is at its own pixel size.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="pixelWidth">The device-pixel width of the placement.</param>
    /// <param name="pixelHeight">The device-pixel height of the placement.</param>
    /// <param name="isNaturalSize">Receives <see cref="ImageSourceResult.IsNaturalSize"/> of the picture that was returned; <see langword="false"/> when none.</param>
    /// <returns>The picture, or <see langword="null"/> when the link was not drawn.</returns>
    private Surface? ResolveIn(Scope scope, string href, int pixelWidth, int pixelHeight, out bool isNaturalSize)
    {
        // The flag is set by the call of the source that drew the picture (the last one when files embed files), so it is read
        // right after; the resolver is not thread-safe, which is what makes this field safe
        _lastNaturalSize = false;
        var surface = ResolveIn(scope, href, pixelWidth, pixelHeight);
        isNaturalSize = surface is not null && _lastNaturalSize;
        return surface;
    }

    /// <summary>
    ///     Resolves a link inside a scope: the top-level document, or an embedded file or scheme-claimed picture.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="pixelWidth">The device-pixel width of the placement.</param>
    /// <param name="pixelHeight">The device-pixel height of the placement.</param>
    /// <returns>The picture, or <see langword="null"/> when the link was not drawn.</returns>
    private Surface? ResolveIn(Scope scope, string href, int pixelWidth, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(href);
        _options.CancellationToken.ThrowIfCancellationRequested();

        // A scheme (other than file:) is handed to the source that claims it, as written; everything else is a path
        var scheme = ImageSourceSet.GetScheme(href);
        return scheme is not null && scheme != "file"
            ? ResolveScheme(scope, href, scheme, pixelWidth, pixelHeight)
            : ResolveFile(scope, href, pixelWidth, pixelHeight);
    }

    /// <summary>
    ///     Resolves a link that has a URI scheme.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="scheme">The scheme in lower case.</param>
    /// <param name="pixelWidth">The device-pixel width of the placement.</param>
    /// <param name="pixelHeight">The device-pixel height of the placement.</param>
    /// <returns>The picture, or <see langword="null"/> when no source claims the scheme or a limit is hit.</returns>
    private Surface? ResolveScheme(Scope scope, string href, string scheme, int pixelWidth, int pixelHeight)
    {
        // No file system is involved: the link is either claimed by a source (which owns access and security) or refused
        var source = _sources.FindByScheme(scheme);
        if (source is null)
        {
            return Unresolved(scope, href, $"network and other URLs are never loaded (no image source claims the scheme '{scheme}')");
        }

        // The chain holds the link itself, so a source that resolves its own link again is a cycle
        var key = "scheme:" + href;

        // The source owns its link, but the fragment must still be within the limits that protect every source
        var fragment = ImageFragment.Parse(SplitHref(href).Fragment);
        if (fragment.Problem is not null)
        {
            return Refuse(scope, href, FragmentInvalidCode, fragment.Problem);
        }

        if (!TryClampRequest(scope, href, pixelWidth, pixelHeight, out var width, out var height))
        {
            return null;
        }

        // No file is read for a scheme-claimed link, so the cycle check (which names the better reason) can run first
        var reason = CheckCycle(scope, key) ?? CheckDepthAndCount(scope);
        if (reason is not null)
        {
            return Unresolved(scope, href, reason);
        }

        _loaded++;
        var request = new ImageSourceRequest
        {
            Href = href,
            Fragment = fragment,
            PixelWidth = width,
            PixelHeight = height,
            Budget = _budget,
            Resolver = new Scope(this, null, [.. scope.Chain, key], scope.Depth + 1, href, [.. scope.Origin, href]),
            ThrowOnUnsupportedFeature = _options.ThrowOnUnsupportedFeature,
            CancellationToken = _options.CancellationToken
        };
        return Draw(scope, source, request);
    }

    /// <summary>
    ///     Resolves a link that names a file in the folder.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="pixelWidth">The device-pixel width of the placement.</param>
    /// <param name="pixelHeight">The device-pixel height of the placement.</param>
    /// <returns>The picture, or <see langword="null"/> when the link was not drawn.</returns>
    private Surface? ResolveFile(Scope scope, string href, int pixelWidth, int pixelHeight)
    {
        var sandbox = GetSandbox();
        if (sandbox is null)
        {
            return Unresolved(scope, href, "the resolver has no folder, so no file can be loaded");
        }

        // Turn the link into a path the sandbox accepts, and pick the source by the extension before touching any file
        var (path, relativeToCurrent, reason) = ToRelativePath(href);
        if (path is null)
        {
            return Unresolved(scope, href, reason ?? "the reference cannot be resolved");
        }

        var source = _sources.FindByExtension(path);
        if (source is null)
        {
            return Unresolved(scope, href, DescribeUnknownFormat());
        }

        // The fragment is checked against the limits before any file is read or counted: a hostile link costs nothing
        var fragment = ImageFragment.Parse(SplitHref(href).Fragment);
        if (fragment.Problem is not null)
        {
            return Refuse(scope, href, FragmentInvalidCode, fragment.Problem);
        }

        // The depth and count limits need no file, so they are applied before the sandbox opens (reads) it: an image
        // that is over a limit is never read. Only the cycle check needs the resolved key and has to wait
        reason = CheckDepthAndCount(scope);
        if (reason is not null)
        {
            return Unresolved(scope, href, reason);
        }

        // The sandbox does every safety check; a refusal is a reason, never a read
        SandboxedFile? file;
        try
        {
            file = sandbox.Resolve(path, relativeToCurrent ? scope.Current : null);
        }
        catch (SandboxException ex)
        {
            return Unresolved(scope, href, DescribeSandboxFailure(ex));
        }

        if (file is null)
        {
            return Unresolved(scope, href, "the file was not found");
        }

        // The name of the file the sandbox found must lead to the same source as the text of the link
        if (!ReferenceEquals(_sources.FindByExtension(file.Key), source))
        {
            return Unresolved(scope, href, DescribeUnknownFormat());
        }

        if (!TryClampRequest(scope, href, pixelWidth, pixelHeight, out var width, out var height))
        {
            return null;
        }

        // The image is counted only now, when every check has passed
        reason = CheckCycle(scope, file.Key);
        if (reason is not null)
        {
            return Unresolved(scope, href, reason);
        }

        _loaded++;
        var name = RelativeName(file);
        var request = new ImageSourceRequest
        {
            Href = href,
            Fragment = fragment,
            Content = file.Content,
            FileName = name,
            Directory = file.Directory,
            PixelWidth = width,
            PixelHeight = height,
            Budget = _budget,
            Resolver = new Scope(this, file, [.. scope.Chain, file.Key], scope.Depth + 1, name, [.. scope.Origin, name]),
            ThrowOnUnsupportedFeature = _options.ThrowOnUnsupportedFeature,
            CancellationToken = _options.CancellationToken
        };
        return Draw(scope, source, request);
    }

    /// <summary>
    ///     Applies the depth and image-count limits to an image about to be loaded; they need no file, so they are
    ///     checked before the file is opened.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <returns>The reason the image must not be loaded, or <see langword="null"/> when it may be.</returns>
    private string? CheckDepthAndCount(Scope scope)
    {
        if (scope.Depth >= _options.MaxDepth)
        {
            return $"files are embedded in each other more than {_options.MaxDepth} levels deep";
        }

        if (_loaded >= _options.MaxImages)
        {
            return $"more than {_options.MaxImages} images are embedded";
        }

        return null;
    }

    /// <summary>
    ///     Applies the cycle check to an image about to be loaded: it needs the identity of the image, so it can only run
    ///     once the file is resolved.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="key">The identity of the image: the full path of the file, or the scheme-claimed link.</param>
    /// <returns>The reason the image must not be loaded, or <see langword="null"/> when it may be.</returns>
    /// <remarks>
    ///     A file that embeds itself stops at once, and one file embedding another that embeds it stops at the second
    ///     occurrence.
    /// </remarks>
    private string? CheckCycle(Scope scope, string key)
    {
        var comparer = GetSandbox()?.KeyComparer ?? StringComparer.Ordinal;
        return scope.Chain.Contains(key, comparer) ? "the file embeds itself (a cycle)" : null;
    }

    /// <summary>
    ///     Clamps the placement size of a link and checks that a picture of that size fits in the pixel budget.
    /// </summary>
    /// <param name="scope">The scope that contains the link, whose origin goes on the note.</param>
    /// <param name="href">The link as written, for the note.</param>
    /// <param name="pixelWidth">The device-pixel width of the placement.</param>
    /// <param name="pixelHeight">The device-pixel height of the placement.</param>
    /// <param name="width">The width to request: <paramref name="pixelWidth"/> clamped to 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <param name="height">The height to request: <paramref name="pixelHeight"/> clamped to 1 to <see cref="Surface.MaxDimension"/>.</param>
    /// <returns><see langword="true"/> when it fits; otherwise a note was added and the link is not drawn.</returns>
    private bool TryClampRequest(Scope scope, string href, int pixelWidth, int pixelHeight, out int width, out int height)
    {
        width = Math.Clamp(pixelWidth, 1, Surface.MaxDimension);
        height = Math.Clamp(pixelHeight, 1, Surface.MaxDimension);
        var requested = (long)width * height;
        if (requested > RemainingPixels)
        {
            TooLarge(scope, href, requested);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Clamps the optional size of a direct render and checks that it can fit in the pixel budget.
    /// </summary>
    /// <param name="pixelWidth">The requested width, or <see langword="null"/> for the natural width.</param>
    /// <param name="pixelHeight">The requested height, or <see langword="null"/> for the natural height.</param>
    /// <returns>The clamped size; a dimension that was not requested stays <see langword="null"/>.</returns>
    /// <remarks>
    ///     This is only the cheap early refusal, like the one for a link: nothing is charged, the source charges what
    ///     it allocates. A natural-size render has no size to check.
    /// </remarks>
    /// <exception cref="ImageSourceBudgetException">Thrown when the requested size exceeds the budget that is left.</exception>
    private (int? Width, int? Height) PrepareDirect(int? pixelWidth, int? pixelHeight)
    {
        int? width = pixelWidth is { } w ? Math.Clamp(w, 1, Surface.MaxDimension) : null;
        int? height = pixelHeight is { } h ? Math.Clamp(h, 1, Surface.MaxDimension) : null;
        var requested = width is null && height is null ? 0L : (long)(width ?? 1) * (height ?? 1);
        if (requested > _budget.Remaining)
        {
            throw new ImageSourceBudgetException(requested, _budget.Remaining);
        }

        return (width, height);
    }

    /// <summary>
    ///     Calls a source for an embedded image and records what it reports.
    /// </summary>
    /// <param name="scope">The scope that contains the link.</param>
    /// <param name="source">The source.</param>
    /// <param name="request">The request.</param>
    /// <returns>The picture, which the caller owns, or <see langword="null"/> when it does not fit in the pixel budget.</returns>
    private Surface? Draw(Scope scope, IImageSource source, ImageSourceRequest request)
    {
        _options.CancellationToken.ThrowIfCancellationRequested();

        // The fragment parameters the source does not use, and the ones given twice, are reported before the source runs
        ReportFragmentParameters(scope, source, request);

        // A refusal for the budget is a warning and nothing is drawn; so is a source that declines the link itself (a page
        // that does not exist); the render carries on
        ImageSourceResult result;
        try
        {
            result = Run(source, request, scope.Name);
        }
        catch (ImageSourceBudgetException ex)
        {
            TooLarge(scope, request.Href, ex.RequestedPixels);
            return null;
        }
        catch (ImageSourceRefusedException ex)
        {
            return Refuse(scope, request.Href, ex.Code, ex.Message);
        }

        // The notes get the embedding link in front of the message and the chain of files in front of the origin, so
        // where a note comes from is clear; the rest is kept as reported. The file that reported them is the last link
        // of the chain: the scope that was created for the request
        var origin = request.Resolver is Scope reporter ? reporter.Origin : scope.Origin;
        foreach (var note in result.Diagnostics)
        {
            _diagnostics.Add(note with { Message = $"{request.Href}: {note.Message}", Origin = [.. origin, .. note.Origin] });
        }

        _echo.AddRange(result.Echo);
        _lastNaturalSize = result.IsNaturalSize;
        _embedded.Add(new EmbeddedImage(request.Href, result.Format ?? source.Name, result.Surface.Width, result.Surface.Height)
        {
            Depth = scope.Depth
        });
        return result.Surface;
    }

    /// <summary>
    ///     Draws the top-level file of a direct render.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="request">The request.</param>
    /// <returns>The result, with the format filled in.</returns>
    /// <exception cref="ImageSourceBudgetException">Thrown when the picture does not fit in the pixel budget.</exception>
    private ImageSourceResult RenderDirect(IImageSource source, ImageSourceRequest request)
    {
        var result = Run(source, request, null);
        return result.Format is null ? result with { Format = source.Name } : result;
    }

    /// <summary>
    ///     Calls a source and settles its charges against the budget: the single place where the resolver's side of the
    ///     budget contract (see <see cref="ImageSourceRequest"/>) is applied.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="request">The request, whose <see cref="ImageSourceRequest.ChargedPixels"/> the source has filled in.</param>
    /// <param name="embedder">The file that contains the link, or <see langword="null"/> for the top level.</param>
    /// <returns>What the source returned, with its surface charged to the budget.</returns>
    /// <exception cref="ImageSourceBudgetException">
    ///     Thrown when the source refused for the budget, or returned a surface larger than what it charged for that does
    ///     not fit in the budget that is left (the surface is disposed).
    /// </exception>
    /// <exception cref="ImageSourceException">Thrown when the source throws, or returns nothing.</exception>
    private ImageSourceResult Run(IImageSource source, ImageSourceRequest request, string? embedder)
    {
        // Whatever goes wrong, what this request charged goes back, so a failed source leaks no budget (the pictures that
        // nested requests drew stay charged: they were allocated)
        ImageSourceResult result;
        try
        {
            result = Invoke(source, request, embedder);
        }
        catch
        {
            request.RefundCharges();
            throw;
        }

        // A source that charged less than it returned (a raster comes back at its own size, a custom source may not have
        // checked) is charged the difference now; a surface that does not fit is discarded
        var returned = (long)result.Surface.Width * result.Surface.Height;
        var excess = returned - request.ChargedPixels;
        if (excess > _budget.Remaining)
        {
            result.Surface.Dispose();
            request.RefundCharges();
            throw new ImageSourceBudgetException(returned, _budget.Remaining);
        }

        if (excess > 0)
        {
            _budget.Charge(excess);
        }

        return result;
    }
    /// <summary>
    ///     Calls a source and wraps the failures that are not cancellation.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="request">The request.</param>
    /// <param name="embedder">The file that contains the link, or <see langword="null"/> for the top level.</param>
    /// <returns>What the source returned.</returns>
    /// <exception cref="ImageSourceException">Thrown when the source throws, or returns nothing.</exception>
    private static ImageSourceResult Invoke(IImageSource source, ImageSourceRequest request, string? embedder)
    {
        try
        {
            return source.Render(request)
                   ?? throw new InvalidOperationException($"The image source '{source.Name}' returned no result.");
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or ImageSourceException or ImageSourceBudgetException or ImageSourceRefusedException))
        {
            // The original exception stays the inner one, so its type is intact for callers that map it
            throw new ImageSourceException(
                $"The image '{request.Href}' could not be drawn by the '{source.Name}' source: {ex.Message}",
                request.Href,
                embedder,
                ex);
        }
    }

    /// <summary>
    ///     Reports a link that was not drawn.
    /// </summary>
    /// <param name="scope">The scope that contains the link; its origin goes on the note.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="reason">Why it was not drawn.</param>
    /// <returns>Always <see langword="null"/>: nothing is drawn.</returns>
    private Surface? Unresolved(Scope scope, string href, string reason)
    {
        _diagnostics.Add(
            new ImageSourceDiagnostic("svg", ImageDiagnosticSeverity.Warning, UnresolvedCode, $"image '{href}' was not drawn: {reason}")
            {
                Origin = scope.Origin
            });
        return null;
    }

    /// <summary>
    ///     Reports a link that a source or the resolver declined to draw because of the link itself.
    /// </summary>
    /// <param name="scope">The scope that contains the link; its origin goes on the note.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="code">The code of the note.</param>
    /// <param name="reason">Why it was not drawn.</param>
    /// <returns>Always <see langword="null"/>: nothing is drawn.</returns>
    /// <remarks>
    ///     Unlike an unresolved link this is <see cref="ImageDiagnosticSeverity.Unsupported"/>: the file was found and
    ///     could be drawn, but not as the link asks, so a strict caller can fail on it.
    /// </remarks>
    private Surface? Refuse(Scope scope, string href, string code, string reason)
    {
        _diagnostics.Add(
            new ImageSourceDiagnostic("svg", ImageDiagnosticSeverity.Unsupported, code, $"image {ImageFragment.QuoteLink(href)} was not drawn: {reason}")
            {
                Origin = scope.Origin
            });
        return null;
    }

    /// <summary>
    ///     Warns about the fragment parameters of a link that its source does not use, and about a parameter that is given
    ///     more than once with different values.
    /// </summary>
    /// <param name="scope">The scope that contains the link; its origin goes on the notes.</param>
    /// <param name="source">The source that will draw the link; its <see cref="IFragmentImageSource"/> declares the names it uses.</param>
    /// <param name="request">The request, whose <see cref="ImageSourceRequest.Fragment"/> is checked.</param>
    /// <remarks>
    ///     A file link is always checked (a source without <see cref="IFragmentImageSource"/> uses no parameter at all).
    ///     A scheme-claimed link is checked only when its source declares parameters, because a scheme source owns the
    ///     meaning of its links. Each name is reported once, in the order of first appearance; a name that is unknown is
    ///     not also reported as a duplicate. Aliases of one source (<c>page</c> and <c>slide</c>) are different names and
    ///     are never reported as duplicates of each other.
    /// </remarks>
    private void ReportFragmentParameters(Scope scope, IImageSource source, ImageSourceRequest request)
    {
        var parameters = request.Fragment.Parameters;
        var declared = source as IFragmentImageSource;
        if (parameters.Count == 0 || (declared is null && !request.HasContent))
        {
            return;
        }

        foreach (var note in request.Fragment.ReviewParameters("image", request.Href, source))
        {
            _diagnostics.Add(
                new ImageSourceDiagnostic("svg", ImageDiagnosticSeverity.Warning, note.Code, note.Message)
                {
                    Origin = scope.Origin
                });
        }
    }

    /// <summary>
    ///     Reports a link whose picture does not fit in the pixel budget.
    /// </summary>
    /// <param name="scope">The scope that contains the link; its origin goes on the note.</param>
    /// <param name="href">The link as written.</param>
    /// <param name="pixels">The number of pixels the picture needs.</param>
    private void TooLarge(Scope scope, string href, long pixels)
    {
        _diagnostics.Add(
            new ImageSourceDiagnostic(
                "svg",
                ImageDiagnosticSeverity.Warning,
                TooLargeCode,
                $"image '{href}' was not drawn: it needs {pixels} pixels but only {RemainingPixels} of the budget of {_options.MaxDecodedPixels} pixels remain")
            {
                Origin = scope.Origin
            });
    }

    /// <summary>
    ///     Describes why a file of an unclaimed extension is not drawn, naming the extensions that are.
    /// </summary>
    /// <returns>The reason.</returns>
    private string DescribeUnknownFormat()
    {
        return "it is not a format that can be drawn (an image can link " + string.Join(' ', _sources.Extensions) + ")";
    }

    /// <summary>
    ///     Creates the exception for a file nobody can draw.
    /// </summary>
    /// <param name="name">The path or file name.</param>
    /// <returns>The exception to throw.</returns>
    private NotSupportedException NoSource(string name)
    {
        return new NotSupportedException(
            $"No image source draws '{name}'. The file name extensions that are handled: {string.Join(' ', _sources.Extensions)}.");
    }

    /// <summary>
    ///     Gets the path of a file relative to the folder, with forward slashes.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The relative path.</returns>
    private string RelativeName(SandboxedFile file)
    {
        return Path.GetRelativePath(_rootDirectory!, file.Key).Replace('\\', '/');
    }

    /// <summary>
    ///     Reads a stream into memory, up to the largest file the sandbox would read.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is longer than the limit.</exception>
    private static byte[] ReadBounded(Stream stream)
    {
        const long limit = SandboxOptions.DefaultMaxFileBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new InvalidDataException($"The file is larger than the maximum of {limit / (1024 * 1024)} MiB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     The place a link is resolved from: the top-level document, or one embedded file or scheme-claimed picture.
    ///     It is the <see cref="IImageResolver"/> a source receives in <see cref="ImageSourceRequest.Resolver"/>.
    /// </summary>
    /// <remarks>
    ///     A scope carries only what differs between levels (the file whose folder a relative link is resolved from and the
    ///     chain of files for the cycle and depth checks); the budget and the lists are shared through the owner, so
    ///     recursion is bounded without any state in the sources.
    /// </remarks>
    private sealed class Scope : IImageNaturalSizeResolver
    {
        /// <summary>
        ///     The resolver that owns the shared state.
        /// </summary>
        private readonly FolderImageResolver _owner;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Scope"/> class.
        /// </summary>
        /// <param name="owner">The resolver that owns the shared state.</param>
        /// <param name="current">The file whose folder relative links resolve against, or <see langword="null"/> for the root folder.</param>
        /// <param name="chain">The identities of the files from the top level down to this one.</param>
        /// <param name="depth">How many levels of embedded files are above links of this scope.</param>
        /// <param name="name">The name of the embedding file or link, for failures; <see langword="null"/> at the top level.</param>
        /// <param name="origin">The names of the files that embed links of this scope, outermost first; empty at the top level.</param>
        public Scope(FolderImageResolver owner, SandboxedFile? current, IReadOnlyList<string> chain, int depth, string? name, IReadOnlyList<string> origin)
        {
            _owner = owner;
            Current = current;
            Chain = chain;
            Depth = depth;
            Name = name;
            Origin = origin;
        }

        /// <summary>
        ///     Gets the file whose folder relative links resolve against, or <see langword="null"/> for the root folder.
        /// </summary>
        public SandboxedFile? Current { get; }

        /// <summary>
        ///     Gets the identities of the files from the top level down to this one, for the cycle check.
        /// </summary>
        public IReadOnlyList<string> Chain { get; }

        /// <summary>
        ///     Gets how many levels of embedded files lie above a link of this scope: <c>0</c> for the top-level document
        ///     (even when a direct render put its file on the chain), <c>1</c> for a file it embeds, and so on.
        /// </summary>
        public int Depth { get; }

        /// <summary>
        ///     Gets the name of the embedding file or link, for failures.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        ///     Gets the names of the embedded files, outermost first, that contain the links of this scope: empty at the
        ///     top level, otherwise the origin of the scope that embedded this file followed by this file's own name.
        ///     It is the <see cref="ImageSourceDiagnostic.Origin"/> of every note about a link of this scope.
        /// </summary>
        public IReadOnlyList<string> Origin { get; }

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight)
        {
            return _owner.ResolveIn(this, href, pixelWidth, pixelHeight);
        }

        /// <inheritdoc />
        public Surface? Resolve(string href, int pixelWidth, int pixelHeight, out bool isNaturalSize)
        {
            return _owner.ResolveIn(this, href, pixelWidth, pixelHeight, out isNaturalSize);
        }
    }
}
