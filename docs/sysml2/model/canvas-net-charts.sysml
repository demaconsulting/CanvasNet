package CanvasNet {
    part def CanvasNetChartsSystem {
        doc /* CanvasNetCharts is a chart data-model and (in later phases) rendering/OOXML-interchange system, distributed as the separate DemaConsulting.CanvasNet.Charts NuGet package, which depends on the core CanvasNet system's Canvas subsystem only. */

        comment designRef /* Design: docs/design/canvas-net-charts.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-charts.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-charts.yaml */
        comment /* CanvasNetCharts depends on the sibling CanvasNet system's Canvas subsystem only (for the Rgba32 color type used by series/point/palette colors) - modeled below as a non-owning `dependency` relationship to that existing subsystem part def (not an ordinary `part` usage, which would incorrectly model it as an owned child of CanvasNetChartsSystem), since both systems live in this same git repository (though each is distributed as its own separate NuGet package - CanvasNetCharts is not folded into the CanvasNet package) rather than being third-party (OTS) or Shared Package dependencies (both of which are reserved for a package produced by a different repository). As of this release (Phase 1), CanvasNetCharts must never reference CanvasNetPptx, CanvasNetPdf, CanvasNetSvg, or CanvasNetVsdx - see the CanvasNetCharts System Design document's Dependencies section. */

        part chartModel : ChartModel;

        dependency usesCanvas from CanvasNetChartsSystem to Canvas;
    }
}
