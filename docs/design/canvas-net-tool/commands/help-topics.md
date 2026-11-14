### HelpTopics

![Commands Structure](CommandsView.svg)

#### Purpose

`HelpTopics` is the tree of help cards that `canvasnet help <topic> [<card> ...]` prints. A card is a short Markdown
text about one subject. The fourteen root cards (the help topics) are one per format the tool reads (`scad`,
`vega-lite`, `mermaid`, `svg`, `pdf`, `pptx`, `vsdx`, and `raster`), the two guidance hubs (`style` and `composition`),
and the cross-cutting subjects (`json`, `errors`, `limits`, and `files`). A large topic is split into cards below its
root, for example `mermaid gantt` and `mermaid gantt settings`, so an agent reads only the part it needs. The cards say
what a format supports, what it does not, which options apply, which diagnostics can appear (and what `--strict` does
with them), where the tool differs from the original program, how to make a picture look good, and how to compose
pictures, and they carry tiny examples that an agent can print and run.

The unit's single responsibility is to load that content, keep it consistent with itself and the code, resolve a card
by its path, and render it as plain text or as JSON. The dispatch of the `help` command and the index of commands and
topics belong to `CommandRegistry` (the file `CommandRegistry.Help.cs`), which calls this unit.

The unit is the internal static partial class `HelpTopics` in three files: `HelpTopics.cs` (the model, the loading and
validation of the embedded cards, the token substitution, and the lookup), `HelpTopics.FrontMatter.cs` (the front-matter
parser), and `HelpTopics.Render.cs` (the text and JSON renderings and the wrapping). The content is embedded in the tool
assembly, so an installed tool needs no other file.

#### Data Model

**Model records** (`internal sealed record`, in `HelpTopics.cs`):

- `HelpExample` holds `Name`, `Kind`, `Language`, `FileName`, `Description`, and `Content`.
- `HelpCard` holds `Id`, `Aliases`, `Title`, `Summary`, `Body`, `ChildIds`, `SeeAlso`, and `Examples`. It also derives
  `Name` (the last segment of the identifier), `RootId`, `ParentId`, `Depth`, `Path` (the identifier with spaces, for
  example `mermaid gantt`), and `Command` (`canvasnet help mermaid gantt`).
- `FrontMatterValue` (in `HelpTopics.FrontMatter.cs`) holds `Line` and one of `Scalar`, `Items`, or `Map`.
- `FrontMatterDocument` holds the front-matter `Properties`, in order, and the `Body`.

The records are immutable, so the catalog is safe to share between threads.

**TopicNames**: the root card identifiers in the order they are listed: `scad`, `vega-lite`, `mermaid`, `svg`, `pdf`,
`pptx`, `vsdx`, `raster`, `style`, `composition`, `json`, `errors`, `limits`, and `files`. The loader requires the
embedded root cards to be exactly this set. **FormatTopicNames** is the first eight, the topics that describe one
format.

**Catalog**: a `Lazy<CardCatalog>` that loads every embedded card on first use. It holds the cards depth first (each
root of `TopicNames`, followed by the cards below it in the order of its `children`) and a dictionary by identifier.
The catalog is read once and never changes afterwards.

**Embedded resources**: the tool project embeds the content with two `EmbeddedResource` items. Both items set
`WithCulture="false"`, so MSBuild never reads a dotted name as a satellite-resource culture.

| Source file (under `Commands/Help/`) | Logical resource name |
| --- | --- |
| `<path>.md`, in folders that mirror the tree | `DemaConsulting.CanvasNet.Tool.Help/<path>.md` |
| `Examples/<root>-<file name>` | `DemaConsulting.CanvasNet.Tool.Help.Examples.<root>-<file name>` |

The card names come from `%(RecursiveDir)`, which separates folders with `\` on Windows and `/` elsewhere; the loader
turns a `\` into `/`. The prefixes are the constants `CardResourcePrefix` and `ExampleResourcePrefix`, and
`CardExtension` is `.md`.

The example sources stay real files with the extension of their format, such as `scad-plate.scad`,
`vega-lite-styled-chart.vl.json`, `mermaid-styled-flow.mmd`, and `svg-report.svg`. They can be edited and checked with
ordinary tools. Every resource is read as text with LF line endings and without trailing line feeds.

**Card file** (`Commands/Help/<path>.md`): the identifier of a card is its path without `.md`, with `/` separators and
segments of lower case letters and digits joined by single dashes. A card that has cards below it has a folder of the
same name. The file starts with YAML front matter between two `---` lines, and the rest is the Markdown body.

The front matter has these keys:

- `title` (required): the title shown at the top of the card.
- `summary` (required): one line, shown in lists and in every See also entry that points to the card.
- `aliases`: other names that select the card among its siblings (`openscad`, `vl`, `mmd`, `graph`, `scales`, and so
  on).
- `children`: the names of the cards below it, in order; the list must be exactly the card files of its folder.
- `see-also`: the identifiers of related cards.
- `examples`: a list of maps, each with `name`, `kind`, `language`, `description`, and either `file` or `content`.

The body is ordinary Markdown: `##` headings, bullets, and fenced code blocks with a language. It has no `#` heading,
because the renderer prints the title as one, and no heading of the generated footer (`## Cards`, `## Examples`, and
`## See also`). An item with a term starts with a code span or bold text followed by a colon and a space, as in
`` - `--strict`: ... ``.

**Examples**: an example `name` is unique below its root, so `--example` finds it from any card of the tree. The
`kind` is one of these:

- `source` (`SourceKind`): the content is the embedded file `Examples/<root>-<file>`, a file the tool can read on its
  own, so the tests render every one with `--strict`.
- `snippet` (`SnippetKind`): stored the same way, but its text links other files. Examples are the `embed` and
  `embed-pages` drawings of `svg embedding` and `svg fragments`, the `pictures` diagram of `mermaid composition`, and
  the `report` figure of `svg composition`. `SourceExamples()` skips a snippet, and a dedicated test renders each one
  with its sibling files.
- `command` (`CommandKind`): the inline `content` is a command line that starts with `canvasnet`.

The `language` is `openscad`, `json`, `xml`, `mermaid`, or `shell`.

**Tokens**: the title, the summary, the body, an example description, and a command may use `{{name}}` tokens, which
the loader replaces from the live constants of the code, so a documented number cannot drift from the real one.
`Tokens` is the table; the values are formatted with the invariant culture and no thousands separator.

| Token | Source |
| --- | --- |
| `schemaVersion` | `ResultWriter.SchemaVersion` |
| `maxDimension` | `Surface.MaxDimension` |
| `maxDpi` | `ArgumentReader.MaxDpi` |
| `qualityMin` | `ImageWriter.MinQuality` |
| `qualityMax` | `ImageWriter.MaxQuality` |
| `qualityDefault` | `ImageWriter.DefaultQuality` |
| `scadMaxTriangles` | `ScadLimits.Default.MaxTriangles` |
| `vlMaxSourceLength` | `VegaLiteLimits.Default.MaxSourceLength` |
| `vlMaxJsonDepth` | `VegaLiteLimits.Default.MaxJsonDepth` |
| `vlMaxDataFiles` | `VegaLiteLimits.Default.MaxDataFiles` |
| `vlMaxDataBytes` | `VegaLiteLimits.Default.MaxDataBytes` |
| `vlMaxDataRows` | `VegaLiteLimits.Default.MaxDataRows` |
| `vlMaxImages` | `VegaLiteLimits.Default.MaxImages` |
| `vlMaxPaintedPixels` | `VegaLiteLimits.Default.MaxPaintedPixels` |
| `mmdMaxTextSize` | `MermaidLimits.Default.MaxTextSize` |
| `mmdMaxEdges` | `MermaidLimits.Default.MaxEdges` |
| `mmdMaxNodes` | `MermaidLimits.Default.MaxNodes` |
| `mmdMaxImages` | `MermaidLimits.Default.MaxImages` |
| `fragmentMaxLength` | `ImageFragment.MaxLength` |
| `fragmentMaxParameters` | `ImageFragment.MaxParameters` |

Single braces such as `{page}` are literal text. A token the table does not know is a load error, so a literal pair of
double braces cannot be written.

**The tree.** Each format topic is an index card with the cards below it:

- `mermaid` has a card for each of the ten diagram types, and each type with a settings table in the guide has a
  `settings` card below it. It also has `style`, `composition`, `options`, `diagnostics`, `unsupported`, and
  `differences`.
- `svg` has `supported`, `embedding`, `fragments`, `units`, `flatten`, `style`, `composition`, `framed-figure`,
  `options`, and `diagnostics`.
- `vega-lite` has `data`, `marks`, `encodings`, `transforms`, `params`, `layout`, `style`, `composition`, `options`,
  `unsupported`, `diagnostics`, `warnings`, and `differences`.
- `scad` has `language`, `views`, `anchors`, `style`, `composition`, `options`, `diagnostics`, and `differences`.
- `json` has `results`, `diagnostics`, and `help`, and `errors` has `codes` and `diagnostics`.
- `pdf`, `pptx`, `vsdx`, `raster`, `limits`, `files`, `style`, and `composition` are single cards.

The tree of each format holds the `##` headings `Supported`, `Unsupported`, `Options`, `Diagnostics`, and
`Known differences`. The `style` and `composition` hubs link to the style and composition card of each drawing format,
and those link back.

Some bullets are compared word for word with the user guides:

- The `## Syntax`, `## Relations`, and `## Tags` bullets of `mermaid <type>` and the `## Settings` bullets of
  `mermaid <type> settings` follow the tables of the Mermaid user guide.
- The bullets of `mermaid unsupported` and `mermaid diagnostics` carry the guide's code table.
- The `## Reference` bullets of `svg flatten` follow the `#### Flatten Reference` table.
- The bullets of `files` follow the `#### File Access Reference` table of the tool user guide.

**Size budget**: the rendered text of a card is at most 6,000 characters, and at most 3,500 for a root card that has
cards below it. A summary is at most 60 characters for a root card and 100 for any other card. The size budget is a
unit test, not a load check.

**TextWidth**: 78, the column at which generated lines are wrapped. Body lines are written at most 80 columns wide,
and 120 inside a code block.

#### Key Methods

**ParseFrontMatter(text, fileName)** (`internal`): splits a card file into its front matter and body. It reads only
this strict YAML subset:

- `key: value` at column 0, where a key is lower case letters and dashes;
- `key:` alone, followed by list items `- value` indented by two spaces;
- a list item that is a map: `- key: value` indented by two spaces, followed by `key: value` lines indented by four;
- plain, single-quoted (`''` escapes a quote), and double-quoted (only `\\` and `\"`) values on one line;
- blank lines and full-line `#` comments.

Anything else is an `InvalidOperationException` with the message `Help card '<file>', line <n>: <reason>.`: a tab, a
flow sequence or mapping, a block scalar, an anchor, an alias, a tag, a trailing comment, an unclosed quote, an
unsupported escape, a plain value that holds a colon followed by a space, a repeated key, a key without a value or
items, deeper nesting, or a missing opening or closing fence. The body is the text after the closing fence, with LF line
endings, without one leading blank line, and without trailing line feeds. No YAML package is used.

**LoadCard(fileName, text)** (`internal`): checks the file name, parses the front matter, and reports these problems
with the file and the line:

- an unknown key, or a missing or non-text `title` or `summary`;
- a list where text is expected, or text where a list is expected;
- a badly formed alias;
- an example with an unknown key or a missing key, or one that is not a source or snippet with a `file` or a command
  with a `content`;
- a command that does not start with `canvasnet`.

It then reads the file of each source or snippet, replaces the tokens (an unknown token is an error naming the file),
and returns the `HelpCard`.

**LoadAll** (private): loads every resource that starts with `CardResourcePrefix` and validates the tree. The roots
must be exactly `TopicNames`, and the `children` of every card must be exactly the card files of its folder, so every
card is reachable. A card below another needs its parent card. Every See also entry must name another card, once.
Names and aliases must be unique among siblings and among the roots, ignoring case, and example names unique below
each root.

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

**Resolve(path)**: splits every word of the path at `/`, finds the root with `Find`, and then each further word among
the children of the previous card by name or alias, ignoring case. It returns `null` when the first word is not a
root; an unknown further word is the `ToolException` described under _Error Handling_.

**SourceExamples()**, **Snippets()**, **AllExamples()**, and **SubtreeExamples(card)**: enumerate the examples with
their cards. **CardsBelowWithExamples(card)** enumerates the cards below a card that have examples of their own, depth
first. The unit tests and the `CanvasNetTool_HelpExamples` self-test render every source example with `--strict`.

**RenderText(card)**: the plain-text card, without a trailing newline, in this order:

1. `# <title>`, a blank line, and the summary.
2. `Also known as: <aliases>`, when the card has aliases.
3. The body as written, after token substitution; the body is not rewrapped and no Markdown is converted.
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

- `kind` `card`: `id`, `aliases`, `title`, `summary`, `body`, `children` and `seeAlso` (each entry with `id`,
  `summary`, and `command`), and `examples` (each with `name`, `kind`, `language`, `fileName` for a file,
  `description`, and `content`).
- `kind` `topics`: `cards`, each with `id`, `aliases`, `summary`, and `command`.
- `kind` `examples`: `id`, `examples` (the card's own examples, each as in the `card` document, with `content` and
  without a `card` key), and `cards` (the cards below it that have examples, each with `id`, `summary`, `command`, and
  `examples`, the names).
- `kind` `example`: `id` and `example` (with `card` and `content`).

These documents replaced the `topic` document of schema version 1, which is why `ResultWriter.SchemaVersion` is 2.
Version 2 has not been released, so the change of the `examples` document (the card's own examples in full, plus
`cards`, instead of a list of the subtree without contents) is part of version 2 and needs no further bump.

**WriteStrings(writer, name, values)** (`internal`): writes a JSON array of strings; `CommandRegistry` uses it for the
alias lists of its own documents.

#### Error Handling

These usage errors are `ToolException` with code `usage-error` and `Command` set to `help`:

- An unknown example name has the message `Help card '<path>' has no example '<name>'.` and the hint
  `Examples: <names>.`, or, when neither the card nor a card below it has an example,
  `Run 'canvasnet help <root> --examples' to find the cards of '<root>' that have examples.`.
- An unknown card below a card has the message `Unknown help card '<word>' under '<path>'.`. The hint is
  `Did you mean '<name>'? Cards under '<path>': <names>. Run '<command>' for their summaries.`, or, for a card without
  cards below it, `'<path>' has no cards below it.`.

Null arguments throw `ArgumentNullException`.

Loading problems are `InvalidOperationException` whose message names the card file, and the line when there is one.
They include a front matter outside the subset, an unknown key, a missing title, a malformed example, an unknown token,
a missing example file, a repeated name or alias, an orphan card, and children or See also entries that do not
resolve. They are defects in the shipped content, not user errors: the unit tests load every card, so a malformed card
fails the build before it reaches a user. The consistency of the content with the code is proven by the unit tests
(see _HelpTopics Unit Verification Design_):

- the command lines (examples and code blocks) use known commands, options, and help cards;
- the options listed exist in `render`, and the `errors codes` card lists every error code;
- every cited diagnostic identifier is produced by a tiny model or chart or exists in the libraries;
- every source example and snippet renders with `--strict` without a diagnostic;
- every Mermaid theme the `mermaid style` card lists renders;
- every config key of the styled Vega-Lite example changes the picture.

#### Dependencies

- **ResultWriter** - the envelope, key order, and escaping of the JSON documents; `ResultWriter.SchemaVersion` for a
  token.
- **ToolException** and **ErrorCodes** - the usage errors for an unknown example or card (documented in _Program Unit
  Design_).
- **CommandRegistry** - `ClosestName`, for the suggestion of an unknown card.
- **ArgumentReader** - `MaxDpi`, for a token.
- **ImageWriter** (Imaging) and the core `Surface` - the quality and size constants for tokens.
- **ScadLimits** (Scad), **VegaLiteLimits** (VegaLite), and **MermaidLimits** (Mermaid) - the resource limits for
  tokens.
- **ImageFragment** (core ImageSources) - the fragment limits for tokens.

#### Callers

- **CommandRegistry** - `Find`, `Roots`, `Resolve`, `SplitPath`, `TopicNames`, `SubtreeExamples`,
  `CardsBelowWithExamples`, and the renderings, for the `help` command, the index, the hint of an unknown command, and
  the examples of a card.
- **Validation** - `SourceExamples`, `All`, `Get`, and `RenderExample`, for the `CanvasNetTool_HelpExamples` self-test.
- Tests - load and check every card.
