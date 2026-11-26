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
`MathMlReaderTests.MathMlReader_Read_EmptyOrBlankText_FailsAtLineOneColumnOne`,
`MathMlReaderTests.MathMlReader_Read_XmlErrorAfterReplacement_IsLocatedInOriginalText`

Asserts that `href` and `xlink:href` are reported and not kept, that the other attributes of other namespaces are
ignored, that the kept attributes have their own locations, and that a refusal is located in the original text; an
empty text and a text of only white space are each a `mathml-syntax-error` located at line 1, column 1, with a hint.
A mismatched end tag after `&Bopf;` (which grew by three characters) is located at line 1, column 29 of the original
text, and the start tag that the message of the XML reader cites is given as line 1, column 23 (its column in the
original text), with no position of the rewritten text left in the message.

##### CanvasNetMathMl-Syntax-MathMlReader-Characters: Literal Characters

**Tests**: `MathMlReaderTests.MathMlReader_Read_LiteralNonAsciiCharacters_AreReadAsWritten`

For Greek letters, the minus sign U+2212, CJK ideographs, mathematical italic letters outside the basic plane, and a mix
of them, asserts that a short equation (the token, a literal minus sign, and a number) and a text of the token repeated
2,000 times (longer than the first buffer of the XML reader, with the token also in an attribute value) are read with
every text and attribute value exactly as written.

##### CanvasNetMathMl-Syntax-MathMlReader-Limits: Elements, Depth, Token Length, And Reports

**Tests**: `MathMlReaderTests.MathMlReader_Read_ElementLimit_Throws`,
`MathMlReaderTests.MathMlReader_Read_DepthLimit_Throws`,
`MathMlReaderTests.MathMlReader_Read_TokenLengthLimit_Throws`,
`MathMlReaderTests.MathMlReader_Read_SkippedElements_CountAgainstElementLimit`,
`MathMlReaderTests.MathMlReader_Read_SkippedSubtreeDescendants_CountAgainstElementLimit`,
`MathMlReaderTests.MathMlReader_Read_SkippedSubtreeTooDeep_FailsOnDepthLimit`,
`MathMlReaderTests.MathMlReader_Read_AnnotationContent_IsChargedToLimits`,
`MathMlReaderTests.MathMlReader_Read_ManyReports_AreBoundedBySuppressionNotice`

Asserts that the first element, level, or raw token text beyond a small limit throws `mathml-limit-exceeded` naming
the limit, located at the element that exceeds the element limit (line 1, column 18), at the first element too deep
(line 1, column 20), and at the start of the token text that is too long (line 1, column 11); that skipped elements of
another namespace and inside a token are charged to the element limit (a text of two MathML elements and three skipped
elements passes a limit of 5 and fails a limit of 4 at the skipped element inside the token, line 1, column 41) while
the element count stays the MathML elements read; that every element inside a skipped element is charged too (one
skipped element holding three elements side by side and one nested passes a limit of 6 with a single report and fails
a limit of 5 at the nested element, line 1, column 46, and one holding 1,000 empty elements fails a limit of 100 at the
99th of them, line 1, column 617); that the nesting limit applies inside skipped content (a skipped element holding
two levels passes a limit of 4 and fails a limit of 3 at the innermost element, line 1, column 34, and markup inside a
token at depth 3 fails a limit of 2 at line 1, column 13); that the elements inside an annotation are charged to both
limits (an `annotation-xml` holding `<apply><plus/><ci>x</ci></apply>` keeps that markup under limits of 7 elements
and depth 5 and fails a limit of 6 elements at the `ci`, line 1, column 59, and a depth of 4 at the `plus`, line 1,
column 52, and an `annotation` whose text is split by an element of another namespace keeps the text `ac` under a
limit of 3 and fails a limit of 2 at that element, line 1, column 21); and that twenty reports of skipped elements and
links under a report limit of 3 are kept as the first three in text order followed by one
`mathml-diagnostics-suppressed` warning naming `MathMlLimits.MaxDiagnostics`.
