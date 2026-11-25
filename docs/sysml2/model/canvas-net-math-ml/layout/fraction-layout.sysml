package CanvasNetMathMl_MathMlFractionLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;

    part def MathMlFractionLayout {
        doc /* The internal FractionLayout of the MathML Layout subsystem. ResolveThickness resolves the linethickness attribute of mfrac as a length or a percentage of the default FractionRuleThickness of the MATH table: an absent value keeps the default, an invalid one (including the MathML 3 names thin, medium, and thick) keeps it with a mathml-attribute-invalid warning, and a negative value is zero, as Chrome draws it. Layout builds the box of mfrac from the boxes of its numerator and denominator as MathML Core 3.3.2 does: with a nonzero thickness (3.3.2.1) the numerator is shifted up by the larger of FractionNumeratorShiftUp and the shift that keeps FractionNumeratorGapMin between it and the bar, the denominator down likewise with FractionDenominatorShiftDown and FractionDenominatorGapMin (the display-style constants in display style), and a bar of the thickness is painted centered on the math axis across the content box in the color of the fraction; with a zero thickness (3.3.2.2) the children take the stack shifts StackTopShiftUp and StackBottomShiftDown, both increased by half of the gap missing to StackGapMin, and no bar is painted. The children are centered in a content box as wide as the wider child, the user agent padding of 1 px is added on each side, and the box of an embellished fraction keeps the operator the engine gives it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/FractionLayout.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/FractionLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/fraction-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/fraction-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/fraction-layout.yaml");
        }

        // The fraction places child boxes and paints its bar with the constants of the math font
        dependency usesMathBox from MathMlFractionLayout to MathMlMathBox;
        dependency usesMathFont from MathMlFractionLayout to MathMlMathFont;

        // linethickness is read from the element and resolved, and reported, by the style resolver
        dependency usesMathTree from MathMlFractionLayout to MathMlMathTree;
        dependency usesStyleResolver from MathMlFractionLayout to MathMlStyleResolver;
    }
}
