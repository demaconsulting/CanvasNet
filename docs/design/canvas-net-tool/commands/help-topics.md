### HelpTopics

![Commands Structure](CommandsView.svg)

#### Purpose

`HelpTopics` is the tree of help cards that `canvasnet help <topic> [<card> ...]` prints. A card is a short Markdown
text about one subject. The eighteen root cards (the help topics) are one per format the tool reads (`scad`,
`vega-lite`, `mermaid`, `plantuml`, `mathml`, `svg`, `pdf`, `pptx`, `vsdx`, and `raster`), the two guidance hubs
(`style` and `composition`), the feature-positions topic (`positions`), the cross-cutting subjects (`errors`, `limits`,
`files`, and `json`), and the Pandoc filter (`pandoc`, alias `filter`). A large topic is split into cards below its
root, for example `mermaid gantt` and `mermaid gantt settings`, so an agent reads only the part it needs.

The knowledge about the drawing and file formats is not owned here: it is the library cards of the Viewer
(`CanvasNetHelp`, see *Help Subsystem Design* and *CanvasNetHelp Unit Design* of the `CanvasNetViewer` system), which
describe the formats in library terms so that any program that uses the library gets the same help. The cards are read,
composed, validated, and rendered by the OTS library `DemaConsulting.HelpCards` (see *DemaConsulting.HelpCards
Integration Design*). This unit is the **command-line layer** on the library cards. Its single responsibility is to:

- add the tool's own cards (the `json` tree of the result documents and the `pandoc` card of the Pandoc filter);
- add the command-line sections of library cards as overlays (the flags, the `kind: command` examples, shell quoting,
  and the `canvasnet` commands that render, inspect, compare, montage, and flatten);
- compose the combined catalog with the topics in their fixed order and the tool's name and version;
- give the help command the render options of the command line (every link written as the `canvasnet help` command that
  prints its target) and the JSON envelope and error object of the tool's contract.

The dispatch of the `help` command, its options, and the index of commands and topics belong to `CommandRegistry` (the
file `CommandRegistry.Help.cs`), which runs `HelpCli` of HelpCards with this unit's catalog and helpers.

The unit is the internal static partial class `HelpTopics` in two files: `HelpTopics.cs` (the source, the combined
catalog, the topics, and the lookup of a root) and `HelpTopics.Render.cs` (the render options, the envelope, the error
object, and the `topics` subject). Its content is embedded in the tool assembly.

#### Data Model

**ToolName** (`canvasnet`), **SourceName** (`canvasnet-tool`), and **CommandTemplate** (`canvasnet help {path}`, where
`{path}` is the card identifier with spaces for slashes): the constants of the command line.

**TopicNames**: the root card identifiers in the order they are listed: the library roots of `CanvasNetHelp.RootIds`
(`scad`, `vega-lite`, `mermaid`, `plantuml`, `mathml`, `svg`, `pdf`, `pptx`, `vsdx`, `raster`, `style`, `composition`,
`positions`, `errors`, `limits`, and `files`), then the tool's `json` and `pandoc`. HelpCards orders the roots of each
source separately, so the tool's roots follow the library's. **FormatTopicNames** is the first ten, the topics that
describe one format.

**Tokens**: the tool's own `{{name}}` tokens: `schemaVersion` (`ResultWriter.SchemaVersion`). Every other token (the
limits, `maxDpi`, the qualities) belongs to the library source, which fills it in from the constants of the code; the
built-in tokens `tool` and `version` come from the catalog builder.

**Source**: `HelpSource.FromAssembly(typeof(HelpTopics).Assembly).WithTokens(Tokens).Imports(CanvasNetHelp.Source)`, the
tool's cards and overlays importing the library cards.

**Catalog**: a `Lazy<HelpCatalog>` (`ExecutionAndPublication`) that builds the combined catalog with `Configure` on
first use; it never changes afterwards, so it is safe to share between threads. **Roots** are its root cards.

**Embedded resources**: the tool project references `DemaConsulting.HelpCards` directly and sets `HelpCardsDirectory` to
`Commands\Help`; the build targets of the package embed every file below that folder as the resource
`DemaConsulting.CanvasNet.Tool.Help/<path>` with `WithCulture="false"`. The folder holds `_source.md` (the name
`canvasnet-tool`, schema 2, and the roots `json` and `pandoc`) and the card files; the tool ships no example files of
its own: its examples are command lines in the front matter.

**Tool card files** (`Commands/Help/`): each file is one of two kinds, in the card format of HelpCards:

- **A tool card**: `json.md`, `json/results.md`, `json/diagnostics.md`, `json/help.md`, and `pandoc.md`, with `title`,
  `summary`, `tags` (from the closed vocabulary of `HelpTags`), and the other keys of a card. Literal `canvasnet` text
  is allowed in these cards.
- **An overlay**: a file at the path of a library card whose front matter starts with `extends: <path>` (the same path).
  It may give `title` and `summary` (which replace the library card's on the command line, so the CLI index can keep its
  wording), `see-also` (appended without duplicates), and `examples` (appended; the `kind: command` examples live here).
  Its body starts with `## Command line` and holds the flags, shell blocks, and commands of the subject (`files` and
  `positions/selectors` add one more section after it, `## The Pandoc filter` and `## Quoting`).

**Links**: the bodies use the neutral links of HelpCards (`[[a/b]]`, `[[a/b#name]]`, `[[#name]]`), validated against
the combined tree. The command link style writes them as `` `canvasnet help a b` `` for a card and
`` `canvasnet help a b --example name` `` for an example, naming the card that holds the example, also for an example of
the card being read.

**Validation options** (unit tests): the combined cards are at most 7,000 characters, and 4,000 for a root card that has
cards below it; a summary is at most 60 characters for a root card and 100 for any other card; a line is at most 80
columns outside a fenced code block and 120 inside one; every warning is an error, so every command example must parse
as a `canvasnet` command line in which no shell would expand a character (the library budget of its own cards is 6,000
and 3,500).

**Help JSON documents**: schema version 2 (`ResultWriter.SchemaVersion`), in the envelope of `ResultWriter`.

#### Key Methods

**Configure(builder)**: `CanvasNetHelp.Configure(builder)` (the library source and the tag vocabulary), then
`AddSource(Source)`, `ToolName(ToolName)`, `ToolVersion(Program.Version)`, and `ExpectedRoots(TopicNames)`. It is the
one place that says how the combined catalog is composed, so the help command and the validation tests compose it the
same way; HelpCards rejects an overlay of an unknown card, a tool card in the tree of a library card, a link that
resolves nowhere, an unknown tag or token, and roots other than `TopicNames`.

**Find(nameOrAlias)**: returns the root card whose name or one of whose aliases equals the argument ignoring case, or
`null`. A unit test proves that no root name or alias equals a command, a command alias, `help`, or `topics`, so a
subject is never ambiguous.

**RenderOptions(format)**: the `HelpRenderOptions` of the command line: the format, the command link style, the
`CommandTemplate`, and the JSON envelope of `Envelope`.

**Envelope(kind, fields, extra)**: writes a help document with `ResultWriter.Envelope("help", ok, ...)`:
`schemaVersion`, `ok` (false only for the kind `error`), `command` (`help`), then `kind` (left out for an error, as in
every failure document of the tool), the tool's own fields (`extra`, the index fields of `CommandRegistry`), and the
fields HelpCards writes for the kind. The documents are one line of ASCII; HelpCards' own pass over the result to make
it ASCII finds nothing to change.

**WriteError(writer, error)** and **ErrorCode(helpCode)**: write the error object of the tool, `error` with `code`,
`message`, and `hint` when there is one; the codes `usage-error`, `not-found`, and `ambiguous` of HelpCards become
`usage-error`, and `catalog-error` (a defective catalog, which a tested build never has) becomes `operation-failed`. The
candidates of HelpCards are left out, because the hint names them.

**RenderTopics(json)**: the `topics` subject: `Catalog.RenderIndex` over the whole tree, as text after an intro line
(one line per card, indented by depth, with the command that prints it and its wrapped summary), or as the index
document of HelpCards with the kind `topics` (`tool`, `version`, `sources`, and `cards`, each with `id`, `title`,
`summary`, `aliases`, `tags`, `source`, `depth`, and `command`).

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
- `limits`: the flags that set or reach the limits and the `formats` listing;
- the MathML cards `mathml`, `mathml/options`, `mathml/composition`, and `mathml/diagnostics`: the `render` and
  `info` commands of an equation, the `display=` fragment, the refused `--theme` and `--positions`, how a warning
  and an error print, and the Pandoc filter's natural display width of an equation (`width="138px"` for the quadratic
  formula of the `mathml/fractions` card, a width a test measures), with `json/results` (the `equation` object of
  `info`), `json/diagnostics` (the `mathml` source), and the `pandoc` card (the `mathml` class and `.mml`).

The CLI user therefore reads one card per topic, the library text first and the command line after it (HelpCards appends
the body of an overlay to the card it extends). The parity of
the `svg/flatten` and `files` cards with the user guide compares the merged bullets (the library's, then the
overlay's).

#### Error Handling

A defective card is a `HelpCatalogException` of HelpCards on the first read of `Catalog`, listing every problem with its
file, line, and rule: a front matter outside the card format, an unknown key or a key an overlay may not have, a missing
title or tags, a malformed example, an unknown token, an overlay of an unknown card or of another path, a tool card in
the tree of a library card, a link that resolves nowhere, and roots other than `TopicNames`. These are defects in the
shipped content, not user errors: the unit tests build and validate the combined catalog, so a defective card fails the
build before it reaches a user. The help command maps such a failure to `operation-failed`.

The usage errors of the help command (an unknown card or example, a wrong option) are reported by HelpCards and shaped
by `WriteError`; see *CommandRegistry Unit Design*. Null arguments throw `ArgumentNullException`.

The consistency of the command-line content with the tool is proven by the unit tests (see *HelpTopics Unit
Verification Design*):

- the command lines (examples and code blocks) use known commands, options, and help cards, and parse without shell
  expansion;
- the options listed in an `## Options` or `## Command line` section exist in `render` or are global options;
- the merged `errors codes` card lists every error code, the tool-only codes in its command-line section;
- the tool's cards cite only diagnostic identifiers the library cards cite;
- the commands of the framed-figure and selectors sections do what the cards say when they are run.

The content of the library cards (the examples render, the advice holds, the cited identifiers exist, the guides
agree) is proven by the tests of the library (see *CanvasNetHelp Unit Verification Design*).

#### Dependencies

- **CanvasNetHelp** (the `Help` subsystem of the `CanvasNetViewer` system) - the library source with its tokens and the
  configuration with the tag vocabulary.
- **DemaConsulting.HelpCards** (OTS) - the source, the builder, the catalog, the render options, and the embedding
  targets.
- **ResultWriter** - the envelope, key order, and escaping of the JSON documents; `ResultWriter.SchemaVersion` for the
  tool's token.
- **ErrorCodes** - the codes of the error object.
- **Program** - `Program.Version`, the version of the catalog.

#### Callers

- **CommandRegistry** - `Catalog`, `ToolName`, `CommandTemplate`, `Envelope`, `WriteError`, `ErrorCode`, `RenderTopics`,
  `Find`, `Roots`, `TopicNames`, and `WriteStrings`, for the `help` command, the index, and the hint of an unknown
  command.
- **Validation** - `Catalog`, for the `CanvasNetTool_HelpExamples` self-test.
- Tests - build and validate the combined catalog and check every card and every command line.
