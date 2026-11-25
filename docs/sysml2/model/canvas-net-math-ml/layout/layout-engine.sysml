package CanvasNetMathMl_MathMlLayoutEngine {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlTokenLayout::*;
    private import CanvasNetMathMl_MathMlRowLayout::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathTree::*;

    part def MathMlLayoutEngine {
        doc /* The internal LayoutEngine of the MathML Layout subsystem: one instance per measure or render call with the math font, the limits, the report sink, and the cancellation token of the call, which creates the StyleResolver, TokenLayout, and RowLayout of the call. Layout walks the equation tree, computes the style of each element from its parent's, and dispatches it: the token elements (mi, mn, mo, mtext, ms, mspace) to the token layout; math, mrow, mstyle, mphantom, merror, and mpadded to the row layout (mpadded and merror with their boxes around the row); semantics and maction to the row layout with their first child only. Every other element (the MathML Core elements that a later version draws, such as mfrac, msqrt, msub, munder, and mtable, and every element outside MathML Core, such as mfenced) is laid out as a row of its children and reported as mathml-element-unsupported. Annotations and elements with display: none get no box; text directly inside a layout element is laid out like an mtext. An attribute without effect on an element this version draws is reported as mathml-attribute-unsupported, while the global attributes that do not change a picture are accepted silently. The walk is recursive with its depth checked against MathMlLimits.MaxNestingDepth, every element and token character is charged to MaxLayoutWork (a located mathml-limit-exceeded MathMlRenderException), and the token is checked at every element. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Layout/LayoutEngine.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/LayoutEngineTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/layout-engine.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/layout-engine.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/layout-engine.yaml");
        }

        // The engine walks the equation tree, resolves each style, and dispatches to the token and row layouts
        dependency usesMathTree from MathMlLayoutEngine to MathMlMathTree;
        dependency usesStyleResolver from MathMlLayoutEngine to MathMlStyleResolver;
        dependency usesTokenLayout from MathMlLayoutEngine to MathMlTokenLayout;
        dependency usesRowLayout from MathMlLayoutEngine to MathMlRowLayout;
        dependency usesMathBox from MathMlLayoutEngine to MathMlMathBox;
        dependency usesMathFont from MathMlLayoutEngine to MathMlMathFont;
    }
}
