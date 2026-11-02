### ScadFileResolver

<!-- cspell:ignore evaluator undef OPENSCADPATH symlink symlinks sandboxed sandboxing junction junctions -->

The `ScadFileResolver` unit is the fourth software unit in the `ScadEvaluation` subsystem. It comprises three
source files in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace, all `internal`:

- `ScadFileResolver.cs` — `ScadFileResolver` (sealed partial class): the phase A driver — expansion, library
  loading from a work queue, and the cycle, depth, count, and statement limits
- `ScadFileResolver.Paths.cs` — the bridge to the core `SandboxedFileResolver` (see _SandboxedFileResolver Unit
  Design_, `canvas-net/files/sandboxed-file-resolver.md`): decoding and parsing of the file it returns, the
  per-file cache, display names, and the OpenSCAD wording of each sandbox refusal
- `ScadSourceUnit.cs` — `ScadSourceUnit`: a source file after `include` expansion plus the libraries it
  imports with `use`

#### Purpose

`ScadFileResolver` resolves the `include` and `use` directives of a document **before any evaluation** and
produces an immutable, include-expanded `ScadSourceUnit` for the main document together with the library units
it imports. It is the only code in the library that touches the file system or calls the caller's file
resolver callback, and it does so only through the core sandbox, because a document may come from an untrusted
source. The sandbox mechanics (path validation, confinement, link refusal, open and bounded read, callback mode,
cache) are implemented and verified once in the core `Files` subsystem; this unit keeps what is specific to OpenSCAD:
the search context, cycles and limits, decoding, naming, and wording. Doing all file access in one phase on the calling
thread keeps the evaluator pure and restartable (a
restart repeats no I/O and never calls the callback again) and makes missing-file warnings precede any output.
The resolution policy and the sandbox are original decisions of the CanvasNetScad dialect (see
_CanvasNetScad System Design_, Clean-Room Provenance); no OpenSCAD source or manual text was consulted.

The unit's behavior is specified by the `CanvasNetScad-ScadEvaluation-ScadFileResolver-*` requirements
(`Include`, `Use`, `Resolution`, `Sandbox`, `ResolverCallback`, `FileSystem`, `Cycles`, `DepthLimit`,
`FileCountLimit`, `MissingFiles`, `NoBase`, `SyntaxErrors`, `SourceLimit`, and `Cancellation`) in
`docs/reqstream/canvas-net-scad/scad-evaluation/scad-file-resolver.yaml`.

#### Data Model

**`ScadSourceUnit`** holds the display name of the file (the name carried by its source locations), the
**expanded statements** (the file-scope statements with every `include` replaced, in place, by the included
file's own recursively expanded statements, keeping their original locations, with the directives removed), and
**`Uses`**, the libraries imported by the file or by any file it includes, in order of **last** use (a later
`use` of the same library moves it to the end). A unit is filled in by the resolver and is immutable in practice
afterwards. Library units may reference each other (libraries may `use` one another), so `Uses` can form a
cycle; the evaluator handles that. `FromProgram(program, name)` builds a unit without resolution (directives
dropped) for evaluating a program in isolation.

**Resolver state.** One instance per `Load` call, holding the effective `ScadOpenOptions`, the `ScadLimits`, the
`ScadEvaluationSink` (for missing-file warnings), the cancellation token, one core `SandboxedFileResolver` (created
from the options: `BaseDirectory`, `LibraryPaths` as the search paths, `FileResolver`, and a byte limit of four bytes per
character of the source limit plus a byte-order mark), the per-file cache keyed by root and file key (a file found
through two overlapping roots is parsed once per root, because the root confines the file's own relative directives),
the library work queue, and the counters
for inclusions and expanded statements. The caches are **per call**: nothing is shared across calls, so concurrent
evaluations of one document cannot influence each other.

**Roots.** The allowed roots are the base directory of the options and each library path, or, in resolver mode, the
logical paths handed to the callback; the sandbox computes them.

#### Key Methods

##### `Load(ScadProgram main, string? mainName)`

Polls cancellation, creates the main unit, expands the main program's statements, and then expands the
registered libraries from a **work queue** (not recursively, so a long chain of libraries cannot deepen the
stack). The main file is the first entry of the include stack, so a file that includes the main file is a cycle.

##### Include semantics

`include <p>` is **textual inclusion**: the included file's top-level statements — assignments, definitions,
module instantiations, `echo`, and everything else — replace the directive and keep their own source
locations (so a diagnostic names the included file). Nested includes are expanded recursively, with paths
resolved relative to the _including_ file. A file included several times is **expanded each time but read and
parsed once** (a missing file is remembered too, so the callback sees each candidate path at most once). The
`use` directives found in an included file are collected into the `Uses` of the including file.

##### Use semantics

`use <p>` loads the file as its **own** `ScadSourceUnit` with its own root scope; it is not expanded into the
including file. The library's own includes are expanded inside the library, and the libraries it uses are
recorded in the library's own `Uses`, private to it. A library is loaded **once** per resolved file;
the cache entry is registered before the library is expanded, so libraries that use each other (or themselves)
terminate. What a library contributes at evaluation time is described in _ScadEvaluator Unit Design_ (the
imports semantics).

##### Resolution policy

The environment is never consulted: `OPENSCADPATH` and all other environment variables are ignored. For path
text `p`, backslashes are first normalized to `/`; the candidates are, in order, **the directory of the
including file**, then **each library path**, in order. The base directory comes from the open options or, for
a document opened from a file, is the folder of that file; a document parsed from a string or a stream has
none, so it needs a library path or a resolver to use `include` or `use`. Two spellings of one path (for
example `a/../b.scad` and `b.scad`) are one file; file-name case is significant on Linux and insensitive on
Windows and macOS (the platform comparer is used for the file keys and the root checks).

- **File-system mode** (no `FileResolver`): the sandbox accepts a candidate only if it lies under the root it was
  derived from (`..` that stays inside the root is allowed) and refuses it when **any component strictly below its
  root, including the file itself, is a symbolic link or junction, whatever the target, a dangling link included**
  (the link rule, its fail-closed probe, the top-down walk, the trusted root and the residual risks are specified at the
  core unit: _SandboxedFileResolver Unit Design_). A link raises `Include file '…' passes through a link ('<component
  below the root>'); links are not followed inside the allowed directories`, and a link check that cannot be completed
  raises `Include file '…' cannot be checked for links and is not read`. To use linked content, a caller adds the real
  directory as a library path. **Residual risk (documented, accepted):** the sandbox protects against path-based
  escapes in static file trees; it is **not a security boundary against a concurrent local attacker with write access to
  the included folders**, and **hard links cannot be detected**; keeping the allowed directories free of hard links to
  files outside them is the caller's responsibility, as is controlling who may write there; neither the requirement nor
  the tests claim protection against either case. A **directory in place of a file** (and any file that exists but
  cannot be opened) is a located `Include file '…' cannot be read` error, never a silently missing file. Files are
  opened read-only with shared access. The bytes the sandbox returns are decoded by `ScadDocument` with the shared
  bounded rules (UTF-8 with byte-order mark detection, at most 4,194,304 characters).
- **Resolver mode** (`FileResolver` supplied): **no file-system access at all.** Each candidate is a
  normalized **logical path** (forward slashes, no leading `/`, `..` collapsed) handed to the callback; the first
  non-null stream wins; the sandbox reads it with its bounded reader and then disposes of it. Library paths
  are used as logical prefixes; entries that
  are not valid logical paths (rooted, a colon, `..` escaping, control characters) are silently ignored; the
  base directory plays no role. An exception thrown by the callback (or by its stream) is a located
  `Include file '…' cannot be read` error, without the callback's own text; a cancellation raised by the callback
  passes through unchanged.
- **Hostile paths** — an absolute path, a drive letter or a colon anywhere in the text (which also excludes
  alternate data streams), an empty path, a control character (including NUL, and C1 characters), and `..` above the
  root — are a **sandbox violation**: `Include path 'p' is not allowed: <reason>`, or
  `Include path 'p' is outside the allowed directories`. In resolver mode the violation is raised **before** the
  callback is asked, so the callback only ever sees normalized logical paths. A configured base directory or library
  path that is not a valid path is a located `A configured directory (BaseDirectory or LibraryPaths) is not a valid path`
  error.
- **Display names** of loaded files are the logical path (resolver mode), the path relative to the base
  directory with `/` when the file is under it, and the full path otherwise.

##### Limits

- **Depth**: nesting of `include` deeper than `MaxIncludeDepth` (default 16, at most 64) →
  `Include nesting too deep (limit N)`. Expansion recursion is bounded by this limit, so it is trivially
  stack-safe even on a small stack.
- **Cycles**: a file already on the active include stack → `Include cycle: a → b → a` (with the arrows
  U+2192 and the chain of display names). A _diamond_ (one file included along two paths) is not a cycle.
- **File count**: the total number of resolved inclusions plus each distinct library loaded exceeding
  `MaxIncludedFiles` → `Included file limit exceeded (limit N)`; this stops a diamond blow-up.
- **Expanded statements**: the total number of statements produced by include expansion exceeding
  `MaxInstances` → `Included statement limit exceeded (limit N)`. Repeated inclusion copies statement
  references, and `MaxIncludedFiles` times the file size could otherwise exhaust memory in phase A, before
  any step accounting exists.
- **Source size**: each file at most 4,194,304 characters (`InvalidDataException`).
- **Cancellation**: polled at entry, between files, and at each library dequeue.

#### Error Handling

- **Warning**: a file that cannot be found (in any candidate) is `Can't open include file 'p'` — the same
  text for `include` and `use` — located at the directive; the directive is dropped, and no library unit is
  created for a missing `use`.
- **`ScadEvaluationException`** (located at the directive): sandbox violations (including a link, a failed link check,
  a file that exists but cannot be read, and an invalid configured directory), cycles, the depth, count, and
  statement limits, and `Cannot resolve 'p': the document has no base directory, library path or file resolver`
  when a directive is found and there is nowhere to look.
- **`ScadSyntaxException`** — a syntax error in an included or used file propagates unchanged; its location
  names that file (the parser is called with the file's display name).
- **`InvalidDataException`** — a file longer than 4,194,304 characters (also when the sandbox's byte bound is hit first).
- **`OperationCanceledException`** — never wrapped.
- The parser's own stack-safety fallback applies to each included file (`ScadParser.Parse` has its own gate);
  the parser's gate and the evaluator's gate are never held at the same time, because phase A finishes before
  phase B starts, so there is no lock-ordering hazard.

**Behavior change from the shared sandbox.** Adopting the core sandbox made four rules stricter than this unit's
earlier private implementation: a C1 control character in a path is refused (as C0 and DEL already were); a dangling
final link is a link error rather than a "Can't open include file" warning; a directory in place of a file, a file
that cannot be opened and an exception thrown by the resolver callback are located errors rather than a missing-file
warning or a raw exception; and a configured base directory or library path that is not a valid path is a located
error rather than a raw `ArgumentException`. Files are also opened with shared access and the open (not an existence
check) decides missing versus unreadable.

#### Dependencies

- **`SandboxedFileResolver`**, **`SandboxOptions`**, **`SandboxedFile`**, **`SandboxException`**, and
  **`SandboxFailureKind`** — the public types of the core `Files` subsystem that perform all file access.
- **`ScadOpenOptions`** (the `ScadDocument` unit) — `BaseDirectory`, `LibraryPaths`, `FileResolver`.
- **`ScadDocument.DecodeBounded`** — the shared bounded UTF-8 decoder (internal and static).
- **`ScadLanguage` subsystem** — `ScadParser.Parse(string, string?)` and the syntax tree (`ScadProgram`,
  `ScadDirectiveStatement`).
- **`ScadEvaluationSink`** — receives the missing-file warnings. **`ScadLimits`** — the limits.
- **.NET base class library** — `System.IO` (`Path`, `MemoryStream`), and `System.Collections.Generic`.

#### Callers

`ScadEvaluator.Evaluate(ScadDocument, …)` is the only production caller: it creates a resolver over the
document's `Options`, `Program`, and `SourceName` on the calling thread before the evaluation pass. The tests
in `test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadFileResolver*Tests.cs` call `Load` directly through
`InternalsVisibleTo`, using the in-memory `ScadVirtualFiles` resolver (which records every path requested, so a
test can assert that no other path is ever asked for) and, for file-system tests, a per-test temporary
directory (`ScadTempDirectory`). The sandbox mechanics (the link walk and its fail-closed probe, the dangling-link
rule, the open rules) are verified on every platform at the core unit through its internal link-probe and open seams;
this unit's tests prove the wording and location of each refusal (`Translate`) and, with real links (a junction or
symbolic link) that skip themselves where the host does not permit creating them, that a real link surfaces as a
located error; the real-link tests are not cited by a requirement.
