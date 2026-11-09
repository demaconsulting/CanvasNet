package CanvasNet {
    part def GraphAlgorithms {
        doc /* The internal static GraphAlgorithms: the graphlib traversals of dagre-d3-es on a Graph. Preorder and Postorder (graphlib's dfs) visit the given roots in order and follow the successors of a directed graph or the neighbors of an undirected graph in the graph's key order, recording each node once, and throw ArgumentException for a root that is not a node; Components lists the connected components ignoring direction; Tarjan lists the strongly connected components in completion order; FindCycles keeps the components with more than one node or with a self loop. The traversals recurse as graphlib does and call EnsureSufficientExecutionStack on every step, so a stack that runs short raises InsufficientExecutionStackException (reported by the Dagre subsystem as mermaid-limit-exceeded) instead of ending the process. The network-simplex ranker of the Dagre subsystem uses Postorder and Preorder. Stateless; depends on the Graph unit and the .NET base class library. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Graphlib/GraphAlgorithms.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Graphlib/GraphAlgorithmsTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/graphlib/graph-algorithms.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/graphlib/graph-algorithms.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/graphlib/graph-algorithms.yaml */
    }
}
