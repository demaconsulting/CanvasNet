namespace DemaConsulting.CanvasNet.Svg;

/// <summary>
///     A report that a rendered SVG document uses a feature the codec cannot draw: content left out, or content drawn
///     without a feature it uses.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SvgLoadOptions.UnsupportedFeatureHandler"/> receives these reports after the document is painted.
///         A report never changes the pixels and never fails the render; failing is the host's choice. Each code is
///         reported at most once per rendered document, with the number of elements in <see cref="Count"/>, and the
///         features and the first element named in <see cref="Message"/>.
///     </para>
///     <para>The record is immutable and thread-safe.</para>
/// </remarks>
public sealed record SvgUnsupportedFeature
{
    /// <summary>
    ///     The code of text, or a part of a text, that is not drawn at all: <c>textPath</c> content, an unknown element
    ///     inside <c>text</c>, text inside <c>foreignObject</c>, a nested <c>svg</c> or an unknown element, a negative
    ///     <c>font-size</c>, a non-finite transform, or coordinates beyond the supported range.
    /// </summary>
    public const string TextNotDrawnCode = "svg-text-not-drawn";

    /// <summary>
    ///     The code of text that is drawn without a feature it uses, such as <c>rotate</c>, <c>textLength</c>,
    ///     <c>letter-spacing</c>, a vertical <c>writing-mode</c>, or a character that no font has a glyph for (drawn as
    ///     the missing-glyph box).
    /// </summary>
    public const string TextUnsupportedCode = "svg-text-unsupported";

    /// <summary>
    ///     Gets the code of the report, <see cref="TextNotDrawnCode"/> or <see cref="TextUnsupportedCode"/>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    ///     Gets a one-line description that names the features or reasons and the first element in document order (its
    ///     name, its <c>id</c> when it has one, and the start of its text).
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Gets the number of distinct elements the report covers; at least 1.
    /// </summary>
    public required int Count { get; init; }
}
