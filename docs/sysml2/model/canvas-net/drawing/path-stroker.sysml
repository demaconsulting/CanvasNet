package CanvasNet_PathStroker {
    private import SoftwareItemMetadata::*;

    part def PathStroker {
        doc /* Public static Stroke entry point converting a centerline Path plus StrokeStyle into closed outline Path geometry ready for nonzero-rule filling. Also covers the supporting public StrokeStyle/LineCap/LineJoin types and the internal StrokePathFlattener/DashSplitter/StrokeOutliner helpers inline; none has independent externally visible behavior of its own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Drawing/LineCap.cs",
                "src/DemaConsulting.CanvasNet/Drawing/LineJoin.cs",
                "src/DemaConsulting.CanvasNet/Drawing/StrokeStyle.cs",
                "src/DemaConsulting.CanvasNet/Drawing/StrokePathFlattener.cs",
                "src/DemaConsulting.CanvasNet/Drawing/DashSplitter.cs",
                "src/DemaConsulting.CanvasNet/Drawing/StrokeOutliner.cs",
                "src/DemaConsulting.CanvasNet/Drawing/PathStroker.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Drawing/StrokeStyleTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/StrokePathFlattenerTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/DashSplitterTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/StrokeOutlinerTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/PathStrokerTests.cs"
            );
            designFiles = ("docs/design/canvas-net/drawing/path-stroker.md");
            verificationFiles = ("docs/verification/canvas-net/drawing/path-stroker.md");
            requirementFiles = ("docs/reqstream/canvas-net/drawing/path-stroker.yaml");
        }
    }
}
