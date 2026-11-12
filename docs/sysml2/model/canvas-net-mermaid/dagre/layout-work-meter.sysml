package CanvasNet {
    part def LayoutWorkMeter {
        doc /* The internal sealed LayoutWorkMeter class: the work budget of the layout of one diagram. Every iterative layout stage charges its steps with Charge, which first checks the cancellation token of the call (OperationCanceledException) and fails with a MermaidRenderException with the code mermaid-limit-exceeded, located at the construct the meter was made for and naming MermaidLimits.MaxLayoutWork with a hint, when the total would pass MaxWork (50,000,000 by default; at least 1). The charges are deterministic (the complete acyclic graph on 30 nodes takes 2,955,593 steps). Failure creates the located mermaid-render-failed of a layout dagre cannot complete, and LimitExceeded the located limit failure, for DagreLayout to throw. UniqueId reproduces lodash's uniqueId with one counter shared by every prefix, counted per meter (per diagram) rather than per page, so a diagram always gets the same dummy names. One meter serves one diagram on one thread and is shared by every layout of the diagram and by the cluster code of the Rendering subsystem; the pie and gantt renderers, the MindmapRenderer, and the CoseLayout of the Cose subsystem charge their work to the meter of their render too. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Dagre/LayoutWorkMeter.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/LayoutWorkMeterTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre/layout-work-meter.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre/layout-work-meter.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre/layout-work-meter.yaml */
    }
}
