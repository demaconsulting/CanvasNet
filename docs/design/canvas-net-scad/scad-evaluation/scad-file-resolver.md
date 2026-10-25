### ScadFileResolver

<!-- cspell:ignore evaluator undef OPENSCADPATH symlink symlinks sandboxed sandboxing junction junctions -->

The `ScadFileResolver` unit is the fourth software unit in the `ScadEvaluation` subsystem. It comprises three
source files in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace, all `internal`:

- `ScadFileResolver.cs` — `ScadFileResolver` (sealed partial class): the phase A driver — expansion, library
  loading from a work queue, the file cache, and the cycle, depth, count, and statement limits
- `ScadFileResolver.Paths.cs` — path normalization, candidate generation, the sandbox checks (including the
  link walk), and file and callback access
- `ScadSourceUnit.cs` — `ScadSourceUnit`: a source file after `include` expansion plus the libraries it
  imports with `use`

#### Purpose

`ScadFileResolver` resolves the `include` and `use` directives of a document **before any evaluation** and
produces an immutable, include-expanded `ScadSourceUnit` for the main document together with the library units
it imports. It is the only code in the library that touches the file system or calls the caller's file
resolver callback, and it confines that access to a sandbox, because a document may come from an untrusted
source. Doing all file access in one phase on the calling thread keeps the evaluator pure and restartable (a
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
`ScadEvaluationSink` (for missing-file warnings), the cancellation token, the caches, the library work queue,
and the counters for inclusions and expanded statements. The cache is **per call**: nothing is shared across
calls, so concurrent evaluations of one document cannot influence each other.

**Roots.** The allowed roots are the base directory of the options and each library path (all converted with
`Path.GetFullPath`), or, in resolver mode, the logical paths handed to the callback.

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

- **File-system mode** (no `FileResolver`): a candidate is `Path.GetFullPath`-normalized and accepted only if it
  lies under the root it was derived from. `..` that stays inside the root is allowed. **Link policy:** a
  candidate is rejected when **any component strictly below its root, including the file itself, is a symbolic
  link or junction, whatever the target** (`FileSystemInfo.LinkTarget` is not null; the probe is an injectable
  `Func<string, bool>` that defaults to that check). The walk runs **top-down from the root**, probes only
  components that exist, and ends at the first nonexistent component (the file is then missing, the search
  continues, and nothing below the missing component is probed, so the existence of a file behind a link is never
  revealed); the first link raises `Include file '…' passes through a link ('<component below the root>'); links
  are not followed inside the allowed directories`. The walk happens before the file is opened, on the normalized
  path (so `a/../lib/x.scad` is judged as `lib/x.scad`), and each component is probed once per resolver call
  (results are cached). A probe that throws (`IOException`, `UnauthorizedAccessException`, …) **fails closed**
  with `Include file '…' cannot be checked for links and is not read`. The **root itself and everything above it
  are trusted** — the caller chose them, and this keeps hosts whose temporary folder is itself a link working.
  Strict rejection was chosen over resolving real paths because resolution is ambiguous (relative links, chains,
  junction targets) and racy; it tightens an earlier rule that allowed a file link whose target was inside a root.
  To use linked content, a caller adds the real directory as a library path. **Residual risk (documented):** an
  attacker who can modify the tree between the check and the open (a time-of-check/time-of-use race) is outside
  the threat model of an untrusted document plus caller-chosen directories. A directory in place of a file
  counts as missing. Files are read with the shared bounded UTF-8 reader of `ScadDocument` (byte-order mark
  detection, at most 4,194,304 characters).
- **Resolver mode** (`FileResolver` supplied): **no file-system access at all.** Each candidate is a
  normalized **logical path** (forward slashes, no leading `/`, `..` collapsed) handed to the callback; the first
  non-null stream wins; the unit reads it with the shared bounded reader and then disposes of it. Library paths
  are used as logical prefixes; entries that
  are not valid logical paths (rooted, a colon, `..` escaping, control characters) are silently ignored; the
  base directory plays no role. An exception thrown by the callback propagates unchanged.
- **Hostile paths** — an absolute path, a drive letter or a colon anywhere in the text (which also excludes
  alternate data streams), an empty path, a control character (including NUL), and `..` above the root — are a
  **sandbox violation**: `Include path 'p' is not allowed: <reason>`, or
  `Include path 'p' is outside the allowed directories`. In resolver mode the violation is raised **before** the
  callback is asked, so the callback only ever sees normalized logical paths.
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
- **`ScadEvaluationException`** (located at the directive): sandbox violations, cycles, the depth, count, and
  statement limits, and `Cannot resolve 'p': the document has no base directory, library path or file resolver`
  when a directive is found and there is nowhere to look.
- **`ScadSyntaxException`** — a syntax error in an included or used file propagates unchanged; its location
  names that file (the parser is called with the file's display name).
- **`InvalidDataException`** — a file longer than 4,194,304 characters. Other `IOException`s from reading an
  existing file propagate as they are.
- **`OperationCanceledException`** — never wrapped.
- The parser's own stack-safety fallback applies to each included file (`ScadParser.Parse` has its own gate);
  the parser's gate and the evaluator's gate are never held at the same time, because phase A finishes before
  phase B starts, so there is no lock-ordering hazard.

#### Dependencies

- **`ScadOpenOptions`** (the `ScadDocument` unit) — `BaseDirectory`, `LibraryPaths`, `FileResolver`.
- **`ScadDocument.ReadBounded`** — the shared bounded UTF-8 stream reader (internal and static).
- **`ScadLanguage` subsystem** — `ScadParser.Parse(string, string?)` and the syntax tree (`ScadProgram`,
  `ScadDirectiveStatement`).
- **`ScadEvaluationSink`** — receives the missing-file warnings. **`ScadLimits`** — the limits.
- **.NET base class library** — `System.IO` (`File`, `Directory`, `FileInfo.LinkTarget`,
  `DirectoryInfo.LinkTarget`, `Path`), and `System.Collections.Generic`.

#### Callers

`ScadEvaluator.Evaluate(ScadDocument, …)` is the only production caller: it creates a resolver over the
document's `Options`, `Program`, and `SourceName` on the calling thread before the evaluation pass. The tests
in `test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadFileResolver*Tests.cs` call `Load` directly through
`InternalsVisibleTo`, using the in-memory `ScadVirtualFiles` resolver (which records every path requested, so a
test can assert that no other path is ever asked for) and, for file-system tests, a per-test temporary
directory (`ScadTempDirectory`). The link rule is verified on every platform through the internal **link-probe
seam** (an optional `Func<string, bool>` constructor parameter that tests replace with a fake reporting chosen
real temporary paths as links, so the walk logic never needs a real link). Additional tests create real links (a
junction or symbolic link) and skip themselves where the host does not permit creating them; they are not cited
by a requirement.
