package CanvasNetPlantUml_PlantUmlActivityRenderer {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlActivityLayout::*;
    private import CanvasNetPlantUml_PlantUmlSceneModel::*;

    part def PlantUmlActivityRenderer {
        doc /* The internal static ActivityRenderer of the PlantUML Activity subsystem (ActivityRenderer.cs). Render turns the placed shapes of an activity diagram into scene items in paint order with roles for the tests (actions, start, stop, end, connectors, diamonds and hexagons, bars, split lines, arrows with heads and labels, notes, partition frames, swimlane fills, dividers, and titles), with their fills, strokes, and locations, skips the space that a shape occupies without paint, and sizes the view box to the geometry with the theme's background. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityRenderer.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.PlantUml.Tests/Activity/ActivityRendererTests.cs");
            designFiles = ("docs/design/canvas-net-plant-uml/activity/activity-renderer.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/activity/activity-renderer.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/activity/activity-renderer.yaml");
        }

        // The renderer builds scene items from the placed geometry
        dependency usesSceneModel from PlantUmlActivityRenderer to PlantUmlSceneModel;
        dependency usesActivityLayout from PlantUmlActivityRenderer to PlantUmlActivityLayout;
        dependency usesCanvas from PlantUmlActivityRenderer to CanvasNet_CanvasSubsystem::Canvas;
    }
}
