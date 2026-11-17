namespace DemaConsulting.CanvasNet.Svg;

/// <summary>
///     An authoring hint about a rendered SVG document: something that draws, but is likely not what the author meant.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SvgLoadOptions.HintHandler"/> receives hints after the document is painted. A hint never changes
///         the pixels and never fails the render; acting on it is the host's choice. Each code is reported at most once
///         per rendered document, with the number of occurrences in <see cref="Count"/> and the first occurrence named in
///         <see cref="Message"/>.
///     </para>
///     <para>The record is immutable and thread-safe.</para>
/// </remarks>
public sealed record SvgHint
{
    /// <summary>
    ///     The code of a filled triangle (or notched, four-point arrow shape) drawn by hand at the end of a stroked
    ///     connector that has no marker at that end: an arrowhead that does not follow the connector's direction. A
    ///     <c>marker</c> with <c>orient="auto"</c> on <c>marker-end</c> draws a head that always does.
    /// </summary>
    public const string ArrowheadShapeCode = "svg-arrowhead-shape";

    /// <summary>
    ///     Gets the code of the hint, for example <see cref="ArrowheadShapeCode"/>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    ///     Gets a one-line description of the hint that names the first occurrence (its element, its <c>id</c> when it
    ///     has one, and its pixel position).
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     Gets the number of occurrences in the document; at least 1.
    /// </summary>
    public required int Count { get; init; }
}
