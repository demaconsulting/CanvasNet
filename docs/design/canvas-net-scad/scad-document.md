## ScadDocument Unit Design

![CanvasNetScad Structure](CanvasNetScadView.svg)

<!-- cspell:ignore lexer lexical -->

### Purpose

`ScadDocument` is the public entry point of `CanvasNetScad`. It is the parse-only front end for
OpenSCAD-language source: it reads source text from a string, a stream or a file, drives the internal
`ScadLanguage` subsystem to tokenize and parse it, and exposes the validated result. A document
proves that the source is syntactically valid and reports what it declares at file scope (the
`include`/`use` directives and the names of the file-scope modules and functions). It never evaluates
the program and never opens another file. The package is an original implementation written from
general language-design knowledge; see _ScadLexer_ (`scad-language/scad-lexer.md`) for the clean-room
statement that applies to the whole system. The unit covers the files `ScadDocument.cs`,
`ScadOpenOptions.cs`, `ScadSourceLocation.cs`, `ScadSyntaxException.cs`, `ScadDirective.cs`,
`ScadDirectiveKind.cs`, and the namespace documentation `NamespaceDoc.cs`.

The unit's behavior is specified by the `CanvasNetScad-ScadDocument-*` requirements in
`docs/reqstream/canvas-net-scad/scad-document.yaml` and, at system level, by `CanvasNetScad-Open`,
`CanvasNetScad-OpenOptions`, `CanvasNetScad-Directives`, `CanvasNetScad-Definitions`,
`CanvasNetScad-SyntaxErrors`, and `CanvasNetScad-InputLimits`.

### Data Model

- **`ScadDocument`** (sealed class, immutable) — holds the internal `ScadProgram` syntax tree, the
  effective `ScadOpenOptions`, and three public read-only lists derived from the tree once at
  construction: `Directives`, `ModuleNames`, and `FunctionNames`. The constructor is private; the only
  ways to obtain a document are `Parse` and the two `Open` overloads. The `Program` and `Options`
  properties are `internal` so that later phases (evaluation, rendering) can consume them without
  widening the public surface. A document holds no unmanaged resources and is not disposable; it is
  safe for concurrent use.
- **`ScadOpenOptions`** (sealed class, init-only) — `BaseDirectory` (`string?`), `LibraryPaths`
  (`IReadOnlyList<string>`, empty by default), `FileResolver` (`Func<string, Stream?>?`), and
  `SourceName` (`string?`). Every property is validated in its `init` accessor: null is allowed for
  the nullable properties, but empty or whitespace text throws `ArgumentException`; a null
  `LibraryPaths` list or entry throws `ArgumentNullException`, and a whitespace entry throws
  `ArgumentException`. `LibraryPaths` is copied defensively into a read-only list, so a later change to
  the caller's list has no effect. `BaseDirectory`, `LibraryPaths` and `FileResolver` are accepted and
  stored now but not consumed until file resolution is added in a later phase; `SourceName` is
  consumed immediately because it appears in every `ScadSourceLocation`. The internal
  `WithEffective(baseDirectory, sourceName)` creates a copy with those two values filled in.
- **`ScadSourceLocation`** (`readonly record struct (string? File, int Line, int Column)`) — a
  one-based position. A column counts UTF-16 code units, a tab counts as one column, and `\r\n`,
  `\n`, and a lone `\r` each end exactly one line. `default(ScadSourceLocation)` has `Line == 0` and
  means "unknown". `ToString()` returns `{File}:{Line}:{Column}`, `{Line}:{Column}` when `File` is
  null, or `?` when the line is zero; numbers use the invariant culture.
- **`ScadSyntaxException`** (sealed, derives from `FormatException`) — carries a `Location`
  (`ScadSourceLocation`, `default` when unknown). The constructor taking `(description, location)`
  builds the message `{location}: {description}`, or just the description when the location is unknown;
  the standard parameterless, message, and message-plus-inner-exception constructors leave the location
  unknown. The type derives from `FormatException` because `InvalidDataException` is sealed in the base
  class library.
- **`ScadDirective`** (sealed record) — `Kind` (`ScadDirectiveKind`), `Path` (the exact text between
  the angle brackets, never empty and never trimmed), and `Location` (the directive keyword).
- **`ScadDirectiveKind`** (enum) — `Include` and `Use`. The same enum is shared by the public API and
  the internal syntax tree (`ScadDirectiveStatement`); consumers should handle unknown values
  defensively.

**Invariants**: a `ScadDocument` exists only for source that tokenized and parsed without error, so
`Directives`, `ModuleNames`, and `FunctionNames` are always consistent with a valid program. The
three lists are in source order, are read-only, and keep duplicates. `ModuleNames` and `FunctionNames`
list only definitions at file scope; definitions nested in blocks or in other definitions are not
listed. Directives are only legal at file scope, so `Directives` is the complete set.

### Key Methods

- `ScadDocument.Parse(string source, ScadOpenOptions? options = null)` — throws
  `ArgumentNullException` for a null source, uses default options when `options` is null, and parses
  the text. Empty, whitespace-only, and comment-only text is a valid empty document; a leading U+FEFF is
  ignored by the lexer. No base directory or source name is invented, so locations are unnamed unless
  the caller supplies `SourceName`.
- `ScadDocument.Open(string path, ScadOpenOptions? options = null)` — validates the path
  (`ArgumentException.ThrowIfNullOrWhiteSpace`), computes the effective options, opens the file with
  `File.OpenRead`, reads it as bounded UTF-8 text, and parses. Unless overridden by `options`, the
  source name is the path exactly as supplied and the base directory is the folder of
  `Path.GetFullPath(path)`. A missing file or folder surfaces as `FileNotFoundException` or
  `DirectoryNotFoundException` from the file system.
- `ScadDocument.Open(Stream stream, ScadOpenOptions? options = null)` — throws
  `ArgumentNullException` for a null stream and `ArgumentException` for an unreadable stream, reads
  from the stream's current position to its end, and leaves the stream open. No base directory or
  source name is invented.
- `ReadBounded(Stream)` (private) — reads UTF-8 with byte-order-mark detection through a
  `StreamReader` (`leaveOpen: true`) in 8,192-character chunks and checks the running length after each
  chunk. As soon as the length exceeds `ScadParser.MaxSourceLength` (4,194,304 UTF-16 characters) it
  throws, so a stream that never ends cannot consume unbounded memory.
- `Create(source, effective)` (private) — calls `ScadParser.Parse(source, effective.SourceName)` and
  wraps the resulting `ScadProgram` in a document. The constructor then extracts the three public lists
  with one pass of type filtering over the program's top-level statements
  (`ScadDirectiveStatement`, `ScadModuleDefinition`, `ScadFunctionDefinition`).

**Guarantees**: every entry point either returns a fully populated,
immutable document or throws; there is no partially valid document. The parse is a pure function of the
source text and options: no file other than the one named by `Open(path)` is accessed, so an
`include`/`use` path need not exist and `FileResolver` is never invoked.

### Error Handling

- Programming-error arguments (a null source, path, or stream; an empty or whitespace path; an
  unreadable stream) throw `ArgumentNullException` or `ArgumentException` synchronously and are never
  caught or converted. `ScadOpenOptions` fails closed in the same way when a property is initialized
  with an invalid value.
- Source that cannot be tokenized or parsed throws `ScadSyntaxException` with the location of the
  first error (see _ScadLanguage_, `scad-language.md`, for the fail-fast model and the exact message
  catalogue in _ScadParser_ and _ScadLexer_). Nesting deeper than 256 levels is reported the same way
  rather than overflowing the stack.
- Source longer than 4,194,304 UTF-16 characters throws a plain `InvalidDataException` with the message
  `Source exceeds the maximum length of 4194304 characters`, not a `ScadSyntaxException`, because no
  source location is meaningful for it. This applies to `Parse` (checked before any lexing), to
  `Open(Stream)`, and to `Open(path)` (checked while reading).
- File-system exceptions from `File.OpenRead` and stream read failures (`IOException` and derivatives)
  propagate unchanged.

### Dependencies

- **`ScadLanguage` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Language`) —
  `ScadParser.Parse(string, string?)` (which tokenizes with `ScadLexer` and parses), the `ScadProgram`
  syntax tree and its statement nodes (`ScadDirectiveStatement`, `ScadModuleDefinition`,
  `ScadFunctionDefinition`), and the constants and factory behind the source-length limit
  (`ScadParser.MaxSourceLength`, `ScadParser.CreateSourceTooLongException`). See _ScadLanguage_
  (`scad-language.md`).
- **.NET base class library** — `System.IO` (`File`, `Stream`, `StreamReader`, `Path`,
  `InvalidDataException`, `FileNotFoundException`), `System.Text.Encoding.UTF8`, `System.Text.StringBuilder`,
  and `System.Linq` for the derived lists.
- No other `CanvasNet` package is used by the code; the project references the core and `Render3D`
  packages only in anticipation of the later evaluation and rendering phases.

### Callers

`ScadDocument` is the sole public entry point for reading OpenSCAD-language source; it is called
directly by consuming applications. Within this repository no other unit calls it. System-level
integration tests in `test/DemaConsulting.CanvasNet.Scad.Tests/ScadSystemIntegrationTests.cs` exercise
it through its public API, and the unit tests in `ScadDocumentTests.cs`, `ScadOpenOptionsTests.cs`,
`ScadSourceLocationTests.cs`, and `ScadSyntaxExceptionTests.cs` verify the individual types.
