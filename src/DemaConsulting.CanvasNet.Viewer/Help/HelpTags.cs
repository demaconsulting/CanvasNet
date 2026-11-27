namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The closed vocabulary of help-card tags: what a card is about (a format) and what kind of knowledge it holds (a
///     scope).
/// </summary>
/// <remarks>
///     <para>
///         Every card has at least one scope tag and may have format tags; a card without a format tag is general (it
///         applies to every format). <see cref="CanvasNetHelp.Configure"/> gives this vocabulary to the catalog builder,
///         which rejects a tag that is not in it and a card without a scope tag, so a host can rely on these constants
///         when it chooses what to give an agent with <c>CanvasNetHelp.Catalog.Filter</c>, for example
///         only <see cref="Authoring"/> and <see cref="Design"/> knowledge, without the <see cref="Hosting"/> and
///         <see cref="FileAccess"/> cards about what the host application controls.
///     </para>
///     <para>
///         Each format tag is also the identifier of the root card of that format. The lists are immutable and the
///         class is safe to use from several threads.
///     </para>
/// </remarks>
public static class HelpTags
{
    /// <summary>
    ///     The format tag of SVG drawings.
    /// </summary>
    public const string Svg = "svg";

    /// <summary>
    ///     The format tag of Mermaid diagrams.
    /// </summary>
    public const string Mermaid = "mermaid";

    /// <summary>
    ///     The format tag of PlantUML diagrams.
    /// </summary>
    public const string PlantUml = "plantuml";

    /// <summary>
    ///     The format tag of MathML equations.
    /// </summary>
    public const string MathMl = "mathml";

    /// <summary>
    ///     The format tag of OpenSCAD models.
    /// </summary>
    public const string Scad = "scad";

    /// <summary>
    ///     The format tag of Vega-Lite charts.
    /// </summary>
    public const string VegaLite = "vega-lite";

    /// <summary>
    ///     The format tag of PDF documents.
    /// </summary>
    public const string Pdf = "pdf";

    /// <summary>
    ///     The format tag of PowerPoint presentations.
    /// </summary>
    public const string Pptx = "pptx";

    /// <summary>
    ///     The format tag of Visio drawings.
    /// </summary>
    public const string Vsdx = "vsdx";

    /// <summary>
    ///     The format tag of raster images (PNG, JPEG, and the other codecs).
    /// </summary>
    public const string Raster = "raster";

    /// <summary>
    ///     The scope tag of what to write in a file: the syntax, the supported features, and examples.
    /// </summary>
    public const string Authoring = "authoring";

    /// <summary>
    ///     The scope tag of making a picture look good: style, palettes, arrows, and framed figures.
    /// </summary>
    public const string Design = "design";

    /// <summary>
    ///     The scope tag of files in files: embedding, fragments, bindings, and positions.
    /// </summary>
    public const string Composition = "composition";

    /// <summary>
    ///     The scope tag of what is reported: error codes, diagnostics, known differences, and unsupported features.
    /// </summary>
    public const string Diagnostics = "diagnostics";

    /// <summary>
    ///     The scope tag of the resource limits.
    /// </summary>
    public const string Limits = "limits";

    /// <summary>
    ///     The scope tag of what the host application controls: render options, sizes, and strict mode.
    /// </summary>
    public const string Hosting = "hosting";

    /// <summary>
    ///     The scope tag of the file-access policies: which files a drawing may read, the roots, and links.
    /// </summary>
    public const string FileAccess = "file-access";

    /// <summary>
    ///     Gets the format tags, in a fixed order: <see cref="Svg"/>, <see cref="Mermaid"/>, <see cref="PlantUml"/>,
    ///     <see cref="MathMl"/>, <see cref="Scad"/>, <see cref="VegaLite"/>, <see cref="Pdf"/>, <see cref="Pptx"/>, <see cref="Vsdx"/>, and
    ///     <see cref="Raster"/>.
    /// </summary>
    public static IReadOnlyList<string> FormatTags { get; } =
        Array.AsReadOnly([Svg, Mermaid, PlantUml, MathMl, Scad, VegaLite, Pdf, Pptx, Vsdx, Raster]);

    /// <summary>
    ///     Gets the scope tags, in a fixed order: <see cref="Authoring"/>, <see cref="Design"/>,
    ///     <see cref="Composition"/>, <see cref="Diagnostics"/>, <see cref="Limits"/>, <see cref="Hosting"/>, and
    ///     <see cref="FileAccess"/>. Every card has at least one of them.
    /// </summary>
    public static IReadOnlyList<string> ScopeTags { get; } =
        Array.AsReadOnly([Authoring, Design, Composition, Diagnostics, Limits, Hosting, FileAccess]);

    /// <summary>
    ///     Gets every tag: the <see cref="FormatTags"/> followed by the <see cref="ScopeTags"/>.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(FormatTags.Concat(ScopeTags).ToArray());
}
