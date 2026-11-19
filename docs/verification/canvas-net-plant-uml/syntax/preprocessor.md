### Preprocessor Unit Verification Design

This document describes the unit-level verification strategy for the `Preprocessor` unit in the
`DemaConsulting.CanvasNet.PlantUml.Syntax` namespace: the internal static `Preprocessor` class with its result records,
and the location map `SourceLines` and `SourceStatement`.

#### Verification Approach

The unit is verified by preprocessing small original texts and comparing the tag, the statements, the pending reports,
and the locations with literal values derived by hand from the input. The unit has no collaborator inside the system;
no mocking is required, and the internal types are reached through the test assembly's `InternalsVisibleTo` access.
Unit tests reside in `PreprocessorTests.cs` within the `DemaConsulting.CanvasNet.PlantUml.Tests` project, in the
`Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any statement, report, or location that differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Syntax-Preprocessor-Framing: The First Block Is The Diagram

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_BareText_IsOneDiagram`,
`PreprocessorTests.PlantUmlPreprocessor_Run_StartUmlBlock_TakesItsContent`,
`PreprocessorTests.PlantUmlPreprocessor_Run_MissingEnd_ThrowsLocated`,
`PreprocessorTests.PlantUmlPreprocessor_Run_SecondBlock_ReportsMultipleDiagrams`,
`PreprocessorTests.PlantUmlPreprocessor_Run_TagsInBlockCommentBeforeStart_AreIgnored`,
`PreprocessorTests.PlantUmlPreprocessor_Run_EndInBlockCommentInsideDiagram_DoesNotEndIt`,
`PreprocessorTests.PlantUmlPreprocessor_Run_TagsInOneLineBlockComments_AreIgnored`,
`PreprocessorTests.PlantUmlPreprocessor_Run_TagsInLineComments_AreIgnored`,
`PreprocessorTests.PlantUmlPreprocessor_Run_EndInUnclosedBlockComment_ThrowsLocated`,
`PreprocessorIncludeTests.PlantUmlPreprocessor_Include_TagsInComments_AreIgnored`

Asserts that a text without a tag is one block with no tag; that an indented, mixed-case `@StartUml` with a name opens
the block at its location and the text around it is ignored; that a block without an end is a syntax error at its
start line whose hint names `@enduml`; and that a second and a third block give one `plantuml-multiple-diagrams` report
at the second. Asserts that tags in comments are comment text: a commented-out diagram in a block comment over several
lines before the real `@startuml` (the block located at the real tag, no report); an `@enduml` in a block comment
inside the diagram (the statement after the comment kept and located on its line); tags in one-line block comments
before, inside, and after the diagram, and an `@enduml` hidden by a block comment opened on the `@startuml` line; tags
after the apostrophe of line comments before, inside, and after the diagram; that an `@enduml` only inside a block
comment that is never closed is a syntax error at the start tag whose hint names the line that opened the comment; and
that an included file is framed the same way.

##### CanvasNetPlantUml-Syntax-Preprocessor-Comments: Comments Are Removed

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_Comments_AreRemoved`

Asserts that a line comment, an indented line comment, an inline block comment, and a block comment over three lines
are removed.

##### CanvasNetPlantUml-Syntax-Preprocessor-Continuation: A Trailing Backslash Joins Lines

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_Continuation_JoinsLinesWithLocations`

Asserts the joined statement and that its second part is located on the second line.

##### CanvasNetPlantUml-Syntax-Preprocessor-Directives: Macros And Variables Are Applied

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_Define_SubstitutesWholeWords`,
`PreprocessorTests.PlantUmlPreprocessor_Run_Variables_AreSubstituted`

Asserts that a macro replaces a whole word but not a longer word that contains it, stops after `!undef`, and that
variables take quoted texts, numbers, and other variables, `?=` keeps an existing value, and an unknown variable stays
as written.

##### CanvasNetPlantUml-Syntax-Preprocessor-UnsupportedDirectives: Other Directives Are Reported

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_UnsupportedDirectives_AreReportedAndDropped`,
`PreprocessorIncludeTests.PlantUmlPreprocessor_Include_UnfollowedForms_AreReported`

Asserts the codes, kinds, and lines of the reports of a macro with parameters, an expression, an `!if` block (dropped
with its message), an include of the standard library and an `!includeurl`, a theme, a pragma (a warning), a function
block, and a built-in function call (kept as text); asserts `plantuml-include-unsupported` at the lines of an include of
a URL, of the standard library, and of a part of a file, an `!includesub`, and an `!includeurl`, and the statement after
them kept.

##### CanvasNetPlantUml-Syntax-Preprocessor-Includes: Local Files Are Read In Place

**Tests**: `PreprocessorIncludeTests.PlantUmlPreprocessor_Include_ReadsTheFileInPlace`,
`PreprocessorIncludeTests.PlantUmlPreprocessor_Include_RepeatsFollowTheDirective`,
`PreprocessorIncludeTests.PlantUmlPreprocessor_Include_FailuresAreLocated`,
`PreprocessorIncludeTests.PlantUmlPreprocessor_Include_DeniedFile_CarriesTheRefusal`

In a temporary folder: asserts that a fragment in a subfolder sets a variable the text uses and includes the block of a
tagged file relative to its own folder, with the included statement located at `more.iuml:2:1`; that a second
`!include` of a file and an `!include` of the diagram's own file are skipped, that `!include_many` includes a file
twice, and that a second `!include_once` is `plantuml-include-failed` at its line; that a missing file (at its line and
column), a text without a folder (with a hint naming `BaseDirectory`), and a file that is not valid UTF-8 are
`plantuml-include-failed` with hints; and that a file outside the roots of the policy is refused with the
`FileReferenceException` of the denial as the inner exception and a message without the host folder.

##### CanvasNetPlantUml-Syntax-Preprocessor-Limits: Size And Nesting Are Bounded

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_SizeOverLimit_ThrowsInvalidData`,
`PreprocessorTests.PlantUmlPreprocessor_Run_DirectiveNestingOverLimit_Throws`,
`PreprocessorIncludeTests.PlantUmlPreprocessor_Include_LimitsAndCycles`

Asserts that comments do not count against the limit, that an original text over four times the limit and a macro
that grows the statements over the limit are refused, and that the fourth nested `!if` under a nesting limit of three
is a located `plantuml-limit-exceeded` error; asserts that two files that include each other are read once each, and
that an include one level past `MaxIncludeDepth` (in the included file), the fourth followed include under
`MaxIncludes` of three, and a file that includes itself with `!include_many` are located `plantuml-limit-exceeded`
errors.

##### CanvasNetPlantUml-Syntax-Preprocessor-Locations: Every Character Is Located

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_Substitution_LocatesInsertedText`,
`PreprocessorTests.PlantUmlPreprocessor_Run_Continuation_JoinsLinesWithLocations`,
`PreprocessorTests.PlantUmlPreprocessor_SourceLines_LocatesEveryLineEnding`

Asserts that a substituted character is located at the variable it replaces, and that the line table locates the
lines after a carriage return and line feed, a lone carriage return, and a line feed.
