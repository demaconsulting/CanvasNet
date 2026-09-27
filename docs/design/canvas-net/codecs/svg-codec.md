## SvgCodec

![Codecs Structure](CodecsView.svg)

<!-- cspell:ignore rasterizing rrggbb sizeless SMIL unparseable Linq uncatchable Glyf Loca renderable -->
<!-- cspell:ignore unitless letterboxing -->

The `SvgCodec` class is the fifth software unit in the `Codecs` subsystem, and the first codec
unit whose dependencies extend beyond `Canvas.Surface`. It provides hand-rolled, decode/
rasterize-only support for a common real-world subset of SVG (Scalable Vector Graphics)
documents, rendering them directly into a `Surface` pixel buffer. Unlike `BmpCodec`, `PngCodec`,
`TiffCodec`, and `JpegCodec`, `SvgCodec` has no `Save` method: an SVG document is XML markup, not
pixel data, so there is no meaningful inverse operation that would turn an arbitrary `Surface`
back into equivalent vector markup.

### Purpose

`SvgCodec` lets callers rasterize an SVG document (from a stream or a file path) into a `Surface`
of a caller-requested pixel size, and lets callers inspect a document's intrinsic size and
channel layout via `GetInfo` without rasterizing it. It is implemented against `System.Xml.Linq`
(`XDocument`/`XElement`) for XML parsing — an approved, already-referenced base class library
dependency — with no new third-party SVG or XML package. `SvgCodec` is a `static` class, for the
same reason as the other codecs: rasterizing an SVG document has no instance state to carry.

#### In-scope subset

- Root `svg` sizing: `viewBox`/`width`/`height` resolve the document's intrinsic user-space
  origin/size, fit into the caller-requested raster via an optional `preserveAspectRatio`
  (`[defer] <align> [<meetOrSlice>]` - all 10 aligns and both `meet`/`slice`; `defer` is parsed
  and ignored, since this codec has no `<image>` element to defer to) - see _ViewBox Fitting
  Policy_ below
- Shapes: `rect` (including `rx`/`ry` rounded corners), `circle`, `ellipse`, `line`, `polyline`,
  `polygon`, and `path` (the full `d` mini-language: `M`/`m`, `L`/`l`, `H`/`h`, `V`/`v`, `C`/`c`,
  `S`/`s`, `Q`/`q`, `T`/`t`, `A`/`a`, `Z`/`z`, in both absolute and relative forms)
- Grouping: `g`, with presentation-attribute inheritance (parent cascades to child, a child may
  override) and nested `transform` composition
- Transforms: `translate`, `scale`, `rotate`, `skewX`, `skewY`, `matrix`, and a
  space/comma-separated list of these, composed so the rightmost-listed function applies to a
  point first
- Presentation attributes: `fill` (keyword, `#rrggbb`/`#rgb`, `rgb(...)`, `none`, or
  `url(#id)`), `fill-opacity`, `fill-rule` (`nonzero`/`evenodd`), `stroke` (same color forms),
  `stroke-width`, `stroke-opacity`, `stroke-linecap`, `stroke-linejoin`, `stroke-miterlimit`,
  `stroke-dasharray`, `stroke-dashoffset`, and `opacity`
- Gradients: `linearGradient`/`radialGradient` under `defs`, with `stop` children,
  `gradientUnits` (`objectBoundingBox`/`userSpaceOnUse`), `gradientTransform`, `spreadMethod`
  (`pad`/`reflect`/`repeat`), and a bounded, cycle-checked `href`/`xlink:href` template
  inheritance chain for color stops
- `use`, referencing any element by id via `href`/`xlink:href`, with `x`/`y` translation (each
  accepting a trailing `%`, resolved against the current viewport - see _Percentage-Based
  Geometry Resolution_ below), and, when the referenced element is a `symbol` with its own
  `viewBox`, a `preserveAspectRatio`-driven fit of that `viewBox` into the `use`/`symbol`
  element's resolved `width`/`height` (see _ViewBox Fitting Policy_ below)
- `marker`, referenced from `line`/`polyline`/`polygon`/`path` via the `marker-start`/
  `marker-mid`/`marker-end` presentation attributes (`url(#id)`), with `markerWidth`/
  `markerHeight`, `refX`/`refY`, `markerUnits` (`strokeWidth`/`userSpaceOnUse`), `orient`
  (`auto`/`auto-start-reverse`/a fixed angle in degrees), an optional `viewBox`, and an optional
  explicit `preserveAspectRatio` (see _ViewBox Fitting Policy_ below)
- `filter`, referenced from any renderable shape or `text` element, or from a `g`/`symbol`
  reference/`use` element (applied to the whole referenced subtree as a single unit), via the
  `filter` presentation attribute (`url(#id)`), with `x`/`y`/`width`/`height` filter-region
  attributes (objectBoundingBox units) and `feFlood`, `feGaussianBlur`, `feOffset`,
  `feComposite`, and `feMerge` primitive children
- `clipPath`, referenced from any renderable shape/text/`g`/`symbol`/`use` element via the
  `clip-path` presentation attribute (`url(#id)`), hard-clipping that element's own content to
  the union of the `clipPath` element's own direct `rect`/`circle`/`ellipse`/`polyline`/
  `polygon`/`path`/`text` children, honoring `clipPathUnits`
  (`userSpaceOnUse`/`objectBoundingBox`) and each child's own `clip-rule` - see _Clipping and
  Masking_ below
- `mask`, referenced from any renderable shape/text/`g`/`symbol`/`use` element via the `mask`
  presentation attribute (`url(#id)`), attenuating that element's own alpha by the referenced
  `mask` element's own rendered content, evaluated as a luminance mask (the SVG-specification
  default), honoring `maskUnits`/`x`/`y`/`width`/`height` (the mask's own region box) and
  `maskContentUnits` (the mask content's own coordinate system) as independent attributes - see
  _Clipping and Masking_ below
- `pattern`, referenced from any renderable shape/text element's own `fill`/`stroke`
  presentation attribute (`url(#id)`) as a tiled paint server, honoring `patternUnits`/
  `patternContentUnits` (`objectBoundingBox`/`userSpaceOnUse`, resolved as two independent
  attributes exactly as `clipPathUnits`/`maskContentUnits` already are), `patternTransform`, an
  optional `viewBox`/`preserveAspectRatio` pair, and a bounded, cycle-checked `href`/
  `xlink:href` template-inheritance chain for tile content - see _Pattern Paint-Server_ below
- `text`, with `x`/`y`, `font-family`, `font-size`, `fill`, `text-anchor`
  (`start`/`middle`/`end`), and `font-weight`/`font-style`, rendered through a caller-supplied
  dictionary of per-family `SvgFontFace` lists (or, via the legacy single-font-per-family
  overload, a plain `TrueTypeFont` dictionary)

#### Out-of-scope subset (tolerated, silently skipped)

`style`, a nested `svg`, `animate`/other SMIL
animation elements, `image`, `foreignObject`, and CSS class/id selectors are all well-formed SVG
constructs this codec does not implement. Encountering one of these does not fail the whole
document: `SvgCodec` silently skips just that element (and, for a container element, everything
nested inside it) and continues walking the rest of the tree. This is a deliberate,
tolerant-parsing policy distinct from the codec's malformed-input rejection policy (see
_Error Handling_ below) — a document using an out-of-scope construct is not itself invalid SVG,
only partially outside this codec's supported feature set. Within the supported `marker` feature
itself, `markerContentUnits` (a rarely-used SVG 2 attribute) and clipping marker content to its
own `markerWidth`/`markerHeight` viewport (`overflow`) are both explicitly out of scope. Within
the supported `filter` feature itself, `filterUnits="userSpaceOnUse"` (tolerantly falls back to
the same objectBoundingBox-relative region computation as the default, rather than being
interpreted as literal absolute user-space coordinates), a `filter` on a shape's own `marker`
content (has no effect - `marker` content is never recursed into by the ordinary element walk, so
a group-level filter on a `<g>` inside a `marker` is never reached), and every filter primitive
other than
the five listed above (`feColorMatrix`, `feTurbulence`, `feDisplacementMap`, `feImage`, `feTile`,
`feDropShadow`, `feConvolveMatrix`, `feDiffuseLighting`, `feSpecularLighting`,
`feComponentTransfer`, and `feMorphology` - each tolerated as a no-op passthrough of its own input
rather than rejected or skipped at the whole-filter level) are all explicitly out of scope.
`feImage` in particular is deliberately never implemented, specifically because it is the only
primitive that could reference another filtered element's own output - omitting it means
filter-chain evaluation needs no additional recursion-depth guard of its own, unlike `use`/
`marker` references. Within the supported `text` feature itself, the `font-weight` relative
keywords `bolder`/`lighter` (which resolve to a value relative to the inherited weight rather
than an absolute one) are not implemented - encountering either keyword tolerantly falls back to
the inherited weight - and the `font-style` keyword `oblique` is folded into the same
`SvgFontStyle.Italic` value as `italic` rather than being distinguished as a third style (see
`SvgFontStyle`'s remarks in the source for the rationale). Within the supported `clipPath`/`mask`
features themselves, the following are explicitly out of scope (see _Clipping and Masking_ below
for the full rationale behind each): a nested `clip-path`/`mask`/`filter` applied to a `clipPath`
element's own children (clip children are rasterized directly, not walked through the ordinary
element-rendering recursion that resolves those attributes elsewhere); a `clip-path`/`mask`/
`filter` attribute placed on the `clipPath`/`mask` element itself (both are `NonRenderingElements`
never reached by the top-down walk, so any such attribute is inert); `<use>`/`<g>`/`<line>`
children of a `clipPath` (only `rect`/`circle`/`ellipse`/`polyline`/`polygon`/`path`/`text` clip
children are supported - `<line>` is excluded because a zero-area centerline contributes nothing
to a union clip, matching the SVG specification); `mask-type`/`mask-mode: alpha` (only luminance
mask semantics are implemented; an alpha-mode request tolerantly falls back to luminance); and a
`clipPath`/`mask` `href`/`xlink:href` template-inheritance chain (mirroring the gradient
chain-walk pattern) - an empty `clipPath`/`mask` with only an `href` resolves to "no children."
Within the supported `pattern` feature itself, the following are explicitly out of scope (see
_Pattern Paint-Server_ below for the full rationale behind each): `href`/`xlink:href` inheritance
of a pattern's own `x`/`y`/`width`/`height`/`patternUnits`/`patternContentUnits`/
`patternTransform`/`viewBox`/`preserveAspectRatio` geometry attributes (only tile _content_ is
inherited through the chain, mirroring the gradient `href` chain's identical "geometry is never
inherited, only stops/content are" simplification); a rendered tile-buffer cache (every pattern
fill/stroke re-renders its own tile from scratch, since a tile's own pixel size depends on the
referencing shape's own bounding box, not only the `pattern` element itself); a dedicated
pattern-reference cycle-depth counter (a pattern-content cycle is instead caught for free by the
pre-existing `MaxElementDepth` guard, mirroring `mask`'s identical reliance); and bilinear/other
non-nearest-neighbor tile resampling (matching this codec's existing "no resampling filter
anywhere" convention).

A percentage value on a shape/text geometry attribute (`x`, `y`, `width`, `height`, `rx`, `ry`,
`cx`, `cy`, `r`, `x1`/`y1`/`x2`/`y2`, `font-size`, `stroke-width`, `stroke-dasharray`,
`stroke-dashoffset`, `use`'s `x`/`y`/`width`/`height`, and `text`'s `x`/`y`) is now a supported
new capability, resolved against the current viewport (see _Percentage-Based Geometry
Resolution_ below) rather than rejected. `stroke-miterlimit` is the sole documented exception -
a unitless ratio, not a length, so a trailing `%` there continues to be treated as an
unparseable/unrecognized value and tolerantly falls back to the inherited miter limit (see
_Error Handling_ below), matching this codec's existing tolerant handling of a malformed
`stroke-miterlimit`.

### Percentage-Based Geometry Resolution

Every geometry attribute listed above resolves a trailing `%` through one shared,
centralized choke point (`ParseGeometryCoordinate`, reached via `GetFloatAttribute`/
`GetOptionalFloat`) rather than each call site parsing percentages independently, so the
existing finite/magnitude guards each of those methods already apply still run **after**
percentage resolution rather than being bypassed for the percentage case. Percentage resolution
uses one of three bases, chosen per attribute by its own geometric axis:

- **Horizontal** (`x`, `width`, `cx`, `x1`/`x2`, `use`'s `x`/`width`, `text`'s `x`) - the current
  viewport's width
- **Vertical** (`y`, `height`, `cy`, `y1`/`y2`, `use`'s `y`/`height`, `text`'s `y`) - the current
  viewport's height
- **Diagonal/axis-agnostic** (`r`, `stroke-width`, `stroke-dasharray`, `font-size`'s basis is a
  further special case below) - `sqrt(viewportWidth^2 + viewportHeight^2) / sqrt(2)`, the SVG
  specification's defined basis for a length with no natural single axis

`font-size`'s percentage is a further special case, resolving against the **parent element's own
already-cascaded `font-size`** (the CSS/SVG-defined basis for a font-relative percentage), not
any viewport dimension. The "current viewport" a horizontal/vertical/diagonal percentage resolves
against is the nearest enclosing element that establishes one (the root `svg`, or a `symbol`
referenced via `use` with its own `viewBox`) - threaded through the existing per-element cascading
`RenderState` record (as two new `ViewportWidth`/`ViewportHeight` fields) rather than a second,
parallel mechanism. A `marker` element establishes no viewport of its own for this purpose: its
content cascade inherits `ViewportWidth`/`ViewportHeight` from the shape it decorates, so a
percentage inside marker content resolves against the referencing shape's own viewport.

**Accepted, documented simplification: gradient `userSpaceOnUse` coordinates.** A
`linearGradient`/`radialGradient`'s own `x1`/`y1`/`x2`/`y2`/`cx`/`cy`/`r`/`fx`/`fy`/`fr`
coordinates are resolved through a separate, gradient-specific code path
(`GetGradientCoordinateOrDefault`) that always treats a percentage as a `[0, 1]`-fraction of the
gradient's own coordinate space, regardless of `gradientUnits`. This is spec-correct for the
default `objectBoundingBox` mode, but under `gradientUnits="userSpaceOnUse"` the SVG
specification instead defines a percentage there as resolving against the current viewport,
exactly like the shape/text geometry attributes above. Threading a viewport basis through
`ResolvePaint`/`BuildGradient`/`GetGradientCoordinateOrDefault` for this one `userSpaceOnUse`
case was evaluated for this phase and intentionally deferred to a follow-up phase as a bounded,
documented simplification (not a defect) - it is unaffected by, and independent of, every other
percentage capability described above.

### Data Model

`SvgCodec` now has two small public data types of its own, alongside the shared
`Codecs.ImageInfo` record struct (see _Codecs Subsystem Design_, `../codecs.md`): `SvgFontFace`
(a `readonly record struct` pairing a `Fonts.TrueTypeFont` with the `Weight`/`Style` face it
represents, modeled directly on `ImageInfo`'s "plain immutable data carrier" style) and
`SvgFontStyle` (a public two-value enum, `Normal`/`Italic`, deliberately narrowed from CSS's
three-way `normal`/`italic`/`oblique` keyword set — the reported use case, "bold titles and
italic keywords", never needs to distinguish a true italic face from a mechanically-slanted
`oblique` one, so `SvgCodec`'s `font-style` parser maps both keywords onto `Italic`). Internally,
it defines a private `RenderState` record capturing the cascading presentation state (fill,
stroke, fill-opacity, stroke-opacity, opacity, fill-rule, stroke-width, stroke-linecap,
stroke-linejoin, stroke-miterlimit, stroke-dasharray, stroke-dashoffset, font-family, font-size,
font-weight, font-style, text-anchor, and the three marker-start/marker-mid/marker-end
specifications) that is threaded down through the element tree alongside an accumulated
`System.Numerics.Matrix3x2` transform, plus a private `RenderContext` capturing fixed per-document
state (the id→`XElement` index, the caller's font-family-to-`SvgFontFace`-list dictionary, and
the resolved fit transform).

### XML Parsing Hardening (XXE)

Both `LoadRootElement` (backing `Load`'s full-document parse) and `LoadRootElementAttributesOnly`
(backing `GetInfo`'s root-start-tag-only parse) construct their own `System.Xml.XmlReaderSettings`
and explicitly set `DtdProcessing = DtdProcessing.Prohibit` and `XmlResolver = null` on each,
in addition to the `MaxCharactersInDocument` budget each already enforces (see _GetInfo Fallback
Policy_ and _Error Handling_ below). These two settings are already `XmlReaderSettings`'s
effective defaults on .NET, so this does not change observed behavior for any input accepted
today; the point is defense-in-depth documentation clarity against XML External Entity (XXE)
injection, rather than closing a currently-exploitable gap. A reader that processed a `DOCTYPE`
declaration, or resolved an external entity referenced from one, could otherwise be used to read
arbitrary local files or trigger unexpected network requests merely by parsing a maliciously
crafted SVG document; explicit settings on both `XmlReaderSettings` instances ensure this
hardening cannot be silently lost by a future .NET default change or by an incomplete edit to only
one of the two settings-construction sites. A `DOCTYPE`-bearing document - including one declaring
an external or parameter entity - is rejected with `InvalidDataException` via both `Load` and
`GetInfo`, exactly as any other malformed-XML input is (see _Error Handling_ below).

### Key Methods

#### LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary\<string, IReadOnlyList\<SvgFontFace\>\>?)

Named distinctly from `Load` (rather than overloaded onto it) because both `fonts` dictionary
value types share the same parameter arity, so an overload sharing the `Load` name would make an
explicit untyped `null` literal passed as the 4th positional argument ambiguous between the two
overloads — a source-breaking compile error for existing callers using that pattern. Reads an
SVG document from an open stream and rasterizes it into a new `width`x`height`
`Surface`. Parses the document with `XDocument.Load`, builds an id→`XElement` index over the
whole tree up front (so `use`/`href`/gradient-template references resolve correctly regardless of
document order), resolves the root `viewBox`/`width`/`height` into an intrinsic size, computes
the `preserveAspectRatio`-driven fit transform into the requested raster (see _ViewBox Fitting
Policy_ below), then recursively walks the tree, baking every transform (the root fit transform composed
with every nested `g`/element `transform`) directly into the `Vector2` points fed into
`Geometry.PathBuilder` before calling `Drawing.PathFiller.Fill`/`Drawing.PathStroker.Stroke` —
neither of which has a transform parameter; they treat `Geometry.Path` coordinates as final
pixel-space. `fonts` is optional; when supplied, `text` elements are matched against it by family
name, then, when a family has more than one registered `SvgFontFace`, by closest
`font-weight`/`font-style` match (see _Text_ under _Gradient, Use, and Text Support and Limits_
below). This is the richer of the two `fonts`-accepting overloads — the legacy single-font-per-
family overload below delegates to this one.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the stream is not well-formed XML, or the document's `viewBox`,
  `transform`, gradient, or path `d` data is malformed (see _Error Handling_ below)
- `ArgumentOutOfRangeException` — `width` or `height` is not positive (propagated, unwrapped,
  from `Surface`'s own constructor — see _Error Handling_ below)

`fonts` is optional; a `null` dictionary, a dictionary with no entry matching a requested
`font-family`, or a matching entry whose face list is empty, all cause the affected `text`
element(s) to be silently skipped rather than throwing — see _Gradient, Use, and Text Support and
Limits_ below.

#### LoadWithFontFaces(string, int, int, IReadOnlyDictionary\<string, IReadOnlyList\<SvgFontFace\>\>?)

Opens `path` as a read-only `FileStream` and delegates to
`LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>?)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is empty or consists only of whitespace
- `InvalidDataException` / `ArgumentOutOfRangeException` — see the stream overload above
- Underlying file-system exceptions propagate uncaught

#### Load(Stream stream, int width, int height, IReadOnlyDictionary\<string, TrueTypeFont\>? fonts = null)

The legacy, pre-existing overload: accepts at most one `TrueTypeFont` per font-family. A thin
wrapper that delegates to `LoadWithFontFaces` above via a private `ToFontFaces` helper, which
wraps each dictionary entry as a single normal-weight (`400`)/normal-style `SvgFontFace` — so
every `text` element resolves to that single registered font regardless of its own
`font-weight`/`font-style`, preserving this overload's behavior exactly as it was before
`SvgFontFace` existed. Reads an SVG document from an open stream and rasterizes it into a new
`width`x`height` `Surface`. `fonts` is optional; when supplied, `text` elements are matched
against it by family name (see _Text_ below).

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the stream is not well-formed XML, or the document's `viewBox`,
  `transform`, gradient, or path `d` data is malformed (see _Error Handling_ below)
- `ArgumentOutOfRangeException` — `width` or `height` is not positive (propagated, unwrapped,
  from `Surface`'s own constructor — see _Error Handling_ below)

`fonts` is optional (defaulting to `null`); a `null` dictionary, or a dictionary containing a
`null`-valued entry that happens to match a requested `font-family`, both cause the affected
`text` element(s) to be silently skipped rather than throwing — see _Gradient, Use, and Text
Support and Limits_ below.

#### Load(string path, int width, int height, IReadOnlyDictionary\<string, TrueTypeFont\>? fonts = null)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream, int, int, ...)`, the
legacy single-font-per-family overload directly above.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is empty or consists only of whitespace
- `InvalidDataException` / `ArgumentOutOfRangeException` — see `Load(Stream, int, int, ...)`
- Underlying file-system exceptions propagate uncaught

#### GetInfo(Stream stream)

Reads only the root `svg` start-tag's own attributes, using a forward-only `System.Xml.XmlReader`
that advances no further than the root element's attributes and never walks into the document
body. This reader is still bounded by the same fixed `MaxCharactersInDocument` character cap
`Load`'s own parse enforces: even though the reader never advances into the document body, it
still advances character-by-character through the root start-tag's own attribute values, so an
oversized single attribute value alone could otherwise force an unbounded amount of data to be
materialized. Resolves the intrinsic size per the three-tier fallback policy described in _GetInfo
Fallback Policy_ below. This is intentionally narrower than `Load`'s full `XDocument.Load` parse
of the whole document: a document that is malformed only beyond the root `svg` element's own
attributes is accepted by `GetInfo` (which never reads that far) even though `Load` would reject
it. Always reports `Channels = 4` and `HasAlpha = true`, because every SVG document this codec
rasterizes produces an RGBA `Surface` regardless of what the source markup does or does not
paint.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the stream is not well-formed XML up to and including the root
  start-tag, its root element is not named `svg`, its `viewBox` attribute is present but
  malformed, or the root start-tag alone (including an oversized attribute value) exceeds the
  fixed character budget

#### GetInfo(string path)

Opens `path` as a read-only `FileStream` and delegates to `GetInfo(Stream)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is empty or consists only of whitespace
- `InvalidDataException` — see `GetInfo(Stream)`
- Underlying file-system exceptions propagate uncaught

### ViewBox Fitting Policy

`SvgCodec` resolves the root element's intrinsic user-space origin/size from its `viewBox`
attribute when present; otherwise from its `width`/`height` attributes when both are present and
positive; otherwise it falls back to the CSS/UA default replaced-element intrinsic size of
300x150 (see _GetInfo Fallback Policy_ below — `Load` and `GetInfo` share this resolution logic).
That intrinsic size is then fit into the caller-requested raster via the root `svg` element's own
`preserveAspectRatio` attribute, parsed as `[defer] <align> [<meetOrSlice>]`: `defer` is parsed
and ignored (it only matters when an `<image>` element also declares its own
`preserveAspectRatio`, which this codec does not implement); `<align>` is `none`, or one of the
9 combinations of `xMin`/`xMid`/`xMax` and `YMin`/`YMid`/`YMax`; `<meetOrSlice>` is `meet`
(default) or `slice`. An absent `preserveAspectRatio` attribute defaults to `xMidYMid meet` — the
equivalent of CSS `object-fit: contain` — scaling uniformly by the smaller of the width and
height ratios and centering the result in the raster, leaving transparent letterbox bars along
whichever axis the intrinsic aspect ratio does not fill: **this default behavior is unchanged
from before this codec parsed `preserveAspectRatio` at all**, since "meet, centered" already _is_
`xMidYMid meet`. An explicit `align="none"` instead stretches the intrinsic size independently on
each axis to exactly fill the raster (no letterboxing, no uniform-scale constraint); every other
explicit `<align>` scales uniformly (by the smaller ratio for `meet`, matching CSS
`object-fit: contain`, or the larger ratio for `slice`, matching CSS `object-fit: cover` and
overflowing the raster instead of letterboxing it) and positions the result along each axis per
its `Min`/`Mid`/`Max` component (flush to the start, centered, or flush to the end, respectively).

This same fit computation (`ComputePreserveAspectRatioFit`) is shared by two further reuse sites,
both new capabilities:

- **`symbol`/`use` viewBox-fit.** When a `use` element references a `symbol` element that has its
  own `viewBox`, the `symbol`'s intrinsic viewBox content is fit into the `use`/`symbol`
  element's own resolved `width`/`height` (falling back, in order, to the `use` element's own
  `width`/`height`, then the `symbol`'s own `width`/`height`, then the current viewport's own
  size), honoring the `symbol`'s own `preserveAspectRatio` attribute exactly like the root `svg`
  case above. The `symbol`'s own viewBox becomes the current viewport for its content's own
  percentage-geometry resolution (see _Percentage-Based Geometry Resolution_ above).
- **`marker`'s own explicit `preserveAspectRatio`.** A `marker` element with both a `viewBox` and
  an _explicit_ `preserveAspectRatio` attribute fits its `viewBox` into its own
  `markerWidth`/`markerHeight` through this same shared helper, honoring the viewBox's own origin
  and the requested `<align>`/`<meetOrSlice>`. A marker with a `viewBox` but **no** explicit
  `preserveAspectRatio` attribute instead keeps its original, simpler fit (a plain "meet"-
  equivalent uniform scale-down with no centering step) completely unchanged, for two reasons:
  first, to guarantee this default path's pre-existing pixel output never regresses; second,
  because a marker's content is always anchored by its own `refX`/`refY` (never clipped to
  `markerWidth`/`markerHeight` - see _Out-of-scope subset_ above), so once that anchoring is
  applied, a viewBox's own origin and an `<align>`'s `Min`/`Mid`/`Max` offset both algebraically
  cancel out of the final rendered position regardless of their value - only the uniform scale
  factor itself (`meet`'s smaller ratio versus `slice`'s larger one) is ever visibly different
  between the default path and an explicit `preserveAspectRatio`. Routing the default (no
  attribute) case through the same origin/align-honoring formula would therefore be a purely
  internal correctness improvement, not a visible behavior change - reserved for if marker-content
  clipping is ever implemented - so it is deliberately kept as a distinct code path instead of
  being unified, to avoid any incidental floating-point (ULP) difference in the default path's
  output.

An intrinsic size that is positive and finite (passing the checks above) can still be small
enough — a subnormal float, for example — that dividing the requested raster dimensions by it
overflows the computed scale to a non-finite value. `Load` validates the resulting fit transform
with the same finiteness check used for composed element transforms immediately after computing
it, and rejects it with `InvalidDataException` rather than silently proceeding: an unchecked
non-finite fit transform would otherwise cause every element in the document to fail that same
per-element finiteness check and render a blank, fully-transparent surface with no exception at
all — a worse "quiet" failure than the sibling non-positive-size case already throws for. The
`symbol`/`use` and `marker` reuse sites above apply the same non-finite-fit tolerance: a
non-finite fit at either site is treated as "no paint" for that `use`/marker instance (skipped
rather than propagating a non-finite value into the rasterizer), matching this codec's existing
tolerant-skip convention for other overflow cases (see _Error Handling_ below).

### GetInfo Fallback Policy

`GetInfo` (and `Load`, internally, for the same purpose) resolves a document's size in three
tiers, in order: (1) the `viewBox` attribute, if present; (2) the `width`/`height` attributes, if
both are present and resolve to positive numbers; (3) the CSS/UA default replaced-element
intrinsic size of 300x150, if neither of the above is present. This mirrors how a web browser
sizes a sizeless, viewBox-less `<img src="...svg">` element, and ensures `GetInfo` always returns
a usable, positive size rather than failing on a document that omits explicit sizing information
entirely. The resolved floating-point size is clamped to the valid `[1, int.MaxValue]` pixel-
dimension range **before** it is cast to `int` — using a `double` intermediate for the clamp,
since `int.MaxValue` is not exactly representable as a `float` (it rounds up to `2147483648f`,
undefined behavior to cast to `int`) but **is** exactly representable as a `double`. This bounds a
`viewBox`/`width`/`height` value large enough to otherwise overflow `Int32` on cast to a
well-defined, valid `ImageInfo` dimension instead.

### Presentation-Attribute Inheritance Model

Every presentation attribute this codec understands (see _In-scope subset_ above) cascades from
a `g` element (or the root `svg` element) to its descendants, exactly like CSS inheritance for
the same SVG properties: a child that does not specify its own value for an attribute inherits
its nearest ancestor's resolved value; a child that does specify its own value overrides the
inherited one for itself and its own descendants. `opacity` is the one attribute that does not
purely inherit as a resolved value — it **multiplies** down the tree (a `g` with `opacity="0.5"`
containing a child with its own `opacity="0.5"` renders that child at an effective 25% opacity).
`SvgCodec` folds `opacity` directly into the alpha channel of whatever `fill`/`stroke` color (or
gradient stop) it resolves to at the leaf, rather than modeling isolated group compositing
(rendering a group to an offscreen buffer and compositing it as a unit). This is a deliberate,
documented simplification: it produces visually identical results for the common case of
non-overlapping shapes within a semi-transparent group, but does not reproduce the subtly
different result SVG's isolated-group compositing model would produce for overlapping shapes
within the same semi-transparent group. A filtered element (see **Filters** below) is the one
exception to this per-pixel fold: because `opacity` applies to the filtered result as a whole,
not to the pre-filter source paint, its `SourceGraphic` is instead rendered fully opaque, and the
element's own `opacity` is applied exactly once, afterward, as a uniform coverage multiplier when
the filtered result is composited onto the canvas.

### Gradient, Use, and Text Support and Limits

**Gradients.** A `fill`/`stroke` of `url(#id)` referencing a `linearGradient`/`radialGradient`
element is resolved into a `Drawing.LinearGradient`/`Drawing.RadialGradient` with
`Drawing.GradientStop`s, honoring `gradientUnits`, `gradientTransform`, and `spreadMethod`
(mapped to `Drawing.GradientSpread`). A gradient element with no `stop` children of its own
inherits its stops from the gradient it references via `href`/`xlink:href`, walking a single
linear chain (not merging stops across multiple levels) with a visited-set cycle check that
raises `InvalidDataException` if the chain loops back on itself, rather than looping
indefinitely. Only color stops are inherited this way — a gradient's own `gradientUnits`/
`gradientTransform`/`spreadMethod`/coordinate attributes are always read directly from the
gradient element itself, never inherited through the chain. This is a deliberate, bounded
simplification of the full SVG href-inheritance model. A gradient element's resolved (pre-alpha)
color stops are cached per gradient element for the lifetime of one `Load` call (keyed by the
gradient `XElement`'s own reference identity), so a gradient referenced by many shapes - directly,
or via many sibling `use` elements - has its `stop` children parsed, and its `href` chain walked,
only once rather than once per reference. This is safe because a gradient's stops never change
within a single `Load` call: the parsed document is built once and never mutated afterward, and
this codec implements no scripting/animation support that could redefine them mid-render.

**Use.** A `use` element referencing any element by id via `href`/`xlink:href` renders a copy of
the referenced element (translated by the `use` element's own `x`/`y`), resolved through the
document-order-independent id index described above. When the referenced element is a `symbol`
with its own `viewBox`, the `symbol`'s content is additionally fit into the `use`/`symbol`
element's resolved `width`/`height` via the shared `preserveAspectRatio` fit helper - see
_ViewBox Fitting Policy_'s "`symbol`/`use` viewBox-fit" bullet above - composed as
`viewportFit * Translate(x, y) * transform`, so the `use` element's own `x`/`y` translation is
applied in the _outer_, already-fitted coordinate space, exactly as the SVG specification
describes. A `use` referencing a nonexistent id, or a resolved `width`/`height`/fit that is
non-positive, non-finite, or otherwise degenerate, is tolerated as a silent no-op. Because `use`
can reference another `use` (directly or through intervening groups), rendering guards against
unbounded mutual recursion with a fixed maximum recursion depth, raising `InvalidDataException`
if it is exceeded rather than recursing indefinitely - see **Element/Group Nesting and
Total-Element Bounds** below for why this depth cap, on its own, does not bound every form of
unbounded rendering work.

**Markers.** A `line`/`polyline`/`polygon`/`path` element's own `marker-start`/`marker-mid`/
`marker-end` presentation attributes (each `url(#id)`, resolved through the same id index and
dangling-reference tolerance as a gradient `fill`/`stroke` reference above) each identify a
`marker` element rendered once per eligible vertex of that shape's already-built local-space
outline (never re-parsed from the shape's own raw attribute text): the first vertex uses
`marker-start`, the last uses `marker-end`, and every vertex between uses `marker-mid` - a
2-vertex shape therefore has a start and an end but no mid. For a multi-subpath `path`, this
whole-shape first/last classification is a deliberate, documented simplification: `marker-start`/
`marker-end` apply only to the very first/last vertex of the _whole_ path, not to each subpath's
own start/end (matches at least one common browser's behavior, rather than a spec clause verified
directly). A vertex's `orient="auto"` rotation angle is the average of its incoming and outgoing
segment tangents (falling back to whichever one is present at an open subpath's own start/end),
except that `orient="auto-start-reverse"` adds a further 180 degrees at the shape's very first
vertex only. Per the SVG specification, an absent/blank `orient` attribute uses the fixed
0-degree default (no rotation) rather than following the vertex tangent - only the explicit
`auto`/`auto-start-reverse` keywords opt into tangent-following behavior; an unparseable explicit
value also tolerantly falls back to this same 0-degree default. Each marker instance is scaled by
`markerWidth`/`markerHeight` (further fitted by the marker's own optional `viewBox` - see
_ViewBox Fitting Policy_'s "marker's own explicit `preserveAspectRatio`" bullet above for the two
distinct code paths this fitting takes depending on whether the marker declares an explicit
`preserveAspectRatio` attribute), then
by the referencing shape's own effective stroke width when `markerUnits` is `strokeWidth` (the
default) or left at 1:1 for `userSpaceOnUse`, then rotated and translated to the vertex position,
and finally composed with the shape's own accumulated transform - so a marker is transformed
through exactly the same pipeline as the shape it decorates. A marker's content renders through a
fresh `RenderState` cascade seeded from the marker element's own presentation attributes (or the
SVG/CSS initial defaults if it sets none), never inheriting the referencing shape's own fill/
stroke - per the SVG specification's independent marker-content model. Marker content renders
through the same `RenderElement` recursion used for every other element (including `use`), so the
existing element-tree-depth and total-rendered-element bounds described below apply to it
automatically; because a `marker` can reference another `marker` (directly, or through a chain,
via a child shape's own `marker-start`/`marker-mid`/`marker-end`), rendering additionally tracks
its own independent `marker`-reference recursion depth, mirroring `use`'s cycle guard exactly
(same fixed-depth-cap pattern, same `InvalidDataException` on exceeding it) but counted
separately, since a marker chain and a `use` chain are independent nesting concerns. `rect`/
`circle`/`ellipse` never receive markers (these shapes have no natural vertices to orient one
along), and a `marker-start`/`marker-mid`/`marker-end` referencing a nonexistent id, or an id that
does not resolve to a `marker` element, is tolerated as a silent no-op for that one vertex.

**Filters.** A directly renderable shape or `text` element's own `filter` presentation attribute
(`url(#id)`, resolved through the same id index and dangling-reference tolerance as a gradient
`fill`/`stroke` reference above) identifies a `filter` element whose primitive children are
evaluated against that one element's own rendered content - `filter` never cascades through
`RenderState` (matching `transform`'s own non-cascading handling), so a filter on an ancestor
element has no effect on its descendants' own, independent `filter` attributes. A `g`/`symbol`
reference or `use` element's own `filter` attribute is resolved the same way but is instead
evaluated against the _whole resolved subtree_'s combined rendered content, as one unit (see
**Group-level filters** below); `marker` content is never recursed into by the ordinary element
walk regardless of any `filter` attribute present there, so filters on marker content continue to
have no effect. The filter region - the rectangular area, in the
element's own local space, that the filter's temporary offscreen buffer covers - defaults to
-10%/-10%/120%/120% (`x`/`y`/`width`/`height`, objectBoundingBox units) of the element's own
local-space bounding box, or uses the filter element's own explicit `x`/`y`/`width`/`height`
attributes when present (still always interpreted as objectBoundingBox-relative fractions, even
when `filterUnits="userSpaceOnUse"` is declared - a deliberate, tolerant simplification). This
local-space region is transformed by the element's own accumulated transform, then rounded
outward to an integer pixel bounding box; if that box is degenerate, non-finite, or exceeds
`Surface.MaxDimension` on either axis, the filter is tolerantly skipped entirely and the element
renders normally, exactly as if it had no `filter` attribute - the same bounded-resource
philosophy as `Surface`'s own dimension cap, applied here to prevent an attacker-controlled
filter region from driving an unbounded temporary allocation. The filter's own `fe*` primitive
chain is additionally bounded by a fixed primitive-count cap (`MaxFilterPrimitivesPerFilter`,
1,000) and a primitive-count-times-region-area work budget (`MaxFilterPrimitiveWorkUnits`,
5,000,000), checked once, upfront, before `SourceGraphic` is even allocated, and tolerantly
skipped the same way once either is exceeded - because, unlike the per-element costs
`MaxTotalRenderedElements` already bounds (which assumes O(1)/O(perimeter) cost per element, not
O(region-area) cost per primitive), a single filter's own primitive-chain cost is
O(primitive count × region area), a cost dimension no pre-existing guard actually covers. That
per-filter ceiling alone is enforced independently for every shape that references a filter, so a
single `filter` definition referenced by many shapes could otherwise be charged the same ceiling
once per reference with no bound on the total number of references - a document with enough
shapes could drive total filter-evaluation work arbitrarily high even though every individual
reference stayed within budget. A second, cumulative, per-`Load`-call `FilterWorkBudget` closes
that gap: it charges each filter application's own region-weighted work unit into one running
total across the whole document, and once that running total would exceed a fixed
`MaxCumulativeFilterWorkUnits` ceiling (50,000,000 - ten times `MaxFilterPrimitiveWorkUnits`,
generous enough that a real document reusing one filter across a modest number of shapes is never
rejected, while still keeping worst-case total filter-evaluation work bounded to a small, fixed
multiple of a single filter's own bound), every further filter application for the remainder of
that `Load` call tolerantly falls back to unfiltered rendering instead - the same fallback
behavior as every other filter resource bound, never a thrown exception. A
`filter` element with zero primitive children is likewise tolerantly skipped, identically to an
out-of-budget chain and checked in the same guard, before `SourceGraphic` is allocated - because
a filter with no primitives to evaluate can never change the rendered output, regardless of how
large its filter region is. When the
region is accepted and the chain's work stays within both the per-filter and cumulative budgets,
the
element's own content is rendered a second time - independently of its main render onto
`context.Surface` - into a fresh, region-sized temporary `Surface` (this buffer is the filter's
implicit `SourceGraphic` input; `SourceAlpha`, its alpha-only derivative, is built lazily only if
some primitive actually references it). Each primitive in document order reads a named or
default input (the previous primitive's own output, or `SourceGraphic` if it is first), and
writes a named or default output, all buffers always exactly the temporary surface's own
dimensions - this invariant is what lets `feComposite`'s `over` operator and `feMerge` both reuse
`Surface.CompositeOver(Surface)` (which requires equal-size surfaces) unchanged, with no new
per-pixel blending math. `feFlood` fills a buffer with a solid, alpha-scaled color
(`flood-color`/`flood-opacity`). `feGaussianBlur` approximates a Gaussian blur with three passes
of a sliding-window box blur (cost independent of the requested radius, unlike a naive
per-pixel-kernel blur), reading only the first (isotropic) component of `stdDeviation` and
clamping it to a fixed `MaxFilterBlurStdDeviationPixels` bound so a pathologically large
requested radius cannot translate into unbounded per-pixel work. `feOffset` shifts a buffer's
content by `dx`/`dy` (scaled by the element's own transform). `feComposite` implements Porter-Duff
`over` by delegating to `Surface.CompositeOver`, and implements `in`/`out`/`atop`/`xor` via one
small, dedicated per-pixel premultiplied-alpha helper - the only genuinely new blending math this
feature introduces. `feMerge` layers each `feMergeNode` child's own resolved input over an
initially transparent accumulator, in document order, via the same `CompositeOver`. Any other
primitive type (`feColorMatrix`, `feTurbulence`, `feDisplacementMap`, `feImage`, `feTile`,
`feDropShadow`, `feConvolveMatrix`, `feDiffuseLighting`, `feSpecularLighting`,
`feComponentTransfer`, `feMorphology`) is a tolerant no-op passthrough of its own input, still
registered under its own `result` name so later primitives can still resolve it by name. The
final primitive's own output buffer is composited onto `context.Surface` at the region's own
pixel position via `Surface.CompositeOverSpan`, clipped to the canvas's own bounds - reusing the
same offset-aware compositing primitive used elsewhere in this codec, rather than inventing new
canvas-writing logic for filters. The element's own cascaded `opacity` (forced to `1.0` while
`SourceGraphic` itself is rendered, so the filter chain always evaluates against a fully-opaque
source) is applied at this same final compositing step, as a uniform per-pixel coverage
multiplier passed to `CompositeOverSpan` - per SVG semantics, `opacity` applies to the filtered
result as a whole, exactly once, not to the pre-filter source paint.

**Group-level filters.** A `filter` attribute on a `g`/`symbol` reference or `use` element
(`RenderFilteredGroup`) reuses the same per-shape pipeline above - offscreen render, primitive
chain, final composite - applied to the group's _combined_ subtree instead of a single element's
own content. Because the filter region must be computed before anything is rendered (the region
determines the temporary buffer's own size), and a group has no single `Path` to call
`GetBounds()` on, a dedicated bounds-only pre-pass (`ComputeSubtreeLocalBounds`) mirrors
`RenderElement`'s own dispatch switch - recursing into the same element kinds
(`g`/`symbol`/shapes/`text`/`use`), honoring the same `MaxElementDepth`/
`MaxTotalRenderedElements`/`MaxUseDepth` guards - but only accumulates each descendant's own
transformed bounding box (via `Rect.Union`) rather than painting anything. Because this pre-pass
necessarily re-visits the same subtree the real render pass below visits again immediately
afterward, `RenderFilteredGroup` always runs it against a fresh, local, independently bounded
scratch `totalElements` counter and `GeometryWorkBudget` instance - never the real
per-`Load`-call counter/budget `RenderElement` itself threads through - so this bounds-only
pre-pass can never permanently consume any of the real resource ceiling the render pass (and
every other filtered shape/group in the document) also needs; the same fixed ceiling constants
still bound the pre-pass's own work against a pathologically large subtree, just via a
call-scoped instance rather than the shared one. Resetting those local-scratch ceilings on every
invocation, however, leaves a distinct resource-safety gap open: a document with many levels of
nested filtered `g`/`symbol`/`use` elements, each wrapping a large subtree, causes each nesting
level's own `RenderFilteredGroup` call to re-walk an overlapping portion of that same subtree
(the real render pass only descends into a nested filtered group's own content after that
group's _own_ pre-pass has already walked it once more), so total pre-pass work grows with
`depth * subtree-size` even though every individual invocation's own element count stays
comfortably under `MaxTotalRenderedElements`. A third, cumulative, per-`Load`-call
`BoundsPrePassWorkBudget` closes this gap the same way `FilterWorkBudget` closes the analogous
"one filter, many references" gap: a single shared instance (never one of the fresh local-scratch
instances above) is threaded down to every `RenderFilteredGroup`/`ComputeSubtreeLocalBounds`/
`ComputeMarkerContentLocalBounds`/`ComputeOneMarkerLocalBounds` call for the whole document,
charged once per element visited by any bounds pre-pass, and once its running total would exceed
a fixed `MaxCumulativeBoundsPrePassWork` ceiling (1,000,000 - ten times
`MaxTotalRenderedElements`, generous enough that a real document with a modest few levels of
nested filtered groups is never rejected), `ComputeSubtreeLocalBounds` throws
`InvalidDataException` - the same throwing convention (and the same call site) as its own existing
`MaxElementDepth`/`MaxTotalRenderedElements` per-invocation guards, rather than the tolerant
per-filter fallback `FilterWorkBudget`/`MaxFilterPrimitiveWorkUnits` themselves use, since this
budget bounds the same kind of "this pre-pass walk is too expensive" condition those per-invocation
guards already treat as a hard rejection. That per-visit charge alone, however, only counts how
many elements a bounds pre-pass visits, never how expensive parsing any one of those elements'
own geometry actually is: a single `path`/`polyline`/`polygon`/`text` element with an enormous
`d`/`points`/text value, nested under many levels of filtered groups, would otherwise have that
same enormous geometry fully re-parsed once per nesting level (each level's own
`RenderFilteredGroup` pre-pass re-visits it, and each individual invocation's own fresh
`GeometryWorkBudget` never accumulates across invocations to catch the repetition) - real CPU cost
proportional to `depth * geometry-size` that neither `MaxCumulativeBoundsPrePassWork` nor any
per-invocation `GeometryWorkBudget` bounds. `BoundsPrePassWorkBudget` therefore also exposes a
second, geometry-weighted charge (`ChargeGeometry`), called from `ComputeSubtreeLocalBounds`'s
`path`/`polyline`/`polygon`/`text` cases using each element's own `d`/`points` attribute character
count (or text character count) as a cheap proxy for its re-parse cost - charged upfront, before
the corresponding `Build*Path` call actually re-parses that geometry, in addition to (never
instead of) the existing per-invocation `GeometryWorkBudget` charge for the same element. Once the
running geometry-weighted total would exceed a fixed `MaxCumulativeBoundsPrePassGeometryWork`
ceiling (2,000,000 - ten times `GeometryWorkBudget`'s own 200,000-unit per-invocation ceiling,
mirroring the same "10x a single operation's own ceiling" precedent used elsewhere in this class),
`ComputeSubtreeLocalBounds` throws `InvalidDataException` identically to its element-visit
counterpart above. A `line`/`polyline`/`polygon`/`path` descendant's
own placed marker content (`marker-start`/`marker-mid`/`marker-end`) is folded into this pre-pass
too (`ComputeMarkerContentLocalBounds`/`ComputeOneMarkerLocalBounds`, sharing
`TryComputeMarkerContentTransform` with `RenderOneMarker`'s own placement math so the two can
never diverge): the real render pass paints marker pixels onto whatever surface is current, which
for a filtered group is the offscreen `SourceGraphic` buffer this pre-pass sizes, and a marker
commonly extends beyond its host shape's own stroke-expanded outline (arrowheads being the
canonical example), so omitting that geometry would size the buffer too small and silently clip
the marker's own pixels. A bare `marker` element encountered directly by the ordinary element walk
is still excluded, unchanged: `marker` remains a `NonRenderingElements` member, and this pre-pass
only ever recurses into a `marker` element's own content when a shape's own
`marker-start`/`marker-mid`/`marker-end` attribute resolves to it, exactly mirroring
`RenderMarkers`/`RenderOneMarker`'s own recursion trigger. A descendant that itself carries a
resolvable `filter` attribute (a shape/`text` element, or a nested `g`/`symbol`/`use` rendered as
its own filtered unit via a nested `RenderFilteredGroup` call) contributes its own filter's
expanded output region (`ApplyOwnFilterToLocalBounds`/`ComputeFilterRegionLocalBounds`, a
pixel-rounding-free local-space variant of `ComputeFilterRegionPixelBounds`'s own region
computation) instead of its raw geometry bounds, mirroring the exact same "would this filter
actually apply, or tolerantly fall back to unfiltered rendering instead" decision
`RenderFilteredShape`/`RenderFilteredGroup` themselves make - a zero-primitive filter, or a filter
region that cannot be computed, falls back to that descendant's own raw bounds instead, so the
pre-pass and the real render pass that follows it never disagree about whether a given
descendant's filter will actually apply. Without this, an outer filtered group's own offscreen
buffer would be sized only from its descendants' raw geometry, silently clipping any nested
filtered descendant's own filter output that extends beyond its raw geometry (for example a
`feFlood` or an enlarged filter region) before the outer filter chain or final composite ever saw
it. `ApplyOwnFilterToLocalBounds` also falls back to that descendant's own raw bounds - rather
than its filter-expanded region - whenever the expanded region's own pre-transform local-space
size is already so large (compared directly against `MaxCoordinateMagnitude`) that it is virtually
certain to still be rejected by `ComputeFilterRegionPixelBounds`'s own pixel-space
`MaxCoordinateMagnitude` check once the real render pass eventually transforms it into actual
pixel space: without this, a descendant filter that is pathologically oversized and therefore
guaranteed to fall back to unfiltered rendering at real render time could still inflate this
pre-pass's own combined bounds enough to trip an _outer_ ancestor filter's own real pixel-space
rejection checks, incorrectly skipping a perfectly reasonable outer filter purely because of an
inner descendant filter that was never actually going to apply. This is a deliberately
conservative approximation, not a precise predictor (the ancestor's own further transform, not
yet known at this point in the pre-pass, is never composed into the comparison), erring toward
never rejecting a borderline-reasonable region so a filter that would genuinely survive the real
check is never under-sized here. The resulting combined local-space bounds are then fed through the same
`ComputeFilterRegionPixelBounds` used for single shapes
(refactored to accept a `Rect` directly, so both call sites share one region-computation core),
and the same per-filter/cumulative work-budget guards apply identically - a group's filter region
can be pathologically large or its combined primitive-count × region-area cost excessive in
exactly the same ways a single shape's can, so no separate resource-safety mechanism was needed.
If the bounds pre-pass finds no renderable content at all (an empty group), or the filter
reference is missing/invalid, or any resource-safety guard rejects the filter, the group falls
back to rendering its children directly and unfiltered - the same tolerant fallback convention as
every other filter resource bound. When the filter is accepted, the group's subtree is rendered a
second time into the region-sized temporary surface (with the group's own `opacity` forced to
`1.0`, mirroring the single-shape convention, so the filter chain evaluates against fully-opaque
source content), the primitive chain evaluates identically to the single-shape case, and the
filtered result is composited back onto the canvas with the group's own `opacity` applied at that
final step - transform applies to the group as a whole via the transform already baked into the
region computation and the offscreen render; `clip-path`/`mask`, when present on the same
element, are applied to that offscreen content before the filter chain runs (see _Clipping and
Masking_ below for the full clip/mask algorithm and how this ordering was generalized across both
the single-shape and group entry points).

**Clipping and masking.** A directly renderable shape/`text` element's, or a `g`/`symbol`
reference/`use` element's, own `clip-path`/`mask` presentation attributes (each `url(#id)`,
resolved through the same id index and dangling-reference tolerance as a gradient `fill`/`stroke`
reference above, and each tolerant of a reference that resolves to a well-formed element which is
not literally a `clipPath`/`mask`, mirroring `filter`'s own wrong-element-type tolerance) are
resolved alongside `filter` at the same two entry points (`RenderShapeWithEffects`/
`RenderGroupWithEffects`, generalized from the pre-existing per-shape/per-group filter-only
pipeline) and applied, in document order, to that element's own pre-filter offscreen content:
geometry → fill/stroke → clip → mask → filter → group opacity - the same ordering the SVG
rendering model specifies, so an element combining `filter` with `clip-path`/`mask` has its
filter chain evaluate against the already-clipped-and-masked result, not the raw, unclipped
geometry.

_Clip-path._ `ApplyClipPath` builds a fresh `coverage` `Surface` the same size as the element's
own offscreen content, then rasterizes each direct child of the referenced `clipPath` element
(`rect`/`circle`/`ellipse`/`polyline`/`polygon`/`path`/`text` only - see the out-of-scope list
above for excluded child kinds) as opaque white directly onto that same buffer via
`PathFiller.Fill`, honoring each child's own `clip-rule` (`nonzero`/`evenodd`). Painting every
child directly over the same buffer, rather than computing an explicit path union, naturally
realizes "union of children" with no separate union step: a pixel covered by any child ends up
opaque regardless of how many children cover it. `ApplyCoverageClip` then multiplies the
element's own content's alpha channel by the coverage buffer's own alpha per pixel (255 →
unchanged, 0 → forced to zero, else linearly scaled) - a hard geometric clip realized as an
alpha-buffer intersection, since no native clip-region primitive exists on `Canvas`/`Surface`
(confirmed by inspection before implementation - only alpha-multiply compositing primitives are
available) - functionally equivalent to a true geometric clip for this codec's purposes, since
every subsequent compositing step in this codec already operates per-pixel on `Surface` alpha
data rather than through a vector clip stack. `clipPathUnits` (default `userSpaceOnUse`) governs
which coordinate system the `clipPath` element's own children are read in: `objectBoundingBox`
prepends an object-bounding-box-to-local-space map (`ComputeObjectBoundingBoxMap`, the same
helper gradients already use for `gradientUnits="objectBoundingBox"`, generalized here to accept a
`Rect` directly rather than only a `Path`) ahead of the element's own transform; the default,
`userSpaceOnUse`, resolves children directly in the referencing element's own local space with no
such prepended map.

_Mask._ `ApplyMask` renders the referenced `mask` element's own children through the _ordinary_
`RenderElement` recursive walk (not direct rasterization, unlike clip-path) into a fresh
`maskSource` `Surface`, then `ApplyLuminanceMask` multiplies the element's own content's alpha by
each mask pixel's own computed luminance (the standard sRGB coefficients
`0.2125*R + 0.7154*G + 0.0721*B`, itself scaled by that mask pixel's own alpha) - the
SVG-specification-default mask mode; `mask-type`/`mask-mode: alpha` is not implemented (see the
out-of-scope list above). Rendering mask content through the ordinary element walk, rather than a
bespoke rasterizer, is a deliberate asymmetry with clip-path: it means mask content can itself
freely use `clip-path`/`mask`/`filter` with no special-casing, and - just as importantly - it
means a mask-reference cycle (an element's `mask` whose own content transitively re-references an
ancestor element's `mask`) is already caught by the pre-existing `MaxElementDepth` guard
`RenderElement` enforces on every recursive descent, needing no new, dedicated depth counter of
its own. Clip-path children, rasterized directly rather than walked, have no equivalent
protection and so instead rely on a flat `MaxClipPathShapesPerClipPath` (256) ceiling on a single
`clipPath` element's own direct child count - and, since a `clipPath` element's own children are
never themselves resolved for a further nested `clip-path`/`mask` reference (see the out-of-scope
list above), a clip-path reference cannot cycle in the first place, so it needs no analogous
cycle guard. `mask`'s region box (`maskUnits`/`x`/`y`/`width`/`height`, default
`objectBoundingBox`) and its content's own coordinate system (`maskContentUnits`, default
`userSpaceOnUse`) are two independent attributes, resolved independently
(`ComputeMaskRegionLocalBounds` for the former, the same `clipPathUnits`-style
object-bounding-box-map prepending for the latter) rather than one being conflated with or
overriding the other - a distinction the SVG specification itself draws and this implementation
preserves. A percentage value under `maskUnits="userSpaceOnUse"` still resolves against the
reference bounds rather than the current viewport, a deliberate, documented simplification since
viewport dimensions are not threaded to this call site; only a bare number literal under
`userSpaceOnUse` is interpreted as a literal absolute local-space coordinate.

_Resource safety._ Both clip and mask reuse `FilterWorkBudget` (not a separate budget class) for
their own offscreen-buffer allocation charge - a deliberate judgment call rather than the
originally-considered new `EffectWorkBudget` class, because clip/mask offscreen-buffer allocation
is the exact same "region-area × content-count" cost shape `FilterWorkBudget` already bounds for
filter application, and reusing one cumulative ceiling across all three effects (rather than
three independent ceilings that could each individually permit a third of a document's total
offscreen-allocation budget) keeps one resource dimension's ceiling from being exhausted by
"legitimate reuse" spread across unrelated attributes. The single `TryCharge` call each of
`RenderShapeEffectsPipeline`/`RenderGroupWithEffects` makes before allocating its own "content"
buffer (via the shared `ComputeEffectsPipelineWorkUnits` helper) charges one region-area unit for
every offscreen buffer this element will actually allocate - the "content"/`SourceGraphic` buffer
always, plus one additional region-area unit each for `ApplyClipPath`'s own `coverage` buffer and
`ApplyMask`'s own `maskSource` buffer when a clip-path/mask respectively applies - so an element
combining clip-path and mask (or either with a filter) is charged for every buffer it allocates,
not just one. A mask region whose pixel-space dimensions
would exceed `Surface.MaxDimension` is rejected the same way an oversized filter region is,
tolerantly falling back to unmasked rendering rather than attempting an oversized allocation -
inherited for free from the same `ConvertLocalRegionToPixelBounds`/region-computation
generalization filter regions already used, now shared via `ResolveEffectsRegionPixelBounds` (see
**Region precedence** immediately below).

_Region precedence._ When more than one of `filter`/`clip-path`/`mask` are present on the same
element, `ResolveEffectsRegionPixelBounds` decides whose region sizes the shared offscreen
buffer(s): the filter's own region wins if a filter is present (unchanged pre-Phase-2 behavior -
filter regions are always objectBoundingBox-relative, a pre-existing simplification); otherwise
the mask's own region (`ComputeMaskRegionPixelBounds`, properly honoring `maskUnits`) if a mask is
present; otherwise (clip-only, no mask or filter) the plain painted/stroke-expanded content
bounds with no `-10%/120%` filter-style expansion. Extracting this precedence into one shared
helper - rather than duplicating an equivalent three-way conditional at both the single-shape and
group entry points - was also what resolved a `SonarAnalyzer` nested-ternary violation the
straightforward inline version of this logic triggered at each call site.

**Pattern paint-server.** A `fill`/`stroke` of `url(#id)` referencing a `pattern` element (resolved
through the same id index and dangling-reference/wrong-element-type tolerance as a gradient
`fill`/`stroke` reference above) is painted as a repeating tiled fill, entirely within
`SvgCodec.Patterns.cs`, dispatched from `RenderFill`/`RenderStroke` _before_ their existing
`ResolvePaint` call - a purely additive branch that leaves every pre-existing solid-color/
gradient/`none`/dangling-reference code path byte-identical. A `pattern` paint is deliberately
_not_ modeled as a `Drawing.Gradient` subtype: `Gradient`'s own constructor is `private protected`,
closing that hierarchy to exactly `LinearGradient`/`RadialGradient` by design, because
`GradientEvaluator`/`ScanlineRasterizer`'s own `Fill(Surface, Path, Gradient, ...)` entry point
pattern-matches exhaustively on those two subtypes alone; extending that closed hierarchy for
`pattern` would also require threading `RenderContext`/recursion state (the id index, fonts,
work budgets, element depth) into the `Drawing` namespace, which today has zero dependency on
`Codecs.Svg` concepts. `Drawing` remains untouched by pattern support - `pattern` is instead its
own, entirely manual, `SvgCodec`-layer per-pixel tile-sampling loop, the same architectural choice
Phase 2's `clipPath`/`mask` support already made for its own manual `Surface` operations rather
than extending `Drawing`.

Rendering proceeds in four stages. First, `RenderPatternFill` resolves the pattern's own tile
rectangle (`x`/`y`/`width`/`height`, `patternUnits` default `objectBoundingBox` reusing the same
`ComputeObjectBoundingBoxMap`/`GetGradientCoordinateOrDefault` helpers gradients already use) and
composes the grid-to-pixel transform as `patternTransform * bboxMap * elementTransform`, tolerant
of a non-invertible/non-finite result (mirrors `BuildGradient`'s own `IsFiniteTransform` check) or
a non-positive tile size, in either of which cases the fill/stroke tolerantly paints nothing.
Second, the tile's own pixel size is computed by reusing `ConvertLocalRegionToPixelBounds` (the
same helper filter/mask regions already use for their own pixel-space sizing/`Surface.MaxDimension`
enforcement - see `MaxPatternTileDimension`'s remarks), and a fresh tile-sized `Surface` is
rendered by walking the resolved content element's own children through the _ordinary_
`RenderElement` recursion (not a bespoke, feature-limited renderer) with `elementDepth + 1` -
exactly the deliberate asymmetry Phase 2's `mask` support already established over `clipPath`'s
direct rasterization, letting tile content freely use `clipPath`/`mask`/`filter`/nested `pattern`s
with no special-casing, and meaning a pattern-content reference cycle is already caught by the
pre-existing `MaxElementDepth` guard with no new, pattern-specific depth constant. `viewBox`/
`preserveAspectRatio` on the `pattern` element itself, when present, fits content into that tile
buffer via `ComputePreserveAspectRatioFit` (verbatim reuse of the same helper already used for
`<svg>`/`<symbol>`/`<marker>`), taking precedence over `patternContentUnits` when both are
present; otherwise, `patternContentUnits` (default `userSpaceOnUse`) is resolved independently of
`patternUnits` - a content coordinate is mapped through `ComputeObjectBoundingBoxMap` (for
`objectBoundingBox`) or left as-is (for `userSpaceOnUse`) against the referencing element's own
_whole_ bounding box, then back through the tile placement transform's own inverse into the tile
buffer's pixel space - so `patternContentUnits="objectBoundingBox"` always maps relative to the
whole referencing shape, never re-normalized to an individual repeated tile's own smaller
sub-range, exactly mirroring how `maskContentUnits` and `patternUnits` are each resolved as
genuinely independent attributes elsewhere in this codec. Third, `SampleTileIntoRegion` samples
the rendered tile buffer into a region-sized destination, one destination pixel at a time: each
pixel's own center is inverse-transformed back into grid space, wrapped (modulo) into the tile's
own `[0, width) x [0, height)` extent via `WrapCoordinate`, rescaled into the tile buffer's own
pixel coordinates, and sampled nearest-neighbor (matching this codec's existing "no bilinear
resampling anywhere" convention) - new, isolated, well-commented arithmetic with no existing
precedent to reuse, since this is a pure C# rasterizer with no native tiled/bitmap-shader
primitive of any kind. That sample's own alpha is then multiplied by a freshly rasterized
antialiased fill/stroke coverage buffer (`PathFiller.Fill` into a fresh `Surface`, the same
approach `ApplyCoverageClip` already uses, generalized here to a fractional, not merely binary,
coverage-multiply since this buffer also carries the sampled tile's own color). Fourth, the
result is composited onto the real canvas via `CompositeFilterResultOntoCanvas` (verbatim reuse,
applying the combined `fill-opacity`/`stroke-opacity`/cascaded-`opacity` multiplier uniformly).

_Resource safety._ Because no rendered-tile cache exists (a tile's own pixel size and content
mapping both depend on the _referencing_ shape's own bounding box, so a naive per-pattern-element
cache would be incorrect across differently sized referencing shapes), every pattern fill/stroke
re-renders its own tile from scratch - bounded, not by a cache, but by reusing `FilterWorkBudget`
(the same cumulative budget class filter/clip-path/mask application already charges, rather than
a new, parallel budget class) for `1 * (tileArea + regionArea)` work units, charged before either
offscreen buffer (the tile buffer, the region-sized coverage/result buffers) is allocated. A
decline tolerantly falls back to painting nothing - matching `ResolvePaint`'s own
dangling-reference tolerance - never a hard exception, since a pattern is a paint-server, not a
required effect: an over-budget pattern fill degrading to "no paint" is analogous to a
dangling/unsupported paint reference, whereas clip-path/mask's own budget decline instead falls
back to "paint normally, ignoring the effect" (there being no equivalent "ignore the paint
entirely" fallback available for a required fill/stroke color).

#### Element/Group Nesting and Total-Element Bounds

The `use`-cycle guard above only bounds recursion that passes back through a `use` element - it
does nothing to bound a document made entirely of many levels of plain nested `g`/`symbol` groups
(no `use` involved), which would otherwise recurse the element-tree walk without limit and
eventually overflow the call stack with an uncatchable `StackOverflowException`, terminating the
process outright and bypassing this codec's documented `InvalidDataException`-wrapping
error-handling policy entirely. Rendering therefore also tracks a second, independent
recursion-depth counter incremented on every element-tree descent - whether through ordinary group
nesting or through a `use` reference - and raises `InvalidDataException` once it is exceeded,
before descending any further. This mirrors the same fixed-depth-cap pattern the Fonts subsystem's
`GlyfLocaReader` unit uses to bound composite glyph nesting, applied here to bound the SVG element
tree instead.

Neither depth cap bounds _total_ rendering work, only how deep any single reference chain may go.
A group legitimately (non-cyclically) referenced by several sibling `use` elements, itself
containing further such fan-out, re-renders its entire subtree once per reference - so the total
number of elements rendered grows exponentially with nesting depth even while every individual
reference chain stays well within both depth caps. This is the same class of amplification the
Fonts subsystem's `GlyfLocaReader` unit guards against with its total-resolved-component budget: a
depth cap alone does not stop a non-cyclic tree from exploding exponentially when the same
sub-tree is legitimately shared. Rendering therefore also tracks a third, independent counter - the
total number of elements rendered/visited across the whole document walk - charged before each
element is processed further, and raises `InvalidDataException` once it exceeds a fixed budget far
beyond any real-world document's element count but small enough to keep worst-case rendering
CPU/memory bounded to a small, practical amount. Every fixed counter/budget in this class
(`GeometryWorkBudget.Charge`, the total-rendered-element counter above, `BuildIdIndex`'s own
whole-document-walk counter described below) checks the new amount against the remaining budget
_before_ adding it to the running total, rather than adding first and checking afterward - a
single call charging an amount large enough to make the addition itself overflow `int` cannot
therefore bypass the budget by wrapping past a small, still-under-budget-looking value. This is
defense-in-depth: given today's fixed constants, no call site can charge an amount anywhere close
to large enough to threaten an `int` overflow before the very next charge past the real budget
already throws, but the check-before-add ordering remains correct regardless of whether these
constants are ever raised in the future. The cumulative filter-work budget described in
**Filters** above (`FilterWorkBudget.TryCharge`) follows the identical check-before-add pattern,
but - because a filter's own resource cost is always tolerated with a fallback rather than treated
as a hard document-rejection condition, per the existing convention for every other filter
resource bound - reports failure by returning `false` (letting the caller fall back to unfiltered
rendering) rather than throwing.

Even the total-rendered-element budget above does not bound the size of a single element's own
content: it counts how many elements are visited, so one `path`/`polyline`/`polygon`/`text`
element with an extremely large `d`/`points`/text value would otherwise count as only a single
element while parsing or allocating an unbounded amount of geometry or text. Rendering therefore
also tracks a fourth, independent budget - the combined total of path `d` data commands,
points-list coordinate pairs, and text characters parsed across the whole document walk - charged
incrementally as each command/coordinate-pair/character is actually parsed (rather than only once
per element), so a single pathological element throws partway through parsing rather than after
its entire unbounded content has already been scanned. This budget mirrors the Fonts subsystem's
`GlyfLocaReader` unit's own total-resolved-point budget (also `200,000`), the same order-of
magnitude precedent for bounding a single pathological structure's parsing cost, applied here as
an independent combined counter across all three sources rather than one bound per source.

None of the above budgets bound a single _attribute value's_ own length independent of any
element/geometry counting: `ParseNumberList` - the shared parser behind `viewBox`, every
transform function's arguments, and `stroke-dasharray` - previously appended every parsed number
to an unbounded list with no upper bound, and this attribute family is not charged against the
geometry-work budget above. `ParseNumberList` therefore also enforces a fifth, independent,
per-call budget (`10,000` numbers - deliberately an order of magnitude below the total-element/
geometry-work budgets, since this bounds a single attribute value rather than a whole document),
charged the moment each number is added rather than after the list is fully built, and rejects the
excess with `InvalidDataException` - a hard rejection, not a tolerant fallback, matching this
codec's existing convention for every other size/arity/syntax budget violation.

None of the budgets above bound the _magnitude_ of an individual coordinate, only how many of them
appear. A document with only a handful of `path`/`points`/shape-geometry commands using an
extreme-but-individually-finite coordinate magnitude (for example `3e38`) counts as a negligible
amount of parsed work toward every count-based budget above, yet can still drive
`Geometry.BezierFlattening`'s per-curve recursive subdivision, and `Geometry.SvgArcConverter`'s
ellipse-center arithmetic, far out of proportion to that count - a "budget counts parsed units,
not real downstream cost" mismatch. `ParseCoordinate` (the shared parser behind every single-value
geometry/length/opacity attribute) and `TryReadNumber` (the shared parser behind path `d` data,
`points` lists, `viewBox`, and transform-function arguments) therefore both additionally enforce a
sixth, independent bound - a fixed `MaxCoordinateMagnitude` constant (`1,000,000`, roughly 100
times the largest real-world coordinate magnitude any fixture in this repository's test suite
uses) - rejecting a syntactically valid, finite value whose absolute magnitude exceeds it, with
the same `InvalidDataException`/"malformed token" convention already used for a non-finite value
at each site. This closes the flattening-cost mismatch (every coordinate feeding a curve is now
bounded to a magnitude whose square cannot overflow `SvgArcConverter`'s own arithmetic) and, as a
side effect, also makes several earlier rounds' overflow-specific regression tests (which relied
on an individual literal beyond this new bound to reach their own deeper tolerant-skip mechanism)
provably unreachable through any input `Load`/`GetInfo` can be given - see the verification
document's "Coordinate Magnitude Bound" scenario for the full list and the reachability
calculations proving this.

`MaxCoordinateMagnitude`'s enforcement above is entirely **pre-transform**: it bounds a source
literal at parse time, before any `transform` attribute has been applied. `RenderShape` calls
`TransformPath` to bake every shape/glyph-run outline's local-space points into final pixel-space
coordinates through `Vector2.Transform` - but a `transform="scale(...)"` function's own argument
only needs to stay _at or under_ `MaxCoordinateMagnitude` to pass its own parse-time check (the
comparison is strict `>`, so a literal of exactly `1,000,000` is not rejected), so an in-bound
local coordinate composed with an in-bound-but-large transform can still produce a final
pixel-space magnitude the flattening/stroking pipeline was never meant to see - a second,
independent gap from the parse-time one above, since neither individual literal involved is
itself out of bounds. `RenderShape` therefore additionally checks the transformed path's every
point (each subpath's start, and every command's `EndPoint`/`Control1`/`Control2` where
applicable) against the same `MaxCoordinateMagnitude` bound immediately after `TransformPath`,
before entering the fill/stroke pipeline. This single check point uniformly covers every shape
this class renders - plain shapes, text/glyph runs, and arc-converted rounded-rect/ellipse
geometry alike - because arc conversion happens before, and its output is just more path commands
consumed by the same `TransformPath` call. On failure, the whole shape is tolerantly skipped
(fill and stroke both omitted), mirroring `BuildRectPath`/`BuildEllipsePath`'s existing
tolerant-skip convention for an overflowing arc conversion, rather than the parse-time check's hard
`InvalidDataException` rejection: unlike a malformed literal (a document-authoring mistake), a huge
final magnitude can arise from perfectly valid, independently-in-bound inputs composing to a
multiplied extreme, so aborting only the affected shape - not the whole document - is the more
tolerant, consistent choice.

A closely related, but independent, gap exists for stroke width: `RenderStroke` scales the
already-validated, locally-finite `stroke-width` by the same transform's estimated uniform scale,
guarding only `!float.IsFinite(strokeWidth) || strokeWidth <= 0f` - a check that catches an
overflow-to-`Infinity` composed scale, but says nothing about a _finite-but-extreme_ effective
width. A compliant, in-bound `stroke-width` (up to `MaxCoordinateMagnitude`) composed with a
large-but-finite transform scale can still yield an effective width many orders of magnitude
beyond what `PathStroker`'s offset-curve generation was ever meant to see, without ever tripping
the finiteness guard. `RenderStroke`'s guard is therefore extended to also reject when the
post-transform-scaled stroke width exceeds `MaxCoordinateMagnitude`, using the same tolerant-skip
convention (the stroke alone is omitted; the shape's fill, if any, still renders normally).
Reusing one constant for both the pre-transform (source-literal) and post-transform (final
pixel-space geometry and stroke-width) bounds keeps this fix minimal; splitting it into two
distinct constants remains possible later, without any structural change, if evidence emerges that
the two bounds should diverge.

A third, independent gap exists even when `pixelPath`'s coordinates and the effective
`strokeWidth` are both individually in-bound: `stroke-miterlimit` is validated (see
`ParseValidMiterLimit`) only against `Drawing.StrokeStyle`'s documented lower-bound contract
("finite and at least `1`"), never against any upper bound. `Drawing.StrokeOutliner`'s miter-join
synthesis (`TryCreateMiter`) only rejects a candidate miter point whose ratio to half the stroke
width exceeds `StrokeStyle.MiterLimit` - a check about the point's ratio to the stroke width, not
about the point's own absolute magnitude. An in-bound-but-large `strokeWidth` (composing with an
in-bound-but-extreme `stroke-miterlimit`, and a near-straight/near-reversed "spike" vertex whose
interior angle is only a fraction of a degree from a full reversal) can therefore still synthesize
a miter point many orders of magnitude beyond `MaxCoordinateMagnitude`, even though every
individual literal involved - each `pixelPath` coordinate, `strokeWidth`, and the miterlimit -
independently passed its own check. This does not currently cause a hang, crash, or exception (the
rasterizer's clip-bounds intersection with the canvas absorbs the resulting oversized fill
harmlessly), but it is the same class of documented-gap issue the two checks above already close,
so `RenderStroke` re-checks the actual outline `PathStroker.Stroke` synthesizes - not a guessed
upper bound on `stroke-miterlimit` itself - against `MaxCoordinateMagnitude` immediately after
stroking, reusing the same `IsWithinCoordinateMagnitudeBudget` helper `RenderShape` already uses
(since `PathStroker.Stroke` only ever emits `LineTo` commands into its returned outline, that
helper directly applies without modification). On failure, the whole stroke is tolerantly skipped,
the same way an oversized post-transform stroke width already is above.

Finally, every budget above only bounds work `RenderElement` itself performs while walking the
element tree during rendering. `Load` separately calls `BuildIdIndex` **before** rendering begins,
to resolve `href`/`url(#id)` references - and that call walks every element in the whole parsed
document (`root.DescendantsAndSelf()`), independent of and unbounded by any of the rendering-time
budgets above. The same gap also left `ParseStops`'s enumeration of a `linearGradient`/
`radialGradient`'s `<stop>` children unbounded, since gradients are non-rendering elements that
`RenderElement` charges only once for the gradient itself and never recurses into (never counts)
its children at all - a document with an extreme number of never-rendered `<defs>` elements, or an
extreme number of `<stop>` children under one gradient, could bypass every rendering-time budget
entirely. `BuildIdIndex`'s whole-document walk therefore reuses the existing
`MaxTotalRenderedElements` budget, charged per element visited during that walk, closing both gaps
with a single guard - since it runs before rendering and covers every element in the document, it
transitively bounds `ParseStops`'s later, otherwise-unbounded `<stop>` enumeration too.

**Text.** A `text` element's `font-family` is matched, case-insensitively, against a
caller-supplied `fonts` dictionary keyed by family name, walking a comma-separated fallback list
of families exactly as CSS `font-family` does. Once a family name matches, the matching family's
registered `SvgFontFace` list is narrowed to a single face via a private `SelectClosestFace`
helper, scoring each candidate face against the element's own cascaded `font-weight`/
`font-style` (parsed by private `ParseFontWeight`/`ParseFontStyle` helpers alongside every other
presentation attribute in `ApplyPresentationAttributes` - `font-weight` accepts the keywords
`normal`(`400`)/`bold`(`700`) or a literal integer, tolerantly falling back to the inherited value
for the unimplemented relative keywords `bolder`/`lighter` or any other unparseable value;
`font-style` accepts `normal`/`italic`/`oblique`, the latter two both resolving to
`SvgFontStyle.Italic`) in priority order: (1) an exact style match always beats a style mismatch,
regardless of weight; (2) among faces tied on style match, the smallest absolute
`font-weight` distance wins; (3) among faces tied on both, the face on the same "boldness side"
as the request (its own weight and the requested weight are both `>= 400` or both `< 400`) wins
over one on the opposite side. This is a deliberately simple approximation of the CSS Fonts
Module Level 4 font-weight fallback cascade - not a byte-for-byte clone of it - matching a
documented "do not over-engineer" design choice; the first-registered face wins any remaining
tie, since the running best is only replaced by a strictly better-scoring candidate. A caller
registering exactly one `SvgFontFace` per family (including every family registered via the
legacy single-font-per-family `Load` overload, which always wraps its entries as a single
normal-weight/normal-style face) is therefore unaffected by this algorithm: with only one
candidate, it is always selected regardless of the requested `font-weight`/`font-style`,
preserving that overload's pre-existing behavior exactly.

Each mapped character's glyph outline, advance
width, and kerning (against the previous glyph) are looked up via
`Fonts.TrueTypeFont.GetGlyphIndex`/`GetGlyphOutline`/`GetAdvanceWidth`/`GetKerning`, and the
resulting glyph run is offset horizontally according to `text-anchor` before each glyph's outline
is transformed into pixel space and filled. If no `fonts` dictionary is supplied at all, none of
its entries match the requested family (including every entry in a fallback list), or a matching
family's face list is empty, that `text`
element is **silently skipped** — not rendered, and not reported as an error. This is a
deliberate tolerant-parsing policy: font availability is entirely up to the caller, and a
document referencing a family the caller did not supply is not malformed input.

### Error Handling

`SvgCodec` draws a firm line between two categories of problem, and handles each one
differently:

- **Malformed or unparseable input** — the stream is not well-formed XML (`Load` throws
  `System.Xml.XmlException` from its full `XDocument.Load` parse of the whole document, bounded to
  a fixed maximum total character count via `XmlReaderSettings.MaxCharactersInDocument` so an
  unbounded-size stream cannot be fully materialized into an in-memory DOM before any other guard
  gets a chance to run — see the accepted-limitation note below; `GetInfo`
  throws the same exception type from its bounded, root-start-tag-only `System.Xml.XmlReader`
  read), or a value the codec must be able to parse to render anything at all is invalid (a
  `viewBox`/`transform` attribute with the wrong number of components or a non-numeric or
  non-finite (`NaN`/`Infinity`) component,
  `path` `d` data with an unrecognized command letter or missing required arguments, or a
  combined total of path-data commands/points-list coordinates/text characters exceeding the
  fixed geometry-parsing work budget described above). Every one of these is caught (or detected)
  and re-thrown/thrown as `System.IO.InvalidDataException` with a descriptive message naming what
  was invalid, exactly the same contract every other codec in this system uses for malformed
  source data. This throwing behavior is deliberately narrow: a non-finite gradient stop
  `offset`/coordinate, an
  `rgb()`/`rgba()` channel or alpha, or the root `<svg>` element's `width`/`height` fallback tier
  is instead tolerated — treated as absent/unrecognized and resolved via each attribute's own
  documented fallback — because rendering can still proceed meaningfully without that one value,
  unlike a malformed `viewBox`/`transform`/`path` `d`, without which nothing can be rendered at
  all. An invalid `stroke-miterlimit` value (non-finite, or less than `1` - the documented
  contract of the `Drawing.StrokeStyle` it eventually feeds) is a further, separate tolerant case:
  rather than throwing, it falls back to the inherited value, matching this codec's existing
  tolerant handling of a malformed `stroke-dasharray`, since there is no natural "skip the whole
  stroke" operation tied to an invalid miter limit alone the way there is for a non-positive
  `stroke-width`. A negative `radialGradient` `r`/`fr` value (below the documented contract of
  the `Drawing.RadialGradient` it eventually feeds — "finite and greater than or equal to zero")
  is likewise a tolerant case: rather than throwing, it falls back to the same default already
  used for an absent attribute, matching this codec's existing tolerant handling of every other
  gradient coordinate (`cx`/`cy`/`fx`/`fy`/`x1`/`y1`/`x2`/`y2`), since a radius is otherwise
  parsed identically to every other gradient coordinate and only its sign is additionally
  constrained. A composed transform that overflows to a non-finite value across nested
  `transform="scale(...)"` groups (each individual literal finite, but their cross-element product
  not) is likewise a tolerant case: the affected element, and independently an affected gradient's
  own `gradientTransform`/bounding-box composition, is skipped/treated as "no paint" rather than
  reaching a `Drawing`-namespace constructor's own finiteness check and throwing an uncaught
  `ArgumentOutOfRangeException`. A `path` `d` attribute whose relative-coordinate accumulation
  (each command's individually-finite parsed literals summed against the current point), or whose
  `S`/`T` smooth-curve reflection, overflows to a non-finite value is likewise a tolerant case:
  the whole `path` element is skipped (rendered as an empty path, the same no-op `RenderShape`
  already applies to any other empty path) rather than reaching `Drawing.DashSplitter`'s
  dash-interval walk with a non-finite path length, which would otherwise stall its finite-step
  `while` loop indefinitely whenever the affected shape also has a
  `stroke-dasharray`. An extreme-but-individually-finite arc radius (an `A`/`a` path-data command,
  or a `rect`'s rounded-corner/`circle`/`ellipse` quarter-arc construction) whose squared magnitude
  overflows `Geometry.SvgArcConverter`'s internal ellipse-center arithmetic to a non-finite control
  point or endpoint is likewise a tolerant case: each emitted Bezier segment's points are validated
  finite immediately before being appended to the path builder, and the whole affected `path`/
  `rect`/`circle`/`ellipse` element is skipped (rendered as an empty path) rather than propagating a
  raw non-finite value into the rasterizer — the `path`-data case reuses `PathDataParser`'s existing
  `RequireFinite`/`OverflowException` tolerant-skip mechanism, and the `rect`/`circle`/`ellipse`
  shape-builder case adds the same convention (a narrowly-scoped `catch (OverflowException)`
  returning an empty path) at its own, previously entirely unguarded, call site. A
  `stroke-dasharray`/`stroke-dashoffset` that is individually finite as parsed, but overflows to a
  non-finite value once scaled by a composed transform's own scale factor (the same scale
  `stroke-width` is already scaled by), is likewise a tolerant case: rather than reaching
  `Drawing.StrokeStyle`'s constructor and throwing, the scaled dash array/offset fall back to "no
  dashing" (a solid stroke) — a more conservative choice than skipping the whole stroke, since the
  stroke geometry itself remains perfectly valid and only its dash pattern overflowed — mirroring
  `ParseDashArray`'s own existing tolerant "malformed dash array -> no dashing" convention.
- **Well-formed but out-of-scope constructs** — see _Out-of-scope subset_ above. These are
  silently skipped, not errors.

**Accepted limitation: document-size bound is a raw character count, not a streaming parse.**
The `MaxCharactersInDocument` bound `Load` applies to its `XDocument.Load` call stops an
unbounded-size stream from being fully materialized into an in-memory DOM before any other guard
(`MaxTotalRenderedElements`, the total-document-element budget, or the geometry-parsing work
budget) ever gets a chance to run — those guards only execute during the rendering walk that
follows a successful parse. It does **not**, however, make parsing itself incremental: a
well-formed document sized just under this character cap can still fully materialize into memory
before any of those other guards reject a single pathological element's content. Closing this
remaining gap completely would require replacing `XDocument`/`XElement` with a fully streaming
parser — an architectural change out of scope for this bound, which specifically targets the
previously-completely-unbounded "raw document size" dimension.

**Caller-supplied output dimensions are not file data.** `Load`'s `width`/`height` parameters are
ordinary API parameters supplied directly by the caller — the raster size they want the document
fit into — not values decoded from the (untrusted) SVG document itself. `SvgCodec` therefore does
**not** pre-validate or wrap them: they are passed straight through to `new Surface(width,
height)`, and that constructor's own `ArgumentOutOfRangeException` is allowed to propagate
unwrapped. This is a deliberate asymmetry with, for example, `BmpCodec`'s handling of a BMP's
_declared_ width/height (which **is** untrusted file data, and so **is** validated against
`Surface.MaxDimension` and re-thrown as `InvalidDataException`): the two cases look superficially
similar (both end up as `Surface` dimensions) but have different trust boundaries, and this
codec's exception contract reflects that difference rather than collapsing it.

Standard argument validation (`ArgumentNullException` for a null stream/path, `ArgumentException`
for a null/empty/whitespace-only path) happens at the start of each public method, before any XML
parsing is attempted.

### Dependencies

`SvgCodec` depends on `Canvas.Surface` (constructing the destination surface and compositing
filled/stroked pixels into it), on the `Geometry` subsystem's `Path`/`PathBuilder` (assembling
transformed shape/glyph outlines) and `SvgArcConverter` (converting elliptical arc path commands
to Bezier curves in local space before they are transformed — arc math itself is never
reimplemented here), on the `Drawing` subsystem's `PathFiller`/`PathStroker` (rasterizing the
already-transformed, pixel-space paths this codec builds) and `Gradient`/`LinearGradient`/
`RadialGradient`/`GradientStop`/`GradientSpread` (representing resolved gradient paint), and on
the `Fonts` subsystem's `TrueTypeFont` (glyph outline/metrics/kerning lookup for text rendering).
This makes `SvgCodec` the first unit in the `Codecs` subsystem whose dependencies are not limited
to `Canvas.Surface` — see _Codecs Subsystem Design_ (`../codecs.md`). Beyond these in-house
units, `SvgCodec` uses only the .NET base class library's `System.Xml.Linq` (`XDocument`/
`XElement`) and `System.Numerics` (`Matrix3x2`/`Vector2`) namespaces, available on every one of
CanvasNet's target frameworks with no new runtime NuGet dependency.

### Callers

`SvgCodec` is a public API entry point invoked directly by consumers of the CanvasNet package; it
is not called by any other unit within this system. It calls into `Canvas.Surface`, `Geometry`,
`Drawing`, and `Fonts` (see _Dependencies_ above) but nothing calls into it from within CanvasNet
itself.
