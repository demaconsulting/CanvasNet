### MontageBuilder

<!-- cspell:ignore canvasnet -->

#### Verification Approach

`MontageBuilder` is verified with unit tests in `MontageBuilderTests.cs` in the Viewer test project. The tests write
small PNG files and the PDF of the fixture factory to a temporary directory, probe them, and call
`MontageBuilder.Execute` with a request of small cells; the expected layout is hand-computed (three cells of 32 pixels in
two columns with spacing and padding of 4). Nothing is mocked; the geometry and pixels of the sheet are verified with the
`ContactSheet` unit.

#### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The cells are placed row by row at the positions of the layout, and the file has the size of the layout.
- A label is the file name, and the page number for a document page; labels make the cells taller.
- A sheet larger than the maximum is `size-limit`, an output that is an input is `same-input-output`, and no inputs are
  a
  defect of the caller.

#### Test Scenarios

**MontageBuilder_Execute_ThreeImages_LaysOutRowByRowAndWritesTheSheet**: three images give 2 columns and 2 rows with the
width and height 76, the written file has that size, and the second cell is to the right of the first and the third is
below it.

**MontageBuilder_Execute_Labels_AreTheFileNames**: with labels the cells are labeled with the file names and the sheet is
taller than without labels.

**MontageBuilder_Execute_DocumentPage_IsLabeledWithThePageNumber**: the first page of the PDF is labeled `source.pdf p1`
and reports page 1 and input 0.

**MontageBuilder_Execute_SheetTooLarge_ThrowsSizeLimit**: 64 columns of 200 pixel cells are `size-limit` and no sheet is
written.

**MontageBuilder_Execute_OutputIsAnInput_ThrowsSameInputOutput**: a sheet whose path is an input is `same-input-output`.

**MontageBuilder_Execute_NoInputs_ThrowsArgumentException**: an empty list of inputs throws `ArgumentException`.

**MontageBuilder_Execute_ExplicitFormatAndExtensionlessOutput_WritesThatFormat** and
**MontageBuilder_Execute_NoFormatAndNoExtension_ThrowsUsageError**: with JPEG in the write options a sheet named `sheet`
is written as a JPEG (it starts with FF D8); without a format a sheet named `sheet` is a `usage-error` and nothing is
written.

**MontageBuilder_Execute_NullRequestMember_ThrowsArgumentNullException** and
**MontageBuilder_Execute_NullInfoElement_ThrowsArgumentNullException**: a request without the output, the sheet options,
or the write options throws `ArgumentNullException` that names the member; a missing input in the list throws it for
`infos` with the position; nothing is written.

**Operations_InputWithPageFragment_UseThePageAndTheLabel** (`InputSelectionTests.cs`): the montage of `source.pdf#page=2`
and `source.pdf` has the labels `source.pdf#page=2` (no page number after it) and `source.pdf p1`, and the pages 2 and
1; a fragment page is the one cell of its input.
