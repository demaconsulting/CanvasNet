package CanvasNetPlantUml_PlantUmlSyntaxSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetPlantUml_PlantUmlPreprocessor::*;
    public import CanvasNetPlantUml_PlantUmlDiagramDetector::*;

    part def PlantUmlSyntax {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Syntax namespace that is the first stage of the PlantUML pipeline: it turns a text into the statements that the diagram parsers read, with every position mapped back to the line and column of the original text, and detects the diagram type. The PlantUmlPreprocessor unit frames the first @start block, removes the comments, joins continued lines, and applies the supported subset of the PlantUML preprocessor, reporting every other directive; the PlantUmlDiagramDetector unit names the type from the tag or from the statements, in the order PlantUML's diagram factories try them. No regular expression runs on diagram text and nothing is read from another file or the network. The subsystem depends only on the public location, limit, exception, code, and pending-report types of the root namespace; it is called by PlantUmlDocument and by the diagram parsers. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Syntax/SyntaxTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/syntax.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/syntax.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/syntax.yaml");
        }

        part preprocessor : PlantUmlPreprocessor;
        part diagramDetector : PlantUmlDiagramDetector;
    }
}
