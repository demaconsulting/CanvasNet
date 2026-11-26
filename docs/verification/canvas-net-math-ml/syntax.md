## Syntax Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Syntax` subsystem of CanvasNetMathMl
(namespace `DemaConsulting.CanvasNet.MathMl.Syntax`): the `CharacterReferences`, `MathMlReader`, `MathTree`, and
`AttributeValues` units working together as the document and the layout use them.

### Verification Approach

The subsystem is verified by reading full original texts with `MathMlReader.Read`, which runs the reference pre-pass,
the hardened XML reader, and the tree builder in sequence, through the test assembly's `InternalsVisibleTo` access, and
by parsing the attribute values of the tree that was read with `AttributeValues` and `StyleDeclarations`. The tests
assert the tree, the locations in the original text, the reports, the annotations, and the refusals. All units are used
as is; no mocking is required. Subsystem tests reside in `Syntax/SyntaxTests.cs` within the
`DemaConsulting.CanvasNet.MathMl.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted. A name, a text, a location, a report, a parsed value, or an error that differs from the expected literal
constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetMathMl-Syntax-Reading, CharacterReferences, And Locations: A Mixed Text Is Read In Place

**Tests**: `SyntaxTests.Syntax_Read_MixedText_IsReadInPlaceWithMappedLocations`

Reads a text with a UTF-8 XML declaration, a reference inside a comment, `&alpha;`, `&InvisibleTimes;`, and `&Bopf;`
on one line, a CDATA section, an SVG element, and an annotation, and asserts the five child elements, their decoded
texts, their locations and the location of an attribute in the original text (line 4, columns 4, 20, 45, and 48), the
single report of the foreign element at line 5, column 36, the annotation, and the element count of 8.

#### CanvasNetMathMl-Syntax-Locations: An Error After References

**Tests**: `SyntaxTests.Syntax_Read_ErrorAfterReferences_IsLocatedInOriginalText`

Reads a mismatched end tag after `&Bopf;` (which grew by three characters) and asserts line 2, column 13: the end tag
name in the original text (column 16 of the rewritten text).

#### CanvasNetMathMl-Syntax-Reading And Locations: Literal Unicode Text

**Tests**: `SyntaxTests.Syntax_Read_LiteralUnicodeText_IsReadAsWritten`

Reads an equation written with a literal Greek letter, minus sign, and CJK ideograph in its `alttext` and literal
characters in its tokens (a mathematical italic x outside the basic plane, U+2212, two CJK ideographs) next to
`&Bopf;`, and asserts the decoded texts, the attribute value, no report, and the location of the `mtext` at line 2,
column 38 (the astral character counts two columns, the reference shifts the rest of the line).

#### CanvasNetMathMl-Syntax-Reading And CharacterReferences: Another Root And An Unknown Name

**Tests**: `SyntaxTests.Syntax_Read_WrongRootAndUnknownName_AreRefusedLocated`

Asserts that an `svg` root after an XML declaration is refused with `mathml-syntax-error` at line 2, column 2 naming
`<svg>` with a hint, and that `&notaname;` after `&alpha;` is refused at its ampersand (line 2, column 12) with a hint.

#### CanvasNetMathMl-Syntax-Reading And Locations: The Limits Through The Full Read

**Tests**: `SyntaxTests.Syntax_Read_Limits_AreEnforcedAndLocatedInOriginalText`

Reads one text, in which `&Bopf;` grows by three characters before every limit is reached, under an element limit of
3, a nesting limit of 3, and a token-length limit of 2, and asserts `mathml-limit-exceeded` naming each property,
located in the original text at the second `mrow` (column 29), at the `mi` inside it (column 35), and at the start of
the text of that `mi`, three named references (column 38).

#### CanvasNetMathMl-Syntax-CharacterReferences: Refused Declarations

**Tests**: `SyntaxTests.Syntax_Read_RefusedDeclarations_AreLocated`

Asserts that a document type declaration is refused at line 1 with the document-type hint, that a UTF-16 encoding
declaration is refused at line 1, column 21, and that a lower-case `utf-8` declaration is accepted.

#### CanvasNetMathMl-Syntax-AttributeValues And Links: The Attributes Of A Read Tree

**Tests**: `SyntaxTests.Syntax_ReadAndParse_AttributesOfTheTree_AreParsedAndLinksReported`

Reads an `mspace` with `thinmathspace` and `1.5ex` and an `mstyle` with `scriptlevel="+1"`, `displaystyle="true"`, a
color written with `&num;`, and an inline style, and asserts the parsed lengths (3/18 em and 1.5 ex), the relative
level, the boolean, red, the style's blue and `add(2)` with no issue; and that the `href` of the `mi` inside is reported
as `mathml-attribute-unsupported` at line 1, column 166 and not kept.

#### CanvasNetMathMl-Syntax-AttributeValues: Invalid Values Of A Read Tree

**Tests**: `SyntaxTests.Syntax_ReadAndParse_InvalidAttributeValues_AreRefused`

Reads a table cell with `rowspan="2"` and `columnspan="-1"` around an `mstyle` with `scriptlevel="1.5"`,
`displaystyle="yes"`, `mathcolor="reddish"`, and `style="color: nope"` and an `mspace` with `width="wide"`, and asserts
that the unsigned integer 2 is read while every other value is refused by its parser, and that the style declaration is
returned as one issue with no color.
