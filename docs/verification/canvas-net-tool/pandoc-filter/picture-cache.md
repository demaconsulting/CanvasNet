### PictureCache

#### Verification Approach

`PictureCache` is verified with xUnit unit tests in `PictureCacheTests.cs`
(`test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/`). The tests call the internal static members directly.
The key is checked against golden values computed by the Lua filter's own composition in Pandoc 3.12.1 (`pandoc.sha1`
of the fields and parts joined with `\n\0\n`, with `canvasnet.lua 17`), so they prove the C# key is the Lua key; the
test's remarks also record the version 16 keys of the same inputs and that a run of the version 16 Lua filter and of
the `pandoc-filter` command built with the key version set to 16 wrote identical cache names. Nothing is stubbed: the
atomic writes and the commit run against real files in a temporary directory created with the `TempDirectory` helper
of the test project.

#### Test Environment

N/A - standard test environment.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The key version is 17 and the keys of fixed inputs equal the golden values of the Lua composition.
- The stems and the names of pictures, copies, markers, code block sources, and partial files follow the Lua filter.
- An atomic write leaves exactly the finished file, an existing file is kept, a failed write leaves no temporary file,
  a lost race keeps the other copy, and a failed commit with nothing there is a `pandoc-filter-error`.

#### Test Scenarios

**PictureCache_ComputeKey_FixedInputs_GiveTheGoldenKeys**: `KeyVersion` is `17`, and four keys over the identity
`1.0.0\ndefault\n` equal their golden SHA-1 values: a Mermaid picture with one part, a flattened `svg` drawing with a
folder, a `--file-access roots` key, and two parts, an OpenSCAD model with three sorted options and two parts, and a
PDF page with no parts.

**PictureCache_Names_FollowTheLuaFilter**: the stem of `My Chart.vl.json` is `My-Chart`, of `a.b.c.pdf` is `a-b-c`, and
of `café.scad` is `caf--` (one dash per UTF-8 byte); in a cache folder the picture is `flow-k.png`, the flattened copy
`d-k.svg`, the markers `k.unsupported` and `k.plain`, a partial file matches `k.<9 digits>.partial.png`, and the source
of a journey code block is `cec4b774cf6e92d591d5001e3909f9dc6ca2cdcc.mmd`, the name the old filter wrote for the same
block (also when the kind `mermaid` is named), while a PlantUML block is
`eccade7d2733145c0211b90c5308527baf6876f0.puml`, the SHA-1 of the prefix that names `plantuml` and its text.

**PictureCache_BlockSourceFile_MathMlBlock_HasMmlExtension**: the source of a MathML block in a cache folder is the
SHA-1 of `canvasnet.lua mathml block\n` and its text with `.mml`, and differs from the name of a Mermaid block of the
same text.

**PictureCache_WriteTextAtomically_NewAndExisting_WritesOnce**: a new file holds the UTF-8 bytes of `é\n` without a byte
order mark, an existing file keeps its text `kept` instead of `replaced`, and the folder holds only the two files, with
no partial file left.

**PictureCache_WriteTextAtomically_WriteFailsAfterCreate_RemovesThePartialFile**: a writer that creates the temporary
file and then throws `IOException` (as a full disk would) makes the write fail with `pandoc-filter-error`, the message
`could not write <partial>`, and the `IOException` as the inner exception, and the folder is left empty: neither the
temporary file nor the target is there.

**PictureCache_Commit_LostRaceKeepsTheOtherCopy_NothingThereThrows**: when the target was written first by another
build, the commit keeps its content and removes the partial file; when the partial file does not exist and neither does
the target, the commit throws `ToolException` with the code `pandoc-filter-error` and the given message
`could not write x`.
