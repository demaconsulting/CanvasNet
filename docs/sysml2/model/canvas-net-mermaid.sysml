package CanvasNet {
    part def CanvasNetMermaidSystem {
        doc /* CanvasNetMermaid is a static, offline, deterministic renderer for the Mermaid diagram language, delivered as the separate DemaConsulting.CanvasNet.Mermaid NuGet package. The public MermaidDocument unit parses Mermaid text from a string, a stream, or a file path (a located, coded, hinted MermaidSyntaxException for invalid text; an InvalidDataException for oversized text), describes the diagram without drawing it (type, supported flag, title, accessible title, direction, node, edge, and subgraph counts), measures it, and renders it onto a CanvasNet Surface at its natural size times a scale or fitted inside an exact size, over a transparent background by default. This version draws flowcharts (flowchart and graph) with Mermaid's own layout, shapes, labels, edges, themes, and styles; every other Mermaid diagram type is recognized and reported as mermaid-diagram-unsupported with an empty transparent picture. Every recognized but unsupported construct is reported with a stable mermaid-* code and location, and strict mode throws. Untrusted input is bounded by MermaidLimits. The internal subsystems are Syntax (preprocessing, detection, YAML, lexing, HTML sanitizing), Configuration (precedence, themes, colors, CSS declarations), Graphlib and Dagre (the layered layout Mermaid bundles), Rendering (clusters, shapes, edges, labels, pictures), Scenegraph (scene model and painter), and Flowchart (parser and model). The MermaidImageSource plugs the package into the CanvasNet image-source contract. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidSystemIntegrationTests.cs, test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidFlowchartIntegrationTests.cs, test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidReferenceLayoutTests.cs, test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidHardeningTests.cs, test/DemaConsulting.CanvasNet.Mermaid.Tests/MermaidUnsupportedDiagramTests.cs */
        comment designRef /* Design: docs/design/canvas-net-mermaid.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-mermaid.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-mermaid.yaml */
        comment /* Third-Party Provenance and Attribution: CanvasNetMermaid is derived with attribution, not clean-room. Its preprocessing, detection, configuration, themes, flowchart grammar and database, shapes, labels, edges, and cluster layout are ported from mermaid (MIT); the layout and graph from dagre-d3-es, the port of dagre and graphlib (MIT); the curves from d3-shape and d3-path (ISC); the color functions from khroma (MIT); the YAML subset from js-yaml (MIT); sorting helpers from lodash (MIT); the markdown subset from marked (MIT). The license texts and the provenance table ship in src/DemaConsulting.CanvasNet.Mermaid/third-party/, and every file with ported logic carries a Portions derived from header. The package is not affiliated with those projects. */
        comment /* CanvasNetMermaid references the sibling CanvasNet system only: its Canvas subsystem (Surface, Rgba32, CssColor), Geometry (paths), Drawing (path filling and stroking), Fonts (the bundled fonts through a font resolver with no system fonts), Rendering (text measuring and drawing, word wrapping), and ImageSources (the image-source contract and the caller's image resolver). It has no OTS runtime dependency. */

        part mermaidDocument : MermaidDocument;
        part mermaidSyntax : MermaidSyntax;
        part mermaidConfiguration : MermaidConfiguration;
        part mermaidGraphlib : MermaidGraphlib;
        part mermaidDagre : MermaidDagre;
        part mermaidRendering : MermaidRendering;
        part mermaidScenegraph : MermaidScenegraph;
        part mermaidFlowchart : MermaidFlowchart;

        dependency usesCanvas from CanvasNetMermaidSystem to Canvas;
        dependency usesDrawing from CanvasNetMermaidSystem to Drawing;
        dependency usesFonts from CanvasNetMermaidSystem to Fonts;
        dependency usesGeometry from CanvasNetMermaidSystem to Geometry;
        dependency usesRendering from CanvasNetMermaidSystem to Rendering;
        dependency usesImageSources from CanvasNetMermaidSystem to ImageSources;
    }
}
