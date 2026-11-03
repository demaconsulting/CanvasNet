### CompositionLayout

![Layout Structure](VegaLiteLayoutView.svg)

The `CompositionLayout` unit is the third software unit in the `Layout` subsystem. It is two source files,
`CompositionLayout.cs` (the records and the grid) and `CompositionLayout.Headers.cs` (the trellis part: headers,
footers, and titles), in the `DemaConsulting.CanvasNet.VegaLite.Layout` namespace, and defines the `internal static
partial` class `CompositionLayout` and the records `CompositionCell`, `HeaderGroup`, `CompositionHeaders`, and
`CompositionResult`. Both files carry the provenance header comment `Portions derived from Vega (BSD-3-Clause)`: the
layout is a port of `gridLayout` and `trellisLayout` of `vega-view-transforms` (`layout/grid.js`), attributed in
`third-party/README.md`.

#### Purpose

`CompositionLayout` decides where the charts of a concatenation, a facet, or a repeat go and where the headers, footers,
and titles of a facet go around them. The cells are built (and measured) by the rendering pipeline, each relative to its
own origin; this unit turns their boxes into positions, so that the layout rule of Vega is defined once and can be
tested with plain boxes, without text, fonts, data, or a canvas. The `Composer` of the rendering pipeline calls it for every
concatenation and facet. The unit's behavior is specified by the
`CanvasNetVegaLite-Layout-CompositionLayout-*` requirements in
`docs/reqstream/canvas-net-vega-lite/layout/composition-layout.yaml`.

#### Data Model

- **`CompositionCell`** `(Group, Bounds)` - the built `SceneGroup` of a cell, and the bounding box of everything in it
  (data rectangle, axes and legends it owns, its title, the stroke of its border) in the coordinates of the cell, that
  is relative to the origin of the group; empty for a cell without content. The width and height of the group are the
  box that `bounds: "flush"` lays out. The position of the group is replaced by the layout.
- **`HeaderGroup`** `(Group, Bounds)` - the built group of a header, footer, or title of a facet, at the origin, with its
  bounds in its own coordinates.
- **`CompositionHeaders`** - the lists `RowHeaders`, `RowFooters`, `ColumnHeaders`, `ColumnFooters` (one entry per row or
  column), the `RowTitle` and `ColumnTitle`, the offsets of the headers and footers from the cells (default 0), the
  offsets of the titles (Vega-Lite uses 10), whether a title is at the end (after the footers) and its band (default
  0.5).
- **`CompositionResult`** `(Items, Bounds, CellOrigins)` - the placed groups in drawing order, the union of the full
  bounds of everything placed, and the origin of every cell (also of a null one).
- The settings are the `CompositionSettings` record of the `SpecModel` subsystem (`Columns`, the two spacings, the two
  alignments `CompositionAlign`, the two `center` flags, and `Flush`), which the normalizer reads from the specification
  with Vega-Lite's `extractCompositionLayout` rules.

#### Key Methods

`Place(cells, headers, settings)` is the one entry point. A cell that is `null` is an empty cell. It works in five steps:

1. **Boxes.** With `Flush` the box of a cell is `(0, 0, width, height)` of its group; otherwise its `Bounds` (the point
   at the origin when the bounds are empty or the cell is `null`), as Vega's `bboxFlush` and `bboxFull`.
2. **Grid.** `LegendBuilder.GridLayout` (the port of `gridLayout` that the legend layout shares) with the number of
   columns (none or 0: one row), the two spacings as padding, the two alignments (`each`, `all`, `none`) and the two
   `center` flags lays the boxes out: a column starts after the widest cell above (the right edge rounded up) plus the
   spacing and the room a cell needs left of its origin (rounded up); `all` uses the largest cell of the grid for every
   track; `none` lets each cell follow its neighbor with its own margin; `center` moves a smaller cell by half the room
   that is left in its track, as Vega does (which only reaches the cells at the start of a track). The anchor is the
   origin, so every origin follows from the boxes.
3. **Headers.** `LayoutHeaders` (Vega's `layoutHeaders`) finds the margin of one kind of header: the edge of the cells of
   the first column (left edge, row headers), the first row (top, column headers), the last column (right, row footers),
   or the last row (bottom, column footers), starting at 0, rounded down (headers) or up (footers) after each cell, and
   adding the offset (as a negative number for headers) only when there are headers; without headers the margin without
   the offset is the result. Each header is then placed at the origin of its row or column (the nearest earlier cell of
   that row or column when its cell is missing, for the empty slots of the last row of a wrapped grid, and at 0 when
   there is none): column headers and footers at `(x of the cell, margin)`, row headers and footers at
   `(margin, y of the cell)`; headers beyond the number of rows or columns are ignored as in Vega. The returned edge is
   the margin rounded again over the bounds of the headers placed, and is what the titles are placed from. With `Flush`
   the edges of a cell are those of its group, with full bounds a cell without content takes no part.
4. **Titles.** `PlaceTitle` (Vega's `layoutTitle`): the row title is at `x = edge - offset` of the row headers (the
   footer edge plus the offset when it is at the end) and `y = round(grid.y1 + band * grid.height)`; the column title
   symmetrically. The grid bounds are those of the full bounds of the cells (the point at the origin when there are
   none). The rounding is half up (`Math.round` of JavaScript).
5. **Result.** The groups are copies with the new position (`with { X, Y }`; the inputs stay unchanged), in the drawing
   order of Vega-Lite (`assembleHeaderMarks` and `assembleGroup`): the row title and the column title, the row headers
   and footers, the column headers and footers, then the cells. The bounds are the union of the full bounds of the cells
   and the bounds of the headers, footers, and titles (always full, also when the grid is flush, as in Vega where the
   layout of the enclosing group unions the full bounds).

#### Error Handling and Limits

The unit throws only `ArgumentNullException` for a null cell list or settings. It loops over the cells and headers of
one composition, which the caller has bounded (the number of views is limited by `MaxViews` before the cells are made)
and charged to the scene budget when it built them, so the unit needs no limit and no cancellation point and creates no
scene items of its own (it only copies the groups it is given to move them).

#### Interactions

The rendering pipeline (`Composer`) builds each cell with `ViewLayout.ResolveSize` and
`ViewLayout.CellBounds`, the axes of a facet as header groups, and calls `Place` for each concatenation and facet; the
bounds of the result go to the legend layout and to `ViewLayout.Measure` (through `LegendLayout.Combine`). The unit
consumes `LegendBuilder.GridLayout` and `TextMeasurer.RoundHalfUp` of the `Guides` subsystem, `CompositionSettings` and
`CompositionAlign` of the `SpecModel` subsystem, and the scene types of the `Scenegraph` subsystem (whose `SceneGroup`
got settable-at-creation `X` and `Y` so that a built group can be moved with a `with` expression). It is stateless and
safe for concurrent use.

#### Deviations from Vega

`headerBand` and `footerBand` (alignment of the headers to the middle of the cell instead of its origin, which Vega-Lite
uses only for a child without a size signal) are not supported: every cell here has a size, so the headers are at the
origin of their cell. A header that has no cell to align to is placed at 0 where Vega would fail. The headers beyond the
limit are dropped without a warning (the caller never gives more than one header per row or column).
