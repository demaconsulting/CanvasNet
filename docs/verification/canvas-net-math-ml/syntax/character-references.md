### CharacterReferences Unit Verification Design

This document describes the unit-level verification strategy for the `CharacterReferences` unit in the
`DemaConsulting.CanvasNet.MathMl.Syntax` namespace.

#### Verification Approach

The unit is verified by rewriting small texts with `CharacterReferences.Rewrite` and mapping positions with
`LocationMap.MapBack` through the test assembly's `InternalsVisibleTo` access, and by checking the generated table
against facts of the W3C source (its size, two-code-point values, and double-escaped values). Nothing is mocked; the
unit has no dependency to replace. Unit tests reside in `Syntax/CharacterReferencesTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A rewritten text, a mapped location, a table value, or an
error that differs from the expected literal constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Syntax-CharacterReferences-Named: Named References And The Table

**Tests**: `CharacterReferencesTests.CharacterReferences_Rewrite_NamedReference_BecomesNumericReference`,
`CharacterReferencesTests.CharacterReferences_Rewrite_TwoCodePointReference_BecomesTwoReferences`,
`CharacterReferencesTests.CharacterReferences_Table_HoldsTheFullW3cSet`

Asserts that `&alpha;` becomes a numeric reference, that a name of two code points becomes two references, and that the
table holds 2125 names with `nvlt` = U+003C U+20D2, `amp` = `&`, and `InvisibleTimes` = U+2062.

##### CanvasNetMathMl-Syntax-CharacterReferences-Numeric: Kept References

**Tests**: `CharacterReferencesTests.CharacterReferences_Rewrite_XmlPredefinedNames_AreKept`

Asserts that the five XML names and numeric references are left as written.

##### CanvasNetMathMl-Syntax-CharacterReferences-Markup: Where References Are Read

**Tests**: `CharacterReferencesTests.CharacterReferences_Rewrite_InsideCommentCdataAndPi_IsUnchanged`,
`CharacterReferencesTests.CharacterReferences_Rewrite_InsideAttributeValue_IsReplaced`

Asserts that references (even unknown ones) inside comments, CDATA sections, and processing instructions are left
alone, and that references in attribute values of both quotes are rewritten.

##### CanvasNetMathMl-Syntax-CharacterReferences-Unknown: An Unknown Name

**Tests**: `CharacterReferencesTests.CharacterReferences_Rewrite_UnknownName_FailsAtAmpersand`

Asserts `mathml-syntax-error` at the ampersand with a hint.

##### CanvasNetMathMl-Syntax-CharacterReferences-Doctype: A Document Type Declaration

**Tests**: `CharacterReferencesTests.CharacterReferences_Rewrite_Doctype_StopsRewriting`

Asserts that the text after a document type declaration is unchanged (an unknown reference after it is not refused
here) and that the declaration is recorded at line 2, column 1.

##### CanvasNetMathMl-Syntax-CharacterReferences-Encoding: A Foreign Encoding

**Tests**: `CharacterReferencesTests.CharacterReferences_Rewrite_ForeignEncodingDeclaration_FailsAtEncoding`

Asserts that `encoding="windows-1252"` fails with `mathml-syntax-error` at line 1, column 21 with a hint naming UTF-8,
that `us-ascii` (single quotes) fails the same way at its encoding (the text is read as UTF-8), and that `Utf-8` is
accepted.

##### CanvasNetMathMl-Syntax-CharacterReferences-Locations: Mapping Back

**Tests**: `CharacterReferencesTests.CharacterReferences_MapBack_AfterReplacements_ReturnsOriginalColumn`,
`CharacterReferencesTests.CharacterReferences_MapBack_AstralCharacterBeforeReference_CountsUtf16Units`,
`CharacterReferencesTests.CharacterReferences_MapBack_LoneCarriageReturn_CountsAsLineBreak`

Asserts that columns after a replacement map to the original columns and columns inside one map to its ampersand,
that a character outside the basic plane before a reference counts as two columns, and that a lone carriage return
counts as one line break for an error location, a mapped position, and the position of an index.
