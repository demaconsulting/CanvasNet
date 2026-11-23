package CanvasNetCharts_OpenXmlChartSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetCharts_OpenXmlChartParser::*;

    part def OpenXmlChart {
        doc /* Format-agnostic OOXML c:chartSpace/c:chart XML parsing, producing a validated ChartModel Chart instance. As of this release, its sole constituent unit is OpenXmlChartParser. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/canvas-net-charts/open-xml-chart.md");
            verificationFiles = ("docs/verification/canvas-net-charts/open-xml-chart.md");
            requirementFiles = ("docs/reqstream/canvas-net-charts/open-xml-chart.yaml");
        }

        part openXmlChartParser : OpenXmlChartParser;
    }
}
