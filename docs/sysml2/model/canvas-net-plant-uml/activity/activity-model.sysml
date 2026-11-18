package CanvasNet {
    part def PlantUmlActivityModel {
        doc /* The internal ActivityModel of the PlantUML Activity subsystem with its instruction classes (ActivityModel.cs, ActivityModel.Elements.cs): the tree of instructions (lists, actions, start, stop, end, connectors, ifs with their branches, while and repeat loops, forks, splits, and groups) with their swimlanes, incoming arrows, and notes, the swimlanes in order, the page texts, and the look with the skin parameters applied. It implements the document's diagram interface: the title, the counts (actions, nodes, and decisions as nodes, arrows as edges, swimlanes and framed groups as subgraphs), the parser's reports in text order, and Layout, which lays the diagram out with PlantUmlActivityLayout, draws it with PlantUmlActivityRenderer, places the page texts with the spacing of an activity diagram, and sets the background and the diagram's own scale, afresh for every call. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityModel.cs, src/DemaConsulting.CanvasNet.PlantUml/Activity/ActivityModel.Elements.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Activity/ActivityModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/activity/activity-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/activity/activity-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/activity/activity-model.yaml */

        // The model lays itself out, draws itself, and places its page texts with the look of the diagram
        dependency usesActivityLayout from PlantUmlActivityModel to PlantUmlActivityLayout;
        dependency usesActivityRenderer from PlantUmlActivityModel to PlantUmlActivityRenderer;
        dependency usesSceneModel from PlantUmlActivityModel to PlantUmlSceneModel;
        dependency usesSkinTheme from PlantUmlActivityModel to PlantUmlSkinTheme;
    }
}
