### DependencyHasher

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`DependencyHasher` collects every file a picture of the Pandoc filter is drawn from, as the parts of its cache key, so
that a change to any of them draws the picture again. It is a literal port of the dependency walks of the Lua filter
(version 16): the includes of an OpenSCAD model, the data files and pictures of a Vega-Lite chart, the pictures of a
Mermaid diagram, the include files of a PlantUML diagram, and the image links of an SVG drawing, for which it also
decides whether the drawing must be flattened (a link to a `.mml` equation makes it composite). An equation is hashed
as its own file only. It also finds the first local file the text of a Vega-Lite or OpenSCAD code block names, which
the block cannot reach because its text is drawn from the cache folder.
The unit is the internal sealed partial class `DependencyHasher`, split over `DependencyHasher.cs` (entry points, model
walk, shared picture walk, file-access reads), `DependencyHasher.Chart.cs`, `DependencyHasher.Mermaid.cs`,
`DependencyHasher.PlantUml.cs`, `DependencyHasher.Svg.cs`, and `DependencyHasher.Blocks.cs` (the block check, with the
supporting record `BlockReference`).

#### Data Model

**MaxDepth** (`internal const int`, 64): the most levels of includes, nested charts and diagrams, linked drawings, or
JSON nesting that are followed.

**MaxDependencies** (`internal const int`, 512): the most files one picture may be drawn from.

**Part** (`byte[]`): the file's label (`FilterPaths.Label`, its path from the picture's folder with forward slashes), a
line feed, and the file's bytes. A missing file an SVG drawing links is the part `missing <label>` (no line feed, no
bytes). Parts are kept in the order they are met (`_parts`).

**Seen** (`_seen`, `Dictionary<string, Seen>` by the exact collapsed path, ordinal): `Taken` while a path is walked or
once it was, and for an SVG drawing afterwards `Composite` or `Plain`. Each path is taken once, so a cycle or a picture
that links back ends.

**Source** (`_source`): the picture's source file, the input the tool is given, which no mode judges.

**Policy** (`_policy`): the `FileAccessPolicy` the tool draws the picture under (`FilterSettings.PolicyOf`), or `null`
in the default mode. **Resolver** (`_resolver`): the core `FileReferenceResolver` that reads the referenced files
through the policy, created on first use with the source's folder as its base directory (the trusted origin the tool
judges the picture's references from), the policy, and no size limit but the largest array; it opens each file at
most once per picture.

**DenyAll** (`internal static FileAccessPolicy`): the policy, named `deny-all`, that `FilterSettings.PolicyOf` gives
when the tool would refuse the file-access arguments; the walk then reads no referenced file at all.

One instance walks one picture and is not thread-safe; the static entry points are, as each call has its own instance.

#### Key Methods

**ForPicture(kind, source, name, policy)**: creates an instance and walks the picture by kind from the folder of
`source`: `scad` with `AddScad`, `vegalite` with `AddChart`, `mermaid` with `AddMermaid`, `plantuml` with `AddPlantUml`;
any other kind (a `pdf`,
`pptx`, or `vsdx` document, and a `mathml` equation, which references no other file, so no name in its text is
followed) is the single part labeled with the file name of `source`. Returns the parts.

**ForSvg(source, policy)**: walks an SVG drawing with `AddSvg` and returns the parts and whether the drawing is
composite.

**FindBlockReference(kind, text)** (static, in `DependencyHasher.Blocks.cs`): the first local file the text of a code
block names, as a `BlockReference(Action, Path, Line)`, or `null`. For `vegalite` the text (without a leading byte
order mark) is parsed as JSON with the walk's options and given to the walk's own collectors (`CollectDataUrls`,
`CollectImageLinks`, and `CollectRowLinks` when a url channel names fields); a data URL counts when
`FilterPaths.LocalDataFile` resolves it, a picture link when `FilterPaths.LocalImageFile` resolves it as written or
percent-decoded, so the check refuses exactly what the walk would follow and leaves remote links and `data:` URIs to
the tool. The collectors return the JSON string values themselves (the last of a repeated member, as the walk follows
it), not their text. `OffsetOf` finds each value's position as the offset of its raw text
(`JsonMarshal.GetRawUtf8Value`) in the parsed bytes, which `JsonDocument.Parse` keeps without copying for the life of
the document. `FirstInText` then takes the local value written first (a data URL on a tie), which gives the action
(`reads the data file` or `draws the picture`) and its line (one more than the line feeds before its opening quote). So
the same text in another member, such as a `title`, never counts, and a name both read and drawn gets the action of the
member written first. For `scad` the text is parsed with `ScadDocument.Parse`, and the first of its `Directives` gives
`includes` or `uses`, its path, and its line, which is the lexer's location of the directive keyword, so a comment or
string naming the same file does not move it; the parser ignores comments and never takes a comparison such as
`a < b && c > d` for a directive, and `import()` reads no file.
Text that cannot be parsed (`JsonException`, `ScadSyntaxException`, `InvalidDataException`) gives `null`, and the tool
then reports the error when it draws the block. Any other kind gives `null`: Mermaid, PlantUML, and MathML blocks keep
their behavior.

**AsBytes(content)** / **FromBytes(text)**: read bytes as Latin-1 (one character per byte, so the ASCII-only classes of
the Lua patterns match), and turn such a text back into the UTF-8 text it holds.

**AddScad(root, path, depth)** (`add_scad_sources`): takes the path once, reads it (an unreadable file is skipped; the
tool reports it), adds its part, and follows every `include <...>` and `use <...>` (Lua patterns
`include%s*<%s*([^>\r\n]-)%s*>` and `use%s*<%s*([^>\r\n]-)%s*>`, comments included) relative to the including file's
folder when `DependencyExists`; all includes first, then all uses; labels are relative to the model's folder.

**AddChart(path, root, depth)** (`add_chart_sources`): takes the specification, adds it labeled with its file name,
skips a leading UTF-8 byte order mark, and decodes it as JSON (a text that is not JSON, or not an object or array, adds
only itself). `AddChartData` collects the string `url` of every `data` object, resolves each with
`FilterPaths.LocalDataFile` from the chart's folder (remote and blank URLs are skipped; no percent-decoding), keeps the
existing ones, sorts them by UTF-8 bytes, and adds each. `AddChartPictures` collects the `url` of a `mark` object, the
string `value` or `datum` and the `field` names anywhere under a `url` channel, and, when there are field names, the
values of those fields in the inline rows (`values` of a data object and the arrays of `datasets`); each link is
taken as written and percent-decoded (the tool's image resolver decodes a local link before it opens it), resolved with
`FilterPaths.LocalImageFile`, deduplicated, kept when it exists, sorted, and walked with `AddImageFiles`. Object members
are read as the Lua JSON decoder made them (a repeated name once, with its last value). Labels of a chart's files are
relative to the chart's own folder.

**AddMermaid(path, root, depth)** (`add_mermaid_sources`): takes and adds the diagram, then collects the picture links
of its nodes: `img: "x"`, `img: 'x'`, and unquoted `img: x` (up to a comma, a brace, or the end of the line, trailing
white space trimmed), and the `src` (double-quoted, single-quoted, or unquoted) of an `<img ...>` tag, all tags and
names in any letter case. Each link is tried as written and percent-decoded with `FilterPaths.LocalImageFile` from the
diagram's folder (fragment and query cut, remote skipped); the existing files are deduplicated, sorted, and walked with
`AddImageFiles`.

**AddPlantUml(path, root, depth)**: takes the file once (an include cycle or a repeated include ends), refuses more
than `MaxDepth` levels, adds the file, and follows every `!include`, `!include_once`, and `!include_many` line
(`CollectPlantUmlIncludes`: the rest of the line after the directive at the start of a line, trailing white space
trimmed and surrounding quotes removed) relative to the folder of the file that names it, as the PlantUML library
resolves it; an include the library never follows is skipped (a path that starts with `<` or `[`, a URL, and a part of
a file after `!`), a path built from a variable names no file, and a file that does not exist adds nothing. The keys of
every other kind of picture are unchanged.

**AddImageFiles(files, root, depth)** (`add_image_files`): for each file not yet seen, by its name end in ASCII lower
case: `.scad` adds a model (`AddScad`, depth 0), `.vl.json` or `.vl` a nested chart labeled from its own folder,
`.mmd` or `.mermaid` a nested diagram, `.puml`, `.plantuml`, or `.pu` a nested PlantUML diagram (`AddPlantUml`), `.svg`
a nested drawing, and any other file (a `.mml` equation included, a leaf) is added as it is.

**AddSvg(path, root, depth)** (`add_svg_sources`): marks the drawing `Taken`, adds it (an unreadable drawing is
`Plain`), and resolves each link `CollectSvgLinks` finds: the fragment and query are cut; a `file:` URI is decoded as it
is read, any other link must be local both as written and percent-decoded (a remote or `data:` link is skipped). An
existing file is walked; a missing file of a kind the tool draws (`PictureAttributes.KindOf`) is a `missing` part. In a
restrictive mode a file the policy denies (`Probe`) is treated the same way, and any denied file makes the drawing
composite, so the flatten reports the denial and the build stops; a missing nested drawing does not. Both lists are
deduplicated and sorted; the missing parts come first. The drawing is composite when it has a missing part, links a
denied file, links a file of a kind the tool draws, or links a nested drawing that is composite (a nested drawing is
walked once; one still being walked, as in a cycle, counts as not composite). It ends as `Composite` or `Plain`.

**CollectSvgLinks(content)** (`collect_svg_links`): a scanner over the text of bytes. Comments, CDATA sections, and
processing instructions are skipped whole; a DOCTYPE or other declaration up to its `>` outside quotes and its internal
subset; an end tag pops a namespace scope. For a start tag it reads the name (letters, digits, `_`, `.`, `-`, `:`) and
the attributes (a quoted value may hold `>`; an unquoted value ends at white space or `>`), binds the `xmlns:P`
prefixes of the element, and pushes them unless the tag closes itself. For an element named `image` or `P:image` the
link is the unprefixed `href`, else the first `P:href` whose prefix is bound to `http://www.w3.org/1999/xlink` on the
element or an ancestor; the five predefined entities and character references below 0x110000 are decoded and Lua white
space is trimmed.

**DependencyExists(path)** (`dependency_exists`): `Probe(path)` is `Taken`.

**Probe(path)**: `Taken`, `Missing`, or `Denied` (`Availability`). In the default mode, and for the source itself, a
file is `Taken` when it opens (`FilterPaths.FileExists`), else `Missing`; in a restrictive mode it is `Taken` when
`Resolve` gives it, `Denied` when the resolver refuses it with `AccessDenied` or `RealPathUnavailable` (or the policy
is `DenyAll`), decided before any byte of it is read, and `Missing` otherwise.

**ReadFile(path)**: every read of the walks. In the default mode, and for the source itself, the bytes as they are
(`FilterPaths.ReadFile`), exactly as before; in a restrictive mode the content `Resolve` gives, or `null`.

**Resolve(path, out denied)**: reads a referenced file as the tool reads it, through `_resolver`: a path as written
outside the roots is refused before anything is opened, and the policy is asked about the real path of the open handle
before any byte is read, so a link inside the roots that leads outside them, or (under `no-symlinks`) any link, is never
read; a policy that cannot be applied refuses (fail closed). A refusal (`FileReferenceException`) or a missing file is
`null`, which the walk treats as a file that is not there (an SVG drawing notes a denied model, chart, diagram, or
document as `missing`), and the tool denies the file when it draws, which stops a restrictive build. `DenyAll` gives
`null` without opening anything. An allowed file has the same bytes, so the parts of the key do not change.

#### Error Handling

Every error is a `ToolException` with `ErrorCodes.PandocFilterError` (`pandoc-filter-error`), raised by `Fail`; a limit
is never a silent cut-off. The messages, with `<N>` the count and `<name>` the file name:

- Model depth: `'<path>' is included more than 64 levels deep` (the full collapsed path).
- Model files: `the model and the files it includes are more than 512 files; the limit is 512, counting the model
  (every file must be hashed so that a changed one is drawn again)`.
- Nested charts and diagrams: `charts and diagrams are drawn inside each other more than 64 levels deep`, and
  `the chart or diagram and the files it draws are more than 512 files; the limit is 512 (every file must be hashed so
  that a changed one is drawn again)`.
- Chart JSON depth: `the chart specification is nested more than 64 levels deep; every data url must be hashed so that
  a changed data file is drawn again` (data URLs), or `...; every image url must be hashed so that a changed picture is
  drawn again` (image links).
- Chart counts: `'<name>' names <N> data files; the limit is 512 (every data file must be hashed so that a changed one
  is drawn again)` and `'<name>' names <N> pictures; the limit is 512 (every picture must be hashed so that a changed
  one is drawn again)`.
- Diagram: `the diagram and the files it draws are more than 512 files; the limit is 512 (every file must be hashed so
  that a changed one is drawn again)`, and the pictures message of a chart.
- Drawing: `SVG drawings are linked inside each other more than 64 levels deep`, `the drawing and the files it links are
  more than 512 files; the limit is 512 (every file must be hashed so that a changed one is drawn again)`, and
  `'<name>' links <N> files; the limit is 512 (every file must be hashed so that a changed one is drawn again)`.
- Document: `cannot read '<name>'` when a `pdf`, `pptx`, or `vsdx` picture cannot be read.

An unreadable model, chart, diagram, or drawing reached by a walk is skipped, and a chart that is not JSON adds only
itself: the tool reports these when it draws. The JSON reader has no depth limit of its own, so the walk's own 64-level
message applies.

#### Dependencies

- **FilterPaths** - path joining, collapsing, labels, local file resolution of data URLs and picture links,
  percent-decoding, byte-order sorting, Lua white space, and the file reads.
- **PictureAttributes** - the kind constants and `KindOf`, which tells a file only the tool draws.
- **FileAccessPolicy**, **FileReferenceResolver**, and **FileReferenceException** (core library) - the policy, the
  race-free read of a referenced file through it, and its refusal.
- **ScadDocument**, **ScadDirective**, and **ScadSyntaxException** (Scad library) - the directives of an OpenSCAD code
  block, found by the parser.
- **System.Text.Json** and **System.Text.RegularExpressions** (.NET) - the chart decoder (and the reader that finds the
  line of a name in a block) and the Lua patterns.
- **ToolException** (tool) and **ErrorCodes** (Viewer library) - the error and its code.

#### Callers

- **PandocFilterEngine** - `ForPicture` in `PlanDraw` and `ForSvg` in `PlanSvg`, with the policy of the picture's
  file-access arguments (`FilterSettings.PolicyOf`); the parts go to `PictureCache.ComputeKey`, and a plain drawing
  is left to Pandoc. `FindBlockReference` in `PlanBlock`, before the text of a code block is written.
