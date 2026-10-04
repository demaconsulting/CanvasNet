using System.Globalization;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore nvsppr nvpr sptree pptx

/// <summary>
///     A single placeholder shape (a <c>&lt;p:sp&gt;</c> with a <c>&lt;p:ph&gt;</c> descendant)
///     parsed structurally from a slide master, slide layout, or slide - identical shape across
///     all three levels, matched between levels by
///     <see cref="PptxDocument.ResolvePlaceholderProperties"/>'s resolver.
/// </summary>
/// <param name="Type">
///     The placeholder's <c>&lt;p:ph type="..."/&gt;</c> value, or <c>"obj"</c> (the OOXML schema
///     default) when the attribute is omitted.
/// </param>
/// <param name="Idx">
///     The placeholder's <c>&lt;p:ph idx="..."/&gt;</c> value, or <c>0</c> (the OOXML schema
///     default) when the attribute is omitted.
/// </param>
/// <param name="ShapeElement">
///     The raw <c>&lt;p:sp&gt;</c> element, retained unparsed beyond this shape - later
///     phases/the Inheritance resolver extract individual property fragments (for example
///     <c>&lt;p:spPr&gt;</c>) from it directly rather than this type pre-extracting every
///     possible fragment.
/// </param>
/// <param name="DeclaredType">
///     The placeholder's own <c>&lt;p:ph type="..."/&gt;</c> value exactly as written, or
///     <see langword="null"/> when the attribute is omitted - distinct from <see cref="Type"/>,
///     which collapses "omitted" and "explicitly <c>obj</c>" into the same schema-defaulted
///     string. <see cref="PptxDocument.ResolvePlaceholderProperties"/> consults this to resolve a
///     slide placeholder's <em>effective</em> type (its own declared type when present,
///     otherwise the idx-matched layout placeholder's type) for master text-style-bucket
///     selection (<see cref="PptxDocument.ResolveEffectiveRunProperties"/>) - an omitted slide
///     <c>type</c> must inherit the matched layout placeholder's type rather than being treated
///     as a genuine, schema-defaulted <c>"obj"</c> placeholder. Defaults to <see langword="null"/>
///     so pre-existing 3-argument call sites continue to compile unchanged.
/// </param>
internal sealed record PptxPlaceholder(string Type, uint Idx, XElement ShapeElement, string? DeclaredType = null);

/// <summary>A parsed slide master: its own part path, its theme's part path, its placeholder shapes, and its text styles.</summary>
/// <param name="PartPath">The master's own resolved part path.</param>
/// <param name="ThemePartPath">The master's resolved <c>/theme</c> relationship target part path.</param>
/// <param name="Placeholders">The master's immediate placeholder shapes, in document order.</param>
/// <param name="TxStyles">
///     The master's own <c>&lt;p:txStyles&gt;</c> element (Phase 1d), parsed into a
///     <see cref="PptxMasterTextStyles"/> - a direct child of <c>&lt;p:sldMaster&gt;</c>, a
///     sibling of <c>&lt;p:cSld&gt;</c>, not nested inside it.
/// </param>
/// <param name="Background">
///     The master's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> element, retained raw/unparsed (the
///     same "retain raw, resolve lazily" pattern <see cref="PptxPlaceholder.ShapeElement"/>
///     already establishes),
///     or <see langword="null"/> when the master declares no background - the final fallback tier
///     of the slide -&gt; layout -&gt; master background-fill resolution chain (see
///     <see cref="PptxDocument.ResolveSlideBackgroundFill"/>).
/// </param>
/// <param name="ShapeTree">
///     The master's full recursive shape tree, parsed via <see cref="PptxDocument.ParseShapeTree"/>
///     from the same <c>&lt;p:cSld&gt;/&lt;p:spTree&gt;</c> element as <paramref name="Placeholders"/> -
///     used (Phase 2 Follow-Up) to render the master's own non-placeholder decorative shapes
///     (pictures, autoshapes, groups, freeform shapes) beneath every slide using this master; its
///     own placeholder shapes are never painted directly from this list (see
///     <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>'s
///     <c>skipPlaceholderShapes</c> mechanism). Defaults to <c>Array.Empty&lt;PptxShapeTreeNode&gt;()</c>
///     so pre-existing 4-argument call sites continue to compile unchanged.
/// </param>
internal sealed record PptxMaster(
    string PartPath,
    string ThemePartPath,
    IReadOnlyList<PptxPlaceholder> Placeholders,
    PptxMasterTextStyles TxStyles,
    XElement? Background = null,
    IReadOnlyList<PptxShapeTreeNode>? ShapeTree = null)
{
    /// <summary>The master's full recursive shape tree - see the constructor parameter's own remarks.</summary>
    public IReadOnlyList<PptxShapeTreeNode> ShapeTree { get; init; } = ShapeTree ?? Array.Empty<PptxShapeTreeNode>();
}

/// <summary>
///     A slide master's own <c>&lt;p:txStyles&gt;</c> element (Phase 1d), carrying each of its
///     (schema-optional) title/body/other list styles unparsed - each is an <c>&lt;a:lstStyle&gt;</c>
///     -shaped element consulted, level-indexed, by the run/paragraph property-inheritance
///     resolver (<see cref="PptxDocument.ResolveEffectiveRunProperties"/>) after a placeholder's
///     own <c>EffectiveTxBodyListStyle</c> and before the theme/hard-coded default.
/// </summary>
/// <param name="TitleStyle">The master's <c>&lt;p:titleStyle&gt;</c> element, or <see langword="null"/> when absent.</param>
/// <param name="BodyStyle">The master's <c>&lt;p:bodyStyle&gt;</c> element, or <see langword="null"/> when absent.</param>
/// <param name="OtherStyle">The master's <c>&lt;p:otherStyle&gt;</c> element, or <see langword="null"/> when absent.</param>
internal sealed record PptxMasterTextStyles(XElement? TitleStyle, XElement? BodyStyle, XElement? OtherStyle);

/// <summary>A parsed slide layout: its own part path, its master's part path, and its placeholder shapes.</summary>
/// <param name="PartPath">The layout's own resolved part path.</param>
/// <param name="MasterPartPath">The layout's resolved <c>/slideMaster</c> relationship target part path.</param>
/// <param name="Placeholders">The layout's immediate placeholder shapes, in document order.</param>
/// <param name="Background">
///     The layout's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> element, retained raw/unparsed, or
///     <see langword="null"/> when the layout declares no background - the middle tier of the
///     slide -&gt; layout -&gt; master background-fill resolution chain (see
///     <see cref="PptxDocument.ResolveSlideBackgroundFill"/>).
/// </param>
/// <param name="ShapeTree">
///     The layout's full recursive shape tree, parsed via <see cref="PptxDocument.ParseShapeTree"/>
///     from the same <c>&lt;p:cSld&gt;/&lt;p:spTree&gt;</c> element as <paramref name="Placeholders"/> -
///     used (Phase 2 Follow-Up) to render the layout's own non-placeholder decorative shapes
///     (pictures, autoshapes, groups, freeform shapes) on every slide using this layout, on top of
///     its master's own equivalent shapes; its own placeholder shapes are never painted directly
///     from this list (see <see cref="PptxDocument.Render(int, int, int, PptxRenderOptions?)"/>'s
///     <c>skipPlaceholderShapes</c> mechanism). Defaults to <c>Array.Empty&lt;PptxShapeTreeNode&gt;()</c>
///     so pre-existing 4-argument call sites continue to compile unchanged.
/// </param>
internal sealed record PptxLayout(
    string PartPath,
    string MasterPartPath,
    IReadOnlyList<PptxPlaceholder> Placeholders,
    XElement? Background = null,
    IReadOnlyList<PptxShapeTreeNode>? ShapeTree = null)
{
    /// <summary>The layout's full recursive shape tree - see the constructor parameter's own remarks.</summary>
    public IReadOnlyList<PptxShapeTreeNode> ShapeTree { get; init; } = ShapeTree ?? Array.Empty<PptxShapeTreeNode>();
}

/// <summary>
///     A parsed slide: its own part path, its layout's part path, its placeholder shapes, and (as
///     of Phase 1e) its full recursive shape tree.
/// </summary>
/// <param name="PartPath">The slide's own resolved part path.</param>
/// <param name="LayoutPartPath">The slide's resolved <c>/slideLayout</c> relationship target part path.</param>
/// <param name="Placeholders">
///     The slide's immediate placeholder shapes (direct children of <c>&lt;p:spTree&gt;</c> only -
///     Phase 1b does not recurse into groups), in document order. Still populated and unchanged by
///     Phase 1e - the run/paragraph property-inheritance resolver
///     (<see cref="PptxDocument.ResolvePlaceholderProperties"/>) continues to consult this flat
///     list rather than <see cref="ShapeTree"/>.
/// </param>
/// <param name="ShapeTree">
///     The slide's full recursive shape tree (Phase 1e), parsed via
///     <see cref="PptxDocument.ParseShapeTree"/> from the same <c>&lt;p:spTree&gt;</c> element as
///     <paramref name="Placeholders"/> - recurses into <c>&lt;p:grpSp&gt;</c> groups (unlike
///     <paramref name="Placeholders"/>) and additionally recognizes <c>&lt;p:pic&gt;</c>/
///     <c>&lt;p:graphicFrame&gt;</c> shapes.
/// </param>
/// <param name="Background">
///     The slide's own <c>&lt;p:cSld&gt;/&lt;p:bg&gt;</c> element, retained raw/unparsed, or
///     <see langword="null"/> when the slide declares no background - the highest-priority tier
///     of the slide -&gt; layout -&gt; master background-fill resolution chain (see
///     <see cref="PptxDocument.ResolveSlideBackgroundFill"/>).
/// </param>
internal sealed record PptxSlide(
    string PartPath,
    string LayoutPartPath,
    IReadOnlyList<PptxPlaceholder> Placeholders,
    IReadOnlyList<PptxShapeTreeNode> ShapeTree,
    XElement? Background = null);

/// <summary>
///     Shared placeholder-shape structural parser, used identically by the master/layout/slide
///     parsers (<see cref="PptxDocument.GetMaster"/>/<see cref="PptxDocument.GetLayout"/>/
///     <see cref="PptxDocument.GetSlide(int)"/>) to avoid duplicating the same XML-shape
///     extraction three times.
/// </summary>
internal static class PptxPlaceholderParser
{
    /// <summary>
    ///     Walks <paramref name="spTreeElement"/>'s immediate <c>&lt;p:sp&gt;</c> children only
    ///     (no group/recursive descent), filters to those with a
    ///     <c>&lt;p:nvSpPr&gt;/&lt;p:nvPr&gt;/&lt;p:ph&gt;</c> descendant, and builds one
    ///     <see cref="PptxPlaceholder"/> per match, applying the <c>type</c>/<c>idx</c> schema
    ///     defaults (<c>"obj"</c>/<c>0</c>) when the corresponding attribute is omitted.
    /// </summary>
    /// <param name="spTreeElement">The <c>&lt;p:spTree&gt;</c> element to walk.</param>
    /// <returns>The placeholder shapes found, in document order.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a <c>&lt;p:ph&gt;</c> element's <c>idx</c> attribute is present but not a
    ///     valid non-negative integer.
    /// </exception>
    internal static IReadOnlyList<PptxPlaceholder> ParsePlaceholderShapes(XElement spTreeElement)
    {
        var placeholders = new List<PptxPlaceholder>();

        foreach (var sp in spTreeElement.Elements(PptxDocument.PresentationNamespace + "sp"))
        {
            if (TryParsePlaceholder(sp) is { } placeholder)
            {
                placeholders.Add(placeholder);
            }
        }

        return placeholders;
    }

    /// <summary>
    ///     Parses a single <c>&lt;p:sp&gt;</c> element's <c>&lt;p:nvSpPr&gt;/&lt;p:nvPr&gt;/
    ///     &lt;p:ph&gt;</c> descendant (if any) into a <see cref="PptxPlaceholder"/>, applying the
    ///     <c>type</c>/<c>idx</c> schema defaults (<c>"obj"</c>/<c>0</c>) when the corresponding
    ///     attribute is omitted. Extracted from <see cref="ParsePlaceholderShapes"/>'s own
    ///     per-<c>&lt;p:sp&gt;</c> inline logic (no behavior change) so the Phase 1e shape-tree
    ///     walker (<see cref="PptxDocument.ParseShapeTree"/>) can reuse the exact same
    ///     placeholder-detection logic without duplicating Phase 1b's placeholder-type/idx
    ///     parsing.
    /// </summary>
    /// <param name="spElement">The <c>&lt;p:sp&gt;</c> element to inspect.</param>
    /// <returns>
    ///     The parsed <see cref="PptxPlaceholder"/>, or <see langword="null"/> when
    ///     <paramref name="spElement"/> has no <c>&lt;p:nvSpPr&gt;/&lt;p:nvPr&gt;/&lt;p:ph&gt;</c>
    ///     descendant (a freeform, non-placeholder shape).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a present <c>&lt;p:ph&gt;</c> element's <c>idx</c> attribute is present but
    ///     not a valid non-negative integer.
    /// </exception>
    internal static PptxPlaceholder? TryParsePlaceholder(XElement spElement)
    {
        var ph = spElement.Element(PptxDocument.PresentationNamespace + "nvSpPr")?
            .Element(PptxDocument.PresentationNamespace + "nvPr")?
            .Element(PptxDocument.PresentationNamespace + "ph");
        if (ph is null)
        {
            return null;
        }

        var declaredType = (string?)ph.Attribute("type");
        var type = declaredType ?? "obj";
        var idx = ParseIdx(ph);

        return new PptxPlaceholder(type, idx, spElement, declaredType);
    }

    /// <summary>
    ///     Parses <c>&lt;p:ph&gt;</c>'s <c>idx</c> attribute, defaulting to <c>0</c> when absent.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the <c>idx</c> attribute is present but not a valid non-negative integer.
    /// </exception>
    private static uint ParseIdx(XElement ph)
    {
        var idxValue = (string?)ph.Attribute("idx");
        if (idxValue is null)
        {
            return 0;
        }

        if (!uint.TryParse(idxValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
        {
            throw new InvalidDataException($"A <p:ph> element has an invalid 'idx' attribute value '{idxValue}'.");
        }

        return idx;
    }
}
