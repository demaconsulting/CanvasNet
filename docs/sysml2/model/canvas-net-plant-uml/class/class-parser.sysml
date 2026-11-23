package CanvasNetPlantUml_PlantUmlClassParser {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlClassModel::*;
    private import CanvasNetPlantUml_PlantUmlDiagramDetector::*;
    private import CanvasNetPlantUml_PlantUmlPreprocessor::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSkinParams::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlSvekSyntax::*;

    part def PlantUmlClassParser {
        doc /* The internal parser of the PlantUML Class subsystem (ClassParser.cs, ClassParser.Members.cs). Parse reads the statements of a class or object diagram one by one by their leading keywords and with the shared Svek readers, without regular expressions: the declarations of classes and their kin (class, abstract, interface, enum, annotation, entity, exception, struct, protocol, metaclass, stereotype, record, dataclass), objects, and maps with display names, generic parameters, stereotypes, colors, extends and implements, and bodies; members written as Name : text; packages and namespaces, nested and with qualified names resolved as PlantUML resolves them; relationships with every drawn decoration, style, length, direction hint, and label; notes on entities, on links, and floating; hide and show rules; the page texts, the direction, the scale, and the skin parameters applied to the look of graph diagrams. Parts it reads but does not draw are reported as plantuml-statement-unsupported; malformed statements and elements of other diagram types are located syntax errors; the nodes, edges, labels, and nesting are bounded by PlantUmlLimits. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassParser.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Class/ClassParser.Members.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Class/ClassParserTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/class/class-parser.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/class/class-parser.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/class/class-parser.yaml");
        }

        // The parser reads detected, preprocessed statements with the shared readers into the model
        dependency usesPreprocessor from PlantUmlClassParser to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlClassParser to PlantUmlDiagramDetector;
        dependency usesSvekSyntax from PlantUmlClassParser to PlantUmlSvekSyntax;
        dependency usesSvekModel from PlantUmlClassParser to PlantUmlSvekModel;
        dependency usesSkinParams from PlantUmlClassParser to PlantUmlSkinParams;
        dependency usesSkinTheme from PlantUmlClassParser to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlClassParser to PlantUmlSceneModel;
        dependency usesClassModel from PlantUmlClassParser to PlantUmlClassModel;
    }
}