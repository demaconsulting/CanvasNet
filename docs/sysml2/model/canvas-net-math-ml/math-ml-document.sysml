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
        doc /* The public API of CanvasNetMathMl in the DemaConsulting.CanvasNet.MathMl namespace: the sealed, immutable, thread-safe MathMlDocument with the static Parse(string), Open(string path), and Open(Stream) entry points (strict UTF-8, byte-order mark dropped, bounded reads), the description properties (SourceName, Display, ElementCount, AltText, Annotations), Measure (the size and the baseline row), Render, and Render(width, height) (MathMlDocument.Render.cs: the reports of reading replayed first, the style of the math element from the options, the layout over the bundled math font, the picture as the content box united with the ink and rounded out, fail-closed mapping of layout defects to mathml-render-failed, delivery of the reports once on the calling thread); the MathMlDisplay enum and the MathMlAnnotation record; the validated MathMlOpenOptions, MathMlRenderOptions, and MathMlLimits; the MathMlLocation, MathMlDiagnostic, MathMlUnsupportedFeature, and MathMlSize records; the MathMlDiagnosticCodes constants; the MathMlSyntaxException, MathMlRenderException, and MathMlUnsupportedFeatureException types; and the internal MathMlReportSink (ordered, de-duplicated, capped delivery on the calling thread) with the MathMlPendingReport records that reading keeps for every call. */

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
                "src/DemaConsulting.CanvasNet.MathMl/NamespaceDoc.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlDocumentTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlRenderOptionsTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlLimitsTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlRecordsAndExceptionsTests.cs",
                "test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlReportSinkTests.cs"
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
    }
}
