### GenericFontFamilies Unit Verification Design

This document describes the unit-level verification strategy for the `GenericFontFamilies` class.

#### Verification Approach

`GenericFontFamiliesTests.cs` calls `TryClassify` and `SplitFamilyList` directly with literal
inputs and asserts the exact outputs; the class is pure and stateless, so no fixtures, files or
mocks are needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: No shared state

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed
for each requirement ID passes across every target framework and every operating system in this
repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Fonts-GenericFontFamilies-Classification: Keywords And Aliases Are Classified

**Tests**: `GenericFontFamilies_TryClassify_Keywords_AreClassifiedWithAndWithoutAliases`,
`GenericFontFamilies_TryClassify_Aliases_AreClassifiedOnlyWhenRequested`,
`GenericFontFamilies_TryClassify_OtherNames_AreNotClassified`

Asserts that each CSS generic keyword (including `cursive` and `fantasy` as sans-serif, and
mixed-case spellings) is classified with the expected design both with and without the common
aliases; that representative aliases of each design (including the Liberation family names) are
classified only when the aliases are requested; and that an unknown name, an empty name, `null`,
a name with a leading space and a quoted name are not classified (nothing is trimmed, unquoted
or otherwise folded), with both outputs `false`.

##### CanvasNet-Fonts-GenericFontFamilies-SplitFamilyList: Lists Are Split Honoring Quotes

**Tests**: `GenericFontFamilies_SplitFamilyList_HonorsQuotesAndDropsEmptyNames`

Asserts that `"Helvetica Neue", Arial ,  sans-serif` gives three names, that `'A, B', serif`
gives two (the comma inside the quotes stays), that surrounding white space is trimmed, that
empty entries and a trailing comma are dropped, that an apostrophe inside double quotes is kept,
and that commas alone, an empty string, white space and `null` give an empty list.
