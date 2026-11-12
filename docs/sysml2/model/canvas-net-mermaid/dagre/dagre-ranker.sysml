package CanvasNet {
    part def DagreRanker {
        doc /* The internal static partial DagreRanker class, the port of dagre's rank/index.js, rank/util.js, rank/feasible-tree.js, and rank/network-simplex.js. Rank picks the ranker by the graph's ranker attribute: longest-path (each node as low as its successors allow), tight-tree (longest path, then a feasible tight tree), or network-simplex for any other value. The network simplex of Gansner et al. simplifies parallel edges, builds a feasible tight tree, numbers it in postorder, computes the cut values, and pivots (leave by the first tree edge with a negative cut value, enter by the edge of least slack across the cut from head to tail, exchange and recompute the numbering, cut values, and ranks from the first tree node) until no cut value is negative, taking the same pivots as dagre. Ranks may be negative and are normalized later. A graph that is not connected, or a cut no edge enters, throws InvalidOperationException (mermaid-render-failed through DagreLayout); every iteration charges the meter. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreRanker.cs, src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreRanker.FeasibleTree.cs, src/DemaConsulting.CanvasNet.Mermaid/Dagre/DagreRanker.NetworkSimplex.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreRankerTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre/dagre-ranker.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre/dagre-ranker.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre/dagre-ranker.yaml */
    }
}
