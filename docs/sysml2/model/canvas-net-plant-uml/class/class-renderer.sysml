package CanvasNetPlantUml_PlantUmlClassRenderer {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlClassRenderer {
        doc /* The internal renderer of the PlantUML Class subsystem (ClassRenderer.cs, ClassRenderer.Header.cs, ClassRenderer.Members.cs, ClassRenderer.Objects.cs). Image measures an entity with the text layout of a call and returns its Svek entity image: a class box as PlantUML's EntityImageClass draws it (the header with the circled character of its kind in PlantUML's colors, the stereotypes in italic 12, the name in italic for an abstract class or an interface, the generic parameters in a dashed white box over the top-right corner; the field and method compartments with the visibility icons of PlantUML's VisibilityModifier, underlined static and italic abstract members, and the separators of BodyEnhanced with their titles), an object box as EntityImageObject draws it, and a map as the table of EntityImageMap. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassRenderer.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassRenderer.Header.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassRenderer.Members.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassRenderer.Objects.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Class/ClassRendererTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/class/class-renderer.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/class/class-renderer.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/class/class-renderer.yaml");
        }

        // The renderer draws the model's entities as Svek entity images with the look and the text layout
        dependency usesClassModel from PlantUmlClassRenderer to CanvasNetPlantUml_PlantUmlClassModel::PlantUmlClassModel;
        dependency usesSvekModel from PlantUmlClassRenderer to PlantUmlSvekModel;
        dependency usesSkinTheme from PlantUmlClassRenderer to PlantUmlSkinTheme;
        dependency usesTextLayout from PlantUmlClassRenderer to PlantUmlTextLayout;
        dependency usesSceneModel from PlantUmlClassRenderer to PlantUmlSceneModel;
    }
}