package CanvasNetLayout_LayoutGraphlibSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetLayout_Graph::*;
    public import CanvasNetLayout_GraphAlgorithms::*;

    part def LayoutGraphlib {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Layout.Graphlib namespace that is the graph data structure of the shared layout: a port of graphlib as dagre-d3-es bundles it for the pinned Mermaid release. The Graph unit is the directed or undirected, simple or multi, flat or compound graph with labels on the graph, the nodes, and the edges (Graph, with the edge identity GraphEdge), whose nodes, children, predecessors, and successors are kept in the own-key order of a JavaScript object (ObjectKeyMap: array-index names first in ascending numeric order, then the other names in insertion order, a name removed and added again moving to the end) and whose edges are kept in insertion order, because every dagre stage breaks ties by that order. The GraphAlgorithms unit holds the graphlib traversals preorder and postorder on the same key order, which the dagre ranker uses. The subsystem depends only on the .NET base class library; it is used by the Dagre subsystem and by the consumers: in Mermaid, by the ClusterLayout unit of the Rendering subsystem and (for ObjectKeyMap) by the FlowchartModel unit of the Flowchart subsystem and the GanttRenderer. graphlib's json reader and writer and the algorithms that the layout does not reach are not ported. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Graphlib/GraphlibTests.cs");
            designFiles = ("docs/design/canvas-net-layout/graphlib.md");
            verificationFiles = ("docs/verification/canvas-net-layout/graphlib.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/graphlib.yaml");
        }

        part graph : Graph;
        part graphAlgorithms : GraphAlgorithms;
    }
}
