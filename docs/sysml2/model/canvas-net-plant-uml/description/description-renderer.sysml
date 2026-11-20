package CanvasNet {
    part def PlantUmlDescriptionRenderer {
        doc /* The internal renderer of the PlantUML Description subsystem (DescriptionRenderer.cs, DescriptionRenderer.Symbols.cs, DescriptionRenderer.UseCase.cs). Image measures a leaf element with the text layout of a call and returns its Svek entity image, with its stereotypes (italic, centered) over its name or description: a component as USymbolComponent2 draws it (margins of 15, 25, 20, and 10, corners of 5, and the UML icon), an interface as an 18-pixel circle box with its name 8 below and its stereotypes 8 above (links end on the circle), an actor as the stick figure of ActorStickMan (shared with the sequence diagrams) between its stereotypes and its name, a business actor with the slash across its head, a use case as the smallest ellipse of the proportions of its texts around their footprint grown by 6 (TextBlockInEllipse), a business use case with its margins and slash, and the node, rectangle, folder, package, frame, cloud, database, and card with the margins and outlines of their USymbol classes; every other symbol is drawn as a rectangle. ContainerImage returns the Svek cluster image of a container: a component container with its outline, icon, stereotypes, and bold title, and the other containers as the Svek cluster symbols with stereotypes of 14, colored by the element or by the skin parameters of its symbol. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.cs, src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.Symbols.cs, src/DemaConsulting.CanvasNet.PlantUml/Description/DescriptionRenderer.UseCase.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Description/DescriptionRendererTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/description/description-renderer.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/description/description-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/description/description-renderer.yaml */

        // The renderer draws the model's elements as Svek entity and cluster images with the look and the text layout
        dependency usesDescriptionModel from PlantUmlDescriptionRenderer to PlantUmlDescriptionModel;
        dependency usesSvekModel from PlantUmlDescriptionRenderer to PlantUmlSvekModel;
        dependency usesSvekRenderer from PlantUmlDescriptionRenderer to PlantUmlSvekRenderer;
        dependency usesSkinTheme from PlantUmlDescriptionRenderer to PlantUmlSkinTheme;
        dependency usesTextLayout from PlantUmlDescriptionRenderer to PlantUmlTextLayout;
        dependency usesSceneModel from PlantUmlDescriptionRenderer to PlantUmlSceneModel;
        // The actor is the sequence diagram's stick figure
        dependency usesSequenceRenderer from PlantUmlDescriptionRenderer to PlantUmlSequenceRenderer;
    }
}