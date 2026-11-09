package CanvasNet {
    part def MermaidGraphlib {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Graphlib namespace that is the graph data structure of the Mermaid layout: a port of graphlib as dagre-d3-es bundles it for the pinned Mermaid release. The Graph unit is the directed or undirected, simple or multi, flat or compound graph with labels on the graph, the nodes, and the edges (Graph, with the edge identity GraphEdge), whose nodes, children, predecessors, and successors are kept in the own-key order of a JavaScript object (ObjectKeyMap: array-index names first in ascending numeric order, then the other names in insertion order, a name removed and added again moving to the end) and whose edges are kept in insertion order, because every dagre stage breaks ties by that order. The GraphAlgorithms unit holds the graphlib traversals preorder, postorder, components, tarjan, and findCycles on the same key order; the dagre ranker uses preorder and postorder. The subsystem depends only on the .NET base class library; it is used by the Dagre subsystem, by the ClusterLayout unit of the Rendering subsystem, and (for ObjectKeyMap) by the FlowchartModel unit of the Flowchart subsystem. graphlib's json reader and writer and the algorithms that the layout does not reach are not ported. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Graphlib/GraphlibTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/graphlib.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/graphlib.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/graphlib.yaml */

        part graph : Graph;
        part graphAlgorithms : GraphAlgorithms;
    }
}
