## Help

![Help Structure](HelpView.svg)

### Overview

The `Help` subsystem (namespace `DemaConsulting.CanvasNet.Viewer.Help`, folder
`src/DemaConsulting.CanvasNet.Viewer/Help/`) carries the help about the drawing and file formats the library reads, as
data that an application reads programmatically. The library is used by other applications, often agent toolkits that
give knowledge to an AI, and the knowledge about what to write in a file, how to make it look good, how files compose,
what is reported, the limits, what the host controls, and which files a document may read belongs with the library that
reads the files. The subsystem therefore ships short Markdown help cards (93 cards under 15 root cards) and tiny example
files as embedded resources of the Viewer assembly, and exposes them through a small, immutable, thread-safe public API.

The subsystem is free of command-line concerns: the library cards describe the formats in library terms (the options of
`ViewerRenderOptions` and its companions, strict mode, the file-access policy, the members of `ViewerImage`) and carry
no command, flag, or result document of the tool. The `canvasnet` command-line tool (the `HelpTopics` unit of the
`CanvasNetTool` system) layers its own cards (the `json` and `pandoc` trees) and its command-line sections (overlays of
library cards) on the library catalog through the public `HelpCatalog.Extend`, so a command-line user still sees one
card per subject. The dependency runs one way: the tool uses `Help`; `Help` knows nothing of the tool.

It contains two units:

- **HelpCatalog** (`HelpCatalog.cs`, `HelpCatalog.Load.cs`, `HelpCatalog.Links.cs`, `HelpCatalog.Render.cs`, and the
  public types `HelpCard`, `HelpExample`, `HelpLink`, and `HelpTags`): the catalog, its loading and validation, the
  tags and filtered views, the neutral links, the extensions of a host, and the renderings. See *HelpCatalog Unit
  Design*.
- **HelpFrontMatter** (`HelpFrontMatter.cs`): the strict parser of the YAML front matter of a card file. See
  *HelpFrontMatter Unit Design*.

The cards themselves are content, not code: `Help/Cards/<path>.md` (one file per card, in folders that mirror the tree)
and `Help/Examples/<root>-<file>` (the example sources). They are reviewed with the `HelpCatalog` unit, whose tests
verify them against the code.

### Interfaces

**HelpCatalog.Default** (provided to every host): the library catalog, loaded and validated once on first use. A
malformed embedded card is a defect of the library and throws `InvalidOperationException` naming the file and the line;
the unit tests load every card, so it cannot ship.

**Cards**, **Roots**, **Find(path)**, **GetChildren(card)**, and **GetSeeAlso(card)** (provided): the visible cards of a
catalog or view depth first, the visible cards without a visible ancestor, the resolution of a path of names or
aliases (null for an unknown or hidden card), the nearest visible descendants of a card, and its visible related cards.

**Filter(include, exclude)** (provided): a view of the cards that have any included tag (all when nothing is included)
and no excluded tag. The tags are the closed vocabulary of `HelpTags`; an unknown (or null) tag is `ArgumentException`.

**FormatMarkdown(card, formatLink)** (provided): the body of a card with each visible in-text link written by the
host's callback, which receives a `HelpLink` (the target card, the example name, and the card that owns the example).

**RenderText(card)**, **RenderJson(card)**, **RenderIndexText()**, and **RenderIndexJson()** (provided): a card or the
visible tree as deterministic plain text and as one JSON object.

**Extend(assembly, resourcePrefix, tokens)** (provided): a new unfiltered catalog with the card files embedded in a
host assembly layered on this one; the `HelpTopics` unit of the tool calls it with the tool assembly, the prefix
`DemaConsulting.CanvasNet.Tool.Help`, and the token `schemaVersion`.

**Consumed**: the constants the cards quote as tokens (`Surface.MaxDimension` of the core `Canvas` subsystem,
`SourceLoader.MaxDpi` and the qualities of `ImageWriter` of the `Imaging` subsystem, `ScadLimits.Default`,
`VegaLiteLimits.Default`, `MermaidLimits.Default`, and `PlantUmlLimits.Default` of the format packages, `ImageFragment`
of the core `ImageSources` subsystem, and `PositionCollector`, `PositionSelection`, `PositionSelector`, and
`ImagePosition` of the core `Positions` subsystem), and `System.Text.Json`'s `Utf8JsonWriter` for the JSON.

### Design

A card file is read in two steps: `HelpFrontMatter.Parse` splits it into its front matter (keys and values with their
lines) and its body (with the line it starts on), and `HelpCatalog` interprets the keys (title, summary, tags, aliases,
children, see-also, examples, and, in a host's overlay, extends), replaces the `{{name}}` tokens from the constants of
the code, builds the tree (the path of a file is the identifier of its card), and resolves the neutral links of every
body against the tree. Every problem is an `InvalidOperationException` that names the file and, where there is one, the
line, so the errors of both units read the same.

**Tags.** Every card has tags from a closed vocabulary defined in code (`HelpTags`): the format tags `svg`, `mermaid`,
`plantuml`, `scad`, `vega-lite`, `pdf`, `pptx`, `vsdx`, and `raster` (each also the identifier of the root card of that
format; a card without a format tag is general), and the scope tags (at least one per card):

| Scope tag | What the card is about |
| --- | --- |
| `authoring` | What to write in a file: syntax, supported features, examples |
| `design` | Making a picture look good: style, palettes, arrows, framed figures |
| `composition` | Files in files: embedding, fragments, bindings, positions |
| `diagnostics` | Error codes, diagnostics, known differences, unsupported lists |
| `limits` | Resource limits |
| `hosting` | What the host application controls: render options, sizes, strict mode |
| `file-access` | File-access policies, roots, and links (the tag hosts most often exclude) |

Every card in the tree of a format has the tag of that format and no other format tag; cards outside a format tree (the
`positions` cards) may carry the tags of the formats they cover.

**Filtered views and hidden cards.** A view decides for each card, by its own tags only, whether it is visible; a hidden
parent does not hide its children. The visible cards below a hidden card move up to its nearest visible ancestor, or to
the roots when there is none, so `Roots` and `GetChildren` list the nearest visible cards in tree order. `Find` walks
the full path through hidden cards (so the style card of a format whose root card is hidden is still found by its path)
and returns null for a hidden card. Related cards drop hidden targets. An in-text link to a hidden card is written as
that card's title, and a link to a hidden example as "an example", without the link; a link to an example whose owner
is visible but which was written with a card that is hidden names the owner instead. A reader is therefore never
pointed at a card it cannot read. A filter of a view composes with the view's own filter.

**Neutral links.** A card body links a card with `[[a/b]]`, an example of a card (or of a card below it) with
`[[a/b#name]]`, and an example of the containing card (or of a card below it) with `[[#name]]`, using canonical
identifiers. Links are recognized only outside fenced code blocks and inline code spans (Mermaid uses the same brackets
in its syntax), and text outside code that starts like a link but is not one is a load error, which catches typos. The
library writes a visible link as "the card `a/b`", "the example `name`", or "the example `name` of the card `a/b`"
(the `Markdown` of a card and the text rendering); a host writes it in its own words through `FormatMarkdown` (the tool
writes `canvasnet help a b` and `--example name`). The library cards are validated against the library catalog alone,
so a library card can never point at a card that only a host adds.

**Extensions.** A host card with `extends: <path>` at the path of an existing card is an overlay: its body is appended
to the card's body after a blank line, its related cards and examples are appended to the card's (related cards without
duplicates), and its title and summary, when given, replace the card's. An overlay cannot change the tags, aliases, or
children of the card. Any other host card starts a new root tree listed after the existing roots, and may not be placed
in the tree of an existing card. The links of host cards resolve against the combined tree. A host's tokens are added
to the library tokens and may not redefine them. Extending never changes the catalog it extends, and a filtered view
cannot be extended (extend first, then filter).

**Token ownership.** The tokens that quote a library limit belong to the library: `maxDimension`, `maxDpi`, the
quality tokens, the limits of the format libraries, the fragment limits, and the position limits are filled in by
`HelpCatalog` from the constants of the code. A host fills in its own tokens (the tool's `schemaVersion`).

**Thread safety.** `Default` is a `Lazy<HelpCatalog>` with `ExecutionAndPublication`, every catalog and card is
immutable once built, and the lists are read-only wrappers, so every member is safe to call from several threads.

The subsystem adds no dependency on a system other than the CanvasNet core library and the format packages whose
constants it quotes; it reads no file and renders nothing. Its unit tests render the examples of the cards through
`CanvasNetViewer.Render` and the `SourceLoader` of the `Imaging` subsystem.
