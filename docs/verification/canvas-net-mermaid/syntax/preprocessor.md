### Preprocessor Unit Verification Design

This document describes the unit-level verification strategy for the `Preprocessor` unit in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: the internal static `Preprocessor` class (`Run`, `CleanupText`,
`MatchFrontmatter`, `DetectDirectives`, `TryMatchDirective`, `RemoveDirectives`, `CleanupComments`, and
`EncodeEntities`) with its result records, and the location map `SourceLines` and `MappedText`.

#### Verification Approach

The unit is verified by preparing small texts and comparing the code, the parser text, the frontmatter, the
directives, and the locations with the values that Mermaid's preprocessing gives for the same texts. The expected
texts of the frontmatter, directive, comment, and entity steps were produced by running Mermaid's own expressions
on the test inputs, including the backtracking cases of the frontmatter expression (an empty body, a block without
its closing line, four dashes). Locations are one-based literals derived by hand from the input. The hostile-input
scenario runs every step on 200,000 characters of the repeated worst-case pattern of each expression and asserts the
result, not the time. The unit's only collaborator is the `YamlSubsetParser` of the same subsystem, which is used
as is; no mocking or stubbing is required, and the internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `PreprocessorTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any prepared text, frontmatter value, directive, or location that differs from the expected literal
constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Syntax-Preprocessor-TextCleanup: Line Endings And Attribute Quotes Are Normalized

**Tests**: `PreprocessorTests.Preprocessor_Run_LineEndings_NormalizedAndMapped`,
`PreprocessorTests.Preprocessor_CleanupText_HtmlAttributes_UseSingleQuotes`

Asserts that a carriage return and line feed pair and a lone carriage return both become a line feed, and that the
double-quoted attribute values of an `<a>` tag become single-quoted while a double-quoted label outside the tag
stays.

##### CanvasNetMermaid-Syntax-Preprocessor-Frontmatter: The Frontmatter Is Read And Removed As Mermaid Does

**Tests**: `PreprocessorTests.Preprocessor_Run_Frontmatter_IsReadAndRemoved`,
`PreprocessorTests.Preprocessor_Run_FrontmatterTitle_FollowsJavaScriptConversion`,
`PreprocessorTests.Preprocessor_MatchFrontmatter_Indented_RemovesIndentFromBody`,
`PreprocessorTests.Preprocessor_MatchFrontmatter_EmptyAndUnclosed_FollowMermaidExpression`,
`PreprocessorTests.Preprocessor_Run_FrontmatterNotMapping_KeepsNothing`

Asserts that the title, display mode, and configuration of a frontmatter are kept and the block is removed from the
code; that the title is converted as JavaScript's `String(value)` (`1.50` is `1.5`, a sequence is joined with
commas) and dropped when falsy (`0`, an empty string); that an indented block closes only at the same indent and
loses its indent; that an empty body closes only after a blank line, while an unclosed block and four dashes are not
frontmatter; and that a frontmatter that is a sequence keeps nothing.

##### CanvasNetMermaid-Syntax-Preprocessor-FrontmatterErrors: Invalid Frontmatter Is Located In The Original Text

**Tests**: `PreprocessorTests.Preprocessor_Run_FrontmatterInvalidYaml_ThrowsLocated`

Asserts that a duplicated key in the frontmatter is a `mermaid-yaml-invalid` error with js-yaml's message, located
at line 3, column 1 of the original text (the second key), not at the line of the frontmatter body.

##### CanvasNetMermaid-Syntax-Preprocessor-Directives: Directives Are Found And Removed

**Tests**: `PreprocessorTests.Preprocessor_Run_Directives_AreDetectedAndRemoved`,
`PreprocessorTests.Preprocessor_TryMatchDirective_Forms_FollowMermaidExpression`

Asserts that an `init` directive with single-quoted JSON and a `wrap` directive are found in order with their
locations and arguments (quotes replaced, JSON flagged, none for `wrap`) and removed from the code, and that the
directive expression accepts a word argument with white space, a type without a colon, and a directive that is not
closed, but not an empty `%%{ }%%`.

##### CanvasNetMermaid-Syntax-Preprocessor-Comments: Only Comment Lines Are Removed

**Tests**: `PreprocessorTests.Preprocessor_CleanupComments_RemovesCommentLinesOnly`

Asserts that an indented comment line and the blank line before it are removed, while a comment after code, a bare
`%%`, and a directive line stay.

##### CanvasNetMermaid-Syntax-Preprocessor-EntityEncoding: The Parser Text Is Encoded As Mermaid Encodes It

**Tests**: `PreprocessorTests.Preprocessor_EncodeEntities_EncodesEntitiesAndStyleSemicolons`,
`PreprocessorTests.Preprocessor_Run_ParserText_EndsWithLineFeedAndMaps`

Asserts that `#quot;` and `#35;` become Mermaid's private markers, that the last semicolon of a `style` and a
`classDef` line with a color is dropped while a semicolon in a label stays, and that the parser text ends with a
line feed.

##### CanvasNetMermaid-Syntax-Preprocessor-Locations: Every Prepared Position Maps To The Original Text

**Tests**: `PreprocessorTests.Preprocessor_Run_LineEndings_NormalizedAndMapped`,
`PreprocessorTests.Preprocessor_Run_Frontmatter_IsReadAndRemoved`,
`PreprocessorTests.Preprocessor_Run_ParserText_EndsWithLineFeedAndMaps`

Asserts that a character after a lone carriage return is on line 3, that the start of the code after a frontmatter
is on line 7 and the frontmatter at line 1, column 1, and that an encoded entity marker of the parser text is
located at the `#` it replaces (line 3, column 4) after a removed comment line.

##### CanvasNetMermaid-Syntax-Preprocessor-SizeLimits: The Text Size Is Bounded And Every Step Is Linear

**Tests**: `PreprocessorTests.Preprocessor_Run_SizeLimits_ThrowInvalidData`,
`PreprocessorTests.Preprocessor_Steps_LongRepeatedPattern_Complete`

Asserts, with a text size limit of 20, that code longer than the limit and an original text longer than four times
the limit are `InvalidDataException`s while a text whose comment lines make it longer than the limit, but whose code
is short, is accepted; and that every step, including the detector's comment replacement, completes on 200,000
characters of each worst-case pattern and keeps the diagram keyword.
