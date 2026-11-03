### HelpTopics

<!-- cspell:ignore canvasnet vegalite jpeg jpg tiff jp2 openscad -->

![Commands Structure](CommandsView.svg)

#### Purpose

`HelpTopics` is the catalog of help topics that `canvasnet help <topic>` prints: one topic for each format the tool
reads (`scad`, `vega-lite`, `svg`, `pdf`, `pptx`, `vsdx`, and `raster`) and three cross-cutting topics (`json`,
`errors`, and `limits`). A topic says what a format supports, what it does not, which options apply, which diagnostics
can appear (and what `--strict` does with them), and where the tool differs from the original program, and it carries
tiny examples that an agent can print and run. Its single responsibility is to load that content, keep it consistent
with the code, and render it as plain text or as JSON. The dispatch of the `help` command and the index of commands and
topics belong to `CommandRegistry` (the file `CommandRegistry.Help.cs`), which calls this unit.

The unit is the internal static partial class `HelpTopics` in `HelpTopics.cs` (the model, the loading of the embedded
content, the token substitution, and the lookup) and `HelpTopics.Render.cs` (the text and JSON renderings and the
wrapping). The content is embedded in the tool assembly, so an installed tool needs no other file.

#### Data Model

**Model records** (`internal sealed record`, in `HelpTopics.cs`): `HelpItem` (`Term`, optional, and `Text`),
`HelpSection` (`Id`, `Title`, and `Items`), `HelpExample` (`Name`, `Kind`, `Language`, `FileName`, `Description`, and
`Content`), and `HelpTopic` (`Name`, `Aliases`, `Title`, `Summary`, `Sections`, and `Examples`). The records are
immutable, so the catalog is safe to share between threads.

**TopicNames**: the canonical names in the order the topics are listed: `scad`, `vega-lite`, `svg`, `pdf`, `pptx`,
`vsdx`, `raster`, `json`, `errors`, `limits`. A topic is added or removed only by changing this list together with the
embedded files, and a unit test proves that the embedded topic files are exactly this set. **FormatTopicNames** is the
first seven names, the topics that describe one format.

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
`svg-shapes.svg`), so they can be edited and checked with ordinary tools. A resource is read as text with LF line
endings and without trailing line feeds.

**Topic file** (`Commands/Help/<topic>.json`): one JSON object with these properties.

- `topic`: the canonical name; must equal the file name without its extension.
- `aliases`: other names that select the topic (`openscad`, `vegalite`, `vl`, `powerpoint`, `visio`, and for `raster`
  `png`, `jpeg`, `jpg`, `bmp`, `tiff`, `tif`, `gif`, `jpeg2000`, and `jp2`).
- `title`: the title shown at the top of the topic.
- `summary`: one line of plain text, shown in the topic list.
- `sections`: an array of `{id, title, items}`; `id` is a kebab-case identifier, unique within the topic, and `items`
  is an array of `{term?, text}`.
- `examples`: an array of `{name, kind, language, fileName?, description, content?}`.

A format topic has the sections `supported`, `unsupported`, `options`, `diagnostics`, and `differences`, and at least
two examples; this is checked by the unit tests, not by the loader. An example `name` is unique within its topic. The
`kind` is `source` (`SourceKind`) or `command` (`CommandKind`). A `source` example has a `fileName`, and its content is
the text of the embedded file `Examples/<topic>-<fileName>`. A `command` example has inline `content` that is a command
line and starts with the tool name followed by a space (`canvasnet`). The `language` is `openscad`, `json`, `xml`, or
`shell`.

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

Single braces such as `{page}` are literal text. A token the table does not know is a load error.

**TextWidth**: 78, the column at which text is wrapped.

#### Key Methods

**All** (property): returns the catalog in `TopicNames` order, loading it on first use. A missing or malformed
resource makes the first access throw `InvalidOperationException`. Because the catalog is a `Lazy`, the exception is
raised again on later accesses.

**Find(nameOrAlias)**: returns the topic whose name or one of whose aliases equals the argument ignoring case, or
`null`. Names and aliases are unique across the catalog (checked at load), and a unit test proves that none equals a
command, a command alias, `help`, or `topics`, so a subject is never ambiguous.

**SourceExamples()**: enumerates every `source` example with its topic. The unit tests and the `CanvasNetTool_HelpExamples`
self-test render each of them with `--strict`.

**LoadTopic(name)** (private): reads the resource `<ResourcePrefix><name>.json`, replaces the `{{token}}` placeholders,
parses the JSON, requires `topic` to equal `name`, requires every text property to be present and non-empty (`topic`,
`title`, `summary`, section `id` and `title`, item `text`, example `name`, `kind`, `language`, and `description`),
requires unique section ids and unique example names, and reads the content of each example: the embedded example file
for a `source`, the inline `content` for a `command`. `LoadAll` then requires every name and alias to be unique across
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

#### Error Handling

An unknown example name is a `ToolException` with code `usage-error`, the message `Topic '<topic>' has no example
'<name>'.`, the hint `Examples: <names>.`, and `Command` set to `help`. Null arguments throw `ArgumentNullException`.

Loading problems (a missing or malformed resource, an unknown token, a repeated name, alias, section id, or example
name, a topic file that declares another topic, an example of the wrong kind, or a command example that does not start
with the tool name) are `InvalidOperationException`. They are defects in the shipped content, not user errors: the unit
tests
load every topic, so a malformed file fails the build before it reaches a user. The consistency of the content with the
code is proven by the unit tests (see _HelpTopics Unit Verification Design_): the command examples use known commands and
options, the options listed exist in `render`, the `errors` topic lists every error code, every cited diagnostic
identifier is produced by a tiny model or chart or exists in the libraries, and every text example renders with
`--strict` without a diagnostic.

#### Dependencies

- **ResultWriter** - the envelope, key order, and escaping of the JSON documents; `ResultWriter.SchemaVersion` for a
  token.
- **ToolException** and **ErrorCodes** - the usage error for an unknown example (documented in _Program Unit Design_).
- **ArgumentReader** - `MaxDpi`, for a token.
- **ImageWriter** (Imaging) and the core `Surface` - the quality and size constants for tokens.
- **ScadLimits** (Scad) and **VegaLiteLimits** (VegaLite) - the resource limits for tokens.

#### Callers

- **CommandRegistry** - `Find`, `All`, `TopicNames`, and the renderings, for the `help` command, the index, the hint of
  an unknown command, and the examples of a topic.
- **Validation** - `SourceExamples`, `All`, `TopicNames`, and `RenderExample`, for the `CanvasNetTool_HelpExamples`
  self-test.
- Tests - load and check every topic.
