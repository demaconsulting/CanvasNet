### MontageBuilder

<!-- cspell:ignore canvasnet -->

![Imaging Structure](ImagingView.svg)

#### Purpose

`MontageBuilder` is the montage operation of the library: it composes images, and pages of documents, into one contact
sheet. Its single responsibility is the order of the plan, the one-at-a-time loading, and the all-or-nothing write; the
geometry and the pixels of the sheet are those of `ContactSheet`, the loading is `SourceLoader`, the writing
`ImageWriter`. It is the library form of the `montage` command of the `canvasnet` tool. The unit is the static class
`MontageBuilder` (`MontageBuilder.cs`) with the request and result records.

#### Data Model

**MontageRequest** (record): the required `Output` (positional) and the `init` properties `Page` (of each document,
counted from 1; `null` for the first), `AllPages` (off), `Dpi` (none), `Scad` (`ScadViewOptions?`; none), `Strict`
(off), `FileAccess` (the `FileAccessPolicy` of the files every cell's input reads beside it, passed to each cell as
`SourceOptions.FileAccess`; `FileAccessPolicy.AllowAll` by default, `null` refused with `ArgumentNullException`),
`AutoOrient` (on), `Sheet` (the `SheetOptions`; default), and `Write` (`WriteOptions.Default`).

**MontageCell** (record): `Index` and `InputIndex` (positions in a list, from 0), `Info`, `Page` (from 1), `X` and `Y`
(of the cell's image area), `LoadedWidth` and `LoadedHeight`, `Label`, `Diagnostics`, and `Echo`. **MontageResult**
(record): `Layout` (the `SheetLayout`), `Written` (the `WriteResult`), and `Cells`.

#### Key Methods

**Execute(infos, request)**: refuses an empty list as a defect of the caller, and a request with a missing member
(`Output`, `Sheet`, or `Write`) or a missing input in the list as `ArgumentNullException`. It plans one cell for each
input (with
every page, one for each page of a document), calculates the layout with `ContactSheet.CalculateLayout` (a sheet larger
than the maximum is `size-limit`, found before any input is decoded), resolves the output format (the format of the write
options when set, else the extension of the output) and checks the output
against every input (`same-input-output`, `output-exists`). It then opens each input in turn, loads its page into a cell
at the cell size (a vector input rendered at that size, a raster input fitted), adds it to the sheet with the label (the
file name, and `p` and the page number for a document page, when labels are on), and disposes it before the next, so the
memory in use is the sheet and one input. The finished sheet is staged and published with `ImageWriter`.

**Input fragment.** `PlanCells` makes a page that the `SourceInfo.Selection` of an input names the one cell of that
input, whatever `Page` and `AllPages` of the request say (the page is checked there); the other parts of the selection
(variables, view, chart parameters) are applied by `SourceLoader.OpenPages` when the input is opened, so one file named
several times is several cells. `CreateLabel` writes the file name, `#` and the fragment of the selection, and the
`p<page>` suffix only for a page of a document that the fragment does not name, so the cells of one file are
told apart on the sheet (the label strip cuts a text that is wider than the cell, with an ellipsis).

#### Error Handling

`ViewerException` with `page-out-of-range`, `size-limit`, `usage-error` (the output format), `same-input-output`,
`output-exists`, `output-write-failed`, and the codes of `SourceLoader`. A null argument, a request with a missing
member, a
missing input in the list, or an empty list of inputs is a
defect. Any other exception propagates; the sheet and the staged file are disposed on every path.

#### Dependencies

- **ContactSheet** - the layout and the composition.
- **SourceLoader** - `OpenPages`, `BoxOptions`, and `CheckPage`.
- **ImageWriter** - the format, the checks, and the write.
- **ViewerException** and **ErrorCodes** - the expected-error contract.
- **CanvasNet core library** - `Surface` and `Rgba32`.

#### Callers

- **MontageCommand** (of the tool) - for `montage`.
- **Library users** - build a contact sheet of files of any supported kind.
