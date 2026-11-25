### TableLayout Unit Verification Design

This document describes the unit-level verification strategy for the `TableLayout` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out tables of hand-built cells (elements built directly with their attributes, and
content boxes with chosen widths, ascents, and descents) in a compact style at 100 px, through the test assembly's
`InternalsVisibleTo` access, over the bundled `MathFont` (whose x-height and `AxisHeight` give the expected padding and
axis), and by asserting the positions and sizes of the row, cell, and content boxes, the rules of the lines and the
frame, the reports of a recording sink, and the limit error with the work a recording `charge` delegate saw. The
alignment and spacing of the MathML 3 attributes are also compared with Firefox at the system level
(`MathMlReferenceLayoutTests`). Unit tests reside in `Layout/TableLayoutTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: A recording delegate stands in for the layout engine's work charging
- **Isolation**: In-memory boxes and elements and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A position, a size, a rule, a report, or an exception
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-TableLayout-Grid: Padding, Columns, And Row Baselines

**Tests**: `TableLayoutTests.TableLayout_Layout_Grid_CellsArePaddedAndCentered`,
`TableLayoutTests.TableLayout_Layout_Row_AlignsCellBaselines`,
`TableLayoutTests.TableLayout_Layout_EmptyTable_HasNoSize`

Asserts that a 2 by 2 table has columns as wide as their widest content plus 80 px (0.4em on each side), contents
centered in their cells, rows as tall as their content plus 0.5ex above and below, rows whose ascent and descent are the
largest padded ones of their cells with every content on the row baseline, and that a table without rows or without
cells has no size.

##### CanvasNetMathMl-Layout-TableLayout-Spans: Placement And Spreading Of Spans

**Tests**: `TableLayoutTests.TableLayout_Layout_Spans_SpreadTheirExcess`,
`TableLayoutTests.TableLayout_Layout_RowSpanZero_SpansToTheEnd`

Asserts that a cell spanning two columns of 100 px and 140 px with 380 px of padded content widens them in proportion
(by 140 × 100/240 and 140 × 140/240 px), that a cell spanning two rows spreads the height it lacks in proportion to the
row heights, that spanning boxes cover the rows and columns they span, that a `rowspan` of 0 and a `rowspan` past the
last row end at the last row, and that the cells of the later rows skip the spanned columns.

##### CanvasNetMathMl-Layout-TableLayout-Axis: Centering On The Math Axis

**Tests**: `TableLayoutTests.TableLayout_Layout_Table_IsCenteredOnTheAxis`

Asserts that the ascent of a two-row table is half its height plus AxisHeight (27.8 px) and its descent half its height
minus it, and that the first row's baseline lies its padded ascent below the top.

##### CanvasNetMathMl-Layout-TableLayout-Alignment: Column And Row Alignment

**Tests**: `TableLayoutTests.TableLayout_Layout_ColumnAlign_LeftCenterRight`,
`TableLayoutTests.TableLayout_Layout_RowAlign_TopBottomCenterBaselineAxis`

Asserts that `columnalign="left right"` puts narrow contents at the left padding and the right padding of their columns
(the last value repeated), that an `mtr` `columnalign` and then an `mtd` `columnalign` override it, and that `rowalign`
puts a small content 0.5ex below the top, 0.5ex above the bottom, centered, on the baseline, or with its axis on the
table's axis (a cell at half the font size moves by half AxisHeight), with the `mtr` and `mtd` values overriding the
table's list.

##### CanvasNetMathMl-Layout-TableLayout-Spacing: Spacing Mode

**Tests**: `TableLayoutTests.TableLayout_Layout_Spacing_ReplacesPadding`

Asserts that `columnspacing="10px 30px"` and `rowspacing="5px"` put those gaps between cells that keep only their outer
0.4em and 0.5ex padding, that `framespacing` has no effect without a frame, and that with a solid frame it adds its 7 px
and 9 px between the one-pixel frame and the rows.

##### CanvasNetMathMl-Layout-TableLayout-Lines: Lines And Frame

**Tests**: `TableLayoutTests.TableLayout_Layout_LinesAndFrame_AreRules`

Asserts that a solid column line is a one-pixel rule in the middle of its one-pixel gap, interrupted by a cell that
spans it, that a dashed column line is a run of dashes of at most 3 px, that a row line spans all columns, and that a
solid frame is a rule along each edge of the table, which is two pixels wider.

##### CanvasNetMathMl-Layout-TableLayout-InvalidValues: Reports Of Invalid And Legacy Attributes

**Tests**: `TableLayoutTests.TableLayout_Layout_InvalidAndLegacyAttributes_AreReported`

Asserts that ten invalid values (alignments, a negative spacing, a frame, a line list, three frame spacings, a row
alignment list on `mtr`, a non-numeric `rowspan`, a zero and an oversized `columnspan`) are each reported once as
`mathml-attribute-invalid` and ignored (the oversized span clamped to 1,000), and that the legacy `align`, `equalrows`,
`columnwidth`, and `groupalign` attributes are reported as `mathml-attribute-unsupported` by the engine.

##### CanvasNetMathMl-Layout-TableLayout-Limit: Grid Slots Before Allocation

**Tests**: `TableLayoutTests.TableLayout_Layout_TooManyCells_FailsBeforeAllocation`

Asserts that a cell spanning 1,000 columns over three rows under a limit of 100 slots fails with a located
`mathml-limit-exceeded` error that names `MathMlLimits.MaxTableCells`, after less work than the slots would cost (the
grid was neither allocated nor charged).
