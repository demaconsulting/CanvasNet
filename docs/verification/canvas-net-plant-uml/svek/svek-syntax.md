### SvekSyntax Unit Verification Design

This document describes the unit-level verification strategy for the `SvekSyntax` unit in the
`DemaConsulting.CanvasNet.PlantUml.Svek` namespace.

#### Verification Approach

The unit is verified with statements preprocessed from small original diagram texts by the Syntax subsystem, asserting
what the readers return and the located errors they throw. No mocking is required. Unit tests reside in
`Svek/SvekSyntaxTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A name, a decoration, a length, a hint, a style, a label,
a code, or a location that differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetPlantUml-Svek-SvekSyntax-Names: Names, Aliases, Stereotypes, And Colors

**Tests**: `SvekSyntaxTests.PlantUmlSvekSyntax_IdentifierEnds_QualifiedName_LongestFirst`,
`SvekSyntaxTests.PlantUmlSvekSyntax_NameAliasStereotypesColor_AreRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_ShorthandEnds_AreRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadName_BusinessFormsAndQualifiers_AreRead`

Asserts the ends of a qualified identifier at an index, longest first; asserts a plain and a quoted name, an alias,
two stereotypes, and a color; asserts the shorthand ends `[Web]`, `(Login)`, `:User:`, `[*]`, `A[H]`, `[H*]`, and
`B[H*]`, a second end in brackets before a label (`[Web] --> [Db] : sql`), and the interfaces `() HTTP` and
`() "Message Queue"` with their kinds and texts; asserts the business forms `(Pay)/` and `:Clerk:/` (alone and as the
ends of a link) and the qualifiers `[k]` and `[q]` of `A [k] --> [q] B`.

##### CanvasNetPlantUml-Svek-SvekSyntax-Arrows: The Arrow Grammar

**Tests**: `SvekSyntaxTests.PlantUmlSvekSyntax_TryReadArrow_EveryDecoration_IsRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadArrow_LengthAndHint_AreRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadArrow_NoBody_ReturnsFalse`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_BracketStyles_AreApplied`,
`SvekSyntaxTests.PlantUmlSvekSyntax_ResolveStyle_Body_DecidesTheStyle`

Asserts the decoration of every entry of PlantUML's `decors1` and `decors2` tables at either end and with decorations at
both ends; asserts the length, hint, and dotted and doubled flags of arrows with every direction word and its short
forms (left and right one long, up and down at least two); asserts that a text without a body is not an arrow; asserts
a color, the line styles, a thickness, and `norank` in brackets; asserts dashed for a dotted body, bold for a doubled
body when the diagram draws it bold, and solid otherwise.

##### CanvasNetPlantUml-Svek-SvekSyntax-Links: Link Statements

**Tests**: `SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_FullStatement_IsRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_Ends_AreReadWithBacktracking`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_RarelyUsedParts_AreRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_SplitLabel_EndLabelsAndMarks_AreTakenOut`,
`SvekSyntaxTests.PlantUmlSvekSyntax_Guillemets_ReplaceStereotypeParts`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_NotALink_ReturnsFalse`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_EndsOfManyParts_AreBounded`

Asserts the ends, the decoration, a quoted end label, the color, and a label with its direction mark of a full
statement; asserts that `A-B` is a link of length 1 and that qualified and quoted names stay whole; asserts the weight,
the roles, the qualifiers, the URL, the stereotype, and the middle decoration; asserts the quoted end labels around a
label and the direction marks at its start or end; asserts the guillemets of `<<include>>`, of a part with one space on
each side (taken away) or two (one kept), of a part after a third `<`, and of a part with a sprite, and none for a part
over two lines, an empty part, a part with another bracket, or angle brackets that make no part, in the label too;
asserts that a class declaration, a name, a note, an arrow without
a second end, and a link followed by other words are not links; asserts that ends of 100 parts
(`MaxLinkEndParts`) are read whole, that an end of 101 parts makes the statement no link, and that a statement of
20,000 parts is no link.

##### CanvasNetPlantUml-Svek-SvekSyntax-Notes: Note Statements

**Tests**: `SvekSyntaxTests.PlantUmlSvekSyntax_TryReadNote_EveryForm_IsRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadNote_UnclosedOrOther_ErrorOrFalse`

Asserts the text, side, target, alias, color, and index of a note attached on one line and as a block, of a note
attached to the last entity, of a floating note on one line and as a block, and of a note on a link; asserts that a
statement that does not start with `note` is not a note.

##### CanvasNetPlantUml-Svek-SvekSyntax-Common: Page Texts And Common Statements

**Tests**: `SvekSyntaxTests.PlantUmlSvekSyntax_TryReadPageText_EveryKind_IsRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_DirectionScaleHideShow_AreRead`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TakeLayoutSettings_NodesepRanksepLinetype`

Asserts a title, an aligned header and footer, a caption, and a legend block; asserts both direction statements, the
scale factors, and `hide` and `show` with what they name; asserts that `nodesep` and `ranksep` are taken out of the
skin parameters with their values, that a value that is not a number is reported, and that `linetype` is reported.

##### CanvasNetPlantUml-Svek-SvekSyntax-Errors: Located Errors

**Tests**: `SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_UnknownStyle_ThrowsLocated`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadLink_BadColor_ThrowsLocated`,
`SvekSyntaxTests.PlantUmlSvekSyntax_ReadColor_NotAColor_ThrowsLocated`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadNote_UnclosedOrOther_ErrorOrFalse`,
`SvekSyntaxTests.PlantUmlSvekSyntax_TryReadPageText_UnclosedOrTooLong_Throws`

Asserts a `plantuml-syntax-error` located at the statement for a style in brackets that is not a style, a color after a
link and after a hash that is not a color, an unclosed note block, and an unclosed title block; asserts a
`plantuml-limit-exceeded` error for a title over the label limit.
