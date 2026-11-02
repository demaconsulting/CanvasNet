## Files

![Files Structure](FilesView.svg)

The `Files` subsystem is the seventh software subsystem in CanvasNet. It provides sandboxed file access: given an
untrusted relative path, it finds and reads the file inside directories the caller chose (or inside a virtual file
source the caller supplies), and refuses everything else. It consists of one unit, `SandboxedFileResolver`, together
with the public types that unit needs - `SandboxOptions`, `SandboxedFile`, `SandboxException`, and
`SandboxFailureKind`.

### Purpose

The `Files` subsystem exists so that the sandbox rules for untrusted paths are implemented, tested, and reviewed
once. Before it existed, `CanvasNetScad` (include and use directives) and `CanvasNetVegaLite` (data URLs) each
carried a private copy of about 230 lines of the same mechanics, with small unintended differences between the
copies. The subsystem is public core API, not an internal helper: a library user that reads files named by
untrusted text can apply exactly the rules those two packages use.

The subsystem has no dependency on any other CanvasNet subsystem and uses only the .NET base class library. It is
deliberately small: it does not decode text, choose a search policy beyond "directory of the referring
file, then base directory, then search paths", or compare file names case-insensitively beyond the one platform rule
needed to compare file keys (see _SandboxedFileResolver Unit Design_). Its only resource limits are the byte limit of one
file (`MaxFileBytes`) and an optional limit on the number of distinct files one resolver reads (`MaxFiles`, off by
default), which refuses a new file beyond it before the file is opened or requested; any other limit (include depth,
cycles, text length) belongs to the caller.

### Units

- **SandboxedFileResolver** - the public resolver (`SandboxedFileResolver`) with its options (`SandboxOptions`),
  result (`SandboxedFile`), and failure reporting (`SandboxException`, `SandboxFailureKind`): validates the path text,
  confines each candidate to its root, refuses symbolic links and junctions, reads with a byte bound, supports a
  caller-supplied callback instead of the file system, and caches per instance; see _SandboxedFileResolver Unit
  Design_ (`files/sandboxed-file-resolver.md`)

### Dependencies

The `Files` subsystem depends only on the .NET base class library (`System.IO` and `System.Collections.Generic`),
available on every CanvasNet target framework, and has no runtime NuGet dependency. It has no dependency on the
`Canvas`, `Codecs`, `Geometry`, `Drawing`, `Fonts`, or `Rendering` subsystems, and none of the core subsystems depends on
it. Its consumers are outside the core package: the `CanvasNetVegaLite` and `CanvasNetScad` systems depend on it through
its public API (see _Callers_).

### Callers

`SandboxedFileResolver` is a public API entry point, invoked directly by consumers of the CanvasNet package. It is
also used internally, through its public API only (no `InternalsVisibleTo` entry exists for it), by two satellite
packages: `CanvasNetVegaLite`'s `DataFileResolver` unit (every local data file of a chart) and `CanvasNetScad`'s
`ScadFileResolver` unit (every `include` and `use`). Each of those units keeps its own exception types, message
wording, locations, limits, and text decoding and translates every `SandboxException` it receives.
