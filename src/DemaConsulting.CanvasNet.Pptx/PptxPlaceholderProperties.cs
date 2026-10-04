using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     The effective, resolved property fragments for a single slide placeholder, produced by
///     <see cref="PptxDocument.ResolvePlaceholderProperties"/>.
/// </summary>
/// <param name="EffectiveSpPr">
///     The first non-null <c>&lt;p:spPr&gt;</c> element found walking slide -&gt; matched layout
///     placeholder -&gt; matched master placeholder (in that order), or <see langword="null"/> if
///     none of the three declares one.
/// </param>
/// <param name="EffectiveTxBodyListStyle">
///     The first non-null <c>&lt;p:txBody&gt;/&lt;a:lstStyle&gt;</c> element found walking the
///     same slide -&gt; matched layout placeholder -&gt; matched master placeholder chain
///     independently of <see cref="EffectiveSpPr"/>, or <see langword="null"/> if none declares
///     one.
/// </param>
/// <param name="Theme">
///     The resolved theme carried through as context (not itself selected via the fallback chain -
///     see <see cref="PptxDocument.ResolvePlaceholderProperties"/>'s remarks).
/// </param>
/// <param name="MasterTextStyles">
///     The owning slide master's own <c>&lt;p:txStyles&gt;</c> (Phase 1d), carried through
///     alongside <see cref="Theme"/> as further context for the run/paragraph property-inheritance
///     resolver (<see cref="PptxDocument.ResolveEffectiveRunProperties"/>) - like <see cref="Theme"/>,
///     it is not itself part of the slide-&gt;layout-&gt;master placeholder matching chain.
///     Defaults to <see langword="null"/> (treated identically to all-<see langword="null"/>
///     buckets) so Phase 1b/1c call sites that predate Phase 1d continue to compile unchanged.
/// </param>
internal sealed record PptxPlaceholderProperties(
    XElement? EffectiveSpPr,
    XElement? EffectiveTxBodyListStyle,
    PptxTheme Theme,
    PptxMasterTextStyles? MasterTextStyles = null);
