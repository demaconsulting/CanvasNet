### DiscoveryCommands

<!-- cspell:ignore canvasnet jpeg -->

#### Verification Approach

`DiscoveryCommands` is verified with unit tests in `DiscoveryCommandsTests.cs`. The `formats` command is run
in-process with fixed versions (through its `internal` constructor) so that the output does not depend on the
build version; the JSON output is compared byte for byte with a golden document. The tests create a `Context`
with controlled arguments and capture the console with the `ToolHost` helper.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The text listing shows the input and output formats, the limits, and the versions.
- The JSON document equals the golden document exactly.
- A stray argument or option is a usage error.

#### Test Scenarios

**DiscoveryCommands_Formats_Metadata_DescribesCommand**: the command is named `formats`, has no aliases and no
options, and has a usage line with examples.

**DiscoveryCommands_Formats_TextOutput_ListsFormatsLimitsAndVersions**: the text has both sections, representative
rows (PNG as a single-page raster, PDF as a multi-page scalable document, JPEG as an output with quality), the
limits, and both versions, and contains no JSON.

**DiscoveryCommands_Formats_JsonOutput_MatchesGoldenDocument**: with `--json` the output is exactly the golden
document followed by a line ending.

**DiscoveryCommands_Formats_DefaultVersions_ReportsRunningVersions**: the default constructor reports
`Program.Version` and a non-empty library version.

**DiscoveryCommands_FormatTables_AreWellFormed**: format names are unique; extensions are lower case, start with a
dot, and are unique within a table; every input kind is one of the four documented kinds.

**DiscoveryCommands_Formats_PositionalArgument_ThrowsUsageError** and
**DiscoveryCommands_Formats_UnknownOption_ThrowsUsageError**: a stray argument or an unknown option is a
`usage-error` naming the command.

**DiscoveryCommands_Formats_NullArguments_ThrowsArgumentNullException**: null arguments are rejected.
