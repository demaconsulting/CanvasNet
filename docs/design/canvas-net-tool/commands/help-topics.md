### HelpTopics

<!-- cspell:ignore canvasnet vegalite jpeg jpg tiff jp2 openscad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`HelpTopics` is the catalog of help topics that `canvasnet help <topic>` prints: one topic for each format the tool
reads (`scad`, `vega-lite`, `mermaid`, `svg`, `pdf`, `pptx`, `vsdx`, and `raster`) and three cross-cutting topics
(`json`, `errors`, and `limits`). A topic says what a format supports, what it does not, which options apply, which
diagnostics can appear (and what `--strict` does with them), and where the tool differs from the original program, and
it carries tiny examples that an agent can print and run. Its single responsibility is to load that content, keep it
consistent with the code, and render it as plain text or as JSON. The dispatch of the `help` command and the index of
commands and topics belong to `CommandRegistry` (the file `CommandRegistry.Help.cs`), which calls this unit.

The unit is the internal static partial class `HelpTopics` in `HelpTopics.cs` (the model, the loading of the embedded
content, the token substitution, and the lookup) and `HelpTopics.Render.cs` (the text and JSON renderings and the
wrapping). The content is embedded in the tool assembly, so an installed tool needs no other file.

#### Data Model

**Model records** (`internal sealed record`, in `HelpTopics.cs`): `HelpItem` (`Term`, optional, and `Text`),
`HelpSection` (`Id`, `Title`, and `Items`), `HelpExample` (`Name`, `Kind`, `Language`, `FileName`, `Description`, and
`Content`), and `HelpTopic` (`Name`, `Aliases`, `Title`, `Summary`, `Sections`, and `Examples`). The records are
immutable, so the catalog is safe to share between threads.

**TopicNames**: the canonical names in the order the topics are listed: `scad`, `vega-lite`, `mermaid`, `svg`, `pdf`,
`pptx`, `vsdx`, `raster`, `json`, `errors`, `limits` (eleven topics). A topic is added or removed only by changing this
list together with the embedded files, and a unit test proves that the embedded topic files are exactly this set.
**FormatTopicNames** is the first eight names, the topics that describe one format.

**Catalog**: a `Lazy<IReadOnlyList<HelpTopic>>` that loads every topic of `TopicNames` on first use; `All` returns it.
The catalog is read once and never changes afterwards.

**Embedded resources**: the tool project embeds the content with two `EmbeddedResource` items whose `LogicalName`
values are flat (no folder segments), like the bundled fonts of the core library:

| Source file (under `Commands/Help/`) | Logical resource name |
| --- | --- |
| `<topic>.json` | `DemaConsulting.CanvasNet.Tool.Help.<topic>.json` |
| `Examples/<topic>-<file name>` | `...Tool.Help.Examples.<topic>-<file name>` |

The two prefixes are the constants `ResourcePrefix` (`DemaConsulting.CanvasNet.Tool.Help.`) and `ExampleResourcePrefix`
(`ResourcePrefix` followed by `Examples.`).

The example sources stay real files with the extension of their format (`scad-plate.scad`, `vega-lite-bar.vl.json`,
`mermaid-flowchart.mmd`, `svg-shapes.svg`), so they can be edited and checked with ordinary tools. A resource is read as
text with LF line endings and without trailing line feeds.

**Topic file** (`Commands/Help/<topic>.json`): one JSON object with these properties.

- `topic`: the canonical name; must equal the file name without its extension.
- `aliases`: other names that select the topic (`openscad`, `vegalite`, `vl`, `mmd`, `powerpoint`, `visio`, and for
  `raster` `png`, `jpeg`, `jpg`, `bmp`, `tiff`, `tif`, `gif`, `jpeg2000`, and `jp2`).
- `title`: the title shown at the top of the topic.
- `summary`: one line of plain text, shown in the topic list.
- `sections`: an array of `{id, title, items}`; `id` is a kebab-case identifier, unique within the topic, and `items`
  is an array of `{term?, text}`.
- `examples`: an array of `{name, kind, language, fileName?, description, content?}`.

A format topic has the sections `supported`, `unsupported`, `options`, `diagnostics`, and `differences`, and at least
two examples; this is checked by the unit tests, not by the loader. An example `name` is unique within its topic. The
`kind` is `source` (`SourceKind`), `command` (`CommandKind`), or `snippet` (`SnippetKind`). A `source` example has a
`fileName`, and its content is the text of the embedded file `Examples/<topic>-<fileName>`; it is a file the tool can
read on its own, so the tests render every one with `--strict`. A `snippet` is stored the same way (`fileName` and an
embedded file) but is text that refers to other files, such as the `embed` diagram of the `svg` topic whose `image`
elements link a model, a chart, and an SVG file, and the `embed-pages` diagram whose links choose pages (`#page=2`,
`#slide=2`), and the `pictures` diagram of the `mermaid` topic whose nodes show `part.scad` and `chart.vl.json`;
`SourceExamples()` does not return a snippet, so the strict-clean drift test
skips it, and a dedicated test renders each snippet with its sibling files (the examples of the other topics for a
model,
a chart, and an SVG file, and the documents of the shared test fixtures for a PDF, PPTX, and VSDX; a new kind rather
than
an exception in the test, so
a snippet is visible as one in `--examples` and in the JSON). A `command` example has inline `content` that is a command
line and starts with the tool name followed by a space (`canvasnet`). The `language` is `openscad`, `json`, `xml`,
`mermaid`, or `shell`.

The `svg` topic has a further section, `flatten` (after `image-links`), that documents the `flatten` command: what is
replaced, what is kept, the size, the rebasing of links for another folder, nothing to flatten, the limits, and
`--strict`; a unit test compares its items, one for one and in order, with the rows of the table under
`#### Flatten Reference` of the tool user guide (`Term | Meaning`). The `diagnostics` section documents
`svg-flatten-image-not-drawn`, which the identifier drift test allows as a code of the Viewer library, the `flatten`
command example is checked like every command example, and the `json` and `limits` topics describe the JSON document
and the limits of `flatten`.

The `svg` topic has one more section, `image-links` (after `supported`), that gathers what an agent needs to link a file
from a diagram: the item `href file types` lists the file name extensions an `image` `href` may link, then items state
the size rule, the folder rule (a query after the file name is ignored; a fragment is read as parameters), the page
fragment (grammar, the parameter of each format, limits, the `&amp;` of an XML attribute), the unknown-parameter note,
and
the warnings `svg-image-unresolved`
and `svg-image-too-large`, and an `example`, which points to the `embed`
snippet, and the file types of the `href file types` item are compared by a unit test, in both directions, with
`CanvasNetViewer.Extensions` (the extensions of the image sources the tool wires in, so a type added to the Viewer is
caught) and with the `formats` table, so the list cannot
drift from what the tool does. The `diagnostics` section of the `svg` topic documents
`image-fragment-unknown-parameter`,
`image-fragment-duplicate-parameter`, `image-fragment-invalid`, and `image-page-out-of-range` with their severity and
the effect of `--strict`; the identifier pattern of the drift test includes the `image-fragment-` and `image-page-`
prefixes and a theory renders each of the four codes through the tool. The `pdf`, `pptx`, `vsdx`, and `scad` topics show
the fragment of their format, the `json` topic describes `detail`, `location`, and `origin`, and the `limits` topic
states the limits of a fragment. The `scad` and `vega-lite` topics, the footer of the `formats` listing, and the usage
of `render` each point to `canvasnet help svg`. The `image-links` section also has the item `variable fragment`, which
documents
the prefixed parameters (`def.<name>=<value>` sets a model variable like `-D`, `param.<name>=<value>` sets a chart
parameter
like `--param`, the case rules, and the codes `image-fragment-invalid`, `vl-param-unknown`, and
`image-fragment-unknown-parameter`); the `vega-lite` topic lists `--param <name=value>` in its options and
`vl-param-unknown` in its
diagnostics (as does `svg`; the drift test accepts the identifier because it is a literal of the library), and the
`scad` topic shows
`#def.` in its link item. A unit test checks the three topics, the usage text, and that the documented command runs.

The `mermaid` topic (alias `mmd`) has the five sections of a format topic and seven more, `class`, `state`, `er`,
`pie`, `gantt`, `mindmap`, and `packet` (after `supported`, in that order): `supported` (flowcharts and their shapes,
links, subgraphs, labels, styles, configuration and themes, picture nodes, and titles, sequence diagrams and their
statements, arrows, participant types, and settings, an overview item each for class, state, and entity relationship
diagrams, pie charts, gantt charts, mindmaps, and packet diagrams, and what `info` reports), `class` (the class diagram
syntax, relation, and setting rows), `state` (the state diagram syntax and setting rows), and `er` (the entity
relationship diagram syntax, cardinality and line, and setting rows), `pie` (the pie chart syntax and setting rows),
`gantt` (the gantt chart syntax, tag, and setting rows), `mindmap` (the mindmap syntax, shape, section, layout, and
color rows and the `mindmap.*` setting rows), and `packet` (the packet diagram syntax, row, bit number, and color rows
and the `packet.*` setting rows), each item by item and in the order of the tables of the Mermaid user guide, which a
unit test compares with the guide's rows, `unsupported` (one term per identifier the renderer reports instead of
drawing: `mermaid-diagram-unsupported` for every other diagram type, which `render` draws as an empty picture and `info`
reports with `supported` false, and the icon (also a mindmap node's `::icon(...)`), math, look, layout (also a mindmap
layout other than cose-bilkent), and theme-CSS identifiers), `options` (`--theme` with the theme fragment, `--scale`,
`--background`, and `--strict`), `diagnostics` (`mermaid-warning` with the specific code in `detail`, the codes of the
errors and of the warnings of the library, and the text limit), and `differences` (the bundled fonts, a static picture,
the layout without the pictures that `info` reports, the white background, the located errors where Mermaid would crash,
a gantt chart drawn without a today line because the tool sets no clock, and the mindmap differences: the exactness of
the chaotic layout, the icons, other layouts, the empty mindmap, a decoration before the first node, and the depth and
work limits, and the packet differences: the limit on the parts of the rows and the settings and errors). The same unit
test checks that every code of the guide's table of unsupported features and warnings is an item of the `unsupported` or
`diagnostics` section with the same text. Its examples are `flowchart` (a source example: a decision, a subgraph, link
labels, and a class), `sequence`, `class`, `state`, `er`, `pie`, `gantt`, `mindmap`, and `packet` (source examples of a
sequence diagram, a class diagram, a state diagram with start
and end states, labeled transitions, a composite state, and a note, and an entity relationship diagram with attribute
tables, keys, comments, and three relationships with their cardinalities, a pie chart, a gantt chart without a
`todayMarker` statement or an unknown reference, so it renders without a warning, and a mindmap with three sections and
every node shape and a Markdown string, without an icon or a layout, and a packet diagram of three rows with ranges,
single bits, `+N` blocks, a block split across two rows, and a title, embedded as
`Examples/mermaid-sequence.mmd`, `mermaid-class.mmd`, `mermaid-state.mmd`, `mermaid-er.mmd`, `mermaid-pie.mmd`,
`mermaid-gantt.mmd`, `mermaid-mindmap.mmd`, and `mermaid-packet.mmd`), all nine rendered with `--strict` by the drift
test and the self-test, `pictures` (a snippet
whose nodes show `part.scad` and `chart.vl.json`, rendered by its own test with the `plate` example of `scad` and the
`bar` example of `vega-lite` as its siblings, which requires one `embed` operation per picture and no diagnostic), and
the commands `render`, `theme`, and `info`. The `svg` topic lists `.mmd` and `.mermaid` among the `href file types` and
`theme=` in its page fragment item, and the `limits` topic has a section for diagrams with the `mmd*` tokens. The
identifier pattern of the drift test includes the `mermaid-` prefix, and the identifiers it finds are looked up in the
Mermaid assembly as well.

**Tokens**: text in a topic file may use `{{name}}` tokens, which the loader replaces from the live constants of the
code before the JSON is parsed, so a documented number cannot drift from the real one. `Tokens` is the table; the values
are formatted with the invariant culture and no thousands separator.

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

Single braces such as `{page}` are literal text. A token the table does not know is a load error.

**TextWidth**: 78, the column at which text is wrapped.

#### Key Methods

**All** (property): returns the catalog in `TopicNames` order, loading it on first use. A missing or malformed
resource makes the first access throw `InvalidOperationException`. Because the catalog is a `Lazy`, the exception is
raised again on later accesses.

**Find(nameOrAlias)**: returns the topic whose name or one of whose aliases equals the argument ignoring case, or
`null`. Names and aliases are unique across the catalog (checked at load), and a unit test proves that none equals a
command, a command alias, `help`, or `topics`, so a subject is never ambiguous.

**SourceExamples()**: enumerates every `source` example with its topic. The unit tests and the
`CanvasNetTool_HelpExamples`
self-test render each of them with `--strict`.

**LoadTopic(name)** (private): reads the resource `<ResourcePrefix><name>.json`, replaces the `{{token}}` placeholders,
parses the JSON, requires `topic` to equal `name`, requires every text property to be present and non-empty (`topic`,
`title`, `summary`, section `id` and `title`, item `text`, example `name`, `kind`, `language`, and `description`),
requires unique section ids and unique example names, and reads the content of each example: the embedded example file
for a `source` or a `snippet`, the inline `content` for a `command`. `LoadAll` then requires every name and alias to be
unique across
the catalog, compared ignoring case.

**RenderText(topic)**: the plain-text topic, without a trailing newline: `Topic: <title>`, a blank line, the summary,
`Also known as: <aliases>` when the topic has aliases, then each section as `<Title>:` followed by its items (a plain
item is a bullet `- text` indented two columns with a hanging indent of four; a term item is its term indented two
columns, followed by its text indented six columns), then `Examples:` with one line per example (`name`, the kind in
brackets, and the description, aligned), and
the closing lines `Print one with: canvasnet help <topic> --example <name>` and
`List them with --examples; add --json for machine-readable output.` The text is wrapped by `AppendWrapped` at
`TextWidth` columns. It is deterministic: no ANSI escape sequence, no detection of the console width, and LF line
endings whatever the operating system.

**AppendWrapped(builder, text, firstPrefix, restPrefix)**: splits the text into words (single spaces), starts the first
line with `firstPrefix` and every further line with `restPrefix`, and starts a new line when the next word would pass
`TextWidth`. A word longer than the free width is never split: it sits on a line of its own. `CommandRegistry` also uses
it for the topic list of the index.

**RenderExampleList(topic)**: one line per example (name, kind, description), without a trailing newline.

**RenderExample(topic, name)**: the exact content of the example (LF line endings, no trailing newline), found by name
ignoring case. This is what `help <topic> --example <name>` prints, and nothing else. An unknown name throws the
`ToolException` described under _Error Handling_.

**RenderJson(topic)**, **RenderExampleListJson(topic)**, and **RenderExampleJson(topic, name)**: the JSON documents,
each written with `ResultWriter.Success("help", ...)`, so they have the common envelope (`schemaVersion`, `ok`,
`command`), one line, the key order below, and ASCII-only escaping.

- `kind` `topic`: `topic`, `aliases`, `title`, `summary`, `sections` (each with `id`, `title`, and `items` of `term`,
  when present, and `text`), and `examples` (each with `name`, `kind`, `language`, `fileName` for a source,
  `description`, and `content`).
- `kind` `examples`: `topic` and `examples` (as above, without `content`).
- `kind` `example`: `topic` and `example` (`name`, `kind`, `language`, `fileName` for a source, `description`, and
  `content`).

**WriteStrings(writer, name, values)** (`internal`): writes a JSON array of strings; `CommandRegistry` uses it for the
alias lists of its own documents.

**Input fragments in the topics.** The `svg` topic has the `input fragment` term (the same fragment on an input path of
`render`, its verbs, `inspect`, `info`, `compare` and `montage`: the rule for the `#`, the overrides, the quoting, the
codes), the `variable fragment` term says that `def.` takes vectors and the viewport variables, the `scad` topic has the
fixed-camera note and the `montage-variants` example, the document topics (`pdf`, `pptx`, `vsdx`) and `vega-lite` show
the command-line form, and the `errors` topic lists `image-fragment-invalid` (the drift test of the error codes
checks that every `ErrorCodes` constant is listed). `HelpTopics_InputFragments_AreDocumentedInTopicsAndUsages` checks
the key phrases and that the usage text of every command that takes an input mentions the fragment.

#### Error Handling

An unknown example name is a `ToolException` with code `usage-error`, the message `Topic '<topic>' has no example
'<name>'.`, the hint `Examples: <names>.`, and `Command` set to `help`. Null arguments throw `ArgumentNullException`.

Loading problems (a missing or malformed resource, an unknown token, a repeated name, alias, section id, or example
name, a topic file that declares another topic, an example of the wrong kind, or a command example that does not start
with the tool name) are `InvalidOperationException`. They are defects in the shipped content, not user errors: the unit
tests
load every topic, so a malformed file fails the build before it reaches a user. The consistency of the content with the
code is proven by the unit tests (see _HelpTopics Unit Verification Design_): the command examples use known commands
and
options, the options listed exist in `render`, the `errors` topic lists every error code, every cited diagnostic
identifier is produced by a tiny model or chart or exists in the libraries (the Mermaid library included), and every
text example renders with `--strict` without a diagnostic.

#### Dependencies

- **ResultWriter** - the envelope, key order, and escaping of the JSON documents; `ResultWriter.SchemaVersion` for a
  token.
- **ToolException** and **ErrorCodes** - the usage error for an unknown example (documented in _Program Unit Design_).
- **ArgumentReader** - `MaxDpi`, for a token.
- **ImageWriter** (Imaging) and the core `Surface` - the quality and size constants for tokens.
- **ScadLimits** (Scad), **VegaLiteLimits** (VegaLite), and **MermaidLimits** (Mermaid) - the resource limits for
  tokens.
- **ImageFragment** (core ImageSources) - the fragment limits for tokens.

#### Callers

- **CommandRegistry** - `Find`, `All`, `TopicNames`, and the renderings, for the `help` command, the index, the hint of
  an unknown command, and the examples of a topic.
- **Validation** - `SourceExamples`, `All`, `TopicNames`, and `RenderExample`, for the `CanvasNetTool_HelpExamples`
  self-test.
- Tests - load and check every topic.
