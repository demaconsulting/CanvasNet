### SvgFlattener

![Imaging Structure](ImagingView.svg)

#### Purpose

`SvgFlattener` is the flatten operation of the library: it writes a copy of an SVG drawing in which every `image` link
that only CanvasNet can draw (a Mermaid diagram, an OpenSCAD model, a Vega-Lite chart, a page of a PDF, PPTX, or VSDX
document, with or without a fragment, and a nested SVG file that links one at any depth) is a PNG picture of its
placement size, as a `data:image/png;base64,...` URI, so that any SVG reader (a browser, WeasyPrint) shows the drawing as
`canvasnet render` draws it. Its single responsibility is to decide which links to replace, at what size, and to change
exactly their attribute values; the drawing of the pictures is the resolver of the render operation
(`SourceLoader.SvgImages` over `CanvasNetViewer.CreateImageResolver`), the placement sizes are those the SVG library
computes, and the publishing is `ImageWriter`. It is the library form of the `flatten` command of the `canvasnet` tool.
The unit is the static partial class `SvgFlattener` in `SvgFlattener.cs` (the records and the orchestration),
`SvgFlattener.Links.cs` (the link scanner, the rewrite, and the rebasing), and `SvgFlattener.Recorder.cs` (the
recording resolver).

#### Data Model

**FlattenRequest** (record): the required `Output` (positional) and the `init` properties `Scale` (1; 0.001 to 8192),
`Strict` (off), `NoClobber` (off), and `MaxOutputBytes` (`DefaultMaxOutputBytes`, 64 MiB).

**FlattenedImage** (record): `Href` (as the SVG library reads it), `Format` (the format name of the linked file, `svg`
for a nested drawing), `Width` and `Height` of the PNG picture, `Elements` (the `image` elements that use the link), and
`Bytes` (the size of the PNG). **RebasedLink** (record): `Href` and `NewHref`. **FlattenResult** (record): `Info`,
`Output` (`null` when nothing was written), `Bytes`, `Replaced`, `Rebased`, `Diagnostics`, `Echo`, and the derived
`Flattened` (at least one link replaced).

**ImageLink** (internal record): the start and length of the raw attribute value in the text, its quote, the value as the
SVG library reads it (entities decoded, trimmed), and the line and column of the attribute. **Capture** (internal
record): the format, size, PNG size, and data URI of a kept picture.

#### Key Methods

**Execute(info, request)**: refuses a null argument or `Output` and a `MaxOutputBytes` that is not positive as defects of
the caller, a scale out of range with `usage-error` (the validator of `SourceLoader`), an input that is not SVG with
`unsupported-format`, and an unusable output (`ImageWriter.CheckTarget`) before anything is read. It then:

1. reads the file (at most 20,000,000 bytes, four for each of the 5,000,000 characters the SVG library reads) and decodes
   it as strict UTF-8, remembering a byte order mark; invalid UTF-8 or a null character is `corrupt-input`, and a
   declared encoding other than UTF-8 or US-ASCII is `unsupported-feature` unless every byte is ASCII, so that the text
   is exactly what the XML reader sees;
2. draws the drawing once with the SVG library on a canvas of its natural size times the scale (shrunk evenly to at most
   2048 by 2048 pixels, the placement sizes enlarged by the same factor), with the **Recorder** as the image resolver;
3. applies the strict policy of render (`SvgImages.ThrowForUnresolved`);
4. finds the link attributes (**FindImageLinks**), and reports each link to a file only CanvasNet draws that the drawing
   never asked for as the warning `svg-flatten-image-not-drawn` with its line and column;
5. rewrites the text (**Rewrite**) and, when at least one link was replaced, encodes it (with the byte order mark of the
   input), checks it against `MaxOutputBytes` (`size-limit`), writes it to a temporary file next to the output, and
   publishes it with `ImageWriter.PublishAll`. Nothing is written when nothing was replaced.

**Recorder.Resolve(href, width, height)**: the SVG library calls it for every link it shows (never for a `data:` URI or
a `#` link), with the device size of the placement through the `viewBox` and every transform (the edge lengths of a
rotated placement). It records the link; a link whose kind (`SourceLoader.SvgImages.KindOfName`, the rule of the render
error mapping) is a raster or unknown is not drawn. A link of a CanvasNet-only kind, or an SVG file, is drawn by the
resolver of render at the placement size times the enlargement; an SVG file is kept only when the pictures the resolver
drew for it (`FolderImageResolver.Embedded`, nested before outer) include one of another format than a raster or SVG at
depth 1 or more. A kept picture is encoded as PNG; a link already kept at least as large, or an SVG file known to be
plain, is not drawn again, so a link shown twice is kept at its largest placement. The sum of the data URIs is checked
against `MaxOutputBytes` as they are kept. It always returns `null`: the drawing itself is not needed.

**FindImageLinks(bytes, text)**: reads the bytes with the hardened XML reader of the SVG library (`SafeXml.CreateReader`,
no DTD, at most 5,000,000 characters) and, for each element whose local name is `image`, takes the unprefixed `href`,
or the `xlink:href` when there is none, as the SVG library does. The reader reports the line and the column of the
attribute name; the value is found from there in the raw text (a table of line starts that counts `\r\n`, `\r`, and
`\n` as one break each, then white space, `=`, white space, and the quote), so a comment, character data, or the text of
another attribute is never taken for a link. A position that does not match the text is a defect
(`InvalidOperationException`).

**Rewrite**: copies the text and replaces, link by link in document order, the raw value of a kept link by its data URI
and, when the output folder is not the folder of the input, the raw value of a relative link that is left by
**RebaseLink**; the size of the copy is checked as it grows. **RebaseLink(href, quote, inputFolder, outputFolder)**
leaves a link with a URI scheme, a `#` link, an empty link, and a rooted path alone (and a path with a null character, or
one the file system cannot resolve, such as one that is too long, since it names no file); otherwise it resolves the
path (cut at the first `?` or `#` and percent-decoded, as the resolver reads it) against the input folder and writes it
relative to the output folder with each segment percent-encoded (a `file:` URI when there is no relative path), followed
by the query and the fragment escaped for the attribute.

#### Error Handling

`ViewerException` with `usage-error`, `unsupported-format`, `corrupt-input`, `unsupported-feature`, `size-limit`,
`same-input-output`, `output-exists`, `output-write-failed`, and the codes of render for a linked file that is found but
broken (`SvgImages.Map`, naming both files). `ArgumentNullException` and `ArgumentOutOfRangeException` for the defects of
the caller. A link that cannot be drawn is a note of the resolver (`svg-image-unresolved`, `svg-image-too-large`, the
fragment notes), an error only with `Strict`. Nothing is written on any failure; the staged file is deleted.

#### Dependencies

- **SourceLoader** - `SvgImages` (the resolver of render, its notes, the strict policy, the error mapping, and
  `KindOfName`), `Read` (the mapping of library exceptions), and `FindOptionProblem` (the scale).
- **ImageWriter** - `CheckTarget`, `StagedOutput`, and `PublishAll`.
- **CanvasNetSvg** - `SvgCodec.LoadWithFontFaces` with `SvgLoadOptions.ImageResolver`.
- **CanvasNet core library** - `FolderImageResolver`, `IImageResolver`, `SafeXml`, `PngCodec`, and `Surface`.
- **ViewerException** and **ErrorCodes** - the expected-error contract.

#### Callers

- **FlattenCommand** (of the tool) - for `flatten`.
- **Library users** - make a drawing that embeds models, charts, diagrams, or document pages readable by other SVG
  readers.
