## Imaging

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx dpi -->

### Verification Approach

The `Imaging` subsystem is verified at two tiers. The unit tests of `SourceLoader` and `ImageWriter` (see _SourceLoader
Unit Verification Design_ and _ImageWriter Unit Verification Design_ under `imaging/`) verify the loader and the
writer in isolation. The subsystem tests in `ImagingSubsystemTests.cs` verify them working together, the way every
command uses them: a source file of each supported kind (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000, SVG, PDF, PPTX, and
VSDX) is loaded and written as each output format (PNG, JPEG, BMP, and TIFF) - an input-by-output matrix of 40 cases -
and the output is decoded with the codec of its format and compared with the pixels the codec or the document library
decodes or renders from the source (or, for the SVG, with the drawing flattened over white): exactly for a lossless
output, on average for JPEG. The tests generate their inputs in a temporary directory (see `Fixtures/README.md`
for the fixture files and for how the PDF, PPTX, and VSDX documents are written by the test project).

### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Every input kind can be written as every output format with the source's pixels.
- Every page of a document loaded from one opened source is written with the pixels the library renders for it.
- The same input gives the same output bytes on every run, for a lossless and for the lossy output.
- A bad input, or a page that does not exist, is an expected error and leaves no output.

### Test Scenarios

**Imaging_Convert_InputKindToOutputFormat_WritesTheSourcePixels**: for each of the 40 combinations the output decodes
to the source pixels - exactly for PNG, BMP, and TIFF outputs, within a mean channel difference of 8 for JPEG (for the
GIF fixture, a pixel-by-pixel pattern of saturated colors that JPEG's chroma subsampling cannot reproduce, only the
size is checked).

**Imaging_Convert_SameInputTwice_WritesIdenticalBytes**: converting each input kind twice to PNG and to JPEG gives
identical bytes.

**Imaging_Convert_TransparentPng_FlattensOnlyForJpeg**: a PNG with a transparent edge keeps the transparency as a PNG
and is white there as a JPEG.

**Imaging_Convert_RequestedSize_WritesThatSize**: a raster and an SVG loaded at an exact size are written at that size.

**Imaging_Convert_DocumentPages_WritesEachPageWithItsPixels**: both pages of a PDF, a presentation, and a drawing are
loaded from one opened source and written as PNG; each file has the size of its page and exactly the pixels the library
renders at that size.

**Imaging_Convert_CorruptInput_LeavesNoOutput**: a truncated PNG is `corrupt-input`, and neither the output nor a
temporary file exists.

**Imaging_Convert_PageOutOfRange_LeavesNoOutput**: page 3 of a two-page document is `page-out-of-range`, and nothing is
written.
