### CanvasNetHelp

![Help Structure](HelpView.svg)

#### Purpose

`CanvasNetHelp` is the public entry point of the help cards of the library. A card is a short Markdown text about one
subject: what a format supports and what it does not, which library options apply, which diagnostics can appear and
what strict mode does with them, where the library differs from the original program, how to make a picture look good,
how to compose pictures, where the features of a picture are, the errors, the limits, and the files a document may read,
with tiny examples that render. The unit's single responsibility is to say which cards the library ships and what their
tokens mean, and to build the library catalog from them; reading, validating, filtering, linking, and rendering the
cards is the work of the OTS library `DemaConsulting.HelpCards` (see *DemaConsulting.HelpCards Integration Design*).

The unit is the public static class `CanvasNetHelp` (`CanvasNetHelp.cs`) with the public static class `HelpTags`
(`HelpTags.cs`), the closed tag vocabulary. The content is embedded in the Viewer assembly, so an installed library
needs no other file.

#### Data Model

- `SourceName` (public constant): `canvasnet-viewer`, the `name` of `Help/Cards/_source.md`, which external links and
  the `SourceName` of every library card carry.
- `RootIds` (public, read-only): `scad`, `vega-lite`, `mermaid`, `plantuml`, `mathml`, `svg`, `pdf`, `pptx`, `vsdx`,
  `raster`, `style`, `composition`, `positions`, `errors`, `limits`, and `files`, the order of `children:` in
  `_source.md`. The catalog is built with these roots expected.
- `Tokens` (internal, read-only): the 36 token values, formatted with the invariant culture and without thousands
  separators: `maxDimension` (`Surface.MaxDimension`), `maxDpi` (`SourceLoader.MaxDpi`), `qualityMin`, `qualityMax`, and
  `qualityDefault` (`ImageWriter`), `scadMaxTriangles` (`ScadLimits.Default`), the seven `vlMax*` limits
  (`VegaLiteLimits.Default`), the four `mmdMax*` limits (`MermaidLimits.Default`), the five `pumlMax*` limits
  (`PlantUmlLimits.Default`), the seven `mmlMax*` limits (`MathMlLimits.Default`), `fragmentMaxLength` and
  `fragmentMaxParameters` (`ImageFragment`), and the five `positions*` limits (`PositionCollector`, `PositionSelection`,
  `PositionSelector`, and `ImagePosition`).
- `Source` (public, immutable): `HelpSource.FromAssembly(typeof(CanvasNetHelp).Assembly).WithTokens(Tokens)`. Creating
  it reads nothing.
- `Catalog` (public): a `Lazy<HelpCatalog>` with `ExecutionAndPublication`, declared after the lists it reads.
- `HelpTags` (static class): the constants `Svg`, `Mermaid`, `PlantUml`, `MathMl`, `Scad`, `VegaLite`, `Pdf`, `Pptx`,
  `Vsdx`, and `Raster` (the format tags), `Authoring`, `Design`, `Composition`, `Diagnostics`, `Limits`, `Hosting`, and
  `FileAccess` (the scope tags, written `file-access`), and the read-only lists `FormatTags`, `ScopeTags`, and `All`.

**Embedded resources**: the Viewer project references `DemaConsulting.HelpCards` and sets `HelpCardsDirectory` to
`Help\Cards`; the build targets of the package embed every file below that folder with `WithCulture="false"`:

| Source file (under `Help/Cards/`) | Logical resource name |
| --- | --- |
| `_source.md` | `DemaConsulting.CanvasNet.Viewer.Help/_source.md` |
| `<path>.md`, in folders that mirror the tree | `DemaConsulting.CanvasNet.Viewer.Help/<path>.md` |
| `<root>/<file>`, the example sources next to their cards | `DemaConsulting.CanvasNet.Viewer.Help/<root>/<file>` |

The separator of a folder may be `\` in the logical name; HelpCards reads it as `/`.

#### Key Methods

- **Configure(builder)**: adds `Source` to a `HelpCatalogBuilder` and sets the tag vocabulary
  (`TagVocabulary(HelpTags.All, HelpTags.ScopeTags)`), then returns the builder for chaining. It is the one place that
  says how the library cards are composed, so `Catalog`, the tool's combined catalog, and the validation tests compose
  them the same way. A null builder is an `ArgumentNullException`.
- **Catalog**: `HelpCatalog.Create(b => Configure(b).ExpectedRoots(RootIds))`, built on the first read and returned to
  every caller afterwards.
- **CreateTokens** (private): reads the constants of the code and formats them with `IFormattable.ToString(null,
  CultureInfo.InvariantCulture)`.

The behavior of the catalog (resolving a path through names and aliases ignoring case, filtered views in which a hidden
card is never listed or linked, the plain, command, and Markdown link styles and the link formatter of a host, the text,
Markdown, and JSON renderings, search, and the composition of a host source that imports `Source`) is that of HelpCards.

#### Error Handling

A defective embedded card (a malformed front matter, an unknown key, tag, token, link, or example file, a tree that does
not match the folders, or roots other than `RootIds`) makes `HelpCatalog.Create` throw a `HelpCatalogException` that
lists every problem with its file, line, and rule; the first read of `Catalog` throws it. The unit tests build the
catalog and validate it with CanvasNet's options, so a defect fails the build rather than a user. A filter with an
unknown tag is an `ArgumentException` of HelpCards.

#### Dependencies

- `DemaConsulting.HelpCards` (OTS): `HelpSource`, `HelpCatalogBuilder`, and `HelpCatalog`, and the build targets that
  embed the cards.
- The constants of the core `Canvas`, `ImageSources`, and `Positions` subsystems, of the `Imaging` subsystem, and of the
  Scad, VegaLite, Mermaid, PlantUml, and MathMl packages, read by `CreateTokens`.

#### Callers

- Library hosts, through `Catalog`, `Source`, and `Configure`.
- The `HelpTopics` unit of the `CanvasNetTool` system, whose source imports `Source` and whose catalog is configured
  with `Configure`.
