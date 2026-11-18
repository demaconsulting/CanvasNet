package CanvasNet {
    part def PlantUmlSequenceLayout {
        doc /* The internal SequenceLayout of the PlantUML Sequence subsystem with the placed records (SequenceLayout.cs, SequenceLayout.Messages.cs, SequenceLayout.Blocks.cs). Run measures the texts with the theme's styles, works out the activation levels of every message, solves the column constraints in one pass in column order (neighbor gaps, message lengths of the text plus 24 and the activation boxes, self message loops, box edges and title margins, and group frames clearing the participants outside them), moves the leftmost extent to the 10-pixel margin, and places the rows from top to bottom with the geometry of PlantUML's teoz sequence engine (heads aligned at their bottom, messages, self messages, notes, activation boxes, group frames with tabs and sections, dividers, delays with dotted life lines, spaces, destroyed life lines, tails, and boxes). It counts its work against MaxLayoutWork (plantuml-limit-exceeded) and checks the cancellation token. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceLayout.cs, src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceLayout.Messages.cs, src/DemaConsulting.CanvasNet.PlantUml/Sequence/SequenceLayout.Blocks.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Sequence/SequenceLayoutTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/sequence/sequence-layout.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/sequence/sequence-layout.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/sequence/sequence-layout.yaml */

        // The layout measures text, reads the styles of the look, and places the model
        dependency usesTextLayout from PlantUmlSequenceLayout to PlantUmlTextLayout;
        dependency usesSkinTheme from PlantUmlSequenceLayout to PlantUmlSkinTheme;
        dependency usesSequenceModel from PlantUmlSequenceLayout to PlantUmlSequenceModel;
    }
}
