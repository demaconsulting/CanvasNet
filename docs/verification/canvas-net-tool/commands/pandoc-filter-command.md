### PandocFilterCommand

#### Verification Approach

`PandocFilterCommand` is verified with unit tests in `PandocFilterCommandTests.cs`. The command is run in-process
through the tool's entry point with the `ToolHost` helper, which captures standard output and standard error; the
document is read from and written to files with `--input` and `--output`, so no standard input is redirected, and the
pictures are drawn by the real `ToolPictureDrawer` into a temporary folder. The filter itself is verified with the
units of the `PandocFilter` subsystem; these tests verify the options, the shim writing, the protocol check, the bytes
in and out, and the errors. The real standard input and output are verified by the system tests (see _CanvasNetTool
Verification_).

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is registered under `pandoc-filter` and its help lists every option and points to `canvasnet help
  pandoc`.
- `--write-shim` writes the embedded shim and reports it as text and as JSON.
- A protocol the tool does not speak is a usage error with the documented message and hint, before the input is read.
- A failed run writes no output, reports the filter's code, and writes the earlier warnings to standard error.

#### Test Scenarios

**PandocFilterCommand_Run_WriteShim_WritesTheShimAsTextOrJson**: `--write-shim` into a new folder writes the bytes of the
embedded shim and the line `wrote <file> (protocol 1)`; with `--json` the document has `ok` true, the command
`pandoc-filter`, `protocol` 1, and the output path.

**PandocFilterCommand_Run_ProtocolMismatch_IsUsageErrorNamingTheFix**: `--protocol 0` with an input that does not exist
fails with `usage-error`, the message `docs/filters/canvasnet.lua speaks protocol 0; this tool needs protocol 1`, and the
hint `Write the shim again: dotnet canvasnet pandoc-filter --write-shim docs/filters/canvasnet.lua` (the input is never
read); `--protocol 2` says `this tool speaks protocol 1 at most`.

**PandocFilterCommand_Run_BadOptions_AreUsageErrors**: a theory over a missing `--protocol`, a protocol that is not a
whole number, a missing `--filter-path`, `--jobs 0`, and a positional argument; each exits with code 1 and its message
on standard error.

**PandocFilterCommand_Run_InputAndOutputFiles_TransformTheDocument**: a document with non-ASCII text (`Grüße`) and a
Mermaid code block is transformed into the output file with nothing on standard output or standard error; the text is
unchanged and the one picture was drawn into `generated/canvasnet`.

**PandocFilterCommand_Run_MissingSource_IsTheFilterErrorAndWritesNothing**: a model that is not in the resource path
fails with the JSON failure document `pandoc-filter-error` and the message `cannot find 'part.scad' in the document's
resource path`; a document that is not JSON fails with `corrupt-input`; no output file is written.

**PandocFilterCommand_Run_FailureAfterAWarning_WritesTheWarningToStandardError**: a model that echoes a line, followed
by a model that does not exist, fails with exit code 1; standard error has the line
`[WARNING] canvasnet.lua: echo.scad: ...` with the echoed text before `Error: pandoc-filter-error: cannot find
'part.scad'`.

**PandocFilterCommand_Help_ListsOptionsAndTheCard**: `help pandoc-filter` exits with code 0, names every option of the
table, and points to `canvasnet help pandoc`.
