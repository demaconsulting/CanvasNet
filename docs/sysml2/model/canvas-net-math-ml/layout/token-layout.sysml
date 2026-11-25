package CanvasNetMathMl_MathMlTokenLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;
    private import CanvasNetMathMl_MathMlStretchyGlyphs::*;
    private import CanvasNetMathMl_MathMlAttributeValues::*;

    part def MathMlTokenLayout {
        doc /* The internal TokenLayout of the MathML Layout subsystem with the MathVariants tables and the MathVariant enum (MathVariants.cs and the generated MathVariants.Data.cs: the italic table of MathML Core Appendix C.1 and the 18 mathvariant values of MathML 3 derived from the font decompositions of UnicodeData.txt, the letterlike symbols filling the holes). LayoutToken lays out mi, mn, mo, mtext, ms, and mspace, and LayoutText lays out text directly inside a layout element: each character is transformed by the mathvariant in effect or, for a single-character mi without one, to its mathematical italic, then drawn with the glyph of the math font or of the first bundled fallback font that has one, placed by its advance without kerning or substitution. An invisible format character takes no space and draws nothing; a character no bundled font has is drawn as the missing-glyph box and reported as mathml-text-missing-glyph. The box is the sum of the advances wide with the ink ascent and descent rounded out to whole CSS pixels (as Chrome lays tokens out, also for a text with a space); a single glyph gives its italic correction and top accent attachment. ms draws its text without quotes; mspace is a blank box of its width, height, and depth; mo is an embellished operator: LayoutOperator resolves its properties from the operator dictionary for its text and its form (the form attribute, else the form its row inferred), overridden by its form, fence, separator, lspace, rspace, stretchy, symmetric, largeop, movablelimits, minsize, and maxsize attributes; with a block stretch size constraint a stretchy operator of one character with a block stretch axis is stretched by StretchyGlyphs to the target of MathML Core 3.2.4.3 (symmetric about the math axis when asked, scaled to minsize and maxsize), centered on the target and in a box of its preferred inline size, or keeps its glyph centered on the target and is reported as mathml-stretch-unavailable when nothing can stretch it; a largeop operator in display style takes its large variant centered on the math axis. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Layout/TokenLayout.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/MathVariants.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/MathVariants.Data.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/TokenLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/token-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/token-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/token-layout.yaml");
        }

        // A token reads its element, its style, and the lengths the resolver resolves
        dependency usesMathTree from MathMlTokenLayout to MathMlMathTree;
        dependency usesStyleResolver from MathMlTokenLayout to MathMlStyleResolver;

        // The glyphs and their metrics come from the math font; the result is a box with glyph items
        dependency usesMathFont from MathMlTokenLayout to MathMlMathFont;
        dependency usesMathBox from MathMlTokenLayout to MathMlMathBox;
        dependency usesFonts from MathMlTokenLayout to CanvasNet_FontsSubsystem::Fonts;

        // An operator reads the dictionary and its boolean attributes, and is stretched or enlarged by StretchyGlyphs
        dependency usesOperatorDictionary from MathMlTokenLayout to MathMlOperatorDictionary;
        dependency usesStretchyGlyphs from MathMlTokenLayout to MathMlStretchyGlyphs;
        dependency usesAttributeValues from MathMlTokenLayout to MathMlAttributeValues;
    }
}
