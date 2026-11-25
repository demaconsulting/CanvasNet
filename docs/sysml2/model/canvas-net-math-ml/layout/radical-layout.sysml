package CanvasNetMathMl_MathMlRadicalLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlStretchyGlyphs::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;

    part def MathMlRadicalLayout {
        doc /* The internal RadicalLayout of the MathML Layout subsystem. LayoutSquareRoot builds the box of msqrt around the row of its children as MathML Core 3.3.3 does: the radical symbol U+221A is stretched by StretchyGlyphs along the block axis over the height of the base plus RadicalVerticalGap (RadicalDisplayStyleVerticalGap in display style) and RadicalRuleThickness, with its base glyph, a size variant, or its glyph assembly of the MATH table (an assembly beyond MathMlLimits.MaxAssemblyParts fails with a located mathml-limit-exceeded error); the ascent is the base's plus the gap, the overbar, and RadicalExtraAscender, and the descent reaches the bottom of the symbol; the symbol is painted at the left with its top the extra ascender below the top of the box, the base follows it, and an overbar rule covers the base, in the color of the radical. As Chrome lays it out, the box is as wide as the base plus the widest construction of the symbol, while the base follows the symbol drawn (the advance of a glyph or a variant, the ink width of an assembly rounded out to whole pixels). LayoutRoot places the index of mroot before the square root of its base: after RadicalKernBeforeDegree (not negative), followed by RadicalKernAfterDegree (not less than minus the width of the index), with its bottom raised from the bottom of the square root by RadicalDegreeBottomRaisePercent (at Chrome's 1/64 precision) of the height of the symbol, which may raise the ascent. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/RadicalLayout.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/RadicalLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/radical-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/radical-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/radical-layout.yaml");
        }

        // The radical places child boxes and paints its symbol and overbar with the constants and glyphs of the math font
        dependency usesMathBox from MathMlRadicalLayout to MathMlMathBox;
        dependency usesMathFont from MathMlRadicalLayout to MathMlMathFont;

        // The radical symbol is stretched along the block axis
        dependency usesStretchyGlyphs from MathMlRadicalLayout to MathMlStretchyGlyphs;
        dependency usesOperatorDictionary from MathMlRadicalLayout to MathMlOperatorDictionary;
    }
}
