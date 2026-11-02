### ScadTextGeometry

<!-- cspell:ignore notdef Liberation kerning kerned flattening unioned subpath subpaths Bezier Casteljau ttb btt -->
<!-- cspell:ignore rtl ltr glyphs glyph unscaled halign textmetrics fontmetrics -->

The `ScadTextGeometry` unit is the fifth software unit in the `ScadGeometry` subsystem. It comprises three source
files in the `DemaConsulting.CanvasNet.Scad.Geometry` namespace, all `internal`, and uses one public options type
in the `DemaConsulting.CanvasNet.Scad` namespace:

- `ScadTextGeometry.cs` — `ScadTextGeometry` (static partial class): `Build` (the `text` module), argument
  validation, the layout of the glyphs, alignment, and the construction of the 2D shape
- `ScadTextGeometry.Flattening.cs` — the reference height of a font, and the iterative flattening of glyph outlines
  into polygon contours
- `ScadFontResolver.cs` — `ScadFontResolver`: parsing of the `font` argument and the translation of a request
  into a call of the core `FontResolver` (owned by the build context, one per build, built with the cancellation
  token of the build), with `ScadFontSpec` and `ScadResolvedFont` (whose source is the core `FontSource`)
- `ScadFontResolutionOptions.cs` (namespace `DemaConsulting.CanvasNet.Scad`) — the public options type that
  `ScadRenderOptions.FontResolution` carries

The name of the unit is `ScadTextGeometry` and not `ScadText` because the evaluation subsystem already has an
internal `ScadText` helper (Unicode-scalar string helpers); the two are unrelated.

#### Purpose

`ScadTextGeometry` builds the `text(text, size, font, halign, valign, spacing, direction, language, script)` module:
it lays a string out with a TrueType font and turns the glyph outlines into flat 2D regions. Text is a
`ScadShape2D` like a `square` is (see _ScadExtrusions Unit Design_), so transforms, 2D booleans, `hull`,
`linear_extrude`, `rotate_extrude`, `color`, the modifiers, and the slab of a top-level 2D shape all accept it
without special cases. The unit exists separately from the primitives because it needs font resolution, layout,
curve flattening, and a policy for outlines that cannot be represented, each with its own failure modes.

The dialect decisions of the unit are original decisions of the CanvasNetScad dialect (see _CanvasNetScad System
Design_, Clean-Room Provenance), pinned by tests; no OpenSCAD source, test, or manual text was consulted, and no
parity with any other implementation's text metrics is claimed. The behavior is specified by the
`CanvasNetScad-ScadGeometry-ScadTextGeometry-*` requirements (`Arguments`, `FontResolution`, `Size`, `Layout`,
`Alignment`, `Glyphs`, `Contours`, `Flat2D`, `Invalid`, `Budget`, `Cancellation`, `FontResolutionSafety`, and
`Determinism`) in
`docs/reqstream/canvas-net-scad/scad-geometry/scad-text-geometry.yaml`.

#### Data Model

- **`Request`** — the validated arguments of one `text`: the string, the size, the spacing, the horizontal
  alignment (−1 left, 0 center, 1 right), the vertical alignment (−1 bottom, 0 center, 1 top, 2 baseline), the
  `Direction` (`LeftToRight`, `RightToLeft`, `TopToBottom`, `BottomToTop`), and the `ScadFontSpec`.
- **`ScadFontSpec`** — a family name (empty for the default font) and the requested bold and italic flags.
- **`ScadResolvedFont`** — the `TrueTypeFont`, its core `FontSource` (`Bundled`, `AdditionalDirectory`, `System`),
  and the warning to report, if any.
- **`ScadFontResolver`** — per build: one core `FontResolver` built from `ScadFontResolutionOptions`
  (`UseSystemFonts`, `AdditionalDirectories`, `MapCommonAliasesToBundled: false`), the cancellation token of the
  build and the scan bounds (the core `FontScanLimits`, defaults unless a test passes others). The core resolver
  keeps the resolutions already made, scans the additional directories at most once (header-only: no parsed font
  is retained by the scan) and loads a face only when it is selected.
- **`Placed`** — one glyph placed on the line: its flattened contours in model units, before alignment.
- The result is an ordinary `ScadGeo`: a single `PolygonRegion` leaf, or a `ScadShape2DKind.Csg` union of leaves
  (one per glyph component) with the tight bounds of all vertices.

#### Key Methods

##### Build(node, context)

1. A child object is a warning (a `text` has no use for children), and the arguments are bound to the documented
   parameter names in the order `text, size, font, halign, valign, spacing, direction, language, script`.
2. `ReadRequest` validates the arguments. `text` is required; a string is used as is, any other value (except a
   function) is converted the way `str()` converts it. `size` is a finite number greater than zero (default 10);
   `spacing` is a finite number that is not negative (default 1); `halign` is `left`, `center`, or `right`
   (default `left`); `valign` is `baseline`, `bottom`, `center`, or `top` (default `baseline`); `direction` is
   `ltr`, `rtl`, `ttb`, or `btt` (default `ltr`); the keywords are case-insensitive; `font` is a string.
   `language` and `script` are bound and ignored whatever their value. Every violation is one located warning
   that leaves the instance empty (the convention of every primitive). An empty string is empty and silent.
3. Cancellation is polled, and the font is resolved (see below); a font warning is reported at the instance.
4. `Layout` places the glyphs (see below), then `MakeShape` aligns them and builds the regions.

##### Size

The scale from font design units to model units is `scale = size / referenceHeight`. The **reference height** of a
font is the ink height of the glyph `H`, measured on its outline flattened with a fixed fine tolerance
(`unitsPerEm / 4096`), so that an upper case `H` is exactly `size` tall on the baseline. When the font has no
usable `H` (not mapped, no outline), the reference height is the larger of the absolute `Ascender` and
`unitsPerEm`, and it is never zero. The measurement polls cancellation before it starts and after every flattened
subpath, so a canceled build does not wait for the reference glyph of a hostile font. This is a deterministic
per-font cap-height rule and the documented
approximation of the way other tools relate a text size to a font; it is not a claim of identical sizes. For the
bundled Liberation Sans Regular (2048 units per em) the `H` is 1409 units high, so `size = 14.09` makes one font
unit exactly 0.01 model units (a value pinned by a system test).

**Known limit.** The reference glyph is flattened with the fixed fine tolerance, while the output glyphs are
flattened with the tolerance derived from the fragment rule, which depends on `size` and so on the scale itself.
Using one basis for both would make the scale circular, so the unit keeps two bases. The `H` of all twelve bundled
faces is drawn with straight lines only (checked by a test), so its flattening does not depend on the tolerance and
an `H` of a bundled font is exactly `size` tall at every `$fn`, `$fa`, and `$fs`. An `H` with curved edges in a font
supplied through an additional directory (or an installed font) can come out slightly shorter than `size` (by at
most the flattening tolerance, about 1% of the size under the default fragment settings, and only when the curve
peak is not an on-curve vertex), and the result is still deterministic.

##### Layout

Glyphs are placed one Unicode scalar at a time (`string.EnumerateRunes`; an unpaired surrogate is U+FFFD). There
is no shaping, no ligature, no combining-mark handling, and no bidirectional or Arabic shaping.

- **Characters.** A control character (`Rune.IsControl`) is skipped silently. Every other scalar is mapped through
  the font's `cmap` (`GetGlyphIndex`); an unmapped scalar is glyph 0, `.notdef`, which is drawn like any glyph (a
  box in the bundled fonts). If `.notdef` has no outline, the character is skipped (it does not advance the pen)
  and one warning at the instance lists the first eight missing scalars as `U+XXXX`. A glyph with no outline (a
  space) advances the pen without drawing.
- **`ltr`.** The pen starts at 0. For each glyph after the first, the pen first moves by
  `kerning(previous, current) * scale` (the font's `kern` table; no kerning without a table), the glyph is placed
  with its origin at the pen, and the pen then moves by `advance(current) * scale * spacing`. Kerning is a font
  metric and is not multiplied by `spacing`.
- **`rtl`.** The scalar sequence is reversed and laid out exactly as `ltr`: the text `"HI"` with `rtl` is the
  layout of `"IH"`.
- **`ttb` and `btt`.** Upright glyphs are stacked along Y: the first glyph stands on the baseline (Y = 0), and the
  pen moves by `advance * scale * spacing` downwards (`ttb`, the pen decreases) or upwards (`btt`) after each
  glyph; there is no kerning and the X origin of every glyph is 0.

##### Alignment

After layout, the minimum and maximum X and Y of all flattened vertices are measured (the **ink extents**, not
the advance box). `halign` moves the minimum X to 0 (`left`, the default), the middle of the X extent to 0
(`center`), or the maximum X to 0 (`right`); `valign` moves nothing (`baseline`, the default), the maximum Y to 0
(`top`), the middle of the Y extent to 0 (`center`), or the minimum Y to 0 (`bottom`). The default `halign` is
therefore ink-flush with X = 0, and the baseline is Y = 0 unless `valign` says otherwise. A coordinate that is
not finite or exceeds one quarter of `ScadGeo.MaxCoordinate` before alignment is a warning (the text is too
large) and the instance is empty.

##### Flattening

`TryFlatten` reads the glyph outline (`TrueTypeFont.GetGlyphOutline`, a `Geometry.Path` in font design units, y
up) and flattens every subpath into a polygon: lines are copied, quadratic curves are converted to cubics, and
cubic curves are flattened by **iterative de Casteljau subdivision** with an explicit stack (first half before
second half, so the order is fixed): a curve is emitted as its end point when the larger distance of its two
control points from its chord **segment** (the finite segment from the start to the end, so a control point that
lies on the chord line beyond an end point still counts and a curve that overshoots its chord keeps the overshoot)
is at most the tolerance, or when the subdivision depth reaches ten (at most 1,024
segments per curve, a bound on the work of a hostile outline). The tolerance in model units is
`(size / 2) * (1 - cos(pi / n))` with `n = max(3, fragments(size / 2))`, the sagitta of a circle of radius
`size / 2` under the instance's `$fn`, `$fa`, and `$fs` (the fragment rule of _ScadPrimitives Unit Design_), and
it is divided by the scale to give the tolerance in font units; the default fragment settings give about 1% of
the size for a size of 10. Consecutive duplicate vertices and a closing duplicate are dropped; a subpath with
fewer than three vertices or no area is dropped; an unreadable glyph (`InvalidDataException`) is reported as
`scad-text-invalid-outline`. A glyph that has drawing subpaths but keeps none of them (every subpath dropped) is
**degenerate**, which is not the same as an empty outline: a mapped degenerate glyph is reported as
`scad-text-invalid-outline` and omits the whole text, whereas an empty outline (a space) just advances the pen
(`.notdef` keeps its skip-with-a-warning behavior). Cancellation is polled after every flattened subpath. The
flattened contours of a glyph are cached per glyph index for the instance
and are moved to their pen position by `Place`, which allocates new arrays.

##### Contours

`TryMakeRegions` makes the regions of one glyph (in model units, after alignment) with `PolygonRegion`, which
normalizes the nesting parity (solid contours counter-clockwise, holes clockwise, whatever the font drew) and
rejects contours that cross or touch. Fonts draw some composite glyphs (an `A` with a ring, a `C` with a
cedilla) as overlapping solids, which the nonzero fill rule reads as their union, so:

1. If the whole glyph is accepted, it is one region (the ordinary case: holes and islands by nesting).
2. Otherwise each contour is tried alone; a contour that is invalid alone makes the glyph an invalid outline
   (`scad-text-invalid-outline`).
3. Otherwise the glyph is split into components: the largest contour is an outer boundary and its winding is the
   winding of solids; the contours with that winding are solids, the others are holes, every hole goes to the
   smallest solid that contains one of its points, and each solid with its holes is one region. The regions are
   unioned by the caller. Holes that fall in no solid make the split fail.
4. If a component is still rejected, the contours overlap in a way that is neither a hole, an island, nor
   overlapping solids: `scad-text-overlapping-contours`.

**Known limit: touching solids.** The split does not tell overlapping solids from solids that merely touch (a
zero-area contact at a vertex or along an edge): every contour that winds like the largest one is a solid, and
solids that touch are accepted as separate regions, exactly like the glyph components that touch one another
(kerned pairs, `spacing = 0`). Detecting the contact here would need a polygon intersection of its own and would
reject glyphs that fonts really draw this way (a cedilla or an accent that touches its base), omitting the whole
text. The contact is instead left to the union that consumes the leaves: an extrusion builds one closed mesh per
region, and a union of meshes whose contact is not a valid solid is reported by the renderer through the mapped
`scad-csg-*` identifiers (for example `scad-csg-non-manifold-edge`, see _ScadGeometryBuilder Unit Design_) at the
producing module. The same holds for touching glyphs.

An invalid glyph omits the **whole** `text` (a partial word would silently change the meaning of what is drawn)
with an unsupported-feature report at the instance; the check of all 12 bundled faces over Basic Latin and
Latin-1 shows that only the overlapping-solid composites (`Å`, `Ç`, `ç`) need the split and none is rejected.

##### Assembling the result

A single region is a `ScadGeo.FromRegion`; several are the union of leaves (`ScadShape2D.CreateCsg(Union, ...)`,
every leaf with the location of the instance) with the tight bounds of all region vertices. Using one leaf per
glyph component means that each glyph component is a valid region by itself; glyphs that touch or overlap one
another (kerned pairs, italics, `spacing = 0`) are not checked per region: contact between solids is left to
the union, which can report `scad-csg-*`. A consumer that distributes over the tree (an extrusion) builds one
closed mesh per glyph component.

##### Font resolution (`ScadFontResolver`)

`ParseSpec` splits the `font` argument at colons: the trimmed text before the first colon is the family; in the
following `key=value` fields, `style` (any case) with the words `Bold`, `Italic`, and `Oblique` (any case, split at
white space and commas) sets the flags; everything else is ignored. `Resolve` spells the OpenSCAD family names `sans`
and `mono` as the generic keywords `sans-serif` and `monospace` and hands the family and the flags to the core
`FontResolver` (see _FontResolver Unit Design_), which follows a fixed order and never fails; the resolver here owns
only the translation and the warning text:

1. **No family** (no `font` argument, or an empty family): the bundled Liberation Sans in the requested style,
   whatever fonts the host has, silently. This is what makes text without a `font` reproducible.
2. **The generic names** (`sans-serif`, `sans`, `serif`, `monospace`, `mono`, and the other CSS generic keywords):
   the bundled Liberation font of that design, silently. They are matched **before** the additional directories and
   the installed fonts (CSS reserves these names), so a font family literally named `sans`, `serif`, or the like in a
   font directory is never used; this is a deliberate change from the earlier resolver, which searched the
   directories first. The well-known names of other systems (`Arial`,
   `Times New Roman`, ...) are **not** mapped (`MapCommonAliasesToBundled` is `false`): they are looked up like any
   other family, and an unknown one is reported.
3. **The additional directories** (`ScadFontResolutionOptions.AdditionalDirectories`), scanned once per build, only
   when a family is named, by the core `FontDirectoryScanner`: each directory in the order given is searched
   recursively for `.ttf`, `.otf`, `.ttc`, and `.otc` files (a directory's own font files, in alphabetical order,
   before its subdirectories), reading only the font headers, so the result never depends on the order the file
   system lists files in and no parsed font is retained by the scan. A selected face is loaded when it is selected
   (a face of an additional directory is loaded when it is selected and kept by the resolver of the build only, so a
   file removed after the scan falls back to the bundled
   sans font with the warning. A missing, malformed, or unreadable directory, a file that does not parse, and a face
   that does not parse are skipped without a diagnostic. The scan is **safe and bounded**: a link (reparse point)
   is never followed, whether it is a root, a subdirectory, or a font file (a root that is a link is not scanned, and
   a linked font file is not read); path, metadata, enumeration, and access failures skip that path or directory
   only; and the default `FontScanLimits` (10,000 directories, 200,000 entries, 5,000 font files, and 20,000 faces,
   all far above a real font collection) stop the scan at the first bound reached, keeping the faces found before
   it. The scan polls the cancellation token of the build (the resolver is constructed by `ScadGeometryContext` with
   it), so a canceled build stops with `OperationCanceledException` instead of completing the scan.
4. **The installed fonts** when `UseSystemFonts` is `true` (the default): the exact family among
   `SystemFontCatalog.Fonts`, which scans the host's font directories at most once per process and only when a
   model asks for a font that step 3 did not find. This lookup is **not cancellable** (`SystemFontCatalog` takes no
   cancellation token), and the first lookup in the process builds the process-wide catalog, so that first call can
   take noticeable time even for a build that is canceled meanwhile; later lookups, in any build, reuse the catalog.
5. **The bundled family names**: `Liberation Sans`, `Liberation Serif`, and `Liberation Mono`, in the requested style.
6. **Fallback**: the bundled sans font in the requested style, with the warning that the font is not available.

Steps 3 and 4 accept **only an exact family match** (ignoring case): the generic-bucket substitution of the
catalog's `FindBestMatch` is not used, because it would make the result depend on what the host has installed. The
style is the closest match by the bold and italic flags (the number of differing flags is the score); the first
face found wins a tie, so earlier directories and earlier paths win. A face that is found but cannot be loaded
resolves to the fallback with a warning that names the file (a family that was found spends its turn: the installed
fonts and the bundled names are not tried for it). The warning text is this unit's own: `The font '<family>' is not
available (<reason>); using the bundled Liberation Sans font`, where the reason (`no such font family was found`, or
`the font file '<path>' could not be loaded`) is the `failure` the core resolver reports. Installed fonts vary from
host to host, so a model that must render identically everywhere names no font, or sets `UseSystemFonts = false`.
The family text is read as a CSS family list by the core resolver, so a comma-separated list is tried in order and
quotes around a name are dropped.

##### ScadFontResolutionOptions

A public, immutable options type: `UseSystemFonts` (default `true`) and `AdditionalDirectories` (default empty; a
copy is taken when the list is set and exposed read-only; a `null` list is an `ArgumentNullException` and a
`null`, empty, or white-space directory an `ArgumentException`). Missing directories are tolerated by the scan.
`ScadRenderOptions.FontResolution` carries it (`null` means the defaults) and `ScadDocument` passes it to the
geometry builder in `ScadGeometrySettings.FontResolution`; the builder's context creates the resolver on first use,
so a model without `text` pays nothing.

#### Error Handling

- **Warnings** (instance empty): the invalid arguments above, a text that is too large, and (instance kept) an
  unknown argument, a surplus argument, a repeated argument, and a child object; also the font warning (the
  bundled sans font is used) and the missing-glyph warning (those characters are skipped).
- **Unsupported features** (the whole `text` omitted): `scad-text-invalid-outline` (a damaged outline, or a mapped
  glyph whose subpaths all lack an area) and
  `scad-text-overlapping-contours`; in strict mode the first of them throws `ScadUnsupportedFeatureException`.
  `text` itself is never reported as unsupported.
- **Errors**: `ScadEvaluationException` with the instance's location when the lower bound of the slab of the
  vertices placed so far (`8V / 3` triangles, checked after every glyph, before any region exists) does not fit
  the triangle budget. The exact charging of the slab or extrusion belongs to `ScadExtrusions` and works on the
  returned shape like for any other 2D shape.
- **Cancellation**: `OperationCanceledException` (never wrapped), polled before the font is resolved and the text
  laid out, while the font directories are scanned, while the reference glyph is flattened, after every glyph,
  after every flattened contour, and before every glyph's regions are built.

`textmetrics()` and `fontmetrics()` stay recognized-but-unsupported evaluator functions (see _ScadBuiltinFunctions
Unit Design_); they are not part of this unit.

#### Dependencies

`ScadTextGeometry` depends on, within the subsystem, `ScadArguments` (binding and typed readers),
`ScadFragmentRule` (the flattening tolerance), `ScadPrimitives.SlabTrianglesLowerBound` (the budget check),
`ScadGeo` and `ScadShape2D` (the result), `ScadGeometryContext` (reporting, budget, cancellation, and the
resolver), and `ScadFeatureIds`; on the evaluation subsystem for `ScadInstance` and `ScadValue`; on
`CanvasNetRender3D` for `PolygonRegion`, `Vector2D`, `CsgOperation`, and `BoundingBox3D`; and on the core
`CanvasNet` system, through its public API only (no `InternalsVisibleTo` and no core API was added for it), for:

- the **`Fonts` subsystem**: `FontResolver` and `FontResolverOptions` (the lookup of the font that a `font`
  argument names, with its `ResolvedFont`, `FontSource` and failure reason), `FontScanLimits` (the scan bounds),
  `TrueTypeFont` (`GetGlyphIndex`, `GetGlyphOutline`, `GetAdvanceWidth`, `GetKerning`, `UnitsPerEm`, `Ascender`,
  `IsBold`, `IsItalic`) and `SystemFontCatalog` (`LoadBundledFallback`); and
- the **`Geometry` subsystem**: `Path`, the outline that `GetGlyphOutline` returns and that the flattening reads.

It uses no platform UI API and no native font library.

#### Callers

`ScadGeometryBuilder` dispatches the `text` module to `ScadTextGeometry.Build` through its leaf-module table (a
leaf: its children are never visited). The result is consumed by every 2D consumer: `ScadBooleans`,
`ScadTransforms`, `ScadHulls`, `ScadExtrusions` (including `MakeSlabs`), and `ScadColors`.

#### Determinism

For the same text, options, and fonts the vertices are identical on every call, thread, and platform: the
flattening order is fixed, nothing depends on time, randomness, or culture (`string.Create` with
`CultureInfo.InvariantCulture` is used for the messages), and the default font is the bundled one. Only an
explicitly named font that is found among the installed fonts depends on the host.
