using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     A run's fully-resolved, effective character properties (Phase 1d), produced by
///     <see cref="PptxDocument.ResolveEffectiveRunProperties"/> by walking the attribute-level
///     run/paragraph/placeholder/master/theme inheritance chain - see
///     <c>PptxDocument.TextInheritance.cs</c>'s class remarks for the full, verified chain.
/// </summary>
/// <param name="FontFamily">The resolved typeface name (<c>&lt;a:latin typeface="..."/&gt;</c>).</param>
/// <param name="SizeEmu">The resolved font size, in EMU (already scaled by any applicable autofit <c>fontScale</c>).</param>
/// <param name="Bold">Whether the run is resolved bold.</param>
/// <param name="Italic">Whether the run is resolved italic.</param>
/// <param name="UnderlineStyle">The run's resolved underline style (Phase 2 Follow-Up: Underline Rendering).</param>
/// <param name="UnderlineColor">
///     The run's resolved underline ink color (Phase 2 Follow-Up: Underline Rendering) - only
///     meaningful when <paramref name="UnderlineStyle"/> is not <see cref="PptxUnderlineStyle.None"/>.
/// </param>
/// <param name="Color">The run's resolved color.</param>
/// <param name="OutlineWidthEmu">
///     The run's resolved text-outline (stroke) width, in EMU, from a run-level <c>&lt;a:ln&gt;</c>
///     child of <c>&lt;a:rPr&gt;</c>/<c>&lt;a:defRPr&gt;</c> (Phase 2 Follow-Up: Run Text Outline) -
///     <see langword="null"/> when no tier in the attribute-level inheritance chain declares one,
///     meaning "no outline is painted for this run". Distinct from a shape's own
///     <c>&lt;p:spPr&gt;/&lt;a:ln&gt;</c>, which strokes the shape's geometry, not its text.
/// </param>
/// <param name="OutlineColor">
///     The run's resolved text-outline ink color, only meaningful when
///     <paramref name="OutlineWidthEmu"/> is non-<see langword="null"/>.
/// </param>
internal sealed record PptxEffectiveRunProperties(
    string FontFamily,
    float SizeEmu,
    bool Bold,
    bool Italic,
    PptxUnderlineStyle UnderlineStyle,
    Rgba32 UnderlineColor,
    Rgba32 Color,
    float? OutlineWidthEmu = null,
    Rgba32 OutlineColor = default);

/// <summary>
///     The discriminator for a run's resolved <c>&lt;a:rPr u="..."/&gt;</c> underline style
///     (Phase 2 Follow-Up: Underline Rendering), produced by
///     <see cref="PptxDocument.GetUnderlineStyle"/>.
/// </summary>
/// <remarks>
///     ECMA-376 §20.1.10.84 defines many more <c>u</c> values than CanvasNet distinguishes for
///     painting: <c>heavy</c>, <c>dotted</c>, <c>dottedHeavy</c>, <c>dash</c>, <c>dashHeavy</c>,
///     <c>dashLong</c>, <c>dashLongHeavy</c>, <c>dotDash</c>, <c>dotDashHeavy</c>,
///     <c>dotDotDash</c>, <c>dotDotDashHeavy</c>, <c>wavy</c>, <c>wavyHeavy</c>, <c>wavyDbl</c>,
///     and any other unrecognized non-empty string, all map to <see cref="Other"/> and are
///     painted as a single solid line identical to <see cref="Single"/> - a documented
///     simplification (true dotted/dashed/wavy stroke patterns are a separable, deferred
///     refinement; see the design document's "Phase 2 Follow-Up: Underline Rendering" section).
/// </remarks>
internal enum PptxUnderlineStyle
{
    /// <summary>No underline is painted (an explicit <c>&lt;a:rPr u="none"/&gt;</c>, or the OOXML schema default when the attribute is absent at every inheritance tier).</summary>
    None,

    /// <summary>A single solid underline (<c>&lt;a:rPr u="sng"/&gt;</c>).</summary>
    Single,

    /// <summary>A double solid underline (<c>&lt;a:rPr u="dbl"/&gt;</c>), painted as two thinner, gapped lines.</summary>
    Double,

    /// <summary>Every other recognized/unrecognized non-<c>"none"</c> <c>u</c> value - see this enum's own remarks.</summary>
    Other,
}

/// <summary>
///     A paragraph's fully-resolved, effective properties (Phase 1d), produced by
///     <see cref="PptxDocument.ResolveEffectiveParagraphProperties"/>.
/// </summary>
/// <param name="Alignment">
///     The resolved horizontal alignment: <c>"l"</c>, <c>"ctr"</c>, or <c>"r"</c> - <c>"just"</c>
///     (full justification) is deliberately resolved to <c>"l"</c>, a documented simplification
///     (see the design document's "Text Layout and Rendering (Phase 1d)" section).
/// </param>
/// <param name="MarginLeftEmu">The resolved left margin, in EMU.</param>
/// <param name="IndentEmu">The resolved first-line indent, in EMU (frequently negative).</param>
/// <param name="LineSpacing">The resolved line spacing (already scaled by any applicable autofit <c>lnSpcReduction</c>).</param>
/// <param name="Bullet">
///     The paragraph's resolved bullet/numbering properties (Phase 2 Follow-Up: Bullets and
///     Numbering), produced by <see cref="PptxDocument.ResolveEffectiveParagraphProperties"/>
///     walking the same attribute-level chain as every other paragraph property. Defaults to
///     <see langword="null"/> so pre-existing call sites continue to compile unchanged; a
///     <see langword="null"/> value is treated identically to
///     <see cref="PptxEffectiveBulletProperties.Kind"/> being <see cref="PptxBulletKind.None"/>
///     (no bullet painted) by every consumer.
/// </param>
/// <param name="SpaceBefore">
///     The resolved <c>&lt;a:spcBef&gt;</c> paragraph spacing-before (Phase 2 Follow-Up:
///     Paragraph Spacing), produced by <see cref="PptxDocument.ResolveEffectiveParagraphProperties"/>
///     walking the same attribute-level chain as every other paragraph property. Defaults to
///     <see cref="PptxLineSpacing.None"/> (no extra spacing) so pre-existing call sites continue
///     to compile unchanged. Only applied as an additive gap between adjacent paragraphs - never
///     before a text body's first paragraph - see <see cref="PptxDocument.BuildLines"/>'s own
///     remarks for the exact additive semantics.
/// </param>
/// <param name="SpaceAfter">
///     The resolved <c>&lt;a:spcAft&gt;</c> paragraph spacing-after (Phase 2 Follow-Up: Paragraph
///     Spacing), mirroring <paramref name="SpaceBefore"/> in every respect except which side of
///     the paragraph it applies to - never applied after a text body's last paragraph.
/// </param>
internal sealed record PptxEffectiveParagraphProperties(
    string Alignment,
    float MarginLeftEmu,
    float IndentEmu,
    PptxLineSpacing LineSpacing,
    PptxEffectiveBulletProperties? Bullet = null,
    PptxLineSpacing? SpaceBefore = null,
    PptxLineSpacing? SpaceAfter = null)
{
    /// <summary>The resolved spacing-before, defaulting to <see cref="PptxLineSpacing.None"/> when not supplied.</summary>
    internal PptxLineSpacing EffectiveSpaceBefore => SpaceBefore ?? PptxLineSpacing.None;

    /// <summary>The resolved spacing-after, defaulting to <see cref="PptxLineSpacing.None"/> when not supplied.</summary>
    internal PptxLineSpacing EffectiveSpaceAfter => SpaceAfter ?? PptxLineSpacing.None;
}

/// <summary>
///     The discriminator for a paragraph's resolved bullet/numbering type, produced by resolving
///     the mutually-exclusive <c>&lt;a:buNone&gt;</c>/<c>&lt;a:buAutoNum&gt;</c>/
///     <c>&lt;a:buChar&gt;</c> OOXML schema choice-group.
/// </summary>
internal enum PptxBulletKind
{
    /// <summary>No bullet is painted for this paragraph (an explicit <c>&lt;a:buNone/&gt;</c>, or the conservative hard-coded default).</summary>
    None,

    /// <summary>A single literal bullet character/glyph string (<c>&lt;a:buChar char="..."/&gt;</c>).</summary>
    Char,

    /// <summary>An auto-numbered bullet (<c>&lt;a:buAutoNum type="..." startAt="..."/&gt;</c>).</summary>
    AutoNum,
}

/// <summary>
///     A paragraph's fully-resolved, effective bullet/numbering properties (Phase 2 Follow-Up:
///     Bullets and Numbering), produced by
///     <see cref="PptxDocument.ResolveEffectiveParagraphProperties"/> walking four independent
///     choice-group chains (type, color, font, size) - see
///     <c>PptxDocument.TextInheritance.cs</c>'s <c>ResolveEffectiveBulletProperties</c> remarks for
///     the full, verified chain.
/// </summary>
/// <param name="Kind">The resolved bullet type discriminator.</param>
/// <param name="Character">
///     The resolved literal bullet character/glyph string, only meaningful when
///     <paramref name="Kind"/> is <see cref="PptxBulletKind.Char"/>; <see langword="null"/>
///     otherwise.
/// </param>
/// <param name="AutoNumType">
///     The resolved <c>&lt;a:buAutoNum type="..."/&gt;</c> value (for example
///     <c>"arabicPeriod"</c>), only meaningful when <paramref name="Kind"/> is
///     <see cref="PptxBulletKind.AutoNum"/>; <see langword="null"/> otherwise.
/// </param>
/// <param name="AutoNumStartAt">
///     The resolved <c>&lt;a:buAutoNum startAt="..."/&gt;</c> value (defaulting to <c>1</c>),
///     only meaningful when <paramref name="Kind"/> is <see cref="PptxBulletKind.AutoNum"/>.
/// </param>
/// <param name="FontFamily">The resolved bullet typeface name (never bold/italic - see the design document's documented simplification).</param>
/// <param name="SizeEmu">The resolved bullet font size, in EMU (not yet scaled by any applicable autofit <c>fontScale</c> - the layout engine applies that scale, mirroring run sizes).</param>
/// <param name="Color">The resolved bullet ink color.</param>
internal sealed record PptxEffectiveBulletProperties(
    PptxBulletKind Kind,
    string? Character,
    string? AutoNumType,
    int AutoNumStartAt,
    string FontFamily,
    float SizeEmu,
    Rgba32 Color)
{
    /// <summary>Builds a <see cref="PptxBulletKind.None"/> instance carrying the supplied (otherwise-unused) font/size/color context.</summary>
    internal static PptxEffectiveBulletProperties CreateNone(string fontFamily, float sizeEmu, Rgba32 color) =>
        new(PptxBulletKind.None, null, null, 1, fontFamily, sizeEmu, color);
}

/// <summary>
///     A paragraph's resolved line spacing: either a percentage of a single line's natural height
///     (<c>&lt;a:spcPct val="..."/&gt;</c>) or a fixed point size (<c>&lt;a:spcPts val="..."/&gt;</c>)
///     - mutually exclusive per the OOXML schema, modeled as a discriminated pair where exactly
///     one of <see cref="Percent"/>/<see cref="FixedEmu"/> is non-null.
/// </summary>
/// <param name="Percent">The resolved percentage (<c>1.0</c> = 100%), or <see langword="null"/> when fixed spacing applies instead.</param>
/// <param name="FixedEmu">The resolved fixed spacing, in EMU, or <see langword="null"/> when percentage spacing applies instead.</param>
internal sealed record PptxLineSpacing(float? Percent, float? FixedEmu)
{
    /// <summary>The OOXML schema default line spacing: 100% of a single line's natural height.</summary>
    internal static PptxLineSpacing Default { get; } = new(1.0f, null);

    /// <summary>
    ///     The "no extra spacing" sentinel (Phase 2 Follow-Up: Paragraph Spacing): 0% of a single
    ///     line's natural height - distinct from <see cref="Default"/>'s "100% of line height"
    ///     sentinel, which is meaningless for an unresolved <c>&lt;a:spcBef&gt;</c>/<c>&lt;a:spcAft&gt;</c>
    ///     (a paragraph that never declares spacing-before/after must contribute zero extra gap,
    ///     not a full extra line's worth).
    /// </summary>
    internal static PptxLineSpacing None { get; } = new(0f, null);
}
