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
/// <param name="Runs">The paragraph's ordered <c>&lt;a:r&gt;</c> runs (an empty paragraph is valid DrawingML - a blank line).</param>
internal sealed record PptxParagraph(PptxRawParagraphProperties RawProperties, IReadOnlyList<PptxTextRun> Runs);

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
internal sealed record PptxRawParagraphProperties(
    int Level,
    string? Algn,
    float? MarLEmu,
    float? IndentEmu,
    XElement? LnSpcElement,
    XElement? SpcBeforeElement,
    XElement? SpcAfterElement,
    XElement? DefRPrElement);

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
