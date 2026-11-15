### ScenePositions Unit Verification Design

This document describes the unit-level verification strategy for the `ScenePositions` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal static class that turns the tags of the guide
groups, the marks groups, and the mark items of a laid-out chart into `ImagePosition` values in the pixels of the
surface a placement draws on (`ScenePositions.Report`, `Collect`, `ViewPath`, `ViewName`, `FacetValues`, and
`Geometry`).

#### Verification Approach

The unit is verified at two levels. The geometry, the ids, the order, the filter, and the placement are verified on
small scenes built by hand from `SceneModel` records: a view group at (10, 20) of 100 by 50 tagged with the path
`hconcat[1]` and the name `right`, holding a marks group whose `SceneMarkContext` gives each row the value `x` = the
row number, with bars, a line with two vertices, or a bar without a recorded row; and a rule, a square symbol, a text,
and a half-disk arc passed to `Geometry` directly. The expectations are worked out by hand from the item geometry. The
conversion of a data value to a position value is verified on `MarkBuilder.TryConvert`, which the marks context of a
real chart calls.

The view paths and the values that come from a real view tree (the layers a composite mark expands to, a stacked
channel, a bin, an aggregate, a temporal field, and a facet cell) depend on the internal `LayerNode` tree that the
specification model, the normalizer, and the composition build, which is impractical to build by hand. These
requirements are therefore verified through `VegaLiteDocument.Render` with small in-memory specifications (see
_VegaLiteDocument Unit Verification Design_): this test dependency on the whole pipeline is deliberate, and the
pipeline's stages are verified under their own verification designs.

The unit depends on the `SceneModel` unit, the `ScenePlacement` of the `ScenePainter` unit, the `TextMeasurer` of the
`Guides` subsystem with the bundled fonts, and the core `Positions` types, which are real, so no mocking or stubbing is
required; the internal types are reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `ScenePositionsTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder, and the document-level tests in `VegaLiteDocumentPositionTests.cs` in the project root.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts, through the text measurer with the default font options
- **Isolation**: Each test builds its own scene or renders its own chart; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A path, an id, a value, a box, or a pixel that differs from the hand-derived value by more than the stated
precision, a position out of the documented order, or a mark reported without a recorded row constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetVegaLite-Scenegraph-ScenePositions-ViewPaths: Canonical View Paths And Names

**Tests**: `VegaLiteDocumentPositionTests.VegaLiteDocument_Render_CompositionPaths_AreCanonical`,
`VegaLiteDocumentPositionTests.VegaLiteDocument_Render_BoxplotStackAndBin_ValuesAndViews`

Asserts that the two charts of a vertical concatenation are the views `vconcat[0]` and `vconcat[1]`, the second with
the name `lower` reported separately, that the second chart's `y` axis is in `vconcat[1]` and the point of its second
layer is `point-1` in `vconcat[1].layer[1]`, and that the mark of the second cell of a column facet is in `facet[1]`
with the `column` value `y`; and that every mark of a boxplot (its rules and ticks among them) is in the root view
`""`, the layers the composite mark expands to adding no segment.

##### CanvasNetVegaLite-Scenegraph-ScenePositions-Ids: Deterministic Ids

**Tests**: `ScenePositionsTests.ScenePositions_Collect_ViewAndBars_BoxesCentersAndIds`,
`ScenePositionsTests.ScenePositions_Collect_LineVertices_OnePositionPerVertex`

Asserts that the view is named by its path `hconcat[1]` and its two bars `bar-0` and `bar-1` with the mark type `bar`,
and that the two vertices of a line are `line-0` and `line-1`.

##### CanvasNetVegaLite-Scenegraph-ScenePositions-Values: Normalized Values Of The Row

**Tests**: `ScenePositionsTests.MarkBuilder_TryConvert_DataValues_ConvertAsDocumented`,
`VegaLiteDocumentPositionTests.VegaLiteDocument_Render_AggregateAndTemporalLine_ValuesAndVertices`,
`VegaLiteDocumentPositionTests.VegaLiteDocument_Render_BoxplotStackAndBin_ValuesAndViews`

Asserts that the date 2024-11-01 converts to the text `2024-11-01T00:00:00.000Z`, a NaN number and a null to null,
and the text `A` to itself; that the first vertex of a line of the sum of `v` over dates has `x` =
`2025-01-01T00:00:00.000Z` and `y` = 8 (the sum of 3 and 5); that two stacked bars report their own values 3 and 5 on
`y`, not the stacked ends; and that a binned bar reports the start of its bin on `x` and a greater end on `x2`. The
facet value of a cell is asserted by the view-path scenario above (the `column` value `y` of the mark in
`facet[1]`).

##### CanvasNetVegaLite-Scenegraph-ScenePositions-Data: Data Fields Of The Row And The Channel Names

**Tests**: `VegaLiteDocumentPositionTests.VegaLiteDocument_Render_MonthTimeUnitLine_DataKeepsTheSourceRow`,
`VegaLiteDocumentPositionTests.VegaLiteDocument_Render_AggregatedNestedAndWideRows_DataAsDocumented`,
`ScenePositionsTests.MarkBuilder_TryConvert_DataValues_ConvertAsDocumented`,
`ScenePositionsTests.ScenePositions_Collect_MarkContextData_ReportsDataAndChannelNames`

Asserts, on a line chart with `point: true` whose `x` is a `month` time unit of `month` and whose rows hold
`2025-10-01` and `2025-11-01` for two regions, that `mark:month=2025-11-01+region=Americas` selects exactly the line
vertex and the point of November Americas (at the same pixel), each with the data `month` = `2025-11-01` (the text
the data gave), `region`, and `revenue` = 6.8 and no time-unit field, with `x` = `2012-11-01T00:00:00.000Z` and `x` among
the channel names, while `mark:x=2025-11-01+color=Americas` selects nothing; that a stacked bar of the sum of `v` by a
`month` time unit and a color has the data `month_m`, `r`, and `sum_v` (no stack fields) with the sum 3 for `r` =
`A`; that a binned point chart with a `calculate` has the data `a`, `b`, `none` (null), `ok`, and `twice` = 6,
without the nested object, the array, or the bin fields; and that a row of 40 fields gives the first 32 by name. Asserts
that `TryConvertData` gives a date read from text as that text and any other date as ISO text, while `TryConvert`
gives the ISO text for both, and that an array is left out; and that a marks group whose context gives data reports it
per row as data (not as values) with the shared `ChannelNames`, which hold `x` and `color` and not `region`, while a
context without `Data` gives none.

##### CanvasNetVegaLite-Scenegraph-ScenePositions-Geometry: Boxes And Attachment Pixels Per Mark Type

**Tests**: `ScenePositionsTests.ScenePositions_Collect_ViewAndBars_BoxesCentersAndIds`,
`ScenePositionsTests.ScenePositions_Geometry_RuleSymbolTextArc_AimAsDocumented`,
`ScenePositionsTests.ScenePositions_Collect_LineVertices_OnePositionPerVertex`

Asserts that the view's box is its data rectangle (10, 20, 100, 50), that a bar at (0, 10) of 20 by 40 has the box
(10, 30, 20, 40) and the pixel (20, 50) at its center, and that a bar of negative height has a box of positive height
(40, 40, 20, 30). Asserts that a rule from (0, 0) to (40, 20) aims at its midpoint (20, 10) with the box (0, 0, 40,
20); that a square of size 16 at (10, 10) aims at its center with a box from x = 8 to x = 12; that a text at (5, 6)
with the offsets (2, 3) aims at its anchor (7, 9) with a measured box that is not empty; and that a half disk of
radius 10 from 12 o'clock clockwise to 6 o'clock aims at its mid-angle at mid-radius (5, 0) with a box from x = 0 to
x = 10. Asserts that the second vertex of a line at (10, 10) in the view, drawn at scale 2 with the offset (1, 1), has
the zero-size box (41, 61, 0, 0) and the pixel (41, 61).

##### CanvasNetVegaLite-Scenegraph-ScenePositions-Filter: Guides First, Then Marks With Recorded Rows

**Tests**: `ScenePositionsTests.ScenePositions_Collect_NoDatumOrFilteredOut_ReportsNoMarks`,
`VegaLiteDocumentPositionTests.VegaLiteDocument_Render_BarChart_ReportsGuidesAndBars`

Asserts that a bar without a recorded row reports nothing (the view alone is reported) and that a filter that accepts
only `view` skips a bar whose row is recorded; and that a titled bar chart reports the title, the view, the `x` and
`y` axes, and then its three bars, in that order.
