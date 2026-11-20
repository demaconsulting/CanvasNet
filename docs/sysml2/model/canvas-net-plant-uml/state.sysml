package CanvasNet {
    part def PlantUmlState {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.State namespace that draws PlantUML state diagrams. The PlantUmlStateParser unit reads the statements of a state diagram (states with display names, descriptions, stereotypes, and colors; transitions; the start, end, and history pseudo-states of every composite state; choices, forks, joins, and junctions; composite states with concurrent regions; notes; hide empty description; the page texts, the direction, the scale, and the skin parameters) into a model with PlantUML's qualified ids, reporting what it reads but does not draw, refusing a transition between concurrent regions, and bounding the diagram by the node, edge, label, and nesting limits; the PlantUmlStateModel unit holds the diagram, counts its facts, and lowers it for every call to the Svek engine (composite states and their concurrent regions as clusters), which lays it out and draws its transitions, notes, and region separators; the PlantUmlStateRenderer unit draws the state boxes, the pseudo-states, and the composite states as PlantUML's EntityImageState, EntityImageCircleStart, EntityImageCircleEnd, EntityImageBranch, EntityImageSynchroBar, EntityImagePseudoState, and Cluster draw them. The subsystem depends on the Svek subsystem (the shared engine), the Syntax subsystem (statements and detection), the Styling subsystem (the look), and the Scenegraph subsystem (scenes, text layout, and page texts). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/State/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/State/StateTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/State/StateTestHelpers.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/state.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/state.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/state.yaml */

        part stateParser : PlantUmlStateParser;
        part stateModel : PlantUmlStateModel;
        part stateRenderer : PlantUmlStateRenderer;
    }
}