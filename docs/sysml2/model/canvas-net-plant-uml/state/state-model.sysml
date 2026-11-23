package CanvasNetPlantUml_PlantUmlStateModel {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlStateRenderer::*;
    private import CanvasNetPlantUml_PlantUmlSvekLayout::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlSvekRenderer::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlStateModel {
        doc /* The internal model of the PlantUML State subsystem (StateModel.cs, StateModel.Elements.cs): the diagram of states and pseudo-states (with their kinds, displays, descriptions, colors, and the regions of a composite state with their arrangement), notes, and transitions, with the page texts, the look, the direction, the layout settings, the scale, and hide empty description. The facts count the simple states and pseudo-states, the transitions that do not touch a note, and the composite states; Lower builds, for every call, the Svek diagram with a cluster per composite state with its StateRenderer image (and a cluster without image per concurrent region), an entity with its StateRenderer image per state and pseudo-state, the notes, and the transitions; Layout lays it out with the Svek engine and decorates it with the page texts with the spacing of PlantUML's graph diagrams. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/State/StateModel.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/State/StateModel.Elements.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/State/StateModelTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/state/state-model.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/state/state-model.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/state/state-model.yaml");
        }

        // The model lowers itself to the Svek engine, draws its elements with the renderer, and decorates the page
        dependency usesSvekModel from PlantUmlStateModel to PlantUmlSvekModel;
        dependency usesSvekLayout from PlantUmlStateModel to PlantUmlSvekLayout;
        dependency usesSvekRenderer from PlantUmlStateModel to PlantUmlSvekRenderer;
        dependency usesStateRenderer from PlantUmlStateModel to PlantUmlStateRenderer;
        dependency usesSkinTheme from PlantUmlStateModel to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlStateModel to PlantUmlSceneModel;
        dependency usesTextLayout from PlantUmlStateModel to PlantUmlTextLayout;
    }
}