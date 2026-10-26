### MontageCommand

<!-- cspell:ignore canvasnet jpeg jpg tiff tif pptx pdf svg scad -->

#### Verification Approach

`MontageCommand` is verified with unit tests in `MontageCommandTests.cs` (the sheet geometry, the fit, the labels, the
result, and the errors) and `MontageCommandSourceTests.cs` (the pages of documents, mixed kinds, vector cells, models,
and notes). The command is run in-process: the tests create a `Context` (with `--json` when the JSON document is
wanted), call `Run`, and capture the console with the `ToolHost` helper; failures are asserted as the `ToolException`
the command throws. Inputs are generated in a temporary directory: flat-colored PNG images, so that the color at a known
position of the sheet says which input is there, the presentation and the PDF of the test project, and the SVG and the
model fixtures. The sheet is decoded with the PNG codec and its pixels are examined at the positions that the layout
formulas give.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is registered under `montage` with the documented options, the shared ones being the render command's.
- The geometry of the sheet follows the layout formulas; the spacing, padding, and background are where the options put
  them; `contain` and `stretch` place the image of a cell as documented; labels add the strip and draw text in it, and the
  rest of the sheet is the same with and without labels.
- Documents contribute their chosen page or every page in order after the inputs that precede them; a single image
  ignores the page options; images, drawings, documents, and models can be mixed; vector cells are rendered at the cell
  size; the notes of the loader are listed with their input and page.
- The text line and the JSON document have the documented content.
- A sheet that is too large is refused before any input is opened; every mistake is an expected error that leaves no
  output; an output that is an input is refused.
- The same montage twice is identical.

#### Test Scenarios

**MontageCommand_Metadata_DeclaresNameSummaryUsageAndOptions**: the name, no alias, the summary, the usage line with
examples, every option declared once with a description, and `-o` as the alias of `--output`.

**MontageCommand_Run_TwoImages_WritesSheetWithBothCellsAndOneLine**: a red 8x6 and a blue 6x8 image make a 536x272 sheet
in two columns; the text line names the size, the number of images, and the columns; the middle of each cell has the color
of its image and the margin is white.

**MontageCommand_Run_ColumnsAndCell_SetGeometry** and **MontageCommand_Run_SpacingPaddingBackground_ArePlaced**: five
images in three columns of 32x24 cells with spacing 4 and padding 2 make a 108x56 sheet whose fifth cell is in the second
row at (38, 30); two red squares in 20x20 cells with spacing 6, padding 3, and the background `#336699` have red at 3 to
22 and 29 to 48 and the background everywhere else.

**MontageCommand_Run_FitContainAndStretch_PlaceCellContent**: a 20x10 image in a 40x40 cell is 40x20 with white rows above
and below with `contain`, and fills every row with `stretch`.

**MontageCommand_Run_Labels_DrawLabelStripsAndWithoutThemNone**: with labels the sheet is 22 rows taller, every row above
the strip equals the sheet without labels, the strip below the cell has text pixels, and the sheet without labels has
only padding below its cells.

**MontageCommand_Run_Json_ListsSheetCellsAndOutput**: the top-level fields and the fields of the sheet and of a cell in
order, the geometry of two 64x64 labeled cells (152x102), the position of the second cell, its loaded size, the output
with its size on disk, and empty diagnostics.

**MontageCommand_Run_SheetTooLarge_ThrowsSizeLimitBeforeLoading**: three columns of 4000 pixels are `size-limit` even
though the second input is corrupt and would fail if it were opened, and no output exists.

**MontageCommand_Run_NoInputs_ThrowsUsageError**, **MontageCommand_Run_MissingOutput_ThrowsUsageError**,
**MontageCommand_Run_InvalidValues_ThrowUsageError**, and **MontageCommand_Run_NullArguments_ThrowsArgumentNullException**:
no input, no output (with a hint), `--columns` 0, 65, and a word, `--cell` 8x8 and 100, a negative spacing and padding,
an
unknown fit, a background that is not opaque, a quality of 0, and null arguments.

**MontageCommand_Run_OutputIsAnInput_ThrowsSameInputOutput** and
**MontageCommand_Run_NoClobberAndExistingOutput_ThrowsOutputExistsAndKeepsFile**: the output named as the first, a middle,
or the last of three inputs is `same-input-output` and the input is untouched; with `--no-clobber` an existing output is
kept, and without it the output is overwritten by a sheet.

**MontageCommand_Run_CorruptInput_ThrowsCorruptInputAndWritesNothing** and **MontageCommand_Run_MissingInput_ThrowsInputNotFound**:
a PNG cut after 30 bytes and a missing file are expected errors, and no output or temporary file remains.

**MontageCommand_Run_SameInputsTwice_WriteIdenticalBytes**: two runs give the same JSON and the same sheet bytes.

**MontageCommand_Run_AllPages_AddsEveryPageInOrderAfterPrecedingInputs**: a PNG and the two-slide presentation with
`--all-pages` give three cells (the PNG, slide 1, slide 2) in two columns; the document cells name their page and page
count; slide 1 has its red left half and slide 2 its blue top half at the positions of their cells.

**MontageCommand_Run_PageOption_UsesThatPageOfEachDocument**, **MontageCommand_Run_PageAndAllPages_ThrowUsageError**,
**MontageCommand_Run_PageBeyondDocument_ThrowsPageOutOfRangeNamingTheFile**, and
**MontageCommand_Run_PageForSingleImage_IsIgnored**: `--page 2` gives page 2 of a presentation and of a PDF and no page
for a PNG; `--page` with `--all-pages` is a usage error; page 3 of a two-slide deck is `page-out-of-range` naming the
file and writing nothing; `--page 3` and `--dpi 300` with a PNG are ignored.

**MontageCommand_Run_MixedKinds_PngSvgPdfPptxScad_EachCellDrawn** and **MontageCommand_Run_VectorCell_IsRenderedNativelyAtCellSize**:
one input of each of five kinds gives five cells with their formats, each with pixels other than the background; the SVG
and the presentation are loaded at the 96x72 cell size and the small PNG at its own size.

**MontageCommand_Run_ScadOptionsWithoutModel_ThrowsUsageError**: `--view` with only a PNG is a usage error, and with a
PNG and a model it is applied to the model.

**MontageCommand_Run_Diagnostics_AreListedWithTheirInputAndPage** and
**MontageCommand_Run_AllPagesWithLabels_LabelsNamePages**: the skipped picture of slide 1 is listed with its input, page,
source `pptx`, and severity `unsupported`; the labels are `a.png`, `deck.pptx p1`, and `deck.pptx p2`.
