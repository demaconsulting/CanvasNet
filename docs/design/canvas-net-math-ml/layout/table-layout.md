### TableLayout

![Layout Structure](MathMlLayoutView.svg)

The `TableLayout` unit is a software unit of the `Layout` subsystem. Its source file is `TableLayout.cs`, which defines
the internal sealed class `TableLayout`, the input records `TableInput`, `TableRowInput`, and `TableCellInput`, and the
enumerations `TableColumnAlign`, `TableRowAlign`, and `TableLine`.

#### Purpose

`TableLayout` lays out `mtable`, `mtr`, and `mtd` as MathML Core 3.5 does (a CSS table centered on the math axis), from
the boxes of the contents of the cells that the layout engine laid out, with the cell spans of HTML, and applies the
MathML 3 table attributes that MathML Core dropped (decision D2 of issue #192), as Firefox draws them. The unit's
behavior is specified by the `CanvasNetMathMl-Layout-TableLayout-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/table-layout.yaml`.

#### Data Model

- `TableInput(Table, Style, Rows)`, `TableRowInput(Row, Style, Cells)`, `TableCellInput(Cell, Style, Content)` - the
  elements with their computed styles; `Row` and `Cell` are null for the anonymous rows and cells the engine wraps
  around stray children, and `Content` is the box of the anonymous row of a cell's children.
- Per instance: the math font, the report sink, and the limits of the call. The class holds no other state.
- Per call: the placed cells (row, column, spans, padding, alignment), the owner of every grid slot, the column widths,
  and the row heights and baselines.

#### Key Methods

- `Layout(table, charge)` - lays the table out; `charge` is the engine's delegate that charges the work budget and
  checks cancellation:
  - **Grid** - the HTML algorithm for processing rows: each cell takes the first column of its row that no cell of an
    earlier row spans down into; `columnspan` is 1-1,000 and `rowspan` 0-65,534 (0 spans to the last row, and a span
    past the last row ends there, as browsers end it). Each time the columns grow, the slot count (rows times columns)
    is checked against `MaxTableCells` before anything is allocated; the grid of owners (`rows × columns` integers) is
    then allocated, charged one per slot, and filled, each cell charged for its area.
  - **Padding** - 0.4em left and right and 0.5ex top and bottom at the cell's font size (`MathFont.XHeight`); an
    anonymous cell has none. In the spacing mode below, only the outer sides of the edge cells keep it.
  - **Columns** - each column is as wide as its widest padded cell of one column; then each cell spanning several
    columns (narrower spans first) spreads the width it lacks over them in proportion to their widths, equally when they
    are all empty. Chrome spreads the excess the same way.
  - **Rows** - the baseline of a row lies below its top by the largest padded ascent of its baseline- or axis-aligned
    cells (cells spanning down from it included, in their first row, as Chrome aligns them); its height adds their
    largest padded descent (cells of one row) and is at least the padded height of its other cells of one row; then each
    cell spanning several rows (shorter spans first) spreads the height it lacks in proportion to the row heights.
  - **Positions** - columns and rows follow each other with their gaps, inside the frame and the frame spacing. The
    table's ascent is half its height plus `AxisHeight` at its font size, its descent half its height minus it.
  - **Boxes** - the `mtable` box (completed by the engine) holds a box per row (as wide as the cells, on the row
    baseline, `Element` the `mtr` or null), which holds a box per cell starting in that row (the whole cell rectangle,
    spans and padding included, on the baseline of its first row, `Element` the `mtd` or null), which holds the content.
    Rows and cells get their `mathbackground`. These are the rectangles `getBoundingClientRect` gives in a browser.
  - **Content** - horizontally at the left, center, or right of the cell inside its padding; vertically on the row
    baseline (`baseline`), with its math axis on the axis of the table (`axis`: moved down by the cell's `AxisHeight`
    minus the table's), or at the top, bottom, or center of the cell inside its padding.
- **Extensions (D2, as Firefox draws them)**, read from the elements and reported as `mathml-attribute-invalid` when
  invalid (the whole attribute is then ignored):
  - `columnalign` (`left`, `center`, `right`) - a list over the columns on `mtable` and `mtr`, the last value repeated,
    one value on `mtd`; the innermost wins. `rowalign` (`top`, `bottom`, `center`, `baseline`, `axis`) - a list over the
    rows on `mtable`, one value on `mtr` and `mtd`.
  - `columnspacing`, `rowspacing` (lists of non-negative lengths over the gaps; 0.8em and 1.0ex by default) and
    `framespacing` (one or two lengths; 0.4em 0.5ex by default) switch the table to the spacing mode of Firefox: the
    gaps lie between the cells instead of the inner padding, the outer padding of MathML Core stays, and with a frame
    the frame spacing lies between the frame and the rows.
  - `columnlines`, `rowlines` (`none`, `solid`, `dashed`; lists over the gaps) - a rule of `LineThickness` (1 CSS pixel)
    in the middle of each gap, which grows by the line; a line is drawn across a row or column only where the cells on
    both sides differ, so a spanning cell interrupts it. `frame` (`none`, `solid`, `dashed`) - a rule around the table,
    outside the outer padding. A dashed line is a run of `DashLength` (3 px) dashes and gaps. The rules are `RuleItem`s
    of the table box in the color of the table.

#### Firefox Spacing Model

The Firefox goldens (`Fixtures/reference-firefox`) fixed the model: without a spacing attribute, Firefox pads every cell
by 0.4em and 0.5ex like MathML Core, except the outer sides when the table has no frame; with a spacing attribute, it
puts the spacing between the cells, keeps the outer padding only with a frame, and adds the frame spacing between the
frame and the rows. CanvasNet keeps the outer padding of MathML Core in every case (so a table keeps Chrome's size when
only an alignment is added), which differs from Firefox for a table without a frame. A line widens its gap by one pixel;
Firefox counts that pixel in the cell after the line, so its cell boxes and contents differ by up to one pixel.

#### Error Handling

An invalid attribute value is a warning and ignored; a span over its maximum is clamped with a warning. A grid with more
slots than `MaxTableCells` throws a located `MathMlRenderException` with `mathml-limit-exceeded` before the grid is
allocated; the work budget and cancellation are checked through `charge`. Lengths are clamped by `MathLength` to plus or
minus 1,000,000 px, so the arithmetic stays finite; a table larger than a surface fails in the document's frame check.

#### Dependencies

The `MathFont` unit (`AxisHeight`, the x-height), the `MathBox` unit (boxes, `PlacedBox`, `RuleItem`,
`BackgroundItem`), the `StyleResolver` unit (`MathStyle`), the `MathTree` unit (the elements and their attributes), the
`AttributeValues` unit (lengths), and the core `Canvas` (the colors).

#### Callers

`LayoutEngine` lays every `mtable` out with it, after laying out the contents of its cells.
