### FlattenCommand

#### Verification Approach

`FlattenCommand` is verified with unit tests in `FlattenCommandTests.cs`. The command is run in-process: the tests
create a `Context` (with `--json` when the JSON document is wanted), call `Run`, and capture the console with the
`ToolHost` helper; failures are asserted with `ToolHost.ExpectFailure` as the exception the command lets escape (a
`ViewerException` of the library or the tool's own `ToolException`). The drawings are written to a temporary directory
next to a copy of the Mermaid diagram of the fixtures. The behavior of the operation itself is verified with the
`SvgFlattener` unit; these tests verify the options, the text and JSON results, and the errors.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is registered under `flatten` with the options `--output`, `--scale`, `--strict`, and `--no-clobber`.
- The text line and the JSON document have the documented content and key order; nothing to flatten writes nothing.
- Every refused command line, input, and output has its documented code.

#### Test Scenarios

**FlattenCommand_Help_ShowsUsageAndOptions**: the name, the usage with its examples, and the four options in order, with
`-o` as the alias of `--output`.

**FlattenCommand_Run_CompositeSvg_WritesCopyAndPrintsSummary** and **FlattenCommand_Run_Scale_DoublesReplacedSize**: the
line names the output, its size, and `flow.mmd 60x40 mermaid` (`120x80` with `--scale 2`), and the copy holds a PNG data
URI.

**FlattenCommand_Run_Json_HasDocumentedShapeAndKeyOrder**: the keys are `schemaVersion`, `ok`, `command`, `tool`,
`input`, `scale`, `flattened`, `replaced`, `rebased`, `outputs`, `diagnostics`, and `echo`, in that order, and a replaced
entry has `href`, `format`, `width`, `height`, `elements`, and `bytes`.

**FlattenCommand_Run_PlainSvg_PrintsNothingToFlattenAndWritesNothing** and
**FlattenCommand_Run_Json_PlainSvg_FlattenedFalse**: a drawing without such links prints the nothing-to-flatten line,
or `flattened` false with empty `replaced` and `outputs`, and writes no file.

**FlattenCommand_Run_HiddenDiagram_WarnsOnStandardError**: a diagram in an unused definition is written to standard error
as `Warning: svg-flatten-image-not-drawn: ...`.

**FlattenCommand_Run_NoOutput_UsageError**, **FlattenCommand_Run_TwoInputs_UsageError**,
**FlattenCommand_Run_NotSvg_UnsupportedFormat**, **FlattenCommand_Run_NoClobber_OutputExists**, and
**FlattenCommand_Run_Strict_UnresolvedLink_FailsUnsupportedFeature**: each refused command line has its code, the hint of
a missing output names `-o`, and an existing output is kept with `--no-clobber`.
