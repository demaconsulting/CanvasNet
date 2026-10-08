namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx stencil stencils glueable Visio NURBS

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Vsdx"/> namespace provides read access to
///     Microsoft Visio (<c>.vsdx</c>) diagram documents via <see cref="VsdxDocument"/>: the OPC
///     (Open Packaging Conventions) package layer, the page/master/stencil structural model and
///     StyleSheet chain, VisioML shape geometry and affine-transform resolution, Master/
///     MasterShape cell-and-geometry-row inheritance, text run layout and glyph-ink rendering,
///     connector/glue-point routing, theme/color resolution, and a public, per-page rendering
///     API. This namespace is distributed as the separate <c>DemaConsulting.CanvasNet.Vsdx</c>
///     NuGet package, which references only the core <c>DemaConsulting.CanvasNet</c> package.
/// </summary>
/// <remarks>
///     <para>
///         This feature was delivered as a single pull request describing its complete, final
///         feature set, followed by three further real-world-corpus bug-fix rounds (Milestones
///         10-12): the OPC package layer (opening a <c>.vsdx</c> file as a ZIP archive, resolving
///         <c>[Content_Types].xml</c> and the full <c>_rels/.rels</c> → <c>visio/document.xml</c>
///         → <c>masters.xml</c>/<c>pages.xml</c>/an optional <c>theme1.xml</c> relationship chain,
///         exclusively through relationship references, never by filename convention); the
///         page/master/stencil structural model and <c>&lt;StyleSheets&gt;</c> chain; a page's
///         full shape tree, resolved to arbitrary group-nesting depth (Master/MasterShape
///         cell-and-geometry-row inheritance, the StyleSheet chain for
///         <c>LineStyle</c>/<c>FillStyle</c>/<c>TextStyle</c>, shape geometry and the
///         shape-local-to-page affine transform, text run layout and glyph-ink rendering, and
///         connector/glue-point routing); and the public, per-page
///         <see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/>/
///         <see cref="VsdxDocument.Render(int, int, VsdxRenderOptions?)"/> rendering API. See
///         <see cref="VsdxDocument"/>'s own remarks for the complete, current scope boundary.
///     </para>
///     <para>
///         A small set of gaps remain documented, rather than fixed, because no in-scope fixture
///         exercises them (see <c>canvas-net-vsdx.md</c>'s own Design Constraints section for the
///         full evidence trail): decoding/rasterizing an embedded-image/<c>Foreign</c> shape's own
///         binary payload is deferred (the shape itself still resolves and renders its own
///         geometry/paint/text normally, since no distinct <c>Type</c>-based handling exists for
///         it); a <c>Themed</c> color cell that depends on a shape's own Quick-Style variation
///         index (rather than a simple scheme lookup) falls back to a neutral default color; a
///         non-solid <c>FillPattern</c> value (built-in hatch/gradient combinations) degrades to
///         the same solid-fill treatment as <c>FillPattern="1"</c> rather than being approximated;
///         and the full <c>BeginArrow</c>/<c>EndArrow</c> style-index table and several
///         documented-but-unobserved geometry row types (<c>NURBSTo</c>, <c>InfiniteLine</c>,
///         <c>RelCubBezTo</c>, <c>SplineStart</c>/<c>SplineKnot</c>, <c>PolylineTo</c>,
///         <c>Ellipse</c>) are not implemented, each degrading tolerantly rather than throwing.
///         The real-world-corpus bug-fix rounds (Milestones 10-12) additionally investigated,
///         and left as documented known limitations rather than fixed (no safe narrow fix was
///         found that would not regress other correctly-rendered shapes in the corpus):
///         <c>FillPattern="0"</c> inconsistently suppressing fill in a few shapes, one
///         unsupported gradient <c>FillPattern</c> value (<c>"36"</c>) affecting a single
///         rack-diagram frame, and minor UML text/border padding differences - see
///         <c>docs/verification/canvas-net-vsdx/vsdx-document.md</c> for the full evidence
///         trail of each.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
