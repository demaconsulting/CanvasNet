### ImageRenderer

<!-- cspell:ignore canvasnet -->

#### Verification Approach

`ImageRenderer` is verified with unit tests in `ImageRendererTests.cs` in the Viewer test project. The tests write real
files (a gradient PNG, and the two-page PDF of the fixture factory) to a temporary directory, probe them with
`SourceLoader`, and call `ImageRenderer.Execute` with request records built by the test; nothing is mocked, because the
unit is the chain of the real loader, pipeline, and writer. The pixels of a written file are compared with the
expected surface (`SurfaceAssert`), and the failure cases assert the error code and that the input is unchanged and no
file or temporary file is left.

#### Test Environment

N/A - standard test environment; the tests write only to temporary directories that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A single image is written as a copy at its own size and reported page by page; the operations are applied in the order
  given and reported after those of the loader.
- Every page of a document is written to a directory under a name made of the input and the page number.
- An output that is the input, an unknown extension, an existing output with the no-clobber policy, and a page that does
  not
  exist are refused with their codes before anything is written.
- A failing operation leaves no output and no temporary file.

#### Test Scenarios

**ImageRenderer_Execute_SingleImage_WritesCopyAndReportsPage**: a gradient PNG rendered without operations gives one page
numbered 1 whose file has the size and exactly the pixels of the input, and the result carries the path of the input.

**ImageRenderer_Execute_Operations_AreAppliedInOrderAndReported**: a request with a crop to 8 by 4 and a resize to 4 by
2
gives a file of 4 by 2, and the last two operations of the page are `crop` and `resize`, in that order.

**ImageRenderer_Execute_AllPagesIntoDirectory_NamesEachPageAfterTheInput**: the PDF rendered with every page into a
folder that does not exist yet writes `source-1.png` and `source-2.png`, and reports the pages 1 and 2.

**ImageRenderer_Execute_OutputIsInput_ThrowsSameInputOutput**: an output that is the input is `same-input-output` and the
input keeps its bytes.

**ImageRenderer_Execute_UnknownExtension_ThrowsUsageError**: an output extension that no format claims is `usage-error`
and nothing is written.

**ImageRenderer_Execute_NoClobberExistingOutput_ThrowsOutputExists**: an existing output with no-clobber is
`output-exists` and keeps its content.

**ImageRenderer_Execute_PageOutOfRange_ThrowsPageOutOfRange**: a page beyond the last is `page-out-of-range`.

**ImageRenderer_Execute_OperationFails_LeavesNothingBehind**: a crop that starts outside the image is `usage-error`, an
existing output keeps its old content, and the directory holds nothing but the input and that output (no temporary
file).
