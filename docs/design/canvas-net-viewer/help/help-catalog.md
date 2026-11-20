### HelpCatalog

![Help Structure](HelpView.svg)

#### Purpose

`HelpCatalog` is the catalog of the help cards of the library. A card is a short Markdown text about one subject: what a
format supports and what it does not, which library options apply, which diagnostics can appear and what strict mode
does with them, where the library differs from the original program, how to make a picture look good, how to compose
pictures, where the features of a picture are, the errors, the limits, and the files a document may read, with tiny
examples that render. The unit's single responsibility is to load that content, keep it consistent with itself and the
code, give views of it by tags, resolve a card by its path, write its links, layer a host's cards on it, and render a
card as plain text or JSON.

The unit is the public sealed partial class `HelpCatalog` in four files: `HelpCatalog.cs` (the API, the views, and
`Default`), `HelpCatalog.Load.cs` (the reading of the resources, the card format, the tags, the tokens, the tree
validation, and the merge of an extension), `HelpCatalog.Links.cs` (the link grammar, the detection of code, the writing
of a body for a view, and the private tree types), and `HelpCatalog.Render.cs` (the text and JSON renderings and the
wrapping). Its public value types are in `HelpCard.cs`, `HelpExample.cs`, `HelpLink.cs`, and `HelpTags.cs`. The content
is embedded in the Viewer assembly, so an installed library needs no other file.

#### Data Model

**Public types** (immutable; constructors internal, so only the catalog creates them):

- `HelpTags` (static class): the constants `Svg`, `Mermaid`, `PlantUml`, `Scad`, `VegaLite`, `Pdf`, `Pptx`, `Vsdx`, and
  `Raster` (the format tags), `Authoring`, `Design`, `Composition`, `Diagnostics`, `Limits`, `Hosting`, and `FileAccess`
  (the scope tags, written `file-access`), and the read-only lists `FormatTags`, `ScopeTags`, and `All` (the format
  tags, then the scope tags).
- `HelpExample`: `Name`, `Kind` (`SourceKind` `source`, a file that renders on its own; `SnippetKind` `snippet`, a file
  that refers to sibling files; `CommandKind` `command`, a command line, only in a host's extension), `Language`,
  `FileName` (null for a command), `Description`, and `Content` (LF line endings, no trailing newline).
- `HelpCard`: `Id` (the path, for example `mermaid/gantt`), `Name` (the last segment), `ParentId` (the structural
  parent, which may be hidden in a view), `Title`, `Summary`, `Tags`, `Aliases`, `Markdown` (the body with the tokens
  replaced and the links in library form for the view the card came from), `ChildIds` (the nearest visible descendants),
  `SeeAlsoIds` (the visible related cards), and `Examples`. Every list is a read-only wrapper, so a cast cannot change
  it.
- `HelpLink`: `CardId` (the target card as written, null for `[[#name]]`; the owner of the example when the written card
  is hidden in the view), `ExampleName` (null for a card link), and `ExampleCardId` (the card that owns the example,
  null for a card link).

**Catalog state**: a `HelpData` shared by every view of one catalog (every card as a private `HelpNode`, with its body
parsed into text parts and resolved links, the root order, and the tokens), the set of visible identifiers (null when
every card is visible), and the `HelpCard` of each visible card, built once for the view.

**LibraryRootOrder** (internal): `scad`, `vega-lite`, `mermaid`, `plantuml`, `svg`, `pdf`, `pptx`, `vsdx`, `raster`,
`style`, `composition`, `positions`, `errors`, `limits`, and `files`. The loader requires the embedded root cards to be
exactly this set.

**Embedded resources**: the Viewer project embeds the content with two `EmbeddedResource` items with
`WithCulture="false"`:

| Source file (under `Help/`) | Logical resource name |
| --- | --- |
| `Cards/<path>.md`, in folders that mirror the tree | `DemaConsulting.CanvasNet.Viewer.Help/<path>.md` |
| `Examples/<root>-<file name>` | `DemaConsulting.CanvasNet.Viewer.Help.Examples.<root>-<file name>` |

A host's resources follow the same pattern under its own prefix. A `\` of `%(RecursiveDir)` becomes `/`. Every resource
is read as text with LF line endings and without trailing line feeds.

**Card file**: the identifier is the path without `.md`, with `/` separators and segments of lower case letters and
digits joined by single dashes. The front matter (see *HelpFrontMatter Unit Design*) has these keys:

- `title` and `summary` (required): the title, and one line shown in lists and in the footers that point at the card.
- `tags` (required): at least one, from `HelpTags.All`, none repeated, at least one scope tag.
- `aliases`: other names that select the card among its siblings.
- `children`: the names of the cards below it, in order; exactly the card files of its folder.
- `see-also`: the identifiers of related cards.
- `examples`: a list of maps with `name`, `kind`, `language`, `description`, and either `file` (a source or snippet,
  read from `Examples/<root>-<file>`) or `content` (a command).
- `extends` (only in a host's overlay, which may otherwise have only `title`, `summary`, `see-also`, and `examples`).

The body is Markdown with `##` headings, bullets, and fenced code blocks with a language; it has no `#` heading and no
heading of the generated footer (`## Cards`, `## Examples`, `## See also`; `FooterHeadings`).

**The tree**: each format root has the cards of its subjects below it - `mermaid` one card per diagram type (with a
`settings` card below each type that has settings), `plantuml` the sequence, activity, class, object, state, usecase,
component, and deployment cards and the preprocessor, `svg` the supported, embedding, fragments, units, flatten,
bindings, arrows, and framed-figure cards, `vega-lite` the data, marks, encodings, transforms, params, layout, and
warnings cards, and `scad` the language, views, and anchors cards; each drawing format also has `style`,
`composition`, `options`, and `diagnostics` cards, and `differences` and `unsupported` cards where its lists are long.
`positions` has `selectors`, `mermaid`, `vega-lite`, and `geometry`, and `errors` has `codes` and `diagnostics`. `pdf`,
`pptx`, `vsdx`, `raster`, `style`, `composition`, `limits`, and `files`
are single cards. The tree of each format holds the `##` headings `Supported`, `Unsupported`, `Options` (the library
options), `Diagnostics`, and `Known differences`, and the `style` and `composition` hubs link to the style and
composition card of each drawing format, which link back.

**Tokens** (`LibraryTokens`): the title, summary, body, and example descriptions may use `{{name}}` tokens, replaced
from the live constants of the code and formatted with the invariant culture without thousands separators:
`maxDimension` (`Surface.MaxDimension`), `maxDpi` (`SourceLoader.MaxDpi`), `qualityMin`, `qualityMax`, and
`qualityDefault` (`ImageWriter`), `scadMaxTriangles` (`ScadLimits.Default`), the `vlMax*` limits
(`VegaLiteLimits.Default`: source length, JSON depth, data files, data bytes, data rows, images, painted pixels), the
`mmdMax*` limits (`MermaidLimits.Default`: text size, edges, nodes, images), the `pumlMax*` limits
(`PlantUmlLimits.Default`: text size, nodes, edges, includes, include depth), `fragmentMaxLength` and
`fragmentMaxParameters` (`ImageFragment`), and `positionsMax` (`PositionCollector.MaxPositions`),
`positionsMaxSelectors` (`PositionSelection.MaxSelectors`), `positionsMaxSelectorLength` and `positionsMaxPredicates`
(`PositionSelector`), and `positionsMaxDataFields` (`ImagePosition.MaxDataFields`). A host adds its own tokens in
`Extend`. An unknown token is a load error.

**Size budget** (a unit test, not a load check): the `RenderText` of a library card is at most 6,000 characters, and at
most 3,500 for a root card with cards below it; a summary is at most 60 characters for a root card and 100 for any
other card. **TextWidth** is 78, the column at which generated lines are wrapped.

#### Key Methods

**Default** (static): the library catalog from a `Lazy<HelpCatalog>` (`ExecutionAndPublication`) that calls
`ReadResources` on the Viewer assembly and `Create` with `LibraryRootOrder`.

**Create(cards, examples, rootOrder)** (internal; the test seam): parses every card (`ParseCard`), refuses `extends`,
checks that the roots are `rootOrder`, builds the nodes, validates the tree (`ValidateTree`), and parses the links of
every body (`ParseBody`) against the tree.

**ParseCard** (private): checks the file name, parses the front matter, refuses a host card that is not an overlay in
the tree of an existing card (before any other key is checked, so a misplaced card gets the placement advice), checks
the allowed keys (fewer for an overlay), the aliases, the tags (`ParseTags`), and the examples (`ParseExample`, which
reads the file of a source or snippet), and replaces the tokens.

**ValidateTree** (private): the children of a card are exactly the card files of its folder, a card below another needs
its parent, related cards are existing other cards named once, names and aliases are unique among siblings and among
roots ignoring case, and example names are unique below each root.

**ParseBody** and **ResolveLink** (private): find the code of the body (`FindCode`: fenced blocks of three or more
backticks and inline code spans of a paragraph), match the link grammar
`[[<card>]]`, `[[<card>#<example>]]`, or `[[#<example>]]` outside code, resolve the card (it must exist) and the example
(found depth first in the tree of the card the link names, or of the containing card for `[[#name]]`, as the command
line's example search does), and report any other text outside code that starts with `[[` and a letter (either case),
digit, or `#`.

**Find(path)**: splits the path at `/` and white space, walks the structural tree from the roots matching each segment
by name or alias ignoring case (through hidden cards), and returns the card of this view, or null.

**Filter(include, exclude)**: validates the tags (`RequireTags`), and returns a new view over the same data whose
visible set is the cards visible here that pass both lists by their own tags.

**VisibleBelow(ids)** (private): each visible card of a list, and in place of a hidden card its nearest visible
descendants; it gives `Roots` and every card's `ChildIds`.

**WriteBody(node, formatLink)** (private): writes the body parts for this view. For a link whose target (the card, or
for an example its owner) is hidden it writes the target's title or "an example"; otherwise, when the card written in an
example link is hidden, it passes the owner as `HelpLink.CardId`; it then calls `formatLink`, or writes the library form
("the card `a/b`", "the example `name`" when the containing card owns it, or "the example `name` of the card `a/b`").

**FormatMarkdown(card, formatLink)**: `WriteBody` with the host's callback.

**GetChildren(card)** and **GetSeeAlso(card)**: the cards of this view for the `ChildIds` and `SeeAlsoIds` of a card
visible in this view (`Require` maps a card of another view with the same identifier to this view's card).

**Extend(assembly, resourcePrefix, tokens)** and **ExtendWith(cards, examples, tokens)** (internal seam): refuse a
filtered view, add the tokens (refusing a redefinition), copy the nodes, parse the host cards, apply each overlay to the
card at its own path (title and summary replaced when given, related cards appended without duplicates, examples
appended), add each other card as a new tree (`ParseCard` has already refused a card in an existing tree with a
message that says to move it to the library or make it an overlay), validate the new trees and the merged cards, parse
the links of the new cards and of the overlay bodies against the combined tree, and append each overlay body after a
blank line.

**RenderText(card)**: `# <title>`, a blank line, the summary, `Also known as: ...` (when there are aliases),
`Tags: ...`, the `Markdown`, then `## Cards` (each visible child as `- <id>` with its summary indented below),
`## Examples` (one line per example: name, `[kind]`, file name, and description; the content is in `Examples` and
`RenderJson`), and `## See also` (each visible related card with its summary). Empty sections are left out; the
generated lines are wrapped by `AppendWrapped`; LF only, no trailing newline.

**RenderJson(card)**: one object on one line, written with `Utf8JsonWriter` and its default encoder (non-ASCII and
HTML-sensitive characters escaped): `id`, `title`, `summary`, `tags`, `aliases`, `body`, `children` and `seeAlso` (each
`id`, `title`, and `summary`), and `examples` (each `name`, `kind`, `language`, `fileName` when there is one,
`description`, and `content`). There is no envelope.

**RenderIndexText()** and **RenderIndexJson()**: one line per visible card, depth first, indented by its visible depth,
with its identifier and wrapped summary; and `{"cards":[...]}` with `id`, `title`, `summary`, `tags`, and `aliases`.

**AppendWrapped(builder, text, firstPrefix, restPrefix)** (private): wraps at `TextWidth`; a word longer than the free
width sits on a line of its own and is never split.

#### Error Handling

- A null argument throws `ArgumentNullException`.
- An unknown or null tag of `Filter` throws `ArgumentException` that lists the vocabulary.
- A card that is not visible in the view, passed to `GetChildren`, `GetSeeAlso`, `FormatMarkdown`, `RenderText`, or
  `RenderJson`, throws `ArgumentException`.
- A malformed card or example, an unknown token, a missing example file, an inconsistent tree, or a link that does not
  resolve throws `InvalidOperationException` whose message names the file and, where there is one, the line. For the
  embedded library cards this is a defect of the library: the unit tests load every card, so it fails the build. For a
  host's cards it is reported by `Extend`, and the host's tests load its cards in the same way.
- `Extend` on a filtered view, an overlay of an unknown card or of another path, a host card in the tree of an existing
  card, and a token that redefines an existing token throw `InvalidOperationException`.

Nothing is handled locally; every error propagates to the caller.

#### Dependencies

- **HelpFrontMatter** - the parsing of the front matter and the messages with the file and the line.
- **SourceLoader** and **ImageWriter** (Imaging) - `MaxDpi` and the quality constants, for tokens.
- **Core library** - `Surface.MaxDimension` (Canvas), `ImageFragment` (ImageSources), and `PositionCollector`,
  `PositionSelection`, `PositionSelector`, and `ImagePosition` (Positions), for tokens.
- **ScadLimits** (Scad), **VegaLiteLimits** (VegaLite), **MermaidLimits** (Mermaid), and **PlantUmlLimits** (PlantUml) -
  the resource limits, for tokens.
- **System.Text.Json** (BCL) - `Utf8JsonWriter` for `RenderJson` and `RenderIndexJson`.

#### Callers

- **Hosts of the library** - read, filter, and render the cards, and add their own with `Extend` (see the "Help for
  agents" section of the Viewer user guide).
- **HelpTopics** (the `CanvasNetTool` system) - `Default.Extend` with the tool's cards and overlays, `Roots`, `Cards`,
  and `FormatMarkdown` with the command-line link writer.
- Tests - load and check every card, and render every example.
