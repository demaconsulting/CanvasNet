package CanvasNet {
    part def PlantUmlSceneModel {
        doc /* The internal scene records of the PlantUML Scenegraph subsystem (SceneModel.cs): SceneRect, SceneSegmentKind and SceneSegment (move, line, cubic, close), SceneStroke (color, width, dashes), SceneFont, SceneTextRun (text, font, color, measured advance, underline, strike), the SceneItem base with its role, id, and location, ScenePath (segments, fill, stroke), SceneText (start, baseline, runs, box), and Scene (view box, items, background, and the diagram's own scale) with BoundsOf and ToPath; and the static SceneShapes builders of rectangles with square or rounded corners, ellipses (the four-arc cubic approximation), polygons, polylines, and lines. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/SceneModel.cs, src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Scenegraph/SceneModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/scenegraph/scene-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/scenegraph/scene-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/scenegraph/scene-model.yaml */

        // Scene colors are core colors and shapes become core paths
        dependency usesCanvas from PlantUmlSceneModel to Canvas;
        dependency usesGeometry from PlantUmlSceneModel to Geometry;
    }
}