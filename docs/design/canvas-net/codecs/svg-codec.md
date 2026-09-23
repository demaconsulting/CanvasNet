## SvgCodec

![Codecs Structure](CodecsView.svg)

<!-- cspell:ignore rasterizing rrggbb sizeless SMIL unparseable Linq uncatchable Glyf Loca -->

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
- `use`, referencing any element by id via `href`/`xlink:href`, with `x`/`y` translation
- `text`, with `x`/`y`, `font-family`, `font-size`, `fill`, and `text-anchor`
  (`start`/`middle`/`end`), rendered through a caller-supplied font dictionary

#### Out-of-scope subset (tolerated, silently skipped)

`style`, `filter`, `mask`, `clipPath`, `pattern`, `marker`, a nested `svg`, `animate`/other SMIL
animation elements, `image`, `foreignObject`, and CSS class/id selectors are all well-formed SVG
constructs this codec does not implement. Encountering one of these does not fail the whole
document: `SvgCodec` silently skips just that element (and, for a container element, everything
nested inside it) and continues walking the rest of the tree. This is a deliberate,
tolerant-parsing policy distinct from the codec's malformed-input rejection policy (see
_Error Handling_ below) — a document using an out-of-scope construct is not itself invalid SVG,
only partially outside this codec's supported feature set.

### Data Model

`SvgCodec` has no public data types of its own beyond the shared `Codecs.ImageInfo` record struct
(see _Codecs Subsystem Design_, `../codecs.md`). Internally, it defines a private `RenderState`
record capturing the cascading presentation state (fill, stroke, fill-opacity, stroke-opacity,
opacity, fill-rule, stroke-width, stroke-linecap, stroke-linejoin, stroke-miterlimit,
stroke-dasharray, stroke-dashoffset, font-family, font-size, text-anchor) that is threaded down
through the element tree alongside an accumulated `System.Numerics.Matrix3x2` transform, plus a
private `RenderContext` capturing fixed per-document state (the id→`XElement` index, the caller's
font dictionary, and the resolved fit transform).

### Key Methods

#### Load(Stream stream, int width, int height, IReadOnlyDictionary\<string, TrueTypeFont\>? fonts = null)

Reads an SVG document from an open stream and rasterizes it into a new `width`x`height`
`Surface`. Parses the document with `XDocument.Load`, builds an id→`XElement` index over the
whole tree up front (so `use`/`href`/gradient-template references resolve correctly regardless of
document order), resolves the root `viewBox`/`width`/`height` into an intrinsic size, computes
the "meet, centered" fit transform into the requested raster (see _ViewBox Fitting Policy_
below), then recursively walks the tree, baking every transform (the root fit transform composed
with every nested `g`/element `transform`) directly into the `Vector2` points fed into
`Geometry.PathBuilder` before calling `Drawing.PathFiller.Fill`/`Drawing.PathStroker.Stroke` —
neither of which has a transform parameter; they treat `Geometry.Path` coordinates as final
pixel-space. `fonts` is optional; when supplied, `text` elements are matched against it by
family name (see _Text Rendering and Font Lookup_ below).

**Throws:**

- `ArgumentNullException` — `stream` or `fonts` is null (`fonts` is nullable overall, but a
  non-null reference containing a null entry is rejected the same way any other null-dictionary
  misuse would be)
- `InvalidDataException` — the stream is not well-formed XML, or the document's `viewBox`,
  `transform`, gradient, or path `d` data is malformed (see _Error Handling_ below)
- `ArgumentOutOfRangeException` — `width` or `height` is not positive (propagated, unwrapped,
  from `Surface`'s own constructor — see _Error Handling_ below)

#### Load(string path, int width, int height, IReadOnlyDictionary\<string, TrueTypeFont\>? fonts = null)

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream, int, int, ...)`.

**Throws:**

- `ArgumentNullException` — `path` is null
- `ArgumentException` — `path` is empty or consists only of whitespace
- `InvalidDataException` / `ArgumentOutOfRangeException` — see `Load(Stream, int, int, ...)`
- Underlying file-system exceptions propagate uncaught

#### GetInfo(Stream stream)

Reads only the root `svg` start-tag's own attributes, using a forward-only `System.Xml.XmlReader`
that advances no further than the root element's attributes and never walks into the document
body. Resolves the intrinsic size per the three-tier fallback policy described in _GetInfo
Fallback Policy_ below. This is intentionally narrower than `Load`'s full `XDocument.Load` parse
of the whole document: a document that is malformed only beyond the root `svg` element's own
attributes is accepted by `GetInfo` (which never reads that far) even though `Load` would reject
it. Always reports `Channels = 4` and `HasAlpha = true`, because every SVG document this codec
rasterizes produces an RGBA `Surface` regardless of what the source markup does or does not
paint.

**Throws:**

- `ArgumentNullException` — `stream` is null
- `InvalidDataException` — the stream is not well-formed XML up to and including the root
  start-tag, its root element is not named `svg`, or its `viewBox` attribute is present but
  malformed

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
That intrinsic size is then fit into the caller-requested raster using a "meet, centered"
transform — the equivalent of CSS `object-fit: contain` or SVG `preserveAspectRatio="xMidYMid
meet"` — scaling uniformly by the smaller of the width and height ratios and centering the result
in the raster, leaving transparent letterbox bars along whichever axis the intrinsic aspect ratio
does not fill. This is the **only** fitting behavior `SvgCodec` implements; the
`preserveAspectRatio` attribute itself is never parsed or read, so a document that requests a
different alignment or a non-uniform ("slice"/"none") fit is still fit as "meet, centered."

### GetInfo Fallback Policy

`GetInfo` (and `Load`, internally, for the same purpose) resolves a document's size in three
tiers, in order: (1) the `viewBox` attribute, if present; (2) the `width`/`height` attributes, if
both are present and resolve to positive numbers; (3) the CSS/UA default replaced-element
intrinsic size of 300x150, if neither of the above is present. This mirrors how a web browser
sizes a sizeless, viewBox-less `<img src="...svg">` element, and ensures `GetInfo` always returns
a usable, positive size rather than failing on a document that omits explicit sizing information
entirely.

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
within the same semi-transparent group.

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
simplification of the full SVG href-inheritance model.

**Use.** A `use` element referencing any element by id via `href`/`xlink:href` renders a copy of
the referenced element (translated by the `use` element's own `x`/`y`), resolved through the
document-order-independent id index described above. A `use` referencing a nonexistent id is
tolerated as a silent no-op. Because `use` can reference another `use` (directly or through
intervening groups), rendering guards against unbounded mutual recursion with a fixed maximum
recursion depth, raising `InvalidDataException` if it is exceeded rather than recursing
indefinitely - see **Element/Group Nesting and Total-Element Bounds** below for why this depth
cap, on its own, does not bound every form of unbounded rendering work.

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
CPU/memory bounded to a small, practical amount.

**Text.** A `text` element's `font-family` is matched, case-insensitively, against a
caller-supplied `fonts` dictionary keyed by family name, walking a comma-separated fallback list
of families exactly as CSS `font-family` does. Each mapped character's glyph outline, advance
width, and kerning (against the previous glyph) are looked up via
`Fonts.TrueTypeFont.GetGlyphIndex`/`GetGlyphOutline`/`GetAdvanceWidth`/`GetKerning`, and the
resulting glyph run is offset horizontally according to `text-anchor` before each glyph's outline
is transformed into pixel space and filled. If no `fonts` dictionary is supplied at all, or none
of its entries match the requested family (including every entry in a fallback list), that `text`
element is **silently skipped** — not rendered, and not reported as an error. This is a
deliberate tolerant-parsing policy: font availability is entirely up to the caller, and a
document referencing a family the caller did not supply is not malformed input.

### Error Handling

`SvgCodec` draws a firm line between two categories of problem, and handles each one
differently:

- **Malformed or unparseable input** — the stream is not well-formed XML (`Load` throws
  `System.Xml.XmlException` from its full `XDocument.Load` parse of the whole document; `GetInfo`
  throws the same exception type from its bounded, root-start-tag-only `System.Xml.XmlReader`
  read), or a value the codec must be able to parse to render anything at all is invalid (a
  `viewBox`/`transform` attribute with the wrong number of components or a non-numeric or
  non-finite (`NaN`/`Infinity`) component, or `path` `d` data with an unrecognized command letter
  or missing required arguments). Every one of these is caught (or detected) and re-thrown/thrown
  as `System.IO.InvalidDataException` with a descriptive message naming what was invalid, exactly
  the same contract every other codec in this system uses for malformed source data. This
  throwing behavior is deliberately narrow: a non-finite gradient stop `offset`/coordinate, an
  `rgb()`/`rgba()` channel or alpha, or the root `<svg>` element's `width`/`height` fallback tier
  is instead tolerated — treated as absent/unrecognized and resolved via each attribute's own
  documented fallback — because rendering can still proceed meaningfully without that one value,
  unlike a malformed `viewBox`/`transform`/`path` `d`, without which nothing can be rendered at
  all.
- **Well-formed but out-of-scope constructs** — see _Out-of-scope subset_ above. These are
  silently skipped, not errors.

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
