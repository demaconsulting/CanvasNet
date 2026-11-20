package CanvasNet {
    part def PlantUmlClass {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.PlantUml.Class namespace that draws PlantUML class and object diagrams. The PlantUmlClassParser unit reads the statements of a class or object diagram (classes and their kin, objects, maps, members, packages and namespaces with qualified names, relationships, notes, hide and show, the page texts, the direction, the scale, and the skin parameters) into a model, reporting what it reads but does not draw and bounding the diagram by the node, edge, label, and nesting limits; the PlantUmlClassModel unit holds the diagram with its member, body, and hide and show rules, counts its facts, and lowers it for every call to the Svek engine, which lays it out and draws its links, packages, and notes; the PlantUmlClassRenderer unit draws the class, object, and map boxes as PlantUML's EntityImageClass, EntityImageObject, and EntityImageMap draw them. The subsystem depends on the Svek subsystem (the shared engine), the Syntax subsystem (statements and detection), the Styling subsystem (the look), and the Scenegraph subsystem (scenes, text layout, and page texts). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Class/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Class/ClassTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/Class/ClassTestHelpers.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/class.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/class.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/class.yaml */

        part classParser : PlantUmlClassParser;
        part classModel : PlantUmlClassModel;
        part classRenderer : PlantUmlClassRenderer;
    }
}