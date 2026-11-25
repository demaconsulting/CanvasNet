package CanvasNetMathMl_MathMlLayoutSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetMathMl_MathMlLayoutEngine::*;
    public import CanvasNetMathMl_MathMlStyleResolver::*;
    public import CanvasNetMathMl_MathMlMathFont::*;
    public import CanvasNetMathMl_MathMlMathBox::*;
    public import CanvasNetMathMl_MathMlTokenLayout::*;
    public import CanvasNetMathMl_MathMlRowLayout::*;

    part def MathMlLayout {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.MathMl.Layout namespace that lays the equation tree out into a tree of boxes as MathML Core does, in CSS pixels at the font size of the call. The MathMlLayoutEngine unit walks the elements, computes their styles, dispatches each to its layout, lays every element this version does not draw out as a row with a report, reports the attributes without effect, and charges the work budget; the MathMlStyleResolver unit computes the inherited math style, math depth, font size, colors, mathvariant, and the inline style subset; the MathMlMathFont unit is the bundled Noto Sans Math with its MATH constants, glyph metrics, italic corrections, top accent attachments, and the deterministic bundled fallback; the MathMlMathBox unit is the immutable box and paint-item model; the MathMlTokenLayout unit lays out the token elements with the automatic italic and the mathvariant mappings; and the MathMlRowLayout unit lays out rows, the grouping elements, mpadded, and merror. The subsystem is called by MathMlDocument; its boxes are painted by MathMlPainter. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/LayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout.yaml");
        }

        part layoutEngine : MathMlLayoutEngine;
        part styleResolver : MathMlStyleResolver;
        part mathFont : MathMlMathFont;
        part mathBox : MathMlMathBox;
        part tokenLayout : MathMlTokenLayout;
        part rowLayout : MathMlRowLayout;
    }
}
