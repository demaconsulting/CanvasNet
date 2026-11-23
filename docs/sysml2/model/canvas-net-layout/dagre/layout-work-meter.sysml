package CanvasNetLayout_LayoutWorkMeter {
    private import SoftwareItemMetadata::*;

    part def LayoutWorkMeter {
        doc /* The internal abstract LayoutWorkMeter class: the work budget of the layout of one diagram. Every iterative layout stage charges its steps with Charge, which first checks the cancellation token of the call (OperationCanceledException) and throws the limit failure that the consumer's LimitExceeded creates when the total would pass MaxWork (at least 1; Mermaid gives MermaidLimits.MaxLayoutWork, 50,000,000 by default, and its MermaidLayoutWorkMeter of the MermaidDocument unit creates a located MermaidRenderException with the code mermaid-limit-exceeded naming the limit with a hint). The charges are deterministic (the complete acyclic graph on 30 nodes takes 2,955,593 steps). The abstract Failure creates the consumer's render failure of a layout dagre cannot complete (mermaid-render-failed for Mermaid), LimitExceeded the consumer's limit failure, for DagreLayout to throw, and IsFailure recognizes the consumer's own failures, which DagreLayout lets pass unchanged; the layout package therefore needs no consumer's exception types. UniqueId reproduces lodash's uniqueId with one counter shared by every prefix, counted per meter (per diagram) rather than per page, so a diagram always gets the same dummy names. One meter serves one diagram on one thread and is shared by every layout of the diagram; in Mermaid the cluster code of the Rendering subsystem, the pie and gantt renderers, the MindmapRenderer, the CoseLayout of the Cose subsystem, the PacketRenderer with the row split of the PacketDiagram, and the GitGraphRenderer charge their work to the meter of their render too. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Layout/Dagre/LayoutWorkMeter.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/LayoutWorkMeterTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/layout-work-meter.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/layout-work-meter.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/layout-work-meter.yaml");
        }
    }
}
