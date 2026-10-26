### MontageCommand

<!-- cspell:ignore canvasnet jpeg tiff tif jpg pptx vsdx dpi scad openscad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`MontageCommand` is the `montage` command: it composes images, and the pages of documents, into one contact sheet. Its
single responsibility is to turn a command line into one pass of probe, layout, load, compose, and write, and to
report the result; the layout and the composition are `ContactSheet` and the loading is `SourceLoader`. The unit is the
partial class `MontageCommand` in `MontageCommand.cs` (the run, the planning of the cells, and the reporting) and
`MontageCommand.Options.cs` (the option table and the reading of the settings). The command has no alias.

#### Data Model

**OptionTable** (`IReadOnlyList<OptionSpec>`, also the `Options` of the command): `-o`/`--output <file>`,
`--columns <N>`, `--cell <WxH>`, `--fit <mode>`, `--spacing <N>`, `--padding <N>`, `--background <color>` (opaque; its
own declaration), `--labels`, the declaration of `--page` taken from `RenderCommand.OptionTable`, `--all-pages` (its own
declaration: the one of `render` talks about output names), the declarations of `--dpi`, `--no-auto-orient`,
`--strict`, the options of an OpenSCAD model, `--filter`, `--quality`, and `--no-clobber` taken from
`RenderCommand.OptionTable` with `RenderCommand.SpecsFor`.

**Settings** (record): the input paths in the order given, the output path, the page, `AllPages`, the resolution, the
shared `ScadViewOptions`, strict and auto-orient, the `SheetOptions` (columns, cell size, spacing, padding, background,
labels, fit, and filter), and the `WriteOptions`.

**PlannedCell** and **PlacedCell** (private records): a cell before it is loaded (the index of its input and the page)
and after it was drawn (also its index, position, the `SourceInfo`, the size as loaded, the label, and the notes of the
loader).

Defaults: cell 256x256, spacing 8, padding 8, a white background, `contain`, and the number of columns that
`ContactSheet` derives from the number of cells.

#### Key Methods

**ReadSettings(reader)**: requires at least one positional argument and `-o` (the hint shows an example); rejects
`--page` together with `--all-pages`; reads `--background` with `ParseColor` and rejects a color that is not opaque;
reads `--cell` with `ParseSize` and requires each side to be at least 16 (`ContactSheet.MinCell`); reads `--columns` (1
to 64), `--spacing` and `--padding` (0 to the maximum image size), `--fit` (with `CompareCommand.ParseFit`),
`--filter`, `--quality`, `--page` (`ParsePage`), `--dpi` (`ParseDpi`), and the shared options of a model
(`RenderCommand.ReadScadOptions`).

**Run(context, args)**: reads the settings; without `--all-pages` the number of cells is the number of inputs, so
`ContactSheet.CalculateLayout` is called first and a sheet that is too large is refused with `size-limit` before any
input is opened; then probes every input (`SourceLoader.Probe`), checks that model options were given only when an
input is a model, plans the cells (`PlanCells`), calculates the layout (again, with the real count, so that with
`--all-pages` the check happens after the page counts were read from the headers), and checks the output with
`ImageWriter.ResolveFormat` and `ImageWriter.CheckTargets` against every input. Then it creates the `ContactSheet`
and, for each input in order, opens it once (`SourceLoader.OpenPages` with `BoxOptions` for the cell size) and for each
of its cells loads the page, adds it to the sheet (which fits it into the cell and draws the label), and disposes it
before the next, so the memory in use is the sheet plus one input. Last, the sheet is detached, staged with
`ImageWriter.Stage`, committed, and reported; a `finally` block deletes the temporary file when the commit did not
happen.

**PlanCells**: one cell for an input that is a single image or a model; for a document, the page chosen with `--page`
(the first by default, checked with `SourceLoader.CheckPage`, whose `page-out-of-range` names the file) or, with
`--all-pages`, every page in ascending order. The cells keep the order of the inputs on the command line.

**CreateSourceOptions**: the options of one input: the resolution only for a document, the model options only for a
model, the strict and auto-orient switches, the filter, and the sheet background as the background of the loader (so
the margin of a document page that is fitted into the cell is the background of the sheet).

**CreateLabel**: the file name of the input (`Path.GetFileName`), followed by a space, `p`, and the page number for a
page of a document, for example `deck.pptx p2`; `ContactSheet` cuts it to the width of the cell.

**Text result**: `wrote {path} ({w}x{h} {format}, {bytes} bytes): {n} image(s) in {c} column(s)`, for example
`wrote sheet.png (1048x552 png, 52000 bytes): 6 images in 3 columns`.

**JSON result**: `ResultWriter.Success(Name, ...)` with these fields after the envelope, in this order: `tool`
(`name`, `version`); `sheet` (`width`, `height`, `columns`, `rows`, `cellWidth`, `cellHeight`, `labelHeight`,
`spacing`, `padding`, `fit`, `labels`, `background`); `cells` (for each cell `index`, `column`, `row`, `x`, `y` - the
top-left corner of its image area -, `input`, `format`, for a document `page` and `pageCount`, `loadedWidth`,
`loadedHeight`, and `label` only with `--labels`); `outputs` (one object: `path`, `format`, `width`, `height`,
`bytes`); `diagnostics` (the notes of the loader, each with its `input`, for a document its `page`, `source`,
`severity`, `code`, and `message`); and `echo`.

#### Error Handling

Command-line mistakes (no input, no output, an unknown option, a malformed or out-of-range value, a cell side below 16,
a background that is not opaque, `--page` with `--all-pages`, and a model option when no input is a model) are
`ToolException` with `usage-error`; a sheet or a size beyond the maximum is `size-limit`, found before any input is
loaded (with `--all-pages`, after the page counts were read); a page beyond the end of a document is
`page-out-of-range`; an output that is one of the inputs is `same-input-output`, an existing output with
`--no-clobber` is `output-exists`, and a path that cannot be written is `output-write-failed`; input failures come from
`SourceLoader` (`input-not-found`, `input-unreadable`, `unsupported-format`, `corrupt-input`, `unsupported-feature`,
`scad-error`, and `budget-exceeded`). Every one is an expected error with exit code 1 and leaves no output. An
exception that is not a `ToolException` is a defect and propagates. Null arguments throw `ArgumentNullException`.

#### Dependencies

- **ArgumentReader** - parsing and the value grammars.
- **ResultWriter** - the JSON document.
- **Context** - the `Json` flag and `WriteLine`.
- **Program** - the tool version for the JSON result.
- **RenderCommand** and **CompareCommand** - the shared option declarations and the reading of the model options and of
  `--fit`.
- **SourceLoader**, **ContactSheet**, and **ImageWriter** (Imaging) - everything that touches files and pixels.

#### Callers

- **CommandRegistry** - `CreateDefault` registers the command under `montage`.
