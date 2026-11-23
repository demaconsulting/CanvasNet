package CanvasNetLayout_DagreNesting {
    private import SoftwareItemMetadata::*;

    part def DagreNesting {
        doc /* The internal static DagreNesting class, the port of dagre's nesting-graph.js, parent-dummy-chains.js, and add-border-segments.js. Run adds the nesting graph of Sander's method before ranking (a root joined to every top-level node, a top and a bottom border node for every cluster joined to its members, every edge length multiplied by the rank factor 2 * height + 1); Cleanup removes the root and the nesting edges after ranking; ParentDummyChains moves each dummy node of a long edge into the cluster its edge crosses at the dummy's rank (along the cluster path through the lowest common ancestor); AddBorderSegments gives every cluster a left and a right border node at each of its ranks, joined from rank to rank. Where dagre would loop forever on a broken cluster path, the port throws InvalidOperationException (the consumer's render failure through DagreLayout, mermaid-render-failed for Mermaid); the recursions check the execution stack. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Layout/Dagre/DagreNesting.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreNestingTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/dagre-nesting.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/dagre-nesting.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/dagre-nesting.yaml");
        }
    }
}
