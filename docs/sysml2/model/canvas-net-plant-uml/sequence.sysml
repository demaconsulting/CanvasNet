package CanvasNetPlantUml_PlantUmlSequenceSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetPlantUml_PlantUmlSequenceParser::*;
    public import CanvasNetPlantUml_PlantUmlSequenceModel::*;
    public import CanvasNetPlantUml_PlantUmlSequenceLayout::*;
    public import CanvasNetPlantUml_PlantUmlSequenceRenderer::*;

    part def PlantUmlSequence {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Sequence namespace that draws PlantUML sequence diagrams. The PlantUmlSequenceParser unit reads the sequence statements (participants of eight kinds, messages with their arrows and activation shorthands, activate, deactivate, destroy, return, autonumber, notes, groups, boxes, dividers, delays, spaces, page texts, hide footbox, scale, and skin parameters) into a PlantUmlSequenceModel, reporting the statements that are not drawn and refusing invalid ones with located errors; the PlantUmlSequenceModel unit holds the participants, boxes, and events, gives the facts (participants as nodes, messages as edges, boxes as subgraphs), and lays the diagram out for every call; the PlantUmlSequenceLayout unit places the participant columns and the rows with the geometry of PlantUML's teoz sequence engine within the layout work limit; the PlantUmlSequenceRenderer unit draws the placed diagram as a scene with PlantUML's shapes. The subsystem depends on the Syntax subsystem (statements and detection helpers), the Styling subsystem (the look), and the Scenegraph subsystem (scenes, text layout, and page texts); the document's diagram registry calls it for the sequence type. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Sequence/SequenceTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/sequence.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/sequence.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/sequence.yaml");
        }

        part sequenceParser : PlantUmlSequenceParser;
        part sequenceModel : PlantUmlSequenceModel;
        part sequenceLayout : PlantUmlSequenceLayout;
        part sequenceRenderer : PlantUmlSequenceRenderer;
    }
}
