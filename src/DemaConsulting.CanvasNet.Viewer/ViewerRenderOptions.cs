using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer;

/// <summary>
///     What to render and how, for <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/>.
/// </summary>
/// <remarks>
///     The record is immutable once built; the extra sources it references must be safe to use from the thread that
///     renders.
/// </remarks>
public sealed record ViewerRenderOptions
{
    /// <summary>
    ///     Gets the width in pixels of the picture, or <see langword="null"/> for the natural width (or, when only the
    ///     height is given, the width that keeps the aspect ratio). A vector source draws at the requested size; a raster
    ///     is returned by its source at its natural size, so the viewer resamples it: with both <see cref="Width"/> and
    ///     <see cref="Height"/> it fits the picture inside that box (keeping its aspect ratio, centered on a transparent
    ///     background) so the result is exactly that size, with one dimension it scales to keep the aspect ratio. When
    ///     given, it must be from 1 to <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/> (8192);
    ///     <see cref="CanvasNetViewer"/> throws <see cref="ArgumentOutOfRangeException"/> for any other value before it
    ///     reads the file. For a raster this differs from <c>SourceOptions.Width</c> and <c>Height</c> of the Imaging
    ///     operations, which stretch it to exactly the size.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    ///     Gets the height in pixels of the picture, or <see langword="null"/> for the natural height. See
    ///     <see cref="Width"/>; the same range, 1 to <see cref="DemaConsulting.CanvasNet.Canvas.Surface.MaxDimension"/>,
    ///     applies.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    ///     Gets the 1-based page, slide or sheet of a document to draw: <c>1</c>, the first, by default. A PDF, a PPTX
    ///     presentation (a slide) and a VSDX drawing have pages; every other file has only page 1.
    /// </summary>
    /// <remarks>
    ///     A page the file does not have makes <see cref="CanvasNetViewer"/> throw a <see cref="ViewerException"/> with the
    ///     code <c>page-out-of-range</c>; a value below 1 throws <see cref="ArgumentOutOfRangeException"/> before the file
    ///     is read. A document embedded in the rendered file chooses its own page with the fragment of its link
    ///     (<c>report.pdf#page=3</c>); it never inherits this value. <see cref="ViewerImage.PageCount"/> tells how many
    ///     pages the file has.
    /// </remarks>
    public int Page { get; init; } = 1;

    /// <summary>
    ///     Gets the resolution, in dots per inch, a page of a document is sized at, or <see langword="null"/> for 96. It
    ///     sets the natural size of a PDF, PPTX or VSDX page (the size <see cref="Width"/> and <see cref="Height"/> act
    ///     on); every other kind ignores it.
    /// </summary>
    /// <remarks>
    ///     It must be finite, greater than 0 and at most <see cref="Imaging.SourceLoader.MaxDpi"/> (2400), otherwise
    ///     <see cref="CanvasNetViewer"/> throws <see cref="ArgumentOutOfRangeException"/> before the file is read; a
    ///     Visio drawing takes a whole number only (a fractional value is a <see cref="ViewerException"/> with the code
    ///     <c>usage-error</c>).
    /// </remarks>
    public double? Dpi { get; init; }

    /// <summary>
    ///     Gets the color that transparent areas are flattened over, or <see langword="null"/> for the default: no
    ///     flattening, so a document page, a model and an SVG file come back over a <b>transparent</b> background and a
    ///     Vega-Lite chart keeps its own. Any color other than fully transparent flattens the picture over it (a raster
    ///     with transparency too). When <see cref="Width"/> and <see cref="Height"/> fit the picture into a box, the margin
    ///     around it has this color (transparent when none is given), and the pixel positions in
    ///     <see cref="ViewerImage.Anchors"/> follow the picture into the box.
    /// </summary>
    public DemaConsulting.CanvasNet.Canvas.Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets how an OpenSCAD model is viewed and evaluated (the viewpoint, the angles, the projection, definitions of
    ///     variables, the time and the triangle limit), or <see langword="null"/> for the library's choice. Every other
    ///     kind ignores it.
    /// </summary>
    /// <remarks>
    ///     The options are checked before the file is read: an unknown view, an elevation outside -90 to 90, a time
    ///     outside the limit, a triangle limit below 1 or a definition the library rejects throws
    ///     <see cref="ArgumentOutOfRangeException"/>. The anchors a model declares come back in
    ///     <see cref="ViewerImage.Anchors"/>. A model embedded in the rendered file chooses its viewpoint with the
    ///     fragment of its link (<c>part.scad#view=front</c>) and never inherits this value. There is no <c>Scale</c>,
    ///     <c>Filter</c> or <c>AutoOrient</c> here: the size is <see cref="Width"/> and <see cref="Height"/> (or
    ///     <see cref="Dpi"/> for a document), and the resampling of a raster is the concern of
    ///     <c>ImageWriter</c> and <c>SourceLoader</c> of the <c>Imaging</c> namespace.
    /// </remarks>
    public Imaging.ScadViewOptions? Scad { get; init; }

    /// <summary>
    ///     Gets the values of the parameters (<c>params</c>) of a Vega-Lite chart, or <see langword="null"/> for the defaults of
    ///     its specification. Every other kind ignores it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="Imaging.ChartOptions.Parameters"/> takes what <c>VegaLiteRenderOptions.Parameters</c> takes: a
    ///         number, a string, a boolean, <see langword="null"/>, a list or a dictionary of values, and for a selection
    ///         parameter the shapes of its initial state (<c>{"x": [2, 6]}</c> for an interval selection). It is the option
    ///         behind <c>--param name=value</c> of the tool and behind the <c>param.&lt;name&gt;=&lt;value&gt;</c>
    ///         fragment of an SVG link, so the command line, the library and an embedded chart behave the same way. The
    ///         values are checked before the file is read: one the library rejects (an empty name, a value nested too deeply)
    ///         throws <see cref="ArgumentOutOfRangeException"/>. A name the chart does not declare is a
    ///         <see cref="ViewerException"/> with the code <c>vega-lite-error</c> that lists the declared names (see
    ///         <see cref="Imaging.ChartOptions.WarnOnUnknownParameters"/> for the warning instead). A chart embedded in the
    ///         rendered file chooses its own parameters with the fragment of its link
    ///         (<c>chart.vl.json#param.threshold=5</c>) and never inherits this value; a chart that is parameterized changes
    ///         its natural size when its layout depends on the parameter, and the picture is sized from the parameterized layout.
    ///     </para>
    ///     <example>
    ///         <code language="csharp">
    ///         var options = new ViewerRenderOptions
    ///         {
    ///             VegaLite = new ChartOptions
    ///             {
    ///                 Parameters = new Dictionary&lt;string, object?&gt;
    ///                 {
    ///                     ["threshold"] = 5.0,
    ///                     ["brush"] = new Dictionary&lt;string, object?&gt; { ["x"] = new object[] { 2, 6 } },
    ///                 },
    ///             },
    ///         };
    ///         using var image = CanvasNetViewer.Render("chart.vl.json", options);
    ///         </code>
    ///     </example>
    /// </remarks>
    public Imaging.ChartOptions? VegaLite { get; init; }

    /// <summary>
    ///     Gets the folder that confines the files the rendered file itself includes or embeds (a model's
    ///     <c>include</c>, a chart's data, the pictures of an SVG file), or <see langword="null"/>. For a render by path the
    ///     default is the folder of the file, and the file must be inside the folder; for a render of a stream the default
    ///     is none, so no file the document names is resolved.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a file that uses a feature its source does not draw fails (the source throws)
    ///     instead of being drawn without it with a note. The default is <see langword="false"/>. For an SVG file it also
    ///     covers its links, as <c>--strict</c> of the tool does: a link that is not drawn (a missing file, one outside the
    ///     folder, a picture too large for the budget, an unusable fragment, a page that does not exist), at any level of
    ///     nesting, is a <see cref="ViewerException"/> with the code <c>unsupported-feature</c> that names the file that
    ///     holds the link; an unknown or repeated fragment parameter stays a warning.
    /// </summary>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Gets your own image sources, or <see langword="null"/> for none. They are placed after the built-in sources,
    ///     so <b>one that claims the same extension or URI scheme as a built-in source wins</b>, and a file type that only
    ///     an extra source claims can be rendered and embedded.
    /// </summary>
    /// <remarks>
    ///     These sources are used by <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/> (and its stream
    ///     overload), which builds its own resolver from the built-in sources plus these - you never create a resolver for
    ///     a <c>Render</c> call. To embed files in a render that <b>you</b> drive yourself (for example
    ///     <c>SvgCodec.Load</c> with <c>SvgLoadOptions.ImageResolver</c>), call
    ///     <see cref="CanvasNetViewer.CreateImageResolver(string, IEnumerable{IImageSource}, FolderImageResolverOptions?)"/>
    ///     with the same extra sources instead; the precedence is the same (later wins).
    /// </remarks>
    public IEnumerable<IImageSource>? ExtraSources { get; init; }

    /// <summary>
    ///     Gets the most pixels the render may decode and allocate in total (the file itself and every picture it embeds), or
    ///     <see langword="null"/> for the resolver default (<see cref="FolderImageResolverOptions.MaxDecodedPixels"/>,
    ///     64,000,000). The default refuses a single 8192 x 8192 picture; set this to render one large image. A render over
    ///     the budget throws <see cref="ImageSourceBudgetException"/>; an embedded picture over it is skipped with a
    ///     <c>svg-image-too-large</c> note. A negative value throws <see cref="ArgumentOutOfRangeException"/>. The
    ///     budget contract is described in the remarks of <see cref="ImageSourceRequest"/>.
    ///     When the render is post-processed to the requested <see cref="Width"/> and <see cref="Height"/> (a raster, or
    ///     any source that returns another size), the final picture is a second buffer that must fit in what is left of
    ///     this budget after the surface the source produced (peak memory is both): one that does not fit throws
    ///     <see cref="ImageSourceBudgetException"/> as well.
    /// </summary>
    public long? MaxDecodedPixels { get; init; }

    /// <summary>
    ///     Gets the token that cancels the render. The default is <see cref="CancellationToken.None"/>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
