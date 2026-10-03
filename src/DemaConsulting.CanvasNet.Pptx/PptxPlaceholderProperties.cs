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
internal sealed record PptxPlaceholderProperties(
    XElement? EffectiveSpPr,
    XElement? EffectiveTxBodyListStyle,
    PptxTheme Theme);
