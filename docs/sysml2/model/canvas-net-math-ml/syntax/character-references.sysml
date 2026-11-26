package CanvasNetMathMl_MathMlCharacterReferences {
    private import SoftwareItemMetadata::*;

    part def MathMlCharacterReferences {
        doc /* The internal static CharacterReferences of the MathML Syntax subsystem with the PrePassResult record (the rewritten text, its location map, and the location of a document type declaration) and the LocationMap class with its Breakpoint records. Rewrite makes one linear scan of the text that follows the markup (text, tags with their attribute values, comments, CDATA sections, processing instructions): a named reference of the built-in table (CharacterReferences.Table.cs, the full W3C HTML+MathML set of 2125 names generated from htmlmathml-f.ent) in text or in an attribute value becomes one numeric reference per code point; numeric references and the five XML names are kept; references inside comments, CDATA, and processing instructions are left alone; an unknown name is a located mathml-syntax-error at its ampersand with a hint; a document type declaration stops the rewriting and is recorded, so that the XML reader refuses it; an XML declaration with an encoding other than UTF-8 (US-ASCII included, since the text is read as UTF-8) is refused at the encoding. A reference never spans a line break, so lines are kept and each changed line keeps sorted breakpoints; MapBack maps a line and column (UTF-16 code units, the XML reader's line breaks) of the rewritten text back to the original, a column inside a replacement to its ampersand. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.MathMl/Syntax/CharacterReferences.cs",
                "src/DemaConsulting.CanvasNet.MathMl/Syntax/CharacterReferences.Table.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.MathMl.Tests/Syntax/CharacterReferencesTests.cs");
            designFiles = ("docs/design/canvas-net-math-ml/syntax/character-references.md");
            verificationFiles = ("docs/verification/canvas-net-math-ml/syntax/character-references.md");
            requirementFiles = ("docs/reqstream/canvas-net-math-ml/syntax/character-references.yaml");
        }
    }
}
