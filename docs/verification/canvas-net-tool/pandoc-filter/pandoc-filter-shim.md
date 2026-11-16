### PandocFilterShim

#### Verification Approach

`PandocFilterShim` is verified at two levels:

- **Unit tests** in `PandocFilterShimTests.cs` (xUnit) call the class directly. They compare the embedded bytes with the
  source file `src/DemaConsulting.CanvasNet.Tool/PandocFilter/canvasnet.lua` and the repository filter
  `docs/filters/canvasnet.lua` (found from the folder that holds `CanvasNet.slnx`, line endings aside), write the shim
  into a temporary directory, and check the protocol errors. Nothing is stubbed.
- **End-to-end script** `docs/filters/test-canvasnet.ps1` verifies the Lua shim inside Pandoc. It builds a small
  document tree in a temporary folder, runs real Pandoc (`dotnet pandoc`) with `docs/filters/canvasnet.lua` and the
  real tool (`dotnet canvasnet`, or the command in `-ToolCommand`/`CANVASNET_TOOL`), and prints PASS or FAIL per check.
  Its groups cover:
  - every handled input is drawn and others are left alone, `param-NAME`, the cache (a picture is drawn once and found
    again; a changed source, include, data file, or attribute draws it again), and Mermaid code blocks and pictures,
    including the diagram types the filter draws itself, a type the tool does not draw left as written with a warning,
    and the order of the defaults-file filters before a later command-line filter;
  - SVG pictures (plain ones left unchanged, composite ones flattened and cached, the scan rules, a link to a missing
    diagram, and WeasyPrint drawing the flattened diagram into a PDF);
  - PNG, JPEG, and remote pictures never start the tool (`CANVASNET_TOOL` names a program that does not exist and the
    build still succeeds), while a picture that needs a tool that cannot start is the clear `cannot run the canvasnet
    tool` error;
  - an outdated shim (a copy with protocol 0) stops the build with `speaks protocol 0` and the `--write-shim` command
    for its own path, and the output of `canvasnet pandoc-filter --write-shim` equals the repository shim;
  - a document with five pictures (two models, a chart, and two Mermaid code blocks, one with a non-ASCII caption)
    starts the tool exactly once per Pandoc run (a wrapper set as `CANVASNET_TOOL` counts the starts), and
    `CANVASNET_JOBS=1` and `CANVASNET_JOBS=4` give the same HTML, warnings, and cache files;
  - diagnostics and failures (an `echo()` of a model becomes a warning prefixed `canvasnet.lua:`, a model that cannot be
    drawn stops the build with the tool's message), attribute and path errors, paths resolved like Pandoc does, the
    opt-in file access of `CANVASNET_FILE_ACCESS` and `CANVASNET_ROOTS`, and the dependency limit of 512 hashed files.

The script is not part of `dotnet test`; the requirements of this unit are linked to the unit tests only.

#### Test Environment

N/A - standard test environment for the unit tests, which need the repository's source tree next to the test assembly.
The end-to-end script needs Pandoc from the repository's tool manifest (`dotnet tool restore`), the tool, and WeasyPrint
(`dotnet weasyprint`) for its PDF check.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The embedded shim is the source file and the repository filter, speaks protocol 1, and names
  DemaConsulting.CanvasNet.Tool 0.2.0-beta.11 or later.
- Writing the shim creates its folder, replaces an existing file, returns the byte count, and leaves no partial file.
- An older protocol names the `--write-shim` command for the shim's path; a newer one says to update the tool.
- The end-to-end script, when run, reports all checks passed.

#### Test Scenarios

**PandocFilterShim_Bytes_AreTheSourceFileAndSpeakTheProtocol**: the embedded bytes equal the source file
`canvasnet.lua` (line endings aside), contain `local PROTOCOL = "1"` and `DemaConsulting.CanvasNet.Tool 0.2.0-beta.11
or later`, and `Protocol` lies between `MinProtocol` and `MaxProtocol`.

**PandocFilterShim_Bytes_AreTheRepositoryFilter**: the embedded bytes equal `docs/filters/canvasnet.lua` (line endings
aside), so the repository's documents are built with the shim the tool writes.

**PandocFilterShim_Write_NewFolderAndExistingFile_WritesTheShim**: writing to `docs/filters/canvasnet.lua` in a folder
that does not exist creates it; writing again over a changed file restores the embedded bytes, returns their count, and
leaves exactly one file in the folder (no partial file).

**PandocFilterShim_CheckProtocol_OutsideTheRange_NamesTheFix**: protocol 1 is accepted; protocol 0 for
`docs/x/canvasnet.lua` is `usage-error` with the message `docs/x/canvasnet.lua speaks protocol 0; this tool needs
protocol 1` and the hint `Write the shim again: dotnet canvasnet pandoc-filter --write-shim docs/x/canvasnet.lua`;
protocol 2 without a path has the message `docs/filters/canvasnet.lua speaks protocol 2; this tool speaks protocol 1 at
most` and a hint that starts with `Update DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json`.
