package CanvasNet {
    part def PlantUmlActivityLayout {
        doc /* The internal ActivityLayout of the PlantUML Activity subsystem with its tiles, canvas, and shapes (ActivityLayout.cs, ActivityLayout.Build.cs, ActivityLayout.Branches.cs, ActivityLayout.IfTiles.cs, ActivityLayout.Loops.cs, ActivityLayout.Parallel.cs, ActivityLayout.Notes.cs, ActivityLayout.Leaves.cs, ActivityLayout.Tiles.cs, ActivityLayout.Canvas.cs, ActivityLayout.Shapes.cs, ActivityLayout.Worm.cs, ActivityLayout.Swimlanes.cs). Run ports the tile (ftile) composition of PlantUML's activity engine: it builds a tile for every instruction (boxes, circles, diamonds, hexagons, bars, notes, partition frames), assembles the tiles of a list 35 pixels apart with arrows, composes ifs, elseif chains, while and repeat loops, forks, merges, and splits with their connections, draws every swimlane's tiles in its column with dividers and centered titles and the arrows between swimlanes across them, merges and simplifies the arrows, removes the empty bands of the drawing on both axes, and places its extent at PlantUML's margins. It counts its work against MaxLayoutWork (plantuml-limit-exceeded) and checks the cancellation token. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Build.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Branches.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.IfTiles.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Loops.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Parallel.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Notes.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Leaves.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Tiles.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Canvas.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Shapes.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Worm.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityLayout.Swimlanes.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Activity/ActivityLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/activity/activity-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/activity/activity-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/activity/activity-layout.yaml */

        // The layout measures text, reads the styles of the look, and places the model
        dependency usesTextLayout from PlantUmlActivityLayout to PlantUmlTextLayout;
        dependency usesSkinTheme from PlantUmlActivityLayout to PlantUmlSkinTheme;
        dependency usesActivityModel from PlantUmlActivityLayout to PlantUmlActivityModel;
    }
}
