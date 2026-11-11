### HelpTopics

<!-- cspell:ignore canvasnet vegalite jpeg jpg tiff jp2 openscad -->

#### Verification Approach

`HelpTopics` is verified with unit tests in `HelpTopicsTests.cs` (the catalog, its consistency with the code, the
examples, and the renderings) and `HelpTopicsTests.Triggers.cs` (the diagnostic identifiers the topics cite). The
tests use the real embedded content and no mock: the catalog is the shipped one, so a malformed or inconsistent topic
fails the build. The content is checked against the things it describes, so that it cannot drift from the code:

- the option terms are looked up in the option table of `RenderCommand`, and the command examples are checked against
  the default `CommandRegistry` (a known command and only options that command or the tool declares);
- the `errors` topic is compared with the error code constants of the tool through reflection;
- the numbers (the largest image side, the resolution limit, the quality range, the triangle budget, and the Vega-Lite
  and Mermaid limits) are the tokens substituted from the live constants, and the `limits` topic is checked to quote
  them;
- every diagnostic identifier a topic cites (`scad-...`, `vl-...`, `mermaid-...`, `pptx-...`, `vsdx-...`, and `pdf-...`)
  is either produced by a tiny model or chart in the trigger table (the **trigger-table test**, which runs the real
  pipeline through `ToolHost`; a second test proves that `--strict` turns the report into `unsupported-feature`), or is
  an error code, or is a string literal
  found in the compiled library assemblies (the **assembly-literal check**, which searches the UTF-16 text of the
  assemblies of the libraries for the literal part of the identifier); an identifier that is none of these makes the
  test fail, so a topic cannot cite an identifier that does not exist;
- every source example (OpenSCAD, Vega-Lite, Mermaid, and SVG) is written to a file and rendered through the real entry
  point (`ToolHost.Invoke`) with `--strict` (the **strict-clean example test**), and must succeed without a diagnostic;
- the **file types** the `svg` topic lists for `<image href>` are compared with `CanvasNetViewer.Extensions` (the
  extensions of the image sources the tool wires in, the sorted union of what the registered sources claim) in
  both directions and with the extensions of the `formats` table; a plain `.json` must not be linkable and `.vl.json`
  must be. The resolver decides by the file name, so the list is also the list quoted in the `svg-image-unresolved`
  message of a file of an unknown type;
- the **`embed` snippet** of the `svg` topic links files that are not there when it is printed alone, so its kind is
  `snippet` and the strict-clean example test skips it (it enumerates only `source` examples). A dedicated test parses
  it as XML, checks that every `href` ends in a supported extension, writes each sibling file from the examples of the
  matching topic (`scad`, `vega-lite`, `svg`), and renders it with `--strict` (decision: a new kind rather than a
  special case in the drift test, so a snippet is visible as such in `--examples` and in the JSON);
- the **`pictures` snippet** of the `mermaid` topic is a diagram whose nodes show a model and a chart, so it is a
  snippet too; a dedicated test writes it with the `plate` example of the `scad` topic as `part.scad` and the `bar`
  example of the `vega-lite` topic as `chart.vl.json` and renders it with `--strict`;
- the **`class`, `state`, and `er` sections** of the `mermaid` topic are compared, item by item and in order, with the
  rows of the class, state, and entity relationship syntax, relation or cardinality, and settings tables of the
  Mermaid user guide (`docs/user_guide/canvas-net-mermaid.md`), and every
  row of the guide's table of unsupported features and warnings must be an item of the `unsupported` or `diagnostics`
  section (the **guide parity test**), so the help that agents read and the guide that people read describe the same
  version;
- the **`flatten` section** of the `svg` topic is compared, item by item and in order, with the rows of the
  `#### Flatten Reference` table of the tool user guide (`docs/user_guide/canvas-net-tool.md`).

The text and JSON renderings are checked for every topic with theory tests. The interaction with the registry
(`help` and its options) is verified by `CommandRegistryTests.cs` and at the subsystem level by
`CommandsSubsystemTests.cs`.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they run the tool through
`ToolHost`, which redirects the console. The example and trigger tests write only to temporary directories that they
delete.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every topic loads with a title, a summary, sections, and examples, in catalog order, and the embedded topic files are
  exactly the catalog.
- Every alias resolves to its topic ignoring case, and no topic name or alias collides with a command, a command alias,
  `help`, `topics`, or another topic.
- No text contains an unresolved `{{token}}`, and the numbers quoted are the live constants.
- Every format topic has the supported, unsupported, options, diagnostics, and differences sections and at least two
  examples.
- Every example has a valid kind, unique names within its topic, and non-empty content with LF line endings and no
  trailing newline; every command example uses a known command and known options.
- Every option a topic lists is an option of `render` or a global option; the `errors` topic lists exactly the error
  codes of the tool.
- Every cited diagnostic identifier is triggered by a tiny input, is an error code, or exists in a library; each
  trigger produces its identifier, and `--strict` turns a report into `unsupported-feature`.
- Every source example renders with `--strict` and no diagnostic; each snippet (of the `svg` and `mermaid` topics)
  renders with `--strict` and no diagnostic once its sibling files exist.
- The `class`, `state`, and `er` sections of the `mermaid` topic have exactly the class syntax, relation, and setting
  rows, the state syntax and setting rows, and the entity relationship syntax, cardinality, and setting rows of the
  Mermaid user guide, in order and worded the same apart from formatting, and every code of the guide's table of
  unsupported features and warnings is an item of the topic.
- The `flatten` section of the `svg` topic has exactly the rows of the flatten reference table of the tool user guide,
  in order and worded the same apart from formatting.
- The file types the `svg` topic lists for `<image href>` are exactly the extensions of `CanvasNetViewer` and of the
  `formats` table; the `image-links` section states the size rule, the folder rule, `svg-image-unresolved`,
  `svg-image-too-large`, and the
  example; the
  `scad` and `vega-lite` topics, the `formats` footer, and the `render` usage point to `canvasnet help svg`.
- The text of a topic is deterministic, plain (no ANSI escape sequence, no carriage return, no trailing newline), and
  wrapped; a word longer than the width is not split.
- The JSON of a topic has the envelope and the fields of the documented shape, on one line and in ASCII.
- An example is returned exactly as embedded; an unknown example name is a usage error that lists the names.

#### Test Scenarios

**HelpTopics_All_EveryTopicLoads_WithTitleSummaryAndSections**: the catalog is exactly the names of `TopicNames`, in
order; every topic has a title, a summary, sections that each have items, and examples; and the topic files embedded in
the assembly are exactly the catalog (no orphan and no missing file).

**HelpTopics_Find_EveryAlias_ResolvesIgnoringCase**: every documented alias (`openscad`, `vegalite`, `vl`, `mmd`,
`powerpoint`, `visio`, `png`, `jpeg`, `jpg`, `bmp`, `tiff`, `tif`, `gif`, `jpeg2000`, and `jp2`) resolves to its topic
in lower and upper case, every canonical name resolves to itself, the alias count is the documented one, and an unknown
name resolves to nothing.

**HelpTopics_Names_DoNotCollideWithCommandsHelpOrEachOther**: the names and aliases of the topics are unique, and none
is
a name or alias of a command of the default registry, `help`, or `topics`.

**HelpTopics_Text_HasNoUnresolvedTokens**: no string of any topic contains `{{`; the token table holds the live values
of `Surface.MaxDimension`, `ScadLimits.Default.MaxTriangles`, `ArgumentReader.MaxDpi`, and
`ResultWriter.SchemaVersion`, `VegaLiteLimits.Default.MaxPaintedPixels`, and `MermaidLimits.Default.MaxEdges`; and the
`limits` topic quotes the image size, the triangle budget, the number of data files, the painted pixels, the Mermaid
nodes and edges, text size, and pictures, and the quality range from the constants.

**HelpTopics_FormatTopics_HaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples**: each of the eight format
topics has the five filled sections and at least two examples.

**HelpTopics_Examples_NamesUniqueKindsValidAndSourcesNonEmpty**: example names are unique within a topic; content is
non-empty, has no carriage return and no trailing line feed; a command example starts with the tool name `canvasnet`
and has no file name; a source example or snippet has a file name with the extension of its format (`.scad`,
`.vl.json`, `.mmd`, or `.svg`); and the `scad`, `vega-lite`, `mermaid`, and `svg` topics each have a source example.

**HelpTopics_SourceExamples_RenderStrictWithoutDiagnostics**: a theory over every text example (the OpenSCAD models, the
Vega-Lite charts, the Mermaid diagrams - the `flowchart`, `sequence`, `class`, `state`, and `er` examples of the
`mermaid` topic - and the SVG drawings; not the snippets): the example is saved under its file
name and rendered with `render <file> --strict --json -o <png>` (a model at 64x64 to stay fast); the exit code is 0,
`ok` is true, there is no diagnostic, and the picture is written. The `scad` topic's `anchors.scad` example (three valid
anchors, one inside a `translate` and a `rotate`) is covered by this theory, so an anchor example that warned would fail
it; the `scad-anchor-invalid` identifier the topic cites is a string literal of the Scad library, so the
cited-identifier drift test finds it without a trigger entry.

**HelpTopics_SvgTopic_ImageHrefFileTypes_MatchTheResolverExtensions**: the extensions found in the `href file types`
item of the `image-links` section of the `svg` topic have no duplicate, are all in `CanvasNetViewer.Extensions`,
and include every one of them (both directions); `CanvasNetViewer.Extensions` is the union of the extensions of the
registered `ImageSources` and does not contain `.json` but does contain `.vl.json`; and they equal the extensions of
`DiscoveryCommands.InputFormats`.

**HelpTopics_SvgTopic_ImageLinksSection_StatesSizeFolderRuleDiagnosticAndExample**: the `image-links` section says that
a linked file is drawn at the size of the image element, that the `href` is relative to the folder of the SVG file and
that an absolute path and a network URL are never followed, that `--strict` applies, has an item for
`svg-image-unresolved` and `svg-image-too-large`, shows the `part.scad` example, names
`canvasnet help svg --example embed`, and the `embed`
example can be printed.

**HelpTopics_SvgTopic_EmbedSnippet_RendersStrictWithItsSiblingFiles**: a theory over the two snippets of the `svg` topic
(`embed` and `embed-pages`), which are not source examples; each parses as XML, every `image` `href` (without its
fragment) ends in a supported extension, and after the sibling files are written (from the `scad`, `vega-lite`, and
`svg`
examples, and the PDF, PPTX, and VSDX documents of the shared fixtures), `render <snippet> --strict --json` succeeds
with
no diagnostic, one `embed` operation per link, and the picture written.

**HelpTopics_MermaidTopic_PicturesSnippet_RendersStrictWithItsSiblingFiles**: the one snippet of the `mermaid` topic
(`pictures`) is not a source example; written under its file name beside `part.scad` (the `plate` example of the
`scad` topic) and `chart.vl.json` (the `bar` example of the `vega-lite` topic), `render <snippet> --strict --json`
exits with 0 with no diagnostic, exactly the two `embed` operations `part.scad` and `chart.vl.json` in that order, and
the picture written.

**HelpTopics_MermaidTopic_DiagramSyntaxItems_MatchUserGuide**: the rows of the tables under each of the guide's
headings `### Supported Class Syntax` (at least 30 rows), `### Supported State Syntax` (at least 18), and
`### Supported Entity Relationship Syntax` (at least 24) are, one for one and in order, the items of the `class`,
`state`, and `er` sections of the `mermaid` topic, with nothing missing or extra at the end: a row of an
`Area | Supported` table is the item text `<area>: <supported>.`, a row of a `Relation | Drawn as` table (the class
relations, and the entity relationship cardinalities and lines) the item whose term is the relation and whose text is
the drawing, and a row of a `Key | Default | Meaning` table the item whose term is the key and whose text is
`<meaning> (default <default>).`; a table with any other header fails the test; and each row of the table under
`### Mermaid Unsupported Features and Strict Mode` (at least 15, with the header `Code`, `Kind`, and `Reported when`)
is an item of the `unsupported` section (kind `Unsupported`, the text of the row) or of the `diagnostics` section (the
kind, a colon, and the text), with the code as its term. Items are compared in a canonical form: backticks dropped,
`\|` read as `|`, a final period added to a guide row, and the first letter of a text (after an `Area:` prefix and its
space) compared ignoring case. Every difference is listed in the failure message, guide row beside help item.

**HelpTopics_SvgTopic_FlattenItems_MatchUserGuide**: the rows of the table under the guide heading
`#### Flatten Reference` (at least 7, each with the header `Term | Meaning`) are, one for one and in order, the items of
the `flatten` section of the `svg` topic: the term is the first cell and the text the second, compared without
backticks and with the first letter of the text ignoring case.

**HelpTopics_SvgTopic_PageFragment_DocumentsGrammarPerFormatCodesAndStrict**: the `page fragment` item names the grammar
(`name=value`), `page=N`, `slide=N`, `view=`, the counting from 1, and the live limits; the `page example` item shows
`report.pdf#page=2`, `deck.pptx#slide=3`, the `&amp;` note, and `canvasnet help svg --example embed-pages`, which can be
printed; the `diagnostics` section has a term for each of the four new codes; and the `--strict` option text names
`image-fragment-invalid` and `image-page-out-of-range` and says that the warnings stay warnings.

**HelpTopics_FormatTopicsJsonAndLimits_DocumentFragmentsDetailLocationAndOrigin**: the `pdf`, `pptx`, `vsdx`, and `scad`
topics show `report.pdf#page=3`, `deck.pptx#slide=3`, `drawing.vsdx#page=2`, and `part.scad#view=front`; the `json`
topic
has the terms `detail`, `location`, and `origin` in its diagnostics section; and the `limits` topic quotes the fragment
limits (2048 characters and 32 parameters) from `ImageFragment`.

**HelpTopics_DiagnosticTriggers_ImageFragmentCodes_AreReportedWithTheirSeverity**: a theory over the four codes: a
diagram
linking the two-page PDF with `#pag=2`, `#page=1&amp;page=2`, `#page=0`, and `#page=9` reports exactly one diagnostic
with
the code `image-fragment-unknown-parameter`, `image-fragment-duplicate-parameter` (both `warning`),
`image-fragment-invalid`, and `image-page-out-of-range` (both `unsupported`), and `--strict` fails only for the last
two.

**HelpTopics_ImageHrefLinking_IsCrossReferencedFromScadVegaLiteFormatsAndRender**: the text of the `scad` topic and the
`vega-lite` topic, the footer of the `formats` listing, and the usage of `render` each contain `<image href` and
`canvasnet help svg`.

**HelpTopics_CommandExamples_UseKnownCommandsAndOptions**: every command example starts with `canvasnet`, names a known
command (or `help`, with `--example` and `--examples`), and uses only options that the command or the tool declares.

**HelpTopics_OptionTerms_ExistInTheRenderOptionTable**: the first word of every term in an `options` section is an
option or alias of `render` or a global option.

**HelpTopics_ErrorsTopic_ListsEveryErrorCode**: the terms of the `codes` section of the `errors` topic are exactly the
values of the error code constants of the tool.

**HelpTopics_CitedDiagnosticIds_ExistInTheLibraries**: every diagnostic identifier cited anywhere in the topics (found
with a pattern for the families `scad-`, `vl-`, `mermaid-`, `pptx-`, `vsdx-`, `pdf-`, `image-fragment-`, and
`image-page-`) is an entry
of the trigger table, an error
code, one of the image-link codes (`svg-image-*` and the four fragment codes, which the theory above renders), or a
placeholder of the naming rule; otherwise the literal part of the identifier (before a `<type>`
placeholder) must be a string literal in one of the library assemblies, the Mermaid assembly included (the
assembly-literal check).

**HelpTopics_DiagnosticTriggers_ProduceTheCitedIds**: a theory over the trigger table: for each cited identifier, a tiny
model or chart is run through the real pipeline and the identifier is reported in the diagnostics (a Vega-Lite warning
has the code `vega-lite-warning` and the identifier at the start of its message). This proves that the identifiers the
topics cite are the ones the tool reports. The table includes `scad-projection-too-complex` (a `projection()` of a
sphere of 100 fragments with a cube subtracted, whose classification is over the work ceiling) and
`scad-projection-open-mesh` (a `projection(cut = true)` of a tetrahedron that lacks a face); the retired
`scad-module-projection` is no longer in it.

**HelpTopics_DiagnosticTriggers_Strict_FailsWithUnsupportedFeature**: the tiny model that triggers `scad-module-offset`
and the tiny chart that triggers `vl-mark-<type>` fail with the error `unsupported-feature` under `--strict`, which is
what the `diagnostics` sections promise; no picture is written.

**HelpTopics_RenderText_IsDeterministicPlainAndWrapped**: a theory over every topic: two renderings are identical; the
text has no escape character, no carriage return, and no trailing newline; it starts with `Topic: <title>`; no line is
longer than 80 columns; it contains every section title, every example name, every term (on a line of its own), and the
`canvasnet help <topic> --example <name>` pointer.

**HelpTopics_AppendWrapped_LongWord_IsNotSplit**: a word longer than the text width between two short words keeps a
line of its own, with the hanging indent, and nothing is lost.

**HelpTopics_RenderJson_HasTheDocumentedShape**: a theory over every topic: the document is one line, ASCII only, with
the schema version, `ok` true, the command `help`, the `kind` `topic`, and the topic name, aliases, title, summary, the
sections (ids, titles, and non-empty item texts), and the examples (every field and the content).

**HelpTopics_RenderExample_UnknownName_ThrowsUsageErrorListingNames**: an unknown example name is a `ToolException` with
code `usage-error`, the message `Topic 'scad' has no example 'nothing'.`, a hint that lists the example names, and the
command `help`, for the text and the JSON rendering alike.

**HelpTopics_RenderExample_Source_ReturnsExactEmbeddedContent**: `RenderExample` (with the name in capitals) returns
exactly the embedded resource, with LF line endings and no trailing newline; the example list has one line per example;
the JSON list has no content; and the JSON of one example has the content and the file name.

**HelpTopics_ParamOptionAndFragmentPrefixes_AreDocumented**: the `vega-lite` topic lists `--param <name=value>` with an
example, the `vega-lite-error` for an unknown
name and the `param.` fragment; the `svg` topic has the item `variable fragment` that names `def.<name>=<value>` (with
`-D name=value`), `param.<name>=<value>` (with `--param`)
and the codes `vl-param-unknown`, `image-fragment-unknown-parameter` and `image-fragment-invalid`; the `scad` topic
shows `part.scad#view=front&amp;def.width=20`; the
`diagnostics` section of both the `vega-lite` and the `svg` topic has `vl-param-unknown`; and the strict option of the
`svg` topic says that `vl-param-unknown` stays a warning.
The drift tests (`HelpTopics_OptionTerms_ExistInTheRenderOptionTable`,
`HelpTopics_CitedDiagnosticIds_ExistInTheLibraries`, `HelpTopics_Text_HasNoUnresolvedTokens`) cover the new text.

**HelpTopics_InputFragments_AreDocumentedInTopicsAndUsages** (`InputFragmentTests.cs`): the `input fragment` term of
the `svg` topic has the rule for the number sign (`no file has that exact name`), the quoting, the overrides of
`--page`, `--view`, `-D`, and `--param`, the codes `image-fragment-invalid`, `page-out-of-range` and `vl-param-unknown`,
that the warnings stay warnings also with `--strict`, and the labels of montage; the `svg` topic shows `def.$vpd=300`;
the `scad` topic has the montage and `'$vpd=300'`; the `errors`, `pdf`, `pptx`, `vsdx`, and `vega-lite` topics have
their lines; and the usage of `render`, `crop`, `inspect`, `compare`, `montage`, and `info` contains `fragment`.
**HelpTopics_ErrorsTopic_ListsEveryErrorCode** proves that `image-fragment-invalid` is listed.
