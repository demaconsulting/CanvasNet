# CanvasNet PDF Package

## API Reference

### PdfDocument

`PdfDocument` is distributed via the separate `DemaConsulting.CanvasNet.Pdf` NuGet package
(namespace `DemaConsulting.CanvasNet.Pdf`), which references the core `DemaConsulting.CanvasNet`
package - see the Installation section of the project README.

The `PdfDocument` sealed class opens a PDF document, parses its cross-reference table/stream and
page tree, and reports each page's displayed (rotation-adjusted) size and its page count.
`Render` interprets a page's content stream, painting real path geometry, device color (`rg`/
`g`/`k`/`cs`/`sc` and related operators, including `/Pattern`-color-space shading and tiling
pattern fills/strokes - axial/radial/mesh (`/ShadingType 2`-`7`) shading patterns driven by
`/FunctionType 0` (including multi-input), `2`, `3` and `4` (PostScript calculator) functions, and
colored/uncolored (`/PaintType 1`/`2`) tiling patterns
rendering a repeating tile), placed image XObjects (`Do`), and text shown with a simple TrueType
font (`Tf`/`Td`/`Tj` and the other `BT`/`ET` text operators) onto the returned `Surface`. A font
with an embedded `/FontDescriptor/FontFile2` stream is always used directly;
a font with no embedded font data is instead automatically substituted, fully silently (no new
API, no "fallback occurred" indicator): first with the closest-matching font actually installed
on the host operating system (matched by family name and bold/italic/serif/fixed-pitch style),
and, when nothing on the host machine matches, with a bundled Liberation Sans/Serif/Mono font
that ships with the `DemaConsulting.CanvasNet` package and is therefore always available. A
recognized Standard-14 name (`Helvetica`, `Times-Roman`, `Courier`, and their bold/italic
variants, and so on) is classified by a fixed, built-in table; any other non-embedded font's
style is derived from its `/FontDescriptor` flags/weight/angle. `Symbol` and `ZapfDingbats`
(matched by exact `/BaseFont` name) instead resolve via a dedicated, bundled Noto substitute font:
`Symbol` tries `NotoSans-Regular.ttf` (Greek letters and general symbols), then
`NotoSansMath-Regular.ttf` (mathematical operators), then `NotoSansSymbols2-Regular.ttf`
(Private-Use-Area-adjacent/rare symbols), in that priority order; `ZapfDingbats` uses
`NotoSansSymbols2-Regular.ttf` alone - using the Symbol/ZapfDingbats built-in encoding (PDF
32000-1 Appendix D) rather than treating them like any other Latin-text font. This is a
documented, accepted fidelity limitation, not full glyph coverage: of Symbol's 163 distinct
mapped codepoints, 161 are covered (only U+2329/U+232A are not); of ZapfDingbats' 202 distinct
mapped codepoints, 158 are covered (the circled-digit Dingbats and a handful of others are not).
Any other font whose `/FontDescriptor` marks it as a symbolic, non-Latin-text glyph set is
never substituted this way, since a symbol/dingbat glyph set with no bundled substitute has no
meaningful generic-family
equivalent - it still fails closed with `UnsupportedImageFeatureException`, exactly as a
non-embedded font of any kind did before this fallback behavior existed. A shading pattern's
`/Extend` is honored per end (PDF 32000-1 §8.7.4.5.2): an extended end repeats its end color
beyond the defining geometry, while a non-extended end (the default) paints nothing past it;
stitching-function boundaries render as sharp transitions, and a mesh shading's `/Function` is
applied to the interpolated parametric value (through a 4096-entry color table whose entries are
each converted from the function output at their exact parameter, so nonlinear color spaces are
converted after evaluation). A function-less mesh in `/CalGray`, `/CalRGB` or `/Lab` interpolates the
vertex components in that space and converts per pixel; other mesh color spaces (including
`/DeviceCMYK`, `/Separation`, `/DeviceN`) convert each vertex first and blend the RGB result, an
approximation. Axial and radial shadings convert up to 256 samples exactly and blend only between
adjacent samples (within about 3 color levels of direct conversion for `/Lab`). A non-integral
`/N` of an `/ICCBased` space or `/Hival` of an `/Indexed` space is rejected as malformed. The
`sh` operator and generic path
clipping (`W`/`W*`) are supported. A clip made only of axis-aligned rectangles (including the union
of several) is applied geometrically, so a fill or image whose edge coincides with a clip edge keeps
its full ink; any other clip is applied as an antialiased coverage mask. Placed images are resampled
once to the device window they cover (area averaging when shrinking, bicubic when enlarging), so thin
features such as one-pixel rules are attenuated rather than dropped. Device, `/CalGray`, `/CalRGB`, `/Lab`, `/ICCBased`,
`/Indexed`, `/Separation`, `/DeviceN` and `/Pattern` color spaces are supported; `/CalGray`, `/CalRGB` and `/Lab`
color spaces are converted to
sRGB through a CIE XYZ pipeline. `/ICCBased` spaces are resolved by their `/N` component
count (or a matching `/Alternate`); the ICC profile data itself is not applied. An image in a `/Lab`
color space with an explicit `/Decode` array fails closed (feature `pdf-image-lab-decode`).
`/Separation` and `/DeviceN` colors (operators, images, `/Indexed` palettes and shadings) are
evaluated through their tint-transform function (types 0 including multi-input, 2, 3 and 4
PostScript calculator, under hard resource limits) and converted via the alternate space;
`/None` colorants paint nothing, and an alternate that is itself Pattern/Indexed/Separation/DeviceN
fails closed.
A multi-component `/DeviceN` image has a bounded per-image tint-evaluation budget; an image whose
distinct pixels would exceed it fails closed (feature `pdf-image-tint-budget`). ICC profile data is
never applied - only `/N`/`/Alternate` are used - and this is silent (no exception is thrown).
**Documented scope boundaries**: `/MMType1` fonts, function-based shadings (`/ShadingType 1`),
transparency groups,
and Type 3 glyphs shown under a clip text-rendering mode (`Tr 4`-`7`; clip modes with outline-based
fonts are supported) all fail closed with `UnsupportedImageFeatureException`
rather than being silently skipped or mis-rendered.

```csharp
using var doc = PdfDocument.Open("file.pdf");
var info = doc.GetPageInfo(0);
using var surface = doc.Render(0, info.Width, info.Height);
```

#### PdfDocument Methods

##### PdfDocument.Open(Stream stream, string? password = null)

```csharp
public static PdfDocument Open(Stream stream, string? password = null)
```

Reads the entirety of an open, readable stream into an in-memory buffer and parses it into a new
`PdfDocument`. Does not take ownership of, and does not dispose, the caller's `stream`.

The optional `password` parameter defaults to `null`, which preserves the library's original
empty-user-password-only behavior byte-for-byte. When a non-`null` password is supplied, it is
tried first as the **user password**, then - if that does not authenticate - as the **owner
password** (ISO 32000-1 Algorithm 3 for RC4/AES-128 documents; the owner-password variant of
ISO 32000-2 Algorithm 2.A for AES-256/R5/R6 documents). R2-R4 (RC4/AES-128) passwords are encoded as
Latin-1 (the ASCII range of PDFDocEncoding); R5/R6 (AES-256) passwords are encoded as UTF-8 with no
SASLprep/Unicode normalization applied - both are intentional scope boundaries. Both encodings
truncate the password's encoded bytes to a maximum of 127 bytes before any hashing.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream does not contain valid, supported PDF content
  (including when normal cross-reference parsing fails and the linear-scan fallback also cannot
  resolve the document catalog).
- `UnsupportedImageFeatureException`: Thrown when the document's trailer declares an `/Encrypt`
  entry that this library cannot open: a security handler other than the PDF "Standard" handler,
  an AES-256 document using an `/R` other than 5 or 6, a crypt filter other than
  the standard `/StdCF` filter (RC4, AES-128/`AESV2`, or AES-256-R5/R6/`AESV3`), a document that
  requires a password but `password` was not supplied (`null`), or a supplied `password` that
  does not authenticate as either the user or the owner password (feature
  `pdf-encrypted-incorrect-password`), or - for an R2-R4 document - contains a character outside
  ASCII 0-127 (feature `pdf-encrypted-password-non-ascii`). A document encrypted with the Standard
  security handler using RC4 (40-128 bit), AES-128, or AES-256 (R5/R6) and an empty user password -
  the vast majority of "owner password"/permission-restricted real-world PDFs - opens and renders
  normally with `password` left at its default `null`; its permission flags are not enforced (this
  library only ever reads for rendering, so copy/print restrictions do not apply).

##### PdfDocument.Open(string path, string? password = null)

```csharp
public static PdfDocument Open(string path, string? password = null)
```

Opens its own internal `FileStream` for the file at `path`, reads it fully, closes that stream
synchronously within `Open` (mirroring `PngCodec.Load(string)`'s open/consume/close pattern), then
parses the buffered content identically to the `Stream` overload above, with the same `password`
parameter and semantics.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty or whitespace-only string.
- `InvalidDataException`: Thrown for the same conditions as `Open(Stream, string?)`.
- `UnsupportedImageFeatureException`: Thrown for the same reasons as `Open(Stream, string?)`.

##### PdfDocument.PageCount

```csharp
public int PageCount { get; }
```

The total number of pages in the document's page tree.

**Exceptions:**

- `ObjectDisposedException`: Thrown when accessed after `Dispose()` has been called.

##### PdfDocument.GetPageInfo(int pageIndex)

```csharp
public PdfPageInfo GetPageInfo(int pageIndex)
```

Returns the specified page's displayed (rotation-adjusted) width/height, plus its normalized
clockwise rotation (0/90/180/270). Width and height are swapped relative to the page's raw
`/MediaBox` when rotation is 90 or 270. A leaf page inherits `/MediaBox`/`/Rotate` from its
page-tree ancestors when it does not declare its own.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is negative or `>= PageCount`.
- `ObjectDisposedException`: Thrown when called after `Dispose()` has been called.

##### PdfDocument.Render(int pageIndex, int width, int height, PdfRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, int width, int height, PdfRenderOptions? options = null)
```

Returns a new `Surface` of the caller-specified `width` x `height` for the given page, cleared to
`options.BackgroundColor` (opaque white by default, when `options` is `null`) and then painted
with the page's interpreted content-stream geometry (path construction/painting with real device
color, placed image XObjects, and text shown with a resolved TrueType font - embedded when
present, otherwise automatically substituted, see *PdfDocument* above). A page with no
`/Contents` simply returns the cleared, unpainted background surface. The `width`/`height` used
is exactly as given - it is not clamped or derived from the page's own `/MediaBox` size. To
reproduce the pre-`PdfRenderOptions` fully transparent background, pass
`new PdfRenderOptions { BackgroundColor = new(0, 0, 0, 0) }`.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is negative or `>= PageCount`, or when
  `width`/`height` is not a valid `Surface` size (propagates from `new Surface(width, height)`).
- `InvalidDataException`: Thrown for malformed `/Contents`, a malformed recognized operator's
  operand count/type, an unresolvable font resource name, or a text-showing operator invoked with
  no font selected.
- `UnsupportedImageFeatureException`: Thrown for a well-formed but unsupported color space,
  stream filter, font subtype (`/MMType1`; `/TrueType`, `/Type0`, `/Type1`, and `/Type3` are all
  supported), an otherwise-symbolic font (other than `Symbol`/`ZapfDingbats`, which resolve via a
  bundled Noto substitute instead) with no embedded font data, font encoding, or
  Type 3 glyph under a clip text-rendering mode (`pdf-text-render-mode-type3-clip`).
- `ObjectDisposedException`: Thrown when called after `Dispose()` has been called.

##### PdfDocument.Render(int pageIndex, float dpi, PdfRenderOptions? options = null)

```csharp
public Surface Render(int pageIndex, float dpi, PdfRenderOptions? options = null)
```

Convenience overload of `Render(int, int, int, PdfRenderOptions?)` for the common "render at a
given resolution" case: reads `GetPageInfo(pageIndex)`'s rotation-adjusted width/height (in
points, 1/72 inch), scales both by `dpi / 72`, rounds to the nearest pixel, and renders at that
size - preserving the page's own aspect ratio, unlike the four-argument overload, and forwarding
`options` unchanged. Use `Render(int, int, int, PdfRenderOptions?)` directly instead when
independent X/Y scaling (non-square pixels, or an exact pixel size regardless of aspect ratio) is
needed.

**Exceptions:**

- `ArgumentOutOfRangeException`: Thrown when `pageIndex` is negative or `>= PageCount`, when
  `dpi` is not a positive, finite number, or when the computed pixel width/height is not a valid
  `Surface` size.
- Also throws every exception `Render(int, int, int, PdfRenderOptions?)` itself can throw.

##### PdfDocument.Dispose()

```csharp
public void Dispose()
```

Releases the document's in-memory buffer and parsed state. Idempotent - safe to call more than
once. Every other public member throws `ObjectDisposedException` once called.

## Examples

### Example 19: Rendering a PDF Page

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pdf;

using var pdf = PdfDocument.Open("document.pdf");
Console.WriteLine(pdf.PageCount);

// Render the first page at 150 DPI onto a transparent background.
var options = new PdfRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };
using var page = pdf.Render(0, 150f, options);
PngCodec.Save(page, "page1.png");
```

### Example 42: Drawing a PDF Page in a Pandoc Document

The Pandoc filter `docs/filters/canvasnet.lua` draws one page of a `.pdf` file; `page` counts from 1 and `dpi`
sets the resolution. The page below is the original `docs/images/showcase/mesh-shading.pdf` of this repository,
a copy of a hand-authored test fixture.

```markdown
![A PDF page with mesh shading](mesh-shading.pdf){page=1 dpi=96 width=40%}
```

![A PDF page with mesh shading](mesh-shading.pdf){page=1 dpi=96 width=40%}
