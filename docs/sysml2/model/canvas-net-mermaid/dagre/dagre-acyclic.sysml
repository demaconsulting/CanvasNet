package CanvasNet {
    part def DagreAcyclic {
        doc /* The internal static DagreAcyclic class, the port of dagre's acyclic.js, greedy-fas.js, and data/list.js. Run reverses a feedback arc set so that the graph can be ranked: by default the edges that a depth-first search finds going back to a node on its stack, and with acyclicer greedy the set of the Eades-Lin-Smyth greedy heuristic (sinks and sources removed first, then the node of greatest out-minus-in weight, its in-edges collected, every parallel edge of a collected pair returned). A reversed edge gets a rev-prefixed name from the meter and keeps its original name; Undo restores every reversed edge with its original direction, name, and label. Negative and fractional weights are not supported (dagre indexes its buckets with them). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreAcyclic.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreAcyclicTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre/dagre-acyclic.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre/dagre-acyclic.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre/dagre-acyclic.yaml */
    }
}
