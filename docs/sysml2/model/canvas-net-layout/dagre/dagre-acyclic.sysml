package CanvasNetLayout_DagreAcyclic {
    private import SoftwareItemMetadata::*;

    part def DagreAcyclic {
        doc /* The internal static DagreAcyclic class, the port of dagre's acyclic.js, greedy-fas.js, and data/list.js. Run reverses a feedback arc set so that the graph can be ranked: by default the edges that a depth-first search finds going back to a node on its stack, and with acyclicer greedy the set of the Eades-Lin-Smyth greedy heuristic (sinks and sources removed first, then the node of greatest out-minus-in weight, its in-edges collected, every parallel edge of a collected pair returned). A reversed edge gets a rev-prefixed name from the meter and keeps its original name; Undo restores every reversed edge with its original direction, name, and label. Negative and fractional weights are not supported (dagre indexes its buckets with them). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.Layout/Dagre/DagreAcyclic.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreAcyclicTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/dagre-acyclic.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/dagre-acyclic.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/dagre-acyclic.yaml");
        }
    }
}
