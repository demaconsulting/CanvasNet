package CanvasNetMathMl_MathMlMathBox {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlMathTree::*;
    private import CanvasNetMathMl_MathMlOperatorDictionary::*;

    part def MathMlMathBox {
        doc /* The internal box model of the MathML Layout subsystem (MathBox.cs): the MathRect value (a rectangle in CSS pixels relative to the origin of a box, y down, with Union and Offset); the IPaintItem interface and its records GlyphItem (font, glyph, size, pen position, baseline, color, and ink), RuleItem, BackgroundItem, and BorderItem; the EmbellishedOperator record (the properties of an embellished operator: its leading and trailing space, core text, form, category, stretchy, symmetric, largeop, movablelimits, fence, separator, stretch axis, minsize, and maxsize); the BlockStretchConstraint value (the ascent and descent a stretchy operator of a row covers); the OperatorContext value (the form and the block or inline stretch size constraint the parent gives an embellished operator); the PlacedBox value (a child box at its offset); and the MathBox class (width, line-ascent, and line-descent, the placed children, the paint items, the italic correction, the top accent attachment, the operator data, the space-like flag, the element, and the phantom flag) with the ContentRect, InkBounds (the union of the items of the box and its children, phantoms excluded), LeadingInk (the ink of the token at the start of a box), and Clearance (how far a box must move right so that its ink does not collide with the ink of a slanted or overlapped token before it, at least the italic correction of a slanted token, as MathML Core adds it). The origin of a box is the left edge of its content box on its alphabetic baseline. Boxes are built bottom-up and never changed, so a finished tree is safe for concurrent reads. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Layout/MathBox.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/MathBoxTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/math-box.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/math-box.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/math-box.yaml");
        }

        // A box keeps the element it was laid out for; a glyph item names its font and color
        dependency usesMathTree from MathMlMathBox to MathMlMathTree;
        dependency usesFonts from MathMlMathBox to CanvasNet_FontsSubsystem::Fonts;
        dependency usesCanvas from MathMlMathBox to CanvasNet_CanvasSubsystem::Canvas;

        // The operator records name forms, categories, and stretch axes
        dependency usesOperatorDictionary from MathMlMathBox to MathMlOperatorDictionary;
    }
}
