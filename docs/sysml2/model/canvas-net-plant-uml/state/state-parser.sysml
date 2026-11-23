package CanvasNetPlantUml_PlantUmlStateParser {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlDiagramDetector::*;
    private import CanvasNetPlantUml_PlantUmlPreprocessor::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSkinParams::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlStateModel::*;
    private import CanvasNetPlantUml_PlantUmlSvekModel::*;
    private import CanvasNetPlantUml_PlantUmlSvekSyntax::*;

    part def PlantUmlStateParser {
        doc /* The internal parser of the PlantUML State subsystem (StateParser.cs, StateParser.Statements.cs). Parse reads the statements of a state diagram one by one by their leading keywords and with the shared Svek readers, without regular expressions: state declarations with a name, a quoted display and an alias, stereotypes (choice, fork, join, start, end, junction, history, and history* choose the kind; the border symbols are reported and drawn as states), colors, a description, or a block; descriptions written as Name : text; composite states opened with a brace or begin and closed with a brace or end state, with concurrent regions started by -- or ||; transitions with their length, direction hint, style, color, and label, where [*], [H], [H*], X[H], and X[H*] are the pseudo-states of the current composite state or of X, each created once with PlantUML's qualified id; notes on states, on transitions, and floating; hide empty description; the page texts, the direction, the scale, and the skin parameters applied to the look of graph diagrams. Names resolve in the current composite state, the composite states around it, or by a unique name. Parts it reads but does not draw are reported as plantuml-statement-unsupported; malformed statements, a transition between two concurrent regions, and statements of other diagram types are located syntax errors; the nodes, edges, labels, and nesting are bounded by PlantUmlLimits. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/State/StateParser.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/State/StateParser.Statements.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/State/StateParserTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/state/state-parser.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/state/state-parser.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/state/state-parser.yaml");
        }

        // The parser reads detected, preprocessed statements with the shared readers into the model
        dependency usesPreprocessor from PlantUmlStateParser to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlStateParser to PlantUmlDiagramDetector;
        dependency usesSvekSyntax from PlantUmlStateParser to PlantUmlSvekSyntax;
        dependency usesSvekModel from PlantUmlStateParser to PlantUmlSvekModel;
        dependency usesSkinParams from PlantUmlStateParser to PlantUmlSkinParams;
        dependency usesSkinTheme from PlantUmlStateParser to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlStateParser to PlantUmlSceneModel;
        dependency usesStateModel from PlantUmlStateParser to PlantUmlStateModel;
    }
}