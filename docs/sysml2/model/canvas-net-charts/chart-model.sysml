package CanvasNet {
    part def ChartModel {
        doc /* Public, immutable, validating chart data model (Chart, ChartSeries, ChartAxis, ChartLegend, ChartTitle, ChartType) and the ChartBuilder fluent construction API, plus (as of Phase 2) a pixel-rendering engine (ChartRenderer). As of this release, its constituent units are ChartDocument and ChartRenderer. */

        comment designRef /* Design: docs/design/canvas-net-charts/chart-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-charts/chart-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-charts/chart-model.yaml */

        part chartDocument : ChartDocument;
        part chartRenderer : ChartRenderer;
    }
}
