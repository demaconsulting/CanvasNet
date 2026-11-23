package CanvasNetPlantUml_PlantUmlDescriptionRenderer {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSequenceRenderer::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlSvekRenderer::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlDescriptionRenderer {
        doc /* The internal renderer of the PlantUML Description subsystem (DescriptionRenderer.cs, DescriptionRenderer.Symbols.cs, DescriptionRenderer.UseCase.cs, DescriptionRenderer.Deployment.cs). Image measures a leaf element with the text layout of a call and returns its Svek entity image, with its stereotypes (italic, centered) over its name or description: a component as USymbolComponent2 draws it (margins of 15, 25, 20, and 10, corners of 5, and the UML icon), an interface as an 18-pixel circle box with its name 8 below and its stereotypes 8 above (links end on the circle), an actor as the stick figure of ActorStickMan (shared with the sequence diagrams) between its stereotypes and its name, a business actor with the slash across its head, a use case as the smallest ellipse of the proportions of its texts around their footprint grown by 6 (TextBlockInEllipse), a business use case with its margins and slash, the node, rectangle, folder, package, frame, cloud, database, card, artifact, storage, file, queue, stack, agent, collections, and label with the margins and outlines of their USymbol classes, a hexagon with the outline Graphviz gives it, a person with its head sized from its body, the boundary, control, and entity with the sequence diagram's symbols, and a circle as an interface; the action, the process, and archimate elements are drawn as rectangles. ContainerImage returns the Svek cluster image of a container: a component, artifact, storage, file, queue, stack, or hexagon container with its symbol's large outline, stereotypes, and bold title, and the other containers as the Svek cluster symbols with stereotypes of 14, colored by the element or by the skin parameters of its symbol. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.Symbols.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.UseCase.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.Deployment.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Description/DescriptionRendererTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/description/description-renderer.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/description/description-renderer.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/description/description-renderer.yaml");
        }

        // The renderer draws the model's elements as Svek entity and cluster images with the look and the text layout
        dependency usesDescriptionModel from PlantUmlDescriptionRenderer to CanvasNetPlantUml_PlantUmlDescriptionModel::PlantUmlDescriptionModel;
        dependency usesSvekModel from PlantUmlDescriptionRenderer to PlantUmlSvekModel;
        dependency usesSvekRenderer from PlantUmlDescriptionRenderer to PlantUmlSvekRenderer;
        dependency usesSkinTheme from PlantUmlDescriptionRenderer to PlantUmlSkinTheme;
        dependency usesTextLayout from PlantUmlDescriptionRenderer to PlantUmlTextLayout;
        dependency usesSceneModel from PlantUmlDescriptionRenderer to PlantUmlSceneModel;
        // The actor is the sequence diagram's stick figure; the boundary, control, entity, and queue its symbols
        dependency usesSequenceRenderer from PlantUmlDescriptionRenderer to PlantUmlSequenceRenderer;
    }
}