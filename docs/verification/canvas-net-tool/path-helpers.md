## PathHelpers

### Verification Approach

`PathHelpers` is verified with unit tests in `PathHelpersTests.cs`. Because it depends only on .NET BCL path
operations and performs no file-system I/O, no mocking or test doubles are required; the tests pass controlled
path strings and assert on the returned path or the thrown exception.

### Test Environment

N/A - standard test environment. The Windows-specific absolute-path case runs only on Windows.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Valid relative paths are combined exactly as `Path.Combine` would.
- Every path that resolves outside the base directory, and every rooted path, is rejected.
- Null inputs are rejected with `ArgumentNullException`.

### Test Scenarios

**PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly**, **..._NestedPaths_CombinesCorrectly**,
**..._CurrentDirectoryReference_CombinesCorrectly**, **..._EmptyRelativePath_ReturnsBasePath**, and
**..._DotDotPrefixedName_CombinesCorrectly**: valid relative paths (plain, nested, `./`, empty, and a name that
only begins with two dots) are combined unchanged.

**PathHelpers_SafePathCombine_DotDotThatStaysUnderBase_CombinesCorrectly**: a relative path with a double-dot
segment that stays under the base directory (`a/../b`) is allowed and combined unchanged, demonstrating that the
rule rejects escape, not the mere presence of `..`.

**PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException** and
**..._DoubleDotsInMiddle_ThrowsArgumentException**: paths that climb out of the base directory throw
`ArgumentException` ("Invalid path component").

**PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException**: Unix and (on Windows) Windows absolute
paths are rejected.

**PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException** and
**PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException**: null inputs throw.
