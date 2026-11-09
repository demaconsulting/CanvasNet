package CanvasNet {
    part def DagreNesting {
        doc /* The internal static DagreNesting class, the port of dagre's nesting-graph.js, parent-dummy-chains.js, and add-border-segments.js. Run adds the nesting graph of Sander's method before ranking (a root joined to every top-level node, a top and a bottom border node for every cluster joined to its members, every edge length multiplied by the rank factor 2 * height + 1); Cleanup removes the root and the nesting edges after ranking; ParentDummyChains moves each dummy node of a long edge into the cluster its edge crosses at the dummy's rank (along the cluster path through the lowest common ancestor); AddBorderSegments gives every cluster a left and a right border node at each of its ranks, joined from rank to rank. Where dagre would loop forever on a broken cluster path, the port throws InvalidOperationException (mermaid-render-failed through DagreLayout); the recursions check the execution stack. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreNesting.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreNestingTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre/dagre-nesting.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre/dagre-nesting.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre/dagre-nesting.yaml */
    }
}
