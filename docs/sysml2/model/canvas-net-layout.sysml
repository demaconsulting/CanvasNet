package CanvasNetLayout {
    private import SoftwareItemMetadata::*;
    public import CanvasNetLayout_LayoutGraphlibSubsystem::*;
    public import CanvasNetLayout_LayoutDagreSubsystem::*;

    part def CanvasNetLayoutSystem {
        doc /* CanvasNetLayout is the shared graph layout of the CanvasNet diagram packages, delivered as the separate DemaConsulting.CanvasNet.Layout NuGet package. It holds a port of graphlib (the Graphlib subsystem: the directed, multi, and compound Graph with the JavaScript key order of its collections, and the depth-first traversals) and of the dagre layered layout of dagre-d3-es (the Dagre subsystem: DagreLayout with cycle breaking, ranking, cluster nesting, normalization, crossing reduction, Brandes-Koepf positioning, and the four rank directions), which positions the nodes of a directed, possibly compound and cyclic graph in ranks and routes its edges with the same results as dagre-d3-es (checked against 224 stage goldens recorded from it). Every iterative stage charges the abstract LayoutWorkMeter that the consumer derives, which bounds the work, observes cancellation, and creates the consumer's own located limit and render failures, so the package reports errors in the words of the package that called it and depends on no diagram package. Layouts are deterministic and share no state between diagrams. All types are internal and visible only to the friend assemblies (the consuming diagram packages, DemaConsulting.CanvasNet.Mermaid and DemaConsulting.CanvasNet.PlantUml, and the tests), which are built and released in lockstep with it; the package has no public API. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.Layout.Tests/LayoutSystemIntegrationTests.cs");
            designFiles = ("docs/design/canvas-net-layout.md");
            verificationFiles = ("docs/verification/canvas-net-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-layout.yaml");
        }

        comment /* Third-Party Provenance and Attribution: CanvasNetLayout is derived with attribution, not clean-room. Its graph and traversals are ported from graphlib and its layered layout from dagre, both as dagre-d3-es bundles them (MIT), with the sort, extremum, and uniqueId semantics of lodash (MIT) and the steps of V8's array sort reimplemented from the published algorithm. The license texts and the provenance table ship in src/DemaConsulting.CanvasNet.Layout/third-party/, and every file with ported logic carries a Portions derived from header. The package is not affiliated with those projects. */
        comment /* CanvasNetLayout references no other CanvasNet system: it depends only on the .NET base class library. It has no OTS runtime dependency. Its consumers are the diagram systems that reference it (CanvasNetMermaid and CanvasNetPlantUml). */

        part layoutGraphlib : LayoutGraphlib;
        part layoutDagre : LayoutDagre;
    }
}
