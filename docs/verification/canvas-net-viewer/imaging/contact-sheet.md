### ContactSheet

<!-- cspell:ignore canvasnet ellipsis luma -->

#### Verification Approach

`ContactSheet` is verified with unit tests in `ContactSheetTests.cs`. The layout is checked as arithmetic on
hand-computed examples (five cells of 100 by 80 in two columns with spacing 10 and padding 5 is a 220 by 270 sheet), and
the composition by looking at the pixels of small sheets made from flat-colored images, so that the expected position,
fit, and background of every region is known exactly. Labels are checked by examining the pixels of the label strip -
text is present in the strip and nowhere else, and its color follows the background - rather than by comparing with a
reference rendering, which keeps the tests independent of glyph shapes; truncation is checked with the real bundled font
by measuring the text. The unit needs no files and no mocking.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces and the bundled font.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Rows, sheet size, and cell origins follow the documented formulas, with and without labels.
- A sheet larger than 8192 pixels on a side is refused with `size-limit` before any pixel is allocated.
- A contained image keeps its aspect ratio, fills the limiting side, and is centered on the background; a stretched image
  fills the cell; images go to the cells in order; transparency is flattened.
- Labels are drawn in the strip, in the color that suits the background, cut with ASCII dots to fit the cell, and not at
  all when disabled.
- Invalid arguments are rejected; the same inputs give identical pixels.

#### Test Scenarios

**ContactSheet_CalculateLayout_ColumnsAndCount_ComputesRowsAndSheetSize**,
**ContactSheet_CalculateLayout_AutomaticColumns_IsCeilingOfSquareRoot**,
**ContactSheet_CalculateLayout_WithLabels_AddsLabelStripPerRow**, and
**ContactSheet_CellOrigin_RowMajor_UsesPaddingAndSpacing**: five cells of 100 by 80 in two columns have three rows and a
220 by 270 sheet; 1, 2, 3, 4, 5, 10, and 17 images have 1, 2, 2, 2, 3, 4, and 5 automatic columns; labels add 22 pixels
per row and no width; the cell origins are row-major with the padding, spacing, and label strip, and an index outside
the sheet throws.

**ContactSheet_CalculateLayout_SheetTooLarge_ThrowsSizeLimit**: 40 rows of 256 pixels, and 33 columns of 256 pixels, are
`size-limit` with a hint about `--cell`; a sheet of exactly 8192 pixels is accepted.

**ContactSheet_Add_Contain_KeepsAspectAndCentersOnBackground**, **ContactSheet_Add_Stretch_FillsTheWholeCell**,
**ContactSheet_Add_SeveralImages_FillCellsInRowMajorOrder**, and **ContactSheet_Add_TransparentImage_IsFlattenedOverBackground**:
a 40 by 20 image in a 20 by 20 cell becomes 20 by 10, centered, with exactly 200 non-background pixels on the sheet;
stretched it fills all 400 pixels of the cell; three images go to the cells at (4, 4), (26, 4), and (4, 26) with the
background between them; a fully transparent image shows the background and the sheet has no transparent pixel.

**ContactSheet_Add_Labels_DrawsTextInStripAndNothingWithoutLabels**: with labels the sheet is 22 pixels taller, the strip
below the cell holds text pixels while the padding around the sheet does not, the sheet without labels holds only the
image, and a `null` label draws nothing.

**ContactSheet_Add_DarkBackground_UsesLightText** and **ContactSheet_Add_LightBackground_UsesDarkText**: on black the
text pixels are gray levels up to white, and on white they are gray levels down to black.

**ContactSheet_FitLabel_LongText_IsTruncatedWithAsciiEllipsisToFit**: a long file name is cut to a beginning followed by
`...` that measures at most the cell width while one more character would not fit; a short name is unchanged; a width
too small for any beginning gives `...`; null arguments throw. The cut itself is verified in depth by the
`TextRenderer_TruncateToWidth_*` unit tests of the core library; two characterization tests written before the label
fitting was delegated to it pin the behavior that must not change.

**ContactSheet_FitLabel_SurrogatePairText_NeverSplitsPair**: a label of astral characters is cut at many widths and every
result encodes strictly (no lone surrogate), is a whole-character beginning plus `...`, and fits.

**ContactSheet_FitLabel_EmptyText_ReturnsEmpty**: an empty label stays empty for the smallest and a wide cell.

**ContactSheet_Add_MoreImagesThanCells_Throws** and **ContactSheet_Constructor_InvalidArguments_Throw**: a second image
for a one-cell sheet, a second `Detach`, and an `Add` after `Detach` throw; every invalid option (count below 1, columns
-1 and 65, cell sides of 15, negative spacing and padding, a translucent background, an undefined fit mode or filter)
throws, and a cell of 16 and 64 columns are accepted.

**ContactSheet_Detach_SameInputsTwice_ProduceIdenticalPixels**: two sheets composed from the same two images and labels
(one of them cut) are pixel-identical.
