package CanvasNetMathMl_MathMlDocument {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlReader::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlLayoutEngine::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlPainter::*;

    part def MathMlDocument {
        doc /* The public API of CanvasNetMathMl in the DemaConsulting.CanvasNet.MathMl namespace: the sealed, immutable, thread-safe MathMlDocument with the static Parse(string), Open(string path), and Open(Stream) entry points (strict UTF-8, byte-order mark dropped, bounded reads), the description properties (SourceName, Display, ElementCount, AltText, Annotations), Measure (the size and the baseline row), Render, and Render(width, height) (MathMlDocument.Render.cs: the reports of reading replayed first, the style of the math element from the options, the layout over the bundled math font, the picture as the content box united with the ink and rounded out, fail-closed mapping of layout defects to mathml-render-failed, delivery of the reports once on the calling thread); the MathMlDisplay enum and the MathMlAnnotation record; the validated MathMlOpenOptions, MathMlRenderOptions, and MathMlLimits; the MathMlLocation, MathMlDiagnostic, MathMlUnsupportedFeature, and MathMlSize records; the MathMlDiagnosticCodes constants; the MathMlSyntaxException, MathMlRenderException, and MathMlUnsupportedFeatureException types; the internal MathMlReportSink (ordered, de-duplicated, capped delivery on the calling thread) with the MathMlPendingReport records that reading keeps for every call; the internal MeasureNatural (the picture size without the surface limit); and MathMlImageSource, the image source (IImageSource, IFragmentImageSource) named mathml that claims .mml and the display fragment parameter (block or inline, any other value refused with the invalid-fragment code before the file is read), reads only the content of the request, draws the equation on a transparent background at its natural size or fitted into the requested size within the pixel budget, and returns the reports as mathml-warning notes (the specific code in the detail) and unsupported-feature notes, both located, honoring strict mode and cancellation. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/MathMlDocument.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlDocument.Render.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlDisplay.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlAnnotation.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlOpenOptions.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlRenderOptions.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlLimits.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlLocation.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlDiagnostic.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlUnsupportedFeature.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlSize.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlDiagnosticCodes.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlSyntaxException.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlRenderException.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlUnsupportedFeatureException.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlReportSink.cs",
                "src/DemaConsulting.CanvasNet.MathMl/MathMlImageSource.cs",
                "src/DemaConsulting.CanvasNet.MathMl/NamespaceDoc.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlDocumentTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlRenderOptionsTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlLimitsTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlRecordsAndExceptionsTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlReportSinkTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlImageSourceTests.cs"
            );
            designFiles = ("docs/design/canvas-net-math-ml/math-ml-document.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/math-ml-document.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/math-ml-document.yaml");
        }

        // The document reads every text into the equation tree and keeps the tree
        dependency usesReader from MathMlDocument to MathMlReader;
        dependency usesMathTree from MathMlDocument to MathMlMathTree;

        // Every measure and render resolves the style of the math element, lays the tree out over the math font, and paints the boxes
        dependency usesStyleResolver from MathMlDocument to MathMlStyleResolver;
        dependency usesLayoutEngine from MathMlDocument to MathMlLayoutEngine;
        dependency usesMathFont from MathMlDocument to MathMlMathFont;
        dependency usesMathBox from MathMlDocument to MathMlMathBox;
        dependency usesPainter from MathMlDocument to MathMlPainter;
        dependency usesCanvas from MathMlDocument to CanvasNet_CanvasSubsystem::Canvas;

        // The image source implements the image-source contract of the core, so that other documents embed equations
        dependency usesImageSources from MathMlDocument to CanvasNet_ImageSourcesSubsystem::ImageSources;
    }
}
