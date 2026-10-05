namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx prst

/// <summary>
///     A resolved DrawingML line style (<c>&lt;a:ln&gt;</c>): its stroke width, resolved paint, and
///     optional dash pattern - everything <see cref="Drawing.PathStroker.Stroke"/> needs to
///     convert a shape's outline geometry into a stroked, fillable outline (see
///     <see cref="PptxDocument.ResolveStrokeOutline"/>).
/// </summary>
/// <param name="WidthEmu">
///     The line's width (<c>&lt;a:ln w="..."/&gt;</c>), in EMU. Always a finite value greater than
///     zero - <see cref="PptxDocument.ResolveLineStyle"/> never produces a
///     <see cref="PptxLineStyle"/> for a zero/negative <em>explicitly-declared</em> width or an
///     explicit <c>&lt;a:noFill/&gt;</c> line (both resolve to <see langword="null"/> instead,
///     meaning "no stroke"); a genuinely <em>absent</em> width instead defaults to
///     <see cref="PptxDocument.ResolveLineStyle"/>'s documented default stroke width - see that
///     method's remarks.
/// </param>
/// <param name="Paint">
///     The line's resolved paint (an <c>&lt;a:ln&gt;</c>'s own <c>&lt;a:solidFill&gt;</c>/
///     <c>&lt;a:gradFill&gt;</c> child, resolved exactly like a shape fill via
///     <see cref="PptxDocument.ResolveFill"/>). Never <see cref="PptxNoFill"/> - that case
///     resolves to a <see langword="null"/> <see cref="PptxLineStyle"/> instead (see
///     <see cref="WidthEmu"/>'s remarks).
/// </param>
/// <param name="DashArray">
///     The line's resolved dash lengths (in EMU, alternating on/off), or <see langword="null"/>
///     for a solid line - resolved from <c>&lt;a:prstDash val="..."/&gt;</c>'s named preset dash
///     pattern (see <see cref="PptxDocument.ResolveLineStyle"/>'s remarks for the supported preset
///     names).
/// </param>
internal sealed record PptxLineStyle(float WidthEmu, PptxPaint Paint, IReadOnlyList<float>? DashArray);
