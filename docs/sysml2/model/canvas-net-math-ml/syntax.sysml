package CanvasNetMathMl_MathMlSyntaxSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetMathMl_MathMlReader::*;
    public import CanvasNetMathMl_MathMlCharacterReferences::*;
    public import CanvasNetMathMl_MathMlMathTree::*;
    public import CanvasNetMathMl_MathMlAttributeValues::*;

    part def MathMlSyntax {
        doc /* The internal subsystem in the DemaConsulting.CanvasNet.MathMl.Syntax namespace that turns MathML text into the equation tree with the location of every element, attribute, and text in the original text, and parses attribute values. The MathMlCharacterReferences unit rewrites the named character references of the full W3C HTML+MathML set into numeric references without a document type and maps the positions of the rewritten text back to the original; the MathMlReader unit reads the rewritten text with the hardened XML reader of the core (document types prohibited), checks the root, skips and reports foreign content, collapses token whitespace, keeps the annotations raw, reports links, and enforces the element, nesting, and token-length limits iteratively; the MathMlMathTree unit is the immutable equation model (elements, text runs, attributes, locations) without System.Xml types; and the MathMlAttributeValues unit parses lengths, booleans, unsigned integers, scriptlevel values, colors, and the supported subset of the inline style attribute. The subsystem depends only on the public location, limit, exception, code, and pending-report types of the root namespace and on the core Packaging and Canvas subsystems; it is called by MathMlDocument and by the Layout subsystem. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Syntax/SyntaxTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/syntax.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/syntax.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/syntax.yaml");
        }

        part mathMlReader : MathMlReader;
        part characterReferences : MathMlCharacterReferences;
        part mathTree : MathMlMathTree;
        part attributeValues : MathMlAttributeValues;
    }
}
