package CanvasNetMathMl_MathMlLayoutSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetMathMl_MathMlLayoutEngine::*;
    public import CanvasNetMathMl_MathMlStyleResolver::*;
    public import CanvasNetMathMl_MathMlMathFont::*;
    public import CanvasNetMathMl_MathMlMathBox::*;
    public import CanvasNetMathMl_MathMlTokenLayout::*;
    public import CanvasNetMathMl_MathMlRowLayout::*;
    public import CanvasNetMathMl_MathMlFractionLayout::*;
    public import CanvasNetMathMl_MathMlRadicalLayout::*;
    public import CanvasNetMathMl_MathMlScriptLayout::*;
    public import CanvasNetMathMl_MathMlUnderOverLayout::*;
    public import CanvasNetMathMl_MathMlTableLayout::*;
    public import CanvasNetMathMl_MathMlOperatorDictionary::*;
    public import CanvasNetMathMl_MathMlStretchyGlyphs::*;

    part def MathMlLayout {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.MathMl.Layout namespace that lays the equation tree out into a tree of boxes as MathML Core does, in CSS pixels at the font size of the call. The MathMlLayoutEngine unit walks the elements, computes their styles (the children of fractions, roots, and scripted elements with the user agent rules of their positions), dispatches each to its layout, lays a fraction, root, or scripted element without the in-flow children MathML Core requires out as a row with a mathml-child-count warning, moves the limits of a movable operator to the script positions outside display style, wraps stray table children in anonymous rows and cells with a mathml-child-count warning, lays every element outside MathML Core out as a row with a report, reports the attributes without effect, and charges the work budget; the MathMlStyleResolver unit computes the inherited math style, math depth, font size, colors, mathvariant, and the inline style subset; the MathMlMathFont unit is the bundled Noto Sans Math with its MATH constants, glyph metrics, italic corrections, top accent attachments, and the deterministic bundled fallback; the MathMlMathBox unit is the immutable box and paint-item model; the MathMlTokenLayout unit lays out the token elements with the automatic italic and the mathvariant mappings, resolves the properties of every mo, and stretches (vertically and horizontally) and enlarges operators; the MathMlRowLayout unit lays out rows (stretching their stretchy operators), the grouping elements, mpadded, and merror; the MathMlFractionLayout unit lays out mfrac with a bar on the math axis or as a stack and resolves linethickness; the MathMlRadicalLayout unit lays out msqrt and mroot with the stretched radical symbol, the overbar, and the kerned and raised index; the MathMlScriptLayout unit lays out msub, msup, msubsup, and mmultiscripts with the script constants of the MATH table; the MathMlUnderOverLayout unit lays out munder, mover, and munderover as limits, accents, or other scripts and stretches their operators along the inline axis; the MathMlTableLayout unit lays out mtable, mtr, and mtd with the HTML spans (bounded by MaxTableCells before the grid is allocated), the padding and axis centering of MathML Core, and the MathML 3 alignment, spacing, line, and frame attributes as Firefox draws them; the MathMlOperatorDictionary unit is the operator dictionary of MathML Core with the form inference and the stretch axis; and the MathMlStretchyGlyphs unit stretches glyphs with the size variants and glyph assemblies of the MATH table, the Unicode-based assemblies of MathML Core B.4, and a rule for line characters. The subsystem is called by MathMlDocument; its boxes are painted by MathMlPainter. */

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
        part fractionLayout : MathMlFractionLayout;
        part radicalLayout : MathMlRadicalLayout;
        part scriptLayout : MathMlScriptLayout;
        part underOverLayout : MathMlUnderOverLayout;
        part tableLayout : MathMlTableLayout;
        part operatorDictionary : MathMlOperatorDictionary;
        part stretchyGlyphs : MathMlStretchyGlyphs;
    }
}
