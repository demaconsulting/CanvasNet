## Validation

### Verification Approach

`Validation` is verified with unit tests in `ValidationTests.cs`. The self-tests run the tool in-process, so
no mocking is needed: the tests create a `Context` with controlled arguments, call `Validation.Run`, and
inspect the captured output, the exit code, and the results files written to temporary paths. A self-validation run
executes every self-test, so the tests that only read the outcome of a not-silent run with a `.trx` results path
(the summary, each self-test's pass and record, the order of the self-tests, and the TRX file) share one such run
through `ValidationRunFixture`, which starts it the first time a test reads it and deletes its results file at the
end. The tests of the other options (silent, depth, the `.xml` and unsupported results paths, the null context) run
with their own context.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

### Acceptance Criteria

- All unit tests pass with zero failures.
- The summary lists every self-test as passed with zero failures, in the documented order, and the exit code is 0.
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
reported as passed, are named in the TRX file, and the run reports nineteen tests in all.

**Validation_Run_VegaLiteSelfTest_PassesAndIsRecorded**: the chart self-test (`CanvasNetTool_RenderVegaLite`) passes
(exit code 0), is reported as passed, is named in the TRX file, and the run reports nineteen tests, nineteen passed,
and none failed.

**Validation_Run_MermaidSelfTest_PassesAndIsRecorded**: the diagram self-test (`CanvasNetTool_RenderMermaid`) passes
(exit code 0), is reported as passed, is named in the TRX file, and the run reports no failed test. The self-test itself
describes a three-node flowchart with `info --json` (the `mermaid` format, the type `flowchart`, `supported`, three
nodes, and two edges) and renders it, requiring the size `info` reported, a white corner, and the default node fill
`#ECECFF` inside the first node.

**Validation_Run_PlantUmlSelfTest_PassesAndIsRecorded**: the PlantUML self-test (`CanvasNetTool_RenderPlantUml`) passes
(exit code 0), is reported as passed, is named in the TRX file, and the run reports no failed test. The self-test
itself describes a two-participant sequence diagram with `info --json` (the `plantuml` format, the type `sequence`,
`supported`, two nodes, and two edges) and renders it with `--strict`, requiring the size `info` reported, a white
corner, and more than 200 pixels of the participant fill `#E2E2F0`.

**Validation_Run_NineteenSelfTests_RunInTheDocumentedOrder**: the passed lines of a run name the nineteen self-tests in
the documented order (`VersionDisplay`, `HelpDisplay`, `Formats`, `RenderPng`, `ConvertFormats`, `Info`, `RenderSvg`,
`PdfMinimal`, `RenderScad`, `RenderVegaLite`, `RenderMermaid`, `RenderPlantUml`, `PandocFilter`, `CropResize`,
`RotateFlipPad`, `Compare`, `Montage`, `Adjustments`, and `HelpExamples`, each with the prefix `CanvasNetTool_`): the
Mermaid, PlantUML, and Pandoc filter self-tests run right after the Vega-Lite one and the help self-test is last; the
exit code is 0. The Pandoc filter
self-test (`CanvasNetTool_PandocFilter`) runs the `pandoc-filter` command on a document with a captioned Mermaid block
and requires a figure whose picture is the one PNG in `generated/canvasnet`, wider than it is tall.

**Validation_Run_AdjustmentsSelfTest_PassesAndIsRecorded**: the photo self-test (`CanvasNetTool_Adjustments`) passes
(exit code 0), is reported as passed, is named in the TRX file, and the run reports no failed test.

**Validation_Run_HelpExamplesSelfTest_PassesAndIsRecorded**: the help self-test (`CanvasNetTool_HelpExamples`) passes
(exit code 0), is reported as passed, is named in the TRX file, and the run reports no failed test. The self-test itself
renders every source example of the help cards with `--strict` (no diagnostic), checks that `help scad --example plate`
prints exactly the embedded source, and checks that `help topics --json` lists every help card.

**Validation_Run_NullContext_ThrowsArgumentNullException**: a null context throws.

**Validation_Run_WithDepth_UsesHeadingDepth**: with `--depth 3` the report heading has three hashes.

**Validation_Run_WithTrxResultsFile_WritesTrxFile**: a `.trx` results path produces a TRX document that names
the contractual self-tests.

**Validation_Run_WithXmlResultsFile_WritesXmlFile**: an `.xml` results path produces a JUnit document.

**Validation_Run_WithUnsupportedResultsFormat_DoesNotWriteFile**: a `.json` results path sets exit code 1 and
writes nothing.
