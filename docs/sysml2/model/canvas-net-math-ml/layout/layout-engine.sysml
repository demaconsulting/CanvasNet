package CanvasNetMathMl_MathMlLayoutEngine {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlStyleResolver::*;
    private import CanvasNetMathMl_MathMlTokenLayout::*;
    private import CanvasNetMathMl_MathMlRowLayout::*;
    private import CanvasNetMathMl_MathMlFractionLayout::*;
    private import CanvasNetMathMl_MathMlRadicalLayout::*;
    private import CanvasNetMathMl_MathMlScriptLayout::*;
    private import CanvasNetMathMl_MathMlUnderOverLayout::*;
    private import CanvasNetMathMl_MathMlTableLayout::*;
    private import CanvasNetMathMl_MathMlMathBox::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;
    private import CanvasNetMathMl_MathMlAttributeValues::*;

    part def MathMlLayoutEngine {
        doc /* The internal LayoutEngine of the MathML Layout subsystem: one instance per measure or render call with the math font, the limits, the report sink, and the cancellation token of the call, which creates the StyleResolver, TokenLayout, RowLayout, FractionLayout, RadicalLayout, ScriptLayout, UnderOverLayout, and TableLayout of the call. Layout walks the equation tree, computes the style of each element from its parent's, and dispatches it: the token elements (mi, mn, mo, mtext, ms, mspace) to the token layout; math, mrow, mstyle, mphantom, merror, mpadded, mprescripts, and none to the row layout (mpadded and merror with their boxes around the row); semantics and maction to the row layout with their first child only; mfrac to the fraction layout, its numerator and denominator under mfrac > * { math-depth: auto-add; math-style: compact } (and math-shift: compact for the denominator); msqrt (the row of its children, whose operators infer their forms, spaced like a grouping element as the anonymous row of an mtd is, so that a lone operator gets no spacing, and which is never an embellished operator) and mroot (its base, and its index under math-depth: add(2) and math-style: compact) to the radical layout; msub, msup, msubsup, and mmultiscripts to the script layout and munder, mover, and munderover to the under- and overscript layout, every child but the first under math-depth: add(1) and math-style: compact (an accent script keeping its font size, a subscript and the base of an accent getting math-shift: compact) and laid out as a postfix operator; an munder, mover, or munderover in compact style whose base is an embellished operator with movablelimits to the script layout, and the others after their operators stretchy along the inline axis are laid out again with an inline stretch size constraint; mtable (math-style: compact) to the table layout, after its mtr children and the cells of each are walked (each mtd laid out as the row of its children), consecutive children of mtable that are not mtr wrapped in an anonymous row and consecutive children of mtr that are not mtd in an anonymous cell, each run reported as a mathml-child-count warning, as CSS wraps stray table children; an mtr or mtd outside a table is a row of its children with the same warning. Every element of MathML Core is drawn. An element without the in-flow children MathML Core requires (two for mfrac, mroot, msub, msup, munder, and mover, three for msubsup and munderover, a base and pairs of scripts around at most one mprescripts for mmultiscripts) is laid out as a row of its children and reported as a mathml-child-count warning (invalid markup, not an unsupported feature); an accent or accentunder other than true or false is a mathml-attribute-invalid warning. Every element outside MathML Core, such as mfenced, is laid out as a row of its children and reported as mathml-element-unsupported. Annotations (never drawn, a documented difference from MathML Core 3.7) and elements with display: none get no box; text directly inside a layout element is laid out like an mtext. An attribute without effect on an element this version draws is reported as mathml-attribute-unsupported, while the global attributes that do not change a picture are accepted silently. The walk is recursive with its depth checked against MathMlLimits.MaxNestingDepth, every element and token character is charged to MaxLayoutWork (a located mathml-limit-exceeded MathMlRenderException), and the token is checked at every element. Before the children of a row are laid out, the form of each embellished operator is inferred from its position among the children that are not space-like (predicted from the tree, cached per element); an embellished grouping element, or an mfrac or a scripted element whose first child is an embellished operator (which then carries that operator), passes the form and the stretch constraint of its parent on to its operator, while fractions, radicals, scripted elements, and tables are never space-like and radicals and tables never embellished operators; the row lays its stretchy operators out again through a relayout function that the engine supplies, charged again to the work budget. */

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

        // The engine walks the equation tree, resolves each style, and dispatches to the token, row, fraction, radical, script, under-over, and table layouts
        dependency usesMathTree from MathMlLayoutEngine to MathMlMathTree;
        dependency usesStyleResolver from MathMlLayoutEngine to MathMlStyleResolver;
        dependency usesTokenLayout from MathMlLayoutEngine to MathMlTokenLayout;
        dependency usesRowLayout from MathMlLayoutEngine to MathMlRowLayout;
        dependency usesFractionLayout from MathMlLayoutEngine to MathMlFractionLayout;
        dependency usesRadicalLayout from MathMlLayoutEngine to MathMlRadicalLayout;
        dependency usesScriptLayout from MathMlLayoutEngine to MathMlScriptLayout;
        dependency usesUnderOverLayout from MathMlLayoutEngine to MathMlUnderOverLayout;
        dependency usesTableLayout from MathMlLayoutEngine to MathMlTableLayout;
        dependency usesMathBox from MathMlLayoutEngine to MathMlMathBox;
        dependency usesMathFont from MathMlLayoutEngine to MathMlMathFont;
        dependency usesOperatorDictionary from MathMlLayoutEngine to MathMlOperatorDictionary;

        // The prediction of display: none reads the inline style with the pure StyleDeclarations.Parse
        dependency usesAttributeValues from MathMlLayoutEngine to MathMlAttributeValues;
    }
}
