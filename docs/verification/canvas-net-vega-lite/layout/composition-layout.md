### CompositionLayout Unit Verification Design

This document describes the unit-level verification strategy for the `CompositionLayout` unit in the
`DemaConsulting.CanvasNet.VegaLite.Layout` namespace: the internal static `CompositionLayout.Place` and its records
(`CompositionCell`, `HeaderGroup`, `CompositionHeaders`, `CompositionResult`). The unit places the cells of a
concatenation or a facet in a grid and the headers, footers, and titles of a facet around it.

#### Verification Approach

The unit is verified with **pure boxes**: cells are groups of a stated width and height with stated bounds of their
content, headers are empty groups with stated bounds, and the settings are built directly. No data, text, font,
configuration, or canvas is involved, so no expectation depends on a measurement; no mocking or stubbing is used, and
the internal types are reached through the test assembly's `InternalsVisibleTo` access. The unit with real cells, axes,
titles, and the painted canvas is verified at the document level by the concatenation and facet integration scenarios
(`VegaLiteConcatIntegrationTests`, `VegaLiteFacetIntegrationTests`).

Every expected number is **derived by hand** from the rules of Vega's `gridLayout` and `trellisLayout` (the edge of a
cell is rounded up, the room before the origin is rounded up, a margin starts at 0 and is rounded outwards, a title is
rounded half up), never copied from a rendering. The structure of Vega's output (which items are headers and footers,
the offsets of 20 and 10 and the half pixel of a one pixel border) was checked against a scratch run of Vega and
Vega-Lite 6 during planning, never a number. The examples:

- Two cells with bounds `(-30.5, -0.5, 100.5, 110.5)` (a left axis and a one pixel border) at the spacing 20: the second
  starts at ceil(30.5) + 20 + ceil(100.5) = 31 + 20 + 101 = 152, and the union reaches `(-30.5, -0.5, 252.5, 110.5)`.
- Two cells `(-10, -5, 110, 85)` in one column: the second is at 5 + 20 + 85 = 110 below. Three cells of 100 by 50 in two
  columns at the spacings 20 and 10: `(0,0)`, `(120,0)`, `(0,60)`.
- Four uneven cells (100 by 40, 50 by 70 with 15 pixels left of its origin, 60 by 30, 120 by 90) at the spacing 10: with
  `each` the second column starts at 25 + 100 = 125 and the second row at 10 + 70 = 80; with `all` at 25 + 120 = 145 and
  10 + 90 = 100; with `none` the cells of the second row only keep their own margin (110, not 125). With `center`, the
  60 wide cell moves right by (100 - 60) / 2 = 20, the 40 tall cell down by (70 - 40) / 2 = 15.
- Flush: groups of 100 by 50 with far larger content are 20 + 100 = 120 apart.
- A hole (null cell) keeps a zero wide track: cells at 0, 120, 140.
- A grid of 2 by 2 cells with bounds `(-0.5, -0.5, 100.5, 50.5)` at the spacing 20: origins `(0,0)`, `(122,0)`, `(0,72)`,
  `(122,72)` (21 + 101 and 21 + 51); the grid is `(-0.5, -0.5, 222.5, 122.5)`: 223 wide and 123 tall. The row header
  margin is floor(-0.5) = -1, the row footer margin ceil(222.5) = 223, the column footer margin ceil(122.5) = 123;
  offsets move them by their amount (-6, -4, 227, 129 for 5, 3, 4, 6). A row title 10 from headers that reach -41 is at
  x = -51 and at y = round(-0.5 plus 61.5) = 61; a column title at the end 10 below footers that reach 153 is at
  y = 163 and at x = round(-0.5 plus 111.5) = 111; band 0 gives round(-0.5) = 0 (half up) and band 1 gives
  round(122.5) = 123 and round(222.5) = 223.
- Three cells in two columns with two column footers: both footers are at ceil(102 + 80.5) = 183, the second below the
  second column although the last row has no cell there (the nearest earlier cell of its column).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own cells, headers, and settings; no files, fonts, clock, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any position or bounds that differs from the hand-derived value constitutes a failure.

#### Unit-Level Test Scenarios

##### Grid

**Tests**: `CompositionLayoutTests.CompositionLayout_Place_HConcatBoxesAreSpacedByTwentyPlusRoundedMargins`,
`CompositionLayoutTests.CompositionLayout_Place_VConcatColumnsOneStacksCells`,
`CompositionLayoutTests.CompositionLayout_Place_WrapColumnsTwoPlacesThirdCellInSecondRow`,
`CompositionLayoutTests.CompositionLayout_Place_SpacingRowAndColumnDiffer`,
`CompositionLayoutTests.CompositionLayout_Place_AlignEachSizesRowsAndColumnsIndividually`,
`CompositionLayoutTests.CompositionLayout_Place_AlignAllUsesTheLargestCell`,
`CompositionLayoutTests.CompositionLayout_Place_AlignNoneFlowsCells`,
`CompositionLayoutTests.CompositionLayout_Place_CenterCentersSmallCellsInTheirTracks`,
`CompositionLayoutTests.CompositionLayout_Place_EmptyCellsKeepTheirTracks`,
`CompositionLayoutTests.CompositionLayout_Place_CellWithEmptyBoundsIsAPointAndDoesNotCountForMargins`,
`CompositionLayoutTests.CompositionLayout_Place_ZeroColumnsIsOneRow`

Asserts the origins of the cells derived above, for a row, a column, a wrapped grid, different row and column spacings,
the three alignments, centering in both directions, a hole, a cell with empty bounds, and a column count of 0.

##### Headers

**Tests**: `CompositionLayoutTests.CompositionLayout_Place_RowHeadersGoLeftOfTheFirstColumn`,
`CompositionLayoutTests.CompositionLayout_Place_ColumnFootersGoBelowTheLastRowIncludingEmptyColumns`,
`CompositionLayoutTests.CompositionLayout_Place_HeadersAreOffsetByTheirOffset`,
`CompositionLayoutTests.CompositionLayout_Place_MoreHeadersThanRowsAreIgnored`,
`CompositionLayoutTests.CompositionLayout_Place_HeaderWithoutAnyCellToAlignToIsAtZero`

Asserts the margins (rounded outwards), the position of each header at its row or column, the offsets, the nearest
earlier cell for a missing cell and the position 0 without one, and the limit on the number of headers.

##### Titles

**Tests**: `CompositionLayoutTests.CompositionLayout_Place_RowTitleSitsHeaderEdgeMinusOffsetAndIsCenteredByBand`,
`CompositionLayoutTests.CompositionLayout_Place_ColumnTitleAtEndUsesFooterEdgePlusOffset`,
`CompositionLayoutTests.CompositionLayout_Place_TitleBandZeroAndOneAnchorStartAndEnd`,
`CompositionLayoutTests.CompositionLayout_Place_RowTitleAtEndUsesTheFooterEdge`,
`CompositionLayoutTests.CompositionLayout_Place_NoCellsStillPlacesTitlesAtTheOrigin`

Asserts the position of the titles by edge, offset, and band (rounded half up), at the end, without headers, and without
cells.

##### Bounds

**Tests**: `CompositionLayoutTests.CompositionLayout_Place_FlushBoundsUseWidthAndHeightOnly`,
`CompositionLayoutTests.CompositionLayout_Place_FlushHeadersAlignToTheGroupBoxes`,
`CompositionLayoutTests.CompositionLayout_Place_ResultBoundsUnionCellsHeadersFootersTitles`,
`CompositionLayoutTests.CompositionLayout_Place_ItemsAreInVegaZOrder`,
`CompositionLayoutTests.CompositionLayout_Place_DoesNotChangeTheInputGroups`,
`CompositionLayoutTests.CompositionLayout_Place_NoCellsGiveEmptyBounds`,
`CompositionLayoutTests.CompositionLayout_Place_NullArgumentsThrow`

Asserts the flush layout of the cells and the headers, the union of the bounds of everything, the drawing order of the
roles, the unchanged input groups with their other properties kept, the empty result, and the argument checks.
