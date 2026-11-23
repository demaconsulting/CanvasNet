package CanvasNetCharts_OpenXmlChartParser {
    private import SoftwareItemMetadata::*;

    part def OpenXmlChartParser {
        doc /* The OpenXmlChartParser static class and its ChartUnsupportedFeatureException: parses a raw c:chartSpace/c:chart XML element into a validated Chart, reading only cached values (c:numCache/c:strCache) and rejecting an unsupported chart kind or data shape. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Charts/OpenXml/OpenXmlChartParser.cs",
                "src/DemaConsulting.CanvasNet.Charts/OpenXml/ChartUnsupportedFeatureException.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Charts.Tests/OpenXmlChartParserTests.cs");
            designFiles = ("docs/design/canvas-net-charts/open-xml-chart/open-xml-chart-parser.md");
            verificationFiles = ("docs/verification/canvas-net-charts/open-xml-chart/open-xml-chart-parser.md");
            requirementFiles = ("docs/reqstream/canvas-net-charts/open-xml-chart/open-xml-chart-parser.yaml");
        }
    }
}
