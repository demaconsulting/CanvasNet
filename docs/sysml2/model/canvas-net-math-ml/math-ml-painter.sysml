package CanvasNetMathMl_MathMlPainter {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathTree::*;

    part def MathMlPainter {
        doc /* The internal static MathMlPainter of CanvasNetMathMl with the MathPlacement record (the scale from CSS pixels to device pixels and the device position of the origin of the math box): it paints a laid-out box tree onto a Surface in four passes, each in tree order (the backgrounds, the glyph outlines scaled from font design units and filled with the nonzero rule, the rules, and the borders), skips phantom subtrees, counts the glyphs against MathMlLimits.MaxPaintedGlyphs before anything is painted (a located mathml-limit-exceeded MathMlRenderException), and checks the cancellation token every 64 items. The tree is collected iteratively, so its depth never threatens the stack. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/MathMlPainter.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/MathMlPainterTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/math-ml-painter.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/math-ml-painter.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/math-ml-painter.yaml");
        }

        // The painter draws the items of the box tree and locates a limit failure at the element of the box
        dependency usesMathBox from MathMlPainter to MathMlMathBox;
        dependency usesMathTree from MathMlPainter to MathMlMathTree;

        // Glyph outlines come from the fonts and become paths filled onto the surface
        dependency usesCanvas from MathMlPainter to CanvasNet_CanvasSubsystem::Canvas;
        dependency usesDrawing from MathMlPainter to CanvasNet_DrawingSubsystem::Drawing;
        dependency usesGeometry from MathMlPainter to CanvasNet_GeometrySubsystem::Geometry;
        dependency usesFonts from MathMlPainter to CanvasNet_FontsSubsystem::Fonts;
    }
}
