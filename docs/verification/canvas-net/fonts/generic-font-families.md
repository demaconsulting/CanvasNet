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

**Tests**: `GenericFontFamilies_SplitFamilyList_HonorsQuotesAndDropsEmptyNames`,
`GenericFontFamilies_SplitFamilyList_HonorsBackslashEscapes`,
`GenericFontFamilies_SplitFamilyList_HandlesBackslashNewline`

Asserts that `"Helvetica Neue", Arial ,  sans-serif` gives three names, that `'A, B', serif`
gives two (the comma inside the quotes stays), that surrounding white space is trimmed, that
empty entries and a trailing comma are dropped, that an apostrophe inside double quotes is kept,
and that commas alone, an empty string, white space and `null` give an empty list. The escape test
asserts that an escaped quote inside a quoted name, an escaped comma outside quotes and an escaped
backslash are decoded without ending the string or splitting the list, that a hexadecimal escape
(with its one optional trailing white-space character, up to six digits) gives that code point, that
an out-of-range code point gives U+FFFD, and that a trailing lone backslash is dropped. The
backslash-newline test asserts that a backslash before a line feed, carriage return, form feed or
CR LF inside a quoted name joins the two lines (nothing is contributed), that the same sequence in
an unquoted name makes the whole list empty, that the white space a hexadecimal escape consumes is
a space, a tab or a newline (CR LF as one) and not a no-break space or U+2028, and that a trailing
lone backslash is still dropped.
