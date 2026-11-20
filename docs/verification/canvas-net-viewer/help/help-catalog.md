### HelpCatalog

#### Verification Approach

`HelpCatalog` is verified with unit tests in `HelpCatalogTests.cs` and its partial files (`HelpCatalogTests.Content.cs`,
`.Library.cs`, `.Guidance.cs`, `.Positions.cs`, `.Triggers.cs`, and `.Bindings.cs`) in the Viewer test project, at two
levels:

- **The mechanics** (`HelpCatalogTests.cs`): small in-memory catalogs built through the internal seams
  `HelpCatalog.Create` (card texts, example files, and a root order) and `HelpCatalog.ExtendWith` (a host's card texts),
  which exercise the loading and its errors, the tags, the links, the views, the host links, the extensions, and the
  renderings with cards whose content the test controls. The public `Extend` is exercised on an assembly without help
  resources.
- **The library content** (the partial files): the real `HelpCatalog.Default` is loaded and checked against the code
  (the tokens are the constants, the error codes are those of `ErrorCodes`, the cited diagnostic identifiers exist, the
  extensions are those of `CanvasNetViewer`, the position limits are those of the `Positions` subsystem), against the
  renderer (every source example and snippet renders in strict mode without a diagnostic, and the advice of the style,
  composition, framed-figure, arrows, positions, and bindings cards is checked by rendering), and against the Mermaid
  and PlantUML user guides (row for row). The examples are rendered through the public API: `CanvasNetViewer.Render`
  with `ViewerRenderOptions.ThrowOnUnsupportedFeature`, and, where the test needs the operations the tool uses (the
  embed operations of a snippet, the strict failure with the code `unsupported-feature`, a theme), `SourceLoader.Load`
  with `SourceOptions.Strict`.

`HelpCardText.cs` is a test helper that reads the `Markdown` of a library card, its bullets and sections, and saves an
example and its sibling files into a temporary folder. Nothing is mocked: the libraries the examples use are the real
format packages.

#### Test Environment

The guide-parity tests read `docs/user_guide/canvas-net-mermaid.md` and `docs/user_guide/canvas-net-plant-uml.md`,
found by walking up from the test output folder to the folder that holds `CanvasNet.slnx` (`build.ps1 -Affected`
selects the Viewer tests when either guide changes). The rendering tests write the examples and their sibling files
to temporary folders that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- A malformed card, example, token, tree, tag list, or link fails to load with a message that names the file and, where
  there is one, the line; a valid catalog builds the documented tree and card data in read-only lists.
- A view shows exactly the cards its tags select, lifts the visible cards below a hidden card, finds a visible card
  through hidden ones and no hidden card, and never points at a hidden card in its roots, children, related cards,
  links, text, or JSON.
- A host's overlay and cards are merged as documented, and a host card that does not fit is refused with the reason.
- The renderings are deterministic, plain, wrapped, and complete.
- The library cards fit the size budget, carry tags from the closed vocabulary with a scope tag, carry no command-line
  text, and state only what the code, the renderer, and the user guides confirm.

#### Test Scenarios

**Loading and the card format.** **HelpCatalog_Create_ValidCards_BuildsTheTree**: a test catalog lists its cards depth
first, its roots in order, each card's children, related cards, parent, name, title, summary, tags, aliases, and its
example with the file content normalized, in lists that are not `List<T>`.
**HelpCatalog_Create_MalformedCard_ThrowsWithTheFileAndTheReason** (a theory): a link to an unknown card, to an
unknown example of a card, and to an unknown example of the containing card, text that starts like a link (including
an upper-case start), missing tags, an unknown tag, a card without a scope tag, a repeated tag, overlay keys and
`extends` in a library card, a related card that does not exist, an unknown token, a repeated example name, a missing
example file, children that are not the files of the folder, wrong roots, and a badly named file each fail with the
file (and the line, where there is one) and the reason. **HelpCatalog_Create_SemanticErrors_ThrowWithFileAndLine**: an
unknown key, a missing title, a list where text is expected, an example of an unknown kind, without a name, and with an
unknown key each name the line.
**HelpCatalog_LibraryTokens_AreTheCodeConstants**: every library token is the formatted constant of the code.

**Lookup.** **HelpCatalog_Find_PathsAndAliases_ResolveOrGiveNull** (a theory): paths with `/` or white space, aliases,
and any case resolve; an empty path and an unknown segment give null.

**Tags.** **HelpTags_All_IsTheFormatTagsThenTheScopeTags**: `All` is `FormatTags` then `ScopeTags`, each once.
**HelpCatalog_Default_Tags_AreFromTheClosedVocabulary_AndEveryCardHasAScopeTag**: every tag of every library card is in
the vocabulary, at most once, each card has a scope tag, and every tag of the vocabulary is used.
**HelpCatalog_Default_HasTheLibraryRootsTagsAndNoCommandExamples**: the library roots are in order, every card has a
scope tag, each format tree carries its own format tag and no other, and no example is a command.

**Links.** **HelpCatalog_Markdown_Links_RenderInLibraryForm**: `[[a/b]]`, `[[a/b#name]]`, and `[[#name]]` are written as
"the card `a/b`", "the example `name` of the card `a/b`", and "the example `name`", and link text in a code span or a
fenced block stays literal.

**Views and hidden cards.** **HelpCatalog_Filter_IncludeExclude_SelectsCardsByTags**,
**HelpCatalog_Filter_UnknownTag_ThrowsArgumentException**, and **HelpCatalog_Filter_OfAView_Composes**: a card is
visible with an included tag and no excluded tag, an unknown or null tag is an `ArgumentException`, and a filter of a
view keeps the cards visible in both. **HelpCatalog_Filter_HiddenParent_LiftsVisibleChildrenToRootsAndChildren**: the
visible children of a hidden card move up to its nearest visible ancestor or the roots.
**HelpCatalog_Filter_Find_HiddenIsNull_VisibleChildOfHiddenParentResolves**: a visible card below hidden ones is found
by its full path, a hidden card is not. **HelpCatalog_Filter_HiddenLinkTargets_BecomePlainText**: a link to a hidden
card becomes its title, a link to a hidden example "an example", and a hidden related card is left out of the card and
its footer. **HelpCatalog_CardMembers_HiddenCard_ThrowArgumentException**: the members that take a card reject a card
the view hides. **HelpCatalog_DefaultFiltered_NeverPointsAtAHiddenCard** (a theory over five filters of the real
catalog): the roots and children follow the nearest-visible rule, hidden cards are not found, and the related cards,
the host links, the library text links, the text, the JSON, and the index name only visible cards.
**HelpCatalog_DefaultFiltered_VisibleCardBelowAHiddenRoot_IsARootFoundByItsPath**: in a `design` view `mermaid` is
hidden and `mermaid/style` is a root found by its path and through the alias `mmd`, with its parent kept.

**Host links.** **HelpCatalog_FormatMarkdown_CallsTheHostOnlyForVisibleLinks**: the host is called once for each visible
link, in order, with the card, the example, and its owner, and never for a hidden one.
**HelpCatalog_FormatMarkdown_ExampleLinkThroughAHiddenCard_NamesTheOwner**: a link to a visible example written with a
hidden card reaches the host with the owner of the example as the card.

**Extensions.** **HelpCatalog_Extend_OverlayAppendsBodySeeAlsoExamplesAndOverridesTitleSummary**: an overlay appends its
body, related cards, and examples and replaces the title, a host card starts a new root, the host token is replaced,
and the extended catalog is unchanged. **HelpCatalog_Extend_MisfitExtension_ThrowsWithTheReason** (a theory): an
overlay of an unknown card or of another path, a host card in the tree of a library card (with the advice to write it
in the catalog's assembly or make it an overlay, also when it lacks the card keys such as `tags`), the overlay keys
`tags` and `children`, a host link to an unknown card, an overlay example whose name repeats one of the tree, an overlay
related card that is the card itself, and a host root whose alias repeats a library root's alias fail with the reason.
**HelpCatalog_Extend_TokenCollisionOrFilteredView_Throws**: a host token that redefines a library token and an
extension of a filtered view fail. **HelpCatalog_Extend_AssemblyWithoutCards_KeepsTheCards**: the public `Extend` reads
the resources of an assembly (one without help resources leaves the cards unchanged).

**Rendering.** **HelpCatalog_RenderText_Card_IsPlainWrappedTextWithFooters**,
**HelpCatalog_RenderJson_Card_HasEveryField**, and **HelpCatalog_RenderIndex_ListsTheVisibleTree**: the text has the
heading, tags, body, and footers of what the view shows, LF only, no trailing newline, and lines of at most 80 columns;
the JSON has every field and only visible cards; the index is indented by visible depth, as text and as JSON.
**HelpCatalog_Default_RenderTextAndJson_AreDeterministicPlainAndComplete** (a theory over every library card): the text
is the same twice, plain, starts with the title, contains the body, keeps its lines short, and lists the children,
related cards, and examples; the JSON is one line with the card's data.

**Thread safety.** **HelpCatalog_Default_IsThreadSafe_AndReturnsTheSameInstance** and
**HelpCatalog_Default_ConcurrentReads_GiveTheSameResults**: `Default` is one instance on every thread, and parallel
`Find`, `Filter`, and `RenderText` give the results of a single thread (no timing is involved).

**Structure of the library content.** **HelpCatalog_Default_EveryCardLoads_WithTitleSummaryAndBody**,
**HelpCatalog_Default_Find_EveryLibraryAlias_ResolvesIgnoringCase**,
**HelpCatalog_Default_Tree_ChildrenAndSeeAlsoResolve**, **HelpCatalog_Default_Resources_AreExactlyTheLibraryCards**,
**HelpCatalog_Default_ChildNames_AreUniqueAmongSiblings**,
**HelpCatalog_Default_Bodies_DoNotUseReservedHeadingsOrTopLevelHeadings**, and
**HelpCatalog_Default_Cards_FitTheSizeBudget**: every library card loads with a title, a summary, and a body; every
alias of a root resolves ignoring case; every child and related card resolves and every card is reachable; the embedded
card files are exactly the cards, whatever path separator the build used; sibling names are unique; no body uses a
top-level or footer heading, and every fence is closed and names its language; and every card fits 6,000 characters
(3,500 for a root with cards below it) with a summary of at most 60 (root) or 100 characters.
**HelpCatalog_Default_Text_HasNoUnresolvedTokens_AndQuotesTheLiveLimits**: no text holds a token, and the limits card
quotes the live constants.

**No command-line text.** **HelpCatalog_LibraryCards_HaveNoCommandLineText**: no title, summary, body, example
description, or example content of a library card names the tool as a word or a `--option` (the one allowed text is the
namespace `https://demaconsulting.com/canvasnet` of the position bindings), and no example is a command.
**HelpCatalog_LibraryCards_CommandLineTextScan_FindsWhatItLooksFor**: a negative control: every allowed text occurs,
and the scan finds the tool name and an option in text that has them but not the Mermaid edges `--o` and `--x`.

**Examples.** **HelpCatalog_Default_Examples_NamesUniqueKindsValidAndSourcesNonEmpty**: every library example is a
source or snippet with content and the extension of its format, names are unique per root, and every embedded example
file is used once. **HelpCatalog_SourceExamples_RenderStrictWithoutDiagnostics** (a theory over every source example):
rendered with `CanvasNetViewer.Render` in strict mode, no diagnostic.
**HelpCatalog_Snippets_RenderStrictWithTheirSiblingFiles** (a theory over every snippet) and
**HelpCatalog_MermaidComposition_PicturesSnippet_RendersStrictWithItsSiblingFiles**: once their sibling files exist, the
snippets load in strict mode without a diagnostic with one embed operation per link, in order.

**Format content.**
**HelpCatalog_FormatCards_SubtreesHaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples**: the tree of every
format has the five sections with bullets, and every drawing format at least two examples.

**Style and composition guidance.** **HelpCatalog_DrawingFormats_HaveStyleAndCompositionCards**,
**HelpCatalog_StyleAndCompositionHubs_LinkBothWays**, **HelpCatalog_MermaidStyleCard_EveryListedThemeRenders** (a theory
over the five themes), **HelpCatalog_VegaLiteStyledExample_ConfigChangesThePicture**,
**HelpCatalog_StyleAdvice_TransparentChartInsideSvg_ShowsThePage**,
**HelpCatalog_FramedFigureCard_IsLinkedFromTheCompositionAndAnchorCards**,
**HelpCatalog_FramedFigureExample_CalloutsEndOnTheModelAnchors**, and
**HelpCatalog_FramedFigureAdvice_PictureAloneAtTheBoxSize_IsThePictureTheFigureEmbeds** (a theory over a diagram, a
chart, and a model): the cards exist and link both ways, every listed theme renders, every config key of the styled
chart changes the picture, a transparent chart shows the page of the SVG figure, the callouts of the framed example end
on the model's anchors, and a picture rendered alone at the size of its box equals the picture the figure embeds (within
a tolerance of 8).

**Arrows.** **HelpCatalog_ArrowsCard_IsLinkedBothWaysAndTeachesMarkers** and
**HelpCatalog_ArrowsExample_TipsLandOnTheTargetEdges**: the card resolves by its aliases, is linked both ways, and
teaches `orient="auto"`, `auto-start-reverse`, and `refX="10"`; every rendered head lands on the edge of its target box.

**Image links and fragments.** **HelpCatalog_SvgEmbedding_HrefFileTypes_MatchTheViewerExtensions**,
**HelpCatalog_SvgEmbedding_ImageLinks_StateSizeLinkRuleDiagnosticsAndExample**,
**HelpCatalog_SvgFragments_PageFragment_DocumentsGrammarPerFormatCodesAndStrict**,
**HelpCatalog_FormatAndLimitsCards_DocumentFragmentsAndTheirLimits**,
**HelpCatalog_ScadAndVegaLiteCards_PointToImageHrefLinking**, and
**HelpCatalog_ScadViewsAdvice_VprDefine_EqualsAzimuthAndElevation**: the listed file types are exactly
`CanvasNetViewer.Extensions` (and `.json` alone is not linkable); the size rule, link rule, codes, strict mode, and
example are stated; the page fragment, its parameters, codes, and live limits are documented; the scad and vega-lite
cards point at image linking; and `def.$vpr=[70,0,45]` draws exactly what an azimuth of 45 and an elevation of 20 draw,
also in an SVG link.

**Consistency with the code.** **HelpCatalog_ErrorsCodes_ListsEveryLibraryErrorCode**: the `errors/codes` items are
every `ErrorCodes` value except `operation-failed`, `threshold-failed`, and `pandoc-filter-error`, and nothing else.
**HelpCatalog_CitedDiagnosticIds_ExistInTheLibraries**: every cited identifier is triggered by the trigger table, an
error code, a code constant of the resolver or the flattener, a render-tested warning, or a literal of a library.
**HelpCatalog_DiagnosticTriggers_ProduceTheCitedIds**,
**HelpCatalog_DiagnosticTriggers_ImageFragmentCodes_AreReportedWithTheirSeverity**, and
**HelpCatalog_DiagnosticTriggers_Strict_FailsWithUnsupportedFeature** (theories): each cited identifier is reported for
its trigger, each image-link code with its severity (strict mode fails for exactly the two that leave the image out),
and strict mode turns the reports of a model and a chart into `unsupported-feature`.

**Mermaid and PlantUML.** **HelpCatalog_MermaidGanttAdvice_ThemeCssStylesTheGridNotGridColor**: `gridColor` changes no
pixel, and the card's `themeCSS` rules recolor the grid lines and resize the tick labels.
**HelpCatalog_MermaidCards_DiagramSyntaxItems_MatchUserGuide** and
**HelpCatalog_PlantUmlCards_DiagramSyntaxItems_MatchUserGuide**: the syntax, relation, tag, setting, and supported
bullets, and the code lists, equal the user guides item for item and in order, code spans included. For PlantUML the
`### Supported Sequence Syntax`, `### Supported Activity Syntax`, `### Supported Class Syntax`,
`### Supported Object Syntax`, `### Supported State Syntax`, `### Supported Use Case Syntax`,
`### Supported Component Syntax`, and `### Supported Deployment Syntax` lists (at least ten each, four for objects,
eight for states, six for use cases and components, and four for deployments) are the `## Supported` bullets of
`plantuml/sequence`, `plantuml/activity`, `plantuml/class`, `plantuml/object`, `plantuml/state`, `plantuml/usecase`,
`plantuml/component`, and `plantuml/deployment`.

**Positions and bindings.** **HelpCatalog_PositionsCard_IsInTheTreeAndLinked**,
**HelpCatalog_PositionsLimits_AreTheLimitsOfTheCode**,
**HelpCatalog_PositionsVegaLite_DataFieldExample_SelectsThePoint**,
**HelpCatalog_PositionsSelectors_Examples_MatchAsTheCardsSay**,
**HelpCatalog_PositionsSelectors_MetadataExamples_SelectOneItem**,
**HelpCatalog_PositionsMermaid_KindsPerType_AreTheKindsReported**,
**HelpCatalog_FramedFlowExample_CalloutsEndOnPositions**, **HelpCatalog_SvgBindings_ListsTheNamespaceAndEveryCode**, and
**HelpCatalog_FramedFigure_RecommendsBindingsFirst**: the positions root follows composition with its four cards and is
linked both ways; its limits are the code's; the data-field, selector, and metadata examples match as the cards say;
each Mermaid type reports the listed kinds; every callout of the framed-flow example is bound to a selector, its
fallback numbers and the numbers `SvgFlattener` bakes match the position computed independently; the bindings card names
the namespace and every code; and the framed-figure card recommends a binding, then the reported positions, then a
labeled grid, in that order.
