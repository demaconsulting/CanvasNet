package CanvasNetCharts_ChartModelSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetCharts_ChartDocument::*;
    public import CanvasNetCharts_ChartRenderer::*;

    part def ChartModel {
        doc /* Public, immutable, validating chart data model (Chart, ChartSeries, ChartAxis, ChartLegend, ChartTitle, ChartType) and the ChartBuilder fluent construction API, plus (as of Phase 2) a pixel-rendering engine (ChartRenderer). As of this release, its constituent units are ChartDocument and ChartRenderer. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/canvas-net-charts/chart-model.md");
            verificationFiles = ("docs/verification/canvas-net-charts/chart-model.md");
            requirementFiles = ("docs/reqstream/canvas-net-charts/chart-model.yaml");
        }

        part chartDocument : ChartDocument;
        part chartRenderer : ChartRenderer;
    }
}
