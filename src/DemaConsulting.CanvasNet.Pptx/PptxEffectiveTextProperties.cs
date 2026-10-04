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
internal sealed record PptxEffectiveParagraphProperties(
    string Alignment,
    float MarginLeftEmu,
    float IndentEmu,
    PptxLineSpacing LineSpacing);

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
