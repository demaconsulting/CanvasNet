### ColorSchemes Unit Verification Design

This document describes the unit-level verification strategy for the `ColorSchemes` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static `ColorSchemes` class (the `Tableau10` list
and its name, `TryGetScheme`, `CategoryColor`, and `Tableau10Color`). In this release `tableau10` is the only scheme;
it is the default category palette of Vega-Lite, and category `i` receives color `i` modulo the number of colors.

#### Verification Approach

The unit is verified by comparing the palette with the ten colors of the documented Vega-Lite default category palette,
written into the test as literals in category order (`#4c78a8`, `#f58518`, `#e45756`, `#72b7b2`, `#54a24b`, `#eeca3b`,
`#b279a2`, `#ff9da6`, `#9d755d`, `#bab0ac`), and by computing the cycling by hand: category 10 is color 0 (`#4c78a8`),
category 24 is color 4 (`#54a24b`), category 107 is color 7 (`#ff9da6`), and category 4 of a three-color list is color 1.
The palette values are small data that the Vega and Vega-Lite configuration publishes; the unit performs no
interpolation and has no other scheme, so there are no recorded outputs of another implementation to compare with. The
unit has no dependencies, so no mocking or stubbing is required; the internal type is reached through the test
assembly's `InternalsVisibleTo` access. The unit's use for ordinal color scales, with the documented fallback for other
schemes, is verified in the _ScaleResolver Unit Verification Design_.

Unit tests reside in `ColorSchemesTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scales` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of an index and a list; no files or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any palette entry, cycled color, lookup result, or exception type that differs from the expectation
constitutes a failure.

#### Unit-Level Test Scenarios

##### Tableau10 Holds The Documented Palette And Cycles

**Tests**: `ColorSchemesTests.ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors`

Asserts that `Tableau10` equals the ten documented colors in order, that its name is `tableau10`, that each index below
ten returns the color at that position, and that larger indexes cycle (10, 24, and 107 give the colors derived above,
also through `CategoryColor` over a user list).

##### Scheme Lookup And Argument Checks

**Tests**: `ColorSchemesTests.ColorSchemes_TryGetScheme_FindsOnlyTableau10IgnoringCase`

Asserts that `tableau10` and `TABLEAU10` are found with ten colors, that another name such as `blues` and a null name
are not found (and give an empty list), and that a negative index throws `ArgumentOutOfRangeException`, an empty list
throws `ArgumentException`, and a null list throws `ArgumentNullException`.
