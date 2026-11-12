### GitGraphParser

![MermaidGitGraph Structure](MermaidGitGraphView.svg)

The `GitGraphParser` unit is the first software unit in the `GitGraph` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.Mermaid.GitGraph` namespace: `GitGraphParser.cs` (the entry point, the grammar, the value
converters, the label limit, and the error machinery, with the enum `GitStatementKind`, the sealed record
`GitStatement`, and the sealed class `GitGraphDiagram`), `GitGraphParser.Lexer.cs` (the readonly record struct
`GitGraphToken`, the token kinds, and the lexer rules and matchers), and `GitGraphParser.Database.cs` (the delegate
`GitGraphFailure`, the sealed records `GitCommit` and `GitBranch`, and the sealed class `GitGraphDatabase`).
`GitGraphParser` is an `internal sealed partial` class, so its two files are one type; `GitGraphDiagram` implements the
system's `IMermaidDiagram`. All three files carry the provenance header comment for the parts of Mermaid,
`@mermaid-js/parser`, and Langium they port.

#### Purpose

`GitGraphParser` reads the preprocessed text of a git graph into a `GitGraphDiagram`, accepting exactly the language of
the Langium grammar of `@mermaid-js/parser` (`gitGraph.langium` with `common.langium`), converting the values as its
value converters do, and running the statements against `GitGraphDatabase`, the port of Mermaid's git graph database
(`gitGraphAst.ts` with the `populate` of `gitGraphParser.ts`, `populateCommonDb.ts`, and `commonDb.ts`).
`GitGraphDiagram` is the parsed diagram: the statements, the database of the text's own main branch, the title and
accessibility texts, the metadata of the document, and the database of a render's configuration (`Database`). The unit
exists so that every text means what it means to Mermaid, so that an invalid text fails with Mermaid's diagnosis at the
right place in the original file, so that the commits, branches, and generated ids are Mermaid's, and so that reading
untrusted text costs time linear in its length.

The unit's behavior is specified by the `CanvasNetMermaid-GitGraph-GitGraphParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/git-graph/git-graph-parser.yaml`.

#### Data Model

**The token kinds** are constants whose values are the names of the Langium tokens, so that messages and
expected-token lists read like Mermaid's parse errors: the keywords `gitGraph`, `gitGraph:`, `commit`, `branch`,
`merge`, `checkout`, `switch`, `cherry-pick`, `id:`, `msg:`, `tag:`, `type:`, `order:`, `parent:`, `NORMAL`,
`REVERSE`, `HIGHLIGHT`, `LR`, `TB`, `BT`, and `:`; the terminals `ACC_DESCR`, `ACC_TITLE`, `TITLE`, `INT`, `STRING`,
`NEWLINE`, and `REFERENCE`; and `EOF`, the end of the tokens (chevrotain's `EOF`), which the lexer adds once at the end
of the text. **`GitGraphToken`** is a token: its kind and the start and end of its text in the parser text.

**The lexer rules** are the token types in the order Langium gives them to chevrotain, each with a hand-written matcher
in place of its regular expression; the first that matches at a position wins, as in chevrotain:

- **The keywords**, longest first and stably: `cherry-pick`, `gitGraph:`, `HIGHLIGHT`, `gitGraph`, `checkout`,
  `REVERSE`, `parent:`, `commit`, `NORMAL`, `branch`, `order:`, `switch`, `type:`, `merge`, `msg:`, `tag:`, `id:`,
  `LR`, `TB`, `BT`, and `:`. The token builder of `@mermaid-js/parser` gives only `gitGraph` the suffix
  `(?:(?=%%)|(?!\S))`, so `gitGraph` matches only when it is followed by `%%`, by white space, or by the end of the
  text. A keyword that a `REFERENCE` can start gives way to a `REFERENCE` that matches strictly more characters there
  (chevrotain's longer alternative, which Langium's `findLongerAlt` gives it): `commits`, `cherry-picked`, `LRx`, and
  `commit2` are names, while `commit`, `LR`, and `NORMAL` stay keywords.
- **The terminals in the order of the grammar**: `ACC_DESCR` (a colon and the rest of the line before any `%%`, or the
  text in braces up to the first `}`); `ACC_TITLE` (a colon and the rest of the line before any `%%`); `TITLE` (`title`
  followed by a tab or a space and the rest of the line before any `%%`, or `title` alone); `INT`
  (`0|[1-9][0-9]*(?!\.)`, solved exactly with the backtracking of the expression, so `02` is the number `0` followed by
  the number `2`, and the digits before a dot give back their last digit); `STRING` (a double- or single-quoted text in
  which a backslash escapes any character but a line terminator); and `NEWLINE` (`\r?\n`). The terminals `BOOLEAN`,
  `FLOAT`, `NUMBER`, and `ID` of `common.langium` are not reached by the grammar and are not in the lexer.
- **The hidden terminals**, which the parser never sees: white space (tabs and spaces), a `---` YAML block, a `%%{...}%%`
  directive, and a `%%` comment up to the end of its line.
- **`REFERENCE` last** (`\w([-\./\w]*[-\w])?` with ASCII word characters, given back to its last character that is not
  a dot or a slash), as Langium orders the terminals of the git graph grammar after the hidden ones of
  `common.langium`. So `TITLE` and `INT` come first: `branch titles` reads a `TITLE`, and `branch 10` reads an `INT`.

**A parser instance** parses one text on one thread and holds the parser text of the source (a `MappedText`, whose
characters stay mapped to the original text), the `MermaidLimits` of the open call, the list of tokens, and the index of
the lookahead token.

**`GitStatement`** is a statement as written: its `GitStatementKind` (`Commit`, `Branch`, `Merge`, `Checkout` for both
`checkout` and `switch`, and `CherryPick`), the location of its keyword, and its options as the converters give them,
not yet sanitized: the `id:`, the message, the branch name, the `order:` (a double, as JavaScript's `parseInt` gives
it), the `type:` (Mermaid's 0, 1, or 2), the `parent:`, and the tags in order.

**`GitCommit`** is an entry of Mermaid's commit map: its id (sanitized, or generated), its message (sanitized, or
composed for a merge or a cherry-pick), its sequence number, Mermaid's type (0 normal, 1 reverse, 2 highlight, 3 merge,
4 cherry-pick), its tags, the ids of its parents, the branch it was made on, and the location of its statement; a merge
also has the `type:` it is drawn with (`CustomType`) and whether its id was given (`CustomId`, which decides whether its
label is drawn). **`GitBranch`** is an entry of Mermaid's `branchConfig`: its name, its order (0 when the statement
gives none; the main branch's from the configuration), and the location of its statement (the diagram keyword for the
main branch).

**`GitGraphDatabase`** is the state of Mermaid's database after one run over the statements for one main branch name
and order: the commits in the order of Mermaid's commit map (a list with an index of the ids, so that a commit whose id
is already used replaces the earlier one in its place), the branches in the order they were made with the head commit
of each, the main branch name and order, the current branch, the head commit, the next sequence number, the state of
the id sequence (mulberry32 seeded with `0x2545F491`), and the count of the branches and commits made for
`MaxNodes`. `BranchOrder` gives the branches in the order Mermaid draws them (`getBranchesAsObjArray`). A database is
never changed after its run and is safe for concurrent use. **`GitGraphFailure`** creates the exception of a failed
operation: a `MermaidSyntaxException` when the text is opened, a `MermaidRenderException` when a render runs the
statements again.

**`GitGraphDiagram`** is immutable and safe for concurrent renders. It holds the location of the diagram keyword
(`KeywordLocation`), the direction (`LR` when the header gives none), the sanitized title of the `title` statement
(`DiagramTitle`), whether a `title` statement with a value set it (`IsTitleSet`), the sanitized accessible title
(`AccessibleTitle`) and description (`AccessibleDescription`), the sanitized frontmatter title (`FrontmatterTitle`),
each empty when not set, the statements in order (`Statements`), the limits of the open call, and the database of the
text's own main branch (`OpenDatabase`). `DrawnTitle` is the title Mermaid draws: the diagram's title when
`IsTitleSet`, even one that sanitizes to empty, else the frontmatter title.

#### Key Methods

- **`Parse(PreprocessedSource, DetectedDiagram, MermaidLimits, MermaidConfig)`** creates a parser over the parser text
  of the source and runs it with the location of the diagram keyword, the frontmatter, and the `GitGraphConfig` of the
  configuration of the text alone; it returns the `GitGraphDiagram`.
- **`Tokenize`** splits the parser text into tokens, trying the keywords and then the terminals in order at each
  position, keeping the tokens of the kinds the grammar reads and skipping the hidden ones, and ending the list with one
  `EOF` token. A position where no rule matches is a lexical error. Every matcher looks at most to the end of its line,
  its closing quote or brace, or the end of its YAML block or directive; the last closing that can end a directive is
  found once, before the tokens, so the tokens are found in time linear in the text.
- **`Run`** reads the grammar
  `NEWLINE* header (NEWLINE | TitleAndAccessibilities | Statement)* EOF` with one token of lookahead, in a loop. The
  header is `gitGraph:`, `gitGraph :`, `gitGraph` followed by a direction and a colon, or `gitGraph` alone; a direction
  without its colon leaves the header alone, so the direction token then ends the diagram with the error that the end
  of the text was expected, as the ALL(*) prediction of Mermaid's parser decides. The title, accessible title, and
  accessible description statements are converted as they are read, and the last of each kind wins. After
  the statements the lookahead must be the end of the tokens. Then it sanitizes and checks the title and accessibility
  texts and the frontmatter title, and runs the statements with **`GitGraphDatabase.Run`** with the text's
  `mainBranchName` and `mainBranchOrder`, its failures created as `MermaidSyntaxException`s.
- **`Statement`** reads one statement: `commit` with `id:`, `msg:` (or a bare string), `tag:`, and `type:` options in
  any order; `branch` with a name (a `REFERENCE` as written or a converted `STRING`) and an optional `order:`; `merge`
  with a name and `id:`, `tag:`, and `type:` options; `checkout` or `switch` with a name; and `cherry-pick` with `id:`,
  `tag:`, and `parent:` options; each ended by **`EndOfLine`** (`NEWLINE+` or the end of the tokens). A repeated option
  keeps its last value, and tags add up. **`StringValue`**, **`NameValue`**, **`TypeValue`**, and **`IntValue`** read
  and convert the values (`IntValue` is Langium's integer conversion, JavaScript's `parseInt`: the nearest double of
  the digits).
- **`ConvertString`** is Langium's `convertString`: the quotes removed, the escapes `\b`, `\f`, `\n`, `\r`, `\t`, `\v`,
  and `\0` replaced, and any other escaped character kept without its backslash.
- **`ConvertSingleLine`** and **`ConvertMultiLine`** convert a title, an accessible title, or an accessible description
  as the common value converter of `@mermaid-js/parser` does (the one-line form trimmed as JavaScript's `trim` trims it,
  each run of two or more tabs and spaces made one space; the lines of the braced form trimmed and its blank lines
  removed). **`ConvertAccDescr`** chooses the one-line or the braced form.
- **`GitGraphDatabase.Run`** creates an empty database (the main branch, checked out, without commits) and runs each
  statement in order:
  - **`Commit`** sanitizes the id, the message, and the tags, counts the commit against `MaxNodes`, and adds a commit
    whose parent is the head, with the given id or a generated one.
  - **`Branch`** sanitizes the name, refuses an existing branch, counts the branch against `MaxNodes`, makes it at the
    head with its order (0 by default), and checks it out.
  - **`Checkout`** sanitizes the name, refuses a branch that does not exist, and makes it the current branch with its
    head.
  - **`Merge`** sanitizes the other branch and the id (the tags are kept as written, as in Mermaid), checks in
    Mermaid's order that the current head was not made on the other branch, that the branch is not merged into itself,
    that the current branch has a commit, that the other branch exists and has a commit, that the heads differ, and
    that the id is not used yet, and adds a merge commit with the message `merged branch {other} into {current}` and
    the two heads as parents.
  - **`CherryPick`** sanitizes the source id, the tags, and the parent, checks in Mermaid's order that the source
    exists, that a given parent is a parent of the source, that a merge is picked with a parent, that the source is not
    on the current branch, and that the current branch has a commit, and adds a cherry-pick commit with a generated id,
    the message `cherry-picked {message} into {current}`, the head and the source as parents, and the given tags (empty
    ones left out) or the default tag `cherry-pick:{id}` (with `|parent:{parent}` for a merge).
  - **`Add`** puts a commit into the commit map, in the place of a commit with the same id, and makes it the head of
    the current branch. Each composed message and tag is checked against `MaxLabelLength`.
- **`GitGraphDatabase.Sanitize`** is Mermaid's `sanitizeText` under the git graph configuration, which has no security
  level, so `sanitizeMore` takes its escape path: an empty text is kept; otherwise each `<br>` tag is kept as `<br/>`
  while `<`, `>`, and `=` are escaped as `&lt;`, `&gt;`, and `&equals;`, and the result is cleaned with `HtmlSanitizer`.
- **`RandomHex`** and **`NewId`** generate an id: the sequence number, a dash, and seven hex digits, each digit the top
  four bits of the next 32-bit output of the mulberry32 sequence (Mermaid's `Math.floor(Math.random() * 16)` with
  `Math.random` the same sequence).
- **`GitGraphDatabase.BranchOrder`** sorts the branches by their order, stably, as JavaScript's sort does in
  `getBranchesAsObjArray`.
- **`GitGraphDiagram.Database(GitGraphConfig)`** returns `OpenDatabase` when the render's `mainBranchName` and
  `mainBranchOrder` are those of the text, else runs the statements again against a new database with the limits of the
  open call and failures created as `MermaidRenderException`s (a rejected operation as `mermaid-render-failed`).
- **`GitGraphDiagram.SanitizeAccTitle`** and **`SanitizeDescription`** are the setters of Mermaid's `commonDb.ts`: the
  text sanitized with `HtmlSanitizer`, the accessible title without its leading white space, and the description
  without the white space after each line feed.
- **Metadata** (`IMermaidDiagram`): `Title` is the title of the `title` statement or null (the document then falls back
  to the frontmatter title unless `IsTitleSet`, which the diagram exposes through `IMermaidDiagram`), `AccTitle` the
  accessible title or null, `Direction` the direction, `NodeCount` the commits of `OpenDatabase`, `EdgeCount` the
  number of their parents, and `SubgraphCount` 0.
- **`GitGraphDiagram.Layout(DiagramRenderContext)`** creates a `GitGraphRenderer` for the render and returns the scene
  it draws (see _GitGraphRenderer Unit Design_).
- **`Is`**, **`Expect`**, **`Fail`**, **`TextOf`**, **`Checked`**, **`CheckDerived`**, and **`Locate`**: `Is` tests the
  lookahead, `Expect` consumes a token of a kind or fails, `Fail` builds the located exception for the lookahead with
  the token names chevrotain lists and a hint, `TextOf` gives the text of a token that is kept and checks it,
  `Checked` and `CheckDerived` check a converted or derived text against `MaxLabelLength`, and `Locate` maps a token to
  the original text.

#### Error Handling

- A token the grammar does not accept is a `MermaidSyntaxException` with code `mermaid-syntax-error`, located at the
  token in the original text, with the message `expected 'A' or 'B', found 'KIND' ('text')` (or `found the end of the
  text`), the expected token names as `Expected` (the names chevrotain lists: `gitGraph` and `gitGraph:` before the
  header; `STRING` after an option keyword; `REFERENCE` and `STRING` for a branch name; `NORMAL`, `REVERSE`, and
  `HIGHLIGHT` after `type:`; `INT` after `order:`; `NEWLINE` and `EOF` after a statement; and `EOF` where no statement
  can start), and a hint.
- Text that no token matches is a lexical error, raised by `Tokenize` before the grammar runs, so it comes before any
  grammar error, as Mermaid lists the errors of its lexer first: a `MermaidSyntaxException` with code
  `mermaid-syntax-error`, located at the first character that is not matched, with the message `unrecognized text ...`,
  no expected tokens, and a hint. Such text is, for example, a branch name with a character outside ASCII, a negative
  order, or an unclosed string.
- An operation the database rejects is refused when the statements run, after the whole text has been read (so a
  grammar error anywhere in the text comes first, as in Mermaid), with a `MermaidSyntaxException` with code
  `mermaid-syntax-error`, located at the statement, with Mermaid's message and a hint; the fourteen messages of
  Mermaid's database are kept word for word (for example `Trying to checkout branch which is not yet created. (Help try
  using "branch dev")` and `Incorrect usage of "merge". Both branches have same head`).
- More branches and commits than `MaxNodes` (the main branch counted) are refused at the statement one too many, with
  the message `the diagram has more than N commits (MermaidLimits.MaxNodes, the main branch, branches and commits)`; a
  text longer than `MaxLabelLength` is refused with the message `a text is N characters long, more than the limit of M
  (MermaidLimits.MaxLabelLength)`; both with code `mermaid-limit-exceeded` and a hint. The parser checks the text of
  every token it keeps, every converted value (located at its token), every sanitized id, message, name, tag, and
  parent, every composed message, default tag, and generated id (located at the statement), every sanitized title and
  accessibility text (located at the diagram keyword), and the sanitized frontmatter title (located at the
  frontmatter).
- `Database` throws a `MermaidRenderException` located at the statement, with code `mermaid-render-failed` and
  Mermaid's message when the database rejects an operation under the render's main branch, and with code
  `mermaid-limit-exceeded` when a limit is exceeded in that run; both propagate.
- `Parse` throws `ArgumentNullException` for a null argument, and so do `Database` and `Layout`.

#### Dependencies

- **GitGraphRenderer** - created by `GitGraphDiagram.Layout` to draw a render.
- **Syntax subsystem** - `PreprocessedSource`, `MappedText`, and `FrontmatterData` (the text mapped to the original and
  the frontmatter title), `DetectedDiagram`, `Preprocessor.TrimJs` (JavaScript's `trim`), `ScanPatterns` (the
  JavaScript white space and line terminator classes), `HtmlSanitizer` (DOMPurify's cleaning of a sanitized text),
  `SourceScanner.Describe` (the description of unmatched text), and `TokenCursor.JoinAlternatives` (the list of
  expected tokens in a message).
- **Configuration subsystem** - `MermaidConfig` and its `GitGraphConfig` section (the main branch name and order).
- **Rendering subsystem** - `LabelBuilder.MatchBreakTag` (the `<br>` tags that the sanitizer keeps).
- **Scenegraph subsystem** - `Scene`, the result of a layout.
- **System** - `IMermaidDiagram`, `DiagramRenderContext`, `MermaidLimits`, `MermaidLocation`, `MermaidSyntaxException`,
  `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- The document pipeline of the system (`MermaidDocument.Pipeline.cs`): its `DiagramRegistry` calls `Parse` for a text
  whose detected type is `gitGraph`, with the limits of the open call and the configuration of the text alone, reads
  the metadata of the `GitGraphDiagram`, and calls its `Layout` for every measure and render.
- **GitGraphRenderer** - calls `Database` with the `GitGraphConfig` of the render and reads the commits, branches, and
  branch order of the database and `Direction`, `DrawnTitle`, and `KeywordLocation` of the diagram.
- The unit and subsystem tests call `Parse`, `Database`, and the converters directly, and the vector tests compare the
  state of the database with Mermaid's.

#### Known Differences From Mermaid

- **No regular expressions.** The lexer is a set of hand-written matchers with the same matches as Langium's regular
  expressions, so lexing is linear in the text.
- **A hand-written parser instead of chevrotain's.** The parser accepts the same language with one token of lookahead
  and the header outcomes of the ALL(*) prediction of Mermaid's parser, and lists the expected tokens that chevrotain
  lists (the first token of each alternative); the grammar errors of the upstream vectors give the identical lists at
  the identical places.
- **The first error only, located.** Mermaid's message lists every error of its lexer and then every error of its
  parser; this unit reports the first of them, located by line and column in the original text, with a hint.
- **Generated ids.** Mermaid draws the digits of a generated id from `Math.random`; this unit draws them from the
  mulberry32 sequence with a fixed seed, restarted for every run of the database, so the ids are the same in every
  render (and equal to those of the seeded reference renders).
- **The text's own main branch at open.** The statements run when the text is opened, with the main branch name and
  order of the text's own configuration; a site configuration that names another main branch runs them again at
  render. So a text that relies on a main branch name that only the site configuration gives (`checkout trunk` with
  `mainBranchName: trunk` only in the site configuration) fails when it is opened, with Mermaid's message for a branch
  that does not exist, where Mermaid draws it.
- **Duplicate commit ids.** A commit whose id is already used replaces the earlier commit in its place, as in Mermaid;
  Mermaid logs a warning, and this unit reports nothing.
- **Limits.** The node and label limits refuse the text when the document is opened (and a render's run of the
  statements again), with code `mermaid-limit-exceeded`; Mermaid has no such limits.
- **Database errors are located syntax errors.** Mermaid's database throws plain errors with the same messages; this
  unit raises them as located `MermaidSyntaxException`s with hints, and as located `MermaidRenderException`s when a
  render's configuration runs the statements again.
- **Accessibility.** `accTitle` and `accDescr` are metadata of the diagram only, since a rendered picture has no SVG
  to carry them.
