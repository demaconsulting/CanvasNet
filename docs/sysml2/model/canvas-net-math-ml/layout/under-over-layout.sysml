package CanvasNetMathMl_MathMlUnderOverLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;

    part def MathMlUnderOverLayout {
        doc /* The internal UnderOverLayout of the MathML Layout subsystem. StretchInline lays the children of munder, mover, and munderover that are embellished operators stretchy along the inline axis out again, through the relayout of the engine, with the width of the widest other child, or, when every child stretches, of the widest of them laid out with a zero constraint (MathML Core 3.4.2.2); a base laid out with the constraint of its parent does not stretch here but still counts toward the width (a documented interpretation of 3.4.2.2). Layout builds the box from the boxes of the base and the scripts as 3.4.2.3 to 3.4.2.5 do: the children are centered on the base, the limits moved by half the italic correction of a large operator; the limits of a large operator are UpperLimitGapMin and LowerLimitGapMin away with their baselines at least UpperLimitBaselineRiseMin and LowerLimitBaselineDropMin from it; the scripts of an operator stretchy along the inline axis use the stretch stack constants; otherwise an accent overscript sits on the base raised so that its bottom is at least AccentBaseHeight high, an accent underscript touches the base, and other scripts are OverbarVerticalGap and UnderbarVerticalGap away, with OverbarExtraAscender and UnderbarExtraDescender outside them. As in Chrome, an overscript is centered rather than placed by its top accent attachment, the gaps are measured between the boxes, and the extents are at least zero. The box of an embellished element keeps the operator the engine gives it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/UnderOverLayout.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/UnderOverLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/under-over-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/under-over-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/under-over-layout.yaml");
        }

        // The scripts are placed as child boxes with the limit, stretch stack, overbar, underbar, and accent constants
        dependency usesMathBox from MathMlUnderOverLayout to MathMlMathBox;
        dependency usesMathFont from MathMlUnderOverLayout to MathMlMathFont;

        // The stretch axis of an operator decides whether it stretches horizontally and which constants apply
        dependency usesOperatorDictionary from MathMlUnderOverLayout to MathMlOperatorDictionary;

        // The computed style of the element gives the font size
        dependency usesStyleResolver from MathMlUnderOverLayout to MathMlStyleResolver;
    }
}
