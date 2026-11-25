### MathMlReader Unit Verification Design

This document describes the unit-level verification strategy for the `MathMlReader` unit in the
`DemaConsulting.CanvasNet.MathMl.Syntax` namespace.

#### Verification Approach

The unit is verified by reading small original texts with `MathMlReader.Read` through the test assembly's
`InternalsVisibleTo` access and asserting the tree, the reports, the annotations, the locations, and the errors. The
`CharacterReferences` pre-pass and the core `SafeXml` reader are used as is (the reader cannot run without them). The
limit scenarios use small limits instead of large inputs. Unit tests reside in `Syntax/MathMlReaderTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A name, a text, a location, a report, or an error that
differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Syntax-MathMlReader-Root: The Root Element

**Tests**: `MathMlReaderTests.MathMlReader_Read_NoNamespaceRoot_IsAccepted`,
`MathMlReaderTests.MathMlReader_Read_WrongRoot_FailsLocated`

Asserts that a `math` root without a namespace and one with a prefixed MathML namespace are accepted, and that another
root fails with `mathml-syntax-error` at its location with a hint.

##### CanvasNetMathMl-Syntax-MathMlReader-ForeignContent: Foreign Elements Are Skipped

**Tests**: `MathMlReaderTests.MathMlReader_Read_ForeignElement_IsSkippedAndReported`

Asserts that an element of another namespace and markup inside a token are skipped with their subtrees and reported as
`mathml-element-unsupported` at their locations.

##### CanvasNetMathMl-Syntax-MathMlReader-Whitespace: Token Whitespace

**Tests**: `MathMlReaderTests.MathMlReader_Read_TokenWhitespace_IsCollapsed`

Asserts that token text is trimmed and its runs of whitespace collapsed to one space, and that whitespace between
elements leaves no child.

##### CanvasNetMathMl-Syntax-MathMlReader-TextOutsideTokens: Stray Text

**Tests**: `MathMlReaderTests.MathMlReader_Read_TextOutsideTokens_IsKeptAndReported`

Asserts that text directly inside a row is kept as a text child and reported, and that an unpaired surrogate is
refused with a located syntax error.

##### CanvasNetMathMl-Syntax-MathMlReader-Annotations: Raw Annotations

**Tests**: `MathMlReaderTests.MathMlReader_Read_AnnotationXml_KeepsContentUnlaidOut`

Asserts that the text of `annotation` and the markup of `annotation-xml` are kept raw with their encodings and that
nothing about them is reported.

##### CanvasNetMathMl-Syntax-MathMlReader-Links And Locations: Attributes

**Tests**: `MathMlReaderTests.MathMlReader_Read_Attributes_KeepLocationsAndReportLinks`,
`MathMlReaderTests.MathMlReader_Read_WrongRoot_FailsLocated`,
`MathMlReaderTests.MathMlReader_Read_EmptyOrBlankText_FailsAtLineOneColumnOne`

Asserts that `href` and `xlink:href` are reported and not kept, that the other attributes of other namespaces are
ignored, that the kept attributes have their own locations, and that a refusal is located in the original text; an
empty text and a text of only white space are each a `mathml-syntax-error` located at line 1, column 1, with a hint.

##### CanvasNetMathMl-Syntax-MathMlReader-Limits: Elements, Depth, And Token Length

**Tests**: `MathMlReaderTests.MathMlReader_Read_ElementLimit_Throws`,
`MathMlReaderTests.MathMlReader_Read_DepthLimit_Throws`,
`MathMlReaderTests.MathMlReader_Read_TokenLengthLimit_Throws`

Asserts that the first element, level, or raw token text beyond a small limit throws `mathml-limit-exceeded` naming
the limit (the element limit located at the element that exceeds it).
