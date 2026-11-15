### FilterSettings

#### Verification Approach

`FilterSettings` is verified with xUnit unit tests in `FilterSettingsTests.cs`. The tests build the settings with the
`FilterTree` helper of `FilterTestSupport.cs`: a temporary working directory with `docs/guide` for the sources, the
shim named as `docs/filters/canvasnet.lua` (not written), the format `html5`, the output file `out/doc.html`, and the
tool version `1.0.0`. The environment variables are a dictionary the test fills, read through the `Environment`
delegate, so no process environment is changed. The file system is real: the tests use temporary folders for sources
outside the working directory and for a local build with DLL files. Nothing else is stubbed and no tool is run.
Refusals are asserted as the `ToolException` thrown, with its code and its exact message.

#### Test Environment

N/A - standard test environment. The tests create and delete temporary folders; the expected `CANVASNET_ROOTS`
separator and the handling of an empty entry depend on the platform and are chosen by the test.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The file-access arguments, folders, restrictiveness, and key text follow the variables as documented, and the policy
  of the dependency walk is none, the tool's own, or the one that denies every file, as documented.
- Every refused variable fails with `pandoc-filter-error` and the exact message of the Lua filter.
- The tool identity, the cache folder, the `src` of a picture, and the number of jobs are as documented.

#### Test Scenarios

**FilterSettings_FileAccessOf_NoVariables_IsTheDefaultMode**: without the variables a picture has no arguments, no
folders, is not restrictive, and has the key text `file-access` and a line break.

**FilterSettings_FileAccessOf_RootsWithoutFolders_AddsTheDefaultFolders**: the mode `Roots , No-Symlinks` with white
space around it is read as `roots,no-symlinks`; a source in the working directory adds only the working directory as
`--allow-root` and lists it as the folder, the result is restrictive, and a source in a folder outside it adds that
folder too, with the key text of all arguments one per line.

**FilterSettings_FileAccessOf_FoldersWithoutMode_ImplyRoots**: `CANVASNET_ROOTS` with a relative folder, an empty
entry, and the absolute working directory gives `--file-access roots` and one `--allow-root` per absolute folder (the
empty entry dropped on Windows and the working directory elsewhere), lists them as the folders, and is restrictive.

**FilterSettings_FileAccessOf_BadVariables_ThrowTheLuaMessage**: an unknown mode (`everything`), and `CANVASNET_ROOTS`
with the mode `ANY` (read as `any`) or `no-symlinks`, fail with `pandoc-filter-error` and the exact message.

**FilterSettings_PolicyOf_BuildsTheToolsPolicyFromTheArguments**: the default mode gives no policy; `roots,no-symlinks`
gives the tool's policy named `roots+no-symlinks`; `no-symlinks` gives `FileAccessPolicy.NoSymlinks`; and `roots` with a
`CANVASNET_ROOTS` folder that does not exist (which the tool refuses) gives `DependencyHasher.DenyAll`.

**FilterSettings_ToolIdentity_DefaultAndLocalBuild_NameTheTool**: without `CANVASNET_TOOL` the identity is `1.0.0`,
`default`, and an empty fingerprint; with `dotnet "<build>/Tool.dll"` it is `1.0.0`, `CANVASNET_TOOL=<command>`, and
the SHA-1 of `Other.DLL` and `Tool.dll` sorted by name and joined with `;`, ignoring `readme.txt`.

**FilterSettings_SplitCommand_QuotesGroupWithoutEscapes**: a quoted path with a space is one argument and `a""b` is
`ab`; an empty quoted part and an unclosed quote fail with the exact messages.

**FilterSettings_CacheFolderAndPictureSrc_FollowTheWriter**: the cache folder is `docs/generated/canvasnet` under the
working directory; the `src` of a picture there is `../docs/generated/canvasnet/flow-k.png` for `html5` and `revealjs`
with the output file `out/doc.html`, and the absolute path with forward slashes for `latex`, for `html5` with embedded
resources, and for `html5` without an output file.

**FilterSettings_ResolveJobs_OptionVariableDefault**: the option 3 wins over the variable 8; the variable `8` with
white space around it gives 8; without both the result is the number of processors up to 4; the variable `0` fails
with `CANVASNET_JOBS is '0'; write a whole number from 1 up to 64`.
