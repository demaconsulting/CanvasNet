### StyleDeclarations Unit Verification Design

This document describes the unit-level verification strategy for the `StyleDeclarations` unit in the
`DemaConsulting.CanvasNet.Mermaid.Configuration` namespace: the internal static `StyleDeclarations` (`ParseItems`,
`ParseBlock`, `ParseSheet`, `NormalizeSelector`, and the value parsers) with `StyleDeclarationSet` and its cascade.

#### Verification Approach

The unit is verified by parsing hand-written style items, declaration blocks, and style sheets and comparing the
declarations, the cascaded values, and the reports with what CSS defines for them: property names are not
case-sensitive, a later declaration wins unless the earlier one is `!important`, a browser drops a declaration with an
unknown property or an invalid value, `1.5em` at a font size of 10 is 15 pixels, `1rem` is 16 pixels, `3pt` is 4
pixels, and an odd dash list is repeated. Reports are collected from a real report sink and asserted by code and
location; strict mode is verified with a sink that throws. The unit's only dependencies are the core `CssColor` and
the report sink, both real, so no mocking or stubbing is required; the internal types are reached through the test
assembly's `InternalsVisibleTo` access.

Unit tests reside in `StyleDeclarationsTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Configuration` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of text with a report sink per test; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any declaration kept or dropped against the documented rule, and any report with another code, location, or
count than asserted, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Configuration-StyleDeclarations-SupportedProperties: Supported Declarations Are Read

**Tests**: `StyleDeclarationsTests.StyleDeclarations_ParseItems_SupportedProperties`

Asserts that `fill`, `Stroke` with `! important`, `stroke-width`, and `color` items are read without a report, that the
important stroke is not replaced by a later `stroke:red`, and that the width and the color are read as 4 pixels and
`#fff`.

##### CanvasNetMermaid-Configuration-StyleDeclarations-Ignored: Unusable Declarations Are Reported

**Tests**: `StyleDeclarationsTests.StyleDeclarations_ParseItems_Ignored_Reported`

Asserts that `text-decoration`, a declaration without a colon, a fill of `nonsense`, and a negative stroke width are
each reported as `mermaid-style-ignored` at the statement's location, and that only the opacity is kept.

##### CanvasNetMermaid-Configuration-StyleDeclarations-Blocks: Separators Inside Quotes And Functions

**Tests**: `StyleDeclarationsTests.StyleDeclarations_ParseBlock_QuotesAndFunctions`

Asserts that a font family with a quoted semicolon and an `rgb()` fill with commas are read whole and an empty
declaration is skipped, without a report.

##### CanvasNetMermaid-Configuration-StyleDeclarations-Cascade: Important Declarations Win

**Tests**: `StyleDeclarationsTests.StyleDeclarationSet_Cascade_HonorsImportant`

Asserts that an important fill survives a later normal fill while the stroke is replaced and a new color is added, and
that a later important fill replaces it.

##### CanvasNetMermaid-Configuration-StyleDeclarations-StyleSheets: Known Selectors Only

**Tests**: `StyleDeclarationsTests.StyleDeclarations_ParseSheet_KnownAndUnsupported`,
`StyleDeclarationsTests.StyleDeclarations_ParseSheet_Strict_Throws`,
`StyleDeclarationsTests.StyleDeclarations_NormalizeSelector`

Asserts that a sheet with a comment keeps the known selectors of a selector list and a child-combinator selector in
order, reports the unknown selector, the at-rule, and the nested rule as `mermaid-theme-css-unsupported` at the given
location, that strict mode throws `MermaidUnsupportedFeatureException` at the first unsupported rule, and the
normalization of white space and of the child combinator.

##### CanvasNetMermaid-Configuration-StyleDeclarations-Values: CSS Values Are Read As A Browser Reads Them

**Tests**: `StyleDeclarationsTests.StyleDeclarations_TryParseLength`, `StyleDeclarationsTests.StyleDeclarations_ValueParsers`

Asserts the lengths of `4px`, `4`, `1.5em`, `1rem`, and `3pt` and the refusal of `-1px` and `wide`; the dash arrays
`5, 5`, `3` (repeated), and `0` (solid) and the refusal of `a b`; the opacity `50%`; the weights `700` and `normal`
and the refusal of `heavy`; the style `oblique 10deg`; and the paint `none` and the refusal of `nonsense`.

##### CanvasNetMermaid-Configuration-StyleDeclarations-FontSizes: Only Drawable Font Sizes Are Accepted

**Tests**: `StyleDeclarationsTests.StyleDeclarations_TryParseFontSize`,
`StyleDeclarationsTests.StyleDeclarations_ParseItems_FontSizeNotDrawable_IsIgnoredWithHint`

Asserts the font sizes of `12px`, `2em` (at 10 pixels), and `32768px` and the refusal of `0`, `1e-300px` (zero
in single precision), `1e300px`, `32769px`, and `1e300em`; and that `font-size:1e-300px` in a style statement is
reported as `mermaid-style-ignored` at the statement's line with a hint naming 32768 and left out, while the
statement's `fill` is kept.
