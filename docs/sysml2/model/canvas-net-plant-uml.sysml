package CanvasNet {
    part def CanvasNetPlantUmlSystem {
        doc /* CanvasNetPlantUml is a static, offline, deterministic renderer for the PlantUML diagram language, delivered as the separate DemaConsulting.CanvasNet.PlantUml NuGet package. The public PlantUmlDocument unit parses PlantUML text from a string, a stream, or a file path (a located, coded, hinted PlantUmlSyntaxException for invalid text; an InvalidDataException for oversized text), taking the first @startuml block of the text (or the whole text when it has no @start tag) as the diagram, and describes the diagram without drawing it (type, supported flag, title, node, edge, and subgraph counts), measures it, and renders it onto a CanvasNet Surface at its natural size times a scale or fitted inside an exact size, over the diagram's own background (white by default) or the caller's, with PlantUML's default look and the fonts bundled with CanvasNet only. Every PlantUML diagram type is recognized; the types this version does not draw are reported as plantuml-diagram-unsupported with an empty transparent picture of 20 by 20 pixels. Every recognized but unsupported construct is reported with a stable plantuml-* code and location, and strict mode throws. Untrusted input is bounded by PlantUmlLimits, local include files are read only under the caller's file-access policy, and the library never uses the network; the PlantUmlImageSource draws a linked diagram for the image resolvers of the other formats. The internal subsystems are Syntax (framing, preprocessing, and detection), Styling (the default skin and the skin parameters), Scenegraph (the scene model with the page texts, the text layout, and the painter), Sequence (the parser, model, layout, and renderer of sequence diagrams, with the geometry of PlantUML's teoz sequence engine), and Activity (the parser, model, layout, and renderer of activity diagrams of the new syntax, with the tile composition of PlantUML's ftile activity engine). */

        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlSystemIntegrationTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlUnsupportedDiagramTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlSequenceIntegrationTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlActivityIntegrationTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlReferenceLayoutTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlIncludeTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlHardeningTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlGalleryTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml.yaml */
        comment /* Third-Party Provenance and Attribution: CanvasNetPlantUml is derived with attribution, not clean-room. Its framing, preprocessor subset, detection order, default look (plantuml.skin), text metrics (Java's logical fonts on Windows), sequence geometry (the teoz engine), and activity tile composition (the ftile engine) follow the MIT-licensed distribution of PlantUML (plantuml-mit). The license text and the provenance table ship in src/DemaConsulting.CanvasNet.PlantUml/third-party/, and every file with ported logic carries a Portions derived from header. The package is not affiliated with the PlantUML project. */
        comment /* CanvasNetPlantUml references the sibling CanvasNet system only: its Canvas subsystem (Surface, Rgba32), Geometry (paths), Drawing (path filling and stroking), Fonts (the bundled fonts through a font resolver with no system fonts), and Rendering (text measuring and text paths), Files (the file reference resolver and the file-access policy of the include files), and ImageSources (the image source interface of the composition hook). It has no OTS runtime dependency. */

        part plantUmlDocument : PlantUmlDocument;
        part plantUmlSyntax : PlantUmlSyntax;
        part plantUmlStyling : PlantUmlStyling;
        part plantUmlScenegraph : PlantUmlScenegraph;
        part plantUmlSequence : PlantUmlSequence;
        part plantUmlActivity : PlantUmlActivity;

        dependency usesCanvas from CanvasNetPlantUmlSystem to Canvas;
        dependency usesDrawing from CanvasNetPlantUmlSystem to Drawing;
        dependency usesFonts from CanvasNetPlantUmlSystem to Fonts;
        dependency usesGeometry from CanvasNetPlantUmlSystem to Geometry;
        dependency usesRendering from CanvasNetPlantUmlSystem to Rendering;
        dependency usesFiles from CanvasNetPlantUmlSystem to Files;
        dependency usesImageSources from CanvasNetPlantUmlSystem to ImageSources;
    }
}
