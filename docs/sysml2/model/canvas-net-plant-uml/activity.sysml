package CanvasNet {
    part def PlantUmlActivity {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Activity namespace that draws PlantUML activity diagrams of the new syntax. The PlantUmlActivityParser unit reads the activity statements (start, stop, end, actions, ifs with elseif and else, while and repeat loops with backward actions, forks, merges, splits, arrows with labels and styles, swimlanes, notes, connectors, partitions, kill and detach, page texts, scale, and skin parameters) into a PlantUmlActivityModel, reporting the statements that are not drawn and refusing invalid ones with located errors; the PlantUmlActivityModel unit holds the tree of instructions and the swimlanes, gives the facts (actions, nodes, and decisions as nodes, arrows as edges, swimlanes and partitions as subgraphs), and lays the diagram out for every call; the PlantUmlActivityLayout unit composes the tiles of the instructions, draws them in their swimlanes, and compresses the drawing as PlantUML's activity engine (ftile) does, within the layout work limit; the PlantUmlActivityRenderer unit turns the placed shapes into scene items. The subsystem depends on the Syntax subsystem (statements and detection helpers), the Styling subsystem (the look), and the Scenegraph subsystem (scenes, text layout, and page texts); the document's diagram registry calls it for the activity type. */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Activity/ActivityTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/activity.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/activity.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/activity.yaml */

        part activityParser : PlantUmlActivityParser;
        part activityModel : PlantUmlActivityModel;
        part activityLayout : PlantUmlActivityLayout;
        part activityRenderer : PlantUmlActivityRenderer;
    }
}
