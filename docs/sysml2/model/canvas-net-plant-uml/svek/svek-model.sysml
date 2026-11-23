package CanvasNetPlantUml_PlantUmlSvekModel {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;

    part def PlantUmlSvekModel {
        doc /* The internal model of the PlantUML Svek subsystem (SvekModel.cs, SvekModel.Elements.cs): SvekDiagram with its entities, clusters (parents before children), notes, and links, its rank direction, spacing, theme, and location; SvekEntity with its ISvekEntityImage (size, clip outline, and painter); SvekCluster with its optional ISvekClusterImage (header band, minimum width, padding, painter), concurrent regions, and the flag to lay it out on its own; SvekNote attached to an entity side, to a link, or floating; SvekLink with a decoration at each end (LinkDecor), a line style, width, color, length in ranks, direction hint, label with its direction mark, end labels, and weight. MembersOf gives the members of a cluster in text order, CountFacts gives the nodes, edges, and subgraphs, and Validate refuses an inconsistent diagram with an ArgumentException. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekModel.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Svek/SvekModel.Elements.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Svek/SvekModelTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/svek/svek-model.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/svek/svek-model.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/svek/svek-model.yaml");
        }

        // The model holds the look of the diagram and the scene items of its images
        dependency usesSkinTheme from PlantUmlSvekModel to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlSvekModel to PlantUmlSceneModel;
    }
}
