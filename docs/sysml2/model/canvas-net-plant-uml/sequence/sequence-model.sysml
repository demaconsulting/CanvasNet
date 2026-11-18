package CanvasNet {
    part def PlantUmlSequenceModel {
        doc /* The internal SequenceModel of the PlantUML Sequence subsystem with its element records (SequenceModel.cs, SequenceModel.Elements.cs): the participants in drawing order (code, display, kind, fill, order), the boxes, and the events from top to bottom (messages with their arrow style, number, and life changes; standalone life changes; notes; group starts, sections, and ends; dividers; delays; spaces), the page texts, and the look with the skin parameters applied. It implements the document's diagram interface: the title, the counts (participants as nodes, messages as edges, boxes as subgraphs), the parser's reports in text order, and Layout, which lays the diagram out with PlantUmlSequenceLayout, draws it with PlantUmlSequenceRenderer, places the page texts, and sets the background and the diagram's own scale, afresh for every call. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceModel.cs, src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceModel.Elements.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Sequence/SequenceModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/sequence/sequence-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/sequence/sequence-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/sequence/sequence-model.yaml */

        // The model lays itself out, draws itself, and places its page texts with the look of the diagram
        dependency usesSequenceLayout from PlantUmlSequenceModel to PlantUmlSequenceLayout;
        dependency usesSequenceRenderer from PlantUmlSequenceModel to PlantUmlSequenceRenderer;
        dependency usesSceneModel from PlantUmlSequenceModel to PlantUmlSceneModel;
        dependency usesSkinTheme from PlantUmlSequenceModel to PlantUmlSkinTheme;
    }
}
