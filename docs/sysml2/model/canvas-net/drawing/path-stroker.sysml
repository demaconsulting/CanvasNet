package CanvasNet {
    part def PathStroker {
        doc /* Public static Stroke entry point converting a centerline Path plus StrokeStyle into closed outline Path geometry ready for nonzero-rule filling. Also covers the supporting public StrokeStyle/LineCap/LineJoin types and the internal StrokePathFlattener/DashSplitter/StrokeOutliner helpers inline; none has independent externally visible behavior of its own. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Drawing/LineCap.cs, src/DemaConsulting.CanvasNet/Drawing/LineJoin.cs, src/DemaConsulting.CanvasNet/Drawing/StrokeStyle.cs, src/DemaConsulting.CanvasNet/Drawing/StrokePathFlattener.cs, src/DemaConsulting.CanvasNet/Drawing/DashSplitter.cs, src/DemaConsulting.CanvasNet/Drawing/StrokeOutliner.cs, src/DemaConsulting.CanvasNet/Drawing/PathStroker.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Drawing/StrokeStyleTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/StrokePathFlattenerTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/DashSplitterTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/StrokeOutlinerTests.cs, test/DemaConsulting.CanvasNet.Tests/Drawing/PathStrokerTests.cs */
        comment designRef /* Design: docs/design/canvas-net/drawing/path-stroker.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/drawing/path-stroker.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/drawing/path-stroker.yaml */
    }
}
