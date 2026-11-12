### SourceLoader

<!-- cspell:ignore canvasnet jpeg tiff jpeg2000 exif gif RRGGBB RRGGBBAA pptx vsdx dpi docx scad openscad Minkowski -->

#### Verification Approach

`SourceLoader` is verified with unit tests in
`SourceLoaderTests.cs`, `SourceLoaderVectorTests.cs` (the page-based sources), `SourceLoaderScadTests.cs` (the
OpenSCAD models), `SourceLoaderVegaLiteTests.cs` (the Vega-Lite charts), and `SourceLoaderMermaidTests.cs` and
`SourceLoaderMermaidImageTests.cs` (the Mermaid diagrams and the pictures of their nodes); the reading and the
application of the EXIF orientation are core operations (`JpegCodec.ReadExifOrientation` and
`Surface.ApplyExifOrientation`) verified in the core
test project, and here only their use by the loader is. The
tests generate their inputs: rasters are written in memory
with the CanvasNet codecs (a gradient that differs at every pixel and channel, so a mirroring, rotation, or offset
changes it), the GIF with a small encoder in the test project (the libraries have none), EXIF segments are injected
into a codec-written JPEG in either byte order, and a PNG that declares an oversized image but holds no pixels is
built from its header. The documents are written by the test project (`FixtureFactory.Pdf.cs`, `.Pptx.cs`, and
`.Vsdx.cs`): a two-page PDF with a rotated second page, and a two-slide presentation and a two-page drawing built
with `System.IO.Compression`, each with a few filled rectangles and no text so that the pixels do not depend on
installed fonts; variants add an EMF picture, an unsupported shape, an unsupported geometry row, malformed content,
an unsupported encryption handler, and a broken part. Charts are JSON text written by the tests
(`FixtureFactory.VegaLite.cs`) with inline data and no custom font, plus a two-bar chart whose rows are in a CSV file
(`TwoBarFileChart` and `TwoBarCsv`, written beside the specification in a temporary folder, so that the chart facts, the
sandbox, and the data errors are verified with real files); one original chart is a fixture file
(`baskets.vl.json`). Diagrams are the fixture files `Fixtures/mermaid/flow.mmd` (a small flowchart),
`shipping.mermaid` (a flowchart with frontmatter, an accessible title, a subgraph, and a class), `sequence.mmd` (a
two-participant sequence diagram), `class.mmd` (a class diagram of two classes and one relation), and `journey.mmd` (a
user journey, a type this version does not draw), plus Mermaid text written by the tests; the expected node fills
are the colors of Mermaid's default and dark themes. The other fixture files are an original SVG
drawing and a JPEG 2000
image copied from the
repository's own codec fixtures (see `Fixtures/README.md`). Expected pixels come from the codecs and document
libraries themselves or from the generator, never from the loader. The tests use real files in a temporary
directory that each test deletes.

Error mapping is verified in four ways: directly, by feeding `Read` each allow-listed exception and a set of
unexpected ones (which must propagate unchanged); by loading, for every supported format, files that are empty, one
byte, a header only, half, and short by one byte, and garbage of the file's own length, and requiring that each
either loads or raises a `ViewerException` with an expected code - an unexpected exception type fails the test, which
is how gaps in the allow-list are found; by doing the same for each document with eight prefix lengths, a dropped
tail, and an overwritten or zeroed middle, probing and loading every page; and by loading garbage with each known
extension and requiring `corrupt-input` (`scad-error` for a model, `vega-lite-error` for a chart, `mermaid-error` for a
diagram; the shared sweeps
include the chart
fixture and also accept `vega-lite-error`). The models have their own sweep in
`SourceLoaderScadTests.cs`, because a model is text and a cut one is
usually still a valid, shorter model: a model with several kinds of statement is cut after every seventh character,
garbage of four sizes is added, and some forty stress models probe the library for exceptions that are not mapped; a
one-off sweep during development (about 950 truncations and random byte
changes per document kind) raised only expected errors, so the allow-list needed no extension for PDF, PPTX, or VSDX.
The diagrams have their own sweep in `SourceLoaderMermaidTests.cs` for the same reason: every prefix of a diagram is
loaded, and the mapping of the library's failures is verified both with real failures (invalid text, more edges than
the limit, a text longer than the limit, a diagram wider than the largest image) and directly, by handing
`RenderDiagram` the library's exceptions.

#### Test Environment

N/A - standard test environment; the tests need no network and write only to temporary directories.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every supported format is recognized by its signature or its package entries, and the content wins over the
  extension.
- The header facts of every format are reported without decoding, including every page of a document.
- Lossless rasters load with exactly their pixels; SVG renders at the requested size over white.
- A page of a document is chosen by its number from 1, rendered at 96 dpi or the resolution given, with the pixels
  the library renders, over white or the background given, and a page that does not exist is `page-out-of-range`.
- Skipped pictures and approximated constructs are reported with their page, or are `unsupported-feature` in strict
  mode.
- The size policy follows the documented rules; a side beyond 8192 pixels is `size-limit`, before decoding.
- A model is drawn from the named view chosen (iso by default, top for a model that is entirely 2D, the view the model
  sets with its own viewport variables when it sets one) at 800x800 pixels unless a size is given; a view name,
  azimuth, or elevation that was given always wins, and the operation reports the view that was drawn; with the
  variables, time, default color, and triangle limit given, the same picture every time; its include and use files
  are read from its folder and below only; a missing include, a syntax error, and a failed assertion are `scad-error`,
  an exceeded limit is `budget-exceeded`, and an unsupported feature is a note or, in strict mode, an error.
- A chart (`.vl.json`, `.vl`, or a `.json` with a Vega-Lite `$schema` in its first 4 KiB, never taken for an SVG) is
  probed
  by parsing and measuring it (the laid-out size, four channels, the marks, data references, parameters, title, and
  schema version) and drawn at that size or the size given, over its own background or the one given, with the bundled
  fonts and UTC dates, with the bars where the layout puts them and the warnings and unsupported features as notes (or,
  in strict mode, `unsupported-feature`); plain Vega and other JSON files are `unsupported-format` with a hint; an
  invalid
  specification (including nesting deeper than, or a string longer than, the limit of the parser, as the nesting limit
  of
  the SCAD parser is a `scad-error`) is `vega-lite-error`, an exceeded limit of the library while the chart is laid out
  or drawn `budget-exceeded`, a specification file longer than the limit of the library `corrupt-input` (as an overlong
  model is), and a chart larger than 8192 pixels `size-limit` (no other failure to measure is `size-limit`).
- A chart whose rows are in a local file (CSV, TSV, or JSON named by a relative `url`) is measured and drawn from the
  folder of its specification and below it only (never the current directory, and no network), exactly like the same
  chart with inline rows; a local file that is missing, outside that folder, or not valid text is a `vega-lite-error` in
  lenient and strict mode alike (never an empty chart); remote data is only a diagnostic (an `unsupported-feature` error
  with strict mode) and is never fetched; and a data limit of the library (rows or bytes) is `budget-exceeded`.
- A JPEG is turned upright by its EXIF orientation unless switched off, and the size applies to the upright image.
- A diagram (`.mmd` or `.mermaid`, never taken for an SVG and never recognized by its content) is probed by parsing and
  measuring it without its pictures (the laid-out size, four channels, and the type, whether it is drawn, the
  direction, the titles, and the numbers of nodes, edges, and subgraphs), with a natural size above 8192 pixels
  reported rather than refused; it is drawn at its natural size times the scale or fitted into the size given, over
  white, the background given, or transparent, in the theme of the options (a theme fragment of the input wins, and a
  name that is not a theme is a `usage-error`), with each warning a `mermaid-warning` note with its detail and location
  and each construct it does not draw an unsupported note (a type that is not drawn is one note and an empty
  picture), or, in strict mode, `unsupported-feature`; invalid text is `mermaid-error` with the location and the hint of
  the library, a limit of the library `budget-exceeded`, an overlong text `corrupt-input`, and a target size above the
  maximum `size-limit`; a cut or damaged diagram is an expected error or a shorter diagram that loads.
- The pictures of the nodes of a diagram are drawn through the sandboxed resolver of the folder of the diagram (a raster
  fills its box), listed once each as `embed` operations, a link outside the sandbox or a cycle is one
  `svg-image-unresolved` warning (an error in strict mode), and a broken picture is an error naming both files; a
  picture node without `w` and `h` that enlarges a diagram drawn at its natural size is charged at the drawn size
  (refused when that does not fit) and reported with the drawn natural size.
- A damaged file of any format is an expected error; an unexpected exception type is never mapped.

#### Test Scenarios

**SourceLoader_Detect_EachKind_ReturnsKind**, **SourceLoader_Detect_SignatureAndExtensionDisagree_TrustsSignature**,
**SourceLoader_Detect_SvgTextWithoutExtension_ReturnsSvg**,
**SourceLoader_Detect_UnrecognizedContent_FallsBackToExtension**, **SourceLoader_Detect_ZipPackage_UsesExtension**,
**SourceLoader_Detect_ZipPackages_UsesEntriesOverExtension**,
**SourceLoader_Detect_OtherSignatures_ReturnsKind**, **SourceLoader_Detect_TextStartingWithBm_IsNotBmp**, and
**SourceLoader_ExtensionKinds_AreWellFormed**: each supported kind is recognized by its content; a PNG named
`.jpg` is a PNG and SVG text named `.png` is an SVG; SVG text is recognized without an extension, after a byte
order mark, a prolog, and a comment; content that matches nothing keeps the kind of its extension; a damaged ZIP
signature is a presentation or a drawing by extension, while a readable archive is told by its entries (a presentation
named `.vsdx` is a presentation, one with no extension is a presentation, and a Word-like archive named `.pptx` is
`unsupported-format`); PDF, a raw JPEG 2000 codestream, and both TIFF byte orders are
recognized; text that starts with `BM` is not a bitmap; the extension table is lower case with a dot and covers
every kind.

**SourceLoader_Detect_UnknownFormat_ThrowsUnsupportedFormat** and
**SourceLoader_Detect_MissingFileOrDirectory_ThrowsInputError**: an unknown file is
`unsupported-format` with a hint; a missing file is `input-not-found` and a directory `input-unreadable`.

**SourceLoader_Detect_VegaLiteSuffixesAndSchema_ReturnsVegaLite**: a file named `.vl.json` (in lower and upper
case), `.vl`, or
`.json` with a top-level Vega-Lite `$schema` (version 5 or 6) in its first 4 KiB is a chart, also when its text contains
an SVG start
tag; a PNG named like a chart is still a PNG (the signature wins); the kind has the format name `vega-lite`, is a
vector kind,
and is neither a model nor a document.

**SourceLoader_Detect_VegaSchema_IsUnsupported**: a `.json` file whose `$schema` is the plain Vega address is
`unsupported-format` with the hint "Vega is not supported"; a `.json` file with no schema, with another schema, or
with the
Vega-Lite schema only after the first 4 KiB is `unsupported-format` with a hint that names the `.vl.json` suffix.

**SourceLoader_Detect_SchemaMustBeATopLevelProperty**: a `.json` file that has the Vega-Lite address in another property
(`description`), in a nested object (`config`), as an array element, or as a `$schema` that is not a string is
`unsupported-format` ("not a Vega-Lite chart"); a plain Vega `$schema` is "Vega is not supported" even when another
property holds the Vega-Lite address; and a top-level `$schema` is a chart wherever it is among the properties (after a
nested value too), with comments, a trailing comma, a byte order mark, or an escaped property name, and also when the
document is cut off by the end of the 4 KiB header after the property.

**SourceLoader_Detect_ScadMentioningSvg_ReturnsScad**: a file named `.scad` whose comment contains the text of an SVG
start tag is a model, not an SVG, while the same text without the `.scad` extension is still taken for an SVG.

**SourceLoader_Detect_MermaidMentioningSvg_ReturnsMermaid**: a diagram whose first line and one label hold the text of
an SVG element, named `flow.mmd` and `Flow.MERMAID`, is a Mermaid diagram, and so is SVG content with the extension
`.mmd`; a Markdown file (`flow.md`) that holds the text of a diagram is `unsupported-format` (no content sniffing); the
kind is a vector kind and a diagram (`IsDiagram`), not a chart, and its format name is `mermaid`. The fallback theory
(**SourceLoader_Detect_UnrecognizedContent_FallsBackToExtension**) also maps unrecognized content named `.mmd` and
`.MERMAID` to `mermaid`.

**SourceLoader_Probe_EachKind_ReportsHeaderFacts**, **SourceLoader_Probe_PngAndSvg_ReportChannelsAndAlpha**, and
**SourceLoader_Probe_JpegWithExif_ReportsOrientation**: for each kind the format, path, byte count, size, decodability,
frame count, and page facts are right (a document has two pages and lists them; a model is 800x800 with one module,
`block`, one function, `half`, and no directives); a PNG with transparency and an SVG
report four channels with alpha; a JPEG with an EXIF segment reports its orientation and its stored size.

**SourceLoader_Probe_VegaLite_ReportsChartFacts**: a chart with a title, a layer of a `bar` and a `rule`, a parameter,
and
inline and named data is probed; the size equals the library's own `Measure` and is larger than the data rectangle,
there are
four channels with alpha, one page, and no module, function, or directive, and the chart facts list the marks `bar`
and `rule`,
the data references `inline` (2 rows) and `name`, the parameter, the title, and schema version 5.

**SourceLoader_Probe_VegaLiteFacet_ReportsTheMeasuredSizeAndTheMarkOfItsCells**: a bar chart faceted into one column for
each value of a field (two cells) is probed; the size equals the library's own `Measure` and the chart facts list the
mark `bar` of its cells.

**SourceLoader_Probe_VegaLiteLargerThanMaximum_IsSizeLimit**: a chart whose data rectangle is 9000 pixels wide is a
`size-limit` error on one line with a hint, not an unexpected exception.

**SourceLoader_Probe_ModelWithDirectives_ListsModulesFunctionsAndDirectives**: a model with an `include`, a `use`, two
modules, and a function declared twice lists the names in source order (the repeated name kept) and the two directives
with their lines, although the files they name do not exist (probing opens no other file); it has one page, no pages
list, and the natural size 800x800.

**SourceLoader_Probe_SyntaxError_ThrowsScadErrorWithLocation**,
**SourceLoader_Load_FailedAssert_ThrowsScadError**, **SourceLoader_Probe_SourceTooLong_ThrowsCorruptInput**, and
**SourceLoader_Probe_DeeplyNestedExpression_ThrowsScadError**: a syntax error is a `scad-error` of one line that
starts with "SCAD syntax error", names the file, line, and column, and keeps the library's exception as its cause; a
failed `assert` is a `scad-error` with the message and the position; a model one character over the 4,194,304 the
library reads is `corrupt-input` ("exceeds the maximum length"); parentheses nested 400 deep are a `scad-error`.

**SourceLoader_Load_VegaLite_RendersInlineChartWithExpectedPixels**: the two-bar chart (a 200x100 data rectangle)
loads at the
laid-out size as a vector source of one page with no notes and a `render` operation (`width`, `height`, `timeZone`); the
corner is white, the bars have the default mark color `#4c78a8` at the points the layout puts them (computed from the
5-pixel padding and the 100-pixel bands, no pixel read back from the library), the pixel above the shorter bar and the
gap
between the bars are white; a scale of 2 doubles the size; a background replaces the white and is reported.

**SourceLoader_Load_VegaLiteComposition_DrawsEveryCellAtTheProbedSizeWithoutDiagnostics**: an `hconcat` of two 200x100
bar charts (more than 400 pixels wide with the axes of both) and a bar chart faceted into two columns (more than 200
pixels wide) are probed and loaded (also with strict mode): the picture has the probed size, there is no diagnostic
(strict mode has nothing to refuse), and bars in the default mark color are drawn in the left half and in the right half
of the picture, so every cell was drawn.

**SourceLoader_Load_VegaLite_MapsDiagnosticsAndStrictErrors**: a chart with an unknown property and a `geoshape` mark
with a `longitude` channel returns the
`vega-lite-warning` note and the `vl-mark-geoshape` and `vl-channel-geo` unsupported notes, each on one line with the
file, the
position, and the pointer; with strict mode the load is `unsupported-feature` naming `vl-mark-geoshape` and keeping the
library's
exception as its cause; malformed JSON is a `vega-lite-error` that starts with "Vega-Lite specification error", names
the
file, and keeps the library's exception; plain Vega named like a chart is a `vega-lite-error`; a missing file is
`input-not-found`.

**SourceLoader_Load_VegaLite_MapsLimitFailuresToBudgetExceeded**: real failures of the library with a row limit of 1
and a
scene-item limit of 2, and one that is not a limit, are mapped: a limit is `budget-exceeded` with a hint and the
exception as
its cause, anything else is a `vega-lite-error` ("Vega-Lite render error: ...") without a hint; this pins the texts of
the
library that the mapping recognizes.

**SourceLoader_Load_VegaLite_MapsPaintLimitFailureToBudgetExceeded**: a real failure of the library for a chart that
paints more than a `MaxPaintedPixels` of 10 (whose message names `VegaLiteLimits.MaxPaintedPixels`) is
`budget-exceeded` with a hint and the exception as its cause, and not an error of the chart; this pins the text of the
paint limit that the mapping recognizes.

**SourceLoader_Load_VegaLite_MapsCompositionLimitFailuresToBudgetExceeded**: a real failure of the library for an
`hconcat` of two charts and for a facet of two cells, each with a view limit of 1 (whose message names
`the limit (MaxViews)`), is `budget-exceeded` with a hint and the exception as its cause; this pins the second text form
that the mapping recognizes.

**SourceLoader_Probe_VegaLiteLocalDataIsMeasuredFromSpecFolder**: a chart whose rows are in `bars.csv` in a sub folder
beside
its specification (the current directory being another folder) is probed and loaded from its path; it has the same
natural
size and the same pixels as the same chart with inline rows, no diagnostic, and the chart facts name the data as a `url`
reference to `bars.csv`.

**SourceLoader_Load_VegaLiteMissingDataMapsToVegaLiteError**: charts that name a missing file, a file beside the
folder of
the specification (`../secret.csv`, which exists), and a file of invalid UTF-8 are each a `vega-lite-error` ("Vega-Lite
render error: ...", "was not found", "outside the allowed directories", "is not valid UTF-8") on one line, keeping the
library's `VegaLiteRenderException` as the cause, whether the chart is probed, loaded, or loaded in strict mode (never
an
empty chart); and a chart with an `https` URL loads with only the `vl-data-remote-url` diagnostic, nothing fetched,
while
strict mode makes it an `unsupported-feature` error naming the feature.

**SourceLoader_Load_VegaLiteDataLimitMapsToBudgetExceeded**: real failures of the library for a file-backed chart opened
from its path, with a row limit of 1, a byte limit of 3, and no data folder at all (a chart parsed from a string), are
mapped by `MapVegaLiteRender`: the row and byte limits are `budget-exceeded` that name the limit (`MaxDataRows` and
`MaxDataBytes`) and keep the exception as the cause, and the chart without a data folder is a `vega-lite-error` ("no
base
directory, data path or file resolver"); this pins the texts of the library that the mapping recognizes.

**SourceLoader_Probe_VegaLiteSourceBeyondLimit_IsCorruptInput**: a chart one character over the limit of the library is
`corrupt-input` naming the limit, as for a model (the parity with SCAD that the design keeps on purpose).

**SourceLoader_Probe_VegaLiteParserLimits_AreSpecificationErrors**: a specification nested ten levels deeper than the
default depth limit and one with a string ten characters over the default string limit are each a `vega-lite-error`
that starts with "Vega-Lite specification error", names the limit, is on one line, and keeps the library's
`VegaLiteSpecException` as its cause; neither is `budget-exceeded`, as the SCAD nesting limit is a `scad-error`.

**SourceLoader_RenderAndMeasureChart_MapArgumentFailures**: an `ArgumentException` and an
`ArgumentOutOfRangeException` thrown by a render are `vega-lite-error` ("Vega-Lite render error: ...") that keeps the
exception as its cause; the same two thrown by a measure are `vega-lite-error`, except an
`ArgumentOutOfRangeException` for the parameter `options` (the size check of the library), which is `size-limit` with a
hint; a `VegaLiteRenderException` that reports a limit is still `budget-exceeded` through the render.

**SourceLoader_Probe_VegaLiteOverflowingDomain_IsNotSizeLimit**: a chart whose quantitative data runs from -1e308 to
1e308 (a domain whose span overflows a double, so that it has no ticks) is probed and measured at a size within the
image limits, so that it is neither `size-limit` nor an unexpected exception.

**SourceLoader_Probe_Mermaid_ReportsDiagramFacts**: `shipping.mermaid` is probed as the `Mermaid` kind with one page,
four channels, and transparency, at exactly the size `MermaidDocument.Measure` gives, with the facts `flowchart`,
supported, `TB`, the title `Shipping`, the accessible title `Shipping steps`, three nodes, two edges, and one subgraph,
and no chart facts or modules.

**SourceLoader_Load_Mermaid_DrawsAtTheNaturalSizeOverWhite**: `flow.mmd` loaded in strict mode is a vector source at the
probed size with a white corner, more than 500 pixels of the default node fill, no note, and the one operation `render`
with the fields `width`, `height`, and `background` `#FFFFFF`, in that order.

**SourceLoader_Load_MermaidSequence_ProbesAndDraws**: `sequence.mmd` is probed with the facts `sequence`, supported,
no direction or titles, two nodes (the participants), one edge (the message), and no subgraph; loaded in strict mode it
is drawn at the probed size with a white corner, more than 2,000 pixels of the default participant fill, and no note.

**SourceLoader_Load_MermaidClass_ProbesAndDraws**: `class.mmd` is probed with the facts `class`, supported, the
direction `TB`, no titles, two nodes (the classes), one edge (the relation), and no subgraph; loaded in strict mode it
is drawn at the probed size with a white corner, more than 2,000 pixels of the default node fill (the class boxes), and
no note.

**SourceLoader_Load_MermaidPie_ProbesAndDraws**: a pie chart titled `Pets` with three sections is probed with the
facts `pie`, supported, no direction, the title `Pets`, no accessible title, three nodes (the slices), no edge, and no
subgraph; loaded in strict mode it is drawn at the probed size with a white corner, exactly the pixels the library draws
over white, more than a tenth of its pixels not white, and no note.

**SourceLoader_Load_MermaidGantt_ProbesAndDrawsWithoutAClock**: a gantt chart of two sections with a dated task and a
critical task `after` it is probed with the facts `gantt`, supported, no direction, no titles, two nodes (the tasks),
no edge, and two subgraphs (the sections), 784 pixels wide; loaded in strict mode it is drawn with exactly the pixels
the library draws without a clock (no today line), and no note.

**SourceLoader_Load_MermaidMindmap_ProbesAndDraws**: a mindmap with the circle root `Garden`, the children `Soil` (with
`Compost`) and `Plants` (with the rectangle `Beans`) is probed with the facts `mindmap`, supported, no direction, no
titles, five nodes, four edges, and no subgraph; loaded in strict mode it is drawn at the probed size, which is the
library's size, with a white corner, exactly the pixels the library draws over white, more than a twentieth of its
pixels not white, and no note.

**SourceLoader_Load_MermaidPacket_ProbesAndDraws**: a `packet-beta` diagram titled `Frame` with the blocks `0-15`, `16`,
`+24` (split across the end of the first row), and `+7` is probed with the facts `packet`, supported, no direction, the
title `Frame`, no accessible title, four nodes (the blocks as written), no edge, and no subgraph, at 1026 by 141 pixels;
loaded in strict mode it is drawn at the probed size, which is the library's size, with a white corner, exactly the
pixels the library draws over white, more than a quarter of its pixels not white, and no note.

**SourceLoader_Load_MermaidSizeAndBackground_FollowTheOptions**: a scale of 2 doubles both sides, a width of 300 keeps
the aspect ratio (the height rounded half away from zero), a 400 by 400 box is exactly that size with white above the
fitted diagram, and a background with zero alpha leaves the corner transparent while the nodes keep the default fill.

**SourceLoader_Load_MermaidTheme_IsAppliedAndValidated**: the theme `dark` draws more than 500 pixels of the dark node
fill and none of the default fill and lists `theme` `dark` in the operation; `Dark` (not the canonical name) is a
`usage-error` whose hint lists `default, base, dark, forest, neutral`, and `midnight` is a `usage-error` before a
missing file is looked for.

**SourceLoader_Load_MermaidUnsupportedType_IsOneNoteThenStrictError**: `journey.mmd` is probed with the facts
`journey`, not supported, no direction or titles, and zero counts; it loads as a 16 by 16 picture (twice the diagram
padding) that is white, with exactly one note (source `mermaid`, severity `unsupported`, code
`mermaid-diagram-unsupported`, on one line, at line 1) that says the type is not drawn by this version, that journey
diagrams are not supported, and that flowcharts are drawn; in strict mode it is `unsupported-feature` naming the
code, with a hint that says the type is not supported and mentions `--strict`, and the library's
`MermaidUnsupportedFeatureException` as its cause.

**SourceLoader_Load_Mermaid_MapsWarningsAndUnsupportedFeatures**: a flowchart with a style declaration outside the
supported subset and a Font Awesome icon in a label has one warning (source `mermaid`, code `mermaid-warning`, detail
`mermaid-style-ignored`, a location after line 1 that the message names as `noisy.mmd:line:`) and one unsupported note
(code `mermaid-icon-unsupported`, no detail), each on one line.

**SourceLoader_Probe_MermaidInvalidText_IsMermaidError**: an unclosed bracket, an unknown diagram keyword (`drawing`),
and an unknown shape name are each a `mermaid-error` whose one-line message starts with "Mermaid syntax error: ", names
`bad.mmd:` and the library's code (`mermaid-syntax-error`, `mermaid-unknown-diagram`, `mermaid-shape-unknown`), with a
hint and the library's `MermaidSyntaxException` as the cause.

**SourceLoader_Probe_MermaidLimits_AreBudgetExceededAndCorruptInput**: a chain of one edge more than the default edge
limit is `budget-exceeded` naming `mermaid-limit-exceeded`, with a hint, and a diagram text longer than the default
text limit is `corrupt-input` naming the file.

**SourceLoader_MapMermaid_MapsByCode**: a `MermaidRenderException` with the limit code thrown by a render is
`budget-exceeded` with the library's hint and the exception as the cause; one with the render-failed code is the
`mermaid-error` "Mermaid render error: a.mmd:2:3: mermaid-render-failed: the layout failed" without a hint; and an
`ArgumentException` of a render is a `mermaid-error`.

**SourceLoader_Load_MermaidLargerThanMaximum_NeedsASizeOption**: a chain of 120 nodes from left to right is probed with
a natural width above 8192 pixels (the size read from the library's message, whose text this test pins), is `size-limit`
with a hint that names `--scale` when it is loaded without a size option, and loads at a scale of 0.25 at a quarter of
the natural width with the default node fill.

**SourceLoader_Load_MermaidTruncatedOrDamaged_IsExpectedErrorOrLoads**: every prefix of `shipping.mermaid`, the empty
one included, either loads or is a `mermaid-error` or `corrupt-input`, never another exception, and 200 bytes of
garbage named `.mmd` are a `mermaid-error`.

**SourceLoader_Probe_Pdf_ListsPagesWithSizeAndRotation**, **SourceLoader_Probe_Pptx_ListsSlidesWithPresentationSize**,
and **SourceLoader_Probe_Vsdx_ListsPagesWithNamesAndSizes**: the PDF lists 144x96 and (rotated) 96x144 points with the
rotation, the presentation two slides of 360x270 points, and the drawing the pages `First` and `Second` with their
own sizes; each in pixels at 96 dpi as well.

**SourceLoader_Load_LosslessRaster_KeepsEveryPixel** and **SourceLoader_Load_GifJpegAndJpeg2000_MatchTheCodecs**:
PNG, BMP, and TIFF load with exactly the generated pixels; the GIF loads with its palette image; the JPEG and the
JPEG 2000 image equal what their codecs decode; none reports operations, diagnostics, or echo.

**SourceLoader_Load_FirstPage_RendersOverWhiteAt96Dpi**, **SourceLoader_Load_SecondPage_RendersThatPage**, and
**SourceLoader_OpenPages_EveryPage_MatchesLoadingEachAlone**: each document renders its first page at 96 dpi with its
rectangles where the document puts them and white elsewhere (vector, one `render` operation, no notes); page 2 is the
rotated PDF page (displayed size, yellow square at the top left), the second slide, and the second drawing page of
its own size; both pages loaded in reverse order from one opened source equal the pages loaded alone.

**SourceLoader_Load_PageThatDoesNotExist_ThrowsPageOutOfRange**: page 0 and page 3 of a document, and page 2 of a PNG
and of an SVG, are `page-out-of-range` with a message naming the page and the file.

**SourceLoader_Load_Dpi_SizesThePage**, **SourceLoader_Load_Dpi_MatchesTheLibraryRender**, and
**SourceLoader_Load_DpiNotUsable_ThrowsUsageError**: resolutions from 48 to 192 dpi give the documented pixel sizes for
each document (with rounding half away from zero), the dpi is recorded in the `render` operation, the PDF at 120 dpi
equals the library's own render at 120 dpi pixel for pixel; zero and not-a-number are `usage-error`, and so is a
fractional resolution for a drawing.

**SourceLoader_Load_SizeOptions_ActOnTheNaturalSize** and **SourceLoader_Load_WidthAndHeight_FitsThePageInsideTheBox**:
scale, width, and height act on the page's size at the resolution; two sides given together fit the page inside that
box without stretching it, centered over the background (white or green), recording a `fit` operation, and need no
fitting when the box has the page's aspect ratio.

**SourceLoader_BoxOptions_VectorKinds_SetBoxAndClearScale**, **SourceLoader_BoxOptions_RasterKinds_AreUnchanged**,
**SourceLoader_BoxOptions_NullArguments_ThrowArgumentNullException**, and
**SourceLoader_Load_VectorWithBox_RendersNativelyAtBox**:
for each vector kind the box options carry the box and no scale and keep every other option; for each raster kind the
very same options come back; null arguments throw; an SVG loaded with the box options is exactly the box, and a 480x360
slide loaded into a 300x300 box is 300x225 centered with bands of the background above and below.

The fit of a raster into a box is the core `Surface.FitInto`, verified by its own tests (see _SurfaceTransforms Unit
Verification Design_, `canvas/surface-transforms.md`); the loader's tests above check that the options it builds
and the pixels it fits agree with it.

**SourceLoader_Load_Background_IsWhatThePageIsRenderedOver**: a transparent background leaves an unpainted pixel
transparent and black gives black, for each document; the `render` operation names the background.

**SourceLoader_Load_DefaultOptions_DrawsIsometricOrthographicAt800** and
**SourceLoader_Load_DefaultOptions_MatchesTheLibraryRender**: the model fixture loads at 800x800 as a vector source
with no notes and one `render` operation (view `iso`, azimuth 45, the library's isometric elevation, `orthographic`,
the size, and `#FFFFFF`), white in the corner and covered in the middle; at 64x64 the pixels equal the library's own
render of the same model with an orthographic projection.

**SourceLoader_Load_EachNamedView_IsDistinctAndDeterministic**, **SourceLoader_ScadViews_HaveTheDocumentedAngles**,
**SourceLoader_Load_OppositeViews_HaveMirroredSilhouettes**,
**SourceLoader_Load_AzimuthAndElevationOverride_ReplaceTheViewAngles**,
**SourceLoader_Load_ViewName_IsCaseInsensitiveAndValidated**, and
**SourceLoader_Load_Perspective_DiffersFromOrthographic**:
a model that looks different from every side (a slab with a tower near one corner and a sphere near the opposite one)
is drawn from each of the seven views twice; each picture is neither empty nor full, the same both times, and no
two views give the same picture; the table has the angles iso (45 and the isometric elevation), front (0, 0), back
(180, 0), left (270, 0), right (90, 0), top (0, 90), and bottom (0, -90) in the order of the help; the covered pixels
of front and back, and of left and right, are mirror images of each other to within a twentieth of the covered area;
an azimuth of 90 on the front view equals the right view, an elevation of 90 equals the top view, and an override
alone starts from `iso`; the name is accepted in any case and another name is a `usage-error` whose hint lists the
views; a perspective view differs from the orthographic one and says so in its operation.

**ScadViewOptions_IsViewExplicit_FollowsTheRules**: a default `ScadViewOptions`, one with the view named `ISO` (any
case), and one that only asks for a perspective projection are not explicit; one with `ViewGiven` set (which the
command line
sets for every `--view`, even `--view iso`), one with an azimuth of 0, one with an elevation of 0, and one with a view
named `top` are explicit. Only an explicit view is passed to the library, so these rules decide who chooses the view.

**SourceLoader_Load_FlatModel_UsesTheTopViewUnlessAViewIsChosen**: a model that is entirely 2D (a centered 20 by 10
square) loaded with the default options reports the view `top` with azimuth 0 and elevation 90 in its `render`
operation, and its center pixel is exactly the flat gray (180, 180, 180, 255); loaded with `ViewGiven` set (the
isometric
view asked for) it reports `iso` and is a different picture; loaded with the view `front` it reports `front`.

**SourceLoader_Load_SizeOptions_FollowTheNaturalSize**, **SourceLoader_Load_Background_IsWhiteColoredOrTransparent**,
**SourceLoader_Load_DefaultColor_ColorsUncoloredSurfaces**, and
**SourceLoader_Load_ModelWithoutGeometry_IsTheBackground**:
a scale of 0.1 gives 80x80, a width of 50 gives 50x50, two sides are exact, and a width over the limit is
`size-limit`; the background is white by default, blue when given, and transparent when it has no alpha (the corner
is clear and the middle covered), each named in the operation; a plain cube is gray, red with a default color, and a
cube the model colors blue stays blue; an empty model, a model of only comments, and one that only defines things
draw nothing but the background.

**SourceLoader_Load_Defines_ChangeTheModel**, **SourceLoader_Load_Defines_AreDescribedByNameAndType**,
**SourceLoader_Load_DefineWithInvalidName_ThrowsUsageError**, and **SourceLoader_Load_Time_SetsTheSpecialVariable**: a
number, a flag, and a text each replace the assignment in a parametric model and give four different pictures; the
operation lists them as `name=value` in name order with text in quotes; a name with a dollar sign in the middle is
a `usage-error` that mentions `-D`; `$t` changes the model and is listed only when given.

**SourceLoader_Load_EchoWarningAndUnsupported_AreReturned** and
**SourceLoader_Load_UnsupportedFeatureInStrictMode_ThrowsUnsupportedFeature**: a model that echoes, calls a module
that does not exist, and uses `surface` returns the echo line with its `ECHO:` prefix, a `scad-warning` note with the
position, and an `unsupported` note whose code is `scad-module-surface`, and the picture is still drawn; with strict
mode
the same model is an `unsupported-feature` error that names the feature and the position in one line, with a hint
that mentions `--strict`.

**SourceLoader_Load_TriangleLimitExceeded_ThrowsBudgetExceeded**, **SourceLoader_Load_TriangleLimitLargeEnough_Loads**,
**SourceLoader_Load_EvaluationLimitExceeded_ThrowsBudgetExceeded**, and
**SourceLoader_MapScadEvaluation_LimitAndOtherFailures_MapToTheirCodes**: a limit of one triangle is `budget-exceeded`
with "Triangle limit exceeded (limit 1)", the position, and a hint that names `--max-triangles`; a limit of 100,000
loads and is recorded in the operation; runaway recursion and a loop of a billion iterations are `budget-exceeded`
with a hint that the limit cannot be changed; a library failure worded with a limit, or caused by the renderer's
budget, maps to `budget-exceeded`, and any other failure to `scad-error`.

**SourceLoader_Load_IncludeAndUse_AreReadFromTheModelFolderAndBelow**,
**SourceLoader_Load_NestedInclude_IsRelativeToTheIncludingFile**, **SourceLoader_Load_MissingInclude_ThrowsScadError**,
**SourceLoader_Load_IncludeOutsideTheModelFolder_ThrowsScadError**, and
**SourceLoader_Load_IncludeCycleAndBrokenInclude_ThrowScadError**: a model that includes a module from a subfolder and
uses a function from another file draws the same picture as the same model in one file, with no notes; an include
inside an included file is relative to that file; an `include` and a `use` of a file that does not exist are a
`scad-error` that starts with "SCAD include not found", names the position, and has a hint about where files are
looked up; an include of an existing file next to the model's folder is refused as "outside the allowed
directories"; an include cycle and a syntax error in an included file are `scad-error`, the latter naming the
included file and the position.

**SourceLoader_Read_ScadExceptions_AreMappedToErrorCodes**: the library's syntax and evaluation exceptions map to
`scad-error`, an evaluation exception worded with a limit to `budget-exceeded`, and its unsupported-feature exception
to `unsupported-feature`; each keeps the position in one line and the exception as its cause.

**SourceLoader_Load_TruncatedAndGarbageModels_GiveAPictureOrToolException** and
**SourceLoader_Load_StressModels_GiveAPictureOrToolException**: a model with several kinds of statement (a variable,
a function, a module, a boolean, a loop, an echo) cut after every seventh character, garbage of four sizes, and some
forty stress models (huge numbers, a negative or zero size, wrong argument types, extrusions and hulls of nothing,
unsupported modules such as `import` and `surface`, runaway recursion, a list of a million elements, a byte-order
mark, a NUL byte, non-ASCII names, modifier characters) are probed and drawn at 16x16 pixels; each either gives a
picture or raises a `ViewerException` - any other exception type fails the test, which is how a gap in the allow-list
would be found. None was found.

**SourceLoader_Load_SizeBeyondMaximum_ThrowsSizeLimit**: a resolution or a width that makes a page larger than 8192
pixels is `size-limit`.

**SourceLoader_Load_PptxMetafilePicture_IsSkippedAndReported**,
**SourceLoader_Load_PptxMetafilePictureStrict_ThrowsUnsupportedFeature**,
**SourceLoader_Load_PptxUnsupportedShape_ThrowsUnsupportedFeature**,
**SourceLoader_Load_VsdxUnsupportedRow_IsReportedAndStrictRefuses**, and
**SourceLoader_Load_PdfStrict_HasNoDiagnostics**: an EMF picture is skipped with one `pptx` note (code
`pptx-image-metafile`, page 1) and the rest of the slide is drawn, with none on slide 2; strict mode makes it
`unsupported-feature` naming the file; a shape with a preset that does not exist is `unsupported-feature` with the
feature token in the message, strict or not; an unknown geometry row is one `vsdx` note
(`vsdx-geometry-row-FutureRowKind`) with the page drawn, and `unsupported-feature` when strict; a PDF has no notes
and strict mode changes nothing.

**SourceLoader_Load_SvgBackground_TransparentKeepsAlphaColorFlattens** and
**SourceLoader_Load_Svg_RendersNaturalSizeOverWhite** and
**SourceLoader_Load_SvgWithSizeOptions_RendersAtRequestedSize**: the SVG fixture renders at 100x80 with the red
and blue squares where the drawing puts them, white where it is empty, and the half-transparent green flattened to
light green; scale, width, height, and exact sizes render at that size (the drawing is at the proportional place
when the aspect ratio is kept); a transparent background keeps alpha and a given color replaces white; the
operations list says what happened.

**SourceLoader_Load_SvgEmbeddingPng_DrawsPictureAndListsEmbedOperation**,
**SourceLoader_Load_SvgEmbeddingScad_DrawsModelOverTransparentBackground**,
**SourceLoader_Load_SvgEmbeddingVegaLite_KeepsChartBackgroundOpaque**,
**SourceLoader_Load_SvgEmbeddingNestedSvg_ListsOnlyTheOuterEmbedOperation**, and
**SourceLoader_Load_SvgEmbeddingByFileName_DrawsListedTypesAndSkipsOthers** (`SourceLoaderSvgImageTests.cs`; each test
writes the files beside a 40 by 40 red SVG in a temporary folder): a green PNG is drawn in its placement with red
around it, no notes, and one `embed` operation (`href`, `format` `png`, and its natural width of 4); the model fixture
is
drawn at the size of its placement (80 pixels at `--scale 2`) with the red diagram at the corner (a transparent
background); the chart fixture is opaque white in its placement, red below it, and drawn at the size of the
placement; `sub/inner.svg` draws `sub/img.png` although the outer folder has no such file, and only the outer
`sub/inner.svg` is an `embed` operation (the picture it embeds is not listed); a PDF named `.pdf` and a chart named
`.vl.json` are drawn (formats `pdf` and `vega-lite`) while the same contents named `.bin` and `.json` are two
`svg-image-unresolved` warnings (the type is decided by the file name).

**SourceLoader_Load_SvgEmbeddingEscapingReference_IsUnresolvedWarning** (three references: `../`, a drive letter, and an
`https` URL, with a valid picture one folder above the SVG; the many forms of escape are tested in the core
`FolderImageResolver` tests),
**SourceLoader_Load_SvgEmbeddingSymbolicLink_IsUnresolvedWarning** (skipped when a link cannot be created), and
**SourceLoader_Load_SvgEmbeddingMissingAndUnknownFiles_AreWarnings**: each escaping reference is the one
`svg-image-unresolved` warning (source `svg`, severity `warning`) naming the reference, nothing is drawn, and there
is no `embed` operation; a link is refused (the one coverage of the symbolic-link sandbox at tool level); a missing
file and a text file are two warnings. The sandbox, the dispatch, the cycle, depth, and count limits, and the file URI
rules are verified by the `ImageSources` subsystem tests and the Viewer tests.

**SourceLoader_Load_SvgEmbeddingTooLargePicture_IsWarningThenStrictError**: a 33-byte PNG that only declares 9000 by
9000
pixels in a 10 by 10 placement is the one warning `svg-image-too-large` (source `svg`, severity `warning`), not an
`svg-image-unresolved` warning, with the diagram unchanged; with strict mode it is an `unsupported-feature` error with
the message `'diagram.svg' embeds 'huge.png', which was not drawn: ...` and a hint naming `svg-image-too-large`.

**SourceLoader_Load_SvgEmbeddingModelWithNotes_MergesDiagnosticsAndEcho**,
**SourceLoader_Load_SvgEmbeddingStrict_UnresolvedAndUnsupportedAreErrors**,
**SourceLoader_Load_SvgEmbeddingBrokenFile_FailsWithItsCodeNamingBothFiles**,
**SourceLoader_Load_SvgEmbeddingBrokenFileInNestedSvg_NamesTheNestedFile**,
**SourceLoader_Load_SvgEmbeddingOuterOptions_DoNotReachEmbeddedFiles**, and
**SourceLoader_Load_SvgWithoutEmbeddedFiles_ReportsNothingExtra**: a model that echoes and uses `offset` gives an
`unsupported` note from `scad` whose message starts with `part.scad:` and keeps the echo line; with strict mode a
missing reference and the unsupported feature are `unsupported-feature` errors (the first with the message
`'missing.svg' embeds 'nothing.png', which was not drawn: ...` and the hint naming the code) while an SVG file without
references is unchanged; a truncated PNG, a model with a syntax error, and a chart with a missing data file are errors
with
`corrupt-input`, `scad-error`, and `vega-lite-error` whose messages name both `diagram.svg` and the embedded file; a
model with a syntax error inside `inner.svg`, which `outer.svg` embeds, names `'inner.svg' embeds 'bad.scad'`; a
scale of 3 and an unknown view name do not reach the embedded files (the raster keeps 4 pixels, the model is drawn at
30 pixels without a `usage-error`); the SVG fixture with strict mode has no notes, no echo, and no `embed` operation.

**SourceLoader_ComputeTargetSize_SizeOptions_FollowsPolicy** and
**SourceLoader_Load_RasterWithWidth_ResizesKeepingAspectRatio**: the size policy for none, scale, width, height, and
both, with rounding and the one-pixel minimum; a raster is resized and the operation recorded.

**SourceLoader_ComputeTargetSize_BeyondMaximum_ThrowsSizeLimit**,
**SourceLoader_Load_PngLargerThanMaximum_ThrowsSizeLimitBeforeDecoding**, and
**SourceLoader_Load_SvgWithHugeNaturalSize_NeedsSizeOption**: exact, scaled, aspect-derived, and natural sizes beyond
8192 are `size-limit` with messages and hints that say which size is too large; 8192 itself is allowed; a PNG header
declaring 9000x9000 is refused for its size (not as corrupt) while probing still reports it; an SVG of 100000x50 is
refused at its natural size and renders at a width that is allowed.

**SourceLoader_Load_RasterBackground_FlattensOnlyWhenAsked** and
**SourceLoader_FormatColor_OpaqueAndTranslucent_FormatsHex**:
a raster keeps its transparency by default and with a transparent background, and is flattened when a color is
given; colors are written `#RRGGBB` or `#RRGGBBAA`.

**SourceLoader_Load_JpegWithExif_AppliesOrientationUnlessDisabled** and
**SourceLoader_Load_JpegWithExifAndWidth_SizesTheOrientedImage**: orientations 6 and 8 swap the size, 3 keeps it, 1
changes nothing, and `AutoOrient = false` leaves the stored image; the operation is recorded; a requested width
applies to the upright image and the result equals orient-then-resize. (The reading of every orientation and the
mapping of the pixels are verified by `JpegCodecExifTests.cs` and `SurfaceExifOrientationTests.cs` in the core test
project.)

**SourceLoader_Load_DamagedFile_ThrowsToolExceptionOrLoads**,
**SourceLoader_Load_DamagedDocument_ThrowsToolExceptionOrLoads**,
**SourceLoader_Load_GarbageWithKnownExtension_ThrowsCorruptInput**,
**SourceLoader_Load_MalformedSvg_ThrowsCorruptInput**,
**SourceLoader_Load_PdfWithBadContentOrEncryption_ThrowsExpectedErrors**,
**SourceLoader_Load_PackageWithBrokenPart_ThrowsCorruptInput**,
**SourceLoader_Read_AllowListedExceptions_AreMappedToErrorCodes**,
**SourceLoader_Read_MultiLineMessage_IsReducedToOneLine**,
and **SourceLoader_Read_UnexpectedExceptions_Propagate**: the error-mapping checks described above; in addition a PDF
whose rectangle operator has too few operands is `corrupt-input`, one that declares an unsupported encryption handler
is `unsupported-feature`, and a valid archive whose slide, page, or presentation part is not valid is `corrupt-input`.
Garbage in a file named `.scad` is a `scad-error` rather than `corrupt-input`, and garbage or a truncation of a chart
is a
`vega-lite-error`, and garbage in a file named `.mmd` a `mermaid-error`, the kinds whose damage is a syntax error (a
diagram is left out of the shared truncation sweep, as a model is, because a cut diagram is usually still valid; it has
its own sweep);
**SourceLoader_OpenPages_UnhandledKind_IsNotAToolException**:
facts that name a kind the loader has no case for
raise an `InvalidOperationException`, which is a defect and not an expected error.

**SourceLoader_PublicMethods_NullArguments_ThrowArgumentNullException** and
**SourceLoader_OpenPages_NullArguments_ThrowArgumentNullException**: each null argument throws.

**SourceLoader_Load_ModelWithAnchors_CollectsThemWithPixelPositions**: a 20 by 20 by 4 box centered on the origin, with
two anchors at `[0, 0, 2]` and `[1, 0, 2]` and a malformed one, is loaded from the top at 100 by 100; the loaded source
has the two anchors in order with their world points, the first at the pixel `(50, 50)` (the center of the picture) and
the second to its right, and the malformed declaration is a `scad-warning` note that contains `scad-anchor-invalid:`;
a model without anchors has none.

**SourceLoader_ExtensionKinds_CannotBeChangedThroughACast**, **SourceLoader_ScadViews_CannotBeChangedThroughACast**, and
**SourceLoader_ScadViewOptionsDefaultDefines_CannotBeChangedThroughACast**: the table of extensions, the list of the
seven
views, and the default `Defines` of a `ScadViewOptions` are not a `Dictionary`, an array, or a `List`; adding,
replacing,
removing, and clearing through `IDictionary` or `IList` throw `NotSupportedException`; and the table and the list are
unchanged afterwards (`.PNG` still means PNG, and there are still seven views).

**SourceLoader_Load_SvgEmbeddingModelWithNotes_NotesCarryTheEmbeddedFileAsOrigin**: an SVG file that embeds a model
that reports a note has that note with the origin `["part.scad"]` (the model file), and a link of the SVG file itself
that is not drawn has an empty origin.

**SourceLoader_Load_SvgEmbeddingNestedSvgWithMissingFile_OriginIsTheNestedFile**: an SVG file that embeds a second SVG
file whose own link does not resolve reports a note with the origin `["inner.svg"]`, the file that holds the link.

**SourceLoader_Load_ScadAnchorWarning_CarriesDetailAndLocation**,
**SourceLoader_Load_ScadGenericWarning_HasNoDetailButHasLocation**,
**SourceLoader_Load_ScadUnsupportedFeature_CarriesLocation**, **SourceLoader_Load_VegaLiteNotes_CarryDetailAndPointer**,
**SourceLoader_Load_DocumentNote_HasNoLocation**,
**SourceLoader_Load_SvgEmbeddingModel_NotesKeepOriginDetailAndLocation**,
and **ImageDiagnostic_Equals_ComparesDetailAndLocation** (`SourceLoaderDiagnosticDetailTests.cs`): a malformed anchor is
a `scad-warning` whose `Detail` is `scad-anchor-invalid` and whose `Location` has the file, line, and column; a warning
whose library code is the generic one has a location and no detail; an unsupported construct keeps its feature as the
code
and gets a location; a chart note has the `vl-*` detail and a JSON pointer; a presentation note has a page and no
location; the notes of an embedded model keep the detail, the location, and the origin; and two notes are equal only
when
the new members are equal. The notes of a diagram are verified by
**SourceLoader_Load_Mermaid_MapsWarningsAndUnsupportedFeatures** (above): a warning is `mermaid-warning` with the
`mermaid-*` code of the library as its detail and the line and column as its location.

**SourceLoader_InputFromMemory_LoadsLikeTheFile**,
**SourceLoader_SourceInput_FileAndMemory_ExposeNameFolderLengthAndStream**,
**SourceLoader_LoadContext_Charge_IsCalledOnceWithTheSizeOfTheSurface**,
**SourceLoader_LoadContext_ChargeRefused_StopsBeforeDecoding**,
**SourceLoader_LoadContext_CanceledToken_ThrowsOperationCanceled**,
**SourceLoader_LoadContext_ScopedResolver_LoadsNestedPicturesAndAddsNoNotes**,
**SourceLoader_LoadContext_ScopedResolverNull_LeavesLinksUnresolved**, and
**SourceLoader_LoadContext_ReturnFittedPage_SkipsTheMargin**
(`SourceLoaderInputTests.cs`): a file loaded from memory gives the kind, size, pixels, and notes of the same file loaded
from disk for every format; the input exposes its name, folder, length, and a fresh stream; the charge hook is called
once per surface with its size before it is decoded, a refusal stops the load before decoding, a canceled token throws,
a scoped resolver draws the nested pictures and adds no notes of its own while a null one leaves the links unresolved,
and
a document page comes back at its fitted size without the margin.

**SourceLoader_Load_InvalidOptionsAndMissingFile_ThrowsUsageErrorBeforeTouchingTheFile**,
**SourceLoader_LoadAndOpenPagesOfInfo_InvalidOptions_ThrowUsageErrorBeforeTouchingTheFile**,
**SourceLoader_FindOptionProblem_BoundaryOptions_AreAccepted**,
**SourceLoader_Load_WidthAboveTheMaximum_IsStillSizeLimit**,
**SourceLoader_Load_PageBeyondTheLast_IsStillPageOutOfRange**,
**SourceLoader_FindOptionProblem_UnknownView_NamesTheViewAndListsTheViews**,
and **SourceLoader_MaxDpi_Is2400** (`SourceLoaderOptionsValidationTests.cs`): a table of malformed options (an undefined
filter, a width or height of 0 or below, a scale of 0, a negative one, NaN, infinity, 0.0009, and 8193, a resolution of
0, a negative one, NaN, infinity, and 2401, an elevation of 91 or -91, a time of 2e9, a triangle limit of 0, an unknown
view, a bad variable name) is a `usage-error` from `Load(path)`, `Load(info)`, and `OpenPages` even when the file does
not
exist; the values at the boundaries are accepted; a width of 9000 is still `size-limit` and a page beyond the last still
`page-out-of-range`; the unknown-view message names the views; and the maximum resolution is 2400. A Mermaid theme that
is not one of the canonical theme names is checked the same way, before the file is touched
(**SourceLoader_Load_MermaidTheme_IsAppliedAndValidated**, above).

**SourceLoader_Load_StrictWithFragmentProblems_FailsOnlyForRefusedLinks** and
**SourceLoader_Load_FragmentProblems_AreNotesOfTheDocument**
(`CanvasNetViewerFragmentTests.cs`): an SVG file with a link to a page that does not exist or with an invalid page value
fails with `unsupported-feature` in strict mode, while an unknown or duplicate parameter is only a note; without strict
mode
the four codes are notes of the document, with the embedding file as origin where the link is in an embedded file.

**SourceLoader_Load_VegaLiteImagePngAndScad_DrawsBothAndListsEmbedOperations**,
**SourceLoader_Load_VegaLiteImagePdfPage_DrawsTheChosenPage**,
**SourceLoader_Load_VegaLiteImageNestedChart_ResolvesFromItsFolder**,
**SourceLoader_Load_VegaLiteImageSelfCycle_IsUnresolved**,
**SourceLoader_Load_VegaLiteImageUnresolvedLink_IsOneWarningAndNothingIsDrawn**,
**SourceLoader_Load_VegaLiteImageStrict_UnresolvedLinkIsAnError**,
**SourceLoader_Load_VegaLiteImagePageOutOfRange_IsWarningThenStrictError**,
**SourceLoader_Load_VegaLiteImageBrokenFile_FailsWithItsCodeNamingBothFiles**,
**SourceLoader_Load_VegaLiteImageMoreThanTheLimit_IsBudgetExceeded**,
**SourceLoader_Load_SvgEmbeddingChartWithImage_DrawsTheChartsPicture**, and
**SourceLoader_Probe_VegaLiteImageChart_ReadsTheSizeWithoutThePictures** (`SourceLoaderVegaLiteImageTests.cs`): charts
with
an image mark are loaded from a temporary folder through the real sandboxed resolver of the viewer. A chart that shows a
generated green PNG (`logo.png`) and the model of the fixtures (`part.scad`) in two 40 by 40 boxes draws the PNG in its
box
(20 to 60 across and 30 to 70 down: green inside, white at the pixels next to it) and a model picture in the other box
(not
all white; white between and beside the boxes) in strict mode without notes and with one `embed` operation for each
file,
`logo.png` and `part.scad`; a link `report.pdf#page=2` draws the green middle of page 2 and lists the link with its
fragment; a chart that shows `sub/inner.vl.json`, whose own picture `logo.png` lies in `sub`, draws the inner chart's
white paper and green picture in the middle; a chart that shows itself is the warning `svg-image-unresolved` (cycle)
and,
in strict mode, an `unsupported-feature` error naming the chart and the link. A missing file, `../secret.png`,
`C:/secret.png`
and `https://example.invalid/secret.png` are each exactly one note, the resolver's `svg-image-unresolved` (no
`vl-mark-image-unresolved` next to it), nothing is drawn and no operation is listed; strict mode fails naming the chart
and
the link with the code in the hint; a page 9 is `image-page-out-of-range` and an error when strict; a truncated PNG is
`corrupt-input` and a model with a syntax error `scad-error`, both naming the chart and the file; 257 rows with distinct
missing links are `budget-exceeded` naming `MaxImages`; an SVG diagram that shows a chart whose picture lies beside the
chart
draws it through the resolver of the diagram; and probing a chart whose picture does not exist reads its size (100 by
100)
and its mark types.

**SourceLoader_Load_MermaidScadAndVegaLitePictures_DrawsBothAndListsEmbedOperations**,
**SourceLoader_Load_MermaidPngPicture_FillsItsBox**, **SourceLoader_Load_MermaidSelfCycle_IsUnresolved**,
**SourceLoader_Load_MermaidPictureOutsideTheSandbox_IsRefused**,
**SourceLoader_Load_MermaidBrokenPicture_FailsWithItsCodeNamingBothFiles**,
**SourceLoader_Load_SvgEmbeddingDiagramWithPicture_DrawsTheDiagramsPicture**,
**SourceLoader_Load_MermaidPictureEnlargingTheLayout_ChargesAndReportsTheDrawnSize**,
**SourceLoader_Load_MermaidPictureEnlargingTheLayout_IsRefusedBeyondTheBudget**, and
**SourceLoader_Load_MermaidPictureEnlargingTheLayout_ReportsTheUnscaledDrawnSize** (`SourceLoaderMermaidImageTests.cs`):
diagrams with picture nodes (`@{ img: ... }`) are loaded from a temporary folder through the real sandboxed resolver of
the viewer. A flowchart whose two nodes show the model of the fixtures (`part.scad`, 120 by 120) and the two-bar chart
(`chart.vl.json`) draws more than 500 pixels of the bar color and changes more than 500 pixels of the half that holds
the model, in strict mode without a note, and lists exactly one `embed` operation for each file, in order (`part.scad`
with the format `scad` and the drawn size 120 by 120, then `chart.vl.json`); the same diagram in a folder without the
files has the same size, no bar color, no `embed` operation, and exactly two notes, each the resolver's
`svg-image-unresolved` (source `svg`) and not also the library's `mermaid-image-unresolved`. A generated 4 by 4 green
PNG in a 60 by 60 node fills its box (between 50 by 50 and 61 by 61 green pixels) with one `embed` operation and no
note. A diagram that shows itself is the warning `svg-image-unresolved` that names the cycle and, in strict mode, an
`unsupported-feature` error whose message says that `'loop.mmd' embeds 'loop.mmd', which was not drawn`. Each of
`../secret.png` (an existing picture one folder up), `https://example.invalid/secret.png`, `C:/secret.png`, and
`missing.png` is exactly one warning of the resolver (source `svg`) that names the link, draws nothing green, lists no
`embed` operation, and is an `unsupported-feature` error naming the diagram and the link in strict mode. A model with a
syntax error shown by a node is a `scad-error` whose message names both the diagram and the model. And an SVG file
that places `sub/flow.mmd` (whose picture `logo.png` lies in `sub`) over a red rectangle draws the green picture through
the resolver of the SVG file, keeps the red where the diagram is transparent, reports nothing in strict mode, and lists
the one `embed` operation `sub/flow.mmd`. A diagram whose one picture node has no `w` and `h` and shows a 400 by 300
PNG, loaded at its natural size with a recording `Charge` hook, returns a surface larger than the probed size, the
charges add up to exactly the pixels of that surface, and the natural size of the result equals the surface size. The
same diagram under a `Charge` hook that holds a budget of 10 000 pixels (more than the probed size, less than the drawn
one) throws `ImageSourceBudgetException` whose requested pixels are the pixels of the surface drawn without a budget and
whose remaining pixels are the whole 10 000 (the charge before the draw given back), and the hook holds only the probed
size. Loaded at a scale of 2 and of 1.5, the same diagram reports the natural size of the diagram drawn at a scale of 1
and returns a surface of that size times the scale, rounded.

**Chart parameters** (`SourceLoader_Load_ChartOptions_AreAppliedAndReportedInTheOperation`,
`SourceLoader_Load_InvalidChartOptions_ThrowsUsageErrorBeforeTouchingTheFile`, and the tests of
`CanvasNetViewerParameterFragmentTests.cs` for an unknown name):
loading a chart with `cutoff` and `brush` in `SourceOptions.VegaLite` gives a `render` operation whose `params` field is
`["brush={\"y\":[2,4]}", "cutoff=4"]` (name order, JSON values), a page narrower than the same chart without the
options, and a `SourceInfo` whose width is that
of the page, while the render operation of the chart without options has no `params` field; an empty parameter name, a
value nested 40 levels deep and a null dictionary are
`usage-error` before a missing file is looked for; an unknown name is `vega-lite-error` naming the parameter and listing
`declared: brush, cutoff`, and the warning
`vl-param-unknown` with `WarnOnUnknownParameters`.

**InputReference_Parse_NoHash_IsThePath**, **InputReference_Parse_SplitsAtTheHashOnlyWhenNoFileHasTheName**,
**InputReference_Parse_NoFile_SplitsAtTheFirstHash**, **SourceLoader_ProbeInputReference_NoFragment_HasNoSelection**,
**SourceLoader_ProbeInputReference_Document_PageIsCheckedAndReplacesTheOption**,
**SourceLoader_ProbeInputReference_InvalidValue_IsImageFragmentInvalid**,
**SourceLoader_ProbeInputReference_OverTheLimitsOrMissing_Throws**,
**InputSelection_ApplyTo_Model_MergesVariablesAndReplacesTheView**,
**InputSelection_ApplyTo_Chart_MergesParametersByName**,
**InputSelection_ApplyTo_Diagram_FragmentThemeReplacesTheCommandTheme**,
**InputSelection_Diagnostics_AreTheCodesOfALinkAndReportedOnce**,
**SourceLoader_Load_InputWithFragment_DrawsWhatTheOptionsWouldDraw**,
**SourceLoader_PublicMethods_NullArguments_ThrowArgumentNullException**, and
**CanvasNetViewer_Render_ScadViewportFragment_FixesTheCamera** (the fragment of an input path): the scenarios of
`InputSelectionTests.cs` use real files in a temporary folder. `Parse` of a text without a `#` is the path; with the
files `a.scad`, `a.scad#view=top` and `p#1.scad` on disk, `a.scad#def.width=3` splits into the file and `def.width=3`,
the real name `a.scad#view=top` is the path with no fragment, `p#1.scad` is a path and `p#1.scad#view=front&def.c=#ff0`
is that file with the fragment `view=front&def.c=#ff0` (a `#` inside the fragment is kept); and with no such file, the
first `#` splits (`missing.scad#view=top`, `missing#a#b`, `#only`, `trailing#`). Probing an input without a fragment
gives no `Selection` and `Specifier` is the path; a null argument is rejected. For
`part.scad#view=top&def.width=20&def.$vpt=[0,0,2]` the selection merged over `front`, azimuth 30 and the variables
`width=5`, `depth=9` has the view `top` (still given), the azimuth 30, `width=20`, `depth=9`, and the list `[0,0,2]`,
applying it twice gives the same, and a fragment with no view keeps `front`. For a PDF `page=2` is the selection page
(and replaces a page of 7), `page=3` is `page-out-of-range`, and `slide=2` (a presentation parameter) is the warning
`image-fragment-unknown-parameter` with no page. Each of `def.width=[1,2`, `view=sideways`, `def.2x=1`,
`param.cutoff={bad` and `page=x` is `image-fragment-invalid` whose message has the fragment and that has a hint, and so
is `theme=midnight` on a diagram; a fragment of 2,049 characters and one of 33 parameters are `image-fragment-invalid`;
a missing file with a fragment is `input-not-found` that says a file of that name does not exist either, and without a
fragment it says nothing of the kind. The notes: a model with `wide=1&def.width=1&def.width=2` has the unknown and the
duplicate warning (source `input`, severity `warning`, in that order); a chart with `param.cutoff=2&param.nope=1` has
one `vl-param-unknown` that lists `declared: cutoff` and a selection with only `cutoff`; a PDF with `extra=1` reports
the warning with the first page that is read and not with the second. A chart parameter of the fragment is merged over
those of the command by name (the fragment wins, the other one stays, `WarnOnUnknownParameters` stays false). For a
diagram, `#theme=Dark&page=2` gives the selection theme `dark` (the canonical name) and no page, with the one warning
`image-fragment-unknown-parameter` (a diagram takes no page); merged over the command theme `forest` it gives `dark`,
while a fragment without a theme keeps `forest`; and loading the probed input draws in `dark` (the `render` operation
lists `theme` `dark`) and reports the warning. A model probed with
`view=top&def.width=20` and loaded into 64 by 64 pixels has exactly the pixels of the same model loaded with those
options and no fragment. In `CanvasNetViewerParameterFragmentTests.cs` the same variables, as the fragment of a link
in an SVG document, fix the camera: a red box of width 5 and one of width 20 drawn with `def.$vpd=200` are as tall as
each other and four times as wide (within a pixel), the same two without it are not as tall as each other, and
`def.$vpt=[10,0,0]` moves the box about 12 pixels to the left.
