// cspell:ignore pptx vsdx scad
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     The options of a direct render by <see cref="CanvasNetViewer"/> that the built-in sources understand, and the
///     facts the source reports back.
/// </summary>
/// <remarks>
///     A direct render has no link, so the page and the other options of the caller reach the source through
///     <see cref="ImageSourceRequest.Options"/> (see <see cref="IImageSourceOptions"/>); the facts the render learned
///     (the page that was drawn, how many pages the file has, the anchors of a model) come back in the same object,
///     because <see cref="ImageSourceResult"/> has no place for them. One instance serves one render.
/// </remarks>
internal sealed class DirectRenderOptions : IImageSourceOptions
{
    /// <summary>
    ///     Gets the 1-based page to draw.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    ///     Gets the resolution of a document page in dots per inch, or <see langword="null"/> for 96.
    /// </summary>
    public double? Dpi { get; init; }

    /// <summary>
    ///     Gets the color transparent areas are flattened over, or <see langword="null"/> for the default of the source:
    ///     transparent for a vector source (the chart keeps its own background) and no flattening for a raster.
    /// </summary>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets how a model is evaluated and viewed, or <see langword="null"/> for the library's choice.
    /// </summary>
    public ScadViewOptions? Scad { get; init; }

    /// <summary>
    ///     Gets the values of the parameters of a chart, or <see langword="null"/> for the defaults of its specification.
    /// </summary>
    public ChartOptions? VegaLite { get; init; }

    /// <summary>
    ///     Gets or sets the page that was drawn (set by the source).
    /// </summary>
    public int DrawnPage { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the number of pages the file has (set by the source).
    /// </summary>
    public int PageCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the anchors a model declared (set by the source).
    /// </summary>
    public IReadOnlyList<SourceAnchor> Anchors { get; set; } = [];
}

/// <summary>
///     The built-in <see cref="IImageSource"/> of <see cref="CanvasNetViewer"/>: it draws the bytes of a link with
///     <see cref="SourceLoader"/>, the same code the <c>canvasnet</c> tool uses for a file on the command line, so a
///     file embedded in an SVG document and a file rendered directly are drawn by one implementation.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why an adapter.</b> The format packages cannot call <see cref="SourceLoader"/> (the viewer depends on
///         them), so their own <see cref="IImageSource"/> classes (<c>PdfImageSource</c>, ...) stay stand-alone
///         providers. This class claims exactly what the matching package source claims (name, extensions, schemes), so
///         the extension table, the precedence and the extension drift tests are unchanged.
///     </para>
///     <para>
///         <b>An embedded file</b> is drawn with the defaults of an embed: a transparent background (a chart keeps its
///         own), a page fitted inside the placement and returned at the fitted size, a raster at its natural size and
///         upright, <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> as <see cref="SourceOptions.Strict"/>,
///         and the page, slide or view the fragment of the link names (<c>#page=2</c>, <c>#slide=2</c>,
///         <c>#view=front</c>), the variables it sets for a model (<c>#def.width=20</c>, like <c>-D width=20</c>) and the
///         parameters it sets for a chart (<c>#param.threshold=5</c>, like <c>--param threshold=5</c>; a name the chart
///         does not declare is the warning <c>vl-param-unknown</c>, not an error) and the theme it names for a Mermaid
///         diagram (<c>#theme=dark</c>, like <c>--theme dark</c>). It never inherits the options of its
///         embedding file. <b>A direct render</b> gets its options
///         from <see cref="DirectRenderOptions"/>.
///     </para>
///     <para>
///         <b>Failures.</b> A page that does not exist or a fragment value that cannot be used is an
///         <see cref="ImageSourceRefusedException"/>. A failure of the file that the loader mapped to a
///         <see cref="ViewerException"/> is thrown as the original library exception (so the documented
///         <see cref="Exception.InnerException"/> of an <see cref="ImageSourceException"/> types hold); a <see cref="ViewerException"/> that has no
///         original (a size limit, a model whose include is missing) is thrown as it is. The class is stateless and
///         thread-safe.
///     </para>
/// </remarks>
internal sealed class LoaderImageSource : IImageSource, IFragmentImageSource
{
    /// <summary>
    ///     The transparent color embedded pictures are drawn over, so the embedding document shows through.
    /// </summary>
    private static readonly Rgba32 Transparent = new(0, 0, 0, 0);

    /// <summary>
    ///     The number of bytes at the start of a file that decide whether it is a raster image.
    /// </summary>
    private const int HeaderLength = 4096;

    /// <summary>
    ///     The source whose name, extensions and schemes this source claims.
    /// </summary>
    private readonly IImageSource _claims;

    /// <summary>
    ///     The kinds of file this source draws.
    /// </summary>
    private readonly SourceKind[] _kinds;

    /// <summary>
    ///     The fragment parameters that choose the page, for a document; empty for every other source.
    /// </summary>
    private readonly string[] _pageParameters;

    /// <summary>
    ///     Whether the fragment parameter <c>view</c> chooses the viewpoint of a model.
    /// </summary>
    private readonly bool _acceptsView;

    /// <summary>
    ///     The prefixes of the fragment parameters that are named by the author of the link: <c>def.</c> for a model,
    ///     <c>param.</c> for a chart, none for every other source.
    /// </summary>
    private readonly string[] _prefixes;

    /// <summary>
    ///     Whether the fragment parameter <c>theme</c> chooses the theme of a Mermaid diagram.
    /// </summary>
    private readonly bool _acceptsTheme;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LoaderImageSource"/> class.
    /// </summary>
    /// <param name="claims">The package source whose name, extensions and schemes this source claims.</param>
    /// <param name="kinds">The kinds of file the source draws; one, or the raster kinds.</param>
    /// <param name="pageParameters">The fragment parameters that choose the page; empty when the source has no pages.</param>
    /// <param name="acceptsView">Whether the fragment parameter <c>view</c> chooses a viewpoint.</param>
    /// <remarks>
    ///     The parameters that only one kind takes are added by the kind: <c>def.</c> for a model, <c>param.</c> for a chart,
    ///     and <c>theme</c> for a Mermaid diagram.
    /// </remarks>
    public LoaderImageSource(IImageSource claims, SourceKind[] kinds, string[] pageParameters, bool acceptsView)
    {
        _claims = claims;
        _kinds = kinds;
        _pageParameters = pageParameters;
        _acceptsView = acceptsView;
        _acceptsTheme = kinds is [SourceKind.Mermaid];
        FragmentParameters = new ReadOnlyCollection<string>(
            [.. pageParameters, .. acceptsView ? new[] { "view" } : [], .. _acceptsTheme ? new[] { MermaidImageSource.ThemeParameter } : []]);
        if (kinds is [SourceKind.Scad])
        {
            _prefixes = [ScadImageSource.DefinePrefix];
        }
        else
        {
            _prefixes = kinds is [SourceKind.VegaLite] ? [VegaLiteImageSource.ParameterPrefix] : [];
        }
        FragmentParameterPrefixes = new ReadOnlyCollection<string>(_prefixes);
    }

    /// <inheritdoc />
    public string Name => _claims.Name;

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => _claims.Extensions;

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => _claims.Schemes;

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameters { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> FragmentParameterPrefixes { get; }

    /// <summary>
    ///     Draws the page of the file of a request that the fragment (or the direct options) names.
    /// </summary>
    /// <param name="request">The request; it must carry the bytes of a file (<see cref="ImageSourceRequest.HasContent"/>).</param>
    /// <returns>The picture, its notes and its text, with the format name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request has no file content.</exception>
    /// <exception cref="ImageSourceRefusedException">Thrown when the page does not exist or a fragment value is not usable.</exception>
    /// <exception cref="ImageSourceBudgetException">Thrown, before drawing, when the picture does not fit in the pixel budget.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the request is canceled.</exception>
    /// <exception cref="ViewerException">Thrown for a failure that has no library exception behind it (a size limit, a missing include).</exception>
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();
        if (!request.HasContent)
        {
            throw new InvalidOperationException(
                $"The request for '{request.Href}' has no file content; a {Name} file is read from a file, not by URI scheme.");
        }

        try
        {
            return Draw(request);
        }
        catch (ViewerException ex) when (ex.InnerException is not null)
        {
            // The documented failure types are the library's own, so the original exception is what the resolver wraps
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    ///     Draws the file of a request with the loader.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The picture and what the loader reported.</returns>
    private ImageSourceResult Draw(ImageSourceRequest request)
    {
        // The file is read from memory, in the folder the resolver found it in, so its includes resolve beside it under the policy of the request
        var input = SourceLoader.SourceInput.FromMemory(request.Content, request.FileName!, request.Directory);
        var kind = FindKind(request);
        var direct = request.Options as DirectRenderOptions;
        var options = CreateOptions(request, kind, direct);

        // A picture that cannot fit is refused from its header, before the header read below can allocate it (a GIF is
        // decoded to be described); the loader charges it for real later
        if (!kind.IsVector())
        {
            var (declaredWidth, declaredHeight) = SourceLoader.Read(input.Name, kind, () => SourceLoader.ReadDeclaredRasterSize(input, kind));
            var declared = (long)declaredWidth * declaredHeight;
            if (declared > request.RemainingPixels)
            {
                throw new ImageSourceBudgetException(declared, request.RemainingPixels);
            }
        }

        using var opened = SourceLoader.OpenInput(input, kind, request.FileAccess);
        var info = opened.Info;
        if (options.Page < 1 || options.Page > info.PageCount)
        {
            throw new ImageSourceRefusedException(FolderImageResolver.PageOutOfRangeCode, DescribeMissingPage(kind, options.Page, info.PageCount));
        }

        var context = new SourceLoader.LoadContext
        {
            CancellationToken = request.CancellationToken,
            Charge = request.EnsureWithinBudget,
            UseScopedResolver = true,
            ScopedResolver = request.Resolver,
            ReturnFittedPage = true
        };
        using var pages = SourceLoader.OpenInputPages(input, info, options, context, opened);
        var loaded = pages.ReadPage(options.Page);

        // The loaded source only owns the surface, which now belongs to the result
        if (direct is not null)
        {
            direct.DrawnPage = options.Page;
            direct.PageCount = info.PageCount;
            direct.Anchors = loaded.Anchors;
        }

        return new ImageSourceResult(loaded.Surface)
        {
            Format = kind.ToFormatName(),
            Diagnostics = [.. loaded.Diagnostics.Select(ToDiagnostic)],
            Echo = loaded.Echo
        };
    }

    /// <summary>
    ///     Tells whether this source draws a kind of file.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the kind is one of the kinds this source draws.</returns>
    internal bool Draws(SourceKind kind)
    {
        return _kinds.Contains(kind);
    }

    /// <summary>
    ///     Reads the page a fragment names.
    /// </summary>
    /// <param name="fragment">The fragment of the link or of the input.</param>
    /// <returns>The 1-based page, or <see langword="null"/> when the source has no pages or the fragment names none.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown with the invalid-fragment code when the value is not a whole number of 1 or more.</exception>
    internal int? ReadPage(ImageFragment fragment)
    {
        return fragment.GetPositiveInt32(_pageParameters);
    }

    /// <summary>
    ///     Finds the kind of the file of a request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The only kind of this source, or for a raster the kind its signature bytes say (whatever the name says).</returns>
    /// <exception cref="InvalidDataException">Thrown when a raster source is given bytes that are not a raster image.</exception>
    private SourceKind FindKind(ImageSourceRequest request)
    {
        if (_kinds.Length == 1)
        {
            return _kinds[0];
        }

        var header = request.Content.Span[..Math.Min(request.Content.Length, HeaderLength)];
        return SourceLoader.DetectRasterSignature(header) ?? throw new InvalidDataException("The data is not a recognized image format.");
    }

    /// <summary>
    ///     Creates the loading options of a request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="kind">The kind of the file.</param>
    /// <param name="direct">The options of a direct render, or <see langword="null"/> for a link in a document.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown when a fragment value cannot be used.</exception>
    private SourceOptions CreateOptions(ImageSourceRequest request, SourceKind kind, DirectRenderOptions? direct)
    {
        // A vector source is drawn at the placement; a raster comes back at its own size, and the caller fits it
        var vector = kind.IsVector();
        var page = direct?.Page ?? ReadPage(request.Fragment) ?? 1;
        var scad = direct?.Scad ?? ReadScad(request.Fragment);
        var chart = direct?.VegaLite ?? ReadChart(request.Fragment);
        return new SourceOptions
        {
            Page = page,
            Dpi = direct?.Dpi,
            Strict = request.ThrowOnUnsupportedFeature,
            FileAccess = request.FileAccess,
            Width = vector && request.PixelWidth is { } w ? Math.Clamp(w, 1, Surface.MaxDimension) : null,
            Height = vector && request.PixelHeight is { } h ? Math.Clamp(h, 1, Surface.MaxDimension) : null,
            Background = direct?.Background ?? DefaultBackground(kind),
            Scad = scad,
            VegaLite = chart,
            Mermaid = ReadDiagram(request.Fragment)
        };
    }

    /// <summary>
    ///     Gets the color a source is drawn over when nobody chose one.
    /// </summary>
    /// <param name="kind">The kind of the file.</param>
    /// <returns>Transparent for a vector source, so the embedding document shows through; <see langword="null"/> for a chart (it keeps its own background) and for a raster (it is not flattened).</returns>
    private static Rgba32? DefaultBackground(SourceKind kind)
    {
        return kind.IsVector() && kind != SourceKind.VegaLite ? Transparent : null;
    }

    /// <summary>
    ///     Reads how a model is viewed and evaluated from a fragment: the viewpoint of <c>view=</c> and the variables of
    ///     every <c>def.&lt;name&gt;=&lt;value&gt;</c>.
    /// </summary>
    /// <param name="fragment">The fragment of the link.</param>
    /// <returns>The options, or <see langword="null"/> when the source takes none or the fragment sets none.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown with the invalid-fragment code when a value cannot be used.</exception>
    internal ScadViewOptions? ReadScad(ImageFragment fragment)
    {
        var view = ReadView(fragment);
        if (!_prefixes.Contains(ScadImageSource.DefinePrefix))
        {
            return view;
        }

        var defines = ImageParameterValues.ReadVariables(fragment, ScadImageSource.DefinePrefix);
        if (defines.Count == 0)
        {
            return view;
        }

        try
        {
            // The library says which variable names it accepts, so it is asked rather than guessed
            _ = new ScadEvaluationOptions { Variables = defines };
        }
        catch (ArgumentException ex)
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.FragmentInvalidCode,
                $"a '{ScadImageSource.DefinePrefix}' fragment parameter is not valid: {Shorten(ex.Message)}");
        }

        return (view ?? new ScadViewOptions()) with { Defines = defines };
    }

    /// <summary>
    ///     Reads the parameters of a chart from the <c>param.&lt;name&gt;=&lt;value&gt;</c> parameters of a fragment.
    /// </summary>
    /// <param name="fragment">The fragment of the link.</param>
    /// <returns>The options, or <see langword="null"/> when the source is not a chart or the fragment sets none.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown with the invalid-fragment code when a value cannot be used.</exception>
    internal ChartOptions? ReadChart(ImageFragment fragment)
    {
        if (!_prefixes.Contains(VegaLiteImageSource.ParameterPrefix))
        {
            return null;
        }

        var parameters = ImageParameterValues.ReadParameters(fragment, VegaLiteImageSource.ParameterPrefix);
        if (parameters.Count == 0)
        {
            return null;
        }

        try
        {
            // The library says which values it accepts, so it is asked rather than guessed
            _ = new VegaLiteRenderOptions { Parameters = parameters };
        }
        catch (ArgumentException ex)
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.FragmentInvalidCode,
                $"a '{VegaLiteImageSource.ParameterPrefix}' fragment parameter is not valid: {Shorten(ex.Message)}");
        }

        // A link is not a command line: a name the chart does not declare is a warning and the chart is drawn without it
        return new ChartOptions { Parameters = parameters, WarnOnUnknownParameters = true };
    }

    /// <summary>
    ///     Reads the theme of a Mermaid diagram from the <c>theme=</c> parameter of a fragment.
    /// </summary>
    /// <param name="fragment">The fragment of the link or of the input.</param>
    /// <returns>The options, or <see langword="null"/> when the source is not a diagram or the fragment names no theme.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown with the invalid-fragment code when the value is not a Mermaid theme.</exception>
    internal DiagramOptions? ReadDiagram(ImageFragment fragment)
    {
        if (!_acceptsTheme || fragment.GetValue(MermaidImageSource.ThemeParameter) is not { } name)
        {
            return null;
        }

        return SourceLoader.FindMermaidTheme(name) is { } theme
            ? new DiagramOptions { Theme = theme }
            : throw new ImageSourceRefusedException(
                FolderImageResolver.FragmentInvalidCode,
                $"the fragment parameter '{MermaidImageSource.ThemeParameter}' must be one of {string.Join(", ", MermaidRenderOptions.ThemeNames)}, not {Show(name)}");
    }

    /// <summary>
    ///     Puts a message of a library on one line and cuts it at 160 characters.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The shortened message.</returns>
    private static string Shorten(string message)
    {
        var line = string.Join(' ', message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        // The name of the argument that an ArgumentException appends means nothing to the author of a link
        var argument = line.LastIndexOf(" (Parameter '", StringComparison.Ordinal);
        line = argument > 0 ? line[..argument] : line;
        return line.Length > 160 ? line[..160] + "..." : line;
    }

    /// <summary>
    ///     Reads the viewpoint a fragment names.
    /// </summary>
    /// <param name="fragment">The fragment of the link.</param>
    /// <returns>The options that choose the viewpoint, or <see langword="null"/> when the source takes none or the fragment names none.</returns>
    /// <exception cref="ImageSourceRefusedException">Thrown with the invalid-fragment code when the name is not a viewpoint.</exception>
    private ScadViewOptions? ReadView(ImageFragment fragment)
    {
        if (!_acceptsView || fragment.GetValue("view") is not { } name)
        {
            return null;
        }

        if (!SourceLoader.ScadViews.Any(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ImageSourceRefusedException(
                FolderImageResolver.FragmentInvalidCode,
                $"the fragment parameter 'view' must be one of {string.Join(", ", SourceLoader.ScadViews.Select(v => v.Name))}, not {Show(name)}");
        }

        return new ScadViewOptions { View = name, ViewGiven = true };
    }

    /// <summary>
    ///     Shows untrusted text in a message: quoted, without control characters, and cut at 40 characters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The quoted text.</returns>
    private static string Show(string text)
    {
        var shown = new string([.. text.Take(40).Select(c => char.IsControl(c) ? '?' : c)]);
        return text.Length > 40 ? $"'{shown}...'" : $"'{shown}'";
    }

    /// <summary>
    ///     Describes a page that the file does not have.
    /// </summary>
    /// <param name="kind">The kind of the file.</param>
    /// <param name="page">The page that was asked for.</param>
    /// <param name="count">The number of pages the file has.</param>
    /// <returns>The sentence, in the words of the kind (a presentation has slides).</returns>
    private static string DescribeMissingPage(SourceKind kind, int page, int count)
    {
        var item = kind == SourceKind.Pptx ? "slide" : "page";
        var whole = "file";
        if (kind.IsDocument())
        {
            whole = kind == SourceKind.Pptx ? "presentation" : "document";
        }
        var plural = count == 1 ? item : item + "s";
        return $"{item} {page} does not exist: the {whole} has {count} {plural}";
    }

    /// <summary>
    ///     Converts a note of the loader to the shape of the image sources.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <returns>The note with every member kept.</returns>
    private static ImageSourceDiagnostic ToDiagnostic(ImageDiagnostic note)
    {
        var severity = note.Severity switch
        {
            "info" => ImageDiagnosticSeverity.Info,
            "unsupported" => ImageDiagnosticSeverity.Unsupported,
            _ => ImageDiagnosticSeverity.Warning
        };
        return new ImageSourceDiagnostic(note.Source, severity, note.Code, note.Message)
        {
            Detail = note.Detail,
            Location = note.Location,
            Page = note.Page,
            Origin = note.Origin
        };
    }
}
