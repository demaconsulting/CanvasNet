## ScadLanguage

<!-- cspell:ignore lexer lexical lexing tokenizer -->

![ScadLanguage Structure](ScadLanguageView.svg)

### Overview

The `ScadLanguage` subsystem (namespace `DemaConsulting.CanvasNet.Scad.Language`, folder
`src/DemaConsulting.CanvasNet.Scad/Language/`) turns OpenSCAD-language source text into an immutable
syntax tree or a precisely located error. It is **internal**: the lexer, the token types, the parser
and the syntax tree are visible only to the `ScadDocument` unit and, through `InternalsVisibleTo`, to
the test assembly, so the tree can evolve while evaluation and rendering are added in later phases. It
contains two units:

- **ScadLexer** — the internal `ScadLexer` (partial class), `ScadToken`, and `ScadTokenKind`; see
  _ScadLexer Unit Design_ (`scad-language/scad-lexer.md`)
- **ScadParser** — the internal `ScadParser` (partial class), the syntax tree records
  (`ScadSyntax.cs`, `ScadStatements.cs`, `ScadExpressions.cs`), and `ScadTreeDepth`; see
  _ScadParser Unit Design_ (`scad-language/scad-parser.md`)

The subsystem's namespace documentation is in `Language/NamespaceDoc.cs`. Its behavior is specified by
the `CanvasNetScad-ScadLanguage-*` requirements in `docs/reqstream/canvas-net-scad/scad-language.yaml`.

### Interfaces

**Exposed** (internal only): `ScadParser.Parse(string source, string? file)`, which tokenizes and
parses in one call, and `ScadParser.Parse(IReadOnlyList<ScadToken> tokens)`, which parses an already
tokenized source; `ScadLexer.Tokenize(string source, string? file)`; the limit constants
`ScadParser.MaxNestingDepth` (256) and `ScadParser.MaxSourceLength` (4,194,304) and the factory
`ScadParser.CreateSourceTooLongException()`; and the syntax tree rooted at `ScadProgram`. Nothing in
this subsystem is public.

**Consumed**: the `ScadSourceLocation` and `ScadSyntaxException` types and the `ScadDirectiveKind`
enum, which are defined in the `ScadDocument` unit's public surface and shared with the internal
syntax tree (a `ScadDirectiveStatement` carries a `ScadDirectiveKind`). The .NET base class library
supplies `System.Globalization`, `System.Text`, and `System.Runtime.CompilerServices`.

**Consumer**: only `ScadDocument`, which calls `ScadParser.Parse(string, string?)` and reads the
resulting `ScadProgram`. See _ScadDocument Unit Design_ (`scad-document.md`).

### Design

**Tokenize, then parse.** The subsystem is a classic two-stage front end. `ScadLexer` converts the
complete source text to a flat list of `ScadToken` values ending with exactly one end-of-file token;
`ScadParser` then walks that list by index, with statements parsed by recursive descent and expressions
by precedence climbing. The separation lets the parser look ahead over a plain list (for example to
tell `name = …` from `name(…)` with one token of lookahead) and keeps character-level concerns
(comments, escapes, number forms, locations) out of the grammar. The parser is name-agnostic: every
`name(args) children` is a module instantiation, so built-in, unknown, and unsupported module names
never cause a syntax error.

**Consequence: lexical errors come first.** Because the whole source is tokenized before any parsing
starts, a lexical error anywhere in the file is reported before any grammatical error, even when the
grammatical error appears earlier in the text. For example, a missing semicolon on line 3 together with
an unterminated string on line 40 reports the string. This is a documented property (also stated in the
`ScadSyntaxException` remarks) and is pinned by the test
`ScadLanguage_Parse_LexicalErrorAfterGrammarError_IsReportedFirst`. Within each stage the first error
in source order wins.

**Fail-fast, single error.** Neither stage attempts recovery. The first error throws a
`ScadSyntaxException` whose `Location` points at the offending token (or, for lexical errors, at the
character or construct that failed), so a caller always receives exactly one precise diagnostic.

**Resource limits (fail closed).** Hostile input must not exhaust memory or the stack, and it must
leave later recursive consumers of the tree safe. Three limits apply:

- **Source length** — `ScadParser.Parse(string, string?)` rejects source longer than
  `MaxSourceLength` (4,194,304 UTF-16 characters) before any lexing, with a plain
  `InvalidDataException` (no location is meaningful). `ScadDocument` applies the same limit while
  reading streams and files.
- **Parser recursion** — a depth counter (`Enter`/`Leave`) limits how deeply the parser itself nests
  (statements, expressions, prefix operators, exponent chains, comprehension elements, and unbraced
  single children) to `MaxNestingDepth` (256). Parsing runs inline on the caller's thread, guarded by
  `RuntimeHelpers.EnsureSufficientExecutionStack`; only when that guard trips (the caller's stack is too
  small, never the nominal depth counter) is the parse restarted on a dedicated thread with a 16 MiB stack
  sized for the worst case at that limit, with the number of such fallback threads bounded by a semaphore.
  The nominal limit is therefore honored on every platform regardless of the caller's stack. On the
  fallback thread a tripped guard is converted to the same `Nesting too deep (limit 256)` error.
  The lexer is iterative and needs no such guard. Any later recursive component, notably the Phase 5
  evaluator, must use the same approach (deterministic counter, inline attempt, bounded sized-stack
  fallback, or an explicit stack) for its own recursion limits.
- **Tree height** — left-associative operator chains and postfix chains are built in loops without
  parser recursion, so a hostile `1+1+1+…` yields an arbitrarily deep left-leaning tree. After a
  successful parse, `ScadTreeDepth.Verify` measures the finished tree iteratively (explicit stack) and
  rejects any node deeper than 256 (program node at depth 0), with the same message located at the
  offending node. The two parts are complementary: the counter protects the parser's own stack, and the
  height check guarantees that every later recursive consumer sees a bounded tree. Details are in
  _ScadParser Unit Design_.

**Immutability and concurrency.** Tokens and syntax nodes are immutable (record structs and records
with read-only collections), and the static entry points create a new single-use lexer or parser
instance per call, so they are safe for concurrent use.

**Clean-room provenance.** Both units are original implementations written from general
language-design knowledge; no OpenSCAD source code, grammar, manual text, or test material was
consulted. Exact parity with any other implementation is a non-goal.
