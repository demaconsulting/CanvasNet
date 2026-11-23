package CanvasNetLayout_DagreOrdering {
    private import SoftwareItemMetadata::*;

    part def DagreOrdering {
        doc /* The internal static partial DagreOrdering class, the port of dagre's order/index.js, init-order.js, build-layer-graph.js, sort-subgraph.js, barycenter.js, resolve-conflicts.js, sort.js, cross-count.js, and add-subgraph-constraints.js. Order builds the layer graph of every rank for the down and up sweeps, starts from the order of a depth-first search from the nodes in rank order, and sweeps alternately up and down (ties biased right on two sweeps of four), sorting each layer by barycenter with each cluster sorted first and kept contiguous, conflicts with the cluster constraints resolved by merging entries (Forster), and the order of sibling clusters recorded as constraints; it keeps the order with the fewest weighted crossings (counted with the accumulator tree of Barth, Juenger, and Mutzel) and stops after four sweeps without improvement. Sorts use the Lodash helpers so that ties and NaN barycenters are ordered as V8 orders them; conflict entries are kept in an ObjectKeyMap for JavaScript's key order. Every sweep charges the meter. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Layout/Dagre/DagreOrdering.cs",
                "src/DemaConsulting.CanvasNet.Layout/Dagre/DagreOrdering.Sort.cs",
                "src/DemaConsulting.CanvasNet.Layout/Dagre/DagreOrdering.CrossCount.cs",
                "src/DemaConsulting.CanvasNet.Layout/Dagre/DagreOrdering.Constraints.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreOrderingTests.cs");
            designFiles = ("docs/design/canvas-net-layout/dagre/dagre-ordering.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre/dagre-ordering.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre/dagre-ordering.yaml");
        }
    }
}
