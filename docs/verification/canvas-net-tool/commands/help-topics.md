### HelpTopics

#### Verification Approach

`HelpTopics` is the command-line layer of the help cards: the library cards of the Viewer with the tool's own cards and
its command-line sections (overlays) layered on them by the OTS library `DemaConsulting.HelpCards`. The content of the
library cards (their tree, tags, links, examples, and the advice they give) is verified by the tests of the library (see
*CanvasNetHelp Unit Verification Design*); the tests here verify what the tool adds and how the help command shows the
merged cards. They are in these files:

- `HelpTopicsTests.cs`: the merged cards, their consistency with the tool and the tool's user guide, the examples, and
  the renderings printed by the help command.
- `HelpTopicsTests.Tree.cs`: the merged tree, the tool's resources, the strict validation with the validator of
  HelpCards, the addressing, and the topics.
- `HelpTopicsTests.Overlays.cs`: the tool's card files (tool cards or overlays), the merged body of an overlaid card,
  the command-line form of the links, and the diagnostic identifiers the tool's cards cite.
- `HelpTopicsTests.CommandLine.cs`: the commands of the command-line sections, run through the tool.
- `InputFragmentTests.cs` and `RenderCommandParamTests.cs`: the fragment and parameter documentation of the merged
  cards, next to the tests of those features.

The helper `HelpView` reads the combined catalog (a card by id, its tree and examples, and its body with the links
written as commands), and the helper `HelpCardText` reads the merged Markdown of a card as the command line shows it:
the bullets under a heading, their terms (a leading code span or bold text), the headings, and the text of a card and
the cards below it with the lines of each paragraph joined.

The tests use the real embedded content of both assemblies and no mock: the merged tree is the shipped one, so a
malformed or inconsistent tool card fails the build. The command-line content is checked against the tool:

- The option terms of every `## Options` and `## Command line` section are looked up in the option table of
  `RenderCommand` and the global options.
- Every command line, in a command example or in a code block of a card, is checked against the default
  `CommandRegistry`: a known command, only options that the command or the tool declares, and for `canvasnet help` a
  path that names a card.
- The merged `errors codes` card is compared with the error code constants through reflection, and its command-line
  section lists exactly the codes only the tool reports.
- The file types the `svg embedding` card lists are compared with the `formats` table, and the `svg flatten` and
  `files` cards with the reference lists of the tool user guide.
- The commands of the framed-figure and selectors sections are run through `ToolHost` on the help examples.

The text and JSON renderings, printed through `CommandRegistry.RenderHelp`, are checked for every card with theory
tests. The interaction with the registry (`help`, its paths, and its options) is verified by `CommandRegistryTests.cs`
and at the subsystem level by `CommandsSubsystemTests.cs`; the help examples self-test by `ValidationTests.cs`.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they run the tool through
`ToolHost`, which redirects the console. The parity tests read `docs/user_guide/canvas-net-tool.md` (`build.ps1
-Changed` selects the tool tests when it changes) and the command-line tests write only to temporary directories that
they delete.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every merged card loads with a title, a summary, and a body; the roots are the eighteen topics in order, the catalog
  is depth first, and every root tree has an example.
- The tool's card files are exactly its own cards (the `json` and `pandoc` trees) and overlays that extend the library
  card at their own path; the merged body of an overlaid card is the library body with its links written as commands
  followed by the overlay body; and no link markup or library link wording survives on the command line.
- Every child and See also entry of the merged tree resolves; every card is reachable; names and aliases are unique
  among siblings and among the roots, and no root collides with a command, `help`, or `topics`.
- The merged cards pass the validator of HelpCards with the tool's options: at most 7,000 characters (an index root at
  most 4,000), every summary one line of at most 60 characters (a root) or 100, lines of at most 80 columns (120 in a
  code block), no reserved or top-level heading, closed fences that name their language, every command example a
  `canvasnet` command line without shell expansion, and no warning.
- A path resolves by names and aliases at every level, ignoring case and with `/`; an unknown card below a card is a
  usage error that suggests the closest card and lists the cards at that level.
- No text contains an unresolved `{{token}}`, and the tool's token is the live schema version.
- The tree of every format topic has filled `Supported`, `Unsupported`, `Options`, `Diagnostics`, and
  `Known differences` sections and at least two examples.
- Every command example starts with `canvasnet` and has no file name; every command line uses a known command,
  options, and help card; every option term is an option of `render` or a global option; and the merged `errors codes`
  card lists exactly the error codes.
- The flatten and file access bullets are the items of the tool user guide, in order.
- The commands of the command-line sections do what the cards say when they are run.
- The text of a card is deterministic and plain, starts with its title, prints the body as written, and lists the cards
  below it, its See also links with their commands and summaries, and its examples.
- The JSON of a card has the envelope first and every documented key, `tags` included, on one line and in ASCII.
- An example is returned exactly as embedded, from its card and from its root, and an unknown example name is a usage
  error that lists the names.

#### Test Scenarios

**HelpTopics_All_EveryCardLoads_WithTitleSummaryAndBody**: the root cards are `TopicNames` in order, both in `All` and
in `Roots`; every card comes after its parent; every card has a title, a summary, and a body; and the tree of every
root has an example.

**HelpTopics_Find_EveryAlias_ResolvesIgnoringCase**: every documented root alias (`openscad`, `vegalite`, `vl`, `mmd`,
`puml`, `mml`, `equation`,
`powerpoint`, `visio`, the raster extensions, `file-access`, `links`, `position`, and `filter`) resolves to its card in
lower and upper case; every name resolves to itself; the number of root aliases is exactly the documented number; and
an unknown name and a card that is not a root (`gantt`) resolve to nothing.

**HelpTopics_Names_DoNotCollideWithCommandsHelpOrEachOther**: the names and aliases of the root cards are unique, and
none equals a command, a command alias, `help`, or `topics`.

**HelpTopics_ChildNames_AreUniqueAmongSiblings**: the names and aliases of the cards below each card are unique,
ignoring case, and none is `topics`.

**HelpTopics_Text_HasNoUnresolvedTokens**: no string of any merged card contains `{{`, and the tool's token table
holds the live schema version (the library tokens and the limits they quote are tested with the library).

**HelpTopics_FormatTopics_SubtreesHaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples**: the tree of each
of the nine format topics has a card with bullets under each of `## Supported`, `## Unsupported`, `## Options`,
`## Diagnostics`, and `## Known differences`, and at least two examples.

**HelpTopics_Examples_NamesUniqueKindsValidAndSourcesNonEmpty**: the merged example names are unique below each root
and the kinds are valid; a command example starts with `canvasnet` and has no file name; every other example has a file
name (the library examples themselves are tested with the library).

**HelpTopics_ToolResources_AreOverlaysOfLibraryCardsOrToolCards**: every embedded card file of the tool either has
`extends:` equal to its own path, naming a card of `CanvasNetHelp.Catalog`, or is a card under the `json` or `pandoc`
roots that the library does not have; there is at least one of each.

**HelpTopics_OverlayCards_BodyIsTheLibraryBodyThenTheOverlayBody**: for every overlay, the merged body starts with
the library card's body written with the command link style (`HelpTopics.RenderOptions`) and continues with exactly the
headings of the overlay body (nothing when the overlay has no body).

**HelpTopics_CommandLineText_WritesLinksAsHelpCommands**: no body or JSON document of a merged card has link
markup that names a card or an example (outside code, where it is literal text such as a Mermaid subroutine shape), and
no text has the library wording "of the card `"; the `plantuml diagnostics` card reads `` `canvasnet help limits` ``
and the `svg embedding` card `` `canvasnet help svg embedding --example embed` `` (the card that holds the example).

**HelpTopics_ToolCards_CiteOnlyDiagnosticIdsTheLibraryCites**: every diagnostic identifier that a tool card or an
overlay cites is cited by a library card too (whose test proves that it exists).

**HelpTopics_MathMlOverlays_DocumentTheCommandLineAndThePandocDisplayWidth**: the `mathml`, `mathml/options`,
`mathml/composition`, and `mathml/diagnostics` cards each have a `Command line` section; the options section has the
terms `--scale <factor>`, `--width <pixels>`, `--background <color>`, `--strict`, `--theme`, `display`, and `info`; the
quadratic example of the `mathml/fractions` card, written to a file and probed, has the natural width the composition
card quotes (`width="<natural>px" for the quadratic formula`); and the `pandoc` card has the item `natural width` and
links the `mathml` card.
**HelpTopics_SvgTopic_ImageHrefFileTypes_MatchTheFormatsCommand**: the extensions found in the `href file types`
bullet of `svg embedding` are unique and are exactly the extensions of the `formats` table (the library test compares
them with `CanvasNetViewer.Extensions`).

**HelpTopics_SvgTopic_FlattenItems_MatchUserGuide** and **HelpTopics_FilesTopic_Items_MatchUserGuide**: the
`## Reference` bullets of `svg flatten` (the library's) followed by its `## Command line` bullets (the overlay's), and
the bullets of `files` in merged order, are, word for word and in order, `<Term>: <Meaning>` of the items of the guide's
reference lists, and every bullet of `files` has a term.

**HelpTopics_FilesTopic_DocumentsModesOptionsDiagnosticsAndPandoc**: the headings of `files` are Modes, Options, In
every mode, Diagnostics, Command line, and The Pandoc filter; its terms include the modes, the two options, the inputs,
what is never read, the limits, the two live denial codes, and the two variables, which are also terms of the `pandoc`
card and are read by the source of `FilterSettings`; and the `svg` and `errors` trees contain `canvasnet help files`.

**HelpTopics_JsonDiagnosticsCard_DocumentsDetailLocationAndOrigin**: the `json diagnostics` card has the terms
`detail`, `location`, and `origin` (the library test checks the fragments of the format cards and the limits card).

**HelpTopics_ImageHrefLinking_IsCrossReferencedFromScadVegaLiteFormatsAndRender**: the text of the `scad` and
`vega-lite` trees, the footer of the `formats` listing, and the usage of `render` each contain `<image href` and
`canvasnet help svg`.

**HelpTopics_CommandExamples_UseKnownCommandsAndOptions**: every command example and every `canvasnet` line of a code
block starts with `canvasnet`, names a known command (or `help`, with `--example` and `--examples`, and a path that
names a card, a command, or `topics`), and uses only options that the command or the tool declares.

**HelpTopics_OptionTerms_ExistInTheRenderOptionTable**: the first word of every option term under an `## Options` or a
`## Command line` heading that starts with a dash is an option or alias of `render` or a global option, and more than
30 such terms are checked.

**HelpTopics_ErrorsTopic_ListsEveryErrorCode**: the terms of the merged `errors codes` card are exactly the values
of the error code constants, and the terms of its `## Command line` section are exactly `operation-failed`,
`threshold-failed`, and `pandoc-filter-error`, the codes only the tool reports.

**HelpTopics_RenderText_IsDeterministicPlainAndComplete**: a theory over every card, printed with
`CommandRegistry.RenderHelp`: two printings are identical; the text has no escape character, no carriage return, and no
trailing newline; it starts with `# <title>` and contains the body with its links as commands. It lists each card below
it and each See also target as `- <command> <summary>`, every example as `- <name> [<kind>]`, and the `--examples` and
`--example <name>` pointers when the card has examples.

**HelpTopics_RenderJson_HasTheDocumentedShape**: a theory over every card: the document is one line, ASCII only, starts
with the keys `schemaVersion` (2), `ok`, `command` (`help`), and `kind` (`card`), and has the keys `id`, `aliases`,
`tags`, `title`, `summary`, `body`, `children`, `seeAlso`, and `examples`; the links carry the id, summary, and command
of their targets, and the examples their content.

**HelpTopics_RenderExample_UnknownName_ThrowsUsageErrorListingNames**: `help scad --example nothing` is a
`ToolException` with code `usage-error`, the message `The help card 'scad' has no example 'nothing'.`, a hint that lists
the example names, and the command `help`; with `--json` the run exits with 1 and the failure document has the same
message.

**HelpTopics_RenderExample_Source_ReturnsExactEmbeddedContent**: `help scad --example PLATE` prints exactly the embedded
resource `scad/plate.scad`, from the root and from `scad language`, and the JSON of one example has the content, the
file name, and the card `scad/language`.

**HelpTopics_RenderExamples_CardWithExamples_PrintsEachExampleInFull**: a theory over every card that has examples of
its own: `--examples` prints text without a carriage return or a trailing newline that starts with
`# <title>: examples`; each example has the heading `## <name> [<kind>]`, its description, and its exact content in a
code fence of its language; the `--example <name>` pointer follows; and the text does not say that the card has no
examples of its own.

**HelpTopics_RenderExamples_IndexCard_ListsCardsBelowWithExamples**: the text of `mermaid --examples`, which has no
examples of its own, says so and lists `- <command> --examples` for every card below it that has examples, and prints
none of their example sections and no code fence; the text of `json --examples` prints the content of its own examples
and then points at `canvasnet help json help --examples`, after the `--example <name>` pointer.

**HelpTopics_RenderExamplesJson_HasTheDocumentedShape**: the examples document of `json` has exactly the keys
`schemaVersion`, `ok`, `command`, `kind` (`examples`), `id` (`json`), `examples`, and `cards`, in that order; the
examples are the card's own, with their content and without a `card` key; and the cards are the cards below it that
have examples, each with `id`, `summary`, `command`, and the example names, in that order.
**HelpTopics_Tree_ChildrenAndSeeAlsoResolve**: walking from the roots reaches every card exactly once, each child
names its parent, and every See also entry names another card once.

**HelpTopics_Tree_ResourcesAreExactlyTheToolCardsAndOverlays**: the tool's embedded resources, with `\` read as `/`,
are exactly `_source.md`, its own cards (the `json` and `pandoc` trees), and the overlays of library cards, each
ending in `.md`, and the children of every tool card are exactly the card files of its folder.

**HelpTopics_Cards_PassTheStrictValidation**: the validator of HelpCards reports no problem for the combined catalog
with the tool's options (7,000 and 4,000 characters, summaries of 60 and 100 characters, 80 and 120 columns, and every
warning an error, so every command example must parse).

**HelpTopics_Validation_UnquotedPagePattern_IsReported**: a host card whose command example has the unquoted page
pattern `out/slide-{page}.png` is reported with the rule `command-parse`.

**HelpTopics_Resolve_PathsAndAliases_FindTheCard**: paths by names, aliases, upper case, and slashes resolve to their
cards (`mermaid gantt`, `MMD GANTT`, `mermaid/gantt/settings`, `mmd graph`, `vl scales`, `svg links`,
`openscad camera`, and `json help`); an unknown root resolves to nothing; and the path of every card resolves to it.

**HelpTopics_Resolve_UnknownChild_ThrowsUsageErrorListingChildren**: `help mermaid giant` is the usage error
`Unknown help card 'giant' under 'mermaid'.` whose hint suggests `gantt`, lists the Mermaid cards, and points to
`canvasnet help mermaid`; a word below `mermaid gantt settings` says that `mermaid/gantt/settings` has no cards below
it.

**HelpTopics_RenderTopics_ListsEveryCard**: the text of the tree has a line for every card, depth first, with its
command indented by depth, and no line over 80 columns; the JSON document (`kind` `topics`) lists every card with its
id, aliases, tags, summary, and command.

**HelpTopics_FramedFigureAdvice_GridAndInspectPixel_ReadThePictureAtTheBoxSize**: the two command lines of the fenced
block of `svg framed-figure` are run on the flowchart example: the render with `--grid` succeeds and differs from the
picture rendered alone at the same size, and `inspect pixel` on the source at that size reads the same color as on the
picture rendered alone, `#CCFFCC`, the fill of the node the card names. The card points at `canvasnet render --help`.

**HelpTopics_ScadViewsAdvice_VprFragment_EqualsAzimuthAndElevation**: `scad views` states that
`'part.scad#def.$vpr=[70,0,45]'` is `--azimuth 45 --elevation 20` and `svg fragments` that `def.$vpr=[70,0,45]` is
azimuth 45, elevation 20; the plate example rendered with that fragment under `--strict` reports azimuth 45 and
elevation 20 and is identical to the plate rendered with `--azimuth 45 --elevation 20`; and an SVG figure that links
`plate.scad#def.$vpr=[70,0,45]` renders under `--strict` without a diagnostic.

**HelpTopics_PositionsSelectors_ShellQuoting_ReachesTheToolIntact** (`HelpTopicsTests.CommandLine.cs`): the quoted
forms of the `## Quoting` section of `positions selectors` for bash, PowerShell 7.3 or later, and cmd, split as each
shell splits a command line, are the selector `node:"my*odd id"`, which the tool names exactly in its warning that it
matched nothing.

**HelpTopics_FramedFigureAdvice_ReadPositions_PixelIsOnTheNode** (`HelpTopicsTests.CommandLine.cs`): the
`read-positions` command of `svg framed-figure` reports the node `send`, and the picture it writes at the box size has
the node's fill `#CCFFCC` in the column of that pixel a fifth of the box height below its top.

**HelpTopics_ParamOptionAndFragmentPrefixes_AreDocumented** (`RenderCommandParamTests.cs`): the `vega-lite options`
card lists `--param <name=value>` with an example, the `vega-lite-error` for an unknown name, and the `param.`
fragment. The `variable fragment` bullet of `svg fragments` names `def.<name>=<value>` (with `-D name=value`),
`param.<name>=<value>` (with `--param`), and the codes `vl-param-unknown`, `image-fragment-unknown-parameter`, and
`image-fragment-invalid`. The `scad` tree shows `part.scad#view=front&amp;def.width=20`; `vega-lite warnings` and
`svg diagnostics` have `vl-param-unknown`; and the `--strict` bullet of `svg options` says that `vl-param-unknown`
stays a warning.

**HelpTopics_InputFragments_AreDocumentedInTopicsAndUsages** (`InputFragmentTests.cs`): the `input fragment` bullet of
`svg fragments` has the rule for the number sign (`no file has that exact name`), the quoting, the overrides of
`--page`, `--view`, `--azimuth`, `--elevation`, `--projection`, `--theme`, `-D`, and `--param`, the codes
`image-fragment-invalid`, `page-out-of-range`, and
`vl-param-unknown`, that the warnings stay warnings also with `--strict`, and the labels of montage. The `svg` tree
shows `def.$vpd=300`; the `scad` tree has the montage and `'$vpd=300'`; the `errors`, `pdf`, `pptx`, `vsdx`, and
`vega-lite` trees have their lines; and the usage of `render`, `crop`, `inspect`, `compare`, `montage`, and `info`
contains `fragment`. **HelpTopics_ErrorsTopic_ListsEveryErrorCode** proves that `image-fragment-invalid` is listed.
