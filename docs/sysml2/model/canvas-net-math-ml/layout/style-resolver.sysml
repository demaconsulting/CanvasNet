package CanvasNetMathMl_MathMlStyleResolver {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlAttributeValues::*;
    private import CanvasNetMathMl_MathMlMathFont::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlTokenLayout::*;

    part def MathMlStyleResolver {
        doc /* The internal StyleResolver of the MathML Layout subsystem with the MathStyle record (font size, math style, math depth, color, mathvariant, and math shift, inherited; background and display: none, of the element alone), the ChildStyleRule record and the MathDepthRule enum (the user agent rules of MathML Core that a layout element applies to a child by position). ResolveRoot computes the style of the math element from the render options (font size, color, and the display override or the display attribute, which sets the math style; the math depth starts at zero); Resolve computes the style of every other element from its parent's, and ResolveChild the style of a child of mfrac, mroot, or a scripted element with the rule of its position (math-depth: auto-add, add(2), or add(1), math-style: compact, math-shift, and font-size: inherit for an accent script, as ScriptRule selects them for the scripted elements), in the order of the CSS cascade: the user agent rules of MathML Core (merror gets a light yellow background, msqrt and mroot math-shift: compact, and the rule of the position applies), the presentational hints of displaystyle, scriptlevel (a relative value from the depth of the parent), mathsize, mathcolor, mathbackground, and mathvariant, which replace the user agent values, and the supported subset of the inline style attribute. Unless mathsize or font-size sets it, the font size follows font-size: math, scaled by the change of math depth with the ScriptPercentScaleDown and ScriptScriptPercentScaleDown constants of the MATH table and 0.71 per further level, and the math depth is clamped to plus or minus 64. ResolveLength resolves the length attributes of mspace, mpadded, and mo. An invalid value is a mathml-attribute-invalid warning and is ignored; a style declaration outside the subset is a mathml-css-unsupported report. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/StyleResolver.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/StyleResolverTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/style-resolver.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/style-resolver.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/style-resolver.yaml");
        }

        // The resolver reads the attributes of the tree and parses their values and the inline style
        dependency usesMathTree from MathMlStyleResolver to MathMlMathTree;
        dependency usesAttributeValues from MathMlStyleResolver to MathMlAttributeValues;

        // The script-level scaling uses the MATH constants of the math font
        dependency usesMathFont from MathMlStyleResolver to MathMlMathFont;

        // The mathvariant values are parsed by the variant tables of the token layout
        dependency usesTokenLayout from MathMlStyleResolver to MathMlTokenLayout;
        dependency usesCanvas from MathMlStyleResolver to CanvasNet_CanvasSubsystem::Canvas;
    }
}
