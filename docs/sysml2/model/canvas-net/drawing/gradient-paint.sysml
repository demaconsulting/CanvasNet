package CanvasNet_GradientPaint {
    private import SoftwareItemMetadata::*;

    part def GradientPaint {
        doc /* Public Gradient abstract base type plus its LinearGradient/RadialGradient subtypes and supporting GradientStop/GradientSpread types, and the internal GradientEvaluator helper that resolves a gradient's color per pixel; consumed by PathFiller's gradient Fill overload. None has independent externally visible behavior of its own outside that consumption. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Drawing/GradientSpread.cs",
                "src/DemaConsulting.CanvasNet/Drawing/GradientStop.cs",
                "src/DemaConsulting.CanvasNet/Drawing/Gradient.cs",
                "src/DemaConsulting.CanvasNet/Drawing/LinearGradient.cs",
                "src/DemaConsulting.CanvasNet/Drawing/RadialGradient.cs",
                "src/DemaConsulting.CanvasNet/Drawing/GradientEvaluator.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Tests/Drawing/GradientStopTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/LinearGradientTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/RadialGradientTests.cs",
                "test/DemaConsulting.CanvasNet.Tests/Drawing/GradientEvaluatorTests.cs"
            );
            designFiles = ("docs/design/canvas-net/drawing/gradient-paint.md");
            verificationFiles = ("docs/verification/canvas-net/drawing/gradient-paint.md");
            requirementFiles = ("docs/reqstream/canvas-net/drawing/gradient-paint.yaml");
        }
    }
}
