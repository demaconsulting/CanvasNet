package CanvasNetLayout_Graph {
    private import SoftwareItemMetadata::*;

    part def Graph {
        doc /* The internal sealed generic Graph (graph, node, and edge label types), the GraphEdge record (tail, head, and optional name; the lesser end first in an undirected graph), and the ObjectKeyMap string-keyed map: a port of graphlib's Graph from dagre-d3-es. A graph is directed, simple, and flat by default and may be undirected, a multigraph (named parallel edges), or compound (nodes nested in parents under an implicit root). It adds nodes and edges with given or default labels (an existing node or edge keeps its label unless a new one is given; the ends of a new edge are added first), refuses a named edge in a simple graph and a parent cycle or a parent in a flat graph with InvalidOperationException, removes a node with all its edges and moves its children to the top level, answers adjacency queries (in-edges, out-edges, node edges, predecessors, successors, neighbors, sources, sinks, leaves) with null for an absent node where graphlib answers undefined, and copies a filtered graph with each kept node under its nearest kept ancestor. Every collection that graphlib keeps in a JavaScript object is an ObjectKeyMap, which lists array-index keys (the canonical decimal integers from 0 to 4294967294) first in ascending numeric order and the other keys in insertion order, a key removed and added again moving to the end; edges are listed in insertion order. JavaScript prototype keys are ordinary names. Not thread-safe; depends only on the .NET base class library. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Layout/Graphlib/Graph.cs",
                "src/DemaConsulting.CanvasNet.Layout/Graphlib/GraphEdge.cs",
                "src/DemaConsulting.CanvasNet.Layout/Graphlib/ObjectKeyMap.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Layout.Tests/Graphlib/GraphTests.cs",
                "test/DemaConsulting.CanvasNet.Layout.Tests/Graphlib/ObjectKeyMapTests.cs"
            );
            designFiles = ("docs/design/canvas-net-layout/graphlib/graph.md");
            verificationFiles = ("docs/verification/canvas-net-layout/graphlib/graph.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/graphlib/graph.yaml");
        }
    }
}
