package CanvasNetPlantUml_PlantUmlSvekSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetPlantUml_PlantUmlSvekModel::*;
    public import CanvasNetPlantUml_PlantUmlSvekSyntax::*;
    public import CanvasNetPlantUml_PlantUmlSvekLayout::*;
    public import CanvasNetPlantUml_PlantUmlSvekGeometry::*;
    public import CanvasNetPlantUml_PlantUmlSvekRenderer::*;

    part def PlantUmlSvek {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Svek namespace that is the shared engine of the diagrams PlantUML lays out with Graphviz (class, object, state, use case, component, and deployment diagrams; PlantUML calls this engine svek). The PlantUmlSvekModel unit describes a diagram lowered for the engine (entities with their images, nested clusters, notes, and links with their decorations, styles, labels, lengths, and hints); the PlantUmlSvekSyntax unit reads the statements those diagrams share; the PlantUmlSvekLayout unit lays the diagram out over the dagre layered layout of the CanvasNetLayout system under one PlantUML work meter per call; the PlantUmlSvekGeometry unit turns routes into curves, clips them, and builds the link decorations; the PlantUmlSvekRenderer unit draws the clusters, notes, and links and calls the entity painters. The subsystem depends on the CanvasNetLayout system (graph, dagre layout, work meter), the Syntax subsystem (statements and detection helpers), the Styling subsystem (the look), and the Scenegraph subsystem (scenes and text layout). The Class subsystem lowers its class and object diagrams to it, the State subsystem its state diagrams, and the Description subsystem its use case, component, and deployment diagrams. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.PlantUml/Svek/NamespaceDoc.cs");
            testFiles = (
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekTestHelpers.cs"
            );
            designFiles = ("docs/design/canvas-net-plant-uml/svek.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/svek.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/svek.yaml");
        }

        part svekModel : PlantUmlSvekModel;
        part svekSyntax : PlantUmlSvekSyntax;
        part svekLayout : PlantUmlSvekLayout;
        part svekGeometry : PlantUmlSvekGeometry;
        part svekRenderer : PlantUmlSvekRenderer;
    }
}
