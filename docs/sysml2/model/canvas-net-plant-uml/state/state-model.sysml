package CanvasNet {
    part def PlantUmlStateModel {
        doc /* The internal model of the PlantUML State subsystem (StateModel.cs, StateModel.Elements.cs): the diagram of states and pseudo-states (with their kinds, displays, descriptions, colors, and the regions of a composite state with their arrangement), notes, and transitions, with the page texts, the look, the direction, the layout settings, the scale, and hide empty description. The facts count the simple states and pseudo-states, the transitions that do not touch a note, and the composite states; Lower builds, for every call, the Svek diagram with a cluster per composite state with its StateRenderer image (and a cluster without image per concurrent region), an entity with its StateRenderer image per state and pseudo-state, the notes, and the transitions; Layout lays it out with the Svek engine and decorates it with the page texts with the spacing of PlantUML's graph diagrams. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/State/StateModel.cs, src/DemaConsulting.CanvasNet.PlantUml/State/StateModel.Elements.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/State/StateModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/state/state-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/state/state-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/state/state-model.yaml */

        // The model lowers itself to the Svek engine, draws its elements with the renderer, and decorates the page
        dependency usesSvekModel from PlantUmlStateModel to PlantUmlSvekModel;
        dependency usesSvekLayout from PlantUmlStateModel to PlantUmlSvekLayout;
        dependency usesSvekRenderer from PlantUmlStateModel to PlantUmlSvekRenderer;
        dependency usesStateRenderer from PlantUmlStateModel to PlantUmlStateRenderer;
        dependency usesSkinTheme from PlantUmlStateModel to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlStateModel to PlantUmlSceneModel;
        dependency usesTextLayout from PlantUmlStateModel to PlantUmlTextLayout;
    }
}