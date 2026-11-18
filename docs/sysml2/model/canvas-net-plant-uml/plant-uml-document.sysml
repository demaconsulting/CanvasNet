package CanvasNet {
    part def PlantUmlDocument {
        doc /* The public API of CanvasNetPlantUml in the DemaConsulting.CanvasNet.PlantUml namespace: the sealed, immutable, thread-safe PlantUmlDocument with the static Parse(string), Open(string path), and Open(Stream) entry points (strict UTF-8, byte-order mark dropped, bounded reads) and the metadata properties (SourceName, DiagramType, IsSupported, Title, NodeCount, EdgeCount, SubgraphCount); the validated PlantUmlOpenOptions, PlantUmlRenderOptions, and PlantUmlLimits; the PlantUmlLocation, PlantUmlDiagnostic, PlantUmlUnsupportedFeature, and PlantUmlSize records; the PlantUmlDiagnosticCodes constants; the PlantUmlSyntaxException, PlantUmlRenderException, and PlantUmlUnsupportedFeatureException types; and the internal PlantUmlReportSink (ordered, de-duplicated, capped delivery on the calling thread) with the PlantUmlPendingReport records that reading keeps for every call, and the pipeline seam (IPlantUmlDiagram and DiagramRegistry, which hands each supported type to its parser). */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDocument.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDocument.Pipeline.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlOpenOptions.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlRenderOptions.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlLimits.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlLocation.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDiagnostic.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlUnsupportedFeature.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlSize.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDiagnosticCodes.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlSyntaxException.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlRenderException.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlUnsupportedFeatureException.cs, src/DemaConsulting.CanvasNet.PlantUml/PlantUmlReportSink.cs, src/DemaConsulting.CanvasNet.PlantUml/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlDocumentTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlLimitsTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlRecordsAndExceptionsTests.cs, test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlReportSinkTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/plant-uml-document.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/plant-uml-document.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/plant-uml-document.yaml */

        // The document preprocesses and detects every text it opens
        dependency usesPreprocessor from PlantUmlDocument to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlDocument to PlantUmlDiagramDetector;
    }
}
