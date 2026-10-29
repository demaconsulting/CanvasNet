### DiscoveryCommands

<!-- cspell:ignore canvasnet jpeg pptx vsdx dpi scad openscad -->

#### Verification Approach

`DiscoveryCommands` is verified with unit tests in `DiscoveryCommandsTests.cs` and
`DiscoveryCommandsDocumentTests.cs` (the page options and the documents), and `DiscoveryCommandsModelTests.cs` (the
description of an OpenSCAD model and the link between the format table and the loader), and `DiscoveryCommandsChartTests.cs`
(the listing of the chart format and the description of a Vega-Lite chart). The `formats` command is run
in-process with fixed versions (through its `internal` constructor) so that the output does not depend on the
build version; the JSON output is compared byte for byte with a golden document. The `info` command is run
in-process on files generated in a temporary directory (rasters written with the codecs, an EXIF segment injected
into a JPEG, and the PDF, presentation, and drawing the test project writes) and on the original SVG fixture, and
its JSON output is compared with the exact expected document. The
format tables are checked against `SourceKind`, `OutputFormat`, `SourceLoader.ExtensionKinds`, and
`ImageWriter.ExtensionFormats`, so a listed format is one the code knows. The tests create a `Context` with
controlled arguments and capture the console with the `ToolHost` helper.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the
console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The text listing shows the input and output formats, the limits, and the versions.
- The JSON document equals the golden document exactly.
- A stray argument or option is a usage error.
- The format tables name exactly the source kinds and output formats the code implements, in order.
- The listing names the `vega-lite` format (kind `chart`, extensions `.vl.json` and `.vl`) in text and JSON, and `info`
  describes a chart as one text line and as the `image` and `chart` objects.
- `info` describes every readable kind as one text line and as JSON, reports the EXIF orientation only when the file
  has one, describes the pages of a document, and reports a bad input as an expected error.

#### Test Scenarios

**DiscoveryCommands_Formats_Metadata_DescribesCommand**: the command is named `formats`, has no aliases and no
options, and has a usage line with examples.

**DiscoveryCommands_Formats_TextOutput_ListsFormatsLimitsAndVersions**: the text has both sections, representative
rows (PNG as a single-page raster, PDF as a multi-page scalable document, JPEG as an output with quality), the
limits, and both versions, and contains no JSON.

**DiscoveryCommands_Formats_JsonOutput_MatchesGoldenDocument**: with `--json` the output is exactly the golden
document followed by a line ending.

**DiscoveryCommands_Formats_DefaultVersions_ReportsRunningVersions**: the default constructor reports
`Program.Version` and a non-empty library version.

**DiscoveryCommands_FormatTables_AreWellFormed**: format names are unique; extensions are lower case, start with a
dot, and are unique within a table; every input kind is one of the five documented kinds (`raster`, `vector`, `document`,
`model`, and `chart`).

**DiscoveryCommands_Formats_PositionalArgument_ThrowsUsageError** and
**DiscoveryCommands_Formats_UnknownOption_ThrowsUsageError**: a stray argument or an unknown option is a
`usage-error` naming the command.

**DiscoveryCommands_Formats_NullArguments_ThrowsArgumentNullException**: null arguments are rejected.

**DiscoveryCommands_FormatTables_InputFormatsMatchSourceKinds**,
**DiscoveryCommands_FormatTables_OutputFormatsMatchOutputFormatsOfWriter**,
**DiscoveryCommands_FormatTables_QualityRangeMatchesWriter**, and
**DiscoveryCommands_FormatTables_EveryListedInputFormatIsReadable**: the input table has one row per `SourceKind`, in
order,
with the extensions the loader maps to that kind (and, for the chart, the suffix `.vl.json`); the output table has
one row per `OutputFormat`, in order, with the
extensions the writer accepts, and only JPEG is lossy; the quality range and default are the writer's; a file of each
listed input kind (including the model) is probed by the loader as the kind the row names, only documents are
multi-page, the `model` row is the model kind, and the model row is a vector, single-page `.scad` format; the golden document
includes the `vega-lite` row.

**DiscoveryCommands_Info_Metadata_DescribesCommand** and **DiscoveryCommands_Info_DefaultRegistry_RegistersInfo**: the
command is named `info`, has no aliases and the two page options, and has a usage line with examples; the default
registry offers it.

**DiscoveryCommands_Info_TextOutput_DescribesFile**: one line for a PNG (`png, 12x8 px, 4 channels with alpha`, the
byte count) and one for the SVG fixture (`svg, 100x80 px (intrinsic size)`).

**DiscoveryCommands_Info_JsonOutput_MatchesGoldenDocument**: the JSON for the SVG fixture is exactly the documented
document.

**DiscoveryCommands_Info_JpegOrientation_IsReportedOnlyWhenPresent** and
**DiscoveryCommands_Info_EachKind_ReportsFormatAndSize**: a JPEG with an EXIF orientation reports it in text and JSON,
one without has no such key, three channels, and no alpha; each readable kind reports its format name, a size, and
that it can be decoded.

**DiscoveryCommands_Info_PdfText_DescribesFileAndFirstPage** and
**DiscoveryCommands_Info_PageOptions_ChoosePagesToDescribe**: the text for the two-page PDF is exactly the file line and
the line of page 1 (pixels at 96 dpi, points, rotation 0); `--page 2` describes only the rotated second page;
`--all-pages` of a presentation lists its slides (called slides) and of a drawing its pages with their names.

**DiscoveryCommands_Info_DocumentJson_ListsPageCountAndPages**: a document has a `document` object and no `image`
object; the default is the first page, `--all-pages` lists both, `--page 2` of the drawing lists the page with its
name and no rotation, each page object equal to the exact text.

**DiscoveryCommands_Info_BadPageOptions_ThrowExpectedErrors** and **DiscoveryCommands_Info_PageOptionsOnImage_DescribeTheImage**:
a page that does not exist (for a document or for a PNG) is `page-out-of-range`; both options together are a usage
error; a PNG described with `--page 1` or `--all-pages` gives the same text as without.

**DiscoveryCommands_Info_BadInput_ThrowsToolException**, **DiscoveryCommands_Info_TruncatedDocument_ThrowsCorruptInput**,
**DiscoveryCommands_Info_CommandLineMistakes_ThrowUsageError**,
and **DiscoveryCommands_Info_NullArguments_ThrowsArgumentNullException**: a missing file, a truncated PNG, a PDF with
no pages, and a text file are `input-not-found`, `corrupt-input`, `corrupt-input`, and `unsupported-format`; each
document cut after 300 bytes is `corrupt-input`; no input, two inputs, and an unknown option are usage errors; null
arguments throw.

**DiscoveryCommands_Info_ModelText_ListsModulesFunctionsAndDirectives**,
**DiscoveryCommands_Info_ModelWithoutDeclarations_DescribesCounts**, **DiscoveryCommands_Info_ModelJson_HasModelObject**,
and **DiscoveryCommands_Info_BrokenModelAndPageTwo_ThrowExpectedErrors**: the text for a model with two modules, a
function, an include, and a use is exactly the counts line (`2 modules, 1 function, 2 include/use directives`) and one
line for each module, function, and directive with its line; a model with nothing declared has zero counts, and one
module is `1 module`; the JSON is exactly the documented document, with the `model` object (modules, functions, and
directives with kind, path, and line) and no `image` or `document` object; a model with a syntax error is a
`scad-error` and page 2 of a model is `page-out-of-range`.

**DiscoveryCommands_Formats_IncludesVegaLiteChart**: the table row is the `chart` kind, a vector, single-page format with
the extensions `.vl.json` and `.vl`; the JSON listing has the same row and the text listing shows it as a single-page,
scalable chart; a file of the kind is probed by the loader as a chart.

**DiscoveryCommands_Info_VegaLiteWritesChartObject**: for a chart with a title, a layer of a `bar` and a `rule` mark, a
parameter, and an inline and a named data source, the JSON is exactly the documented document - the `image` object
with the
laid-out size, four channels, and alpha, then the `chart` object with the title, the schema major version, the
marks, the data
references (kind, row count, and line), and the parameters, and no `model` or `document` object - and the text is one line
with the size, the counts, the title, the schema version, and the byte count.

**DiscoveryCommands_Info_ChartWithoutOptionalFacts_OmitsThemAndReportsErrors** and
**DiscoveryCommands_InfoUsage_NamesVegaLite**: a chart of one `tick` mark with no data, schema, or title has no
`title` and no
`schemaMajorVersion` key, empty data and parameter lists, and zero counts in the text; a malformed chart is a
`vega-lite-error`
and page 2 of a chart is `page-out-of-range`; the usage text names Vega-Lite charts and has a chart example.
