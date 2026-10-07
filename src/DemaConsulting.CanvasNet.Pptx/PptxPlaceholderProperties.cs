using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm prst cust

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
///     The first <c>&lt;p:txBody&gt;/&lt;a:lstStyle&gt;</c> element (walking the same slide -&gt;
///     matched layout placeholder -&gt; matched master placeholder chain, independently of
///     <see cref="EffectiveSpPr"/>) that itself declares at least one <c>&lt;a:lvl1pPr&gt;</c>
///     through <c>&lt;a:lvl9pPr&gt;</c> level-override child, or <see langword="null"/> if none
///     declares one. A tier's own empty, self-closing <c>&lt;a:lstStyle/&gt;</c> (no level-override
///     child at all) does <em>not</em> count as "declaring one" and is walked past, for the same
///     reason <see cref="EffectiveGeometrySpPr"/> walks past an empty, otherwise-"present"
///     <c>&lt;p:spPr/&gt;</c>: a real-world slide placeholder commonly declares its own empty
///     <c>&lt;a:lstStyle/&gt;</c> while deliberately relying on the layout/master for level-based
///     run/paragraph overrides (for example a large title <c>&lt;a:defRPr sz="..."/&gt;</c>) -
///     <c>GetLevelDefRPr</c>/<c>GetLevelElement</c> (<see cref="PptxDocument"/>'s text-inheritance
///     resolver, in <c>PptxDocument.TextInheritance.cs</c>) index this
///     element by level, so stopping at an empty element here would silently defeat every level
///     lookup against the layout/master's real override instead of consulting it.
/// </param>
/// <param name="EffectiveXfrmElement">
///     The first non-null <c>&lt;p:spPr&gt;/&lt;a:xfrm&gt;</c> element found walking the same
///     slide -&gt; matched layout placeholder -&gt; matched master placeholder chain, resolved
///     <em>independently</em> of <see cref="EffectiveSpPr"/> (a per-attribute lookup, not a
///     by-product of whichever tier's whole <c>&lt;p:spPr&gt;</c> element won): a real-world
///     placeholder commonly declares its own empty <c>&lt;p:spPr/&gt;</c> (no <c>&lt;a:xfrm&gt;</c>
///     child at all, deliberately relying on the layout/master for position/size while still
///     being "present" for <see cref="EffectiveSpPr"/>'s own element-level fallback) - resolving
///     <see cref="EffectiveSpPr"/>'s <c>&lt;a:xfrm&gt;</c> child directly would incorrectly stop at
///     that empty element and never consult the layout/master's own geometry, silently rendering
///     nothing. Defaults to <see langword="null"/> so pre-existing call sites continue to compile
///     unchanged.
/// </param>
/// <param name="EffectiveGeometrySpPr">
///     The first <c>&lt;p:spPr&gt;</c> element (walking the same slide -&gt; matched layout
///     placeholder -&gt; matched master placeholder chain) that itself declares an
///     <c>&lt;a:prstGeom&gt;</c> or <c>&lt;a:custGeom&gt;</c> child, resolved independently of
///     <see cref="EffectiveSpPr"/> for exactly the same reason <see cref="EffectiveXfrmElement"/>
///     is: a real-world placeholder's own (otherwise "present", element-level-winning) empty
///     <c>&lt;p:spPr/&gt;</c> commonly omits geometry too, deliberately relying on the slide
///     master's own placeholder shape (which, per ECMA-376's standard master content, always
///     declares <c>&lt;a:prstGeom prst="rect"/&gt;</c>) - unlike fill/line (see
///     <see cref="PptxDocument.ResolveFill"/>'s own remarks: fill/line inheritance from
///     placeholder/layout/master is a deliberate, documented Phase 1c simplification, not
///     resolved at all), geometry has no such graceful no-op fallback - <see cref="PptxDocument.ResolveShapeGeometry"/>
///     throws when neither child is present, so failing to walk this chain independently turns a
///     a real, common document shape into an unhandled exception instead of a silent skip.
///     Defaults to <see langword="null"/> so pre-existing call sites continue to compile
///     unchanged.
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
/// <param name="EffectivePlaceholderType">
///     The slide placeholder's effective type for master text-style-bucket selection
///     (<see cref="PptxDocument.SelectMasterTextStyle"/>/<see cref="PptxDocument.DefaultTypeface"/>):
///     its own <see cref="PptxPlaceholder.DeclaredType"/> when explicitly present, otherwise the
///     idx-matched layout placeholder's <see cref="PptxPlaceholder.Type"/> (mirroring the same
///     hop-1 idx-only match already used for <see cref="EffectiveXfrmElement"/>/
///     <see cref="EffectiveGeometrySpPr"/>), otherwise the slide placeholder's own
///     schema-defaulted <see cref="PptxPlaceholder.Type"/> - a slide <c>&lt;p:ph&gt;</c> that
///     omits <c>type</c> must inherit the matched layout placeholder's type rather than being
///     treated as a genuinely-declared <c>"obj"</c> placeholder (see the companion planning
///     report's root-cause finding for the Section Header title font-size bug). Defaults to
///     <see langword="null"/> so pre-existing call sites continue to compile unchanged; a
///     <see langword="null"/> value means "no placeholder-specific override available" (for
///     example a non-placeholder shape), and callers should fall back to their own raw type.
/// </param>
internal sealed record PptxPlaceholderProperties(
    XElement? EffectiveSpPr,
    XElement? EffectiveTxBodyListStyle,
    PptxTheme Theme,
    PptxMasterTextStyles? MasterTextStyles = null,
    XElement? EffectiveXfrmElement = null,
    XElement? EffectiveGeometrySpPr = null,
    string? EffectivePlaceholderType = null);
