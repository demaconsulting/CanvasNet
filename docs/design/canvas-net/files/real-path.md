### RealPath

![Files Structure](FilesView.svg)

The `RealPath` unit is the only place in CanvasNet that asks the operating system directly about files. It comprises
five internal source files in the `DemaConsulting.CanvasNet.Files` namespace:

- `RealPath.cs` - `RealPath` (internal static partial class): the dispatch per operating system, the open, the handle
  facts (`HandleFacts`), the real location of a folder, short-name expansion, the on-disk letter case of a path and
  its confirmation against an open handle, and the path comparison rules
- `RealPath.Windows.cs` - the Windows facts and identity of an open handle, and whether a path is a network location
- `RealPath.Unix.cs` - the Linux facts of an open handle, and the Linux and macOS identity of a file
- `NativeMethods.cs` - `NativeMethods` (internal static class): the private `DllImport` declarations and the wrappers
  that add buffer growth, prefix stripping, error translation, and structure offsets
- `FileOpenHooks.cs` - `FileOpenHooks` (internal sealed class): the test seams around the open (`BeforeOpen`, `Open`,
  `AfterOpen`, `RealPathOf`, `IsRegularFileOf`, `DirectoryLocationOf`, `ExactPathOf`)

#### Purpose

A file-access decision is free of races only when it is made about the file that is read, so the real path must come
from the open handle rather than from the name. .NET 8, 9 and 10 have no API for this (`FileSystemInfo.LinkTarget` and
`ResolveLinkTarget` work by name, and the path of a `SafeFileHandle` is internal), so this unit asks the operating
system. It also opens files without blocking on Linux and macOS and tells a regular file from a directory, a device or
a named pipe, which .NET cannot do for an open handle either. Because real paths are compared exactly (a folder may be
case-sensitive on every platform), it gives a path in the letter case stored on disk and confirms that spelling against
the open handle by file identity.

The unit's behavior is specified by the `CanvasNet-Files-RealPath-*` requirements (`FinalPath`, `Unavailable`,
`RegularFile`, `NonBlockingOpen`, `CanonicalDirectory`, `NetworkLocation`, `Comparison`, `ExactCase`, and `Identity`) in
`docs/reqstream/canvas-net/files/real-path.yaml`.

#### Data Model

**`HandleFacts`** (readonly record struct): `IsRegularFile` (`bool?`) and `FinalPath` (`string?`); `null` means the
platform cannot tell. **`PathComparison`** / **`PathComparer`**: `OrdinalIgnoreCase` on Windows and macOS, `Ordinal`
elsewhere; used only for paths as written (the lexical pre-check of a policy, the lexical confinement of the sandbox,
Windows drive roots) and for cache keys, never for a decision about a real path. Real paths are always compared with
`StringComparison.Ordinal` (on macOS after both sides are brought into Unicode composed form), because Windows
per-directory case sensitivity and case-sensitive APFS make two names that differ only in letter case two different
folders. The unit is stateless except for one cached flag in `NativeMethods` that remembers
whether the C library exports `fstat`.

#### Key Methods

- `OpenForRead` - Windows: `File.OpenHandle` read-only with read, write and delete sharing. Linux and macOS:
  `open` with `O_RDONLY | O_NONBLOCK | O_NOCTTY | O_CLOEXEC` (the values of each system).
- `Inspect` (type) - Windows: `GetFileType` is `FILE_TYPE_DISK` and the handle is not a directory. Linux: `fstat`, with
  `S_IFMT` equal to `S_IFREG`. macOS: `vst_mode` from `proc_pidfdinfo`.
- `Inspect` (path) - Windows: `GetFinalPathNameByHandleW` (normalized name, DOS volume). Linux: the link of
  `/proc/self/fd/<fd>`. macOS: `vip_path` from `proc_pidfdinfo`.
- `TryGetCanonicalDirectory` - Windows: `CreateFileW` with backup semantics and no access rights, then the final path
  of that handle. Linux: `open` with `O_PATH | O_CLOEXEC` (no read permission needed), then the `/proc` link. macOS:
  `realpath(3)` with a caller-owned buffer of `PATH_MAX` (1024) bytes; it resolves by name and needs only search
  permission on the components, never read permission, so a folder that may be searched but not read (execute-only)
  has a real location too. `NULL` (failure) is no location.
- `IsNetworkLocation` - Windows only (`false` elsewhere): a path that starts with `\\` and is not in a device
  namespace (`\\.\`, `\\?\`) is a UNC path, and so is `\\?\UNC\`; an ordinary extended-length drive path (`\\?\Z:\`)
  is classified by its drive; a path on a drive whose `DriveInfo.DriveType` is `Network` (a mapped
  network drive) is a network location too, so a host working on `Z:\` keeps working; a drive that cannot be asked is
  treated as local. A mapped drive cannot be created on CI, so that branch is covered by review only.
- `IsWindowsDeviceNamespace` - pure text: the path starts with `\\.\`, `\\?\` or `\??\` (either slash).
- `IsOrdinaryExtendedPath` - pure text: `\\?\` followed by a drive (`\\?\C:` or `\\?\C:\...`) or a volume GUID
  (`\\?\Volume{GUID}` or `\\?\Volume{GUID}\...`, the GUID in the 36-character form); the one rule both resolvers use to
  exempt an extended-length directory, and that `UnderRoots` uses to reject any other device-namespace root.
- `ExpandShortNames` - Windows: `GetLongPathNameW`; elsewhere the path is returned unchanged.
- `GetExactPathName` - gives a full path in the letter case stored on disk, component by component, and never throws.
  The path is made full (`Path.GetFullPath`; an invalid path is returned as given); the root is normalized by
  `ExactRoot`; each component is looked up in its parent folder as corrected so far, whose entries are listed with a
  `FileSystemEnumerable` (options: `MatchCasing.CaseInsensitive`, `MatchType.Simple`, `IgnoreInaccessible`, no
  attribute skipped, no recursion) without a search pattern, keeping only the entries whose name equals the component
  under `OrdinalIgnoreCase` (on macOS both in Unicode composed form). An entry spelled exactly as written wins (the
  component is kept); otherwise the single matching entry gives the stored name; with no match or several matches
  (`Docs` and `docs` in a case-sensitive folder) the component is kept as written. A missing parent folder
  (`DirectoryNotFoundException`) keeps the rest of the path as written; a folder that cannot be listed
  (`UnauthorizedAccessException`, `IOException`, `SecurityException`, `ArgumentException`, `NotSupportedException`)
  keeps that component as written and goes on with the next. Because entries are compared as plain names, `*` and `?`
  in a name (legal on Linux and macOS) match only themselves. A trailing separator of the input is kept. The real
  paths a deciding comparison sees never carry the `\\?\` prefix (it is stripped from every final path); a written
  path that does (only used as the NoSymlinks expected spelling) keeps its root (`\\?\C:\`, `\\?\Volume{GUID}\`) and
  has the components below it corrected.
- `ExactRoot` - pure text: a drive letter (`c:\`, `\\?\c:\`) is upper-cased, as Windows reports it in a final path; a
  UNC root, a volume GUID root and `/` are kept as they are.
- `TryGetExactCanonicalDirectory` - `TryGetCanonicalDirectory`, then `GetExactPathName`: the spelling a trusted folder
  (a root or an anchor) is compared in; a missing folder has no location.
- `TryConfirmExactPath(handle, realPath, exactPath)` - returns `realPath` when the two are equal under `Ordinal` (no
  identity check is needed); otherwise returns `exactPath` only when `IsSameFile(handle, exactPath)` is `true`, and
  `null` (unconfirmed) when it is `false` or unknown.
- `IsSameFile(handle, path)` - opens `path` without following a link at its last component and compares its identity
  with that of the open handle, and requires that `path` reaches the file through no linked folder: otherwise a spelling
  inside an allowed folder could reach a file that lies elsewhere through a link another process put in a folder of the
  spelling. Windows and Linux compare the final path of the file opened by `path` (`GetFinalPathNameByHandleW`, the
  `/proc/self/fd` link) with `path` exactly; macOS opens `path` with `O_NOFOLLOW_ANY`. Never throws (a path that cannot
  be opened or an identity that cannot be read is `null`, unknown).
- `IsUnder(path, root)` and `PathsEqual` - the deciding comparisons of real paths, under `Ordinal` on every platform
  (macOS: both sides in composed form): `PathsEqual` compares after `Path.TrimEndingDirectorySeparator`; `IsUnder` is
  true when the paths are equal after `Path.TrimEndingDirectorySeparator`, or when the path starts with the boundary,
  which is the root itself when it ends in a separator (`C:\`, `/`) and the root followed by
  `Path.DirectorySeparatorChar` otherwise. `Path.GetRelativePath` is never used for the decision, because it applies
  the platform's case rule.
- `IsUnderAsWritten(path, root)` - the same boundary under `PathComparison`, for the written-path pre-check only.
- `CommonAncestor` - the deepest folder two full paths share: the roots are compared under `PathComparison` (a
  drive letter names the same drive in either case), the components under `Ordinal`, so two
  spellings that differ only in letter case share a shallower folder, which trusts less.

Per system:

- **Windows**: the final path buffer starts at 512 characters and grows to the size the call asks for (at most 32,768);
  `\\?\UNC\` becomes `\\` and the `\\?\` prefix is stripped from a drive path; any other spelling (which the
  `VOLUME_NAME_DOS` flag should never yield) is no path (unknown, fail closed). The result resolves symbolic links,
  junctions, substituted and mapped drives, and short (8.3) names. **Identity**: `CreateFileW` with
  `FILE_READ_ATTRIBUTES` (0x80), read, write and delete sharing, `OPEN_EXISTING`, and `FILE_FLAG_BACKUP_SEMANTICS |
  FILE_FLAG_OPEN_REPARSE_POINT` (0x02000000 | 0x00200000, so a link at the last component is opened itself); then
  `GetFileInformationByHandle` for both handles into a 52-byte `BY_HANDLE_FILE_INFORMATION`, comparing
  `dwVolumeSerialNumber` (offset 28) and the 64-bit file index `nFileIndexHigh` (44) and `nFileIndexLow` (48); the final
  path of the identity handle must equal the spelling exactly (a junction or link in a folder changes it).
- **Linux**: `open` is imported without its optional mode argument, which is only read with `O_CREAT` (never passed),
  so the call is correct on every ABI; `EINTR` is retried. `errno` maps to the framework's exceptions (`ENOENT` to
  `FileNotFoundException`, `ENOTDIR` to `DirectoryNotFoundException`, `EACCES`/`EPERM` to
  `UnauthorizedAccessException`, `ENAMETOOLONG` to `PathTooLongException`, others to `IOException`). The flags are
  `O_NONBLOCK` 0x800, `O_NOCTTY` 0x100, `O_CLOEXEC` 0x80000, `O_PATH` 0x200000 (x64 and Arm64). `fstat` fills a
  256-byte `struct stat` buffer; on x64 `st_nlink` is 64 bits at offset 16 and `st_mode` 32 bits at offset 24, on Arm64
  (the generic layout) `st_mode` is at offset 16 and `st_nlink` 32 bits at offset 20 (other architectures: type and
  link count unknown); where an older glibc does not export `fstat`, `__fxstat` is used with `_STAT_VER` 1 (x64) or 0
  (Arm64). The `/proc` link is read before `fstat`; a file whose link count is zero (deleted while open), whose link
  count is unknown, or whose link does not start with `/` has no path. Deletion is never inferred from the `(deleted)`
  suffix Linux appends, because a live file may carry that text in its name; the link text is kept verbatim.
  **Identity**: `st_dev` (64 bits at offset 0) and `st_ino` (64 bits at offset 8) of `fstat`, the same on x64 and Arm64;
  the path is opened with `O_PATH | O_CLOEXEC | O_NOFOLLOW` (`O_NOFOLLOW` 0x20000 on x64, 0x8000 on Arm64; any other
  architecture has no known value, so the identity is unknown), which opens a link at the last component itself and
  needs no read permission; the `/proc/self/fd` link of that descriptor must equal the spelling exactly (a link in a
  folder changes it).
- **macOS**: `proc_pidfdinfo(pid, fd, PROC_PIDFDVNODEPATHINFO = 2, buffer, 1200)` fills `struct vnode_fdinfowithpath`:
  `proc_fileinfo` (24 bytes) followed by `vnode_info_path` (`vnode_info` of 152 bytes - `vinfo_stat` 136, `vi_type`,
  `vi_pad`, `vi_fsid` - and a 1024-byte path). The mode (16 bits) is at offset 28 and the NUL-terminated UTF-8 path at
  offset 176. The structure has no pointer- or `long`-sized field, so the layout is the same on x64 and Arm64; a call
  that fills fewer than 1200 bytes gives no path, and the type then comes from `fstat` (on Arm64, whose `struct stat`
  has the 16-bit `st_mode` at offset 4), so a device or named pipe is still recognized. The flags are `O_NONBLOCK` 0x4,
  `O_NOCTTY` 0x20000, `O_CLOEXEC` 0x01000000 (`OpenNonBlocking` ignores its `pathOnly` argument on macOS, which has no
  `O_PATH`; folders are canonicalized with `realpath` instead). `realpath` is imported non-variadic as
  `realpath(byte[] path, byte[] resolved)` returning a pointer; the result is decoded as UTF-8 up to the first NUL of
  the caller's buffer, and nothing is freed. Paths are compared in Unicode composed form. **Identity**: `vst_dev`
  (32 bits at offset 24, the first field of `vinfo_stat`) and `vst_ino` (64 bits at offset 32, after `vst_mode` and
  `vst_nlink`) from `proc_pidfdinfo` for both descriptors; the path is opened with `O_NOFOLLOW` (0x100) and
  `O_NOFOLLOW_ANY` (0x20000000, macOS 11 and later) added, so a link at any component fails with `ELOOP` (unknown,
  unconfirmed).
- **Other systems**: `File.OpenHandle` (blocking) and unknown facts; a restrictive policy then fails closed.
- `NativeMethods.WithDescriptor` is the one place that reads the raw descriptor of a handle (for the C calls and the
  `/proc` link), inside `DangerousAddRef`/`DangerousRelease`, so the descriptor cannot be closed and reused meanwhile.

**Imports**: `kernel32.dll` with `DefaultDllImportSearchPaths(System32)` and explicit `CharSet.Unicode`; `libc` (the
runtime maps it to the C library on Linux and to `/usr/lib/libc.dylib`, which re-exports `libSystem`, on macOS) with
`SafeDirectories`. The macOS imports are `open`, `proc_pidfdinfo`, `fstat` (fallback for the type), and `realpath`.
Strings for the C library are passed as NUL-terminated UTF-8 byte arrays; no unsafe code is used.

#### Error Handling

`OpenForRead` throws the framework's file exceptions so callers tell a missing file from an unreadable one. `Inspect`,
`TryGetCanonicalDirectory`, `ExpandShortNames`, `GetExactPathName`, `IsSameFile` and `TryConfirmExactPath` never throw
for a platform failure: a failing or missing native call (`IOException`, `UnauthorizedAccessException`,
`ExternalException`, `DllNotFoundException`, `EntryPointNotFoundException`) yields unknown facts, no location, the name
as written, or an unconfirmed spelling, and the caller decides (fail closed for a restrictive policy). An unknown
identity is never treated as a match.

#### Dependencies

- **.NET base class library** - `System.IO`, `System.Runtime.InteropServices`, `Microsoft.Win32.SafeHandles`.
- **Operating system** - `kernel32.dll` (Windows), the C library and `/proc` (Linux), the C library / `libSystem`
  (macOS). These are platform components present on every supported system, not packages.

#### Callers

`FileReferenceResolver` (open, handle facts, on-disk spelling and its confirmation), `FileAccessPolicy` (comparison,
real folder locations in their on-disk letter case, short names and the on-disk spelling of a written path), and
`SandboxedFileResolver` (open, handle facts and confirmation of its verified open). The tests reach the unit through the
core assembly's `InternalsVisibleTo` grant.
