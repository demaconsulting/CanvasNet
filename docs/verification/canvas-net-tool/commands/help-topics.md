### HelpTopics

#### Verification Approach

`HelpTopics` is verified with unit tests in these files:

- `HelpTopicsTests.cs`: the cards, their consistency with the code and the user guides, the examples, and the
  renderings.
- `HelpTopicsTests.FrontMatter.cs`: the front-matter parser.
- `HelpTopicsTests.Tree.cs`: the tree, the size budget, the addressing, and the loader's checks.
- `HelpTopicsTests.Guidance.cs`: the style and composition guidance, checked against the renderer.
- `HelpTopicsTests.Triggers.cs`: the diagnostic identifiers the cards cite.

The helper `HelpCardText` reads the Markdown of a card: the bullets under a heading, their terms (a leading code span
or bold text), the headings, and the text of a card and the cards below it with the lines of each paragraph joined.

The tests use the real embedded content and no mock: the tree is the shipped one, so a malformed or inconsistent card
fails the build. The content is checked against the things it describes, so that it cannot drift from the code:

- The option terms of every `## Options` section are looked up in the option table of `RenderCommand`.
- Every command line, in a command example or in a code block of a card, is checked against the default
  `CommandRegistry`: a known command, only options that the command or the tool declares, and for `canvasnet help` a
  path that names a card.
- The `errors codes` card is compared with the error code constants of the tool through reflection.
- The numbers (the largest image side, the resolution limit, the quality range, the triangle budget, and the Vega-Lite
  and Mermaid limits) are the tokens substituted from the live constants, and the `limits` card is checked to quote
  them.
- Every diagnostic identifier a card cites (`scad-...`, `vl-...`, `mermaid-...`, `pptx-...`, `vsdx-...`, and `pdf-...`)
  must exist. Either the **trigger-table test** produces it with a tiny model or chart run through `ToolHost` (and a
  second test proves that `--strict` turns the report into `unsupported-feature`), or it is an error code, or the
  **assembly-literal check** finds the literal part of the identifier in the UTF-16 text of a library assembly.
  Anything else fails the test, so a card cannot cite an identifier that does not exist.
- Every source example (OpenSCAD, Vega-Lite, Mermaid, and SVG) is written to a file and rendered through the real entry
  point (`ToolHost.Invoke`) with `--strict` (the **strict-clean example test**), and must succeed without a diagnostic.
- The **file types** the `svg embedding` card lists for `<image href>` are compared in both directions with
  `CanvasNetViewer.Extensions` (the extensions of the image sources the tool wires in) and with the extensions of the
  `formats` table. A plain `.json` must not be linkable, and `.vl.json` must be.
- Every **snippet** links files that are not there when it is printed alone, so the strict-clean example test skips it,
  and the **snippet test** renders each one instead. The snippets are `embed` and `embed-pages` of the `svg` tree,
  `pictures` of `mermaid composition`, and `report` of `svg composition`. The test reads the links: the `href` of each
  SVG `image`, or the `img` and `<img src>` of a Mermaid diagram. It writes each sibling file from the source example
  with that file name, else from the first source example with that extension, else from the PDF, PPTX, or VSDX fixture.
  It then renders the snippet with `--strict` and requires no diagnostic and one `embed` operation per link, in order.
- The bullets of `mermaid <type>` (`## Syntax`, `## Relations`, `## Tags`) and of `mermaid <type> settings`
  (`## Settings`) are compared, row for row and in order, with the class, state, entity relationship, pie, gantt,
  mindmap, packet, and git graph tables of the Mermaid user guide, code spans included. Every row of the guide's table
  of unsupported features and warnings must be a bullet of `mermaid unsupported` or `mermaid diagnostics` (the **guide
  parity test**), so the help that agents read and the guide that people read describe the same version.
- The `## Reference` bullets of `svg flatten` and the bullets of `files` are compared, word for word and in order, with
  the `#### Flatten Reference` and `#### File Access Reference` tables of the tool user guide.
- The **style advice** is checked against the renderer: each theme the `mermaid style` card lists draws the examples
  without a diagnostic, leaving out any `config` key of the styled Vega-Lite example changes the picture, and a chart
  with `"background": "transparent"` shows the page of an SVG drawing that embeds it, while one without it stays white.

The text and JSON renderings are checked for every card with theory tests. The interaction with the registry (`help`,
its paths, and its options) is verified by `CommandRegistryTests.cs` and at the subsystem level by
`CommandsSubsystemTests.cs`.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they run the tool through
`ToolHost`, which redirects the console. The example, guidance, and trigger tests write only to temporary directories
that they delete.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every card loads with a title, a summary, and a body; the roots are the topics in order, the catalog is depth first,
  and every root tree has an example.
- The front-matter parser reads its subset, and every construct outside it, every semantic error of a card, an unknown
  token, and a bad file name are errors that name the file (and the line).
- The embedded card files are exactly the cards; every child and See also entry resolves; every card is reachable; and
  names and aliases are unique among siblings and among the roots, and no root collides with a command, `help`, or
  `topics`.
- Every card is at most 6,000 characters (an index root at most 3,500), and every summary is one line of at most 60
  characters (a root) or 100.
- A path resolves by names and aliases at every level, ignoring case and with `/`; an unknown card below a card is a
  usage error that suggests the closest card and lists the cards at that level.
- No text contains an unresolved `{{token}}`, and the numbers quoted are the live constants.
- The tree of every format topic has filled `Supported`, `Unsupported`, `Options`, `Diagnostics`, and
  `Known differences` sections and at least two examples. Every drawing format has a style card and a composition card
  linked both ways with the `style` and `composition` hubs.
- Every example has a valid kind, a name unique below its root, a file used by exactly one example, and non-empty
  content with LF line endings and no trailing newline. Every command line uses a known command, options, and help
  card.
- Every option an `## Options` section lists is an option of `render` or a global option, and the `errors codes` card
  lists exactly the error codes of the tool.
- Every cited diagnostic identifier is triggered by a tiny input, is an error code, or exists in a library. Each
  trigger produces its identifier, and `--strict` turns a report into `unsupported-feature`.
- Every source example renders with `--strict` and no diagnostic, and every snippet does too once its sibling files
  exist, with one embed per link.
- The Mermaid, flatten, and file access bullets are the rows of the guides, in order.
- Every Mermaid theme the style card lists renders; every config key of the styled chart changes the picture; and a
  transparent chart shows the page of an SVG figure.
- The `svg framed-figure` card is linked both ways with the composition, style, and anchor cards; the callout arrows
  of its framed figure end on the anchors of the model; and a diagram, a chart, and a model rendered alone at the size
  of a box are the picture an SVG figure embeds in that box.
- The text of a card is deterministic and plain, starts with its title, prints the body as written, keeps lines of at
  most 80 columns (120 in a code block), and lists the cards below it, its See also links with their summaries, and its
  examples. A word longer than the width is not split.
- The JSON of a card has the envelope and the documented keys in order, on one line and in ASCII.
- An example is returned exactly as embedded, from its card and from its root, and an unknown example name is a usage
  error that lists the names.

#### Test Scenarios

**HelpTopics_All_EveryCardLoads_WithTitleSummaryAndBody**: the root cards are `TopicNames` in order, both in `All` and
in `Roots`; every card comes after its parent; every card has a title, a summary, and a body; and the tree of every
root has an example.

**HelpTopics_Find_EveryAlias_ResolvesIgnoringCase**: every documented root alias (`openscad`, `vegalite`, `vl`, `mmd`,
`powerpoint`, `visio`, the raster extensions, `file-access`, and `links`) resolves to its card in lower and upper case;
every name resolves to itself; the number of root aliases is exactly the documented number; and an unknown name and
a card that is not a root (`gantt`) resolve to nothing.

**HelpTopics_Names_DoNotCollideWithCommandsHelpOrEachOther**: the names and aliases of the root cards are unique, and
none equals a command, a command alias, `help`, or `topics`.

**HelpTopics_ChildNames_AreUniqueAmongSiblings**: the names and aliases of the cards below each card are unique,
ignoring case, and none is `topics`.

**HelpTopics_Text_HasNoUnresolvedTokens**: no string of any card contains `{{`; the token table holds the live values;
and the `limits` card quotes them.

**HelpTopics_FormatTopics_SubtreesHaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples**: the tree of each
of the eight format topics has a card with bullets under each of `## Supported`, `## Unsupported`, `## Options`,
`## Diagnostics`, and `## Known differences`, and at least two examples.

**HelpTopics_Examples_NamesUniqueKindsValidAndSourcesNonEmpty**: example names are unique below each root; content is
non-empty with LF line endings and no trailing newline; a command example starts with `canvasnet` and has no file name;
a source or snippet has a file name with the extension of its format; every embedded example file belongs to exactly
one example; and each drawing format has a source example.

**HelpTopics_SourceExamples_RenderStrictWithoutDiagnostics**: a theory over every source example, including the styled
Mermaid flowchart and the styled Vega-Lite chart: rendered with `--strict` (a model at 64 by 64 pixels), it succeeds
without a diagnostic and writes the picture.

**HelpTopics_Snippets_RenderStrictWithTheirSiblingFiles**: a theory over every snippet (`embed`, `embed-pages`,
`pictures`, and `report`): it links only supported file types, and with its sibling files it renders with `--strict`
without a diagnostic, with one `embed` operation per link in the order of the links.

**HelpTopics_MermaidTopic_PicturesSnippet_RendersStrictWithItsSiblingFiles**: the `pictures` snippet of
`mermaid composition`, with the `plate` and `bar` examples as `part.scad` and `chart.vl.json`, renders with `--strict`
without a diagnostic and embeds exactly `part.scad` and `chart.vl.json`, in that order.

**HelpTopics_MermaidTopic_DiagramSyntaxItems_MatchUserGuide**: for each of the eight diagram types, the rows of the
guide's tables under its heading (at least the documented number) are, in order and with their code spans, the bullets
of `mermaid <type>` (`Syntax`, `Relations`, `Tags`) and of `mermaid <type> settings` (`Settings`), with a final period
and the first letter of each part before a colon and a space compared ignoring case. Every row of the code table is a
bullet of `mermaid unsupported` or `mermaid diagnostics`.

**HelpTopics_SvgTopic_FlattenItems_MatchUserGuide** and **HelpTopics_FilesTopic_Items_MatchUserGuide**: the bullets
are, word for word and in order, `<Term>: <Meaning>` of the rows of the guide's reference tables (at least 7 and 16
rows), and every bullet of `files` has a term.

**HelpTopics_FilesTopic_DocumentsModesOptionsDiagnosticsAndPandoc**: the headings of `files` are Modes, Options, In
every mode, Diagnostics, and The Pandoc filter; its terms include the modes, the two options, the inputs, what is
never read, the limits, the two live denial codes, and the two variables, which `docs/filters/canvasnet.lua` reads;
and the `svg` and `errors` trees contain `canvasnet help files`.

**HelpTopics_SvgTopic_ImageHrefFileTypes_MatchTheResolverExtensions**: the extensions found in the `href file types`
bullet of `svg embedding` are unique, are exactly `CanvasNetViewer.Extensions`, and are exactly the extensions of the
`formats` table.

**HelpTopics_SvgTopic_ImageLinksSection_StatesSizeLinkRuleDiagnosticAndExample**: the `svg embedding` card states the
size rule, the folder rule, the network URL and absolute path rules, `--strict`, `--file-access`, the terms
`svg-image-unresolved`, `svg-image-too-large`, `link rule`, and `image-access-denied`, the example `href`, and
`canvasnet help svg --example embed`, which prints the `embed` snippet.

**HelpTopics_SvgTopic_PageFragment_DocumentsGrammarPerFormatCodesAndStrict**: the `page fragment` and `page example`
bullets of `svg fragments` name the grammar, the page, slide, and view parameters, the counting from 1, the live
limits, the examples with `&amp;`, and `canvasnet help svg --example embed-pages`. The `svg diagnostics` card has the
four fragment codes, and the `--strict` bullet of `svg options` names the two that fail and says that the warnings stay
warnings.

**HelpTopics_FormatTopicsJsonAndLimits_DocumentFragmentsDetailLocationAndOrigin**: the `pdf`, `pptx`, `vsdx`, and
`scad` trees show `report.pdf#page=3`, `deck.pptx#slide=3`, `drawing.vsdx#page=2`, and `part.scad#view=front`. The
`json diagnostics` card has the terms `detail`, `location`, and `origin`, and the `limits` card quotes the fragment
limits from `ImageFragment`.

**HelpTopics_DiagnosticTriggers_ImageFragmentCodes_AreReportedWithTheirSeverity**: a theory over the four codes: a
diagram linking the two-page PDF with `#pag=2`, `#page=1&amp;page=2`, `#page=0`, and `#page=9` reports exactly one
diagnostic with the code `image-fragment-unknown-parameter`, `image-fragment-duplicate-parameter` (both `warning`),
`image-fragment-invalid`, and `image-page-out-of-range` (both `unsupported`), and `--strict` fails only for the last
two.

**HelpTopics_ImageHrefLinking_IsCrossReferencedFromScadVegaLiteFormatsAndRender**: the text of the `scad` and
`vega-lite` trees, the footer of the `formats` listing, and the usage of `render` each contain `<image href` and
`canvasnet help svg`.

**HelpTopics_CommandExamples_UseKnownCommandsAndOptions**: every command example and every `canvasnet` line of a code
block starts with `canvasnet`, names a known command (or `help`, with `--example` and `--examples`, and a path that
names a card, a command, or `topics`), and uses only options that the command or the tool declares.

**HelpTopics_OptionTerms_ExistInTheRenderOptionTable**: the first word of every option term under an `## Options`
heading is an option or alias of `render` or a global option, and more than 30 such terms are checked.

**HelpTopics_ErrorsTopic_ListsEveryErrorCode**: the terms of the `errors codes` card are exactly the values of the
error code constants of the tool.

**HelpTopics_CitedDiagnosticIds_ExistInTheLibraries**: every diagnostic identifier cited anywhere in the cards (found
with a pattern for the families `scad-`, `vl-`, `mermaid-`, `pptx-`, `vsdx-`, `pdf-`, `image-fragment-`, `image-page-`,
and `image-access-`) is an entry of the trigger table, an error code, one of the image-link codes, or a placeholder of
the naming rule. Otherwise the literal part of the identifier (before a `<type>` placeholder) must be a string literal
in one of the library assemblies (the assembly-literal check).

**HelpTopics_DiagnosticTriggers_ProduceTheCitedIds**: a theory over the trigger table: for each cited identifier, a tiny
model or chart is run through the real pipeline and the identifier is reported in the diagnostics (a Vega-Lite warning
has the code `vega-lite-warning` and the identifier at the start of its message).

**HelpTopics_DiagnosticTriggers_Strict_FailsWithUnsupportedFeature**: the tiny model that triggers `scad-module-offset`
and the tiny chart that triggers `vl-mark-<type>` fail with the error `unsupported-feature` under `--strict`; no picture
is written.

**HelpTopics_RenderText_IsDeterministicPlainAndWrapped**: a theory over every card: two renderings are identical; the
text has no escape character, no carriage return, and no trailing newline; it starts with `# <title>` and contains the
body as written; no line is longer than 80 columns (120 inside a code block). It lists each card below it and each
See also target as `- <command> <summary>`, every example name, and the `--example <name>` pointer when the card has
examples.

**HelpTopics_AppendWrapped_LongWord_IsNotSplit**: a word longer than the text width between two short words keeps a
line of its own, with the hanging indent, and nothing is lost.

**HelpTopics_RenderJson_HasTheDocumentedShape**: a theory over every card: the document is one line, ASCII only, with
exactly the keys `schemaVersion` (2), `ok`, `command` (`help`), `kind` (`card`), `id`, `aliases`, `title`, `summary`,
`body`, `children`, `seeAlso`, and `examples`, in that order; the links carry the id, summary, and command of their
targets, and the examples their content.

**HelpTopics_RenderExample_UnknownName_ThrowsUsageErrorListingNames**: an unknown example name is a `ToolException` with
code `usage-error`, the message `Help card 'scad' has no example 'nothing'.`, a hint that lists the example names, and
the command `help`, for the text and the JSON rendering alike.

**HelpTopics_RenderExample_Source_ReturnsExactEmbeddedContent**: `RenderExample` (with the name in capitals) returns
exactly the embedded resource, from the root and from `scad language`. The example list has one line per example of
the tree; the JSON list has the root id, the owning card of each example, and no content; and the JSON of one example
has the content, the file name, and the card `scad/language`.

**HelpTopics_ParseFrontMatter_SupportedSubset_ReadsEveryConstruct**: a card with CRLF line endings that uses every
construct of the subset parses to the keys in order with their values and lines (a quoted value with `''`, `\"`, and
`\\`, a list of values, a list of maps, comments, and blank lines), and its body follows the closing fence.

**HelpTopics_ParseFrontMatter_OutsideTheSubset_ThrowsWithFileAndLine**: a theory over a tab, a flow sequence and
mapping, both block scalars, an anchor, an alias, a tag, a trailing comment (plain and after a quote), an unclosed
quote, an unsupported escape, a plain value with a colon and a space, a value that starts with a backtick, a repeated
key (at the top level and in a map), bad indentation (four cases), a key of a map without a value, a key without a value
or items, and an upper case key. Each is an `InvalidOperationException` whose message starts with `Help card '<file>',
line <n>:` and names the reason.

**HelpTopics_ParseFrontMatter_MissingFence_ThrowsWithFileAndLine**: a card without the opening fence fails at line 1,
and one without the closing fence at its last line.

**HelpTopics_LoadCard_SemanticErrors_ThrowWithFileAndLine**: a theory over an unknown key, a missing title, a list
where text is expected, text where a list is expected, a bad alias, an example of an unknown kind, an example without
a name, a command that is not the tool's, and an unknown example key: each names the file and the line.

**HelpTopics_LoadCard_TokensAndFileNames_AreChecked**: an unknown token is an error that names the file and the token;
a file name with a space or capitals is an error; and a good card has its identifier, name, root, path, command,
children, and the token replaced.

**HelpTopics_Tree_ChildrenAndSeeAlsoResolve**: walking from the roots reaches every card exactly once, each child
names its parent, and every See also entry names another card once.

**HelpTopics_Tree_ResourcesAreExactlyTheCards**: the embedded card resources, with `\` read as `/`, are exactly the
cards, each ending in `.md`.

**HelpTopics_Cards_FitTheSizeBudget**: no card renders to more than 6,000 characters, no root card with cards below it
to more than 3,500, and every summary is one line of at most 60 characters (a root) or 100.

**HelpTopics_Resolve_PathsAndAliases_FindTheCard**: paths by names, aliases, upper case, and slashes resolve to their
cards (`mermaid gantt`, `MMD GANTT`, `mermaid/gantt/settings`, `mmd graph`, `vl scales`, `svg links`, and
`openscad camera`); an unknown root and an empty path resolve to nothing; and the path of every card resolves to it.

**HelpTopics_Resolve_UnknownChild_ThrowsUsageErrorListingChildren**: `mermaid giant` is the usage error
`Unknown help card 'giant' under 'mermaid'.` whose hint suggests `gantt`, lists the Mermaid cards, and points to
`canvasnet help mermaid`; a word below `mermaid gantt settings` says that it has no cards below it.

**HelpTopics_RenderTopics_ListsEveryCard**: the text of the tree has a line for every card, depth first, with its path
indented by depth, and no line over 80 columns; the JSON document (`kind` `topics`) lists every card with its id,
summary, and command.

**HelpTopics_Bodies_DoNotUseReservedHeadingsOrTopLevelHeadings**: no body has a `#` heading or a heading of the
footer outside a code block, and every code block names its language and is closed.

**HelpTopics_DrawingFormats_HaveStyleAndCompositionCards**: `scad`, `vega-lite`, `mermaid`, and `svg` each list a
`style` and a `composition` card, which link to the hubs and to each other, and the root links to both hubs.

**HelpTopics_StyleAndCompositionHubs_LinkBothWays**: the `style` and `composition` hubs link to the card of every
drawing format and to each other; the top-level help names `canvasnet help style`; and from `mermaid gantt` the See
also links lead to `mermaid style`, then to `mermaid composition` and the hubs.

**HelpTopics_MermaidStyleCard_EveryListedThemeRenders**: a theory over the five themes: the first bullet under
`## Themes` of `mermaid style` lists exactly `default`, `base`, `dark`, `forest`, and `neutral`, and with each theme the
flowchart example and the styled example render with `--strict` without a diagnostic.

**HelpTopics_VegaLiteStyledExample_ConfigChangesThePicture**: the styled Vega-Lite example sets at least six config
keys; the `vega-lite style` card names each; and for each key, the chart drawn without it differs from the chart drawn
with it.

**HelpTopics_StyleAdvice_TransparentChartInsideSvg_ShowsThePage**: an SVG drawing with a yellow page embeds the styled
chart: with `"background": "transparent"` a pixel of the chart's padding is yellow, and without it the pixel is white.

**HelpTopics_FramedFigureCard_IsLinkedFromTheCompositionAndAnchorCards**: `svg framed-figure` is listed under `svg`,
links to `composition`, `svg composition`, `svg style`, and `scad anchors`, and those and `scad composition` link
back.

**HelpTopics_FramedFigureExample_CalloutsEndOnTheModelAnchors**: the `framed` example embeds `anchors.scad` and uses
an `arrow` marker on its callouts; the model rendered alone at the size of its image reports anchors whose pixels plus
the image x and y are within a quarter unit of the last point of every callout line, and at least two different
anchors are hit.

**HelpTopics_FramedFigureAdvice_PictureAloneAtTheBoxSize_IsThePictureTheFigureEmbeds**: a theory over the Mermaid
flowchart, the Vega-Lite bar chart, and the anchors model: each, rendered alone with `--size` equal to a box, has no
pixel that differs from an SVG figure that embeds it in that box.

**HelpTopics_ParamOptionAndFragmentPrefixes_AreDocumented** (`RenderCommandParamTests.cs`): the `vega-lite options`
card lists `--param <name=value>` with an example, the `vega-lite-error` for an unknown name, and the `param.`
fragment. The `variable fragment` bullet of `svg fragments` names `def.<name>=<value>` (with `-D name=value`),
`param.<name>=<value>` (with `--param`), and the codes `vl-param-unknown`, `image-fragment-unknown-parameter`, and
`image-fragment-invalid`. The `scad` tree shows `part.scad#view=front&amp;def.width=20`; `vega-lite warnings` and
`svg diagnostics` have `vl-param-unknown`; and the `--strict` bullet of `svg options` says that `vl-param-unknown`
stays a warning.

**HelpTopics_InputFragments_AreDocumentedInTopicsAndUsages** (`InputFragmentTests.cs`): the `input fragment` bullet of
`svg fragments` has the rule for the number sign (`no file has that exact name`), the quoting, the overrides of
`--page`, `--view`, `-D`, and `--param`, the codes `image-fragment-invalid`, `page-out-of-range`, and
`vl-param-unknown`, that the warnings stay warnings also with `--strict`, and the labels of montage. The `svg` tree
shows `def.$vpd=300`; the `scad` tree has the montage and `'$vpd=300'`; the `errors`, `pdf`, `pptx`, `vsdx`, and
`vega-lite` trees have their lines; and the usage of `render`, `crop`, `inspect`, `compare`, `montage`, and `info`
contains `fragment`. **HelpTopics_ErrorsTopic_ListsEveryErrorCode** proves that `image-fragment-invalid` is listed.
