package CanvasNetPlantUml_PlantUmlDocument {
    private import SoftwareItemMetadata::*;
    private import CanvasNetPlantUml_PlantUmlDiagramDetector::*;
    private import CanvasNetPlantUml_PlantUmlPreprocessor::*;
    private import CanvasNetPlantUml_PlantUmlScenePainter::*;
    private import CanvasNetPlantUml_PlantUmlTextLayout::*;

    part def PlantUmlDocument {
        doc /* The public API of CanvasNetPlantUml in the DemaConsulting.CanvasNet.PlantUml namespace: the sealed, immutable, thread-safe PlantUmlDocument with the static Parse(string), Open(string path), and Open(Stream) entry points (strict UTF-8, byte-order mark dropped, bounded reads) the metadata properties (SourceName, DiagramType, IsSupported, Title, NodeCount, EdgeCount, SubgraphCount), Measure, Render, and Render(width, height) (PlantUmlDocument.Render.cs: the reports of reading replayed first, the layout of a supported diagram or the empty transparent 20 by 20 scene of a type that is not drawn, fail-closed mapping of layout defects to plantuml-render-failed, the background of the call or of the diagram); the validated PlantUmlOpenOptions (with the BaseDirectory and the FileAccess policy of the include files), PlantUmlRenderOptions, and PlantUmlLimits; the PlantUmlImageSource (an IImageSource named plantuml for .puml, .plantuml, and .pu that draws a linked diagram at the size of its placement over a transparent background, with its includes in the folder of the request under its policy, and its warnings and unsupported features as notes); the PlantUmlLocation, PlantUmlDiagnostic, PlantUmlUnsupportedFeature, and PlantUmlSize records; the PlantUmlDiagnosticCodes constants; the PlantUmlSyntaxException, PlantUmlRenderException, and PlantUmlUnsupportedFeatureException types; and the internal PlantUmlReportSink (ordered, de-duplicated, capped delivery on the calling thread) with the PlantUmlPendingReport records that reading keeps for every call, and the pipeline seam (IPlantUmlDiagram with its Layout, DiagramRenderContext, and DiagramRegistry, which hands each supported type to its parser). */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDocument.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDocument.Render.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDocument.Pipeline.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlOpenOptions.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlRenderOptions.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlLimits.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlLocation.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDiagnostic.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlUnsupportedFeature.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlSize.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlDiagnosticCodes.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlSyntaxException.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlRenderException.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlUnsupportedFeatureException.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlReportSink.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/PlantUmlImageSource.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/NamespaceDoc.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlDocumentTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlRenderOptionsTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlLimitsTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlRecordsAndExceptionsTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlReportSinkTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/PlantUmlImageSourceTests.cs"
            );
            designFiles = ("docs/design/canvas-net-plant-uml/plant-uml-document.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/plant-uml-document.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/plant-uml-document.yaml");
        }

        // The document preprocesses and detects every text it opens
        dependency usesPreprocessor from PlantUmlDocument to PlantUmlPreprocessor;
        dependency usesDiagramDetector from PlantUmlDocument to PlantUmlDiagramDetector;

        // Every measure and render lays text out and paints the scene with the call's own text layout and painter
        dependency usesTextLayout from PlantUmlDocument to PlantUmlTextLayout;
        dependency usesScenePainter from PlantUmlDocument to PlantUmlScenePainter;
        dependency usesCanvas from PlantUmlDocument to CanvasNet_CanvasSubsystem::Canvas;

        // The open options carry the file-access policy of the include files; the image source plugs into the image resolvers
        dependency usesFiles from PlantUmlDocument to CanvasNet_FilesSubsystem::Files;
        dependency usesImageSources from PlantUmlDocument to CanvasNet_ImageSourcesSubsystem::ImageSources;
    }
}
