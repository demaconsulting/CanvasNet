package CanvasNet {
    part def PlantUmlPreprocessor {
        doc /* The internal static Preprocessor of the PlantUML Syntax subsystem with its records DiagramBlock and PreprocessedSource, and the location map SourceLines and SourceStatement (Preprocessor.MappedText.cs). Run frames the first @start ... @end block of a text (a text without a tag is one diagram; a block without an end tag is a located syntax error; a further block is reported as plantuml-multiple-diagrams), removes the apostrophe line comments and the block comments, joins the lines that end with a backslash, applies !define without parameters, !undef, and !$variable assignments by whole-word substitution, and reports every other directive as a pending report (includes, themes, pragmas, and the rest) while dropping the blocks they open. Every statement keeps the original offset of every character. The original text may be at most four times PlantUmlLimits.MaxTextSize and the statements at most MaxTextSize (an InvalidDataException otherwise); dropped directive blocks nest at most MaxNestingDepth deep. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.PlantUml/Syntax/Preprocessor.cs, src/DemaConsulting.CanvasNet.PlantUml/Syntax/Preprocessor.MappedText.cs, src/DemaConsulting.CanvasNet.PlantUml/Syntax/NamespaceDoc.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.PlantUml.Tests/Syntax/PreprocessorTests.cs */
        comment designRef /* Design: docs/design/canvas-net-plant-uml/syntax/preprocessor.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-plant-uml/syntax/preprocessor.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-plant-uml/syntax/preprocessor.yaml */
    }
}
