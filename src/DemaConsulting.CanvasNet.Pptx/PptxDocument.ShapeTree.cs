using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore grpsp cxnsp sptree pptx xfrm

/// <summary>
///     Implements the <see cref="PptxDocument"/> shape-tree walker (Phase 1e, extended by the
///     Phase 2 Follow-Up connector-rendering work and Phase 4's chart-graphic-frame dispatch):
///     recursively parses a <c>&lt;p:spTree&gt;</c>
///     (or a <c>&lt;p:grpSp&gt;</c>, whose own shape children are direct siblings of its
///     <c>&lt;p:nvGrpSpPr&gt;</c>/<c>&lt;p:grpSpPr&gt;</c> rather than a nested
///     <c>&lt;p:spTree&gt;</c>, per ECMA-376's <c>CT_GroupShape</c> content model) into a
///     <see cref="PptxShapeTreeNode"/> hierarchy, dispatching each recognized child element kind to
///     its own node type and silently skipping anything else.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     The maximum number of nested <c>&lt;p:grpSp&gt;</c> levels <see cref="ParseShapeTree"/>
    ///     (and, mirroring it at render time, <c>PptxDocument.Render.cs</c>'s own
    ///     <c>RenderNode</c>) will recurse into before failing closed, bounding the worst-case
    ///     call-stack depth a single, attacker-controlled shape tree can drive. 100 levels is far
    ///     beyond any legitimate PowerPoint-authored group nesting (real decks rarely nest more
    ///     than a handful of groups deep; PowerPoint's own UI makes deeper nesting increasingly
    ///     impractical to construct by hand), while still leaving comfortable headroom below the
    ///     default platform stack size - unlike the ZIP-entry-size (<see cref="MaxPartBytes"/>),
    ///     XML-character-count (<see cref="MaxPartCharacters"/>), and chart cached-point-count
    ///     (<c>OpenXmlChartParser.MaxCachedPointCount</c>) caps, group-shape nesting depth has no
    ///     bound of its own: a crafted <c>.pptx</c> easily fits thousands of nested
    ///     <c>&lt;p:grpSp&gt;</c> elements within the existing ~2,000,000-character XML-part cap,
    ///     which - left unchecked - drives a <see cref="StackOverflowException"/> that cannot be
    ///     caught, crashing the process rather than a clean, fail-closed rejection.
    /// </summary>
    private const int MaxGroupShapeNestingDepth = 100;

    /// <summary>
    ///     Walks <paramref name="spTreeOrGroupElement"/>'s immediate children in document order,
    ///     dispatching each recognized shape element to its own <see cref="PptxShapeTreeNode"/>:
    ///     <c>&lt;p:sp&gt;</c> &#8594; <see cref="PptxSpShapeNode"/> (via
    ///     <see cref="PptxPlaceholderParser.TryParsePlaceholder"/>), <c>&lt;p:pic&gt;</c> &#8594;
    ///     <see cref="PptxPictureShapeNode"/>, <c>&lt;p:graphicFrame&gt;</c> &#8594;
    ///     <see cref="PptxGraphicFrameShapeNode"/> (eagerly parsing its <c>&lt;a:graphicData&gt;</c>'s
    ///     own <c>uri</c> attribute: a chart kind, when <paramref name="resolveChartPart"/> is
    ///     non-null, is dispatched to <see cref="ParseChart"/> (Phase 4); every other kind,
    ///     including a chart when <paramref name="resolveChartPart"/> is <see langword="null"/>,
    ///     is dispatched to the pre-existing <see cref="ParseTable"/>, which itself still throws
    ///     <see cref="PptxUnsupportedFeatureException"/> for any non-table kind), and
    ///     <c>&lt;p:grpSp&gt;</c> &#8594;
    ///     <see cref="PptxGroupShapeNode"/> (resolving its child transform via
    ///     <see cref="ResolveGroupChildTransform"/> and recursing into the same group element for
    ///     its own children, since a group's children are direct siblings of its own
    ///     <c>&lt;p:nvGrpSpPr&gt;</c>/<c>&lt;p:grpSpPr&gt;</c>, not wrapped in a nested
    ///     <c>&lt;p:spTree&gt;</c>), and <c>&lt;p:cxnSp&gt;</c> &#8594;
    ///     <see cref="PptxConnectorShapeNode"/> (Phase 2 Follow-Up: Connector Shape Rendering).
    ///     As of the companion planning report's parse-time containment fix,
    ///     <paramref name="containUnsupportedGraphicFrames"/> gates whether a
    ///     <c>&lt;p:graphicFrame&gt;</c> declaring a recognized-but-unsupported (non-table,
    ///     non-chart, or recognized-but-unsupported chart) kind
    ///     is skipped (master/layout-owned shape trees) or still propagates (a slide's own shape
    ///     tree), mirroring <c>PptxDocument.Render.cs</c>'s own <c>RenderNode</c>
    ///     <c>skipPlaceholderShapes</c> render-time convention, extended to parse time.
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
    /// <param name="containUnsupportedGraphicFrames">
    ///     When <see langword="true"/>, a <c>&lt;p:graphicFrame&gt;</c> whose
    ///     <c>&lt;a:graphicData&gt;</c> declares a recognized-but-unsupported (non-table) kind -
    ///     a chart, SmartArt, OLE object, etc. - has its <see cref="PptxUnsupportedFeatureException"/>
    ///     caught and that node silently skipped (its other siblings still parse normally),
    ///     mirroring <c>PptxDocument.Render.cs</c>'s own <c>RenderNode</c>
    ///     <c>skipPlaceholderShapes</c>-gated render-time convention. When <see langword="false"/>
    ///     (the default), the exception propagates unchanged, aborting the rest of this shape
    ///     tree's parse. <see cref="GetSlide"/> keeps the default <see langword="false"/> (a
    ///     slide's own unsupported graphic frame must still hard-fail
    ///     <see cref="Render(int, int, int, PptxRenderOptions?)"/> - see
    ///     the three protected <c>PptxFixturesCorpusTests.cs</c> slide-level-throw tests);
    ///     <see cref="GetLayout"/>/<see cref="GetMaster"/> pass <see langword="true"/>, since each
    ///     is parsed once and cached, shared by every slide using that layout/master - an
    ///     uncontained exception there would otherwise abort every slide sharing it. Threaded
    ///     through the recursive <c>&lt;p:grpSp&gt;</c> self-call unchanged.
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
    ///     element, or (via <see cref="ParseTable"/>, <see cref="ParseChart"/>, or
    ///     <see cref="ResolveGroupChildTransform"/>) when a <c>&lt;p:graphicFrame&gt;</c>'s table
    ///     or chart, or a <c>&lt;p:grpSp&gt;</c>'s own transform, is otherwise malformed; or when a
    ///     <c>&lt;p:grpSp&gt;</c> is nested more than <see cref="MaxGroupShapeNestingDepth"/>
    ///     levels deep.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown (via <see cref="ParseTable"/> or, for a chart, via <see cref="ParseChart"/>)
    ///     when a <c>&lt;p:graphicFrame&gt;</c>'s <c>&lt;a:graphicData&gt;</c> declares a
    ///     recognized-but-unsupported kind (a non-table kind for <see cref="ParseTable"/>; a
    ///     recognized-but-unsupported chart kind, such as a radar chart, for
    ///     <see cref="ParseChart"/>) and <paramref name="containUnsupportedGraphicFrames"/> is
    ///     <see langword="false"/> (the default). When <paramref name="containUnsupportedGraphicFrames"/>
    ///     is <see langword="true"/>, this exception is instead caught and the offending node
    ///     silently skipped.
    /// </exception>
    /// <param name="resolveBlipImage">
    ///     Threaded unchanged into <see cref="ParseTable"/> and the recursive
    ///     <c>&lt;p:grpSp&gt;</c> self-call - see <see cref="ResolveFill"/>'s matching parameter.
    /// </param>
    /// <param name="resolveChartPart">
    ///     Resolves a <c>&lt;c:chart&gt;</c> element's own <c>r:id</c> relationship, scoped to the
    ///     owning slide/layout/master part, to that chart part's root <see cref="XElement"/> -
    ///     see <see cref="ParseChart"/>'s matching parameter. When a <c>&lt;p:graphicFrame&gt;</c>'s
    ///     <c>&lt;a:graphicData&gt;</c> declares a chart (its <c>uri</c> attribute ends in
    ///     <c>"/chart"</c>) and this parameter is non-null, <see cref="ParseChart"/> is called
    ///     instead of <see cref="ParseTable"/>. Defaults to <see langword="null"/> (Phase 4's own
    ///     graceful-degradation safety net): a <see langword="null"/> resolver falls back to
    ///     today's pre-existing <see cref="ParseTable"/> call for every <c>&lt;p:graphicFrame&gt;</c>,
    ///     preserving every pre-existing call site's exact behavior unchanged. Threaded through
    ///     the recursive <c>&lt;p:grpSp&gt;</c> self-call unchanged.
    /// </param>
    /// <param name="depth">
    ///     The number of <c>&lt;p:grpSp&gt;</c> levels already entered to reach
    ///     <paramref name="spTreeOrGroupElement"/> (<c>0</c> for the slide/layout/master's own
    ///     top-level <c>&lt;p:spTree&gt;</c>, the default for every pre-existing call site).
    ///     Incremented on the recursive <c>&lt;p:grpSp&gt;</c> self-call; once it would exceed
    ///     <see cref="MaxGroupShapeNestingDepth"/>, that nested group's own children are not
    ///     walked and an <see cref="InvalidDataException"/> is thrown instead - see
    ///     <see cref="MaxGroupShapeNestingDepth"/>'s own remarks for why this bound exists.
    /// </param>
    internal static IReadOnlyList<PptxShapeTreeNode> ParseShapeTree(
        XElement spTreeOrGroupElement, Func<PptxTheme> themeResolver, Func<string, XElement?>? tableStyleResolver = null,
        Func<PptxColorMap>? colorMapResolver = null, bool containUnsupportedGraphicFrames = false,
        Func<XElement, Surface>? resolveBlipImage = null, Func<string, XElement>? resolveChartPart = null,
        int depth = 0)
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
                try
                {
                    var graphicData = child.Element(DrawingNamespace + "graphic")?.Element(DrawingNamespace + "graphicData");
                    var uri = (string?)graphicData?.Attribute("uri") ?? string.Empty;
                    if (resolveChartPart is not null && graphicData is not null && uri.EndsWith("/chart", StringComparison.Ordinal))
                    {
                        nodes.Add(new PptxGraphicFrameShapeNode(child, Table: null, Chart: ParseChart(graphicData, resolveChartPart)));
                    }
                    else
                    {
                        nodes.Add(new PptxGraphicFrameShapeNode(
                            child, ParseTable(child, themeResolver(), colorMapResolver?.Invoke(), tableStyleResolver, resolveBlipImage), Chart: null));
                    }
                }
                catch (PptxUnsupportedFeatureException) when (containUnsupportedGraphicFrames)
                {
                    // A master/layout-owned graphic frame of an unsupported (non-table, non-chart,
                    // or recognized-but-unsupported chart) kind is skipped rather than aborting
                    // this entire cached shape tree - see this parameter's own XmlDoc remarks.
                }
            }
            else if (child.Name == PresentationNamespace + "grpSp")
            {
                if (depth >= MaxGroupShapeNestingDepth)
                {
                    throw new InvalidDataException(
                        $"A <p:grpSp> element is nested beyond the maximum supported depth of {MaxGroupShapeNestingDepth} levels.");
                }

                var groupXfrm = child.Element(PresentationNamespace + "grpSpPr")?.Element(DrawingNamespace + "xfrm") ??
                    throw new InvalidDataException("A <p:grpSp> element has no <p:grpSpPr>/<a:xfrm> element.");
                var childTransform = ResolveGroupChildTransform(groupXfrm);
                var children = ParseShapeTree(
                    child, themeResolver, tableStyleResolver, colorMapResolver, containUnsupportedGraphicFrames,
                    resolveBlipImage, resolveChartPart, depth + 1);
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
