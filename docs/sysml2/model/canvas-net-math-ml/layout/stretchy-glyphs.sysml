package CanvasNetMathMl_MathMlStretchyGlyphs {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;

    part def MathMlStretchyGlyphs {
        doc /* The internal static StretchyGlyphs of the MathML Layout subsystem with the StretchKind enum, the StretchGlyph value, and the StretchResult record (StretchyGlyphs.cs and the generated StretchyGlyphs.Fallback.cs: the 41 Unicode-based glyph assemblies of MathML Core Appendix B.4). Stretch implements the algorithm to shape a stretchy glyph of MathML Core 5.3.2 along the block or the inline axis: without a construction in the math font, a rule for a line character, or a B.4 entry it fails; otherwise the base glyph when it covers the target, else the first size variant of the MATH table whose advance measurement covers it, else the glyph assembly, else the last one tried; a character without a construction in the math font stretches as a rule of its own thickness when it is a line character (low line, macron, combining low line, horizontal bar, overline) and with its Unicode-based assembly otherwise. SolveAssembly implements MathML Core 5.3.1: every extender repeated the fewest times that reach the target at the minimum overlap and one overlap spread evenly between MinConnectorOverlap and the shortest connector that joins two glyphs (the connectors at the outer ends of the assembly join nothing and are left out; a shorter connector lowers the minimum), refusing more than MathMlLimits.MaxAssemblyParts glyphs with a located mathml-limit-exceeded MathMlRenderException before any glyph is placed; vertical parts are placed from the bottom with the bottom of their ink on the end of the previous part, horizontal parts from the left, a horizontal assembly taking the ink of its parts, rounded out, as its ascent and descent. LargeVariant finds the large variant of an operator in display style, Glyph the base glyph, and PreferredInlineSize the widest construction. The horizontal stretching serves the operators of the under- and overscript elements. The class is stateless and safe for concurrent use. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Layout/StretchyGlyphs.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/StretchyGlyphs.Fallback.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/StretchyGlyphsTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/stretchy-glyphs.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/stretchy-glyphs.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/stretchy-glyphs.yaml");
        }

        // The glyphs, their ink and advances, the constructions, and the constants come from the math font
        dependency usesMathFont from MathMlStretchyGlyphs to MathMlMathFont;
        dependency usesMathBox from MathMlStretchyGlyphs to MathMlMathBox;
        dependency usesOperatorDictionary from MathMlStretchyGlyphs to MathMlOperatorDictionary;
        dependency usesFonts from MathMlStretchyGlyphs to CanvasNet_FontsSubsystem::Fonts;
    }
}
