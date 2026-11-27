## DemaConsulting.HelpCards

This document describes the integration and usage design for the `DemaConsulting.HelpCards` OTS software item.

### Purpose

`DemaConsulting.HelpCards` is DEMA Consulting's library for help that ships inside a library or tool as short Markdown
cards with YAML front matter. It is chosen so that the help cards of the Viewer library and of the `canvasnet` tool are
read, validated, composed, filtered, linked, searched, and rendered by one maintained library, with the same card
format, link styles, JSON documents, and agent tool as the other DEMA Consulting tools, instead of a bespoke engine in
CanvasNet. CanvasNet keeps only what is its own: which cards there are, the values of the limits they quote, the tag
vocabulary, its stricter rules, and the JSON contract of its tool.

### Features Used

- **Embedding** - the build targets of the package embed every file below `HelpCardsDirectory` as the resource
  `<AssemblyName>.Help/<path>`; the Viewer sets the folder `Help\Cards` and the tool `Commands\Help`.
- **Sources** (`DemaConsulting.HelpCards.Loading.HelpSource`) - `FromAssembly` (the embedded cards), `WithTokens` (the
  live limits of the code, and the tool's `schemaVersion`), and `Imports` (the tool's source imports the library source,
  so its overlays extend library cards with `extends:` and its links name them); `FromFiles` in tests. The `_source.md`
  file of each source gives its name (`canvasnet-viewer`, `canvasnet-tool`), schema 2 of the card format, and the order
  of its roots. The text `\{{` is the escape of a literal pair of opening braces (the hexagon node of a Mermaid
  mindmap).
- **Composition** (`DemaConsulting.HelpCards.Composition.HelpCatalogBuilder`) - `AddSource`, `TagVocabulary` (the
  vocabulary of `HelpTags`, at least one scope tag per card), `ExpectedRoots`, `ToolName`, and `ToolVersion`.
- **Catalog** (`DemaConsulting.HelpCards.Catalog.HelpCatalog`) - `Create` (a `HelpCatalogException` lists every defect),
  `Cards`, `Roots`, `Find`, `GetChildren`, `GetSeeAlso`, `FindExample`, `Filter`, `Render`, `RenderIndex`,
  `RenderExample`, `FormatBody`, and the card, example, and link types.
- **Presentation** (`DemaConsulting.HelpCards.Presentation.HelpRenderOptions`) - the text and JSON formats, the command
  link style with the template `canvasnet help {path}`, and a JSON envelope of the tool.
- **Command line** (`DemaConsulting.HelpCards.Integration.HelpCli` and `HelpCliOptions`) - the tool's help command:
  `CatalogFactory`, `CommandTemplate`, `IndexPreamble`, `IndexSections`, `FallbackResolver` (the tool's own subjects),
  `Output` and `Error` (writers of one run, forwarded to the tool's context), `JsonEnvelope`, `JsonError`, and
  `ExitCodes` (1 for every failure).
- **Testing** (`DemaConsulting.HelpCards.Testing`) - `HelpCardValidator.Validate` with `HelpValidationOptions` (the card
  and root-index budgets, the summary lengths, `MaxLineLength` and `MaxFenceLineLength`, `WarningsAreErrors`, and the
  host rule `IHelpCardRule` against command-line text in library cards); used by the unit tests only.

### Integration Pattern

`DemaConsulting.HelpCards` is a runtime dependency, referenced as a standard `PackageReference` with the same pinned
version in two projects. In `src/DemaConsulting.CanvasNet.Viewer/DemaConsulting.CanvasNet.Viewer.csproj` it is a normal
dependency of the Viewer package, because `CanvasNetHelp` exposes its `HelpSource`, `HelpCatalogBuilder`, and
`HelpCatalog` in the public API. In `src/DemaConsulting.CanvasNet.Tool/DemaConsulting.CanvasNet.Tool.csproj` it is in
the runtime dependencies, so the tool package bundles its assembly like the other runtime dependencies; the direct
reference is required because the embedding targets of the package apply only to a project that references it, not
through the project reference to the Viewer. The test projects reference it only transitively and embed nothing.

The library catalog is built once, on first use, by `CanvasNetHelp.Catalog`; the tool's combined catalog by
`HelpTopics.Catalog`. Each help command of the tool creates a `HelpCli` with writers of its own and the shared catalog.
There is no disposal requirement. The version reference is a NuGet minimum version, as for every package reference of
the repository, so a consumer of the Viewer package may resolve a later version of HelpCards; the Viewer and the tool
always reference the same version.

Three behaviors of `HelpCli` differ from the contract of the tool, so `CommandRegistry.ExecuteHelp` adds a little local
glue around it instead of waiting for an upstream change:

- **Equals form of option values** - `HelpCli` reads the value of an option only as the next word, while every other
  command of the tool also accepts `--name=value`. A help option that takes a value written in that form is split into
  two words before `HelpCli.Run` (`SplitInlineValues`); a flag with a value stays one word and remains an unknown
  option.
- **`id` of the example document** - `HelpCli` renders the document of one example from the card that holds the
  example (`CliDispatcher.ShowExample` in `Integration/CliDispatcher.Lookup.cs` passes that card to `RenderExample`),
  although `HelpCatalog.RenderExample` and the namespace documentation of the JSON documents
  (`Presentation/NamespaceDoc.cs`) give `id` as the card that was asked for. The tool writes the fields again with
  `id` set to the card that its path words name, in the same order (`FindRequestedCardId` and `WriteWithId`), so the
  document is the one the tool always wrote; `example.card` still names the card that holds the example.
- **Usage errors without a hint** - `CliArgumentParser` (`Integration/CliArgumentParser.cs`) reports several argument
  errors with no hint: an option without its value, an option given twice, `--example` or `--examples` without a card
  path, two options that cannot be combined, and `--tag`, `--source`, or `--all` with a card path. `HelpCliError` has
  no kind finer than `usage-error` to tell them apart. Every other usage error of the tool has a hint, so
  `CommandRegistry.ExecuteHelp` gives each non-catalog failure without a hint the general hint `HelpOptionsHint` (in
  the `JsonError` hook and for the text error), without matching messages.

The first two are reported upstream, and the missing hints are an upstream gap too. When a later version of HelpCards
accepts `--name=value`, writes the requested card as `id`, and gives every usage error a hint, the glue does nothing
and can be removed.
