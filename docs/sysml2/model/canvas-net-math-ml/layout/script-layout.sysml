package CanvasNetMathMl_MathMlScriptLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;

    part def MathMlScriptLayout {
        doc /* The internal ScriptLayout of the MathML Layout subsystem. Layout builds the box of msub, msup, and msubsup from the boxes of their base and scripts as MathML Core 3.4.1 does: a subscript is shifted down by the largest of SubscriptShiftDown, its ascent less SubscriptTopMax, and, unless the base is a single character (a token that is not an operator and draws one glyph, whose subscript keeps the shift of the font as TeX sets it), SubscriptBaselineDropMin below the descent of the base; a superscript up by the largest of SuperscriptShiftUp (SuperscriptShiftUpCramped when the math-shift of the element is compact), SuperscriptBottomMin above its descent, and the ascent of the base less SuperscriptBaselineDropMax; with both, the gap SubSuperscriptGapMin is kept by raising the superscript while its bottom stays below SuperscriptBottomMaxWithSubscript, then lowering the subscript. The scripts follow the base, the subscript pulled back by the italic correction of a large operator and the superscript moved right by the italic correction of another operator (only operators drawn with a size variant or an assembly have one, as in Chrome) or, when its ink would otherwise overlap the ink at the end of a base that is not an operator (the hook of a zeta, the arm of a y, whatever the glyph of the superscript), by the italic correction of the base as MathML Core places it, and the element ends SpaceAfterScript after the base or the script that ends last as placed (unlike Chrome, which counts the whole of the wider script after the base and so leaves extra space after the lower limit of an integral); the extents are at least zero. LayoutMultiscripts builds the box of mmultiscripts as 3.4.3.1 does: the shifts are the largest over the pairs, the prescript pairs are aligned right in their columns before the base and the mprescripts box, and the postscript pairs follow the base, each with SpaceAfterScript. The box of an embellished element keeps the operator the engine gives it. The layout of an munder, mover, or munderover whose limits move to the script positions outside display style is the same. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/ScriptLayout.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/ScriptLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/script-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/script-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/script-layout.yaml");
        }

        // The scripts are placed as child boxes with the script constants of the math font
        dependency usesMathBox from MathMlScriptLayout to MathMlMathBox;
        dependency usesMathFont from MathMlScriptLayout to MathMlMathFont;

        // The computed style of the element gives the font size and the math shift
        dependency usesStyleResolver from MathMlScriptLayout to MathMlStyleResolver;
    }
}
