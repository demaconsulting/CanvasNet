# CanvasNet SVG Package

## API Reference

### SvgCodec

`SvgCodec` is distributed via the separate `DemaConsulting.CanvasNet.Svg` NuGet package (namespace
`DemaConsulting.CanvasNet.Svg`), which references the core `DemaConsulting.CanvasNet` package -
see the Installation section of the project README.

The `SvgCodec` static class decodes and rasterizes a common real-world subset of SVG documents
into a `Surface` of caller-chosen pixel dimensions. `SvgCodec` is decode-only: there is no `Save`.
It supports basic shapes (`rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`, `path`),
grouping (`g`) with cascading presentation attributes, `transform` functions, linear/radial
gradients (including `xlink:href`/`href` template inheritance), `use` references, `marker`
elements (referenced via `marker-start`/`marker-mid`/`marker-end`, with `markerWidth`/
`markerHeight`, `refX`/`refY`, `markerUnits`, `orient`, and an optional `viewBox`), `filter`
elements (referenced via the `filter` presentation attribute on any directly renderable shape or
`text` element, or on a `g`/`symbol` reference/`use` element - applied to the whole referenced
subtree as a single unit - with `x`/`y`/`width`/`height` filter-region attributes and `feBlend`/
`feColorMatrix`/`feComponentTransfer`/`feComposite`/`feConvolveMatrix`/`feDiffuseLighting`/
`feDisplacementMap`/`feDropShadow`/`feFlood`/`feGaussianBlur`/`feImage`/`feMerge`/`feMorphology`/
`feOffset`/`feSpecularLighting`/`feTile`/`feTurbulence` primitive children), `clipPath` elements
(referenced via the `clip-path` presentation attribute, hard-clipping to the union of the
`clipPath` element's own rect/circle/ellipse/polyline/polygon/path/text children, honoring
`clipPathUnits` and each child's own `clip-rule`), `mask` elements (referenced via the `mask`
presentation attribute, attenuating alpha by the referenced content's own computed luminance,
honoring `maskUnits`/`x`/`y`/`width`/`height` and `maskContentUnits` as independent attributes) -
`clip-path`/`mask` apply to the same per-shape/per-group targets `filter` does, and, when
combined with `filter` on the same element, are applied before that element's own filter chain
runs - `pattern` elements (referenced from a shape/text element's own `fill`/`stroke` as a tiled
paint server, honoring `patternUnits`/`patternContentUnits`, `patternTransform`, an optional
`viewBox`/`preserveAspectRatio`, and `href`/`xlink:href` tile-content inheritance) - `image`
elements (a base64-encoded `data:` URI in a supported raster format, decoded through that
format's own existing codec and drawn into the element's own `x`/`y`/`width`/`height` placement
rect) - `image-rendering` on an `image` (attribute, `style` declaration or stylesheet rule, inherited
from ancestors) selects the resampling filter: `pixelated`, `crisp-edges` and `optimizeSpeed` draw with
hard nearest-pixel edges, any other recognized value (for example `auto` or `optimizeQuality`) smooths,
and an unrecognized value is ignored so the inherited value applies - and best-effort,
weight/style-aware `text` rendering - against a caller-supplied dictionary of per-family font faces
(and, for a family you do not register, installed fonts and the bundled Liberation fonts - see
*SvgCodec Default Fonts* below):
either a single `TrueTypeFont` per family (the legacy shape), or a list of `SvgFontFace` values -
each pairing a `TrueTypeFont` with the `font-weight`/`font-style` it represents - letting a caller
register distinct bold/italic variants of a family and have `SvgCodec` pick the closest-matching
face for each `text` element's own cascaded `font-weight`/`font-style`. The root
`viewBox`/`width`/`height` are fit into the requested raster via the root `svg` element's own
`preserveAspectRatio` attribute (`[defer] <align> [<meetOrSlice>]` - all 10 aligns and both
`meet`/`slice`), defaulting to "xMidYMid meet" (equivalent to CSS `object-fit: contain`) when the
attribute is absent - the centered-letterbox default every document without an
explicit `preserveAspectRatio` attribute already rendered with. This same fit algorithm also
applies to a `use` element referencing a `symbol` element with its own `viewBox`, and to a
`marker` element's own explicit `preserveAspectRatio` attribute. Every shape/text geometry
attribute (`x`/`y`/`width`/`height`/`rx`/`ry`/`cx`/`cy`/`r`/`x1`/`y1`/`x2`/`y2`/`font-size`/
`stroke-width`/`stroke-dasharray`/`stroke-dashoffset`/`use`'s `x`/`y`/`width`/`height`/`text`'s
`x`/`y`) also accepts a trailing `%`, resolved against the current viewport (or, for `font-size`,
the parent element's own `font-size`) - `stroke-miterlimit` is the sole exception, since it is a
unitless ratio rather than a length. A `style` element's text content is parsed as CSS, matching
type/class/id/universal/compound selectors (including comma-separated lists and
descendant/child combinators) against elements, with standard CSS specificity/cascade-order
resolution and a 3-tier precedence (presentation attribute, then any matching stylesheet rule,
then an inline `style="..."` attribute, which always wins). Well-formed but
out-of-scope constructs (`animate`/SMIL,
`foreignObject`, nested `svg`, and any remaining unrecognized filter primitive type)
are silently skipped/passed through so the rest of the document still
renders; malformed/unparseable input throws `InvalidDataException`.

Rendering is bounded by fixed resource budgets so that hostile input cannot consume unbounded
memory or CPU: a cap on document size (5,000,000 characters), element nesting depth (100) and
total rendered elements (100,000); cumulative geometry and CSS-matching work budgets; and, for
filters, a cap of 1,000 primitives per filter, a per-filter work budget (primitive count times
region area) and a cumulative per-`Load`-call filter work budget. A filter whose region is too
large, whose chain exceeds either budget, or that has no primitives is skipped and the element
renders unfiltered rather than throwing; exceeding the document, depth or element caps throws
`InvalidDataException`. The deterministic effect counters used to verify these budgets (work
charged, charges rejected, effects surfaces and pixels allocated) are internal to the library and
are not part of the public API.

#### SvgCodec Methods

##### SvgCodec.LoadWithFontFaces(Stream, ..., IReadOnlyDictionary&lt;string, IReadOnlyList&lt;SvgFontFace&gt;&gt;?)

```csharp
public static Surface LoadWithFontFaces(
    Stream stream,
    int width,
    int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts)
```

Decodes and rasterizes an SVG document from an open, readable stream into a new `width` x
`height` `Surface`, matching each `text` element's cascaded `font-family`/`font-weight`/
`font-style` against `fonts` - a dictionary mapping family names to the list of `SvgFontFace`
instances registered for that family. When a family has more than one registered face, the face
whose `Weight`/`Style` most closely matches the element's own cascaded `font-weight`/`font-style`
is selected: an exact style match always beats a style mismatch; among faces tied on style, the
smallest weight distance wins; among faces tied on both, the face on the same "boldness side"
(`>= 400` or `< 400`) as the request wins. A `null` value, a dictionary with no entry matching a
given `text` element's `font-family`, or a matching entry whose face list is empty, makes that
element use the default font instead (see *SvgCodec Default Fonts* below) - text is never silently
dropped.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is not a valid `Surface` size (not
  pre-validated by `SvgCodec`; propagates from `new Surface(width, height)`).
- `InvalidDataException`: Thrown when the stream does not contain valid, supported SVG content.

##### SvgCodec.LoadWithFontFaces(string, ..., IReadOnlyDictionary&lt;string, IReadOnlyList&lt;SvgFontFace&gt;&gt;?)

```csharp
public static Surface LoadWithFontFaces(
    string path,
    int width,
    int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts)
```

Decodes and rasterizes an SVG file at the specified path, as
`LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>?)`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown for the same reason as the stream overload above.
- `InvalidDataException`: Thrown for the same conditions as the stream overload above.

##### SvgCodec.Load(..., fonts, SvgLoadOptions) and SvgCodec.LoadWithFontFaces(..., fonts, SvgLoadOptions)

```csharp
public static Surface Load(Stream stream, int width, int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts, SvgLoadOptions options)
public static Surface Load(string path, int width, int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts, SvgLoadOptions options)
public static Surface LoadWithFontFaces(Stream stream, int width, int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts, SvgLoadOptions options)
public static Surface LoadWithFontFaces(string path, int width, int height,
    IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts, SvgLoadOptions options)
```

The same as the overloads without `options`, with explicit load options. `SvgLoadOptions` has one
property, `UseSystemFonts` (default `true`); `new SvgLoadOptions()` behaves exactly like omitting the
argument. `options` must not be null (`ArgumentNullException`); the other exceptions are as for the
overloads above.

##### SvgCodec Default Fonts

A `text` element is drawn even when you register no font. For each `text` element the font is
chosen in this order:

1. **A font you registered** for a family in the element's `font-family` list (case-insensitive;
   the whole list is checked against your dictionary first, so a registered family anywhere in the
   list wins over a generic keyword).
2. **Otherwise, each family of the `font-family` list, in order:** a generic keyword is drawn with
   a bundled Liberation font - `sans-serif`, `system-ui`, `ui-sans-serif`, `cursive`, and
   `fantasy` with Liberation Sans; `serif` and `ui-serif` with Liberation Serif; `monospace` and
   `ui-monospace` with Liberation Mono - and any other name is looked up among the fonts installed
   on the machine, matching the family name exactly (ignoring case).
3. **Otherwise,** Liberation Sans.

Bold is chosen when the cascaded `font-weight` is 600 or more, and italic when `font-style` is
`italic` or `oblique`; the installed or bundled variant that matches is used. Installed fonts differ
between machines, so the same SVG can look different on different hosts. For output that is
identical everywhere, pass `new SvgLoadOptions { UseSystemFonts = false }`: installed fonts are then
never consulted and every text you did not register a font for uses the bundled Liberation fonts.

```csharp
// Reproducible: text is drawn with the bundled Liberation fonts on every machine
var surface = SvgCodec.Load("diagram.svg", 800, 600, fonts: null,
    new SvgLoadOptions { UseSystemFonts = false });
```

##### SvgCodec.Load(Stream stream, int width, int height, IReadOnlyDictionary&lt;string, TrueTypeFont&gt;? fonts = null)

```csharp
public static Surface Load(
    Stream stream,
    int width,
    int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
```

Decodes and rasterizes an SVG document from an open, readable stream into a new `width` x
`height` `Surface`, using at most one `TrueTypeFont` per font-family. A thin wrapper over the
richer `SvgFontFace`-list overload above: each registered font is wrapped as a single
normal-weight (`400`)/normal-style face, so every `text` element always resolves to that single
registered font, regardless of its own `font-weight`/`font-style` - exactly as before the richer
overload existed. If `fonts` is supplied, `text` elements are rendered using the matching
`TrueTypeFont` keyed by family name; unmatched or missing fonts make that `text` element use the
default font (see *SvgCodec Default Fonts* above) rather than being skipped or throwing. Callers
registering more than one face per family (bold/
italic variants) should call the richer overload directly instead. Because the richer overload is
named `LoadWithFontFaces` rather than sharing the `Load` name, there is no ambiguity between the
two: `Load(stream, width, height, null)` always resolves to this legacy single-font-per-family
overload with no fonts registered, and `LoadWithFontFaces(stream, width, height, null)` always
resolves to the richer per-face overload with no fonts registered - each call is unambiguous
regardless of which overload the caller intends.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `ArgumentOutOfRangeException`: Thrown when `width` or `height` is not a valid `Surface` size (not
  pre-validated by `SvgCodec`; propagates from `new Surface(width, height)`).
- `InvalidDataException`: Thrown when the stream does not contain valid, supported SVG content.

##### SvgCodec.Load(string path, int width, int height, IReadOnlyDictionary&lt;string, TrueTypeFont&gt;? fonts = null)

```csharp
public static Surface Load(
    string path,
    int width,
    int height,
    IReadOnlyDictionary<string, TrueTypeFont>? fonts = null)
```

Decodes and rasterizes an SVG file at the specified path, as `Load(Stream, int, int, ...)`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `ArgumentOutOfRangeException`: Thrown for the same reason as `Load(Stream, int, int, ...)`.
- `InvalidDataException`: Thrown for the same conditions as `Load(Stream, int, int, ...)`.

##### SvgCodec.GetInfo(Stream stream)

```csharp
public static ImageInfo GetInfo(Stream stream)
```

Parses only the SVG document's root `svg` start-tag and its own attributes (never reading into
the document body) and returns an `ImageInfo` describing its intrinsic size, without rasterizing
pixel data. Uses `viewBox` when present; otherwise falls back to `width`/`height` attributes;
otherwise falls back to the CSS/UA default replaced-element intrinsic size of 300x150. `Channels`
is always 4 and `HasAlpha` is always true.

**Exceptions:**

- `ArgumentNullException`: Thrown when `stream` is null.
- `InvalidDataException`: Thrown when the stream is not well-formed XML up to and including the
  root start-tag, its character count exceeds the document character cap, its root element is not
  named `svg`, or its `viewBox` attribute is present but malformed (not exactly four numbers, or
  non-positive width/height). A malformed or unparseable `width`/`height` attribute is **not**
  included in this list: such a value is treated as absent and falls back to the next sizing tier
  (ultimately the 300x150 default size) rather than throwing.

##### SvgCodec.GetInfo(string path)

```csharp
public static ImageInfo GetInfo(string path)
```

Parses an SVG file at the specified path and returns an `ImageInfo`.

**Exceptions:**

- `ArgumentNullException`: Thrown when `path` is null.
- `ArgumentException`: Thrown when `path` is an empty string.
- `InvalidDataException`: Thrown for the same conditions as `GetInfo(Stream)`.

When rendering a `.pptx` presentation via the separate `DemaConsulting.CanvasNet.Pptx` package's
`PptxDocument.Render`, a `<p:pic>` picture blip that declares only a Microsoft SVG extension
fallback (no raster `r:embed`/`r:link`, only `<a:extLst>/<a:ext>` wrapping an
`<asvg:svgBlip r:embed="...">`) is automatically resolved and rasterized via `SvgCodec` at the
referenced SVG's own intrinsic size - no extra caller code is required beyond calling
`PptxDocument.Render` itself.

## Examples

### Example 14: Decoding and Rasterizing an SVG Document

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Svg;
using System.IO;
using System.Text;

const string svg = """
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
      <rect x="10" y="10" width="80" height="80" fill="#0080ff"/>
    </svg>
    """;

// Probe the intrinsic size from the viewBox before rasterizing.
using var probeStream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
var info = SvgCodec.GetInfo(probeStream);
Console.WriteLine($"{info.Width}x{info.Height}"); // Output: 100x100

// Rasterize the document into a caller-chosen 64x64 surface (SvgCodec is decode-only).
using var loadStream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
using var surfaceSvg = SvgCodec.Load(loadStream, 64, 64);
Console.WriteLine(surfaceSvg[32, 32].A); // Output: 255 (well inside the filled rectangle)
```
