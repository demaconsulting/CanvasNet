package CanvasNet {
    part def PlantUmlScenePainter {
        doc /* The internal ScenePainter of the PlantUML Scenegraph subsystem with the ScenePlacement record (ScenePainter.cs, ScenePainter.Text.cs). Paint fills a new Surface with the background and draws the items in order: the fill and the stroke (with scaled dashes, drawn solid when finer than a pixel fraction or too many) of every path, and the runs of every text line with the faces the text layout resolves, at their measured advances, with a bar for an underlined or struck run. ScenePlacement places a scene at a uniform scale or fitted and centered into a size. Items with a coordinate that is not finite or beyond 32,768 device pixels are skipped; the device pixels of every item's box are charged to PlantUmlLimits.MaxPaintedPixels before it is painted (a located plantuml-limit-exceeded failure); cancellation is checked before every item. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/ScenePainter.cs, src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/ScenePainter.Text.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Scenegraph/ScenePainterTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/scenegraph/scene-painter.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/scenegraph/scene-painter.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/scenegraph/scene-painter.yaml */

        // The painter draws scenes onto core surfaces with the core filler, stroker, and text paths
        dependency usesSceneModel from PlantUmlScenePainter to PlantUmlSceneModel;
        dependency usesTextLayout from PlantUmlScenePainter to PlantUmlTextLayout;
        dependency usesCanvas from PlantUmlScenePainter to Canvas;
        dependency usesDrawing from PlantUmlScenePainter to Drawing;
        dependency usesRendering from PlantUmlScenePainter to Rendering;
    }
}