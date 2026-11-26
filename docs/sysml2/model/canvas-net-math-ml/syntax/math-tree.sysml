package CanvasNetMathMl_MathMlMathTree {
    private import SoftwareItemMetadata::*;

    part def MathMlMathTree {
        doc /* The internal equation tree of the MathML Syntax subsystem (MathTree.cs), the single internal model of an equation: the abstract MathNode record with its location in the original text; the MathElement record (the local element name, the attributes keyed by local name, the children in document order) with ElementChildren, TextContent, GetAttribute, and IsTextTokenName (mi, mn, mo, mtext, ms); the MathText record (the collapsed text of a token, or text written directly inside a layout element); and the MathAttribute value (an attribute value with its own location). The tree holds no System.Xml type, so a later front end (for example LaTeX math) can produce it, and it is immutable and safe for concurrent use. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet.MathMl/Syntax/MathTree.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Syntax/MathTreeTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/syntax/math-tree.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/syntax/math-tree.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/syntax/math-tree.yaml");
        }
    }
}
