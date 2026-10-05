using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore grpsp cxnsp sptree pptx xfrm

/// <summary>
///     Implements the <see cref="PptxDocument"/> shape-tree walker (Phase 1e, extended by the
///     Phase 2 Follow-Up connector-rendering work): recursively parses a <c>&lt;p:spTree&gt;</c>
///     (or a <c>&lt;p:grpSp&gt;</c>, whose own shape children are direct siblings of its
///     <c>&lt;p:nvGrpSpPr&gt;</c>/<c>&lt;p:grpSpPr&gt;</c> rather than a nested
///     <c>&lt;p:spTree&gt;</c>, per ECMA-376's <c>CT_GroupShape</c> content model) into a
///     <see cref="PptxShapeTreeNode"/> hierarchy, dispatching each recognized child element kind to
///     its own node type and silently skipping anything else.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Walks <paramref name="spTreeOrGroupElement"/>'s immediate children in document order,
    ///     dispatching each recognized shape element to its own <see cref="PptxShapeTreeNode"/>:
    ///     <c>&lt;p:sp&gt;</c> &#8594; <see cref="PptxSpShapeNode"/> (via
    ///     <see cref="PptxPlaceholderParser.TryParsePlaceholder"/>), <c>&lt;p:pic&gt;</c> &#8594;
    ///     <see cref="PptxPictureShapeNode"/>, <c>&lt;p:graphicFrame&gt;</c> &#8594;
    ///     <see cref="PptxGraphicFrameShapeNode"/> (eagerly parsing its table via
    ///     <see cref="ParseTable"/>), and <c>&lt;p:grpSp&gt;</c> &#8594;
    ///     <see cref="PptxGroupShapeNode"/> (resolving its child transform via
    ///     <see cref="ResolveGroupChildTransform"/> and recursing into the same group element for
    ///     its own children, since a group's children are direct siblings of its own
    ///     <c>&lt;p:nvGrpSpPr&gt;</c>/<c>&lt;p:grpSpPr&gt;</c>, not wrapped in a nested
    ///     <c>&lt;p:spTree&gt;</c>), and <c>&lt;p:cxnSp&gt;</c> &#8594;
    ///     <see cref="PptxConnectorShapeNode"/> (Phase 2 Follow-Up: Connector Shape Rendering).
    /// </summary>
    /// <param name="spTreeOrGroupElement">
    ///     The <c>&lt;p:spTree&gt;</c> (slide-level) or <c>&lt;p:grpSp&gt;</c> (nested-group-level)
    ///     element whose immediate children are walked.
    /// </param>
    /// <param name="themeResolver">
    ///     Lazily invoked to resolve the slide's theme (see <see cref="GetTheme"/>) only when a
    ///     <c>&lt;p:graphicFrame&gt;</c> table is actually encountered, then threaded through to
    ///     <see cref="ParseTable"/> so its cells' fills can be resolved against theme-referenced
    ///     colors/fonts at shape-tree-build time rather than deferring theme resolution to paint
    ///     time. Deferred (rather than eagerly resolved by the caller) so slides with no tables at
    ///     all - the overwhelming majority - never require their layout/master/theme relationship
    ///     chain to be walked.
    /// </param>
    /// <param name="tableStyleResolver">
    ///     Lazily invoked (via <see cref="ParseTable"/>'s own matching parameter) only when a
    ///     <c>&lt;p:graphicFrame&gt;</c> table actually declares a non-empty
    ///     <c>&lt;a:tableStyleId&gt;</c>, to resolve it against <c>ppt/tableStyles.xml</c> - see
    ///     <see cref="TryResolveTableStyle"/>. Threaded through the recursive <c>&lt;p:grpSp&gt;</c>
    ///     self-call unchanged. Defaults to <see langword="null"/> ("no table style available") so
    ///     every pre-existing call site keeps compiling and behaving unchanged.
    /// </param>
    /// <param name="colorMapResolver">
    ///     Lazily invoked (via <see cref="ParseTable"/>'s own <c>colorMap</c> parameter) only when
    ///     a <c>&lt;p:graphicFrame&gt;</c> table is actually encountered, to resolve the effective
    ///     <see cref="PptxColorMap"/> a cell's own fill/border scheme-color token should be
    ///     resolved against - see <see cref="ParseTable"/>'s own <c>colorMap</c> parameter and its
    ///     documented residual (master/layout-owned-table-only) limitation. Threaded through the
    ///     recursive <c>&lt;p:grpSp&gt;</c> self-call unchanged, mirroring
    ///     <paramref name="tableStyleResolver"/>'s own threading pattern. Defaults to
    ///     <see langword="null"/> (resolving to <see cref="PptxColorMap.Default"/>) so every
    ///     pre-existing call site keeps compiling and behaving unchanged.
    /// </param>
    /// <returns>
    ///     The recognized shape-tree nodes, in document order. Unrecognized element kinds (for
    ///     example <c>&lt;p:nvGrpSpPr&gt;</c>, <c>&lt;p:grpSpPr&gt;</c>, or
    ///     <c>&lt;p:contentPart&gt;</c>) are silently skipped - a tree-walk tolerance, not a
    ///     fail-closed feature rejection, matching the existing Phase 1b precedent of silently
    ///     excluding non-placeholder shapes from <see cref="PptxSlide.Placeholders"/>.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a <c>&lt;p:grpSp&gt;</c> element has no <c>&lt;p:grpSpPr&gt;/&lt;a:xfrm&gt;</c>
    ///     element, or (via <see cref="ParseTable"/> or <see cref="ResolveGroupChildTransform"/>)
    ///     when a <c>&lt;p:graphicFrame&gt;</c>'s table or a <c>&lt;p:grpSp&gt;</c>'s own transform
    ///     is otherwise malformed.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown (via <see cref="ParseTable"/>) when a <c>&lt;p:graphicFrame&gt;</c>'s
    ///     <c>&lt;a:graphicData&gt;</c> declares a recognized-but-unsupported (non-table) kind.
    /// </exception>
    internal static IReadOnlyList<PptxShapeTreeNode> ParseShapeTree(
        XElement spTreeOrGroupElement, Func<PptxTheme> themeResolver, Func<string, XElement?>? tableStyleResolver = null,
        Func<PptxColorMap>? colorMapResolver = null)
    {
        var nodes = new List<PptxShapeTreeNode>();

        foreach (var child in spTreeOrGroupElement.Elements())
        {
            if (child.Name == PresentationNamespace + "sp")
            {
                nodes.Add(new PptxSpShapeNode(child, PptxPlaceholderParser.TryParsePlaceholder(child)));
            }
            else if (child.Name == PresentationNamespace + "pic")
            {
                nodes.Add(new PptxPictureShapeNode(child));
            }
            else if (child.Name == PresentationNamespace + "graphicFrame")
            {
                nodes.Add(new PptxGraphicFrameShapeNode(
                    child, ParseTable(child, themeResolver(), colorMapResolver?.Invoke(), tableStyleResolver)));
            }
            else if (child.Name == PresentationNamespace + "grpSp")
            {
                var groupXfrm = child.Element(PresentationNamespace + "grpSpPr")?.Element(DrawingNamespace + "xfrm") ??
                    throw new InvalidDataException("A <p:grpSp> element has no <p:grpSpPr>/<a:xfrm> element.");
                var childTransform = ResolveGroupChildTransform(groupXfrm);
                var children = ParseShapeTree(child, themeResolver, tableStyleResolver, colorMapResolver);
                nodes.Add(new PptxGroupShapeNode(child, childTransform, children));
            }
            else if (child.Name == PresentationNamespace + "cxnSp")
            {
                nodes.Add(new PptxConnectorShapeNode(child));
            }

            // Any other element kind (<p:nvGrpSpPr>, <p:grpSpPr>, <p:contentPart>, or anything
            // unrecognized) is silently skipped.
        }

        return nodes;
    }
}
