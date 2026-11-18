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
`PreprocessorTests.PlantUmlPreprocessor_Run_SecondBlock_ReportsMultipleDiagrams`

Asserts that a text without a tag is one block with no tag; that an indented, mixed-case `@StartUml` with a name opens
the block at its location and the text around it is ignored; that a block without an end is a syntax error at its
start line whose hint names `@enduml`; and that a second and a third block give one `plantuml-multiple-diagrams` report
at the second.

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

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_UnsupportedDirectives_AreReportedAndDropped`

Asserts the codes, kinds, and lines of the reports of a macro with parameters, an expression, an `!if` block (dropped
with its message), two includes, a theme, a pragma (a warning), a function block, and a built-in function call (kept
as text).

##### CanvasNetPlantUml-Syntax-Preprocessor-Limits: Size And Nesting Are Bounded

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_SizeOverLimit_ThrowsInvalidData`,
`PreprocessorTests.PlantUmlPreprocessor_Run_DirectiveNestingOverLimit_Throws`

Asserts that comments do not count against the limit, that an original text over four times the limit and a macro
that grows the statements over the limit are refused, and that the fourth nested `!if` under a nesting limit of three
is a located `plantuml-limit-exceeded` error.

##### CanvasNetPlantUml-Syntax-Preprocessor-Locations: Every Character Is Located

**Tests**: `PreprocessorTests.PlantUmlPreprocessor_Run_Substitution_LocatesInsertedText`,
`PreprocessorTests.PlantUmlPreprocessor_Run_Continuation_JoinsLinesWithLocations`,
`PreprocessorTests.PlantUmlPreprocessor_SourceLines_LocatesEveryLineEnding`

Asserts that a substituted character is located at the variable it replaces, and that the line table locates the
lines after a carriage return and line feed, a lone carriage return, and a line feed.
