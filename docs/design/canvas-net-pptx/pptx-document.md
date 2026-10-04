## PptxDocument

![CanvasNetPptx Structure](CanvasNetPptxView.svg)

<!-- cspell:ignore ooxml pptx navigations hlink srgb xfrm prst cust fmla scrgb patt misrender -->
<!-- cspell:ignore bodyPr pPr rPr txBody txStyles lstStyle defRPr ctrTitle lIns tIns rIns bIns -->
<!-- cspell:ignore algn marL lnSpc spcBef spcAft justLow lnSpcReduction fontScale noAutofit -->
<!-- cspell:ignore normAutofit spAutoFit spcPct spcPts -->

`PptxDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pptx` NuGet package
(namespace `DemaConsulting.CanvasNet.Pptx`), which references the core `DemaConsulting.CanvasNet`
package.

The `PptxDocument` class is the sole software unit of the `CanvasNetPptx` system. As of Phase 1a,
its dependencies were limited to the .NET base class library's `System.IO.Compression.ZipArchive`
(reading the `.pptx` ZIP container) and `System.Xml.Linq` (parsing `[Content_Types].xml` and each
`.rels` relationships part) — no `CanvasNet` core-system type was used yet. As of Phase 1c, the
unit additionally depends on several core `DemaConsulting.CanvasNet` types - `Geometry.Path`/
`PathBuilder`, `Drawing.PathStroker`/`StrokeStyle`, and `Canvas.Rgba32`/`Gradient`/
`LinearGradient` - reusing the already-tested core geometry/drawing machinery rather than
reimplementing it in this package (see _CanvasNetPptx System Design_'s Dependencies section,
`../canvas-net-pptx.md`, and the _Geometry and Paint (Phase 1c)_ section below). As of Phase 1d
(this release), the unit additionally depends on core `Fonts.TrueTypeFont`/`SystemFontCatalog`
(font resolution and glyph-outline extraction) and `Drawing.PathFiller` (glyph-ink fill), reusing
the same font-resolution/glyph-painting pattern `DemaConsulting.CanvasNet.Pdf` already established
(see the _Text Layout and Rendering (Phase 1d)_ section below).

### Package Layer (Phase 1a)

A `.pptx` file is an OOXML (Office Open XML) package: a ZIP archive whose parts are discovered by
following a chain of relationships, starting from the package root. `PptxDocument.Package.cs`
implements this generic package layer, independent of any presentation-specific semantics:

- **Opening the ZIP archive**: the private constructor buffers the entire input stream into a
  byte array, wraps it in a non-writable `MemoryStream`, and opens that stream as a read-only
  `ZipArchive`. Any failure to do so - an unreadable or corrupt (non-ZIP) stream - is wrapped in
  `InvalidDataException` (`ZipArchive`'s own constructor already throws this type for a corrupt
  archive; other failure shapes, such as `IOException`/`ArgumentException`/`NotSupportedException`/
  `EndOfStreamException`, are caught and re-thrown wrapped in the same type, so every
  package-layer failure is uniformly `InvalidDataException`).
- **`[Content_Types].xml` resolution**: parsed once, at construction time, into two lookups - a
  case-insensitive `Default` extension-to-content-type map (for example `"xml"` ->
  `"application/xml"`) and an exact-match `Override` part-path-to-content-type map (for example
  `"ppt/presentation.xml"` -> `"application/vnd.openxmlformats-officedocument.presentationml.
  presentation.main+xml"`). `ResolvePart(string partPath)` consults the override map first,
  falling back to the default map keyed by the part's file extension, and throws
  `InvalidDataException` when the package does not contain the requested part, or when neither
  map resolves a content type for it.
- **Relationship resolution**: the package-level relationships (`_rels/.rels`) are parsed eagerly
  at construction time (so a malformed package-level relationships part fails fast at `Open()`
  time), while every other part's own relationships (`{dir}/_rels/{partName}.rels`) are parsed
  lazily, on first access, and cached. `ResolveRelationship(string sourcePartPath, string
  relationshipId)` (the empty string for `sourcePartPath` denotes the package root) looks up the
  given relationship ID within the source part's own relationships, then resolves its `Target`
  attribute per OPC's relative-reference rules: a target beginning with `'/'` is resolved relative
  to the package root; any other target is resolved relative to the source part's own directory,
  with `"."`/`".."` segments in the combined path normalized away exactly like a familiar
  filesystem path (so, for example, `ppt/slides/slide1.xml`'s relationship to
  `"../slideLayouts/slideLayout1.xml"` resolves to `"ppt/slideLayouts/slideLayout1.xml"`, not
  `"ppt/slides/slideLayouts/slideLayout1.xml"`). An unknown relationship ID, a relationship whose
  `TargetMode` is `"External"` (not supported this phase), or a target that would traverse past
  the package root (more `".."` segments than preceding directory segments) all throw
  `InvalidDataException`.

**Phase 1a scope boundary**: no presentation-specific part (`ppt/presentation.xml`, a slide, a
slide layout/master) is itself interpreted - `ResolvePart`/`ResolveRelationship` only resolve a
part's content type and a relationship's target path, both of which are generic OPC package
concepts. The `_entriesByPath`/`_defaultContentTypes`/`_overrideContentTypes`/
`_relationshipCache` fields hold the entirety of this phase's parsed state.

**Phase 1b additions**: `PackageRelationship` gained a `Type` field (the relationship's own
`Type` attribute, captured by `ParseRelationships` alongside the existing `Target`/`TargetMode`
fields). Four OOXML navigations used by Phase 1b (package root -> `ppt/presentation.xml`, slide
-> layout, layout -> master, master -> theme) have no explicit `r:id` anywhere in the referencing
part's own body XML - each is, by OOXML convention, simply "the referencing part's one
relationship of a given `Type`". `ResolveRelationshipByType(string sourcePartPath, string
relationshipTypeSuffix)` resolves such a relationship by an ordinal `EndsWith` match against each
non-external relationship's `Type` URI (for example `"/officeDocument"`, `"/slideLayout"`,
`"/slideMaster"`, `"/theme"`), throwing `InvalidDataException` when none match.
`LoadPartXmlRoot(string partPath)` loads and parses a part's XML root element (checking the part
exists first), reused by every Phase 1b parser (`InitializePresentation`/`GetTheme`/`GetMaster`/
`GetLayout`/`GetSlide`) instead of each duplicating the same existence-check-then-parse logic.

### Public API (Phase 1a)

`PptxDocument.cs` implements the sealed, `IDisposable` public surface:

- **`Open(Stream stream)`**: validates `stream` is non-null (`ArgumentNullException`), buffers it
  fully into a byte array via `CopyTo` (so the caller's stream need not remain open or seekable
  afterward, exactly like `PdfDocument.Open`'s own buffering pattern), and constructs a new
  `PptxDocument` from the buffered bytes.
- **`Open(string path)`**: validates `path` is non-null (`ArgumentNullException`) and not
  empty/whitespace-only (`ArgumentException`), then opens a `FileStream` and delegates to
  `Open(Stream)`.
- **`Dispose()`**: idempotent (a `_disposed` flag guards a second call from re-disposing already-
  released resources); disposes the underlying `ZipArchive` and its backing `MemoryStream`.

### Presentation Parsing (Phase 1b)

`PptxDocument.Presentation.cs` locates and parses `ppt/presentation.xml`, called once from the
constructor (`InitializePresentation`), immediately after the Phase 1a package layer has parsed
the package-level relationships:

- **Locating the presentation part**: `ppt/presentation.xml` is not referenced by an explicit
  `r:id` anywhere - it is the package's single `/officeDocument` relationship target, resolved
  via `ResolveRelationshipByType(string.Empty, "/officeDocument")` (see the Package Layer
  section's "Phase 1b additions" above). Its root element is validated to be a PresentationML
  `<p:presentation>` element, throwing `InvalidDataException` otherwise.
- **Slide size**: `<p:sldSz cx="..." cy="..."/>` is parsed into a `PptxSlideSize` (an EMU-unit
  width/height record struct), exposed as the public `SlideSize` property. A missing `<p:sldSz>`,
  or a missing/non-numeric/non-positive `cx`/`cy` attribute, throws `InvalidDataException`.
- **Slide list**: `<p:sldIdLst>`'s `<p:sldId r:id="..."/>` children are resolved, in document
  order, into a list of slide part paths (via the presentation part's own relationships),
  exposed as the public `SlideCount` property (the resolved list's count). A missing
  `<p:sldIdLst>`, an empty slide list, or any child with a missing or unresolvable `r:id` throws
  `InvalidDataException`.
- **Pixel conversion**: `GetSlideSizeInPixels(float dpi)` converts `SlideSize`'s EMU dimensions to
  pixels at the caller's chosen resolution (914400 EMU per inch), rounding each dimension to the
  nearest pixel (`MidpointRounding.AwayFromZero`) - the EMU analogue of `PdfDocument.Render(int,
  float)`'s own points-to-pixels conversion, provided ahead of any Phase 1b rendering surface so a
  later phase reuses this exact, already-tested arithmetic. Rejects a non-positive or non-finite
  `dpi` with `ArgumentOutOfRangeException`.

**Eager/lazy validation split**: only the presentation part itself, its slide size, and its slide
list's relationship resolvability are validated eagerly, at `Open()` time. Each resolved slide
part's own existence, well-formedness, and layout/master/theme chain are validated lazily, on
first access via `GetSlide(int)` - mirroring Phase 1a's own eager (package-level relationships) /
lazy (per-part relationships) split.

### Theme Parsing (Phase 1b)

`PptxDocument.Theme.cs` resolves a theme part (`ppt/theme/themeN.xml`, reached via a slide
master's `/theme` relationship - see _Master/Layout/Slide Placeholder Models_ below) into a
`PptxTheme`, lazily and cached by part path (`GetTheme(string themePartPath)`):

- **Color scheme**: `<a:clrScheme>`'s 12 named slots (`dk1`/`lt1`/`dk2`/`lt2`/`accent1`-`accent6`/
  `hlink`/`folHlink`) are each resolved to a concrete `DemaConsulting.CanvasNet.Canvas.Rgba32`
  value - an `<a:srgbClr val="RRGGBB"/>` resolves directly via `Rgba32.Parse`; an
  `<a:sysClr val="windowText" lastClr="RRGGBB"/>` resolves to its cached `lastClr` RGB equivalent
  (no actual OS system-color resolution is performed or possible offline). `Rgba32.Parse`'s own
  `FormatException` on invalid hex is caught and re-thrown wrapped in `InvalidDataException`, so
  every theme-parsing failure is uniformly `InvalidDataException` like every other structural
  failure in this unit.
- **Font scheme**: `<a:fontScheme>`'s major (heading) and minor (body) typeface collections are
  each resolved into a `PptxFontCollection` naming the Latin (`<a:latin typeface="..."/>`), East
  Asian (`<a:ea typeface="..."/>`), and complex-script (`<a:cs typeface="..."/>`) typeface - a
  missing `typeface` attribute defaults to an empty string (tolerated), but a missing `<a:latin>`/
  `<a:ea>`/`<a:cs>` element itself throws `InvalidDataException`.

This is `internal` state (`PptxTheme`/`PptxColorScheme`/`PptxFontScheme`/`PptxFontCollection` are
all `internal` records), carried alongside the placeholder property-inheritance chain as context
for a later rendering phase to resolve a scheme-color/font token - not itself part of the
placeholder matching chain (see _Placeholder Inheritance Resolution_ below).

### Master/Layout/Slide Placeholder Models (Phase 1b)

`PptxDocument.Masters.cs`/`PptxDocument.Layouts.cs`/`PptxDocument.Slides.cs` structurally parse a
slide master/layout/slide part, respectively, each lazily resolved and cached (`GetMaster(string
masterPartPath)`, `GetLayout(string layoutPartPath)`, `GetSlide(int slideIndex)`):

- **`GetMaster`**: validates the part's root is `<p:sldMaster>` with a `<p:cSld>/<p:spTree>`
  descendant, resolves the master's own `/theme` relationship (via `ResolveRelationshipByType`,
  see the Package Layer section's "Phase 1b additions" above), and parses its immediate
  placeholder shapes into a `PptxMaster` (part path, theme part path, placeholder list).
- **`GetLayout`**: validates the part's root is `<p:sldLayout>`, resolves the layout's own
  `/slideMaster` relationship, and parses its immediate placeholder shapes into a `PptxLayout`
  (part path, master part path, placeholder list).
- **`GetSlide(int slideIndex)`**: range-checks `slideIndex` against `SlideCount`
  (`ArgumentOutOfRangeException`), validates the slide part's root is `<p:sld>`, resolves the
  slide's own `/slideLayout` relationship, and parses its immediate placeholder shapes into a
  `PptxSlide` (part path, layout part path, placeholder list).

All three share `PptxPlaceholder.cs`'s `PptxPlaceholderParser.ParsePlaceholderShapes`: it walks a
`<p:spTree>`'s immediate `<p:sp>` children only (no group/recursive descent - Phase 1b does not
parse freeform or grouped shapes), filters to those with a `<p:nvSpPr>/<p:nvPr>/<p:ph>`
descendant, and builds one `PptxPlaceholder` (type, idx, raw `<p:sp>` element) per match, applying
the OOXML schema defaults (`type="obj"`, `idx="0"`) when the corresponding attribute is omitted.
A non-placeholder `<p:sp>` (no `<p:ph>` descendant) is silently excluded, not an error.

### Placeholder Inheritance Resolution (Phase 1b)

`PptxDocument.Inheritance.cs`'s `ResolvePlaceholderProperties` implements the verified ECMA-376
(ISO/IEC 29500) §19.3.1.36 placeholder matching algorithm - **two different, level-specific,
single-criterion matches**, not a uniform "type+idx exact match, then type-only fallback":

1. **Slide -> layout**: matched by `idx` ALONE. `type` is not consulted at this hop. A miss (no
   layout placeholder shares the slide placeholder's `idx`) is a genuine miss - it is not retried
   by type.
2. **Layout -> master**: matched by `type` ALONE, through a fixed remapping table (`body`/`chart`/
   `bitmap`/`orgChart`/`mediaClip`/`obj`/`pic`/`subTitle`/`tbl`/`clipArt`/`dgm`/`media` all remap
   to `"body"`; `ctrTitle` remaps to `"title"`; every other type maps to itself), using the
   _matched layout placeholder's own type_ (not the slide placeholder's type). `idx` is not
   consulted at this hop. This hop only runs when hop 1 found a layout match - a hop 1 miss
   short-circuits the chain entirely (no fallback to matching the master directly against the
   slide's own type).

Once the matched layout/master placeholders (if any) are determined, each of exactly two named
property categories - `<p:spPr>` and `<p:txBody>/<a:lstStyle>` - is independently resolved via a
first-non-null-in-chain walk: slide -> matched layout -> matched master, taking the first element
present at all (an empty element still counts as present, stopping the fallback for that
category; no deeper per-attribute merging is performed in Phase 1b). The resolved theme is
carried through unchanged as context (`PptxPlaceholderProperties.Theme`) - it is not itself part
of the matching chain, since a theme supplies scheme-level tokens a property fragment may
reference, rather than containing placeholder-shaped XML to match against.

### Public API (Phase 1b)

`PptxDocument.cs` gains three new public members, each guarded by
`ObjectDisposedException.ThrowIf(_disposed, this)`:

- **`SlideCount`** (`int`): the presentation's declared slide count (`_slidePartPaths.Count`).
- **`SlideSize`** (`PptxSlideSize`): the presentation's declared slide size, in EMU.
- **`GetSlideSizeInPixels(float dpi)`** (`(int Width, int Height)`): converts `SlideSize` to
  pixels at the given resolution - see _Presentation Parsing (Phase 1b)_ above.

All other Phase 1b members (`GetTheme`/`GetMaster`/`GetLayout`/`GetSlide`/
`ResolvePlaceholderProperties`, and every new record type) are `internal`, exercised by the test
project via `InternalsVisibleTo`, matching Phase 1a's own `ResolvePart`/`ResolveRelationship`
precedent - no public shape/rendering surface is introduced this phase.

### Geometry and Paint (Phase 1c)

Phase 1c adds DrawingML shape geometry and paint resolution: resolving a shape's transform,
geometric path, fill, and stroke into the core `DemaConsulting.CanvasNet.Geometry`/
`DemaConsulting.CanvasNet.Drawing`/`DemaConsulting.CanvasNet.Canvas` types a future rendering
phase paints directly, rather than reinventing path/stroke/gradient machinery in this package.
Every new member introduced this phase is `internal` - no public shape/rendering surface is
introduced yet (matching Phase 1b's own precedent); this is confirmed by inspection, not merely
asserted.

#### Shape Frame Transform

`PptxDocument.Geometry.cs`'s `ResolveShapeFrame(XElement xfrm)` resolves a shape's `<a:xfrm>`
element into a `PptxShapeFrame` (an `internal sealed record` pairing a `System.Numerics.Matrix3x2`
transform with the shape's own declared width/height in EMU):

- **Position/size**: `<a:off x="..." y="..."/>` and `<a:ext cx="..." cy="..."/>` are both
  required; either missing throws `InvalidDataException`.
- **Rotation**: `rot` (an optional attribute, 60,000ths of a degree, defaulting to `0` when
  omitted) rotates the shape about its own center - `(w/2, h/2)` in local coordinates - matching
  DrawingML's own rotation semantics (never about the parent origin). A non-numeric `rot` throws
  `InvalidDataException`.
  OOXML's `rot` is a clockwise angle in screen (y-down) coordinates; `Matrix3x2.CreateRotation`'s
  standard mathematical rotation matrix, applied in this package's y-down coordinate space,
  already turns a positive angle clockwise on screen (the y-axis flip inherent in screen
  coordinates reverses the usual y-up counter-clockwise sense) - so `rot`'s 60,000ths-of-a-degree
  value converts directly to radians with no extra sign negation needed.
- **Flip**: `flipH`/`flipV` (optional boolean attributes, `"1"`/`"true"` truthy, defaulting to
  `false`) mirror the shape about its own center's vertical/horizontal axis respectively, applied
  _before_ rotation (matching DrawingML's own documented order of operations: flip, then
  rotate, then translate).
- **Composition order**: the final transform is `flip * rotate * translate(off)`, composed so
  that `Vector2.Transform(localPoint, frame.Transform)` (row-vector convention, matching
  `Path.Transform(Matrix3x2)`'s own convention) maps a point in the shape's own `(0,0)`-`(w,h)`
  local coordinate space directly into its parent's coordinate space in one call.

#### Group Shape Child Transform Composition

A group shape (`<p:grpSp>`) has its own `<a:xfrm>` carrying _two_ coordinate boxes: `off`/`ext`
(the group's own placement, in its parent's coordinate space - resolved exactly like any other
shape's frame via `ResolveShapeFrame`) and `chOff`/`chExt` (the child coordinate space every
shape nested directly inside the group is authored in, which generally differs in both offset
and scale from the group's own `off`/`ext` box).

`ResolveGroupChildTransform(XElement groupXfrm)` resolves only the `chOff`/`chExt`-to-`off`/`ext`
half of this composition - a `Matrix3x2` mapping a point in the group's _child_ coordinate space
into the group's _parent_ coordinate space (not merely its own local box - the group's own
resolved `ResolveShapeFrame` transform, including its own flip/rotation about its own center, is
folded in as the final step). `chOff` defaults to `(0,0)` and `chExt` defaults to the group's own
`ext` (`WidthEmu`/`HeightEmu`) when either is omitted - a common case for an unscaled,
un-translated child coordinate space. A zero-width/height `chExt` (which cannot be meaningfully
scaled from) falls back to an identity scale factor of `1` along that axis rather than dividing by
zero.

A child shape nested in the group resolves its _own_ `ResolveShapeFrame(childXfrm).Transform`
exactly as it would as a top-level shape (its `<a:xfrm>` is still expressed in the group's child
coordinate space, not the group's own parent space), then composes
`childFrame.Transform * groupChildTransform` to obtain the child's full transform into the
group's parent space (row-vector composition order, child-local-first - matching every other
transform composition in this phase). For a shape nested in multiple levels of groups, this
composition chains: each level's `ResolveGroupChildTransform` result is itself post-multiplied by
its own parent group's child transform before being combined with a descendant's own
`ResolveShapeFrame` result.

**Group-semantics deferral boundary**: this phase implements transform composition only. Full
group-shape semantics beyond that - inherited/overridden fill or line style cascading from a
group to its children, group-level effects, and the `<p:grpSp>`'s own enumeration/recursion
through a slide's full shape tree (Phase 1b's placeholder parser still only walks a `<p:spTree>`'s
_immediate_ `<p:sp>` children, not recursing into `<p:grpSp>`/parsing freeform shapes at all) -
are explicitly deferred to Phase 1e, per the approved plan.

#### Preset Geometry

`PptxPresetGeometry.cs`'s `Build(string prst, float w, float h)` builds the core `Path` for a
named preset geometry (`<a:prstGeom prst="..."/>`), sized to exactly `(0,0)`-`(w,h)` local
coordinates (the caller then applies the shape's resolved `PptxShapeFrame.Transform` via
`Path.Transform(Matrix3x2)` to place it into parent coordinates).

**Supported presets (24)**: `rect`, `roundRect`, `ellipse`, `triangle`, `rtTriangle`, `diamond`,
`parallelogram`, `trapezoid`, `hexagon`, `octagon`, `pentagon`, `chevron`, `homePlate`, `pie`,
`donut`, `plus`, `rightArrow`, `leftArrow`, `upArrow`, `downArrow`, `leftRightArrow`,
`upDownArrow`, `star4`, `star5`. This set was chosen to cover the small handful of presets
(`rect`/`roundRect`/`ellipse`/`triangle`) PowerPoint itself defaults new shapes to, plus a
practical cross-section of the arrow, star, and other basic-shape categories real-world
presentations most commonly use, per the approved plan's "aim for ~24 total" guidance.

**Adjustment-value scope**: OOXML preset shapes are parameterized by named "adjustment values"
(`<a:avLst>/<a:gd name="adj" fmla="val NNNNN"/>`) a document may override to reshape a preset (for
example a `roundRect`'s corner radius, or a `homePlate`'s notch depth). This phase does not parse
`<a:avLst>` at all - every preset is built using a single, fixed, documented proportion
approximating that preset's own OOXML schema default adjustment value(s) (for example
`roundRect`'s corner radius is fixed at `min(w,h)/6`, matching the schema default of `16667`
(16.667%) rather than reading any `<a:gd name="adj">` override). This is a deliberate
simplification: a shape whose author customized its adjustment handles renders using the default
proportion instead, not pixel-exact to every possible customization, but still a recognizable
instance of the named preset shape. Parsing `<a:avLst>` is deferred to a later phase.

**Unsupported presets**: an unrecognized `prst` name throws `PptxUnsupportedFeatureException`
(feature token `"pptx-preset-geometry"`) rather than silently falling back to an incorrect shape
(for example rendering an unknown preset as a plain rectangle) - this fail-closed approach lets a
caller distinguish "this shape's geometry could not be resolved" from "this shape resolved to the
wrong geometry", which a silent fallback would make indistinguishable.

`ResolveShapeGeometry(XElement spPr, float widthEmu, float heightEmu)` dispatches between
`<a:prstGeom>` (delegating to `PptxPresetGeometry.Build`) and `<a:custGeom>` (delegating to
`ResolveCustomGeometry` below), throwing `InvalidDataException` when `<p:spPr>` has neither
element, or when a present `<a:prstGeom>` is missing its required `prst` attribute.

#### Custom Geometry

`ResolveCustomGeometry(XElement custGeom, float widthEmu, float heightEmu)` parses a shape's
`<a:custGeom>` element - every `<a:path>` under its `<a:pathLst>` (not merely the first; multiple
`<a:path>` elements each contribute their own independent subpath/subpaths to the result) - into
the core `Path` type, interpreting each path command:

- **`<a:moveTo>`/`<a:lnTo>`**: `PathBuilder.MoveTo`/`.LineTo`.
- **`<a:cubicBezTo>`** (two control points + an endpoint): `PathBuilder.CubicBezierTo`.
- **`<a:quadBezTo>`** (one control point + an endpoint): `PathBuilder.QuadraticBezierTo`.
- **`<a:close>`**: `PathBuilder.Close`.

Each `<a:path>`'s own `w`/`h` attributes declare that path's local coordinate space (defaulting to
the shape's own `widthEmu`/`heightEmu` - an identity scale - when either is omitted); every point
within that `<a:path>` is scaled by `widthEmu / w` and `heightEmu / h` respectively before being
added to the builder, so a path authored in an arbitrary coordinate space still fills the shape's
declared extent exactly. A `<a:custGeom>` with no `<a:pathLst>` resolves to `Path.Empty` (not an
error - a degenerate but well-formed case).

**Best-effort command handling**: an unrecognized path-command element (for example OOXML's own
distinct, center-parameterized `<a:arcTo>` command, which this phase does not convert to the core
`Path`'s SVG-endpoint-parameterized arc representation) is silently skipped rather than thrown -
a deliberate, documented difference from preset geometry's fail-closed philosophy, chosen because
a custom geometry's overall silhouette is still usably close even with one skipped arc segment,
whereas an entire unsupported preset shape has no usable partial rendering at all.

#### Color Resolution and Transforms

`PptxDocument.Paint.cs`'s `ResolveColor(XElement colorElement, PptxTheme theme)` resolves any
DrawingML color-definition element to a concrete `Rgba32`:

- **`<a:srgbClr val="RRGGBB"/>`**: resolved directly via `Rgba32.Parse`.
- **`<a:sysClr .../>`**: resolved via its `lastClr` attribute, identical to Phase 1b's own theme
  `sysClr` handling.
- **`<a:schemeClr val="..."/>`**: resolved by looking the named slot up in the _supplied_
  `PptxTheme`'s `PptxColorScheme` - reusing Phase 1b's already-verified theme resolver rather than
  re-implementing its 12-slot lookup table a second time. The 12 ordinary slot names
  (`dk1`/`lt1`/`dk2`/`lt2`/`accent1`-`accent6`/`hlink`/`folHlink`) map directly to their
  like-named `PptxColorScheme` property; the four background/text aliases `bg1`/`tx1`/`bg2`/`tx2`
  map to `lt1`/`dk1`/`lt2`/`dk2` respectively (OOXML's own documented aliasing of the "background"
  and "text" roles onto the light/dark scheme slots); `phClr` (a placeholder-fill color, only
  meaningful inside a shape-style-reference context this phase does not thread through) falls
  back to `dk1` as a documented simplification. An unrecognized slot name throws
  `InvalidDataException`.
- **Any other color-definition element** (for example `<a:hslClr>`, `<a:prstClr>`,
  `<a:scrgbClr>`) throws `PptxUnsupportedFeatureException` (feature token
  `"pptx-color-kind"`) rather than guessing at an approximate RGB conversion.

**Color-transform pipeline**: a color-definition element's child transforms
(`<a:lumMod>`/`<a:lumOff>`/`<a:shade>`/`<a:tint>`/`<a:alpha>`, each a percentage expressed in
OOXML's thousandths-of-a-percent unit, where `100000` = 100%) are applied to the resolved base
color in a **fixed pipeline order**, deliberately independent of each transform element's actual
position in the source XML (real-world documents do not reliably order these consistently, so a
fixed, documented order is simpler and more predictable than attempting to honor document order):

1. **`lumMod`/`lumOff`** (luminance modulation/offset), applied together first, in HSL space:
   the base color is converted to HSL, `L' = clamp(L * lumMod + lumOff, 0, 1)` is computed (each
   of `lumMod`/`lumOff` defaulting to `1.0`/`0.0` respectively when absent), and the result is
   converted back to RGB.
2. **`shade`** (darken toward black), in RGB space: `c' = c * (val / 100000)` per channel.
3. **`tint`** (lighten toward white), in RGB space: `c' = c * (val / 100000) + 255 * (1 - val /
   100000)` per channel.
4. **`alpha`**, applied last: replaces the color's alpha channel outright with
   `val / 100000 * 255` (not multiplied against any existing alpha - every base color this phase
   resolves is fully opaque before this step).

Each RGB channel conversion casts its computed `float` result directly to `byte` (truncating, not
rounding, toward zero) after `Math.Clamp`-ing to `[0, 255]` - for example a `50%` `alpha` applied
to a fully-opaque color resolves to alpha `127`, not `128` (`0.5 * 255 = 127.5`, truncated).

#### Fill Resolution

`ResolveFill(XElement? spPr, PptxTheme theme, float widthEmu, float heightEmu)` resolves a
shape's fill from its `<p:spPr>` (or lack thereof) into one of three closed `PptxPaint` subtypes:

- **`PptxNoFill`** (a singleton, `PptxNoFill.Instance`): resolved when `spPr` is `null`, when
  `<p:spPr>` has an explicit `<a:noFill/>` child, or when it has no recognized fill child element
  at all (a documented simplification: no placeholder/theme format-scheme fill inheritance is
  implemented this phase - an inherited fill a real presentation would show is resolved as
  unfilled instead).
- **`PptxSolidFill(Rgba32 Color)`**: resolved from `<a:solidFill>`'s single color-definition
  child, via `ResolveColor` above.
- **`PptxGradientFill(Gradient Gradient)`**: resolved from `<a:gradFill>`, via
  `ResolveGradientFill` below.
- **`<a:pattFill>`/`<a:blipFill>`** (pattern/picture fill): rejected with
  `PptxUnsupportedFeatureException` (feature tokens `"pptx-pattern-fill"`/`"pptx-picture-fill"`
  respectively) - implementing either requires tiling/image-decoding machinery out of scope for
  this phase, and resolving either to `PptxNoFill` would silently drop a fill a real presentation
  shows, which this phase's fail-closed philosophy for unsupported-but-declared features rejects
  in favor of a caller-visible, distinguishable exception. Deferred to Phase 1e.

#### Gradient Fill

`ResolveGradientFill(XElement gradFill, PptxTheme theme, float widthEmu, float heightEmu)`
resolves `<a:gradFill>`'s `<a:gsLst>` gradient stops (each `<a:gs pos="...">`'s `pos` - OOXML's
own thousandths-of-a-percent unit - mapped to the core `GradientStop`'s `[0,1]` position, and its
single color-definition child resolved via `ResolveColor`) combined with its `<a:lin ang="...">`
linear-direction angle (60,000ths of a degree) into a core `LinearGradient`:

- `angleRadians = (ang / 60000) * (pi / 180)`; `direction = (cos(angleRadians), sin(angleRadians))`.
- `extent = (|direction.X| * widthEmu + |direction.Y| * heightEmu) / 2` (the gradient's half-length
  along its own direction, sized so the gradient exactly spans the shape's bounding box corner to
  corner along that direction).
- `center = (widthEmu / 2, heightEmu / 2)`; `start = center - direction * extent`;
  `end = center + direction * extent`.

**Scope**: only linear (`<a:lin>`) gradients are supported this phase - the most common gradient
fill in practice. A `<a:gradFill>` with no `<a:lin>` child (for example a path/radial gradient,
`<a:path path="circle"/>` or similar) throws `PptxUnsupportedFeatureException` (feature token
`"pptx-gradient-path"`) rather than silently approximating it as a linear gradient, which would
visibly misrender the shape's fill. A `<a:gradFill>` missing its required `<a:gsLst>` throws
`InvalidDataException`. Radial/path gradients are deferred to a later phase, pending a core
`RadialGradient`/path-gradient paint type this package can reuse.

#### Stroke/Line Style Resolution

`ResolveLineStyle(XElement? lnElement, PptxTheme theme)` resolves a shape's `<a:ln>` stroke
element into a `PptxLineStyle` (an `internal sealed record` capturing width in EMU, a resolved
`PptxPaint`, and an optional dash array), or `null` (meaning "no stroke is drawn") when:

- `lnElement` is `null` (no `<a:ln>` element at all - a documented simplification: this does
  _not_ resolve a theme-inherited default line width/style, matching this phase's broader
  decision not to implement placeholder/theme format-scheme property inheritance for shape
  styling);
- `<a:ln>`'s `w` attribute (width, in EMU) is missing or non-positive; or
- the resolved fill (typically `<a:solidFill>`, via `ResolveFill`) is `PptxNoFill` (an explicit
  `<a:noFill/>` line, or a line with no recognized fill child).

**Dash resolution**: `<a:prstDash val="..."/>` resolves a dash array proportional to the line's
own `widthEmu`, for `dash`/`dashDot`/`dot`/`lgDash`/`lgDashDot`/`sysDash`/`sysDot`; any other
preset name (including the common `solid`, and any name this phase does not recognize) resolves
to `null` (a solid line) - a documented, deliberate cosmetic degradation (an unrecognized dash
preset renders as a solid line rather than throwing) distinct from preset-geometry's fail-closed
philosophy, chosen because a solid line where a dashed one was intended is still a usable, visible
stroke, unlike an entirely unresolved shape.

**Realizing a stroke outline**: `ResolveStrokeOutline(Path shapePath, PptxLineStyle lineStyle)`
mirrors the PDF renderer's own stroke-operator call pattern exactly: it constructs a core
`StrokeStyle(lineStyle.WidthEmu, dashArray: lineStyle.DashArray)` and calls the core
`PathStroker.Stroke(shapePath, style)` static entry point, producing the stroked outline path a
later rendering phase fills (via `PathFiller.Fill(..., FillRule.NonZero)`) with
`lineStyle.Paint`'s resolved color - reusing the exact same, already-tested stroking machinery the
PDF renderer uses, rather than a second, divergent implementation for this package. This phase has
no rendering surface yet, so only the outline geometry is produced here.

#### Deferred to a Later Phase

The following are explicitly out of scope for Phase 1c, each because it depends on machinery not
yet implemented anywhere in `CanvasNet` (not merely unimplemented in this package):

- **Pattern fill** (`<a:pattFill>`) and **picture fill** (`<a:blipFill>`) - require tiling/
  image-decoding machinery; deferred to Phase 1e.
- **Radial/path gradients** - require a core `RadialGradient`/path-gradient paint type this
  package can reuse; deferred to a later phase.
- **`<a:avLst>` adjustment-value parsing** for preset geometries - deferred to a later phase; see
  _Preset Geometry_ above.
- **Full group-shape semantics** beyond transform composition (style cascading, a slide's
  `<p:spTree>` recursing into `<p:grpSp>`/parsing freeform, non-placeholder shapes at all) -
  deferred to Phase 1e; see _Group Shape Child Transform Composition_ above.

### Text Layout and Rendering (Phase 1d)

Phase 1d adds DrawingML text body parsing, attribute-level property inheritance, word-wrap/
alignment/vertical-anchor/autofit text layout, and glyph-ink rendering onto a core
`DemaConsulting.CanvasNet.Canvas.Surface`. It additionally depends on the core
`DemaConsulting.CanvasNet.Fonts.TrueTypeFont`/`SystemFontCatalog` types (font resolution and
glyph-outline extraction) and `DemaConsulting.CanvasNet.Drawing.PathFiller` (glyph-ink fill),
reusing exactly the same font-resolution/glyph-painting pattern `DemaConsulting.CanvasNet.Pdf`'s
`PdfDocument.Text.cs` already established, rather than a second, divergent implementation. Every
new member introduced this phase is `internal` - no public shape/rendering surface is introduced
yet (matching Phase 1b/1c's own precedent).

#### Text Body Parsing

`PptxDocument.Text.cs` parses a shape's `<p:txBody>` element (`ParseTextBody`) into a
`PptxTextBody` (an `internal sealed record`, defined alongside its sibling small record types in
`PptxTextBody.cs`, mirroring `PptxShapeFrame.cs`/`PptxPaint.cs`'s one-file-per-small-type-family
convention): resolved body properties plus an ordered list of paragraphs.

- **`ParseBodyProperties(XElement? bodyPrElement)`** resolves `<a:bodyPr>`'s vertical anchor
  (`anchor="t"/"ctr"/"b"`, defaulting to `t`/`PptxTextAnchor.Top`), word-wrap mode
  (`wrap="square"/"none"`, defaulting to `square`/`PptxTextWrap.Square`), and the four insets
  (`lIns`/`tIns`/`rIns`/`bIns`, each in EMU), defaulting to the OOXML schema's own documented
  defaults - `91440`/`45720`/`91440`/`45720` EMU respectively - when `<a:bodyPr>` itself, or an
  individual inset attribute, is absent. The raw autofit child element (`<a:noAutofit>`/
  `<a:normAutofit>`/`<a:spAutoFit>`, if present) is retained **unparsed**, as an `XElement?`, for
  the layout engine (`PptxDocument.TextLayout.cs`) to interpret - body-property parsing does not
  itself implement autofit policy.
- **`ParseParagraph(XElement pElement)`** resolves `<a:p>` into a `PptxParagraph`: raw paragraph
  properties (via `ParseParagraphProperties`) plus an ordered list of runs (via `ParseRun`). A
  paragraph with no recognized child element resolves to an empty run list - a valid, empty
  paragraph (a blank line), not an error; `InvalidDataException` is reserved for genuinely
  malformed structure, not merely sparse/empty content.
- **`ParseParagraphProperties(XElement? pPrElement)`** resolves `<a:pPr>`'s `algn`, `marL`,
  `indent`, `lnSpc`, `spcBef`, `spcAft`, and `defRPr` child/attributes, each retained **raw and
  unresolved** (a `PptxRawParagraphProperties` record) - resolution against the inheritance chain
  happens later, in `PptxDocument.TextInheritance.cs`, not here. The paragraph's own `lvl`
  attribute (its placeholder/master style level) is clamped to the OOXML schema's documented
  ten-level `[0,8]` range.
- **`ParseRun(XElement rElement)`** resolves `<a:r>` into a `PptxTextRun`: its own raw,
  unresolved `<a:rPr>` element (or `null`) plus its `<a:t>` text, defaulting to `string.Empty`
  when `<a:t>` is absent, per the OOXML schema.

#### Master `<p:txStyles>` Parsing

`PptxDocument.Masters.cs`'s `GetMaster` additionally parses the slide master root element's own
`<p:txStyles>` child - a direct sibling of `<p:cSld>` per ECMA-376 (ISO/IEC 29500) §19.3.1.53's
`CT_SlideMaster` content model, not nested inside it, exactly paralleling how `GetMaster` already
reads `<p:cSld>` as a direct child of the same root - into a new `PptxMasterTextStyles(XElement?
TitleStyle, XElement? BodyStyle, XElement? OtherStyle)` record (`PptxPlaceholder.cs`), each an
`<a:lstStyle>`-shaped element or `null` when the master omits that schema-optional style bucket.
`PptxMaster` gains a new, non-nullable `TxStyles` field (always populated, even when all three
buckets inside are `null`), and `PptxPlaceholderProperties` gains a new optional
`MasterTextStyles` field threaded through unconditionally from `ResolvePlaceholderProperties`'s
new optional `masterTextStyles` parameter - independent of whether a layout/master placeholder
match was found, since a master's own default text styling exists even for a shape with no
placeholder match at all (see _Property Inheritance_ below).

#### Property Inheritance

**Design decision: attribute-level, not element-level, fallback.** Phase 1b's placeholder
property inheritance (`<p:spPr>` and `<p:txBody>/<a:lstStyle>`) resolves at the level of the
_whole element_: the first category-level element present anywhere in the chain wins outright,
with no deeper merging. Run/paragraph text-property inheritance deliberately does **not** reuse
that same element-level pattern. Real-world DrawingML very commonly overrides a single attribute
at the run level (for example, bolding one word within an otherwise plain sentence) without
overriding every other attribute at the same level; an element-level fallback would incorrectly
discard every other, more specific attribute resolution available at a shallower level for that
run. `PptxDocument.TextInheritance.cs` therefore resolves **each attribute independently**,
walking the same conceptual chain - run/paragraph own value -> paragraph's own `defRPr` ->
placeholder's level-indexed `<a:lstStyle>` entry -> master's level-indexed `<p:txStyles>` bucket
entry -> theme/hard-coded default - via six small private per-attribute helpers for run
properties (`ResolveTypeface`, `ResolveFontSizeEmu`, `ResolveBold`, `ResolveItalic`,
`ResolveUnderline`, `ResolveRunColor`) and an analogous set for paragraph properties, each
independently taking its own first non-`null` hit.

- **`ResolveEffectiveRunProperties(PptxTextRun run, PptxParagraph paragraph,
  PptxPlaceholderProperties placeholderProperties, PptxTheme theme, string placeholderType)`**
  resolves a `PptxEffectiveRunProperties` (font family, size in EMU, bold, italic, underline,
  color). Font size (`<a:rPr sz="1800"/>` means 18pt, i.e. `sz / 100` points, further scaled by
  `* 12700` to EMU) and every boolean flag (`"1"`/`"true"`) are parsed at whichever tier first
  supplies a non-`null` value.
- **`ResolveEffectiveParagraphProperties(...)`** resolves a `PptxEffectiveParagraphProperties`
  (alignment, left margin, indent, line spacing). Alignment normalizes `algn="just"`/`"justLow"`
  (full/low text justification) to `"l"` (left) - full justification requires redistributing
  inter-word spacing per line, a separable refinement explicitly deferred (see _Deferred to a
  Later Phase_ below). Line spacing (`<a:lnSpc>`) is either `<a:spcPct val="…"/>` (percentage,
  `val / 100000`) or `<a:spcPts val="…"/>` (fixed points, `val / 100 * 12700` EMU) - mutually
  exclusive per schema - modeled as a `PptxLineSpacing(float? Percent, float? FixedEmu)`
  discriminated record (exactly one field non-`null`), with a static `Default` of 100%.
- **`<p:txStyles>` bucket selection**: for a given shape's placeholder type, `title`/`ctrTitle`
  selects the master's `TitleStyle`, the empty-string sentinel (used for a non-placeholder shape
  - see `PptxDocument.Masters.cs`'s `RemapPlaceholderType`/placeholder-matching precedent in
  Phase 1b) selects `OtherStyle`, and every other type (including `body` and every type the
  Phase 1b remapping table folds into `body`) selects `BodyStyle`.

#### Text Layout

`PptxDocument.TextLayout.cs`'s `ResolveTextLayout(PptxTextBody textBody, PptxPlaceholderProperties
placeholderProperties, PptxTheme theme, string placeholderType, float widthEmu, float heightEmu,
Func<string, bool, bool, TrueTypeFont> fontResolver)` produces a `PptxTextLayout`
(`PptxTextLayout.cs`): an ordered list of `PptxGlyphPlacement` (resolved font, glyph index,
baseline-relative origin in the shape's own local, unrotated coordinate space, size, and color)
plus the resolved total block height.

1. Every run's/paragraph's effective properties are resolved via `PptxDocument.TextInheritance.cs`
   (above).
2. **Autofit** (the body's raw autofit element) is applied in three tiers, resolved once for the
   whole text body before layout:
   - **No scaling** - `<a:noAutofit>`, an absent autofit element, or `<a:spAutoFit>` (shape-resize
     autofit; deliberately not implemented - see _Deferred to a Later Phase (Phase 1d)_ below,
     since it would require resizing the owning shape itself, a cross-cutting concern beyond this
     unit's text-layout scope).
   - **Explicit `<a:normAutofit fontScale="…" lnSpcReduction="…"/>`** - the stored factors are
     applied verbatim (PowerPoint persists these once it has itself computed a fit).
   - **Attribute-less `<a:normAutofit/>`** - a bounded, deterministic shrink loop: starting at
     100% and stepping down in 10% increments (at most 9 iterations, floored at 10%), the loop
     stops at the first scale whose naturally laid-out height fits the available height. This
     reproduces PowerPoint's own "shrink text on overflow" user-visible effect without depending
     on PowerPoint's own unpublished exact algorithm (which lazily computes and persists its own
     `fontScale`/`lnSpcReduction` once a user has interacted with the file - an attribute-less
     `<a:normAutofit/>` is PowerPoint's own "not yet computed" state).
3. **Tokenization and word-wrap**: each run's text is tokenized into whitespace/non-whitespace
   tokens via a small manual scanner (not `string.Split(' ')`, so consecutive spaces/tabs are
   preserved as their own token's own advance width, rather than collapsed). Each distinct
   `(FontFamily, Bold, Italic)` combination is resolved to a `TrueTypeFont` once via
   `fontResolver`, memoized per layout call (a document-level, cross-call cache is not needed
   this phase, mirroring Phase 1b's own per-document, not static, theme/master/layout caches).
   Tokens are packed greedily onto lines bounded by `widthEmu` less the body's horizontal insets;
   a single token that alone exceeds the available width is placed alone on its own (overflowing)
   line rather than looping indefinitely - a documented overflow policy, not a crash.
4. **Line height** is computed from the _tallest_ run on that line:
   `(Ascender - Descender + LineGap) / UnitsPerEm * SizeEmu * LineSpacingFactor`. **Horizontal
   start X** is computed from the effective alignment (`l`/`ctr`/`r` - `just`/`justLow` already
   normalized to `l` by property inheritance, above) against the available width, honoring
   `MarginLeftEmu`/`IndentEmu` (first line of a paragraph only, per the OOXML indent model).
5. **Vertical start Y** is computed from `PptxBodyProperties.Anchor` (`t`/`ctr`/`b`) and the
   total resolved block height within `heightEmu` less the body's vertical insets. No clipping is
   applied when the block overflows the available height - text is positioned exactly as
   computed, even past the shape's own box (see _Deferred to a Later Phase (Phase 1d)_ below).
6. One `PptxGlyphPlacement` is emitted per non-whitespace glyph.

#### Rendering

`PptxDocument.TextRender.cs`'s `PaintTextLayout(Surface surface, PptxTextLayout layout, Matrix3x2
shapeToSurfaceTransform)` paints a resolved layout's glyphs onto a core `Surface`, mirroring
`DemaConsulting.CanvasNet.Pdf`'s `PdfDocument.Text.cs`'s own glyph-painting pattern exactly: for
each `PptxGlyphPlacement`, a `glyphMatrix = Matrix3x2.CreateScale(glyph.SizeEmu /
glyph.Font.UnitsPerEm) * Matrix3x2.CreateTranslation(glyph.OriginXEmu, glyph.OriginYEmu) *
shapeToSurfaceTransform` is composed (scale to EMU-per-unit, then translate to the shape-local
baseline origin, then compose with the shape's own frame transform), the glyph's outline
(`TrueTypeFont.GetGlyphOutline`) is re-issued through a fresh `Geometry.PathBuilder` with every
point transformed by `glyphMatrix`, and the result filled via `Drawing.PathFiller.Fill(surface,
path, glyph.Color, Drawing.FillRule.NonZero)`. A glyph whose outline has zero subpaths (a
whitespace or control character) is skipped entirely, rather than issuing an empty fill.

`ResolveTextFont(string familyNameHint, bool bold, bool italic)` resolves a DrawingML typeface
name hint to a concrete `TrueTypeFont` via the core `Fonts.SystemFontCatalog.FindBestMatch`
(always passing `serif: false, fixedPitch: false`, since DrawingML never signals either
classification - unlike the PDF renderer's Standard-14 font concept, DrawingML typeface names are
a free-form string with no implied serif/fixed-pitch/symbolic classification), falling back to
`Fonts.SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic)` when
no installed font matches. This is the default `fontResolver` delegate `ResolveTextLayout` is
driven with in production use; tests inject their own synthetic resolver for deterministic,
installed-font-independent assertions.

#### Deferred to a Later Phase (Phase 1d)

The following are explicitly out of scope for Phase 1d, each because it depends on machinery not
yet implemented anywhere in `CanvasNet`, or is a separable refinement with its own, independent
design cost:

- **Bullets and numbering** (`<a:buChar>`/`<a:buAutoNum>`/`<a:buNone>`) - require a separate glyph/
  counter-rendering pass distinct from run-text layout; deferred to a later phase.
- **Full text justification** (`algn="just"`/`"justLow"`) - requires redistributing inter-word
  spacing per line to reach the line's own right margin exactly; normalized to left-alignment
  this phase (see _Property Inheritance_ above).
- **`<a:spAutoFit>` shape-resize autofit** - requires resizing the owning shape itself in response
  to its own text content, a cross-cutting concern beyond this unit's text-layout scope; treated
  as a pass-through (no scaling) this phase (see _Text Layout_ above).
- **Kerning** - `<a:rPr kern="…"/>` and font-pair kerning tables are not consulted; glyph advances
  use each glyph's own unscaled advance width only.
- **Text clipping on overflow** - an overflowing text block is positioned exactly as computed,
  without being clipped to the shape's own bounding box (see _Text Layout_ above).
- **A full per-slide public `Render` API** - this phase delivers the text-layout/rendering
  _primitives_ (`ResolveTextLayout`/`PaintTextLayout`), not a complete shape-tree-walking,
  slide-level rendering entry point; deferred to a later phase alongside full group-shape
  semantics (see _Geometry and Paint (Phase 1c)_'s own deferred-items list above).
