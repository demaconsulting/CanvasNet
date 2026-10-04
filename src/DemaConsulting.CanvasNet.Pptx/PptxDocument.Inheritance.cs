using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore sppr txbody lststyle sptree pptx ctrtitle sldnum

/// <summary>
///     Implements the <see cref="PptxDocument"/> placeholder property-inheritance resolver
///     (Phase 1b): the isolated, independently-testable implementation of ECMA-376 (ISO/IEC
///     29500) §19.3.1.36's placeholder matching algorithm.
/// </summary>
/// <remarks>
///     <para>
///         <strong>The verified matching algorithm</strong> (confirmed against the `python-pptx`
///         reference implementation's actual matching code and independent empirical
///         PowerPoint-repackaging notes - see the companion planning report for the full
///         evidentiary trail) is <em>not</em> a single "type+idx exact match, then type-only
///         fallback" test applied uniformly - it is two different, level-specific,
///         single-criterion matches:
///     </para>
///     <list type="number">
///         <item>
///             <description>
///                 <strong>Slide -&gt; layout</strong>: matched by <c>idx</c> ALONE. <c>type</c> is
///                 not consulted at this hop at all. A miss (no layout placeholder shares the
///                 slide placeholder's <c>idx</c>) is a genuine miss - it is not retried by type.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <strong>Layout -&gt; master</strong>: matched by <c>type</c> ALONE, through the
///                 fixed <see cref="RemapPlaceholderType"/> remapping table, using the
///                 <em>matched layout placeholder's own type</em> (not the slide placeholder's
///                 type). <c>idx</c> is not consulted at this hop. This hop only runs when hop 1
///                 found a layout match - a hop 1 miss short-circuits the chain (there is no
///                 layout type to remap/match against), it does not fall back to matching the
///                 master directly against the slide's own type.
///             </description>
///         </item>
///     </list>
///     <para>
///         The theme is not part of this matching chain itself - a master/layout references its
///         theme via an ordinary OPC relationship, supplying scheme-level color/font tokens a
///         shape's own property fragment may reference, rather than theme itself containing
///         placeholder-shaped XML to match against. It is therefore carried through
///         <see cref="PptxPlaceholderProperties.Theme"/> unchanged, as context for a later phase,
///         not selected via the same first-non-null fallback used for
///         <see cref="PptxPlaceholderProperties.EffectiveSpPr"/>/
///         <see cref="PptxPlaceholderProperties.EffectiveTxBodyListStyle"/>.
///     </para>
/// </remarks>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Resolves <paramref name="slidePlaceholder"/>'s effective property fragments by matching
    ///     it against <paramref name="layoutPlaceholders"/> and <paramref name="masterPlaceholders"/>
    ///     per the verified ECMA-376 placeholder matching algorithm (see this class's remarks),
    ///     then independently walking each named property category (<c>&lt;p:spPr&gt;</c>,
    ///     <c>&lt;p:txBody&gt;/&lt;a:lstStyle&gt;</c>) slide -&gt; matched layout -&gt; matched
    ///     master, taking the first element present at all (an empty element still counts as
    ///     present, stopping the fallback for that category - no deeper per-attribute merging is
    ///     performed in Phase 1b).
    /// </summary>
    /// <param name="slidePlaceholder">The slide-level placeholder to resolve.</param>
    /// <param name="layoutPlaceholders">The owning slide's layout's placeholder shapes.</param>
    /// <param name="masterPlaceholders">That layout's master's placeholder shapes.</param>
    /// <param name="theme">The resolved theme, carried through unchanged as context.</param>
    /// <returns>The resolved <see cref="PptxPlaceholderProperties"/>.</returns>
    /// <param name="masterTextStyles">
    ///     The owning slide master's own <c>&lt;p:txStyles&gt;</c> (Phase 1d), carried through
    ///     unchanged into the returned <see cref="PptxPlaceholderProperties.MasterTextStyles"/> -
    ///     optional (defaults to <see langword="null"/>) so Phase 1b/1c call sites continue to
    ///     compile unchanged.
    /// </param>
    internal static PptxPlaceholderProperties ResolvePlaceholderProperties(
        PptxPlaceholder slidePlaceholder,
        IReadOnlyList<PptxPlaceholder> layoutPlaceholders,
        IReadOnlyList<PptxPlaceholder> masterPlaceholders,
        PptxTheme theme,
        PptxMasterTextStyles? masterTextStyles = null)
    {
        // Hop 1: slide -> layout, matched by idx ALONE (type is not consulted here - verified,
        // see this class's remarks). A miss is a genuine miss, not retried by type.
        PptxPlaceholder? matchedLayoutPlaceholder = null;
        foreach (var candidate in layoutPlaceholders)
        {
            if (candidate.Idx == slidePlaceholder.Idx)
            {
                matchedLayoutPlaceholder = candidate;
                break;
            }
        }

        // Hop 2: layout -> master, matched by TYPE ALONE through the fixed remapping table (idx
        // is not consulted here - verified). Only runs when hop 1 found a match; a hop 1 miss
        // short-circuits the chain rather than falling back to matching the slide's own type.
        PptxPlaceholder? matchedMasterPlaceholder = null;
        if (matchedLayoutPlaceholder is not null)
        {
            var remappedType = RemapPlaceholderType(matchedLayoutPlaceholder.Type);
            foreach (var candidate in masterPlaceholders)
            {
                if (candidate.Type == remappedType)
                {
                    matchedMasterPlaceholder = candidate;
                    break;
                }
            }
        }

        var effectiveSpPr =
            slidePlaceholder.ShapeElement.Element(PresentationNamespace + "spPr") ??
            matchedLayoutPlaceholder?.ShapeElement.Element(PresentationNamespace + "spPr") ??
            matchedMasterPlaceholder?.ShapeElement.Element(PresentationNamespace + "spPr");

        var effectiveTxBodyListStyle =
            GetTxBodyListStyle(slidePlaceholder) ??
            (matchedLayoutPlaceholder is null ? null : GetTxBodyListStyle(matchedLayoutPlaceholder)) ??
            (matchedMasterPlaceholder is null ? null : GetTxBodyListStyle(matchedMasterPlaceholder));

        return new PptxPlaceholderProperties(effectiveSpPr, effectiveTxBodyListStyle, theme, masterTextStyles);
    }

    /// <summary>Extracts a placeholder's <c>&lt;p:txBody&gt;/&lt;a:lstStyle&gt;</c> element, if present.</summary>
    private static XElement? GetTxBodyListStyle(PptxPlaceholder placeholder) =>
        placeholder.ShapeElement.Element(PresentationNamespace + "txBody")?.Element(DrawingNamespace + "lstStyle");

    /// <summary>
    ///     Remaps a layout placeholder's <c>type</c> to the type used to match against a master
    ///     placeholder, per the verified python-pptx table: <c>"body"</c>, <c>"chart"</c>,
    ///     <c>"bitmap"</c>, <c>"orgChart"</c>, <c>"mediaClip"</c>, <c>"obj"</c>, <c>"pic"</c>,
    ///     <c>"subTitle"</c>, <c>"tbl"</c>, <c>"clipArt"</c>, <c>"dgm"</c>, and <c>"media"</c> all
    ///     remap to <c>"body"</c>; <c>"ctrTitle"</c> remaps to <c>"title"</c>; every other type
    ///     (including <c>"title"</c>, <c>"dt"</c>, <c>"ftr"</c>, <c>"sldNum"</c>, and any
    ///     unrecognized value) maps to itself - a defensive, non-throwing fallback, since an
    ///     unrecognized <c>type</c> value is tolerated input, not a structural package error.
    /// </summary>
    private static string RemapPlaceholderType(string type) =>
        type switch
        {
            "body" or "chart" or "bitmap" or "orgChart" or "mediaClip" or "obj" or "pic"
                or "subTitle" or "tbl" or "clipArt" or "dgm" or "media" => "body",
            "ctrTitle" => "title",
            _ => type,
        };
}
