### RealPath

![Files Structure](FilesView.svg)

The `RealPath` unit is the only place in CanvasNet that asks the operating system directly about files. It comprises
five internal source files in the `DemaConsulting.CanvasNet.Files` namespace:

- `RealPath.cs` - `RealPath` (internal static partial class): the dispatch per operating system, the open, the handle
  facts (`HandleFacts`), the real location of a folder, short-name expansion, and the path comparison rules
- `RealPath.Windows.cs` - the Windows facts of an open handle, and whether a path is a network location
- `RealPath.Unix.cs` - the Linux facts of an open handle
- `NativeMethods.cs` - `NativeMethods` (internal static class): the private `DllImport` declarations and the wrappers
  that add buffer growth, prefix stripping, error translation, and structure offsets
- `FileOpenHooks.cs` - `FileOpenHooks` (internal sealed class): the test seams around the open (`BeforeOpen`, `Open`,
  `AfterOpen`, `RealPathOf`)

#### Purpose

A file-access decision is free of races only when it is made about the file that is read, so the real path must come
from the open handle rather than from the name. .NET 8, 9 and 10 have no API for this (`FileSystemInfo.LinkTarget` and
`ResolveLinkTarget` work by name, and the path of a `SafeFileHandle` is internal), so this unit asks the operating
system. It also opens files without blocking on Linux and macOS and tells a regular file from a directory, a device or
a named pipe, which .NET cannot do for an open handle either.

The unit's behavior is specified by the `CanvasNet-Files-RealPath-*` requirements (`FinalPath`, `Unavailable`,
`RegularFile`, `NonBlockingOpen`, `CanonicalDirectory`, `NetworkLocation`, and `Comparison`) in
`docs/reqstream/canvas-net/files/real-path.yaml`.

#### Data Model

**`HandleFacts`** (readonly record struct): `IsRegularFile` (`bool?`) and `FinalPath` (`string?`); `null` means the
platform cannot tell. **`PathComparison`** / **`PathComparer`**: `OrdinalIgnoreCase` on Windows and macOS, `Ordinal`
elsewhere. On a case-sensitive volume or directory of Windows or macOS this comparison is a known limit (see
_FileAccessPolicy Unit Design_). The unit is stateless except for one cached flag in `NativeMethods` that remembers
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
  namespace (`\\.\`, `\\?\`) is a UNC path; a path on a drive whose `DriveInfo.DriveType` is `Network` (a mapped
  network drive) is a network location too, so a host working on `Z:\` keeps working; a drive that cannot be asked is
  treated as local. A mapped drive cannot be created on CI, so that branch is covered by review only.
- `ExpandShortNames` - Windows: `GetLongPathNameW`; elsewhere the path is returned unchanged.

Per system:

- **Windows**: the final path buffer starts at 512 characters and grows to the size the call asks for (at most
  32,768); the `\\?\` prefix is stripped and `\\?\UNC\` becomes `\\`. The result resolves symbolic links, junctions,
  substituted and mapped drives, and short (8.3) names.
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
- **macOS**: `proc_pidfdinfo(pid, fd, PROC_PIDFDVNODEPATHINFO = 2, buffer, 1200)` fills `struct vnode_fdinfowithpath`:
  `proc_fileinfo` (24 bytes) followed by `vnode_info_path` (`vnode_info` of 152 bytes - `vinfo_stat` 136, `vi_type`,
  `vi_pad`, `vi_fsid` - and a 1024-byte path). The mode (16 bits) is at offset 28 and the NUL-terminated UTF-8 path at
  offset 176. The structure has no pointer- or `long`-sized field, so the layout is the same on x64 and Arm64; a call
  that fills fewer than 1200 bytes gives no path, and the type then comes from `fstat` (on Arm64, whose `struct stat`
  has the 16-bit `st_mode` at offset 4), so a device or named pipe is still recognized. The flags are `O_NONBLOCK` 0x4,
  `O_NOCTTY` 0x20000, `O_CLOEXEC` 0x01000000 (`OpenNonBlocking` ignores its `pathOnly` argument on macOS, which has no
  `O_PATH`; folders are canonicalized with `realpath` instead). `realpath` is imported non-variadic as
  `realpath(byte[] path, byte[] resolved)` returning a pointer; the result is decoded as UTF-8 up to the first NUL of
  the caller's buffer, and nothing is freed. Paths are compared in Unicode composed form.
- **Other systems**: `File.OpenHandle` (blocking) and unknown facts; a restrictive policy then fails closed.
- `NativeMethods.WithDescriptor` is the one place that reads the raw descriptor of a handle (for the C calls and the
  `/proc` link), inside `DangerousAddRef`/`DangerousRelease`, so the descriptor cannot be closed and reused meanwhile.
- `IsUnder(path, root)`, `PathsEqual`, and `CommonAncestor` implement the comparison rules: the separator is the
  boundary, trailing separators do not matter, and components are compared whole under `PathComparison`.

**Imports**: `kernel32.dll` with `DefaultDllImportSearchPaths(System32)` and explicit `CharSet.Unicode`; `libc` (the
runtime maps it to the C library on Linux and to `/usr/lib/libc.dylib`, which re-exports `libSystem`, on macOS) with
`SafeDirectories`. The macOS imports are `open`, `proc_pidfdinfo`, `fstat` (fallback for the type), and `realpath`.
Strings for the C library are passed as NUL-terminated UTF-8 byte arrays; no unsafe code is used.

#### Error Handling

`OpenForRead` throws the framework's file exceptions so callers tell a missing file from an unreadable one. `Inspect`,
`TryGetCanonicalDirectory`, and `ExpandShortNames` never throw for a platform failure: a failing or missing native call
(`IOException`, `UnauthorizedAccessException`, `ExternalException`, `DllNotFoundException`,
`EntryPointNotFoundException`) yields unknown facts or no location, and the caller decides (fail closed for a
restrictive policy).

#### Dependencies

- **.NET base class library** - `System.IO`, `System.Runtime.InteropServices`, `Microsoft.Win32.SafeHandles`.
- **Operating system** - `kernel32.dll` (Windows), the C library and `/proc` (Linux), the C library / `libSystem`
  (macOS). These are platform components present on every supported system, not packages.

#### Callers

`FileReferenceResolver` (open and handle facts), `FileAccessPolicy` (comparison, real folder locations, short names),
and `SandboxedFileResolver` (open and handle facts of its verified open). The tests reach the unit through the core
assembly's `InternalsVisibleTo` grant.
