using System.Linq;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore bodypr lstyle lnspc spcpct spcpts marl

/// <summary>
///     A parsed <c>&lt;p:txBody&gt;</c> element (Phase 1d): resolved body properties plus its
///     ordered paragraphs, produced by <see cref="PptxDocument.ParseTextBody"/>.
/// </summary>
/// <param name="Properties">The text body's resolved <c>&lt;a:bodyPr&gt;</c> properties.</param>
/// <param name="Paragraphs">The text body's ordered <c>&lt;a:p&gt;</c> paragraphs.</param>
internal sealed record PptxTextBody(PptxBodyProperties Properties, IReadOnlyList<PptxParagraph> Paragraphs);

/// <summary>
///     A text body's resolved <c>&lt;a:bodyPr&gt;</c> properties, produced by
///     <see cref="PptxDocument.ParseBodyProperties"/>.
/// </summary>
/// <param name="Anchor">The vertical text anchor (<c>anchor="t"/"ctr"/"b"</c>, defaulting to <see cref="PptxTextAnchor.Top"/>).</param>
/// <param name="Wrap">The word-wrap mode (<c>wrap="square"/"none"</c>, defaulting to <see cref="PptxTextWrap.Square"/>).</param>
/// <param name="InsetLeftEmu">The left inset (<c>lIns</c>), in EMU, defaulting to 91440 (0.1 inch) when absent.</param>
/// <param name="InsetTopEmu">The top inset (<c>tIns</c>), in EMU, defaulting to 45720 (0.05 inch) when absent.</param>
/// <param name="InsetRightEmu">The right inset (<c>rIns</c>), in EMU, defaulting to 91440 (0.1 inch) when absent.</param>
/// <param name="InsetBottomEmu">The bottom inset (<c>bIns</c>), in EMU, defaulting to 45720 (0.05 inch) when absent.</param>
/// <param name="AutofitElement">
///     The body's raw autofit child element (one of <c>&lt;a:noAutofit/&gt;</c>,
///     <c>&lt;a:normAutofit/&gt;</c>, or <c>&lt;a:spAutoFit/&gt;</c>), retained unparsed for the
///     layout engine (<see cref="PptxDocument.ResolveTextLayout"/>) to interpret, or
///     <see langword="null"/> when <c>&lt;a:bodyPr&gt;</c> declares none (equivalent to
///     <c>&lt;a:noAutofit/&gt;</c>).
/// </param>
internal sealed record PptxBodyProperties(
    PptxTextAnchor Anchor,
    PptxTextWrap Wrap,
    float InsetLeftEmu,
    float InsetTopEmu,
    float InsetRightEmu,
    float InsetBottomEmu,
    XElement? AutofitElement);

/// <summary>
///     A parsed <c>&lt;a:p&gt;</c> paragraph: its own raw, unresolved properties plus its ordered
///     runs, produced by <see cref="PptxDocument.ParseParagraph"/>.
/// </summary>
/// <param name="RawProperties">The paragraph's own raw, unresolved properties.</param>
/// <param name="Items">
///     The paragraph's content in document order: each item is either an <c>&lt;a:r&gt;</c> run
///     (<see cref="PptxRunItem"/>) or an explicit <c>&lt;a:br&gt;</c> line break
///     (<see cref="PptxLineBreakItem"/>). An empty paragraph is valid DrawingML - a blank line.
/// </param>
internal sealed record PptxParagraph(PptxRawParagraphProperties RawProperties, IReadOnlyList<PptxParagraphItem> Items)
{
    /// <summary>
    ///     The paragraph's ordered <c>&lt;a:r&gt;</c> runs only, with any interleaved
    ///     <c>&lt;a:br&gt;</c> items excluded - a convenience accessor for callers (such as the
    ///     run-property inheritance resolver) that only need run content/order, not break
    ///     positions. The returned order always matches <see cref="Items"/>'s run-item order.
    /// </summary>
    internal IReadOnlyList<PptxTextRun> Runs { get; } =
        Items.OfType<PptxRunItem>().Select(item => item.Run).ToList();
}

/// <summary>
///     A single item of <see cref="PptxParagraph.Items"/> content, in document order: either a
///     run (<see cref="PptxRunItem"/>) or an explicit line break (<see cref="PptxLineBreakItem"/>).
/// </summary>
internal abstract record PptxParagraphItem
{
    private protected PptxParagraphItem()
    {
    }
}

/// <summary>A paragraph content item wrapping a parsed <c>&lt;a:r&gt;</c> run (or <c>&lt;a:fld&gt;</c> field).</summary>
/// <param name="Run">The wrapped run.</param>
/// <param name="FieldType">
///     The owning <c>&lt;a:fld&gt;</c> element's own <c>type</c> attribute value (e.g.
///     <c>"slidenum"</c>, <c>"datetime1"</c>), or <see langword="null"/> when this item was
///     parsed from a plain <c>&lt;a:r&gt;</c> run, or from an <c>&lt;a:fld&gt;</c> with no/empty
///     <c>type</c> attribute. See <see cref="PptxDocument.ParseParagraph"/>'s remarks for how this
///     is captured, and <see cref="PptxDocument.SubstituteSlideNumberField"/> for the one field
///     type (<c>"slidenum"</c>) whose cached <see cref="PptxTextRun.Text"/> is replaced at render
///     time with the slide's own current 1-based slide number - every other field type (and every
///     plain run) keeps rendering its parsed <see cref="PptxTextRun.Text"/> unchanged.
/// </param>
internal sealed record PptxRunItem(PptxTextRun Run, string? FieldType = null) : PptxParagraphItem;

/// <summary>
///     A paragraph content item representing an explicit <c>&lt;a:br&gt;</c> line break -
///     DrawingML's explicit line-break element, which forces a new layout line at this point in
///     the paragraph regardless of word-wrap, independent of any run text.
/// </summary>
internal sealed record PptxLineBreakItem : PptxParagraphItem
{
    /// <summary>The single, stateless instance used for every parsed <c>&lt;a:br&gt;</c>.</summary>
    internal static readonly PptxLineBreakItem Instance = new();
}

/// <summary>
///     A paragraph's own raw, unresolved properties (its <c>&lt;a:pPr&gt;</c> element's
///     attributes/children) - resolution against the inheritance chain (placeholder/layout/
///     master/theme) happens in <see cref="PptxDocument.ResolveEffectiveParagraphProperties"/>,
///     not here.
/// </summary>
/// <param name="Level">The paragraph's <c>lvl</c> attribute (0-based, clamped to <c>[0,8]</c>), defaulting to <c>0</c> when absent.</param>
/// <param name="Algn">The paragraph's own <c>algn</c> attribute, or <see langword="null"/> when absent.</param>
/// <param name="MarLEmu">The paragraph's own <c>marL</c> (left margin, EMU) attribute, or <see langword="null"/> when absent.</param>
/// <param name="IndentEmu">The paragraph's own <c>indent</c> (first-line indent, EMU, frequently negative) attribute, or <see langword="null"/> when absent.</param>
/// <param name="LnSpcElement">The paragraph's own <c>&lt;a:lnSpc&gt;</c> element, or <see langword="null"/> when absent.</param>
/// <param name="SpcBeforeElement">The paragraph's own <c>&lt;a:spcBef&gt;</c> element, or <see langword="null"/> when absent.</param>
/// <param name="SpcAfterElement">The paragraph's own <c>&lt;a:spcAft&gt;</c> element, or <see langword="null"/> when absent.</param>
/// <param name="DefRPrElement">
///     The paragraph's own <c>&lt;a:pPr&gt;/&lt;a:defRPr&gt;</c> element (the paragraph-level run
///     property default every run in the paragraph without its own override falls back to,
///     ranking above the placeholder/master level-indexed style in the run-property inheritance
///     chain), or <see langword="null"/> when absent.
/// </param>
/// <param name="BulletProperties">
///     The paragraph's own raw, unresolved bullet/numbering markup (the
///     <c>&lt;a:buNone&gt;</c>/<c>&lt;a:buAutoNum&gt;</c>/<c>&lt;a:buChar&gt;</c> choice element,
///     plus the independent <c>&lt;a:buClrTx&gt;</c>/<c>&lt;a:buClr&gt;</c>,
///     <c>&lt;a:buFontTx&gt;</c>/<c>&lt;a:buFont&gt;</c>, and <c>&lt;a:buSzTx&gt;</c>/
///     <c>&lt;a:buSzPct&gt;</c>/<c>&lt;a:buSzPts&gt;</c> choice elements), resolved against the
///     inheritance chain in <see cref="PptxDocument.ResolveEffectiveBulletProperties"/> - not
///     here. Defaults to <see cref="PptxRawBulletProperties.Empty"/> (every field
///     <see langword="null"/>) so pre-existing call sites continue to compile unchanged.
/// </param>
internal sealed record PptxRawParagraphProperties(
    int Level,
    string? Algn,
    float? MarLEmu,
    float? IndentEmu,
    XElement? LnSpcElement,
    XElement? SpcBeforeElement,
    XElement? SpcAfterElement,
    XElement? DefRPrElement,
    PptxRawBulletProperties? BulletProperties = null)
{
    /// <summary>
    ///     The paragraph's own raw bullet properties, defaulting to
    ///     <see cref="PptxRawBulletProperties.Empty"/> when <see cref="BulletProperties"/> itself
    ///     is <see langword="null"/> (every pre-existing call site that omits the parameter
    ///     entirely).
    /// </summary>
    internal PptxRawBulletProperties EffectiveBulletProperties => BulletProperties ?? PptxRawBulletProperties.Empty;
}

/// <summary>
///     A paragraph's own raw, unresolved bullet/numbering markup, produced by
///     <see cref="PptxDocument.ParseBulletProperties"/>. Each field is one member of its own
///     independent OOXML schema choice-group (at most one of that group's members is present per
///     <c>&lt;a:pPr&gt;</c>/<c>&lt;a:lvl{N}pPr&gt;</c>); the four groups (type, color, font, size)
///     are themselves independent of each other, mirroring the per-attribute inheritance
///     granularity already used for run properties - see
///     <see cref="PptxDocument.ResolveEffectiveBulletProperties"/>'s remarks.
/// </summary>
/// <param name="TypeElement">
///     The one of <c>&lt;a:buNone&gt;</c>/<c>&lt;a:buAutoNum&gt;</c>/<c>&lt;a:buChar&gt;</c>
///     present, or <see langword="null"/> when none is declared at this tier.
/// </param>
/// <param name="ColorElement">
///     The <c>&lt;a:buClrTx&gt;</c> or <c>&lt;a:buClr&gt;</c> element present, or
///     <see langword="null"/> when neither is declared at this tier.
/// </param>
/// <param name="FontElement">
///     The <c>&lt;a:buFontTx&gt;</c> or <c>&lt;a:buFont&gt;</c> element present, or
///     <see langword="null"/> when neither is declared at this tier.
/// </param>
/// <param name="SizeElement">
///     The <c>&lt;a:buSzTx&gt;</c>, <c>&lt;a:buSzPct&gt;</c>, or <c>&lt;a:buSzPts&gt;</c> element
///     present, or <see langword="null"/> when none is declared at this tier.
/// </param>
internal sealed record PptxRawBulletProperties(
    XElement? TypeElement,
    XElement? ColorElement,
    XElement? FontElement,
    XElement? SizeElement)
{
    /// <summary>The shared, all-<see langword="null"/> instance for a tier that declares no bullet markup at all.</summary>
    internal static PptxRawBulletProperties Empty { get; } = new(null, null, null, null);
}

/// <summary>
///     A parsed <c>&lt;a:r&gt;</c> run: its own raw, unresolved <c>&lt;a:rPr&gt;</c> element plus
///     its <c>&lt;a:t&gt;</c> text, produced by <see cref="PptxDocument.ParseRun"/>.
/// </summary>
/// <param name="RawRPr">The run's own raw, unresolved <c>&lt;a:rPr&gt;</c> element, or <see langword="null"/> when absent.</param>
/// <param name="Text">The run's <c>&lt;a:t&gt;</c> text, defaulting to <see cref="string.Empty"/> when absent.</param>
internal sealed record PptxTextRun(XElement? RawRPr, string Text);

/// <summary>The resolved vertical text anchor of a text body (<c>&lt;a:bodyPr anchor="..."/&gt;</c>).</summary>
internal enum PptxTextAnchor
{
    /// <summary>Text anchors to the top of its shape's box (<c>anchor="t"</c>, the OOXML schema default).</summary>
    Top,

    /// <summary>Text anchors to the vertical middle of its shape's box (<c>anchor="ctr"</c>).</summary>
    Middle,

    /// <summary>Text anchors to the bottom of its shape's box (<c>anchor="b"</c>).</summary>
    Bottom,
}

/// <summary>The resolved word-wrap mode of a text body (<c>&lt;a:bodyPr wrap="..."/&gt;</c>).</summary>
internal enum PptxTextWrap
{
    /// <summary>Text wraps within the shape's bounding box (<c>wrap="square"</c>, the OOXML schema default).</summary>
    Square,

    /// <summary>Text never wraps (<c>wrap="none"</c>) - every paragraph lays out as a single, unbounded-width line.</summary>
    None,
}
