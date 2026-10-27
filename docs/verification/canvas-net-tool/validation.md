## Validation

### Verification Approach

`Validation` is verified with unit tests in `ValidationTests.cs`. The self-tests run the tool in-process, so
no mocking is needed: the tests create a `Context` with controlled arguments, call `Validation.Run`, and
inspect the captured output, the exit code, and the results files written to temporary paths.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

### Acceptance Criteria

- All unit tests pass with zero failures.
- The summary lists every self-test as passed with zero failures, and the exit code is 0.
- TRX and JUnit files are written for `.trx` and `.xml` paths and name the contractual self-tests.
- An unsupported extension sets exit code 1 and writes no file.

### Test Scenarios

**Validation_Run_NotSilent_PrintsSummary**: the output contains the header, a passed line for
`CanvasNetTool_VersionDisplay`, `CanvasNetTool_HelpDisplay`, and `CanvasNetTool_Formats`, the "Total Tests:" and
"Passed:" lines, and "Failed: 0".

**Validation_Run_FormatsSelfTest_PassesAndIsRecorded**: the formats self-test passes (exit code 0) and its name is
in the TRX file.

**Validation_Run_ImagingSelfTests_PassAndAreRecorded**: the eight imaging self-tests (`CanvasNetTool_RenderPng`,
`CanvasNetTool_ConvertFormats`, `CanvasNetTool_Info`, `CanvasNetTool_RenderSvg`, `CanvasNetTool_PdfMinimal`,
`CanvasNetTool_RenderScad`, `CanvasNetTool_CropResize`, and `CanvasNetTool_RotateFlipPad`) pass (exit code 0), are
reported as passed, and are named in the TRX file.

**Validation_Run_WithSilentContext_ExitCodeIsZero**: a silent run leaves exit code 0.

**Validation_Run_CompareMontageSelfTests_PassAndAreRecorded**: the two comparison self-tests
(`CanvasNetTool_Compare` and `CanvasNetTool_Montage`) pass (exit code 0), are
reported as passed, are named in the TRX file, and the run reports fourteen tests in all.

**Validation_Run_AdjustmentsSelfTest_PassesAndIsRecorded**: the photo self-test (`CanvasNetTool_Adjustments`) passes
(exit code 0), is reported as passed, is named in the TRX file, and the run reports no failed test.

**Validation_Run_NullContext_ThrowsArgumentNullException**: a null context throws.

**Validation_Run_WithDepth_UsesHeadingDepth**: with `--depth 3` the report heading has three hashes.

**Validation_Run_WithTrxResultsFile_WritesTrxFile**: a `.trx` results path produces a TRX document that names
the contractual self-tests.

**Validation_Run_WithXmlResultsFile_WritesXmlFile**: an `.xml` results path produces a JUnit document.

**Validation_Run_WithUnsupportedResultsFormat_DoesNotWriteFile**: a `.json` results path sets exit code 1 and
writes nothing.
