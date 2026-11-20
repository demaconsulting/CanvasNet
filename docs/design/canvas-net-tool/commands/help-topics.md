### HelpTopics

![Commands Structure](CommandsView.svg)

#### Purpose

`HelpTopics` is the tree of help cards that `canvasnet help <topic> [<card> ...]` prints. A card is a short Markdown
text about one subject. The seventeen root cards (the help topics) are one per format the tool reads (`scad`,
`vega-lite`, `mermaid`, `plantuml`, `svg`, `pdf`, `pptx`, `vsdx`, and `raster`), the two guidance hubs (`style` and
`composition`), the feature-positions topic (`positions`), the cross-cutting subjects (`json`, `errors`, `limits`, and
`files`), and the Pandoc filter (`pandoc`, alias `filter`). A large topic is split into cards below its root, for
example `mermaid gantt` and `mermaid gantt settings`, so an agent reads only the part it needs.

The knowledge about the drawing and file formats is not owned here: it is the help catalog of the Viewer library
(`HelpCatalog.Default`, see *Help Subsystem Design* and *HelpCatalog Unit Design* of the `CanvasNetViewer` system),
which describes the formats in library terms so that any program that uses the library gets the same help. This unit
is the **command-line layer** on that catalog. Its single responsibility is to:

- add the tool's own cards (the `json` tree of the result documents and the `pandoc` card of the Pandoc filter);
- add the command-line sections of library cards as overlays (the flags, the `kind: command` examples, shell quoting,
  and the `canvasnet` commands that render, inspect, compare, montage, and flatten);
- keep the topics in their fixed order and resolve a card by its path, with the usage errors of the tool;
- write every link of a card as the `canvasnet help` command that prints its target;
- render a card, the tree, and the examples as plain text or as the help JSON documents of the tool.

The dispatch of the `help` command and the index of commands and topics belong to `CommandRegistry` (the file
`CommandRegistry.Help.cs`), which calls this unit.

The unit is the internal static partial class `HelpTopics` in two files: `HelpTopics.cs` (the command-line card
record, the combined catalog, the link writer, and the lookup) and `HelpTopics.Render.cs` (the text and JSON
renderings and the wrapping). Its content is embedded in the tool assembly.

#### Data Model

**HelpCard** (`internal sealed record`, in `HelpTopics.cs`): the card as the command line shows it: `Id`, `Aliases`,
`Tags`, `Title`, `Summary`, `Body` (the library card's body followed by the overlay's, with the tokens replaced and
every link written by `FormatLink`), `ChildIds`, `SeeAlso`, and `Examples` (the library's `HelpExample` values, then the
overlay's). It derives `Name`, `RootId`, `ParentId`, `Depth`, `Path` (the identifier with spaces, for example
`mermaid gantt`), and `Command` (`canvasnet help mermaid gantt`). The library's `HelpCard` type is imported under the
alias `LibraryCard`.

**TopicNames**: the root card identifiers in the order they are listed: `scad`, `vega-lite`, `mermaid`, `plantuml`,
`svg`, `pdf`, `pptx`, `vsdx`, `raster`, `style`, `composition`, `positions`, `json`, `errors`, `limits`, `files`, and
`pandoc`. The loader requires the roots of the combined catalog to be exactly this set, so the tool keeps its own order
and lists its topics among the library's. **FormatTopicNames** is the first nine, the topics that describe one format.

**Tokens**: the tool's own `{{name}}` tokens, passed to `HelpCatalog.Extend`: `schemaVersion`
(`ResultWriter.SchemaVersion`). Every other token (the limits, `maxDpi`, the qualities) belongs to the library, which
fills it in from the constants of the code; an extension may not redefine a library token.

**Catalog**: a `Lazy<CardCatalog>` that, on first use, extends `HelpCatalog.Default` with the tool's resources, checks
the roots against `TopicNames`, converts every card of the combined catalog to the command-line `HelpCard`, and lists
them depth first from the roots in `TopicNames` order with a dictionary by identifier. The catalog is read once and
never changes afterwards, so it is safe to share between threads.

**Embedded resources**: the tool project embeds its card files with two `EmbeddedResource` items with
`WithCulture="false"`:

| Source file (under `Commands/Help/`) | Logical resource name |
| --- | --- |
| `<path>.md`, in folders that mirror the tree | `DemaConsulting.CanvasNet.Tool.Help/<path>.md` |
| `Examples/<root>-<file name>` | `DemaConsulting.CanvasNet.Tool.Help.Examples.<root>-<file name>` |

The prefixes are the constants `ResourcePrefix`, `CardResourcePrefix`, and `ExampleResourcePrefix`, and `CardExtension`
is `.md`. The tool currently ships no example files of its own: its examples are command lines in the front matter.

**Tool card files** (`Commands/Help/`): each file is one of two kinds, in the card file format of the library (see
*HelpCatalog Unit Design*):

- **A tool card**: `json.md`, `json/results.md`, `json/diagnostics.md`, `json/help.md`, and `pandoc.md`, with `title`,
  `summary`, `tags` (from the library's closed vocabulary), and the other keys of a card. Literal `canvasnet` text is
  allowed in these cards.
- **An overlay**: a file at the path of a library card whose front matter starts with `extends: <path>` (the same path).
  It may give `title` and `summary` (which replace the library card's on the command line, so the CLI index can keep its
  wording), `see-also` (appended without duplicates), and `examples` (appended; the `kind: command` examples live here).
  Its body starts with `## Command line` and holds the flags, shell blocks, and commands of the subject (`files` and
  `positions/selectors` add one more section after it, `## The Pandoc filter` and `## Quoting`). 58 library cards have
  an overlay.

**Links**: the bodies use the library's neutral links (`[[a/b]]`, `[[a/b#name]]`, `[[#name]]`), validated by the library
against the combined tree. `FormatLink` writes them for the command line.

**Size budget** (a unit test): the rendered text of a card, with its command-line section, is at most 7,000 characters,
and at most 4,000 for a root card that has cards below it; a summary is at most 60 characters for a root card and 100
for any other card (the library budget of its own cards is 6,000 and 3,500). **TextWidth** is 78.

**Help JSON documents**: schema version 2 (`ResultWriter.SchemaVersion`). The `tags` of a card were added to the `card`
and `topics` documents as an added key, so the version did not change.

#### Key Methods

**LoadAll** (private): `HelpCatalog.Default.Extend(typeof(HelpTopics).Assembly, ResourcePrefix, Tokens)` (the library
parses, validates, and merges the tool's cards and overlays: an overlay of an unknown card, a tool card in the tree of a
library card, a link that resolves nowhere, or a malformed front matter fails with the file and the line), then requires
the roots to be `TopicNames`, and converts each card with `ToToolCard` (its body from
`catalog.FormatMarkdown(card, FormatLink)`).

**FormatLink(link)** (`internal`): writes a library `HelpLink` as the command that prints its target:
`` `canvasnet help a b` `` for a card, `` `canvasnet help a b --example name` `` for an example of a card, and
`` `--example name` `` for an example of the card being read (`[[#name]]`).

**All**, **Roots**, **Get(id)**, **ChildrenOf(card)**, and **Subtree(card)**:

- `All` returns every card, depth first.
- `Roots` returns the root cards in `TopicNames` order.
- `Get(id)` returns a card by identifier.
- `ChildrenOf(card)` returns the cards below a card, in order.
- `Subtree(card)` returns a card followed by every card below it.

A malformed card makes the first access throw `InvalidOperationException`, and because the catalog is a `Lazy`, again
on later accesses.

**Find(nameOrAlias)**: returns the root card whose name or one of whose aliases equals the argument ignoring case, or
`null`. A unit test proves that no root name or alias equals a command, a command alias, `help`, or `topics`, so a
subject is never ambiguous.

**Resolve(path)**: splits every word of the path at `/` (`SplitPath`), finds the root with `Find`, and then each
further word among the children of the previous card by name or alias, ignoring case. It returns `null` when the first
word is not a root; an unknown further word is the `ToolException` described under *Error Handling*.

**SourceExamples()**, **Snippets()**, **AllExamples()**, and **SubtreeExamples(card)**: enumerate the examples with
their cards. **CardsBelowWithExamples(card)** enumerates the cards below a card that have examples of their own, depth
first. The unit tests and the `CanvasNetTool_HelpExamples` self-test render every source example with `--strict`.

**RenderText(card)**: the plain-text card, without a trailing newline, in this order:

1. `# <title>`, a blank line, and the summary.
2. `Also known as: <aliases>`, when the card has aliases.
3. The body as written (the library body, then the overlay's), after token substitution and with the links written
   as commands; the body is not rewrapped and no Markdown is converted.
4. `## Cards`: for each card below it, a bullet with its exact command and, indented, its summary, followed by a pointer
   to `--examples` (`Find the cards below that have examples with: <command> --examples`) when cards below it have
   examples.
5. `## Examples`: one aligned line per own example (name, kind in brackets, and description), followed by
   `Print them in full with: <command> --examples, or one alone with: <command> --example <name>.`
6. `## See also`: each link as a bullet with the exact command and, indented, the summary of the target.

Each footer section appears only when it is not empty, and the generated lines are wrapped by `AppendWrapped`. The
text is deterministic: no ANSI escape sequence, no detection of the console width, and LF line endings whatever the
operating system.

**RenderTopicsText()** and **RenderTopicsJson()**: the whole tree, depth first. The text has one line per card: the
help path indented by depth, padded to a column, and the wrapped summary.

**AppendWrapped(builder, text, firstPrefix, restPrefix)**: splits the text into words (single spaces), starts the first
line with `firstPrefix` and every further line with `restPrefix`, and starts a new line when the next word would pass
`TextWidth`. A word longer than the free width is never split: it sits on a line of its own. `CommandRegistry` also
uses it for the topic list of the index.

**RenderExamples(card)**: what `help <path> --examples` prints, LF only and without a trailing newline: the heading
`# <title>: examples`; for each own example of the card, a section `## <name> [<kind>]` (followed by the file name for
a source or a snippet), the wrapped description, and the exact content in a code fence of its language; then
`Print one alone (to save it to a file) with: <command> --example <name>`. A card without examples of its own prints
`'<path>' has no examples of its own.` instead of the sections. When cards below it have examples, a section
`## Cards below with examples` follows, with a bullet `<command> --examples` per card and, indented, its example names.
Only the card's own examples are printed in full, which keeps the output of a topic short; the cards below are
pointers. The generated prose is wrapped by `AppendWrapped`; the code is never wrapped.

**CodeFence(content)** (`internal`): the fence of an example: at least three backticks, and one more than the longest
run of backticks in the content, so the content cannot end the block early.

**RenderExample(card, name)**: the exact content of one example of the card or of a card below it, found by name
ignoring case. The content is what `help <path> --example <name>` prints, and nothing else.

**RenderJson(card)**, **RenderExamplesJson(card)**, and **RenderExampleJson(card, name)**: these JSON documents, and
`RenderTopicsJson()`, are written with `ResultWriter.Success("help", ...)`. They have the common envelope
(`schemaVersion`, `ok`, `command`), one line, the key order below, and ASCII-only escaping.

- `kind` `card`: `id`, `aliases`, `tags`, `title`, `summary`, `body`, `children` and `seeAlso` (each entry with `id`,
  `summary`, and `command`), and `examples` (each with `name`, `kind`, `language`, `fileName` for a file,
  `description`, and `content`).
- `kind` `topics`: `cards`, each with `id`, `aliases`, `tags`, `summary`, and `command`.
- `kind` `examples`: `id`, `examples` (the card's own examples, each as in the `card` document, with `content` and
  without a `card` key), and `cards` (the cards below it that have examples, each with `id`, `summary`, `command`, and
  `examples`, the names).
- `kind` `example`: `id` and `example` (with `card` and `content`).

These documents replaced the `topic` document of schema version 1, which is why `ResultWriter.SchemaVersion` is 2.
Version 2 has not been released, so the change of the `examples` document (the card's own examples in full, plus
`cards`, instead of a list of the subtree without contents) is part of version 2 and needs no further bump.

**WriteStrings(writer, name, values)** (`internal`): writes a JSON array of strings; `CommandRegistry` uses it for the
alias lists of its own documents.

#### Overlays of the Library Cards

Every library card whose subject has a command-line side has an overlay. The library card keeps the knowledge in library
terms (for example strict mode, the requested width, the file-access policy, `ViewerImage.Anchors`, `SvgFlattener`), and
the overlay's `## Command line` section maps it to the tool:

- the `options` cards of each format and `svg/options`, `pdf`, `pptx`, `vsdx`, and `raster`: every flag
  (`--width`, `--size`, `--scale`, `--background`, `--page`, `--dpi`, `--strict`, `--file-access`, `--allow-root`, and
  the format's own options such as `--theme`, `--param`, `-D`, and the view options of a model) and the command
  examples;
- the root card of each drawing format: the `render` command with `--json`;
- the composition, style, framed-figure, anchors, arrows, and bindings cards: the commands that render, inspect a pixel,
  read the positions, draw a grid, make a montage or a comparison, and flatten, with shell blocks and their quoting;
- `svg/fragments` and `positions/selectors`: the fragment of an input path on the command line and the quoting of a
  selector for bash, PowerShell, and cmd;
- `errors` (which also replaces the title and summary), `errors/codes`, and `errors/diagnostics`: the exit codes,
  standard error, the `--json` failure document, the codes only the tool reports (`operation-failed`,
  `threshold-failed`, and `pandoc-filter-error`), and the meaning of `usage-error` on the command line;
- `files`: the `--file-access` and `--allow-root` options and the Pandoc filter's variables;
- `limits`: the flags that set or reach the limits and the `formats` listing.

The CLI user therefore reads one card per topic, the library text first and the command line after it. The parity of
the `svg/flatten` and `files` cards with the user guide compares the merged bullets (the library's, then the
overlay's).

#### Error Handling

These usage errors are `ToolException` with code `usage-error` and `Command` set to `help`:

- An unknown example name has the message `Help card '<path>' has no example '<name>'.` and the hint
  `Examples: <names>.`, or, when neither the card nor a card below it has an example,
  `Run 'canvasnet help <root> --examples' to find the cards of '<root>' that have examples.`.
- An unknown card below a card has the message `Unknown help card '<word>' under '<path>'.`. The hint is
  `Did you mean '<name>'? Cards under '<path>': <names>. Run '<command>' for their summaries.`, or, for a card without
  cards below it, `'<path>' has no cards below it.`.

Null arguments throw `ArgumentNullException`.

Loading problems are `InvalidOperationException` from `HelpCatalog.Extend` (or the check of `TopicNames`) whose
message names the card file, and the line when there is one: a front matter outside the subset, an unknown key or a key
an overlay may not have, a missing title or tags, a malformed example, an unknown token, an overlay of an unknown card
or of another path, a tool card in the tree of a library card (the message says to move it to the library or make it an
overlay), a link that resolves nowhere, and children or See also entries that do not resolve. They are defects in the
shipped content, not user errors: the unit tests load every card, so a malformed card fails the build before it reaches
a user. The consistency of the command-line content with the tool is proven by the unit tests (see *HelpTopics Unit
Verification Design*):

- the command lines (examples and code blocks) use known commands, options, and help cards;
- the options listed in an `## Options` or `## Command line` section exist in `render` or are global options;
- the merged `errors codes` card lists every error code, the tool-only codes in its command-line section;
- the tool's cards cite only diagnostic identifiers the library cards cite;
- the commands of the framed-figure and selectors sections do what the cards say when they are run.

The content of the library cards (the examples render, the advice holds, the cited identifiers exist, the guides
agree) is proven by the tests of the library (see *HelpCatalog Unit Verification Design*).

#### Dependencies

- **HelpCatalog** (the `Help` subsystem of the `CanvasNetViewer` system) - the library cards and examples, the
  parsing and validation of the tool's card files, the merge of the overlays (`Extend`), the views, and the links
  (`FormatMarkdown` with `HelpLink`); the library tokens (limits, `maxDpi`, qualities) are filled in there.
- **ResultWriter** - the envelope, key order, and escaping of the JSON documents; `ResultWriter.SchemaVersion` for the
  tool's token.
- **ToolException** and **ErrorCodes** - the usage errors for an unknown example or card (documented in *Program Unit
  Design*).
- **CommandRegistry** - `ClosestName`, for the suggestion of an unknown card.

#### Callers

- **CommandRegistry** - `Find`, `Roots`, `Resolve`, `SplitPath`, `TopicNames`, `SubtreeExamples`,
  `CardsBelowWithExamples`, and the renderings, for the `help` command, the index, the hint of an unknown command, and
  the examples of a card.
- **Validation** - `SourceExamples`, `All`, `Get`, and `RenderExample`, for the `CanvasNetTool_HelpExamples` self-test.
- Tests - load and check every card and every command line.
