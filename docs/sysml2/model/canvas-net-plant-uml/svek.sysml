package CanvasNet {
    part def PlantUmlSvek {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Svek namespace that is the shared engine of the diagrams PlantUML lays out with Graphviz (class, object, state, use case, component, and deployment diagrams; PlantUML calls this engine svek). The PlantUmlSvekModel unit describes a diagram lowered for the engine (entities with their images, nested clusters, notes, and links with their decorations, styles, labels, lengths, and hints); the PlantUmlSvekSyntax unit reads the statements those diagrams share; the PlantUmlSvekLayout unit lays the diagram out over the dagre layered layout of the CanvasNetLayout system under one PlantUML work meter per call; the PlantUmlSvekGeometry unit turns routes into curves, clips them, and builds the link decorations; the PlantUmlSvekRenderer unit draws the clusters, notes, and links and calls the entity painters. The subsystem depends on the CanvasNetLayout system (graph, dagre layout, work meter), the Syntax subsystem (statements and detection helpers), the Styling subsystem (the look), and the Scenegraph subsystem (scenes and text layout). No diagram type uses it yet; the class, state, and description subsystems of phase 2 lower their models to it. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Svek/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekTestHelpers.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/svek.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/svek.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/svek.yaml */

        part svekModel : PlantUmlSvekModel;
        part svekSyntax : PlantUmlSvekSyntax;
        part svekLayout : PlantUmlSvekLayout;
        part svekGeometry : PlantUmlSvekGeometry;
        part svekRenderer : PlantUmlSvekRenderer;
    }
}
