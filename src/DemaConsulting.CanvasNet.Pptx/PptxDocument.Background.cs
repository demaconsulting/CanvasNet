using System.Globalization;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore bgpr bgref pptx phclr patt

/// <summary>
///     Implements the <see cref="PptxDocument"/> slide/layout/master background-fill resolver
///     (Phase 2 Follow-Up): resolves a slide's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> background
///     fill - falling back to its layout's, then its master's, own <c>&lt;p:bg&gt;</c> when the
///     slide declares none - into a concrete <see cref="PptxPaint"/>, reusing the existing Phase
///     1c <see cref="ResolveFill"/>/<see cref="ResolveColor"/> pipeline verbatim rather than
///     re-implementing fill/color resolution for backgrounds - see <c>pptx-document.md</c>'s
///     "Phase 2 Follow-Up: Slide/Layout/Master Background Fill (&lt;p:bg&gt;)" design section for
///     the full resolution algorithm and the exact fidelity/deferred boundary.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     A <c>&lt;p:bgRef idx="..."/&gt;</c> <c>idx</c> value of <c>0</c> or <c>1000</c> means
    ///     "no background fill" (per ECMA-376's <c>CT_StyleMatrixReference</c> style-matrix-index
    ///     convention).
    /// </summary>
    private const uint BgRefNoFillIdxLow = 0;

    /// <summary>See <see cref="BgRefNoFillIdxLow"/>'s remarks.</summary>
    private const uint BgRefNoFillIdxHigh = 1000;

    /// <summary>
    ///     A <c>&lt;p:bgRef idx="..."/&gt;</c> <c>idx</c> value of 1001 or above indexes into the
    ///     theme's <c>&lt;a:fmtScheme&gt;/&lt;a:bgFillStyleLst&gt;</c>, 0-based from this offset
    ///     (<c>idx</c> 1001 is <see cref="PptxTheme.BgFillStyleList"/>'s first entry).
    /// </summary>
    private const uint BgFillStyleListIdxOffset = 1001;

    /// <summary>
    ///     Resolves the first-present-wins slide -&gt; layout -&gt; master <c>&lt;p:bg&gt;</c>
    ///     background fill into a concrete <see cref="PptxPaint"/>, matching the Phase 1b
    ///     placeholder-inheritance "first element present at all wins" precedent exactly (see
    ///     <see cref="ResolvePlaceholderProperties"/>'s remarks) - an empty-but-present
    ///     <c>&lt;p:bg/&gt;</c> still wins over a lower-priority tier's own <c>&lt;p:bg&gt;</c>.
    /// </summary>
    /// <param name="slideBackground">The slide's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> element, or <see langword="null"/> when absent.</param>
    /// <param name="layoutBackground">The slide's layout's own <c>&lt;p:bg&gt;</c> element, or <see langword="null"/> when absent.</param>
    /// <param name="masterBackground">That layout's master's own <c>&lt;p:bg&gt;</c> element, or <see langword="null"/> when absent.</param>
    /// <param name="theme">The resolved theme, used to resolve any <c>&lt;a:schemeClr&gt;</c> and, for a <c>&lt;p:bgRef&gt;</c>, its <see cref="PptxTheme.BgFillStyleList"/>.</param>
    /// <param name="widthEmu">The slide's own declared width, in EMU (needed to position a gradient background fill).</param>
    /// <param name="heightEmu">The slide's own declared height, in EMU (needed to position a gradient background fill).</param>
    /// <returns>
    ///     The resolved <see cref="PptxPaint"/>, or <see langword="null"/> when none of
    ///     <paramref name="slideBackground"/>/<paramref name="layoutBackground"/>/
    ///     <paramref name="masterBackground"/> declare a <c>&lt;p:bg&gt;</c> at all - the caller
    ///     falls back to <see cref="PptxRenderOptions.BackgroundColor"/> in that case (see
    ///     <see cref="Render(int, int, int, PptxRenderOptions?)"/>).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved <c>&lt;p:bg&gt;</c> declares neither <c>&lt;p:bgPr&gt;</c> nor
    ///     <c>&lt;p:bgRef&gt;</c>, when a <c>&lt;p:bgRef&gt;</c>'s <c>idx</c> attribute is missing
    ///     or not a valid non-negative integer, or when a <c>&lt;p:bgRef idx&gt;</c> of 1001 or
    ///     above indexes past the end of (or into an empty) <see cref="PptxTheme.BgFillStyleList"/> -
    ///     propagated unchanged from <see cref="ResolveFill"/>/<see cref="ResolveColor"/> for any
    ///     other malformed DrawingML construct.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when a <c>&lt;p:bgRef idx&gt;</c> is in the 1-999 range (the rare,
    ///     not-used-for-backgrounds-in-practice <c>&lt;a:fillStyleLst&gt;</c> half of the style
    ///     matrix - feature token <c>"pptx-bg-fill-style-ref"</c>), or propagated unchanged from
    ///     <see cref="ResolveFill"/> (a pattern/picture fill or a radial/path gradient).
    /// </exception>
    internal static PptxPaint? ResolveSlideBackgroundFill(
        XElement? slideBackground,
        XElement? layoutBackground,
        XElement? masterBackground,
        PptxTheme theme,
        float widthEmu,
        float heightEmu)
    {
        var background = slideBackground ?? layoutBackground ?? masterBackground;
        return background is null ? null : ResolveBackgroundElement(background, theme, widthEmu, heightEmu);
    }

    /// <summary>
    ///     Dispatches a single <c>&lt;p:bg&gt;</c> element's <c>&lt;p:bgPr&gt;</c> (an explicit
    ///     fill, resolved via the existing <see cref="ResolveFill"/>) or <c>&lt;p:bgRef&gt;</c>
    ///     (a theme format-scheme style reference, resolved via
    ///     <see cref="ResolveBackgroundStyleReference"/>).
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when neither child is present.</exception>
    private static PptxPaint ResolveBackgroundElement(XElement backgroundElement, PptxTheme theme, float widthEmu, float heightEmu)
    {
        var bgPr = backgroundElement.Element(PresentationNamespace + "bgPr");
        if (bgPr is not null)
        {
            // <p:bgPr> directly contains its fill-definition child (<a:noFill>/<a:solidFill>/
            // <a:gradFill>/<a:pattFill>/<a:blipFill>), same as <p:spPr> - ResolveFill's existing
            // child-dispatch logic applies unchanged.
            return ResolveFill(bgPr, theme, widthEmu, heightEmu);
        }

        var bgRef = backgroundElement.Element(PresentationNamespace + "bgRef");
        if (bgRef is not null)
        {
            return ResolveBackgroundStyleReference(bgRef, theme, widthEmu, heightEmu);
        }

        throw new InvalidDataException("A <p:bg> element has neither <p:bgPr> nor <p:bgRef>.");
    }

    /// <summary>
    ///     Resolves a <c>&lt;p:bgRef idx="..."/&gt;</c> theme-indexed background reference: maps
    ///     <c>idx</c> to the theme's <see cref="PptxTheme.BgFillStyleList"/> (per ECMA-376's
    ///     <c>CT_StyleMatrixReference</c> style-matrix-index convention - <c>0</c>/<c>1000</c>
    ///     mean "no background"; <c>1</c>-<c>999</c> index the rare, out-of-scope
    ///     <c>&lt;a:fillStyleLst&gt;</c>; <c>1001</c> and above index
    ///     <c>&lt;a:bgFillStyleLst&gt;</c>, 0-based from that offset), substituting
    ///     <c>&lt;p:bgRef&gt;</c>'s own single color-definition child (resolved via the existing
    ///     <see cref="ResolveColor"/>, with no override of its own - this is the base <c>phClr</c>
    ///     value itself) for every <c>&lt;a:schemeClr val="phClr"/&gt;</c> token the matched
    ///     style-list entry declares.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>idx</c> is missing or not a valid non-negative integer, or when an
    ///     <c>idx</c> of 1001 or above indexes past the end of (or into an empty)
    ///     <see cref="PptxTheme.BgFillStyleList"/>.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <c>idx</c> is in the 1-999 range (feature token
    ///     <c>"pptx-bg-fill-style-ref"</c>).
    /// </exception>
    private static PptxPaint ResolveBackgroundStyleReference(XElement bgRefElement, PptxTheme theme, float widthEmu, float heightEmu)
    {
        var idxValue = (string?)bgRefElement.Attribute("idx") ??
            throw new InvalidDataException("A <p:bgRef> element has no 'idx' attribute.");
        if (!uint.TryParse(idxValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            throw new InvalidDataException($"A <p:bgRef> element has a non-numeric 'idx' attribute value '{idxValue}'.");
        }

        if (idx is BgRefNoFillIdxLow or BgRefNoFillIdxHigh)
        {
            return PptxNoFill.Instance;
        }

        if (idx < BgFillStyleListIdxOffset)
        {
            throw new PptxUnsupportedFeatureException(
                "pptx-bg-fill-style-ref",
                $"A <p:bgRef idx=\"{idx}\"> references the theme's <a:fillStyleLst> (idx 1-999), which is not supported for slide backgrounds.");
        }

        var listIndex = idx - BgFillStyleListIdxOffset;
        if (listIndex >= theme.BgFillStyleList.Count)
        {
            throw new InvalidDataException(
                $"A <p:bgRef idx=\"{idx}\"> indexes past the end of the theme's <a:bgFillStyleLst> (which has {theme.BgFillStyleList.Count} entries).");
        }

        var styleEntry = theme.BgFillStyleList[(int)listIndex];

        // <p:bgRef>'s own single color-definition child (when present) supplies the concrete
        // phClr substitution value for the matched style-list entry's own <a:schemeClr
        // val="phClr"/> token(s) - resolved with no override of its own, since this color IS the
        // base phClr value, not itself parameterized by a further override.
        var colorElement = bgRefElement.Elements().FirstOrDefault();
        var phClrOverride = colorElement is null ? (Rgba32?)null : ResolveColor(colorElement, theme);

        // theme.BgFillStyleList's entries are themselves fill-definition elements (e.g.
        // <a:solidFill>), not a parent containing one - wrap in a synthetic parent so
        // ResolveFill's existing "look up a direct named child" dispatch logic applies unchanged
        // (adding an XElement that already has a parent clones it, leaving the theme's own tree
        // untouched).
        var syntheticFillParent = new XElement("pptxSyntheticBgFillParent", styleEntry);
        return ResolveFill(syntheticFillParent, theme, widthEmu, heightEmu, phClrOverride);
    }
}
