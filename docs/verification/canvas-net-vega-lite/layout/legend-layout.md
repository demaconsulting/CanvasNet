### LegendLayout Unit Verification Design

This document describes the unit-level verification strategy for the `LegendLayout` unit in the
`DemaConsulting.CanvasNet.VegaLite.Layout` namespace: the internal static `LegendLayout` class (`AxisBounds`, `Place`,
and `Combine`) and its `LegendPlacement` record. The unit places the built legends of a view next to its data rectangle
and its axes and joins their reach with the extents of the axes.

#### Verification Approach

The unit is verified with synthetic `LegendResult` boxes of a stated size and no content, placed against a data
rectangle of 200 by 100 pixels with the real `SpecParser` and `ConfigResolver` (for the layout configuration). No text is
measured, so no expectation depends on a font; no mocking or stubbing is used, and the internal types are reached
through the test assembly's `InternalsVisibleTo` access. The unit with real legends, axes, the layout iteration, and the
painted canvas is verified at the document level by the legend integration scenarios (see the Layout subsystem
verification document and `VegaLiteLegendIntegrationTests`).

The expected numbers are derived by hand from the rules of Vega's view layout (offset 18, margin 8, anchor `start`,
direction `vertical` for the left and right side, rounding half up):

- A right box of 50 by 40: x = ceil(200) + 18 = 218, y = 0; a second box of 60 by 30 is at y = 40 + 8 = 48 and keeps the
  x of the first (alignment `each`). A left box ends at 0 - 18, so it starts at -18 - 50 = -68; a top row ends at -18
  (y = -18 - 40 = -58); a bottom row starts at 100 + 18 = 118, the second box at 50 + 8 = 58.
- The inner orients are at 18 from the corners: top-left (18, 18), top-right (200 - 18 - 50 = 132, 18), bottom-left (18,
  100 - 18 - 40 = 42).
- The anchor `middle` puts the center of a box of 40 at y = 50 (y = 30), the anchor `end` its bottom at y = 100 (y = 60),
  and a row of 60 centers at x = 100 (x = 70).
- A right axis that reaches 230.4 puts a right legend at ceil(230.4) + 18 = 249; a bottom axis that reaches 124.2 puts a
  bottom legend at 125 + 18 = 143, while the anchor `start` of the frame `group` keeps it at x = 0.
- With the bounds `full`, the second of two boxes whose bounds are one pixel larger on every side is placed at
  ceil(41) + 8 + 1 = 50.
- The reach of a legend with bounds (218, -30) to (268, 40) next to axes of (10, 5, 3, 20) is (10, 30, 68, 20).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own configuration and boxes; no files, fonts, clock, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position or bounds that differs from the hand-derived value constitutes a failure.

#### Unit-Level Test Scenarios

##### Orients

**Tests**: `LegendLayoutTests.LegendLayout_Place_RightLegendIsOffsetFromTheDataRectangle`,
`LegendLayoutTests.LegendLayout_Place_LeftLegendEndsLeftOfTheDataRectangle`,
`LegendLayoutTests.LegendLayout_Place_TopAndBottomLegendsFormRows`,
`LegendLayoutTests.LegendLayout_Place_InnerOrientsSitInTheCorners`,
`LegendLayoutTests.LegendLayout_Place_NoneUsesLegendXAndLegendY`

Asserts the positions derived above for every orient, the role of the groups, the union of the bounds, and the reach on
the left.

##### Stacking

**Tests**: `LegendLayoutTests.LegendLayout_Place_RightLegendsStackInAColumn`,
`LegendLayoutTests.LegendLayout_Place_LargestRequestedOffsetWins`,
`LegendLayoutTests.LegendLayout_Place_OrientsArePlacedIndependentlyAndKeepTheOrder`,
`LegendLayoutTests.LegendLayout_Place_FullBoundsPlaceTheBorderInsideTheGrid`

Asserts the margin between boxes, the largest requested offset (30 over 5), independent orients that keep the order of
the given legends, and the full-bounds grid.

##### Configuration

**Tests**: `LegendLayoutTests.LegendLayout_Place_LayoutConfigurationOfTheOrientWins`,
`LegendLayoutTests.LegendLayout_Place_AnchorMovesTheLegendAlongTheSide`

Asserts a margin of 20, the object of an orient that sets the direction and the offset over the layout object, and the
anchors `middle` and `end`.

##### Axes And Extents

**Tests**: `LegendLayoutTests.LegendLayout_Place_LegendsStartAfterTheAxes`,
`LegendLayoutTests.LegendLayout_Combine_TakesTheLargerReachOfEachSide`,
`LegendLayoutTests.LegendLayout_Place_NoLegendsGivesAnEmptyPlacement`

Asserts the two union boxes of `AxisBounds`, the positions beyond the axes, the larger reach on each side, the unchanged
extents without a legend, and the empty placement.
