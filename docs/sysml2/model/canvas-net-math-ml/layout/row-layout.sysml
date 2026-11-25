package CanvasNetMathMl_MathMlRowLayout {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;

    part def MathMlRowLayout {
        doc /* The internal RowLayout of the MathML Layout subsystem. LayoutRow lays out mrow and every element laid out like it (the inferred row of math, mstyle, mphantom, merror, semantics, maction, and every element this version does not draw) as MathML Core 3.3.1.2 does: the children left to right on a shared alphabetic baseline with the maximum ascent and descent; an embellished operator child gets its leading space before it and its trailing space after it unless the row is itself an embellished operator and is not math; the italic correction of the last child becomes the row's but is not added between children (as Chrome lays rows out). A grouping row of one embellished operator and space-like children is an embellished operator, and a row of space-like children is space-like (MathML Core 3.2.4.1 and 3.2.5.1). LayoutPadded lays out mpadded (MathML Core 3.3.6: the requested width, height, and depth replace the inner box's, percentages and invalid values keep it, negative height and depth are clamped to zero, and lspace and voffset move the content); LayoutError puts the 1 px red border of merror around a row, growing the box on every side. Before the children are placed, Stretch lays the stretchy embellished operators with a block stretch axis out again through the relayout function of the engine with a block stretch size constraint (MathML Core 3.3.1.1): the largest ascent and descent of the other children, or, when every child stretches, of the operators laid out with the constraint (0, 0); a row that passes the constraint of its parent on stretches nothing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/RowLayout.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/RowLayoutTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/row-layout.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/row-layout.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/row-layout.yaml");
        }

        // Rows place child boxes; mpadded reads its attributes through the resolver; merror paints a border color
        dependency usesMathBox from MathMlRowLayout to MathMlMathBox;
        dependency usesMathTree from MathMlRowLayout to MathMlMathTree;
        dependency usesStyleResolver from MathMlRowLayout to MathMlStyleResolver;
        dependency usesCanvas from MathMlRowLayout to CanvasNet_CanvasSubsystem::Canvas;

        // The stretch pass selects the operators with a block stretch axis
        dependency usesOperatorDictionary from MathMlRowLayout to MathMlOperatorDictionary;
    }
}
