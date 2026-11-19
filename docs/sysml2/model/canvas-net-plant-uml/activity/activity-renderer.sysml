package CanvasNet {
    part def PlantUmlActivityRenderer {
        doc /* The internal static ActivityRenderer of the PlantUML Activity subsystem (ActivityRenderer.cs). Render turns the placed shapes of an activity diagram into scene items in paint order with roles for the tests (actions, start, stop, end, connectors, diamonds and hexagons, bars, split lines, arrows with heads and labels, notes, partition frames, swimlane fills, dividers, and titles), with their fills, strokes, and locations, skips the space that a shape occupies without paint, and sizes the view box to the geometry with the theme's background. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityRenderer.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Activity/ActivityRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/activity/activity-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/activity/activity-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/activity/activity-renderer.yaml */

        // The renderer builds scene items from the placed geometry
        dependency usesSceneModel from PlantUmlActivityRenderer to PlantUmlSceneModel;
        dependency usesActivityLayout from PlantUmlActivityRenderer to PlantUmlActivityLayout;
        dependency usesCanvas from PlantUmlActivityRenderer to Canvas;
    }
}
