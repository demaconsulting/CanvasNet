package CanvasNetPlantUml_PlantUmlPreprocessor {
    private import SoftwareItemMetadata::*;

    part def PlantUmlPreprocessor {
        doc /* The internal static Preprocessor of the PlantUML Syntax subsystem with its records DiagramBlock and PreprocessedSource, and the location map SourceLines and SourceStatement (Preprocessor.MappedText.cs). Run frames the first @start ... @end block of a text (a text without a tag is one diagram; a block without an end tag is a located syntax error; a further block is reported as plantuml-multiple-diagrams), removes the apostrophe line comments and the block comments, joins the lines that end with a backslash, applies !define without parameters, !undef, and !$variable assignments by whole-word substitution, reads the local files of !include, !include_once, and !include_many in place (Preprocessor.Includes.cs: the IncludeResolver finds, checks, and reads them through the core FileReferenceResolver under the caller's FileAccessPolicy, decodes them as strict UTF-8, and names them relative to the base directory; a file with a tag contributes its first block; a second !include is skipped, !include_many repeats, a second !include_once and a missing, refused, or invalid file are located plantuml-include-failed errors), and reports every other directive as a pending report (URLs, the standard library, parts of files, themes, pragmas, and the rest) while dropping the blocks they open. Every statement keeps the original offset of every character. The original text may be at most four times PlantUmlLimits.MaxTextSize and the statements at most MaxTextSize (an InvalidDataException otherwise); dropped directive blocks nest at most MaxNestingDepth deep, and includes at most MaxIncludeDepth deep and MaxIncludes in number. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.PlantUml/Syntax/Preprocessor.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Syntax/Preprocessor.Includes.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Syntax/Preprocessor.MappedText.cs",
                "src/DemaConsulting.CanvasNet.PlantUml/Syntax/NamespaceDoc.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/Syntax/PreprocessorTests.cs",
                "test/DemaConsulting.CanvasNet.PlantUml.Tests/Syntax/PreprocessorIncludeTests.cs"
            );
            designFiles = ("docs/design/canvas-net-plant-uml/syntax/preprocessor.md");
            verificationFiles = ("docs/verification/canvas-net-plant-uml/syntax/preprocessor.md");
            requirementFiles = ("docs/reqstream/canvas-net-plant-uml/syntax/preprocessor.yaml");
        }

        // The include files are found, checked, and read by the core file reference resolver under the caller's policy
        dependency usesFiles from PlantUmlPreprocessor to CanvasNet_FilesSubsystem::Files;
    }
}
