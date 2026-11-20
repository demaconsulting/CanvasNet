package CanvasNet {
    part def PlantUmlDescription {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Description namespace that draws PlantUML description diagrams (the use case, component, and deployment diagrams; this version draws the component diagrams). The PlantUmlDescriptionParser unit reads every statement of a description diagram (elements of every symbol with their display names, stereotypes, colors, and multi-line descriptions; the shorthands [C], () I, (U), and :A: with their business forms; containers with blocks; links with every decoration; notes; the page texts, the direction, the scale, and the skin parameters) into a model with PlantUML's qualified ids, giving a name only written in links the symbol PlantUML gives it, reporting what it reads but does not draw, refusing malformed statements, a redeclaration with another symbol, and statements of other diagram types, and bounding the diagram by the node, edge, label, and nesting limits; the PlantUmlDescriptionModel unit holds the diagram, counts its facts, and lowers it for every call to the Svek engine (containers as clusters), which lays it out and draws its links and notes; the PlantUmlDescriptionRenderer unit draws the components, interfaces, and leaf symbols and the component container as PlantUML's EntityImageDescription and USymbol classes draw them. The subsystem depends on the Svek subsystem (the shared engine), the Syntax subsystem (statements and detection), the Styling subsystem (the look), and the Scenegraph subsystem (scenes, text layout, and page texts). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Description/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Description/DescriptionTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/Description/DescriptionTestHelpers.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/description.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/description.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/description.yaml */

        part descriptionParser : PlantUmlDescriptionParser;
        part descriptionModel : PlantUmlDescriptionModel;
        part descriptionRenderer : PlantUmlDescriptionRenderer;
    }
}