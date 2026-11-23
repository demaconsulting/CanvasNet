package CanvasNetPlantUml_PlantUmlClassModel {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlClassRenderer::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlSvekLayout::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlSvekRenderer::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlClassModel {
        doc /* The internal model of the PlantUML Class subsystem (ClassModel.cs, ClassModel.Elements.cs): the diagram of entities (classes and their kin, objects, and maps with their bodies, stereotypes, generics, and colors), packages, notes, links, and hide and show rules, with the page texts, the look, the direction, the layout settings, and the scale. ClassMember and ClassBody read a member's visibility and modifiers and split a body into separators, fields, and methods; PortionsOf applies the hide and show rules in order; the facts count the entities, the links that do not touch a note, and the packages that hold something; Lower builds, for every call, the Svek diagram with a cluster per package, an entity with its ClassRenderer image per entity, the notes, and the links; Layout lays it out with the Svek engine and decorates it with the page texts with the spacing of PlantUML's graph diagrams. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassModel.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassModel.Elements.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Class/ClassModelTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/class/class-model.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/class/class-model.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/class/class-model.yaml");
        }

        // The model lowers itself to the Svek engine, draws its entities with the renderer, and decorates the page
        dependency usesSvekModel from PlantUmlClassModel to PlantUmlSvekModel;
        dependency usesSvekLayout from PlantUmlClassModel to PlantUmlSvekLayout;
        dependency usesSvekRenderer from PlantUmlClassModel to PlantUmlSvekRenderer;
        dependency usesClassRenderer from PlantUmlClassModel to PlantUmlClassRenderer;
        dependency usesSkinTheme from PlantUmlClassModel to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlClassModel to PlantUmlSceneModel;
        dependency usesTextLayout from PlantUmlClassModel to PlantUmlTextLayout;
    }
}