package CanvasNet {
    part def ChartModel {
        doc /* Public, immutable, validating chart data model (Chart, ChartSeries, ChartAxis, ChartLegend, ChartTitle, ChartType) and the ChartBuilder fluent construction API. As of this release (Phase 1), its single constituent unit is ChartDocument. */

        comment designRef /* Design: docs/design/canvas-net-charts/chart-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-charts/chart-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-charts/chart-model.yaml */

        part chartDocument : ChartDocument;
    }
}
