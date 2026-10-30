### AxisBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `AxisBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal `AxisBuilder` class (`Build` and the static
`GetExtents`) and the `AxisRequest`, `AxisTick`, and `AxisResult` types of its file. The unit builds the axis guide of a
linear, band, or point position scale: the grid (a group of its own that is drawn behind everything), the tick marks,
the labels, the domain line, and the title, as scene items inside an axis group positioned at the data rectangle edge
with Vega's half pixel shift, and it reports the extent that the guide needs beyond the data rectangle so that the
layout can grow the canvas.

#### Verification Approach

The unit is verified by building axes for original specifications through the shared helper `ChartTestHelpers.Axis`,
which runs the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleResolver`, creates the scale model for a
data rectangle of a chosen size, and calls the builder with a real `TextMeasurer`. The assertions are on the scene
items (roles, positions, strings, alignments, baselines, angles, strokes, font sizes), on the tick list (values,
labels, positions), and on the extents. No mocking or stubbing is used; the collaborators are real because the unit's
contract is how the scale, the tick algorithm, the number format, the configuration, and the text measure combine. The
internal types are reached through the test assembly's `InternalsVisibleTo` access.

The expected values are derived by hand from the d3 tick algorithm, the scale arithmetic, and the Vega axis layout
rules, and not recorded from the code:

- A linear x axis of 0 to 100 over 300 pixels asks for ceil(300 / 40) = 8 ticks; d3 `ticks(0, 100, 8)` has 100 / 8 = 12.5,
  power 10, error 1.25 below the square root of 2, so the step is 10 and the ticks are 0, 10, ..., 100 (eleven); a y axis
  of 0 to 50 over 200 pixels asks for 5 ticks, 50 / 5 = 10 has error 1, so the ticks are 0, 10, ..., 50 (six). Tick positions
  are 3 pixels per unit on x and 200 - 4 x value on y, labels sit at the unrounded position, tick marks are 5 pixels
  long, grid lines run the 200 pixels of the data rectangle, the x axis group is at (0.5, 200.5), and `labelFlush` aligns
  the first label left and the last label right while the others are centered.
- Three bands over 300 pixels with the bar padding (inner 0.1, outer 0.05) have step 300 / (3 - 0.1 + 0.1) = 100, band
  width 90, and the first band at (300 - 100 x 2.9) / 2 = 5; the tick of a band is at round(5 + 45 - 0.5) =
  round(49.5) = 50 (half up), so the ticks are at 50, 150, 250, while the labels use the unrounded 49.5, 149.5,
  249.5 and, for a discrete x axis, Vega-Lite's default angle of 270. A point scale has no band offset.
- The bottom axis extent: tick marks are rules from 0 to 5 that grow by half their 1 pixel width (5.5); a 10 pixel label
  hangs from y = 5 + 2 = 7 (the baseline offset round(0.79 x 10) = 8 cancels the box offset round(0.8 x 10) = 8), so its
  box is 7 to 17; the extent is 17; the 11 pixel title hangs from 17 + 4 = 21 and ends at 32, so the bottom extent is 32,
  the group's bounds end at 232, and the side extents are the half pixel shift 0.5. For a left axis, the labels end 7
  pixels left of the data rectangle, so the extent is 7 plus the measured width of the widest label ("50") and the
  rotated title is at x = -(extent + 4), for a total of extent + 4 + 11. `minExtent: 40` pushes the title to 44, and
  `offset: 10` moves the group to y = 210.5 and the bounds to 200 + 10 + 44 + 11.
- Label overlap by parity: 21 ticks 5 pixels apart (`tickCount` 20 over 100 pixels) each have a label wider than the
  spacing; parity keeps every other label until none overlaps: 21 becomes 11 (every 10), then 6 (0, 20, ..., 100,
  where "80" and the right-aligned "100" still overlap), then 3 (0, 40, 80), which no longer overlap.
- Explicit values: `values` of 25, 0, 50, 500, 100 on a 0 to 100 domain over 300 pixels keep the values in range order,
  drop 500, and give positions 0, 75, 150, 300 with the `.1f` labels `0.0`, `25.0`, `50.0`, `100.0`; `tickCount: 2` gives
  100 / 2 = 50, error 5 (from the square root of 10 up to the square root of 50), step 50, so the labels are `0`, `50`,
  `100`; a `$,.2f` format gives `$0.00` and `$100.00`.
- Binned and normalized axes: the twelve weights of the histogram binned into 0 to 35 by 5 over 200 pixels ask for one tick
  per ten pixels (20), which the d3 algorithm turns into the step 2, so the ticks are 0, 2, ..., 34 (eighteen) and the
  explicit `tickCount` of 5 gives 0, 5, ..., 35 (eight); a field that is binned already keeps one tick per 40 pixels
  (5), so the domain 0 to 30 has 0, 5, ..., 30 (seven); a normalized stack has the domain 0 to 1, so a 100 pixel axis
  has the ticks 0, 0.5, and 1, labeled `0%`, `50%`, and `100%` by the default `.0%`, `0.0%`, `50.0%`, and `100.0%` by a
  configured `.1%`, and `0.00`, `0.50`, and `1.00` by an explicit `.2f`.

Unit tests reside in `AxisBuilderTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Guides` folder; the shared helpers `ChartTestHelpers` (`Parts`, `Axis`, `Marks`, and `Of`) are in
`ChartTestHelpers.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through a real `TextMeasurer`; label widths are measured by the test with the
  same measurer where they enter an extent, so no width is a recorded constant
- **Isolation**: Each test builds its own specification text; no files are read; the cancellation scenario uses an
  already canceled token

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any tick value, label, position, alignment, baseline, angle, stroke, font size, extent, or z-order that differs
from the hand-derived expectation, or any exception of the wrong type or without the documented location, constitutes a
failure.

#### Unit-Level Test Scenarios

##### Linear Axis Ticks, Labels, Grid, And Domain

**Tests**: `AxisBuilderTests.AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels`

Asserts the derived x and y ticks, labels, positions, tick marks, grid lines, domain, group offsets, and flush
alignment; that the roles in the axis group are `axis-tick`, `axis-label`, `axis-domain`, and `axis-title` while the
grid is its own group (`axis-grid-group`) at the same offset; that the axis is not in front; and that the y axis is on
the left with its labels right aligned, middle based, 7 pixels left of the data rectangle.

##### Band And Point Axis Categories

**Tests**: `AxisBuilderTests.AxisBuilder_Build_BandAxisUsesExpectedCategoriesAndGrid`

Asserts the derived band ticks, the unrounded label positions and the 270 degree default angle, no grid by default,
`labelAngle: 0` giving centered labels hanging below the tick with `grid: true` drawing a line at each tick, `values`
keeping the listed categories that exist in range order (`a` before `c`, `zzz` dropped), and point scale positions.

##### Title And Extent Match The Analytic Layout

**Tests**: `AxisBuilderTests.AxisBuilder_Build_TitleAndExtentMatchAnalyticLayout`

Asserts the derived title position (150, 21, centered, top baseline, 11 pixel bold black), the bottom bounds and
extents, the left axis title and extent, and the effect of `minExtent` and `offset`.

##### Orientation And Part Switches

**Tests**: `AxisBuilderTests.AxisBuilder_Build_OrientAndPartSwitchesChangeTheItems`

Asserts that a top axis sits at the top edge with ticks pointing up (-5), labels above them (y = -7, bottom baseline),
and its title above the labels (y = -21); that a right axis is at x = 300.5 with left-aligned labels and a title rotated
by 90 degrees; that switching off the domain, ticks, labels, grid, and title leaves an empty group with no extent and
no items; and that `axis: null` produces no axis.

##### Label Overlap By Parity

**Tests**: `AxisBuilderTests.AxisBuilder_Build_OverlappingLabelsAreRemovedByParity`

Asserts the derived result (21 ticks kept, labels `0`, `40`, `80`), that `labelOverlap: false` keeps all 21 labels,
and that the tick marks are not removed.

##### Values, Counts, Formats, Flush, And Rounding

**Tests**: `AxisBuilderTests.AxisBuilder_Build_ValuesCountFormatAndFlushAreHonored`

Asserts the derived explicit values, count, and format results, that without flush the end labels are centered, and that
`tickRound: false` keeps the fractional band positions 49.5, 149.5, 249.5 instead of rounding.

##### Configuration Precedence

**Tests**: `AxisBuilderTests.AxisBuilder_Build_ConfigurationPrecedenceFollowsVegaLite`

Asserts, with `config.axis`, `config.axisX`, `config.axisBottom`, and `config.axisQuantitative` all defining properties,
that the most specific configuration wins (a tick size of 7 from `axisBottom` over 8 and 9, a red domain, a blue
grid, 12 pixel labels, a 14 pixel title), that the axis object of the channel wins over all of them (tick size 4, a
domain width of 3 alongside), that properties nobody names keep Vega's defaults, and that `config.axisBand` applies to
a band scale (tick size 6).

##### Z-Order

**Tests**: `AxisBuilderTests.AxisBuilder_Build_AxesOfRectMarksWithDiscreteChannelsAreInFront`

Asserts that axes are behind the marks by default (a bar chart), in front of a `rect` mark with discrete channels, in
front with an explicit `zindex: 1`, and behind with an explicit `zindex: 0` on a `rect` chart.

##### Titles And Grid Follow The Plan

**Tests**: `AxisBuilderTests.AxisBuilder_Build_TitlesFollowThePlanAndACountHasAGrid`

Asserts that the axis title of an aggregated, binned, or counted channel is the default title of the plan (`Sum of v`,
`v (binned)`, and `Count of Records`), that an explicit title (`Total`) still wins, and that a count (a definition
without a field) and an aggregated measure have a grid while a binned axis has none.

##### Binned Axis Asks For A Tick Per Ten Pixels

**Tests**: `AxisBuilderTests.AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels`

Asserts the derived tick values above: 0, 2, ..., 34 for a binned field over 200 pixels, 0, 5, ..., 35 for an explicit
`tickCount` of 5, and 0, 5, ..., 30 for a field that is binned already.

##### Normalized Stack Uses The Normalized Number Format

**Tests**: `AxisBuilderTests.AxisBuilder_Build_NormalizedStackUsesTheNormalizedNumberFormat`

Asserts the derived labels above for the default, the configured, and the explicit format, that an ordinary stack has no
percent label, and that its axis title is `n`.

##### Invalid Settings, Limits, And Cancellation

**Tests**: `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations`

Asserts that an invalid orient for the channel (`left` on x) and an invalid d3 format (`!!`) throw
`VegaLiteSpecException` at `/encoding/x/axis/orient` and `/encoding/x/axis/format` (with the file and a line); that
more ticks than `MaxTicks` (a continuous axis with a count of 100 over 3,000 pixels and a limit of 5, and a band axis
with three categories and a limit of 2) throw `VegaLiteRenderException` at `/encoding/x` naming `MaxTicks`; that a
canceled token throws `OperationCanceledException`; and that a null request throws `ArgumentNullException`.
