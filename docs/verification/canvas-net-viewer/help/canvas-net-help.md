### CanvasNetHelp

#### Verification Approach

`CanvasNetHelp` is verified with unit tests in `CanvasNetHelpTests.cs` and its partial files
(`CanvasNetHelpTests.Content.cs`, `.Library.cs`, `.Guidance.cs`, `.Positions.cs`, `.Triggers.cs`, `.Bindings.cs`, and
`.MathMl.cs`) in the Viewer test project, at two levels:

- **The unit** (`CanvasNetHelpTests.cs`): the tag vocabulary, the 36 tokens (the constants of the code), the source (a
  catalog of the source alone has the library roots and an example read from its file next to its card), the catalog
  (one instance on every thread, the library roots and tags, no command example), the configuration (a host card with a
  tag outside the vocabulary or without a scope tag fails the catalog with the rule of HelpCards), a filter with an
  unknown tag, and the strict validation of every library card with the validator of HelpCards and CanvasNet's options
  (6,000 and 3,500 characters, summaries of 100 and 60 characters, 80 columns outside a fenced code block and 120
  inside, every warning an error, and the rule `NoCommandLineTextRule` of the test project).
- **The library content** (the partial files): the real `CanvasNetHelp.Catalog` is checked against the code (the error
  codes are those of `ErrorCodes`, the cited diagnostic identifiers exist, the extensions are those of
  `CanvasNetViewer`, the position limits are those of the `Positions` subsystem, and the limits card quotes the live
  constants), against the renderer (every source example and snippet renders in strict mode without a diagnostic, and
  the advice of the style, composition, framed-figure, arrows, positions, and bindings cards is checked by rendering),
  and against the Mermaid, PlantUML, and MathML user guides (row for row). The examples are rendered through the public
  API: `CanvasNetViewer.Render` with `ViewerRenderOptions.ThrowOnUnsupportedFeature`, and, where the test needs the
  operations the tool uses (the embed operations of a snippet, the strict failure with the code `unsupported-feature`, a
  theme), `SourceLoader.Load` with `SourceOptions.Strict`.

The mechanics of the cards (the card format and its errors, links, views, extensions, and renderings) belong to the OTS
library `DemaConsulting.HelpCards` and are not repeated here (see *DemaConsulting.HelpCards OTS Verification*); the
content tests exercise them on the real cards. `HelpCardText.cs` is a test helper that reads the `Body` of a library
card, its bullets and sections, the embedded files, and saves an example and its sibling files into a temporary folder;
`NoCommandLineTextRule.cs` is the validator rule against command-line text. Nothing is mocked: the libraries the
examples use are the real format packages.

#### Test Environment

The guide-parity tests read `docs/user_guide/canvas-net-mermaid.md`, `docs/user_guide/canvas-net-plant-uml.md`, and
`docs/user_guide/canvas-net-math-ml.md`, found by walking up from the test output folder to the folder that holds
`CanvasNet.slnx` (`build.ps1 -Changed` selects the Viewer tests when any of these guides changes). The rendering tests
write the examples and their sibling files to temporary folders that each test deletes.

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- The tokens are the 36 constants of the code, the source and the catalog hold exactly the library cards with the
  library roots in order, and the catalog is one instance on every thread.
- The tag vocabulary is applied to the library cards, to a host's cards, and to filters.
- The library cards pass the strict validation: the budgets, the column limits, the headings, the fences, no unused
  token, and no command-line text, with no warning.
- The folders mirror the tree, the example names are unique per root, and every embedded file is used.
- The library cards state only what the code, the renderer, and the user guides confirm.

#### Test Scenarios

**The unit.** **HelpTags_All_IsTheFormatTagsThenTheScopeTags**: `All` is `FormatTags` then `ScopeTags`, each once.
**CanvasNetHelp_Tokens_AreTheCodeConstants**: there are 36 tokens, each the formatted constant of the code, and no
`schemaVersion`. **CanvasNetHelp_Source_LoadsTheEmbeddedCardsAndExamples**: a catalog of `Source` alone is named
`canvasnet-viewer`, has the library roots, and gives the `ohm` example of `mathml/basics` with the file name `ohm.mml`
and its MathML content. **CanvasNetHelp_Catalog_IsThreadSafe_AndReturnsTheSameInstance** and
**CanvasNetHelp_Default_ConcurrentReads_GiveTheSameResults**: `Catalog` is one instance on every thread, and parallel
`Find`, `Filter`, `Render`, and `RenderIndex` give the results of a single thread (no timing is involved).
**CanvasNetHelp_Catalog_HasTheLibraryRootsTagsAndNoCommandExamples**: the library roots are in order, every card belongs
to the library source and has a scope tag, each format tree carries its own format tag and no other, and no example is a
command. **CanvasNetHelp_Configure_AppliesTheTagVocabulary** (a theory): a host card with the tag `nonsense` fails with
`tag-unknown`, and one without a scope tag with `tag-required`.
**CanvasNetHelp_Catalog_Filter_UnknownTag_ThrowsArgumentException**: a filter with an unknown tag is refused.
**CanvasNetHelp_LibraryCards_PassTheStrictValidation**: the validator reports no problem for the library cards with
CanvasNet's options.

**Tags.** **CanvasNetHelp_Default_Tags_AreFromTheClosedVocabulary_AndEveryCardHasAScopeTag**: every tag of every library
card is in the vocabulary, at most once, each card has a scope tag, and every tag of the vocabulary is used.

**Views and rendering of the real cards.** **CanvasNetHelp_DefaultFiltered_NeverPointsAtAHiddenCard** (a theory over
five filters of the real catalog): the roots and children follow the nearest-visible rule, hidden cards are not found,
and the related cards, the links a host formats (a card link to a visible card; an example link to an example whose
owner is visible), the library text links, the text, the JSON, and the index name only visible cards.
**CanvasNetHelp_DefaultFiltered_VisibleCardBelowAHiddenRoot_IsARootFoundByItsPath**: in a `design` view `mermaid` is
hidden and `mermaid/style` is a root, without a parent in the view, found by its path and through the alias `mmd`.
**CanvasNetHelp_Default_RenderTextAndJson_AreDeterministicPlainAndComplete** (a theory over every library card): the
text is the same twice, plain, starts with the title, contains the body, keeps its lines at 80 columns (120 in a fence),
and lists the children, related cards, and examples; the JSON is one line with the card's data. **Structure of the
library content.** **CanvasNetHelp_Default_EveryCardLoads_WithTitleSummaryAndBody**,
**CanvasNetHelp_Default_Find_EveryLibraryAlias_ResolvesIgnoringCase**,
**CanvasNetHelp_Default_Tree_ChildrenAndSeeAlsoResolve**, **CanvasNetHelp_Default_Resources_AreExactlyTheLibraryCards**,
**CanvasNetHelp_Default_Children_AreTheCardFilesOfTheFolder**, and
**CanvasNetHelp_Default_ChildNames_AreUniqueAmongSiblings**: every library card loads with a title, a summary, and a
body; every alias of a root resolves ignoring case; every child and related card resolves and every card is reachable;
the embedded card files are exactly the cards and `_source.md`, whatever path separator the build used, and no C# file
is embedded; the children of every card are exactly the card files of its folder; and sibling names are unique.
**CanvasNetHelp_Default_Text_HasNoUnresolvedTokens_AndQuotesTheLiveLimits**: no text holds a token, and the limits card
quotes the live constants.

**No command-line text.** **CanvasNetHelp_LibraryCards_HaveNoCommandLineText**: no title, summary, body, example
description, or example content of a library card names the tool as a word or a `--option` (the one allowed text is the
namespace `https://demaconsulting.com/canvasnet` of the position bindings), and no example is a command.
**CanvasNetHelp_LibraryCards_CommandLineTextScan_FindsWhatItLooksFor**: a negative control: every allowed text occurs,
and the scan finds the tool name and an option in text that has them but not the Mermaid edges `--o` and `--x`.

**Examples.** **CanvasNetHelp_Default_Examples_NamesUniqueKindsValidAndSourcesNonEmpty**: every library example is a
source or snippet with content and the extension of its format, names are unique per root, and every embedded example
file is used once. **CanvasNetHelp_SourceExamples_RenderStrictWithoutDiagnostics** (a theory over every source example):
rendered with `CanvasNetViewer.Render` in strict mode, no diagnostic.
**CanvasNetHelp_Snippets_RenderStrictWithTheirSiblingFiles** (a theory over every snippet) and
**CanvasNetHelp_MermaidComposition_PicturesSnippet_RendersStrictWithItsSiblingFiles**: once their sibling files exist,
the snippets load in strict mode without a diagnostic with one embed operation per link, in order.

**Format content.**
**CanvasNetHelp_FormatCards_SubtreesHaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples**: the tree of
every format has the five sections with bullets, and every drawing format at least two examples.

**Style and composition guidance.** **CanvasNetHelp_DrawingFormats_HaveStyleAndCompositionCards**,
**CanvasNetHelp_StyleAndCompositionHubs_LinkBothWays**, **CanvasNetHelp_MermaidStyleCard_EveryListedThemeRenders** (a
theory over the five themes), **CanvasNetHelp_VegaLiteStyledExample_ConfigChangesThePicture**,
**CanvasNetHelp_StyleAdvice_TransparentChartInsideSvg_ShowsThePage**,
**CanvasNetHelp_FramedFigureCard_IsLinkedFromTheCompositionAndAnchorCards**,
**CanvasNetHelp_FramedFigureExample_CalloutsEndOnTheModelAnchors**, and
**CanvasNetHelp_FramedFigureAdvice_PictureAloneAtTheBoxSize_IsThePictureTheFigureEmbeds** (a theory over a diagram, a
chart, and a model): the cards exist and link both ways, every listed theme renders, every config key of the styled
chart changes the picture, a transparent chart shows the page of the SVG figure, the callouts of the framed example end
on the model's anchors, and a picture rendered alone at the size of its box equals the picture the figure embeds (within
a tolerance of 8).

**Arrows.** **CanvasNetHelp_ArrowsCard_IsLinkedBothWaysAndTeachesMarkers** and
**CanvasNetHelp_ArrowsExample_TipsLandOnTheTargetEdges**: the card resolves by its aliases, is linked both ways, and
teaches `orient="auto"`, `auto-start-reverse`, and `refX="10"`; every rendered head lands on the edge of its target box.

**Image links and fragments.** **CanvasNetHelp_SvgEmbedding_HrefFileTypes_MatchTheViewerExtensions**,
**CanvasNetHelp_SvgEmbedding_ImageLinks_StateSizeLinkRuleDiagnosticsAndExample**,
**CanvasNetHelp_SvgFragments_PageFragment_DocumentsGrammarPerFormatCodesAndStrict**,
**CanvasNetHelp_FormatAndLimitsCards_DocumentFragmentsAndTheirLimits**,
**CanvasNetHelp_ScadAndVegaLiteCards_PointToImageHrefLinking**, and
**CanvasNetHelp_ScadViewsAdvice_VprDefine_EqualsAzimuthAndElevation**: the listed file types are exactly
`CanvasNetViewer.Extensions` (and `.json` alone is not linkable); the size rule, link rule, codes, strict mode, and
example are stated; the page fragment, its parameters, codes, and live limits are documented; the scad and vega-lite
cards point at image linking; and `def.$vpr=[70,0,45]` draws exactly what an azimuth of 45 and an elevation of 20 draw,
also in an SVG link.

**Consistency with the code.** **CanvasNetHelp_ErrorsCodes_ListsEveryLibraryErrorCode**: the `errors/codes` items are
every `ErrorCodes` value except `operation-failed`, `threshold-failed`, and `pandoc-filter-error`, and nothing else.
**CanvasNetHelp_CitedDiagnosticIds_ExistInTheLibraries**: every cited identifier is triggered by the trigger table, an
error code, a code constant of the resolver or the flattener, a render-tested warning, or a literal of a library.
**CanvasNetHelp_DiagnosticTriggers_ProduceTheCitedIds**,
**CanvasNetHelp_DiagnosticTriggers_ImageFragmentCodes_AreReportedWithTheirSeverity**, and
**CanvasNetHelp_DiagnosticTriggers_Strict_FailsWithUnsupportedFeature** (theories): each cited identifier is reported
for its trigger, each image-link code with its severity (strict mode fails for exactly the two that leave the image
out), and strict mode turns the reports of a model and a chart into `unsupported-feature`.

**Mermaid and PlantUML.** **CanvasNetHelp_MermaidGanttAdvice_ThemeCssStylesTheGridNotGridColor**: `gridColor` changes no
pixel, and the card's `themeCSS` rules recolor the grid lines and resize the tick labels.
**CanvasNetHelp_MermaidCards_DiagramSyntaxItems_MatchUserGuide** and
**CanvasNetHelp_PlantUmlCards_DiagramSyntaxItems_MatchUserGuide**: the syntax, relation, tag, setting, and supported
bullets, and the code lists, equal the user guides item for item and in order, code spans included. For PlantUML the
`### Supported Sequence Syntax`, `### Supported Activity Syntax`, `### Supported Class Syntax`,
`### Supported Object Syntax`, `### Supported State Syntax`, `### Supported Use Case Syntax`,
`### Supported Component Syntax`, and `### Supported Deployment Syntax` lists (at least ten each, four for objects,
eight for states, six for use cases and components, and four for deployments) are the `## Supported` bullets of
`plantuml/sequence`, `plantuml/activity`, `plantuml/class`, `plantuml/object`, `plantuml/state`, `plantuml/usecase`,
`plantuml/component`, and `plantuml/deployment`.

**MathML.** **CanvasNetHelp_MathMlCards_FencedEquations_RenderStrictWithoutDiagnostics** and
**CanvasNetHelp_MathMlGuide_FencedEquations_RenderStrictWithoutDiagnostics**: every whole equation (a fenced `xml` block
or a Pandoc `{.mathml …}` block, including the copies shown inside a `markdown` block, that starts with `<math`) of the
MathML cards and of the MathML user guide renders through `CanvasNetViewer.Render` in strict mode with a picture and
without a diagnostic (fragments of single elements are not rendered), and the guide's Pandoc blocks are found.
**CanvasNetHelp_MathMlGuide_RenderingExample_StatesTheMeasuredNumbers**: the equation of the guide's rendering example
(Example 78), read from the guide, is parsed and measured, and the guide states what the library reports: the display
and element count (`Block, 22 elements`), the natural size and baseline (`138x44, baseline 32`), the pixel size at scale
3 (`413x131 pixels`), and the inline size (`96x33`) in the library block and in the Viewer block.
**CanvasNetHelp_MathMlComposition_FigureBlocks_RenderStrictWithTheEquation**: the SVG figure, the Mermaid flowchart, and
the Vega-Lite chart of `mathml/composition` render in strict mode beside the quadratic example without a diagnostic,
each drawing the equation once, and the natural size the card quotes is the size a probe reports.
**CanvasNetHelp_MathMlDiagnostics_CitedCodes_AreReportedByTheirTriggers**: every `mathml-*` code that
`mathml/diagnostics` and `mathml/unsupported` list is reported by a trigger written in the test - as a warning note with
the code as its detail, as an unsupported note, or as the error the card names with the code as its detail code; only
the generic warning code and the code of an internal layout failure have no trigger.
**CanvasNetHelp_MathMlPitfalls_StretchyFalseParentheses_AreNarrower**: on the real layout, `f(x)` with
`stretchy="false"` parentheses is narrower than with stretchy ones, and `= -40` with the negative number in its own row
is narrower than with the minus spaced as a binary operator, as `mathml/pitfalls` says.
**CanvasNetHelp_MathMlCards_CoverTheAuthoringTasks**: each authoring-task card (basics, fractions, scripts,
large-operators, fences, matrices, aligned, units, symbols, accents, formulas, style, and pitfalls) exists below
`mathml` with at least one source example, the root links the pitfalls and composition cards, the format cards have
their five sections, and every example of the tree is a source example in a `.mml` file.
**CanvasNetHelp_MathMlCards_ElementAttributeAndCodeItems_MatchUserGuide**: the `## Supported` bullets of
`mathml/elements` and `mathml/attributes` equal the items of the guide's element and attribute lists, item for item and
in order, code spans included, and every item of the guide's code list is a bullet of `mathml/unsupported` or
`mathml/diagnostics`.

**Positions and bindings.** **CanvasNetHelp_PositionsCard_IsInTheTreeAndLinked**,
**CanvasNetHelp_PositionsLimits_AreTheLimitsOfTheCode**,
**CanvasNetHelp_PositionsVegaLite_DataFieldExample_SelectsThePoint**,
**CanvasNetHelp_PositionsSelectors_Examples_MatchAsTheCardsSay**,
**CanvasNetHelp_PositionsSelectors_MetadataExamples_SelectOneItem**,
**CanvasNetHelp_PositionsMermaid_KindsPerType_AreTheKindsReported**,
**CanvasNetHelp_FramedFlowExample_CalloutsEndOnPositions**, **CanvasNetHelp_SvgBindings_ListsTheNamespaceAndEveryCode**,
and **CanvasNetHelp_FramedFigure_RecommendsBindingsFirst**: the positions root follows composition with its four cards
and is linked both ways; its limits are the code's; the data-field, selector, and metadata examples match as the cards
say; each Mermaid type reports the listed kinds; every callout of the framed-flow example is bound to a selector, its
fallback numbers and the numbers `SvgFlattener` bakes match the position computed independently; the bindings card names
the namespace and every code; and the framed-figure card recommends a binding, then the reported positions, then a
labeled grid, in that order.
