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
/// <param name="Underline">Whether the run is resolved underlined.</param>
/// <param name="Color">The run's resolved color.</param>
internal sealed record PptxEffectiveRunProperties(
    string FontFamily,
    float SizeEmu,
    bool Bold,
    bool Italic,
    bool Underline,
    Rgba32 Color);

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
internal sealed record PptxEffectiveParagraphProperties(
    string Alignment,
    float MarginLeftEmu,
    float IndentEmu,
    PptxLineSpacing LineSpacing,
    PptxEffectiveBulletProperties? Bullet = null);

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
}
