## Help

![Help Structure](HelpView.svg)

### Overview

The `Help` subsystem (namespace `DemaConsulting.CanvasNet.Viewer.Help`, folder
`src/DemaConsulting.CanvasNet.Viewer/Help/`) carries the help about the drawing and file formats the library reads, as
data that an application reads programmatically. The library is used by other applications, often agent toolkits that
give knowledge to an AI, and the knowledge about what to write in a file, how to make it look good, how files compose,
what is reported, the limits, what the host controls, and which files a document may read belongs with the library that
reads the files. The subsystem therefore ships short Markdown help cards (under 16 root cards) and tiny example files
as embedded resources of the Viewer assembly, and exposes them through a small, immutable, thread-safe public API.

The cards are read, validated, composed, filtered, linked, searched, and rendered by the OTS library
`DemaConsulting.HelpCards` (see *DemaConsulting.HelpCards Integration Design*). The subsystem owns only what is
CanvasNet's: which cards there are, the values of the limits they quote, the closed tag vocabulary, and the stricter
rules its tests apply to the cards.

The subsystem is free of command-line concerns: the library cards describe the formats in library terms (the options of
`ViewerRenderOptions` and its companions, strict mode, the file-access policy, the members of `ViewerImage`) and carry
no command, flag, or result document of the tool. The `canvasnet` command-line tool (the `HelpTopics` unit of the
`CanvasNetTool` system) layers its own cards (the `json` and `pandoc` trees) and its command-line sections (overlays of
library cards) on the library cards in a source of its own that imports `CanvasNetHelp.Source`, so a command-line user
still sees one card per subject. The dependency runs one way: the tool uses `Help`; `Help` knows nothing of the tool.

It contains one unit:

- **CanvasNetHelp** (`CanvasNetHelp.cs` and the public vocabulary `HelpTags.cs`): the library source with its tokens,
  the library catalog, and the configuration of a catalog builder. See *CanvasNetHelp Unit Design*.

The cards themselves are content, not code: `Help/Cards/<path>.md` (one file per card, in folders that mirror the tree),
the example sources next to their cards (`Help/Cards/<root>/<file>`), and `Help/Cards/_source.md`, which names the
source. They are reviewed with the `CanvasNetHelp` unit, whose tests verify them against the code.

### Interfaces

**CanvasNetHelp.Catalog** (provided to every host): the library catalog, a `HelpCatalog` of HelpCards, built and
validated once on first use. A defective embedded card is a defect of the library and throws `HelpCatalogException`
listing every problem with its file, line, and rule; the unit tests build the catalog, so it cannot ship.

**CanvasNetHelp.Source** and **CanvasNetHelp.Configure(builder)** (provided): the library cards as a `HelpSource`
(named `canvasnet-viewer`, with the live limits as tokens) that a host imports into its own source, and the action that
adds the source and the tag vocabulary of `HelpTags` to a `HelpCatalogBuilder`, so every catalog that holds the library
cards applies the same vocabulary.

**CanvasNetHelp.RootIds** and **HelpTags** (provided): the sixteen root cards in order, and the format and scope tags.

**The catalog of HelpCards** (provided through `Catalog`): `Cards`, `Roots`, `Find(path)`, `Resolve(path)`,
`GetChildren`, `GetSeeAlso`, `FindExample`, `Filter(includeTags, excludeTags)`, `FilterSource`, `Search`, `Suggest`,
`Render`, `RenderIndex`, `RenderExamples`, `RenderExample`, and `FormatBody`, with the card, example, and link types of
HelpCards.

**Consumed**: `DemaConsulting.HelpCards` (the catalog, the builder, the source, and the embedding targets of the
package), and the constants the cards quote as tokens (`Surface.MaxDimension` of the core `Canvas` subsystem,
`SourceLoader.MaxDpi` and the qualities of `ImageWriter` of the `Imaging` subsystem, `ScadLimits.Default`,
`VegaLiteLimits.Default`, `MermaidLimits.Default`, `PlantUmlLimits.Default`, and `MathMlLimits.Default` of the format
packages, `ImageFragment` of the core `ImageSources` subsystem, and `PositionCollector`, `PositionSelection`,
`PositionSelector`, and `ImagePosition` of the core `Positions` subsystem).

### Design

The project file sets `HelpCardsDirectory` to `Help\Cards`, and the build targets of HelpCards embed every file below it
as the resource `DemaConsulting.CanvasNet.Viewer.Help/<path>` (the default folder `Help` cannot be used, because it
holds the C# files of the subsystem). `CanvasNetHelp.Source` reads them with `HelpSource.FromAssembly`; the `_source.md`
file names the source `canvasnet-viewer`, declares schema 2 of the card format, and orders the roots. An example's
`file:` is resolved next to its card, and its file name is the last segment of `file:`. HelpCards reads the front
matter, builds the tree, checks the keys, tags, links, and examples, replaces the `{{name}}` tokens with the values of
`CanvasNetHelp.Tokens`, and reports every problem with its file, line, and rule. A literal pair of opening braces in a
card or example (the hexagon node of a Mermaid mindmap) is written `\{{`, the escape of HelpCards.

**Tags.** Every card has tags from a closed vocabulary defined in code (`HelpTags`): the format tags `svg`, `mermaid`,
`plantuml`, `mathml`, `scad`, `vega-lite`, `pdf`, `pptx`, `vsdx`, and `raster` (each also the identifier of the root
card of that format; a card without a format tag is general), and the scope tags (at least one per card):

| Scope tag | What the card is about |
| --- | --- |
| `authoring` | What to write in a file: syntax, supported features, examples |
| `design` | Making a picture look good: style, palettes, arrows, framed figures |
| `composition` | Files in files: embedding, fragments, bindings, positions |
| `diagnostics` | Error codes, diagnostics, known differences, unsupported lists |
| `limits` | Resource limits |
| `hosting` | What the host application controls: render options, sizes, strict mode |
| `file-access` | File-access policies, roots, and links (the tag hosts most often exclude) |

`CanvasNetHelp.Configure` gives the vocabulary to the builder (`TagVocabulary(HelpTags.All, HelpTags.ScopeTags)`), so a
card with another tag or without a scope tag fails the catalog, and a filter with an unknown tag is an
`ArgumentException`. Every card in the tree of a format has the tag of that format and no other format tag; cards
outside a format tree (the `positions` cards) may carry the tags of the formats they cover.

**Filtered views, links, and extensions.** These are HelpCards behavior: a view decides for each card, by its own tags,
whether it is visible; the visible cards below a hidden card move up to its nearest visible ancestor or to the roots;
`Find` walks the full path through hidden cards and returns null for a hidden card; and a link to a hidden target is
plain text. A card body links a card with `[[a/b]]`, an example with `[[a/b#name]]`, and an example of the containing
card with `[[#name]]`, recognized only outside code; the plain style writes them as "the card `a/b`" and "the example
`name` of the card `a/b`", and a host writes them in its own words with a link formatter. A host source that imports
`CanvasNetHelp.Source` may extend a library card with `extends: <path>` (its body, related cards, and examples are
appended) and may add root cards, which follow the library roots.

**Token ownership.** The tokens that quote a library limit belong to the library: `maxDimension`, `maxDpi`, the quality
tokens, the limits of the format libraries, the fragment limits, and the position limits are the 36 values of
`CanvasNetHelp.Tokens`. A host fills in its own tokens (the tool's `schemaVersion`) on its own source; the built-in
tokens `tool` and `version` of HelpCards come from the catalog builder.

**CanvasNet's rules.** HelpCards' validator (`HelpCardValidator`) applies CanvasNet's stricter options in the unit
tests: 6,000 characters a card and 3,500 a root with cards below it, summaries of 100 characters and 60 for a root,
lines of 80 columns outside a fenced code block and 120 inside one, every warning an error, and the local rule that a
library card has no command-line text. The tests add the rules HelpCards has no option for: the children of a card are
exactly the card files of its folder, the example names of a root's tree are unique, every source example renders in
strict mode, and the parity of the cards with the user guides.

**Thread safety.** `Catalog` is a `Lazy<HelpCatalog>` with `ExecutionAndPublication`, and every catalog, source, and
card of HelpCards is immutable, so every member is safe to call from several threads.

The subsystem adds no dependency on a system other than the CanvasNet core library, the format packages whose
constants it quotes, and HelpCards; it reads no file and renders nothing. Its unit tests render the examples of the
cards through `CanvasNetViewer.Render` and the `SourceLoader` of the `Imaging` subsystem.
