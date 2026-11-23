package CanvasNetLayout_LayoutDagreSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetLayout_DagreLayout::*;
    public import CanvasNetLayout_DagreAcyclic::*;
    public import CanvasNetLayout_DagreRanker::*;
    public import CanvasNetLayout_DagreNesting::*;
    public import CanvasNetLayout_DagreNormalizer::*;
    public import CanvasNetLayout_DagreOrdering::*;
    public import CanvasNetLayout_DagrePositioning::*;
    public import CanvasNetLayout_DagreCoordinateSystem::*;
    public import CanvasNetLayout_LayoutWorkMeter::*;

    part def LayoutDagre {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Layout.Dagre namespace that lays out the graphs of the diagram packages (Mermaid's flowcharts, class, state, and ER diagrams): a port of the dagre layered layout of dagre-d3-es, the package the pinned Mermaid release bundles, with the same ranks, orders, coordinates, edge routes, label positions, and drawing size as dagre-d3-es (checked against 224 stage goldens recorded from it). The DagreLayout unit is the entry point (Run) and runs dagre's stages in dagre's order, with the input and layout labels and the lodash and V8 sort semantics that settle ties as JavaScript does; DagreAcyclic breaks cycles, DagreRanker ranks (network simplex, tight tree, or longest path), DagreNesting keeps clusters together (nesting graph, dummy chains, border segments), DagreNormalizer splits long edges into chains of dummy nodes and back, DagreOrdering reduces crossings, DagrePositioning assigns coordinates (Brandes-Koepf), and DagreCoordinateSystem handles the four rank directions. LayoutWorkMeter, which is abstract, bounds the work of the layout of one diagram with the budget the consumer gives, observes cancellation, has the consumer create its own located failures (for Mermaid, mermaid-limit-exceeded naming MermaidLimits.MaxLayoutWork, and mermaid-render-failed for a layout dagre cannot complete), and holds the per-diagram counter of lodash's uniqueId. The subsystem depends only on the Graphlib subsystem (and the .NET base class library); it is called by the consumers' dagre wrappers (Mermaid's ClusterLayout of its Rendering subsystem), which derive the meter (Mermaid's MermaidLayoutWorkMeter). As of this release, its constituent units are DagreLayout, DagreAcyclic, DagreRanker, DagreNesting, DagreNormalizer, DagreOrdering, DagrePositioning, DagreCoordinateSystem, and LayoutWorkMeter. */

        metadata info : SoftwareInfo {
            testFiles = (
                "test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreTests.cs",
                "test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreGoldens.cs",
                "test/DemaConsulting.CanvasNet.Layout.Tests/Dagre/DagreTestGraphs.cs",
                "test/DemaConsulting.CanvasNet.Layout.Tests/Fixtures/upstream-vectors/dagre-layouts.json"
            );
            designFiles = ("docs/design/canvas-net-layout/dagre.md");
            verificationFiles = ("docs/verification/canvas-net-layout/dagre.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout/dagre.yaml");
        }

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
