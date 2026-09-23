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

A percentage value on a shape/text geometry attribute (`x`, `y`, `width`, `height`, `rx`, `ry`,
`cx`, `cy`, `r`, `x1`/`y1`/`x2`/`y2`, `font-size`, `stroke-width`, `stroke-miterlimit`,
`stroke-dashoffset`, `use`'s `x`/`y`, and `text`'s `x`/`y`) is also out of scope, but is handled
differently from the constructs above: `SvgCodec` has no defined viewport-relative basis to
resolve such a percentage against, so rather than being tolerated/silently skipped, it is
explicitly **rejected** with `InvalidDataException` (see _Error Handling_ below) — opacity-family
attributes and gradient coordinates/`stop` `offset` are unaffected, since both have a well-defined
`[0, 1]`-fraction basis this codec already resolves correctly.

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

Opens `path` as a read-only `FileStream` and delegates to `Load(Stream, int, int, ...)`.

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
That intrinsic size is then fit into the caller-requested raster using a "meet, centered"
transform — the equivalent of CSS `object-fit: contain` or SVG `preserveAspectRatio="xMidYMid
meet"` — scaling uniformly by the smaller of the width and height ratios and centering the result
in the raster, leaving transparent letterbox bars along whichever axis the intrinsic aspect ratio
does not fill. This is the **only** fitting behavior `SvgCodec` implements; the
`preserveAspectRatio` attribute itself is never parsed or read, so a document that requests a
different alignment or a non-uniform ("slice"/"none") fit is still fit as "meet, centered."

An intrinsic size that is positive and finite (passing the checks above) can still be small
enough — a subnormal float, for example — that dividing the requested raster dimensions by it
overflows the computed scale to a non-finite value. `Load` validates the resulting fit transform
with the same finiteness check used for composed element transforms immediately after computing
it, and rejects it with `InvalidDataException` rather than silently proceeding: an unvalidated
non-finite fit transform would otherwise cause every element in the document to fail that same
per-element finiteness check and render a blank, fully-transparent surface with no exception at
all — a worse "quiet" failure than the sibling non-positive-size case already throws for.

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
CPU/memory bounded to a small, practical amount. Every fixed counter/budget in this class
(`GeometryWorkBudget.Charge`, the total-rendered-element counter above, and `BuildIdIndex`'s own
whole-document-walk counter described below) checks the new amount against the remaining budget
*before* adding it to the running total, rather than adding first and checking afterward - a
single call charging an amount large enough to make the addition itself overflow `int` cannot
therefore bypass the budget by wrapping past a small, still-under-budget-looking value. This is
defense-in-depth: given today's fixed constants, no call site can charge an amount anywhere close
to large enough to threaten an `int` overflow before the very next charge past the real budget
already throws, but the check-before-add ordering remains correct regardless of whether these
constants are ever raised in the future.

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
  `System.Xml.XmlException` from its full `XDocument.Load` parse of the whole document, bounded to
  a fixed maximum total character count via `XmlReaderSettings.MaxCharactersInDocument` so an
  unbounded-size stream cannot be fully materialized into an in-memory DOM before any other guard
  gets a chance to run — see the accepted-limitation note below; `GetInfo`
  throws the same exception type from its bounded, root-start-tag-only `System.Xml.XmlReader`
  read), or a value the codec must be able to parse to render anything at all is invalid (a
  `viewBox`/`transform` attribute with the wrong number of components or a non-numeric or
  non-finite (`NaN`/`Infinity`) component, a percentage value on a shape/text geometry attribute
  (`x`, `y`, `width`, `height`, `rx`, `ry`, `cx`, `cy`, `r`, `x1`/`y1`/`x2`/`y2`, `font-size`,
  `stroke-width`, `stroke-miterlimit`, `stroke-dashoffset`, `use`'s `x`/`y`, and `text`'s `x`/`y`),
  `path` `d` data with an unrecognized command letter or missing required arguments, or a
  combined total of path-data commands/points-list coordinates/text characters exceeding the
  fixed geometry-parsing work budget described above). Every one of these is caught (or detected)
  and re-thrown/thrown as `System.IO.InvalidDataException` with a descriptive message naming what
  was invalid, exactly the same contract every other codec in this system uses for malformed
  source data. A shape/text geometry attribute's percentage is rejected rather than resolved,
  because `SvgCodec` has no defined viewport-relative basis to resolve it against - unlike
  opacity-family attributes and gradient coordinates/`stop` `offset`, which correctly treat a
  percentage as a `[0, 1]` fraction of their own well-defined basis and are unaffected by this
  rejection. This throwing behavior is deliberately narrow: a non-finite gradient stop
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
  `while` loop indefinitely whenever the affected shape also has a   `stroke-dasharray`. An extreme-but-individually-finite arc radius (an `A`/`a` path-data command,
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
