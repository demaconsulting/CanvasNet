package CanvasNetMathMl_MathMlOperatorDictionary {
    private import SoftwareItemMetadata::*;

    part def MathMlOperatorDictionary {
        doc /* The internal static OperatorDictionary of the MathML Layout subsystem with the OperatorForm, StretchAxis, and OperatorCategory enums and the OperatorProperties record (OperatorDictionary.cs and the generated OperatorDictionary.Data.cs: the special tables of MathML Core Appendix B.1 Figure 24, the categories of Figure 25 as sorted ranges per form, the values of Figure 26, and the characters with an inline stretch axis of Figure 28). Category determines the category of an operator from its text and form with the algorithm of B.1 (a text of one or two UTF-16 characters, the reserved range U+0320 to U+03FF, the Arabic operators of the supplementary planes, the combining long solidus and vertical line overlays, the two-character ASCII operators mapped to U+0320 plus their index, ForceDefault for the infix vertical line and tilde operator, Default for anything else); Lookup applies the fallback to the infix, postfix, and prefix forms when the form was inferred and gives the spacing in em and the stretchy, symmetric, largeop, and movablelimits values of the category; IsFence and IsSeparator give the fence and separator defaults; Axis gives the stretch axis of a single character; InferForm gives prefix to the first and postfix to the last of several in-flow children that are not space-like and infix otherwise (MathML Core 3.2.4.2); and TryParseForm parses the form attribute. The class is stateless and safe for concurrent use. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Layout/OperatorDictionary.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Layout/OperatorDictionary.Data.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Layout/OperatorDictionaryTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/layout/operator-dictionary.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/layout/operator-dictionary.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/layout/operator-dictionary.yaml");
        }
    }
}
