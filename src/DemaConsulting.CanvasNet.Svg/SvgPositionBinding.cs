namespace DemaConsulting.CanvasNet.Svg;

/// <summary>
///     The outcome of one SVG position binding: an attribute in the CanvasNet namespace that places a coordinate pair of an
///     element on a feature of an embedded picture.
/// </summary>
/// <remarks>
///     <para>
///         A binding such as <c>cn:x2y2="#flow node:send"</c> (with <c>xmlns:cn="https://demaconsulting.com/canvasnet"</c>)
///         asks the codec to replace the <c>x2</c>/<c>y2</c> of a <c>line</c> with the point where the feature
///         <c>node:send</c> of the picture drawn by <c>&lt;image id="flow"&gt;</c> lands, in the line's own user space.
///         The attribute is matched by its expanded name (the namespace URI and the local name), never by its prefix, and
///         the namespace URI is never fetched. <see cref="SvgLoadOptions.BindingHandler"/> receives one instance per
///         binding attribute, in document order, before anything is painted.
///     </para>
///     <para>
///         A binding that cannot be resolved leaves the numbers written in the document in place (the fallback) and
///         carries one of the codes of this type in <see cref="Code"/>. The record is immutable and thread-safe.
///     </para>
/// </remarks>
public sealed record SvgPositionBinding
{
    /// <summary>
    ///     The namespace URI of the binding attributes: <c>https://demaconsulting.com/canvasnet</c>.
    /// </summary>
    public const string Namespace = "https://demaconsulting.com/canvasnet";

    /// <summary>
    ///     The code of a value that is not <c>#imageId selector</c> or whose selector is not valid.
    /// </summary>
    public const string MalformedCode = "svg-binding-malformed";

    /// <summary>
    ///     The code of an image id that names no element of the document, or an element that is not an <c>image</c>.
    /// </summary>
    public const string ImageMissingCode = "svg-binding-image-missing";

    /// <summary>
    ///     The code of a binding this version does not resolve: an attribute on the wrong element, a <c>text</c> with a
    ///     list of coordinates, an element or image that is not painted directly (inside <c>defs</c>, a pattern, a marker,
    ///     a mask, a clip path, a nested <c>svg</c>, or referenced by <c>use</c>), an image whose link is not external, a
    ///     host resolver that cannot report positions, or a source that reports none (a raster, a document page).
    /// </summary>
    public const string UnsupportedCode = "svg-binding-unsupported";

    /// <summary>
    ///     The code of a selector that matched no position of the picture.
    /// </summary>
    public const string UnmatchedCode = "svg-binding-unmatched";

    /// <summary>
    ///     The code of a selector that matched more than one position (exactly one is required).
    /// </summary>
    public const string AmbiguousCode = "svg-binding-ambiguous";

    /// <summary>
    ///     The code of a binding whose picture was not drawn (missing, denied, over a limit, or of zero size) or whose
    ///     matched position has no pixel.
    /// </summary>
    public const string UnavailableCode = "svg-binding-unavailable";

    /// <summary>
    ///     The code of a binding whose element or image has a transform that cannot be inverted (for example
    ///     <c>scale(0)</c>).
    /// </summary>
    public const string SingularCode = "svg-binding-singular";

    /// <summary>
    ///     Gets the local name of the element that carries the binding, for example <c>line</c>.
    /// </summary>
    public required string Element { get; init; }

    /// <summary>
    ///     Gets the <c>id</c> of the element that carries the binding, or <see langword="null"/> when it has none.
    /// </summary>
    public string? ElementId { get; init; }

    /// <summary>
    ///     Gets the 0-based index of the element in document order, the root element being 0; the order an XML reader
    ///     meets the start tags in.
    /// </summary>
    public required int ElementIndex { get; init; }

    /// <summary>
    ///     Gets the local name of the binding attribute: <c>x1y1</c>, <c>x2y2</c>, <c>center</c>, <c>xy</c>, or another
    ///     name in the namespace, which is unsupported.
    /// </summary>
    public required string Attribute { get; init; }

    /// <summary>
    ///     Gets the value of the binding attribute as written.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    ///     Gets the image id of the value (without the <c>#</c>), or <see langword="null"/> when the value has none.
    /// </summary>
    public string? ImageId { get; init; }

    /// <summary>
    ///     Gets the selector text of the value, or <see langword="null"/> when the value has none.
    /// </summary>
    public string? Selector { get; init; }

    /// <summary>
    ///     Gets the name of the attribute that receives the horizontal coordinate (<c>x1</c>, <c>x2</c>, <c>cx</c> or
    ///     <c>x</c>), or <see langword="null"/> for a binding on the wrong element.
    /// </summary>
    public string? XAttribute { get; init; }

    /// <summary>
    ///     Gets the name of the attribute that receives the vertical coordinate (<c>y1</c>, <c>y2</c>, <c>cy</c> or
    ///     <c>y</c>), or <see langword="null"/> for a binding on the wrong element.
    /// </summary>
    public string? YAttribute { get; init; }

    /// <summary>
    ///     Gets the resolved horizontal coordinate in the user space of the element, rounded to 3 decimals, or
    ///     <see langword="null"/> when the binding was not resolved.
    /// </summary>
    public double? X { get; init; }

    /// <summary>
    ///     Gets the resolved vertical coordinate in the user space of the element, rounded to 3 decimals, or
    ///     <see langword="null"/> when the binding was not resolved.
    /// </summary>
    public double? Y { get; init; }

    /// <summary>
    ///     Gets the code of the failure (one of the constants of this type), or <see langword="null"/> when the binding
    ///     was resolved.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    ///     Gets a message that names the element, the attribute and the outcome.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the binding was resolved and its coordinates were written.
    /// </summary>
    public bool IsResolved => Code is null;
}
