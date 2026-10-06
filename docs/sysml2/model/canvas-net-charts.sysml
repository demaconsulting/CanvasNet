package CanvasNet {
    part def CanvasNetChartsSystem {
        doc /* CanvasNetCharts is a chart data-model, pixel-rendering, and OOXML-chart-parsing system, distributed as the separate DemaConsulting.CanvasNet.Charts NuGet package, which depends on the core CanvasNet system's Canvas, Drawing, Geometry, Fonts, and Rendering subsystems only. */

        comment designRef /* Design: docs/design/canvas-net-charts.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-charts.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-charts.yaml */
        comment /* CanvasNetCharts depends on the sibling CanvasNet system's Canvas (Rgba32 color type and the Surface pixel buffer ChartRenderer paints onto and returns), Drawing (path filling/stroking), Geometry (transforms/rectangles), Fonts (TrueType text layout/metrics), and Rendering (the bundled fallback font) subsystems only - modeled below as non-owning `dependency` relationships to those existing subsystem parts (not an ordinary `part` usage, which would incorrectly model them as owned children of CanvasNetChartsSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetCharts is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). As of this release (Phase 3), CanvasNetCharts must never reference CanvasNetPptx, CanvasNetPdf, CanvasNetSvg, or CanvasNetVsdx - see the CanvasNetCharts System Design document's Dependencies section. */

        part chartModel : ChartModel;
        part openXmlChart : OpenXmlChart;

        dependency usesCanvas from CanvasNetChartsSystem to Canvas;
        dependency usesDrawing from CanvasNetChartsSystem to Drawing;
        dependency usesGeometry from CanvasNetChartsSystem to Geometry;
        dependency usesFonts from CanvasNetChartsSystem to Fonts;
        dependency usesRendering from CanvasNetChartsSystem to Rendering;
    }
}
