package CanvasNetMathMl_MathMlLayoutEngine {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlTokenLayout::*;
    private import CanvasNetMathMl_MathMlRowLayout::*;
    private import CanvasNetMathMl_MathMlFractionLayout::*;
    private import CanvasNetMathMl_MathMlRadicalLayout::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;

    part def MathMlLayoutEngine {
        doc /* The internal LayoutEngine of the MathML Layout subsystem: one instance per measure or render call with the math font, the limits, the report sink, and the cancellation token of the call, which creates the StyleResolver, TokenLayout, RowLayout, FractionLayout, and RadicalLayout of the call. Layout walks the equation tree, computes the style of each element from its parent's, and dispatches it: the token elements (mi, mn, mo, mtext, ms, mspace) to the token layout; math, mrow, mstyle, mphantom, merror, and mpadded to the row layout (mpadded and merror with their boxes around the row); semantics and maction to the row layout with their first child only; mfrac to the fraction layout, its numerator and denominator under mfrac > * { math-depth: auto-add; math-style: compact } (and math-shift: compact for the denominator); msqrt (the row of its children, whose operators infer their forms and which is never an embellished operator) and mroot (its base, and its index under math-depth: add(2) and math-style: compact) to the radical layout. An mfrac or mroot without exactly two in-flow children is laid out as a row of its children and reported as a mathml-child-count warning (invalid markup, not an unsupported feature). Every other element (the MathML Core elements that a later version draws, such as msub, munder, and mtable, and every element outside MathML Core, such as mfenced) is laid out as a row of its children and reported as mathml-element-unsupported. Annotations and elements with display: none get no box; text directly inside a layout element is laid out like an mtext. An attribute without effect on an element this version draws is reported as mathml-attribute-unsupported, while the global attributes that do not change a picture are accepted silently. The walk is recursive with its depth checked against MathMlLimits.MaxNestingDepth, every element and token character is charged to MaxLayoutWork (a located mathml-limit-exceeded MathMlRenderException), and the token is checked at every element. Before the children of a row are laid out, the form of each embellished operator is inferred from its position among the children that are not space-like (predicted from the tree, cached per element); an embellished grouping element, or an mfrac whose numerator is an embellished operator (which then carries that operator), passes the form and the stretch constraint of its parent on to its operator, while fractions and radicals are never space-like and radicals never embellished operators; the row lays its stretchy operators out again through a relayout function that the engine supplies, charged again to the work budget. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Layout/LayoutEngine.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/LayoutEngine.Elements.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/LayoutEngineTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/layout-engine.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/layout-engine.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/layout-engine.yaml");
        }

        // The engine walks the equation tree, resolves each style, and dispatches to the token, row, fraction, and radical layouts
        dependency usesMathTree from MathMlLayoutEngine to MathMlMathTree;
        dependency usesStyleResolver from MathMlLayoutEngine to MathMlStyleResolver;
        dependency usesTokenLayout from MathMlLayoutEngine to MathMlTokenLayout;
        dependency usesRowLayout from MathMlLayoutEngine to MathMlRowLayout;
        dependency usesFractionLayout from MathMlLayoutEngine to MathMlFractionLayout;
        dependency usesRadicalLayout from MathMlLayoutEngine to MathMlRadicalLayout;
        dependency usesMathBox from MathMlLayoutEngine to MathMlMathBox;
        dependency usesMathFont from MathMlLayoutEngine to MathMlMathFont;
        dependency usesOperatorDictionary from MathMlLayoutEngine to MathMlOperatorDictionary;
    }
}
