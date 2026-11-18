package CanvasNet {
    part def CanvasNetPlantUmlSystem {
        doc /* CanvasNetPlantUml is a static, offline, deterministic renderer for the PlantUML diagram language, delivered as the separate DemaConsulting.CanvasNet.PlantUml NuGet package. The public PlantUmlDocument unit parses PlantUML text from a string, a stream, or a file path (a located, coded, hinted PlantUmlSyntaxException for invalid text; an InvalidDataException for oversized text), taking the first @startuml block of the text (or the whole text when it has no @start tag) as the diagram, and describes the diagram without drawing it (type, supported flag, title, node, edge, and subgraph counts). Every PlantUML diagram type is recognized; the types this version does not draw are reported as plantuml-diagram-unsupported. Every recognized but unsupported construct is reported with a stable plantuml-* code and location, and strict mode throws. Untrusted input is bounded by PlantUmlLimits, and the library never uses the network. The internal subsystems are Syntax (framing, preprocessing, and detection). */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlSystemIntegrationTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml.yaml */
        comment /* Third-Party Provenance and Attribution: CanvasNetPlantUml is derived with attribution, not clean-room. Its framing, preprocessor subset, and detection order follow the MIT-licensed distribution of PlantUML (plantuml-mit). The license text and the provenance table ship in src/DemaConsulting.CanvasNet.PlantUml/third-party/, and every file with ported logic carries a Portions derived from header. The package is not affiliated with the PlantUML project. */
        comment /* CanvasNetPlantUml references the sibling CanvasNet system only: its Canvas subsystem (Surface, Rgba32). It has no OTS runtime dependency. */

        part plantUmlDocument : PlantUmlDocument;
        part plantUmlSyntax : PlantUmlSyntax;

        dependency usesCanvas from CanvasNetPlantUmlSystem to Canvas;
    }
}
