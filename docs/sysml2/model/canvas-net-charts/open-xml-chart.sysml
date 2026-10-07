package CanvasNet {
    part def OpenXmlChart {
        doc /* Format-agnostic OOXML c:chartSpace/c:chart XML parsing, producing a validated ChartModel Chart instance. As of this release, its sole constituent unit is OpenXmlChartParser. */

        comment designRef /* Design: docs/design/canvas-net-charts/open-xml-chart.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-charts/open-xml-chart.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-charts/open-xml-chart.yaml */

        part openXmlChartParser : OpenXmlChartParser;
    }
}
