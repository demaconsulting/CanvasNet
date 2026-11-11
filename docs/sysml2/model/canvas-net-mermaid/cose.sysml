package CanvasNet {
    part def MermaidCose {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.Mermaid.Cose namespace that is the force-directed layout Mermaid draws its mindmaps with: a port of the cose-bilkent layout (cytoscape-cose-bilkent over cose-base and layout-base) as the pinned Mermaid release runs it, with quality proof and no animation, and of the straight-edge points that Mermaid reads back from cytoscape. Given the sizes of the nodes of one tree and its edges, it gives the center of every node and the start, middle, and end point of every edge; it knows nothing about Mermaid text, shapes, or paint. Only the path a tree takes is ported (the flat forest, the radial placement around the center of the tree, the spring embedder with its repulsion grid and cooling schedule, and the move to the origin less the graph margin); any input that is not one tree is refused. The layout of a mindmap makes no random choice, so no random number generator is ported. The layout is chaotic, so the port follows JavaScript's operation order and number semantics and computes the sine, cosine, logarithm, and power correctly rounded, as Chrome does, without the host's math library: the positions are Mermaid's bit for bit for bit-identical node sizes and the same on every platform (checked against 36 layouts and the math of Chrome recorded with the libraries of the pinned Mermaid bundle). Every step charges the layout work meter of the render, which bounds the work and observes cancellation; a grid JavaScript could not build fails as mermaid-render-failed. As of this release, its constituent unit is CoseLayout. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/Cose/CoseTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid/cose.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid/cose.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid/cose.yaml */
        comment /* Third-Party Provenance and Attribution: the layout ports the parts of layout-base and cose-base (MIT, iVis@Bilkent) and of cytoscape-cose-bilkent and cytoscape (MIT, The Cytoscape Consortium) that a mindmap reaches, and the options of Mermaid's cose-bilkent setup (MIT); the license texts ship as third-party/LICENSE-cose-base.txt and third-party/LICENSE-cytoscape.txt. The correctly rounded math is original work of this repository. */
        comment /* Within the CanvasNetMermaid system the Cose subsystem depends only on the Dagre subsystem (the layout work meter) and the public exception and diagnostic-code types of the package root; it is called by the MindmapRenderer unit of the Mindmap subsystem. */

        part coseLayout : CoseLayout;
    }
}
