### FilterPaths

#### Verification Approach

`FilterPaths` is verified with xUnit unit tests in `FilterPathsTests.cs`
(`test/DemaConsulting.CanvasNet.Tool.Tests/PandocFilter/`). The tests call the internal static members directly with
fixed paths, URIs, and URLs, and compare the results with the values the Lua filter and `pandoc.path` give on the
platform; expected paths are built with `FilterPaths.Join` and `FilterPaths.Separator` so the same tests hold on
Windows and elsewhere. Nothing is stubbed: `LocateSource` runs against real files written to a temporary directory
created with the `TempDirectory` helper of the test project.

#### Test Environment

N/A - standard test environment. `FilterPaths_Collapse_WindowsDrives_FollowSystemFilePath` checks the Windows drive
rules and is skipped on other platforms; the search path and letter case assertions select the expected values of the
platform the tests run on.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Collapsed, relative, and pandoc.path results match the Lua filter on the platform.
- The `file:` URI and remote URL rules match the tool's own rules.
- A local data file, a containment check, and a located source give the documented result, and blank, remote, or
  missing inputs give none.

#### Test Scenarios

**FilterPaths_Collapse_DotSegments_AreResolvedLikeTheLuaFilter**: six paths are collapsed with the platform's
separators: `/a/b/../c` to `/a/c`, `/a/./b//c/` to `/a/b/c`, `/../../a` to `/a` (staying at the root), `../a/./b` to
`../a/b` and `a/../../b` to `../b` (keeping a leading `..`), and `a/b/..` to `a`.

**FilterPaths_Collapse_WindowsDrives_FollowSystemFilePath**: on Windows, `c:/x/../../y` collapses to `C:\y`, `C:\x` is
absolute while `\x` and `C:x` are not, a path on another drive has no relative path, and `..` stops at the share of
`\\server\share\..\..\x`, giving `\\server\x`.

**FilterPaths_RelativePath_SharedRoot_ClimbsWithForwardSlashes**: the path from `<temp>/a/b` to `<temp>/a/c/d.png` is
`../c/d.png`, and the label of the target from `<temp>/a` is `c/d.png`.

**FilterPaths_FileUriPath_HostlessUris_ArePaths**: `file:///C:/x%20y.csv` is `C:/x y.csv`, `FILE:/data/a.csv` is
`/data/a.csv`, and `file:///a` is `/a`; `file://host/a`, `file:///%2F%2Fserver/a`, and `files:///a` give no path.

**FilterPaths_IsRemoteUrl_FollowsTheToolsRule**: an `https:` URL, a `data:` URL after leading spaces, `//server/a.csv`,
`\\server\a.csv`, and `file://host/a.csv` are remote; `file:///a.csv`, `C:/a.csv`, and `data/a.csv` are not.

**FilterPaths_LocalDataFile_RelativeAbsoluteAndRemote_ResolveLikeTheTool**: relative to a folder, `data\a.csv` is
`data/a.csv`, `a%20b.csv` keeps its percent sign, a data URL keeps `#x` while a picture link loses `#page=2`, a
`file:///` URI of the folder is its path, and a blank URL and an `https:` URL give no file.

**FilterPaths_IsSameOrUnder_ComparesWholeSegments**: a folder is the same as itself with a trailing separator, a file
two levels below it is under it, a folder whose name only starts with it (`docs2`) is not, and a path under the
upper-cased folder name is under it on Windows only.

**FilterPaths_LocateSource_ResourcePath_FindsAsWrittenOrDecoded**: with the resource folders `a` and `b`, the name
`my%20flow.mmd` finds `b/my flow.mmd` through its decoded form, and `none.mmd` finds nothing.

**FilterPaths_PathFunctions_FollowPandocPath**: the folder of `name.svg` is `.` and of `a/b.svg` is `a`, the file name of
`a/b.svg` is `b.svg`, the stem of `chart.vl.json` is `chart.vl`, `%C3%A9` decodes to `é`, and `100%` stays; the search
path `a;;"b c"` gives `a` and `b c` on Windows, and `a::"b c"` gives `a`, `.`, and `"b c"` elsewhere.
