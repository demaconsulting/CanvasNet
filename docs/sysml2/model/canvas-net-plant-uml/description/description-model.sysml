package CanvasNet {
    part def PlantUmlDescriptionModel {
        doc /* The internal model of the PlantUML Description subsystem (DescriptionModel.cs, DescriptionModel.Elements.cs): the diagram of elements (with their symbols, names, displays, stereotypes, colors, and containers), notes, and links, with the page texts, the look, the direction, the layout settings, and the scale. The facts count the leaf elements, the links that do not touch a note, and the containers; Lower builds, for every call, the Svek diagram with a cluster per container (parents first) with its DescriptionRenderer image, an entity with its DescriptionRenderer image per leaf element, the notes, and the links; Layout lays it out with the Svek engine and decorates it with the page texts with the spacing of PlantUML's graph diagrams. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionModel.cs, src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionModel.Elements.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Description/DescriptionModelTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/description/description-model.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/description/description-model.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/description/description-model.yaml */

        // The model lowers itself to the Svek engine, draws its elements with the renderer, and decorates the page
        dependency usesSvekModel from PlantUmlDescriptionModel to PlantUmlSvekModel;
        dependency usesSvekLayout from PlantUmlDescriptionModel to PlantUmlSvekLayout;
        dependency usesSvekRenderer from PlantUmlDescriptionModel to PlantUmlSvekRenderer;
        dependency usesDescriptionRenderer from PlantUmlDescriptionModel to PlantUmlDescriptionRenderer;
        dependency usesSkinTheme from PlantUmlDescriptionModel to PlantUmlSkinTheme;
        dependency usesSceneModel from PlantUmlDescriptionModel to PlantUmlSceneModel;
        dependency usesTextLayout from PlantUmlDescriptionModel to PlantUmlTextLayout;
    }
}