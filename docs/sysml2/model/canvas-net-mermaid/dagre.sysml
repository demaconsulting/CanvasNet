package CanvasNet {
    part def MermaidDagre {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Dagre namespace that lays out the graphs of Mermaid flowcharts: a port of the dagre layered layout of dagre-d3-es, the package the pinned Mermaid release bundles, with the same ranks, orders, coordinates, edge routes, label positions, and drawing size as dagre-d3-es (checked against 224 stage goldens recorded from it). The DagreLayout unit is the entry point (Run) and runs dagre's stages in dagre's order, with the input and layout labels and the lodash and V8 sort semantics that settle ties as JavaScript does; DagreAcyclic breaks cycles, DagreRanker ranks (network simplex, tight tree, or longest path), DagreNesting keeps clusters together (nesting graph, dummy chains, border segments), DagreNormalizer splits long edges into chains of dummy nodes and back, DagreOrdering reduces crossings, DagrePositioning assigns coordinates (Brandes-Koepf), and DagreCoordinateSystem handles the four rank directions. LayoutWorkMeter bounds the work of the layout of one diagram (MermaidLimits.MaxLayoutWork, failing with a located mermaid-limit-exceeded), observes cancellation, creates the located mermaid-render-failed of a layout dagre cannot complete, and holds the per-diagram counter of lodash's uniqueId. The subsystem depends on the Graphlib subsystem and the public location and exception types of the package root; it is called by ClusterLayout of the Rendering subsystem, and the Flowchart subsystem creates the meter. As of this release, its constituent units are DagreLayout, DagreAcyclic, DagreRanker, DagreNesting, DagreNormalizer, DagreOrdering, DagrePositioning, DagreCoordinateSystem, and LayoutWorkMeter. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreTests.cs (with the helpers test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreGoldens.cs and test/DemaConsulting.CanvasNet.Mermaid.Tests/Dagre/DagreTestGraphs.cs and the fixture test/DemaConsulting.CanvasNet.Mermaid.Tests/Fixtures/upstream-vectors/dagre-layouts.json) */
        comment designRef /* Design: docs/design/canvas-net-mermaid/dagre.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/dagre.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/dagre.yaml */

        part dagreLayout : DagreLayout;
        part dagreAcyclic : DagreAcyclic;
        part dagreRanker : DagreRanker;
        part dagreNesting : DagreNesting;
        part dagreNormalizer : DagreNormalizer;
        part dagreOrdering : DagreOrdering;
        part dagrePositioning : DagrePositioning;
        part dagreCoordinateSystem : DagreCoordinateSystem;
        part layoutWorkMeter : LayoutWorkMeter;
    }
}
