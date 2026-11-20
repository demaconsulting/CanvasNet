package CanvasNet {
    part def PlantUmlStateRenderer {
        doc /* The internal renderer of the PlantUML State subsystem (StateRenderer.cs). Image measures a state or a pseudo-state with the text layout of a call and returns its Svek entity image: a state as PlantUML's EntityImageState draws it (a box with corners of 25, at least 50 by 50, the name above a line and the descriptions below it, a line of four dashes drawn as a rule) or, after hide empty description, as EntityImageStateEmptyDescription draws it; the start as a filled circle of 20, the end as a ring of 22 around a filled circle of 12, a junction as a filled circle of 12, a choice as a diamond of 24, a fork or a join as a bar of 80 by 8 across the ranks, and a history as a circle of 22 with H or H*. CompositeImage returns the Svek cluster image of a composite state: a header band with rounded top corners, a rounded outline, a line under the name, and the title, padded 10. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/State/StateRenderer.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/State/StateRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/state/state-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/state/state-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/state/state-renderer.yaml */

        // The renderer draws the model's elements as Svek entity and cluster images with the look and the text layout
        dependency usesStateModel from PlantUmlStateRenderer to PlantUmlStateModel;
        dependency usesSvekModel from PlantUmlStateRenderer to PlantUmlSvekModel;
        dependency usesSkinTheme from PlantUmlStateRenderer to PlantUmlSkinTheme;
        dependency usesTextLayout from PlantUmlStateRenderer to PlantUmlTextLayout;
        dependency usesSceneModel from PlantUmlStateRenderer to PlantUmlSceneModel;
    }
}