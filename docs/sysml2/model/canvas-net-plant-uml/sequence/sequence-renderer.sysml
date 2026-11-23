package CanvasNetPlantUml_PlantUmlSequenceRenderer {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;
    private import CanvasNetPlantUml_PlantUmlSequenceLayout::*;
    private import CanvasNetPlantUml_PlantUmlSkinTheme::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlSequenceRenderer {
        doc /* The internal static SequenceRenderer of the PlantUML Sequence subsystem (SequenceRenderer.cs, SequenceRenderer.Participants.cs, SequenceRenderer.Messages.cs, SequenceRenderer.Blocks.cs). Render draws a placed sequence diagram as scene items in PlantUML's paint order with roles for the tests: boxes, group fills, dashed and dotted life lines, activation boxes, the heads and tails of the eight participant kinds (a rounded box, a stick figure, the boundary, control, and entity symbols, a cylinder, stacked boxes, and a queue), messages with their filled, open, half, cross, and circle heads, numbers, and labels, self message loops, notes as folded sheets, hexagons, or rectangles, group frames with header tabs, conditions, and dashed sections, dividers, delay texts, and destroy crosses; it moves every item by the layout's offset and sizes the view box to the body. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceRenderer.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceRenderer.Participants.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceRenderer.Messages.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceRenderer.Blocks.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Sequence/SequenceRendererTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/sequence/sequence-renderer.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/sequence/sequence-renderer.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/sequence/sequence-renderer.yaml");
        }

        // The renderer builds scene items with the look of the diagram from the placed geometry
        dependency usesSceneModel from PlantUmlSequenceRenderer to PlantUmlSceneModel;
        dependency usesTextLayout from PlantUmlSequenceRenderer to PlantUmlTextLayout;
        dependency usesSkinTheme from PlantUmlSequenceRenderer to PlantUmlSkinTheme;
        dependency usesSequenceLayout from PlantUmlSequenceRenderer to PlantUmlSequenceLayout;
        dependency usesCanvas from PlantUmlSequenceRenderer to CanvasNet_CanvasSubsystem::Canvas;
    }
}
