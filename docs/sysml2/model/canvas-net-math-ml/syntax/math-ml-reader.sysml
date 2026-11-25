package CanvasNetMathMl_MathMlReader {
    private import SoftwareItemMetadata::*;
    private import CanvasNetMathMl_MathMlCharacterReferences::*;
    private import CanvasNetMathMl_MathMlMathTree::*;

    part def MathMlReader {
        doc /* The internal static MathMlReader of the MathML Syntax subsystem with the MathReadResult record (the root element, the element count, the pending reports, and the annotations). Read rewrites the named references first (MathMlCharacterReferences), encodes the rewritten text as strict UTF-8 (an unpaired surrogate is a located syntax error), and reads it through SafeXml.CreateReader of the core (document types prohibited, no resolver, a character cap); every XmlException becomes a located mathml-syntax-error MathMlSyntaxException with a hint, at the position mapped back to the original text. The root must be math in the MathML namespace or in no namespace. An element of another namespace, and markup inside a token, is reported as mathml-element-unsupported and skipped with its subtree; the content of annotation and annotation-xml is kept raw and never laid out or reported; token text is trimmed and its runs of XML whitespace collapsed; whitespace between elements is dropped; other text inside a layout element is kept and reported; href and the xlink attributes are reported and never kept; attributes of other namespaces are ignored. The read is iterative (an explicit stack) and enforces MathMlLimits.MaxNestingDepth, MaxElements, and MaxTokenLength with located mathml-limit-exceeded errors. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Syntax/MathMlReader.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Syntax/NamespaceDoc.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Syntax/MathMlReaderTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/syntax/math-ml-reader.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/syntax/math-ml-reader.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/syntax/math-ml-reader.yaml");
        }

        // The reader rewrites the named references and maps every reported position back to the original text
        dependency usesCharacterReferences from MathMlReader to MathMlCharacterReferences;

        // The reader builds the equation tree
        dependency usesMathTree from MathMlReader to MathMlMathTree;

        // The rewritten text is read by the hardened XML reader of the core
        dependency usesPackaging from MathMlReader to CanvasNet_PackagingSubsystem::Packaging;
    }
}
