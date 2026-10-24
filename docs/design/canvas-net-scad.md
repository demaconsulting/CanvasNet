# System Design

<!-- cspell:ignore lexer lexing tokenization tokenizes tokenized tokenize parsers recursive-descent -->
<!-- cspell:ignore nupkg unpackable evaluator evaluators polyhedra Minkowski -->

This document provides the system-level design for CanvasNetScad.

![CanvasNetScad Structure](CanvasNetScadView.svg)

## Architecture

CanvasNetScad is a clean-room front end for the OpenSCAD language, delivered incrementally as its own
NuGet package (`DemaConsulting.CanvasNet.Scad`, root namespace `DemaConsulting.CanvasNet.Scad`),
independent of the other document systems in this repository. It reads OpenSCAD-language source text
and, in the current phase (Phase 4), is **parse-only**: it validates the source against a documented
grammar and reports what the source declares at file scope, or the first syntax error with its file,
line, and column. It does not evaluate expressions, generate geometry, resolve `include`/`use`
directives, or render anything; those capabilities are the subject of later phases (see the
_Implementation Phase Plan_ below). The project is not yet packable: its `IsPackable` property is
`false` until Phase 6 delivers the first user-visible capability, so that no mid-roadmap release
publishes a parse-only package.

`CanvasNetScad` consists of one direct unit and one subsystem:

- **ScadDocument** (unit; namespace `DemaConsulting.CanvasNet.Scad`, folder
  `src/DemaConsulting.CanvasNet.Scad/`): the public API. `ScadDocument` offers the entry points
  `Parse(string)`, `Open(string)` (a file path), and `Open(Stream)` and exposes `SourceName`,
  `Directives`, `ModuleNames`, and `FunctionNames`. The supporting public types are `ScadOpenOptions`
  (immutable, validated options), `ScadSourceLocation` (file, line, column), `ScadSyntaxException`
  (the error type, a `FormatException`), `ScadDirective`, and `ScadDirectiveKind`. See _ScadDocument
  Unit Design_ (`canvas-net-scad/scad-document.md`).
- **ScadLanguage** (internal subsystem; namespace `DemaConsulting.CanvasNet.Scad.Language`, folder
  `Language/`): the language front end, with two units. `ScadLexer` converts source text into a token
  list (the internal `ScadLexer`, `ScadToken`, `ScadTokenKind`). `ScadParser` converts the tokens into
  an immutable syntax tree (the internal `ScadParser`, the syntax-tree records, and `ScadTreeDepth`,
  which verifies the finished tree's height). The subsystem has no public types: the syntax tree is
  deliberately internal so that it can evolve while evaluation and rendering are added. See
  _ScadLanguage Subsystem Design_ (`canvas-net-scad/scad-language.md`).

Dependencies inside the system are one-directional: `ScadDocument` calls `ScadParser`, which calls
`ScadLexer`; the `ScadLanguage` subsystem never calls back into `ScadDocument` (it uses only the
shared `ScadSourceLocation`, `ScadSyntaxException`, and `ScadDirectiveKind` value types).

## External Interfaces

The system exposes the following public API in the `DemaConsulting.CanvasNet.Scad` namespace:

- **`ScadDocument`** (sealed, immutable, thread-safe, not `IDisposable`): the static
  `Parse(string, ScadOpenOptions?)`, `Open(string, ScadOpenOptions?)`, and
  `Open(Stream, ScadOpenOptions?)` entry points; and the `SourceName`, `Directives`, `ModuleNames`, and
  `FunctionNames` properties.
- **`ScadOpenOptions`** (sealed): init-only `BaseDirectory`, `LibraryPaths`, `FileResolver`, and
  `SourceName`. The first three are accepted and validated now but consumed only from Phase 5.
- **`ScadSourceLocation`** (readonly record struct): `File`, one-based `Line`, and one-based `Column`;
  `default` (line zero) means "unknown".
- **`ScadSyntaxException`** (sealed, derives from `FormatException`): carries the `Location` of the
  first syntax error; the message is prefixed with the location when it is known.
- **`ScadDirective`** (sealed record) and **`ScadDirectiveKind`** (`Include`, `Use`): one recorded
  file-scope directive.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `ScadDocument.Parse(string, ...)` | Inbound | OpenSCAD-language source text | Non-null; at most 4,194,304 UTF-16 characters; empty, whitespace-only, and comment-only text is a valid empty document; a leading U+FEFF is ignored |
| `ScadDocument.Open(string, ...)` | Inbound | File path | Non-null and not empty or whitespace; read as UTF-8 with byte-order-mark detection; missing file or directory surfaces the natural .NET exception |
| `ScadDocument.Open(Stream, ...)` | Inbound | Readable stream | Non-null and readable; read from the current position as UTF-8; left open; reading stops one 8 KiB chunk past the size limit |
| `ScadOpenOptions` properties | Inbound | Text, list, delegate | Each value validated when initialized (text null or non-whitespace; library paths a non-null list without null or whitespace entries, copied); instance therefore always valid |
| `ScadDocument` properties | Outbound | Names, read-only lists | `Directives`, `ModuleNames`, and `FunctionNames` are file-scope only, in source order, read-only |
| `ScadSyntaxException` | Outbound | Exception with `Location` | Thrown for the first lexical or grammatical error and for nesting deeper than 256 levels |
| `InvalidDataException` | Outbound | Exception | Thrown, not as a `ScadSyntaxException`, when the source exceeds 4,194,304 characters (no location is meaningful) |
<!-- markdownlint-enable MD013 -->

Any file referenced by an `include` or `use` directive is never opened: directives are recorded only.
The `ScadLanguage` subsystem's types are internal and visible only to the test assembly through
`InternalsVisibleTo`. See the unit and subsystem design documents for every type's complete
constructor, property, and exception detail.

## Dependencies

`CanvasNetScad` references the separate `CanvasNet` system and the `CanvasNetRender3D` system through
project references in
`src/DemaConsulting.CanvasNet.Scad/DemaConsulting.CanvasNet.Scad.csproj`:

- `CanvasNet` (its own package, `DemaConsulting.CanvasNet`), for the future `Surface`/`Rgba32` output
- `CanvasNetRender3D` (its own package, `DemaConsulting.CanvasNet.Render3D`), whose `Scene` the later
  phases will build from evaluated geometry

**In Phase 4 no type from either system is used by any code.** The references are architectural
scaffolding added now so that the project graph does not have to be restructured when Phase 6
renders a document through `CanvasNetRender3D`; they are modeled in the SysML2 model as the
`usesCanvas` and `usesRender3D` dependencies. Consequently the Phase 4 behavior is independent of
both systems.

**`CanvasNetScad` must never reference `DemaConsulting.CanvasNet.Svg`, `DemaConsulting.CanvasNet.Pdf`,
`DemaConsulting.CanvasNet.Pptx`, `DemaConsulting.CanvasNet.Vsdx`, or `DemaConsulting.CanvasNet.Charts`.**
It is a source-language front end for 3D models, unrelated to those document-format systems.

Beyond these project references, `CanvasNetScad` uses only the .NET base class library: `System.IO`
(`StreamReader`, `File`), `System.Text` (`StringBuilder`, `Encoding`), `System.Globalization`
(invariant-culture number parsing and formatting), `System.Collections.ObjectModel`
(`ReadOnlyCollection<T>`), and `System.Runtime.CompilerServices` (the stack-space guard). These are
platform types available on every target framework, not OTS Software Items, and require no runtime
NuGet dependency; the project references exactly the same build-time analyzers and tooling packages
as its sibling projects.

This is an ordinary, same-repository, system-to-system dependency: it is neither an OTS Software
Item nor a Shared Package (that category is scoped to a package produced by a _different_
repository). `CanvasNetScad` introduces no new OTS Software Item — see _OTS Integration Design_
(`docs/design/ots.md`).

## Risk Control Measures

`CanvasNetScad` has no segregation requirement for safety. Because OpenSCAD-language source may come
from an untrusted origin, its risk control measures are defensive-correctness measures against
invalid and hostile input:

- **Fail-closed validation.** Every entry point and every `ScadOpenOptions` property validates its
  argument eagerly and throws a specific exception (`ArgumentNullException`, `ArgumentException`)
  rather than coercing it; options are immutable once constructed, so the parser trusts every
  instance it receives.
- **Fail-fast syntax errors.** The first lexical or grammatical error aborts parsing with one
  `ScadSyntaxException` carrying its location. There is no error recovery and no partial syntax tree,
  so a consumer can never observe a half-parsed program.
- **Bounded recursion.** Nesting deeper than 256 levels is rejected with a `ScadSyntaxException`.
  The limit is enforced twice: by a recursion counter while parsing (backed by
  `RuntimeHelpers.EnsureSufficientExecutionStack`, whose failure is converted to the same error), and
  by an iterative post-parse check of the finished tree's height (`ScadTreeDepth`). The second check
  is necessary because left-associative operator chains and postfix chains are built in loops: a
  hostile input can build a very deep, left-leaning tree without any parser recursion, which would
  overflow the stack of a later recursive consumer. Every later consumer of the tree (an evaluator, a
  dumper, record equality, record printing) may therefore assume a tree depth of at most 256. A stack
  overflow cannot be caught and would terminate the host process.
- **Bounded input size.** Source longer than 4,194,304 UTF-16 characters is rejected with an
  `InvalidDataException`. `Open(Stream)` reads in chunks and stops one chunk past the limit, so a
  stream that never ends cannot consume unbounded memory. The size of the syntax tree is
  proportional to the number of tokens, so this also bounds tree memory.
- **No file or network access.** Parsing reads only the source it is given. `include` and `use` paths
  are recorded as text and never opened, and the file resolver and base directory in
  `ScadOpenOptions` are not consumed. No code in the source is ever executed.
- **Deterministic, culture-independent behavior.** Numbers are parsed and locations formatted with the
  invariant culture, so results do not depend on the host's locale.

## Data Flow

**Parse path (`ScadDocument.Parse`, `Open(string)`, `Open(Stream)`):**

1. **Validate** the arguments and options (null source, null or whitespace path, unreadable stream).
   `Open(string)` fixes the effective source name (the path as supplied, unless overridden) and the
   effective base directory (the folder of the path's full name, unless overridden).
2. **Read**: `Open` reads the file or stream as UTF-8 in chunks, detecting and removing a byte-order
   mark, and fails with `InvalidDataException` as soon as the running length exceeds the limit.
   `Parse` checks the length of the supplied string.
3. **Tokenize**: `ScadLexer.Tokenize` converts the whole source into a token list ending with an
   end-of-file token, skipping white space and comments and tracking one-based line and column
   positions. Any lexical error aborts here, so a lexical error anywhere in the file is reported
   before any grammatical error, even one earlier in the file (a documented consequence of
   tokenizing before parsing, pinned by a test).
4. **Parse**: `ScadParser` runs recursive descent for statements and precedence climbing for
   expressions over the token list and builds the immutable syntax tree, counting nesting depth as it
   goes.
5. **Verify**: `ScadTreeDepth.Verify` iteratively checks the height of the finished tree.
6. **Extract**: `ScadDocument` reads the file-scope directives, module names, and function names
   from the tree once, as read-only lists.
7. **Output**: an immutable `ScadDocument`, safe to share between threads.

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Dependency direction**: `CanvasNetScad` must never reference `CanvasNetSvg`, `CanvasNetPdf`,
  `CanvasNetPptx`, `CanvasNetVsdx`, or `CanvasNetCharts` (see Dependencies above)
- **Determinism**: The same source always yields the same tree or the same error at the same location,
  on every platform and in every culture
- **Name-agnostic parsing**: The parser does not know any built-in module or function. Every
  `name(arguments) children` statement is a module instantiation, so an unknown or unsupported name
  never produces a syntax error; resolving names is the job of the evaluation phase
- **Additive evolution**: The public types are sealed, options are init-only, results are read-only
  lists, and records and enumerations tolerate added members, so later phases extend the API without
  breaking it

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it references,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs. No platform-specific native interop, OS-specific APIs, or framework-version-specific features
are used. Locations count UTF-16 code units and treat `\r\n`, `\n`, and a lone `\r` each as one line
break, so results are identical on every platform regardless of the checked-out line-ending style of
the source.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, enabled in Phase 6
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Front-end role**: A later phase evaluates a parsed `ScadDocument` into a `CanvasNetRender3D`
  `Scene` and renders it; the parser never calls into the renderer, and the renderer has no knowledge
  of the OpenSCAD language

### Clean-Room Provenance

OpenSCAD is licensed under the GPL and its manual under CC BY-SA. `CanvasNetScad` is an
**original, clean-room implementation**, written from general knowledge of C-family language design
and the approved roadmap for this repository. No OpenSCAD source code, grammar or lexer definition,
test corpus, manual prose, manual example, or library (and no parser, grammar, or syntax-highlighter
derived from any of these) was read, copied, ported, or paraphrased, and none was consulted to decide
a detail. Where the roadmap was silent, a decision was made by general language-design reasoning and
recorded in this document. The test fixtures
(`test/DemaConsulting.CanvasNet.Scad.Tests/ScadFixtures/`) are original works authored for this
repository and licensed with it; their `README.md` records their provenance. Exact parity with the
behavior of any existing implementation is an explicit non-goal.

The provenance statement is repeated in the namespace documentation, the project description, the
fixture README, and the purpose of each unit design, and every `CanvasNetScad` review-set checks it
(see `.reviewmark.yaml`). Any future contribution to this system shall observe the same rule.

### Supported And Deferred Features

<!-- markdownlint-disable MD013 -->
| Feature | Status | Notes |
| ------- | ------ | ----- |
| Lexical forms: numbers, strings with escapes, identifiers, special variables, comments, directives | Supported (Phase 4) | See _ScadLexer Unit Design_ |
| Assignments, module and function definitions, module instantiations with positional and named arguments | Supported (Phase 4) | The parser does not know built-in names |
| Modifier characters (disable, show-only, highlight, background) | Supported (Phase 4) | On instantiation-like statements and blocks |
| `if`/`else`, `for`, `intersection_for`, `let`, `echo`, `assert` (statements and expressions) | Supported (Phase 4) | |
| Expressions: literals, vectors, ranges, postfix index/member/call, function literals, conditional, operators | Supported (Phase 4) | Precedence table in _ScadParser Unit Design_ |
| List comprehensions: `for`, C-style `for`, `if`/`else`, `let`, `each` | Supported (Phase 4) | Valid only inside a vector |
| `include`/`use` directives | Recorded (Phase 4) | File-scope only; never resolved or opened |
| Public `ScadOpenOptions.BaseDirectory`, `LibraryPaths`, `FileResolver` | Accepted, not consumed | Consumed from Phase 5 |
| Expression evaluation, variable scoping, special-variable semantics, built-in functions | Deferred (Phase 5) | Adds `ScadEvaluationException` and `ScadLimits` |
| Module resolution, `include`/`use` file resolution, file-access sandbox policy | Deferred (Phase 5) | Governed by the open options |
| Primitive solids and 2D shapes, transforms, boolean operations, extrusions | Deferred (Phase 6) | Mapped onto `CanvasNetRender3D` |
| Public render entry point, render options, unsupported-feature reporting | Deferred (Phase 6) | Adds `Render`, `ScadRenderOptions`, `ScadUnsupportedFeature` types and the diagnostics |
| Packaging as a NuGet package, README and user guide | Deferred (Phase 6) | `IsPackable` is `false` until then |
| Hull and Minkowski operations | Deferred (Phase 7) | Depends on `CanvasNetRender3D` support |
| Text and external-file import | Deferred (later) | |
| Multiple error reporting and error recovery | Not planned | Additive later, for example as an errors list |
<!-- markdownlint-enable MD013 -->

### Deliberate Strictness And Deviations

Because the implementation is clean-room, the accepted language is the documented grammar of this
design, not a verified clone of any other implementation. Each point below is either a superset of,
or stricter than, what a user might expect; relaxing any of them later is additive. Each is pinned by
a test.

1. Identifiers are ASCII only and cannot start with a digit (`2cm` lexes as a number then an
   identifier and is then a syntax error).
2. An unknown escape in a string is an error. `\x` takes exactly two hexadecimal digits, `\u` four,
   and `\U` six; surrogate code points and code points above U+10FFFF are rejected; `\x00` and
   `\u0000` are accepted.
3. `include` and `use` are accepted only at file scope and must be followed by an angle-bracketed
   path on the same line; both words are reserved.
4. An assignment or a module or function definition is not accepted as an unbraced child statement
   ("Use braces around a declaration").
5. A C-style `for(init; condition; increment)` is accepted only inside a list comprehension; in
   statement position it is an error.
6. A modifier is accepted only in front of an instantiation-like statement or a block.
7. The power operator binds tighter than unary minus and is right-associative (so `-2^2` is
   `-(2^2)` and `2^3^2` is `2^(3^2)`); relational and equality operators are two separate levels, as
   in C-family languages.
8. A trailing comma is accepted in vectors, parameter lists, argument lists, `let` assignment lists,
   and iterator lists.
9. Parsing is fail-fast, reports a single error, and reports lexical errors before grammatical errors.
10. Nesting is limited to 256 levels and the source length to 4,194,304 characters.

### Determinism And Immutability

Every public result type is immutable. A `ScadDocument` and its internal syntax tree can be shared
between threads, and parsing the same text any number of times, on any thread, in any culture, yields
equal trees and identical errors.

## Implementation Phase Plan

`CanvasNetScad` is delivered incrementally; each phase is a separate pull request that adds
requirements, design, verification, and tests for its own scope.

1. **Phase 4 — Scaffolding, lexer, parser (delivered).** The project and test project, the
   `ScadLanguage` subsystem (`ScadLexer`, `ScadParser`), the parse-only public `ScadDocument` surface,
   original fixtures with a provenance README, and the complete companion documentation. (The phase
   number continues the numbering of the repository's 3D roadmap, whose first three phases delivered
   `CanvasNetRender3D`.)
2. **Phase 5 — Evaluation (planned).** An evaluator over the syntax tree: values, scoping, functions,
   built-in mathematical functions, special variables, `include`/`use` resolution governed by
   `ScadOpenOptions`, and resource limits (`ScadLimits`, `ScadEvaluationException`).
3. **Phase 6 — Geometry and rendering (planned).** Mapping evaluated modules onto `CanvasNetRender3D`
   scenes, the public render entry point and its options and diagnostics, the flip of `IsPackable`, and
   the README and user guide.
4. **Phase 7 — Hull and Minkowski (planned)**, depending on `CanvasNetRender3D` support.
5. **Later — Text and external-file import.**

Every later phase adds members and types rather than changing the signatures delivered here.
