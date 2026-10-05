## PptxDocument

![CanvasNetPptx Structure](CanvasNetPptxView.svg)

<!-- cspell:ignore ooxml pptx navigations hlink srgb xfrm prst cust fmla scrgb patt misrender -->
<!-- cspell:ignore bodyPr pPr rPr txBody txStyles lstStyle defRPr ctrTitle lIns tIns rIns bIns -->
<!-- cspell:ignore algn marL lnSpc spcBef spcAft justLow lnSpcReduction fontScale noAutofit -->
<!-- cspell:ignore normAutofit spAutoFit spcPct spcPts -->
<!-- cspell:ignore srcRect blipFill grpSp grpSpPr nvGrpSpPr cxnSp tblGrid gridCol tblPr tcPr -->
<!-- cspell:ignore lnL lnR lnT lnB pattFill hMerge vMerge gridSpan rowSpan tableStyleId -->
<!-- cspell:ignore graphicFrame graphicData contentPart unrenderable -->
<!-- cspell:ignore autoshape pythonpptx groupshape paintable aiden0z aiden unnamespaced reparenting FAFAF -->
<!-- cspell:ignore bgRef bgFillStyleLst phClr fmtScheme asvg -->
<!-- cspell:ignore tblStyle tblStyleLst wholeTbl tcStyle tcBdr insideH insideV bandRow firstRow -->
<!-- cspell:ignore band1H band2H gridlines -->
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
(see the _Text Layout and Rendering (Phase 1d)_ section below). As of Phase 1e (this release),
the unit additionally reuses the raster codec dispatch pattern established by
`DemaConsulting.CanvasNet.Svg`'s `SvgCodec.ResolveRasterDecoder` to decode a `<p:pic>` shape's
embedded image (see the _Images, Tables, and Shape Tree (Phase 1e)_ section below); no new core
`CanvasNet` type is introduced by Phase 1e beyond what Phase 1c/1d already established.

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
present at all; no deeper per-attribute merging is performed in Phase 1b. For `<p:spPr>` itself
(fill/line resolution), an empty element still counts as present, stopping the fallback for that
category. `<p:txBody>/<a:lstStyle>` is the one named exception (see "Genuine bug 4" below): a
tier's own empty, self-closing `<a:lstStyle/>` (no `<a:lvl1pPr>`..`<a:lvl9pPr>` level-override
child) is walked past rather than "winning", mirroring the same empty-element-skipping precedent
`<a:xfrm>`/geometry resolution already establishes (see "Genuine bug 2" below). The resolved
theme is carried through unchanged as context (`PptxPlaceholderProperties.Theme`) - it is not
itself part of the matching chain, since a theme supplies scheme-level tokens a property fragment
may reference, rather than containing placeholder-shaped XML to match against.

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
group to its children, and group-level effects - remain deferred to a later phase (see
_Deferred to a Later Phase_ below). The `<p:grpSp>`'s own enumeration/recursion through a slide's
full shape tree (Phase 1b's placeholder parser still only walks a `<p:spTree>`'s _immediate_
`<p:sp>` children, not recursing into `<p:grpSp>`/parsing freeform shapes at all) was deferred to
Phase 1e at the time this phase was written, and is now implemented - see
_Images, Tables, and Shape Tree (Phase 1e)_'s _Shape Tree_ subsection below.

#### Preset Geometry

`PptxPresetGeometry.cs`'s `Build(string prst, float w, float h)` builds the core `Path` for a
named preset geometry (`<a:prstGeom prst="..."/>`), sized to exactly `(0,0)`-`(w,h)` local
coordinates (the caller then applies the shape's resolved `PptxShapeFrame.Transform` via
`Path.Transform(Matrix3x2)` to place it into parent coordinates).

**Supported presets (28)**: `rect`, `roundRect`, `ellipse`, `triangle`, `rtTriangle`, `diamond`,
`parallelogram`, `trapezoid`, `hexagon`, `octagon`, `pentagon`, `chevron`, `homePlate`, `pie`,
`donut`, `plus`, `rightArrow`, `leftArrow`, `upArrow`, `downArrow`, `leftRightArrow`,
`upDownArrow`, `star4`, `star5`, `curvedRightArrow`, `curvedLeftArrow`, `curvedUpArrow`,
`curvedDownArrow`. This set was chosen to cover the small handful of presets
(`rect`/`roundRect`/`ellipse`/`triangle`) PowerPoint itself defaults new shapes to, plus a
practical cross-section of the arrow, star, and other basic-shape categories real-world
presentations most commonly use, per the approved plan's "aim for ~24 total" guidance. The four
curved-arrow presets were added in a later real-world-corpus hardening pass (see _Phase 2: Real-
World Corpus Hardening_'s "Genuine bug 3" entry below) once `curvedUpArrow` was found to crash
rendering of a real-world deck; the remaining three (`curvedDownArrow`/`curvedLeftArrow`/
`curvedRightArrow`) were added alongside it because the existing `Mirror`/`RotateQuarter`
derivation pattern already used for `upArrow`/`downArrow`/`leftArrow` (each derived from
`rightArrow`/`leftRightArrow`) made deriving all four curved arrows from one canonical
`CurvedRightArrow` builder equally cheap, not because they were independently reported.

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
- **`<a:pattFill>`/`<a:blipFill>`** (pattern/picture fill) **as an ordinary shape's own background
  fill** (under `<p:spPr>`, as distinct from a dedicated `<p:pic>` picture shape - see
  _Images, Tables, and Shape Tree (Phase 1e)_ below for the latter, which Phase 1e does
  implement): rejected with `PptxUnsupportedFeatureException` (feature tokens
  `"pptx-pattern-fill"`/`"pptx-picture-fill"` respectively) - implementing either as a shape's own
  _background_ requires tiling/general picture-as-fill machinery still out of scope, and
  resolving either to `PptxNoFill` would silently drop a fill a real presentation shows, which
  this phase's fail-closed philosophy for unsupported-but-declared features rejects in favor of a
  caller-visible, distinguishable exception. Remains deferred to a later phase.

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
- `<a:ln>`'s `w` attribute (width, in EMU) is _explicitly present_ with a non-positive
  (`<= 0`) value (covering both `w="0"` and any negative `w` - OOXML's own "explicit zero/negative
  width means no stroke" idiom); or
- the resolved fill (typically `<a:solidFill>`, via `ResolveFill`) is `PptxNoFill` (an explicit
  `<a:noFill/>` line, or a line with no recognized fill child).

A `w` attribute that is genuinely _absent_ (as opposed to explicitly zero/negative) instead
resolves to a documented default stroke width of `9525` EMU (`0.75`pt), provided the `<a:ln>`
still resolves a recognized (non-`PptxNoFill`) fill. This matches a real-world, commonly seen
document pattern - an `<a:ln>` that declares only a color/fill (for example
`<a:ln><a:solidFill><a:srgbClr val="FF0000"/></a:solidFill></a:ln>`), relying on PowerPoint's own
default stroke weight for an un-set `w` - which previously rendered with no visible outline at
all, collapsing to the exact same "no stroke" outcome as an explicit `w="0"`. The `9525` EMU
(`0.75`pt) value is PowerPoint's own observed default new-shape outline weight; this is an
**application-level convention**, not a formally declared ECMA-376/ISO-29500 XSD schema default -
the `CT_LineProperties` schema type declares no `default="..."` for `w` - so the value is
cross-checked against community/library documentation (for example python-pptx's own documented
default) and this codebase's own existing `1pt = 12700` EMU conversion, rather than cited as a
schema default.

This default-width fallback applies **only** to `ResolveLineStyle` itself (and therefore to every
call site that defers its width/fill decision to it directly - an ordinary shape's own `<a:ln>`
with a recognized fill, table-cell borders, and a `<p:style>/<a:lnRef>` theme style-list entry). It
does **not** change `ResolveShapeLineStyle`'s case-3 "fill-less, width-less `<a:ln>`, no
style-color" behavior, nor `ResolveConnectorLineStyle`'s own per-attribute merge logic - both
deliberately, separately retain their existing "a present, width-less `<a:ln>` never gains a width
from style or default" policy, each independently documented and tested below.

**Dash resolution**: `<a:prstDash val="..."/>` resolves a dash array proportional to the line's
own `widthEmu`, for `dash`/`dashDot`/`dot`/`lgDash`/`lgDashDot`/`sysDash`/`sysDot`; any other
preset name (including the common `solid`, and any name this phase does not recognize) resolves
to `null` (a solid line) - a documented, deliberate cosmetic degradation (an unrecognized dash
preset renders as a solid line rather than throwing) distinct from preset-geometry's fail-closed
philosophy, chosen because a solid line where a dashed one was intended is still a usable, visible
stroke, unlike an entirely unresolved shape.

**Realizing a stroke outline**: `ResolveStrokeOutline(Path shapePath, PptxLineStyle lineStyle,
Matrix3x2 localToSurface)` mirrors the PDF renderer's own stroke-operator call pattern exactly: it
constructs a core `StrokeStyle(lineStyle.WidthEmu, dashArray: lineStyle.DashArray)` and calls the
core `PathStroker.Stroke(shapePath, style, flattenTolerance)` static entry point, producing the
stroked outline path a later rendering phase fills (via `PathFiller.Fill(...,
FillRule.NonZero)`) with `lineStyle.Paint`'s resolved color - reusing the exact same, already-tested
stroking machinery the PDF renderer uses, rather than a second, divergent implementation for this
package. This phase has no rendering surface yet, so only the outline geometry is produced here.

- **Scale-aware flatten tolerance (regression fix).** Unlike `PdfDocument.PaintStroke`, which
  strokes an already device-space-baked path, `ResolveStrokeOutline` strokes `shapePath` in the
  shape's own native, pre-transform local coordinate space (PPTX's own EMU units - typically
  hundreds of thousands to millions per shape), with the caller applying `localToSurface` only
  afterward. Calling `PathStroker.Stroke` with its library default `flattenTolerance: 0.25f`
  against that native EMU-space geometry produced a confirmed regression: a curved shape (for
  example an `ellipse` or `roundRect` preset geometry) flattened to roughly 200x more tessellation
  points than its eventual rendered pixel scale ever needed (around 8,000 points for a typical
  slide-scale ellipse, versus the ~40 actually needed), which in turn triggered a false-positive
  inner-ring collapse in the shared `StrokeOutliner` (see
  `docs/design/canvas-net/drawing/path-stroker.md`'s "Inner-ring collapse" section) - rendering a
  `<a:noFill/>` shape's thin `<a:ln>` outline as a solid-filled interior instead of a thin ring.
  `ResolveStrokeOutline` now derives its own `flattenTolerance` from `localToSurface`'s own scale
  (the larger of its two row-vector lengths, a conservative choice ensuring the more-magnified
  axis still stays within the intended deviation), via a private `ResolveFlattenTolerance` helper
  that returns `0.25f / scale` - keeping the _effective_, post-transform flattening deviation at
  the library's intended ~0.25 device-pixel target regardless of the shape's own native EMU
  magnitude, falling back to the unscaled `0.25f` default when the transform's scale is
  non-finite or non-positive (for example, a degenerate zero-size shape). This both resolves the
  false-collapse regression and incidentally fixes a ~200x stroke-tessellation performance cost
  that affected every stroked PPTX shape, not only the reported bug. The shared `StrokeOutliner`
  tolerance fix above was still made and is the primary defense (it does not depend on this
  caller choosing a sane `flattenTolerance`); this change is a secondary, scale-appropriate
  hardening of the specific caller that originally triggered it. See
  `PptxRenderTests.Render_FullSlideScaleEllipseWithNoFillAndLnMissingWidth_RendersThinRingNotSolidDisc`
  and `PptxRenderTests.Render_FullSlideScaleRoundRectWithNoFillAndLnMissingWidth_RendersThinOutlineNotSolidFill`.

#### Deferred to a Later Phase

The following are explicitly out of scope for Phase 1c, each because it depends on machinery not
yet implemented anywhere in `CanvasNet` (not merely unimplemented in this package):

- **Pattern fill** (`<a:pattFill>`) and **picture fill** (`<a:blipFill>`) **as an ordinary
  shape's own background fill** - require tiling/general picture-as-fill machinery; remains
  deferred to a later phase (Phase 1e implements a dedicated `<p:pic>` picture _shape_, which is a
  distinct construct - see _Images, Tables, and Shape Tree (Phase 1e)_ below).
- **Radial/path gradients** - require a core `RadialGradient`/path-gradient paint type this
  package can reuse; deferred to a later phase.
- **`<a:avLst>` adjustment-value parsing** for preset geometries - deferred to a later phase; see
  _Preset Geometry_ above.
- **Full group-shape semantics** beyond transform composition and shape-tree recursion (style
  cascading, group-level effects) - remains deferred to a later phase. The `<p:grpSp>`'s own
  enumeration/recursion through a slide's full shape tree, listed here as deferred at the time
  this phase was written, is now implemented - see _Shape Tree_ in
  _Images, Tables, and Shape Tree (Phase 1e)_ below.

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
6. **Per-character glyph-coverage fallback**: for each non-whitespace character, the run's own
   primary font (resolved once per run - see step 3 above) is tried first
   (`TrueTypeFont.GetGlyphIndex(ch)`); only when that returns glyph `0` (`.notdef`) is a single
   bundled fallback font (one per `(bold, italic)` pair, memoized the same way the primary-font
   cache is) consulted, and that character alone paints from whichever font actually resolved a
   non-`.notdef` glyph for it. A run is never rebound to the fallback font as a whole - each
   character's resolution is independent, so a character the primary font _does_ cover is
   unaffected by a neighboring character's fallback substitution.
7. One `PptxGlyphPlacement` is emitted per non-whitespace glyph, carrying whichever font (primary
   or fallback) actually resolved that specific character per step 6.

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
no installed font matches. This resolves only a run's **primary** font (one call per distinct
`(FontFamily, Bold, Italic)` combination, before any character is inspected); it is the default
`fontResolver` delegate `ResolveTextLayout` is driven with in production use. Per-character
coverage fallback (when the primary font itself lacks a given codepoint) is a distinct, later
step living entirely in `PptxDocument.TextLayout.cs` - see _Text Layout_ step 6 above - and is
driven by a second, independent delegate (`fallbackFontResolver`, defaulting to
`Fonts.SystemFontCatalog.LoadBundledFallback` with the same `serif: false, fixedPitch: false`
convention, keyed/cached by `(bold, italic)` only, not by family name, since a fallback font is
family-agnostic by design). Tests inject their own synthetic resolvers (for either delegate) for
deterministic, installed-font-independent assertions.

#### Deferred to a Later Phase (Phase 1d)

The following are explicitly out of scope for Phase 1d, each because it depends on machinery not
yet implemented anywhere in `CanvasNet`, or is a separable refinement with its own, independent
design cost:

- ~~**Bullets and numbering** (`<a:buChar>`/`<a:buAutoNum>`/`<a:buNone>`) - require a separate
  glyph/counter-rendering pass distinct from run-text layout; deferred to a later phase.~~ (closed
  by the _Phase 2 Follow-Up: Bullets and Numbering Rendering_ section below.)
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
- **Per-character glyph-coverage fallback is a single-tier, single-font mechanism, not a full
  font-linking/script-itemization pipeline** - a character missing from the primary font falls
  back to exactly one bundled font (step 6 above); DrawingML's own `<a:ea>` (East Asian) and
  `<a:cs>` (complex script) typeface overrides, and a genuine multi-font fallback chain keyed by
  Unicode script/block (as a real font-linking implementation would provide), are not consulted or
  implemented - a character missing from both the primary font and the single bundled fallback
  still paints as `.notdef` (unchanged from pre-fix behavior for that residual case).

A full per-slide public `Render` API was deferred to a later phase alongside full group-shape
semantics and picture/table support, and is now implemented (see _Images, Tables, and Shape Tree
(Phase 1e)_ and _Full Slide Rendering (Phase 1f)_ below).

### Images, Tables, and Shape Tree (Phase 1e)

Phase 1e adds three additive capabilities: raster picture decoding/cropping/compositing for
dedicated `<p:pic>` picture shapes (`PptxDocument.Images.cs`), `<a:tbl>` table structural
parsing/cell-rect computation/painting (`PptxDocument.Tables.cs`), and a full recursive
shape-tree walker recognizing every shape kind this phase supports, including nested
`<p:grpSp>` groups (`PptxDocument.ShapeTree.cs`). Every new member introduced this phase is
`internal` - no public shape/rendering surface is introduced yet (matching Phase 1b/1c/1d's own
precedent). This phase introduces no new core `CanvasNet` dependency beyond what Phase 1c/1d
already established (`Geometry.Path`, `Drawing.PathFiller`, `Canvas.Surface`/`Rgba32`) - raster
image decoding reuses the core codec infrastructure the `DemaConsulting.CanvasNet.Svg` package's
`SvgCodec.ResolveRasterDecoder` already established for embedding a raster image inside another
document format.

**Scope decision**: only a dedicated `<p:pic>` picture shape is in scope this phase for image
rendering. An ordinary shape's own _background_ fill as a picture or pattern
(`<a:blipFill>`/`<a:pattFill>` under `<p:spPr>`/`ResolveFill`, as distinct from a `<p:pic>` shape)
remains out of scope and continues to be rejected by `ResolveFill` exactly as Phase 1c left it
(see _Fill Resolution_ above) - this phase does not touch `ResolveFill` at all.

#### Picture Decoding, Cropping, and Painting

`PptxDocument.Images.cs`'s `ResolvePictureSurface(XElement picElement, PptxDocument document)`
resolves a `<p:pic>` shape's `<p:blipFill>/<a:blip>` into a decoded core `Surface`:

- **Relationship resolution**: `<a:blip>`'s `r:embed` attribute (an embedded-image relationship
  ID) is resolved via the existing `ResolveRelationship`/`GetPartBytes` package-layer primitives
  into the raw media part bytes. `GetPartBytes` bounds the part's decompressed size to
  `MaxPartBytes` (256 MiB), rejecting an oversized/zip-bomb-shaped media part with
  `InvalidDataException` rather than trusting its declared or compressed size. One with only
  `r:link` (an external, non-embedded image
  reference) throws `PptxUnsupportedFeatureException` (feature token `"pptx-image-link"`) - a
  linked image has no embedded bytes this package can read without performing file-system I/O
  outside the supplied package stream, a well-formed-but-deliberately-unsupported construct, not
  malformed data. One with neither `r:embed` nor `r:link`, but carrying only a Microsoft SVG
  extension fallback (`<a:extLst>/<a:ext uri="{96DAC541-7B7A-43D3-8B79-37D633B846F1}">` wrapping an
  `<asvg:svgBlip>` element - the "Insert Icon" SVG-with-no-raster-fallback pattern real PowerPoint
  produces) throws `PptxUnsupportedFeatureException` (feature token `"pptx-image-svg-only"`) -
  also a well-formed, valid OOXML construct this package does not yet decode, not malformed data.
  Only when neither an embed/link attribute nor this recognized extension fallback is present does
  `<a:blip>` throw `InvalidDataException` (genuinely malformed - ECMA-376 requires at least one of
  these).
- **Content-type dispatch**: the resolved media part's content type (via the existing
  `ResolvePart` content-type lookup) dispatches to a private `ResolveRasterDecoder` helper
  mirroring `DemaConsulting.CanvasNet.Svg.SvgCodec.ResolveRasterDecoder`'s own
  content-type-to-decoder dispatch pattern (PNG/JPEG/BMP/GIF, via the core raster codecs already
  used elsewhere in `CanvasNet`) rather than inventing a second, divergent raster-decode entry
  point. An unrecognized content type throws `PptxUnsupportedFeatureException` (feature token
  `"pptx-image-format"`).
- **Crop rectangle**: `ResolveSrcRect(XElement blipFillElement)` resolves `<p:blipFill>`'s
  optional `<a:srcRect>` into a `PptxSrcRect(float Left, float Top, float Right, float Bottom)`
  record (`PptxPicture.cs`) - each edge a `[0,1]`-normalized fraction of the image's own
  full extent, parsed from OOXML's thousandths-of-a-percent `l`/`t`/`r`/`b` attributes (each
  defaulting to `0` - no crop on that edge - when its own attribute is absent), or `null` when
  `<a:srcRect>` itself is absent entirely (meaning "no cropping at all" - the full image).
- **Painting**: `PaintPicture(Surface destination, Surface image, PptxSrcRect? srcRect, Matrix3x2
  shapeTransform, float widthEmu, float heightEmu, Path? clipPath = null)` composes the crop
  rectangle's own crop-to-unit-square mapping with `shapeTransform` into a single
  image-to-surface matrix, inverts it once, and for every destination pixel whose inverse-mapped
  coordinate falls within `[0,1]x[0,1]` samples the source image with nearest-neighbor filtering
  (no bilinear/anti-aliased resampling this phase - a documented simplification, consistent with
  this package's existing "no anti-aliasing yet" posture established by `PaintTextLayout`'s own
  glyph-ink fill). Each sampled source pixel is alpha-blended "over" the existing destination
  pixel via the shared internal `Canvas.Rgba32.CompositeOver(Rgba32, Rgba32)` helper (standard
  Porter-Duff "over" alpha compositing - straight/unassociated alpha in and out, `outA = fgA +
  bgA * (1 - fgA)`, each color channel `outC = (fgC * fgA + bgC * bgA * (1 - fgA)) / outA` when
  `outA != 0` else `0`, round-half-away-from-zero, clamped to `[0, 255]`) rather than overwritten
  outright - a fully or partially transparent source pixel therefore lets the existing destination
  content show through correctly instead of being replaced by whatever RGB value happens to be
  stored alongside that non-opaque alpha (a real-world-confirmed bug fixed after this phase first
  shipped: a logo PNG with an unassociated-alpha white matte around its letters previously painted
  a solid white rectangle instead of a transparent background). This same helper is reused, not
  duplicated, by `DemaConsulting.CanvasNet.Pdf`'s own `CompositeImageOntoSurface` (see that
  package's own design documentation) - exposed cross-assembly via this package's own
  `InternalsVisibleTo` grant from `DemaConsulting.CanvasNet`. A singular (non-invertible)
  `shapeTransform` (for example a zero-area shape frame) paints nothing, rather than throwing or
  dividing by zero. The optional `clipPath` parameter (default `null`, preserving the original
  unclipped behavior) clips the painted image to the picture's own resolved non-rectangular
  preset/custom geometry - see "Phase 2 Follow-Up: Picture Preset-Geometry Clipping" below for the
  full rationale.

**Deliberate divergence from the PDF renderer**: `PaintPicture` does **not** apply the `(1 - v)`
row flip `DemaConsulting.CanvasNet.Pdf`'s own image-painting code applies. PDF's content stream
coordinate space is y-up, so painting a top-down-row-ordered decoded image there requires
flipping its sampled V coordinate; this package's shape-local/surface space is already y-down
(matching every other transform in this unit - see _Shape Frame Transform_ above), so the image's
own top-down row order already matches the surface's own row order with no flip needed. Applying
the PDF renderer's flip here, by copy-paste habit, would paint every picture upside down - this
is a deliberate, documented decision, not an oversight. Both renderers' per-pixel alpha-blending
behavior is otherwise identical (see above).

#### Table Parsing

`PptxDocument.Tables.cs`'s `ParseTable(XElement graphicFrameElement, PptxTheme theme)` parses a
`<p:graphicFrame>`'s `<a:graphic>/<a:graphicData>/<a:tbl>` into a `PptxTable` (`PptxTable.cs`):

- **Graphic-frame kind dispatch**: a `<a:graphicData>` whose `uri` attribute does not end in
  `"/table"` throws `PptxUnsupportedFeatureException` (feature token
  `"pptx-graphic-frame-kind"`) - a `<p:graphicFrame>` can declare other graphic data kinds (for
  example an embedded chart or OLE object), each a well-formed-but-deliberately-unsupported
  construct this phase does not implement. A missing `<a:graphicData>`/`<a:tbl>` throws
  `InvalidDataException` (structurally malformed).
- **Column grid**: `<a:tblGrid>/<a:gridCol>` elements resolve `PptxTable.ColumnWidthsEmu` (each
  `w` attribute, in EMU); a missing `<a:tblGrid>` or a non-numeric `w` throws
  `InvalidDataException`.
- **Rows**: each `<a:tr>` resolves a `PptxTableRow(HeightEmu, Cells)`; a missing/non-numeric `h`
  attribute throws `InvalidDataException`.
- **Cells**: `ParseTableCell(XElement tcElement, PptxTheme theme, float widthEmu, float
  heightEmu)` resolves each `<a:tc>` into a `PptxTableCell`, reading `gridSpan`/`rowSpan`
  (defaulting to `1`) and `hMerge`/`vMerge` (defaulting to `false`) attributes - each
  merge-continuation placeholder cell (`hMerge`/`vMerge` set) is still structurally parsed (it is
  a required, well-formed `<a:tc>` entry per ECMA-376), just excluded from
  `ResolveCellRects`'s own output (see below). A non-numeric `gridSpan`/`rowSpan` throws
  `InvalidDataException`.
- **Cell fill/border/text reuse**: a cell's `<a:tcPr>` fill (via the already-verified Phase 1c
  `ResolveFill`), left/right/top/bottom line styles (`<a:lnL>`/`<a:lnR>`/`<a:lnT>`/`<a:lnB>`, via
  the already-verified Phase 1c `ResolveLineStyle`), and `<a:txBody>` text body (via the
  already-verified Phase 1d `ParseTextBody`) are each resolved **verbatim, unmodified** -
  deliberately reusing the exact same resolvers an ordinary shape's own fill/border/text use,
  rather than maintaining a second, divergent resolution path solely for table cells.

**Signature deviation from the original plan**: the plan's one-line signature summary for
`ParseTable` omitted a `theme` parameter; this implementation adds it (`ParseTable(XElement
graphicFrameElement, PptxTheme theme)`), since `ParseTableCell`'s own fill resolution
(`ResolveFill`) requires a `PptxTheme` to resolve theme-referenced colors - the plan's own
detailed prose already implied this requirement even though its one-line signature summary did
not spell it out explicitly.

#### Cell-Rect Resolution

`ResolveCellRects(PptxTable table)` resolves each of a table's non-merge-continuation cells (a
cell with neither `hMerge` nor `vMerge` set) into a `PptxResolvedTableCell` carrying its own
EMU-space rectangle (`X`, `Y`, `Width`, `Height`), by walking each row's `<a:tc>` entries in
document order while accumulating a running column offset.

**Merge-continuation column-advancement rule**: each `<a:tc>` XML entry - whether a real cell or
an `hMerge`/`vMerge` continuation placeholder - represents (in the common, well-formed case) one
physical grid column; a `gridSpan="N"` cell is typically followed by `(N-1)` separate continuation
`<a:tc>` entries, each itself a single-column-wide placeholder. The per-iteration running column
offset therefore normally advances by that **single column's own width**
(`table.ColumnWidthsEmu[columnIndex]`), **never** by the governing cell's own full merged-span
width - only the surviving (non-continuation) cell's own stored rectangle width uses the full
merged-span sum (`SumColumnWidths(..., cell.GridSpan)`). Advancing by the merged width instead
would double-count the columns already covered by that cell's own trailing continuation entries,
corrupting every subsequent cell's computed left edge in the same row - a defect caught and fixed
during this phase's own test-driven implementation (see `ResolveCellRects_HorizontalMerge_
ComputesSpannedWidth`).

**Corrected invariant - a schema-permitted gap, not a guarantee (Review Follow-Up)**: the
preceding paragraph's original wording overstated ECMA-376's own actual guarantee. `CT_TableRow`
declares its `tc` children as `minOccurs="0" maxOccurs="unbounded"` - the schema does **not**
require a `gridSpan="N"` cell's `(N-1)` continuation placeholders to actually be present in the
XML at all; a producer could omit them (or emit fewer than expected), and a conformant consumer
must still render something sensible rather than corrupting every later cell's position in the
row. `ResolveCellRects` now walks each row by index and, for every governing (non-continuation)
cell, counts how many of the actually-present following entries are its own `hMerge`
continuations (bounded by the cell's own declared `GridSpan - 1`); any shortfall between that
actual count and the expected count is compensated defensively - both the running `xEmu` offset
and `columnIndex` are advanced past the missing placeholders' own column widths before resuming
the per-iteration loop - so a malformed row can no longer cause a later, unrelated cell to overlap
the governing cell's own merged region. This mirrors the file's existing clamp-don't-throw
convention elsewhere in this unit: a structurally-sparse row degrades to a best-effort, visually
sensible layout rather than throwing (see
`ResolveCellRects_GridSpanMissingHMergePlaceholder_CompensatesColumnAdvance`).

#### Table Painting

`PaintTable(Surface destination, PptxTable table, PptxTheme theme, Matrix3x2 shapeTransform,
Func<string, bool, bool, TrueTypeFont?> fontResolver)` paints each resolved cell rectangle in
turn:

- **Fill**: the cell's own resolved `PptxPaint` (solid or gradient) is painted via the core
  `PathFiller`, after transforming the cell's own `Path.Rectangle` through `shapeTransform` (a
  `PptxNoFill` cell paints no fill pixels at all).
- **Border**: each of the cell's four resolved `PptxLineStyle`s (left/right/top/bottom, each
  independently nullable) is stroked via the already-verified Phase 1c `ResolveStrokeOutline`
  pattern, one side at a time.
- **Text**: the cell's own text body (when present) is painted via the already-verified Phase 1d
  `ResolveTextLayout`/`PaintTextLayout` pipeline, constrained to the cell's own rectangle
  dimensions exactly as an ordinary shape's own text body would be.

An empty cell (no fill, no border, no text) paints no pixels at all - a common, valid table shape.

#### Shape Tree

`PptxDocument.ShapeTree.cs`'s `ParseShapeTree(XElement spTreeOrGroupElement, Func<PptxTheme>
themeResolver)` recursively parses a `<p:spTree>` (or, for a nested group, a `<p:grpSp>`'s own
direct children - **not** wrapped in a nested `<p:spTree>`, per ECMA-376's `CT_GroupShape`
content model, an assumption the plan flagged as "likely, not fully verified" and this phase's
own synthetic test fixtures (`PptxGroupsTests.cs`) now directly encode and exercise) into a
closed `PptxShapeTreeNode` hierarchy (`PptxShapeTree.cs`):

- **`<p:sp>` → `PptxSpShapeNode`**: resolves its placeholder, if any, via
  `PptxPlaceholderParser.TryParsePlaceholder` - a pure extract-method refactor of Phase 1b's own
  per-`<p:sp>` placeholder-detection logic (previously inline in `ParsePlaceholderShapes`, now a
  standalone, independently callable helper), with no behavior change to the existing
  `PptxSlide.Placeholders` flat list (confirmed by re-running the full pre-existing test suite
  immediately after the extraction, before writing any Phase 1e code that depends on it).
- **`<p:pic>` → `PptxPictureShapeNode`**: the raw element only - `ResolvePictureSurface`/
  `PaintPicture` are invoked later, by a future rendering pass, not eagerly here (unlike a
  table's own eager `ParseTable` - decoding image bytes eagerly for every picture in a shape tree
  that might never be rendered would be wasteful, whereas a table's own structural parse is cheap
  pure-XML work).
- **`<p:graphicFrame>` → `PptxGraphicFrameShapeNode`**: eagerly parses its table via `ParseTable`.
- **`<p:grpSp>` → `PptxGroupShapeNode`**: resolves its own child-coordinate-space transform via
  the already-verified Phase 1c `ResolveGroupChildTransform` (reading its `<p:grpSpPr>/<a:xfrm>`
  element, throwing `InvalidDataException` when absent) and recurses `ParseShapeTree` on the same
  group element for its own children.
- **Anything else** (`<p:nvGrpSpPr>`, `<p:grpSpPr>`, `<p:contentPart>`, or any other unrecognized
  element kind) is **silently skipped** - a tree-walk tolerance, not a fail-closed feature
  rejection, matching the existing Phase 1b precedent of silently excluding non-placeholder
  shapes from the flat placeholder list. (`<p:cxnSp>` is recognized and dispatched to its own
  `PptxConnectorShapeNode`, not silently skipped - see the _Phase 2 Follow-Up: Connector Shape
  Rendering_ section below.)

**Deferred, lazy theme resolution**: a `<p:graphicFrame>`'s table needs a `PptxTheme` to resolve
its cells' fills (see _Table Parsing_ above), but the overwhelming majority of slides declare no
tables at all. Rather than `GetSlide` eagerly walking its slide's layout → master → theme
relationship chain for every slide, `ParseShapeTree` accepts a `Func<PptxTheme> themeResolver`
invoked lazily, only when a `<p:graphicFrame>` is actually encountered - confirmed by a dedicated
test (`ParseShapeTree_NoGraphicFrame_NeverInvokesThemeResolver`) asserting the resolver delegate
is never called for a shape tree with no tables. `GetSlide` populates the new
`PptxSlide.ShapeTree` field (`IReadOnlyList<PptxShapeTreeNode>`) alongside the existing, unchanged
`PptxSlide.Placeholders` flat list - the inheritance resolver continues to consult the flat list,
not the shape tree.

`PptxMaster`/`PptxLayout` are **not** changed - full freeform shape-tree enumeration for masters/
layouts remains explicitly out of scope (masters/layouts commonly only declare placeholders plus
occasional decorative pictures; extending this is a reasonable, documented future increment).

#### Deferred to a Later Phase (Phase 1e)

The following are explicitly out of scope for Phase 1e, each because it depends on machinery not
yet implemented anywhere in `CanvasNet`, or is a separable refinement with its own, independent
design cost:

- **An ordinary shape's own background fill as a picture or pattern** (`<a:blipFill>`/
  `<a:pattFill>` under `<p:spPr>`/`ResolveFill`, as distinct from a dedicated `<p:pic>` shape,
  which this phase does implement) - requires tiling/general picture-as-fill machinery; `ResolveFill`
  is untouched this phase and continues to reject both with `PptxUnsupportedFeatureException`
  exactly as Phase 1c left it.
- **Picture effects/shadows** - `<p:pic>`'s own `<p:spPr>/<a:effectLst>` is not consulted; only
  the picture's own pixels are painted.
- **Nested tables** - a table cell's own `<a:txBody>` is painted as plain text only; a cell
  containing another `<a:tbl>` is not specially recognized.
- **Table auto-sizing to fit overflowing cell content** - `ResolveCellRects` computes each cell's
  rectangle purely from the table's own declared column widths/row heights; a cell whose text
  content overflows its own declared row height is not given additional vertical space (beyond
  the row height the table itself declares) the way PowerPoint's own auto-grow-row behavior does.
- ~~**Table style/banding** (`<a:tblPr>`'s `<a:tableStyleId>` and first-row/banded-row
  styling)~~ (closed by the _Phase 2 Follow-Up: Table Style/Banding Resolution_ section below -
  `firstCol`/`lastCol`/`lastRow`/corner-cell style parts, column banding (`band1V`/`band2V`), and
  table-style-driven font color remain deferred, as documented in that section).
- ~~`<p:cxnSp>` connector shapes~~ (closed by the _Phase 2 Follow-Up: Connector Shape Rendering_
  section below).
- **Master/layout full shape-tree enumeration** - `PptxMaster`/`PptxLayout` still only expose
  their own flat placeholder lists, not a full `ParseShapeTree` result (see _Shape Tree_ above).
- **Group-level style cascading** - a group's own `<p:grpSpPr>` (for example an inherited line/
  fill style flowing down to a child with no `<p:spPr>` of its own) is not implemented; each
  child shape's own properties are resolved independently of its enclosing group's properties.

A full per-slide public `Render` API was deferred to a later phase and is now implemented (see
_Full Slide Rendering (Phase 1f)_ below).

### Full Slide Rendering (Phase 1f)

Phase 1f adds the public, slide-level rendering API (`PptxDocument.Render.cs`):
`Render(int slideIndex, int width, int height, PptxRenderOptions? options = null)` and a DPI
convenience overload `Render(int slideIndex, float dpi, PptxRenderOptions? options = null)`,
mirroring `DemaConsulting.CanvasNet.Pdf.PdfDocument.Render`'s own public API shape/semantics as
closely as PPTX's different, EMU-based, rotation-free slide geometry model allows. This phase is
purely integration - no new geometry/paint/text/image/table resolution logic is introduced; every
pixel painted is produced by an already-verified Phase 1c/1d/1e resolver or painter.

#### Rendering Options

`PptxRenderOptions` mirrors `PdfRenderOptions`'s own shape: a `sealed` class with a single
`init`-only `BackgroundColor` property (defaulting to opaque white) and a `Default` static
instance, so future rendering options can be added as new properties without a breaking change
to `Render`'s own signature.

#### Base Transform

`Render` builds the EMU-to-pixel base transform directly from the slide's own `SlideSize` (EMU)
and the requested pixel width/height:

```text
baseTransform = Scale(width / SlideSize.WidthEmu, height / SlideSize.HeightEmu)
```

Unlike `PdfDocument`'s own `BuildBaseCtm`, this transform needs no page-rotation handling (PPTX
slides have no rotation concept analogous to a PDF page's own `/Rotate` entry) and no y-axis flip
(PPTX's coordinate space is already y-down, matching this library's own surface convention - see
_Picture Decoding, Cropping, and Painting_'s own "Deliberate divergence from the PDF renderer"
note in the Phase 1e section above for the same point applied to picture compositing).
`SlideSize.WidthEmu`/`HeightEmu` are `long`; both are explicitly
cast to a floating-point type before dividing, so a requested pixel size far smaller than the
slide's own EMU magnitude does not truncate the transform to a zero scale factor via integer
division.

#### Shape-Tree Walk and Per-Node-Kind Dispatch

`Render` resolves the slide's layout/master/theme chain once, eagerly, at the top of the method -
unlike `GetSlide`'s own lazy, invoke-on-demand theme resolution (see _Shape Tree_ in the Phase 1e
section above) - because virtually any shape's own fill or text can reference a theme scheme
color at paint time, not only a table's own cell fills. It then walks the slide's own
`PptxSlide.ShapeTree` (Phase 1e) recursively, in document order, threading an accumulating
`Matrix3x2` transform (starting from the base transform above) through each node:

- **`PptxGroupShapeNode`**: composes its own `ChildTransform` (already resolved by
  `ParseShapeTree` via `ResolveGroupChildTransform` - not re-resolved here) with the accumulated
  parent transform, then recurses into its own `Children` with the composed transform.
- **`PptxSpShapeNode`** (an ordinary or placeholder shape): resolves its effective `<p:spPr>` -
  for a placeholder, via `ResolvePlaceholderProperties` walking the slide → layout → master
  chain; for a freeform shape, directly from its own `<p:spPr>` - resolves its geometry (
  `ResolveShapeGeometry`) and fill/stroke (`ResolveFill`/`ResolveLineStyle`/
  `ResolveStrokeOutline`), paints them, and then, if the shape declares its own `<p:txBody>`,
  resolves and paints its text via `ResolveTextLayout`/`PaintTextLayout`. A placeholder's own
  text content is always read from the slide-level shape element's own `<p:txBody>` - never from
  a layout/master placeholder's own text, which supplies styling only, never content (confirmed
  by a dedicated test, `Render_PlaceholderShape_ReadsTextFromSlideLevelShapeNotLayout`). A
  non-placeholder shape's text resolves its placeholder properties as
  `new PptxPlaceholderProperties(null, null, theme, master.TxStyles)` - the fuller form carrying
  the master's own text styles, distinct from `PaintTable`'s own narrower, cell-scoped
  `new PptxPlaceholderProperties(null, null, theme)`.
- **`PptxPictureShapeNode`**: resolves and composites its embedded image via
  `ResolvePictureSurface`/`ResolveSrcRect`/`PaintPicture` (Phase 1e).
- **`PptxGraphicFrameShapeNode`**: paints its already-parsed `PptxTable` via `PaintTable` (Phase
  1e), using the graphic frame's own direct `<p:xfrm>` child - **not** nested in a `<p:spPr>`
  like an ordinary shape or picture, per ECMA-376's `CT_GraphicalObjectFrame` content model.

A `<p:cxnSp>` connector shape is now represented in the parsed shape tree as its own
`PptxConnectorShapeNode`, with a dedicated `RenderConnector` case in `Render`'s own dispatch (see
the _Phase 2 Follow-Up: Connector Shape Rendering_ section below).

#### Tolerant Skip Policy

A shape, picture, or graphic-frame whose fully-resolved geometry element declares no `<a:xfrm>`/
`<p:xfrm>` anywhere in its own ancestry is **skipped silently**, not treated as an error - a
position-less shape is a genuinely unrenderable (not malformed) construct this phase tolerates,
mirroring `ParseShapeTree`'s own established "tolerant tree walk" precedent (see _Shape Tree_
above). The same policy applies to a `<p:pic>` missing its own `<p:blipFill>`.

#### Deferred to a Later Phase (Phase 1f)

With this phase, the planned PPTX 1.0 feature set is complete. The following remain
unimplemented, carried forward unchanged from Phase 1d/1e's own deferred-items lists (see above):
~~a slide's own `<p:bg>` background fill (no parsing support exists anywhere in this codebase),~~
(closed by the _Phase 2 Follow-Up: Slide/Layout/Master Background Fill_ section below - the single
highest-visual-impact gap left by this phase),
~~`<p:cxnSp>` connector shapes,~~ (closed by the _Phase 2 Follow-Up: Connector Shape Rendering_
section below), nested tables, table auto-sizing/banding, group-level style
cascading beyond transform composition, picture effects/shadows,
~~master/layout full shape-tree rendering,~~
(closed by the _Phase 2 Follow-Up: Master/Layout Decorative Shape Rendering_ section below - a
still-later pass that independently walks and paints each master's/layout's own non-placeholder
shapes), ~~bullets/numbering,~~ (closed by the _Phase 2 Follow-Up: Bullets and Numbering
Rendering_ section below) full text justification, `<a:spAutoFit>` shape-resize autofit,
kerning, and text clipping on overflow. None of these is a currently planned phase; any of them
remaining important is a candidate for a future, corpus-driven hardening pass (`pptx-phase-2`),
not a scheduled increment of this unit's own design.

### Phase 2: Real-World Corpus Hardening

Phase 1a-1f were verified exclusively against small, hand-authored, synthetic `.pptx` packages
(see each phase's own test file). Phase 2 adds no new feature; it instead renders a corpus of ten
real-world `.pptx` files - every slide of every file - and investigates any ungraceful failure
(an unexpected exception, or a silent paint of zero pixels where content is genuinely expected)
discovered against an already-implemented, in-scope Phase 1b/1c/1d/1e/1f feature.

**Corpus sourced**: ten files from two independent, non-synthetic sources - `python-pptx`'s own
MIT-licensed `features/steps/test_files/` integration-test corpus (eight files, each exercising a
specific, named construct: blank slide, autoshape adjustment handles, group shapes, pictures,
tables, font properties, text-frame properties, and a richer multi-construct "shapes" deck) and
two files downloaded from samplelib.com (a blank slide and an eight-slide general-purpose sample
presentation). Licensing: `python-pptx`'s MIT license is reproduced verbatim in
`PptxFixtures/PythonPptx.LICENSE`; samplelib.com's own terms.html states only that downloading its
files locally is permitted, which is a permissive download statement, not a formal copyright or
license grant - see `PptxFixtures/README.md` for the full, file-by-file provenance table and the
exact wording of both statements. Two additional staged candidates
(`pythonpptx-minimal.pptx`/`pythonpptx-mst-placeholders.pptx`) were excluded: both declare zero
slides, so neither can exercise `Render` at all.

**Methodology**: `PptxFixturesCorpusTests.cs` opens each of the ten fixtures, asserts a sane
(positive) slide count and slide size, and then, for every slide, either renders it against a
transparent background and asserts at least one non-transparent pixel was painted somewhere
(proving real content, not a vacuously-true all-background render), or - for the two slides known
in advance to declare chart/SmartArt `<p:graphicFrame>` content - asserts the render instead
throws `PptxUnsupportedFeatureException`, since charts/diagrams remain an explicitly deferred
feature (see the Phase 1e deferred-items list above).

**What was found**: running the full corpus surfaced three ungraceful results, one of which was
an already-correct, merely-undocumented behavior and two of which were genuine, in-scope bugs.

- **Chart-exception-graceful-behavior hypothesis: confirmed.** Both of the two chart/SmartArt-
  bearing slides (`pythonpptx-shp-shapes.pptx` slide index 0 and
  `samplelib-sample-presentation.pptx` slide index 4) throw `PptxUnsupportedFeatureException`
  (feature token `pptx-graphic-frame-kind`) exactly as the pre-existing `ParseTable` uri-check
  logic (Phase 1e) already implements - no source change was needed for this.
- **`pythonpptx-shp-groupshape.pptx` paints zero pixels: confirmed as correct, deferred behavior,
  not a bug.** All four shapes in this fixture declare their fill/line/font exclusively via a
  `<p:style>` shape-style-matrix reference (`<a:fillRef>`/`<a:lnRef>`/`<a:effectRef>`/
  `<a:fontRef>`, each only an `idx` plus a `<a:schemeClr>`), never an explicit `<a:solidFill>`/
  `<a:ln>` inside their own `<p:spPr>`, and every shape's own `<p:txBody>` is empty. Resolving a
  shape-style-matrix reference into a concrete color is explicitly out of scope (see the Phase 1c
  deferred-items list above); `ResolveFill` already gracefully resolves an unrecognized/absent
  fill to `PptxNoFill` rather than throwing. The fixture, as authored, genuinely has zero
  paintable ink under the currently-implemented feature set - the corpus test's own assertion was
  adjusted to match (`PptxDocument_Render_ShpGroupShapeFixture_RendersWithoutError`: renders
  without throwing, no painted-pixel assertion), not the source.
- **Genuine bug 1 - centered/right-aligned text invisible in a `wrap="none"` shape.**
  `pythonpptx-txt-font-props.pptx` slide index 3 (a centered, `wrap="none"` underline demo)
  painted zero pixels. Root cause: `ResolveTextLayout` (`PptxDocument.TextLayout.cs`) used the
  same effectively-infinite width (`float.MaxValue / 4f`, used to suppress word-wrapping for a
  `wrap="none"` shape) for both line-breaking **and** `PositionLines`'s horizontal alignment math.
  For `algn="ctr"`/`algn="r"`, this placed every glyph at an astronomical X offset, far outside
  the shape and the rendered surface - a silent, incorrect-output failure on text alignment, an
  already-implemented, in-scope feature (Phase 1d), not a deferred one. **Fix**: a new
  `alignmentWidth` (`MathF.Max(0f, widthEmu - insetLeft - insetRight)` - the shape's real,
  finite declared width, independent of the wrap setting) is now passed to `PositionLines`
  instead of the infinite wrap-suppression width, which continues to be used only for
  `BuildLines`'s own line-breaking decision. Before: all glyphs of a centered/right-aligned
  `wrap="none"` paragraph positioned off-canvas, painting nothing. After: alignment is computed
  against the same finite width a `wrap="square"` shape would use, identically for both wrap
  modes. Regression test: `ResolveTextLayout_AlignCenter_WrapNone_StillCentersAgainstShapesDeclaredWidth`
  (`PptxTextLayoutTests.cs`).
- **Genuine bug 2 - placeholder geometry/position inheritance broken by an empty, but present,
  `<p:spPr/>`.** `samplelib-sample-presentation.pptx` slide indices 0, 1, and 2 each painted zero
  pixels. Root cause: `ResolvePlaceholderProperties` (`PptxDocument.Inheritance.cs`, Phase 1b)
  resolves a placeholder's effective `<p:spPr>` via a first-non-null-**element**-wins chain
  (slide's own element, then the matched layout's, then the matched master's) - since `??` only
  falls through on a `null` reference, a slide's own empty, self-closing `<p:spPr/>` (declaring
  neither `<a:xfrm>` nor `<a:prstGeom>`/`<a:custGeom>`, and deliberately relying on the
  layout/master for both - extremely common in `.pptx` files not authored by actual PowerPoint,
  exactly this corpus's generator) still "wins" that chain outright, so the layout/master's real,
  populated `<p:spPr>` is never consulted. For `<a:xfrm>`, this caused `RenderShape`'s existing
  "no resolvable `<a:xfrm>` anywhere - skip silently" tolerant-skip policy (see above) to discard
  the whole shape. Fixing only the `<a:xfrm>` half then exposed a second, cascading failure:
  `ResolveShapeGeometry` (Phase 1c) has no equivalent graceful fallback for a missing geometry
  child - it throws `InvalidDataException` - so once the shape stopped being silently skipped, it
  instead crashed. Both `<a:xfrm>` and geometry resolution/position inheritance are Phase 1b/1c
  features, already implemented and already expected to work via inheritance; this is squarely
  in scope, not a deferred-feature gap. **Fix**: rather than changing `EffectiveSpPr`'s own
  element-level semantics (which fill/line resolution continues to rely on unchanged, matching
  the Phase 1c "fill/line inheritance from placeholder/layout/master is a deliberate
  simplification, not resolved at all" design), two new fields were added to
  `PptxPlaceholderProperties` - `EffectiveXfrmElement` and `EffectiveGeometrySpPr` - each
  resolved independently via its own per-tier (slide -> matched layout -> matched master) walk
  that looks specifically for an `<a:xfrm>` child, or a `<p:spPr>` that itself declares an
  `<a:prstGeom>`/`<a:custGeom>` child, respectively, continuing past an empty-but-present
  `<p:spPr>` that lacks one. This mirrors the precedent `PptxDocument.TextInheritance.cs` already
  established for per-attribute (not per-whole-element) run/paragraph property resolution.
  `RenderShape` now resolves its `<a:xfrm>` and geometry `<p:spPr>` from these new fields for the
  placeholder branch (a non-placeholder shape's own `<p:spPr>` is unaffected), and skips the
  shape silently - consistent with the existing tolerant-skip policy - if either is still
  unresolved anywhere in the chain, rather than letting `ResolveShapeGeometry` throw. Before:
  slides 0-2 either rendered nothing (shape skipped) or threw `InvalidDataException`. After: all
  three slides' placeholders inherit their master's standard `<a:prstGeom prst="rect">` geometry
  and their layout's `<a:xfrm>` position/size, and paint visible content. Regression test:
  `Render_PlaceholderShapeWithEmptySpPr_InheritsXfrmAndGeometryFromLayout` (`PptxRenderTests.cs`).
- **Genuine bug 3 - `curvedUpArrow` preset geometry crash, plus a placeholder text-style-bucket
  mis-selection causing a "Section Header" title placeholder to render at roughly 1/3 PowerPoint's
  own font size.** Discovered via a separate real-world-corpus visual comparison against real
  PowerPoint COM-automation ground truth (not the ten-file automated corpus above) on two decks
  not held in this repository.
  - **Crash**: one slide used `<a:prstGeom prst="curvedUpArrow"/>`, which `PptxPresetGeometry.Build`
    did not recognize, throwing `PptxUnsupportedFeatureException` and failing that slide's render
    entirely. **Fix**: added `curvedRightArrow` as a new canonical quarter-annulus-band builder
    (an outer arc sweeping 90&deg;&rarr;0&deg; and an inner arc sweeping 20&deg;&rarr;90&deg;,
    connected by straight lines, producing the arrowhead naturally where the inner arc stops
    short of the outer arc's full sweep), with `curvedLeftArrow` derived via the existing `Mirror`
    helper and `curvedUpArrow`/`curvedDownArrow` derived via a new `CurvedUpOrDownArrow` helper
    composing `RotateQuarter` and `Mirror` - mirroring the existing `upArrow`/`downArrow`/
    `leftArrow` derivation pattern from `rightArrow`/`leftRightArrow` exactly, since the
    architecture already made deriving all four cheaply and naturally. Regression tests:
    `PptxPresetGeometry_Build_CurvedUpArrow_PointsUpwardWithoutThrowing`,
    `PptxPresetGeometry_Build_CurvedArrowSiblings_EachPointsTowardItsOwnDeclaredEdge`
    (`PptxGeometryTests.cs`), `Render_CurvedUpArrowShape_RendersWithoutThrowingAndPaintsInk`
    (`PptxRenderTests.cs`).
  - **Title font-size bug - confirmed root cause and fixed.** A "Section Header"-layout-style
    title placeholder whose slide-level `<p:ph idx="0"/>` omits `type` (a common, valid OOXML
    pattern - the slide placeholder inherits its effective type from the idx-matched layout
    placeholder, per ECMA-376/`python-pptx`'s own matching algorithm) rendered at the master's
    `<p:bodyStyle>` font size (28pt in the reported case) instead of `<p:titleStyle>` (90pt).
    Root cause, empirically confirmed by a synthetic repro built against the unmodified code
    (master `titleStyle` sz="9000"/`bodyStyle` sz="2800", layout placeholder `type="title"
    idx="0"`, slide placeholder `<p:ph idx="0"/>` with `type` omitted): `RenderShape`
    (`PptxDocument.Render.cs`) passed the slide placeholder's own raw, schema-defaulted `Type`
    ("obj" - OOXML's `type`-omitted default) directly into
    `ResolveEffectiveRunProperties`/`SelectMasterTextStyle`'s master-text-style-bucket selection,
    even though `ResolvePlaceholderProperties` (`PptxDocument.Inheritance.cs`) already correctly
    resolves an idx-matched **effective** type for `<a:xfrm>`/geometry inheritance via its
    existing hop-1 (idx-only) match - that correctly-resolved type was simply never reused for
    text-style selection. **Fix**: `PptxPlaceholder` gained a new `DeclaredType` field (the raw
    `<p:ph type="..."/>` value, or `null` when omitted, distinct from `Type`'s schema-defaulted
    value) and `PptxPlaceholderProperties` gained a new `EffectivePlaceholderType` field, computed
    by `ResolvePlaceholderProperties` as the slide placeholder's own `DeclaredType` when present,
    otherwise the idx-matched layout placeholder's `Type`, otherwise the slide placeholder's own
    schema-defaulted `Type` as a last resort. `RenderShape` now passes
    `placeholderProperties.EffectivePlaceholderType ?? placeholder.Type` into text-style
    resolution instead of `placeholder.Type` directly. Before: an omitted-type title placeholder
    resolved to `bodyStyle`. After: it correctly resolves to `titleStyle`, matching an
    explicitly-`type="title"`-declared placeholder's rendering exactly. Verified
    fail-before/pass-after by temporarily reverting the fix and confirming the new test failed
    with the pre-fix (28pt/`bodyStyle`) result before re-applying it. Regression tests:
    `ResolveEffectiveRunProperties_SectionHeaderTitlePlaceholderWithOmittedType_ResolvesMasterTitleStyleNotBodyStyle`,
    `Render_SectionHeaderTitleWithOmittedType_PaintsIdenticallyToExplicitTitleType`
    (`PptxRenderTests.cs`).
  - **Subtitle "invisible sliver" bug - investigated, not reproduced as a bug in isolation.** A
    subtitle placeholder on a different slide rendered as a tiny, essentially unreadable sliver of
    text compared to PowerPoint's ground truth. Two independent hypotheses were investigated with
    synthetic repro cases built directly against the unmodified code: (1) the same placeholder-type
    mis-threading bug above - ruled out, because `SelectMasterTextStyle` already buckets every
    non-`title`/`ctrTitle` type (including both the schema-defaulted `"obj"` and an explicitly
    declared `"subTitle"`) identically into `bodyStyle` - an omitted-vs-explicit `type` therefore
    makes no difference to a subtitle's resolved font size either way, so the title bug's fix
    cannot be the (sole) cause of a subtitle-specific symptom; (2) the attribute-less
    `<a:normAutofit/>` bounded shrink loop (`ResolveAutofitScale`, `PptxDocument.TextLayout.cs`)
    over-shrinking given correct inputs - tested directly with a master `bodyStyle` sz="3200",
    a realistically-sized subtitle box, and content that genuinely fits: the resolved
    `AppliedFontScale` stayed at `1.0` (no over-shrink), refuting this hypothesis for this
    scenario. Neither hypothesis reproduces the reported symptom from a minimal, correctly-formed
    synthetic fixture; the real-world deck's specific box dimensions/content that produced the
    "sliver" are not available in this repository, so the precise trigger could not be isolated
    further in this pass. This is reported as an open, unconfirmed root cause rather than a forced
    fix - the two negative-result repro cases are retained as permanent regression tests
    (`ResolveTextLayout_SubtitlePlaceholderWithAttributeLessNormAutofit_DoesNotOverShrinkContentThatFits`,
    `PptxRenderTests.cs`) both documenting the two mechanisms' confirmed-correct behavior in
    isolation and guarding against a future regression of either.
- **Genuine bug 4 - `<a:lstStyle>` level-override inheritance broken by an empty, but present,
  `<a:lstStyle/>`.** Reported from a real-world deck ("ERF IWF Breadboard Peer Review.pptx", not
  in this repository): a `ctrTitle` placeholder rendered its title at roughly 28pt instead of its
  layout's declared 60pt. Root cause: the exact same structural mechanism as Genuine bug 2, but
  for `<p:txBody>/<a:lstStyle>` instead of `<p:spPr>`/`<a:xfrm>`/geometry - `ResolvePlaceholderProperties`
  resolved `effectiveTxBodyListStyle` via a first-non-null-**element**-wins chain (slide, then
  matched layout, then matched master), so a slide's own empty, self-closing `<a:lstStyle/>` (no
  `<a:lvl1pPr>`..`<a:lvl9pPr>` level-override child at all, deliberately relying on the
  layout/master for level-based run/paragraph overrides) still "won" that chain outright. Because
  `PptxDocument.TextInheritance.cs`'s `GetLevelDefRPr`/`GetLevelElement` index this element by
  level, an empty `<a:lstStyle/>` with no level children caused every level lookup against it to
  silently fail exactly as if the element were entirely absent, so the run's font-size resolution
  fell through past the layout's real 60pt `<a:defRPr>` override to the master's `<p:titleStyle>`
  bucket (28pt) instead. **Fix**: a new `GetTxBodyListStyleWithLevelOverride` helper
  (`PptxDocument.Inheritance.cs`) mirrors `GetSpPrWithGeometry`'s own precedent exactly - it
  returns a tier's `<a:lstStyle>` element only when that element itself declares at least one
  `<a:lvl1pPr>`..`<a:lvl9pPr>` child, walking past a tier's own empty-but-present `<a:lstStyle/>`
  otherwise. The three `effectiveTxBodyListStyle` resolution call sites in
  `ResolvePlaceholderProperties` now call this new helper instead of the unconditional
  `GetTxBodyListStyle`. Unlike Genuine bug 2, no new `PptxPlaceholderProperties` field was needed -
  `EffectiveTxBodyListStyle` itself is now resolved this way directly, since (unlike `<p:spPr>`,
  which fill/line resolution still relies on whole-element semantics for) nothing in this
  codebase depends on `<a:lstStyle>`'s own unconditional, whole-element-presence resolution.
  Before: an empty slide-level `<a:lstStyle/>` silently blocked the layout's real level override.
  After: the layout/master's own level-bearing `<a:lstStyle>` is correctly consulted, while a
  slide-level `<a:lstStyle>` that genuinely declares its own level override still wins (no
  regression to that pre-existing, correct case). Regression tests:
  `PptxDocumentInheritance_ResolvePlaceholderProperties_SlideLstStyleEmptyWithNoLevelOverride_FallsThroughToLayoutLevelOverride`,
  `PptxDocumentInheritance_ResolvePlaceholderProperties_SlideLstStyleHasLevelOverride_SlideWinsOverLayout`
  (`PptxDocumentTests.cs`),
  `ResolveEffectiveRunProperties_CtrTitlePlaceholderWithEmptySlideLstStyle_ResolvesLayoutDefRPrSizeNotMasterTitleStyleFallback`,
  `Render_CtrTitlePlaceholderWithEmptySlideLstStyle_PaintsIdenticallyToAbsentLstStyle`
  (`PptxRenderTests.cs`).
  - **Candidate follow-up (investigated, not fixed in this pass): `effectiveSpPr`'s fill/line
    resolution has the identical structural mechanism.** `effectiveSpPr`'s own three-tier `??`
    chain (`PlaceholderInheritancePerCategory`) has the same "empty element still counts as
    present" behavior for `<p:spPr>`-based fill/line resolution. However, this is not the same bug
    class in effect: `ResolveFill`'s own graceful `fillParentElement is null`-means-`PptxNoFill`
    no-op behavior, and `EffectiveGeometrySpPr`'s own documented "fill/line inheritance from
    placeholder/layout/master is a deliberate, documented Phase 1c simplification, not resolved at
    all" remarks, both confirm per-tier fill/line inheritance for a placeholder was never
    implemented or promised - unlike `<a:xfrm>`/geometry and `<a:lstStyle>` level overrides, which
    the design does promise to resolve via inheritance. An empty `<p:spPr/>` "winning" fill/line
    resolution today produces `PptxNoFill`/no stroke, indistinguishable in effect from the
    already-accepted "fill/line inheritance is not resolved" simplification - there is no
    currently-promised behavior being silently defeated. Recorded here as a candidate follow-up:
    if `effectiveSpPr`-based fill/line inheritance is ever promoted to a promised, per-tier-
    inherited feature (mirroring geometry/`<a:lstStyle>`), the identical empty-element fix pattern
    (`GetTxBodyListStyleWithLevelOverride`/`GetSpPrWithGeometry`'s own precedent) would need to be
    applied there too. No code change made for `effectiveSpPr` in this pass.

**What remains intentionally deferred**: every item already listed in the Phase 1c, 1d, 1e, and
1f _Deferred to a Later Phase_ sections above remains deferred unchanged - this phase fixed two
genuine inheritance/alignment bugs on already-implemented features, confirmed one already-graceful
deferred-feature boundary (chart/diagram graphic frames) against real files, and confirmed one
already-correct deferred-feature boundary (shape-style-matrix fill/line resolution) likewise; it
did not implement, and does not propose implementing, any item from those lists (charts, OLE,
movies, connectors, nested tables, table auto-sizing/banding, group-level style cascading,
bullets/numbering, full text justification, shape auto-fit, kerning, text-overflow clipping,
picture effects/shadows,
pattern/picture shape fill, radial/path gradients, or adjustment-value (`avLst`) geometry
parsing). Slide background fill (`<p:bg>`) is no longer on this list - see _Phase 2 Follow-Up:
Slide/Layout/Master Background Fill (`<p:bg>`)_ below, which closed this gap in a later pass.
Master/layout full shape-tree rendering is likewise no longer on this list - see _Phase 2
Follow-Up: Master/Layout Decorative Shape Rendering_ below, which closed that gap in a
still-later pass. Bullets and numbering rendering is likewise no longer on this list - see
_Phase 2 Follow-Up: Bullets and Numbering Rendering_ below, which closed that gap in a still
later pass. The shape-style-matrix (`<p:style>`'s `fillRef`/`lnRef`) fill/line resolution noted
above as an "already-correct deferred-feature boundary" has since been superseded: it was in fact
a genuine, unimplemented gap (the matrix reference was parsed by nothing at all, rather than
gracefully deferred) - see _Phase 2 Follow-Up: Shape Style References (`<p:style>`)_ below, which
closed it in a still-later pass.

#### Phase 2 Follow-Up: Corpus Growth to Three Sources

A later follow-up pass grew the fixture corpus from ten files across two sources to **twenty**
files across **three** independent sources, adding seven further `python-pptx` files (a slide
background fill, placeholder geometry-inheritance regression coverage, nine unpopulated-
placeholder types, `wrap="none"` + `<a:spAutoFit/>` text, a lone connector plus a picture, and
explicit solid-fill/line-stroke property variety) and three files newly sourced from
`aiden0z/pptx-renderer` (Apache-2.0 licensed, not MIT - see `PptxFixtures/README.md` and
`PptxFixtures/Aiden0zPptxRenderer.LICENSE` for the corrected license finding and full provenance):
a dense org-chart-style slide with a sibling chart slide, an image-crop/background-fill slide, and
a "stale table frame" slide. Every candidate was empirically verified against a built harness
calling `PptxDocument.Open`/`Render` directly before inclusion, the same methodology the original
Phase 2 pass established.

**New confirmed-graceful deferred-feature boundaries found this pass**:

- `pythonpptx-dml-fill.pptx` is the first fixture in this corpus to place a `<a:blipFill>` or
  `<a:pattFill>` directly inside an ordinary shape's own `<p:spPr>` (a picture or pattern used as a
  shape's own background fill, rather than a picture/pattern applied to a `<p:pic>` or table cell).
  `ResolveFill` already throws `PptxUnsupportedFeatureException` for both cases (feature tokens
  `pptx-picture-fill` and `pptx-pattern-fill` respectively) - this pass is the first to exercise
  either exception token against a real file; no source change was needed.
- `aiden0z-table-stale-frame.pptx`'s single table graphic frame declares a `<p:xfrm>` extent that
  does not match the sum of its own `<a:gridCol>` widths - a real-world "stale frame size"
  authoring artifact. Confirmed, by direct code reading of `PptxDocument.Tables.cs`, that column/
  row sizing is derived entirely from `<a:tblGrid>`/`<a:tr h>`, independent of the graphic frame's
  own declared extent, and empirically confirmed this renders and paints without exception - a
  genuine, confirmed-graceful edge case, not a bug.

**No new bugs were found in this pass** - unlike the original Phase 2 pass (which fixed two
genuine inheritance/alignment bugs), every ungraceful-looking result investigated in this
follow-up pass (two chart/SmartArt exceptions, two shape-background-fill exceptions, several
zero-painted-pixel slides) matched an already-implemented, already-documented deferred-feature or
exception-path precedent; none required a source change.

**Seven additional candidates were evaluated and excluded** (see `PptxFixtures/README.md`'s own
"Excluded Staged Candidates" section for full per-candidate rationale): `shp-freeform.pptx` (no
freeform content actually baked into the published file), `shp-autoshape-adjustments.pptx`
(strictly redundant with the already-included `pythonpptx-shp-autoshape-props.pptx`),
`dml-effect.pptx` (its only effect-related element is an empty, no-op `<a:effectLst/>` override),
`mst-shapes.pptx`/`mst-slide-layouts.pptx`/`lyt-shapes.pptx` (each declares zero slides, the same
disqualifying precedent as the original pass's two excluded candidates), and
`aiden0z/pptx-renderer`'s own `docs/example/embedded-font/source.pptx` (near-byte-identical in XML
structure to the included `aiden0z-image-crop-css-reset.pptx`; the font-embedding differentiator
is not observable to CanvasNet, which has no custom font-embedding support).

Every item in this unit's own _Deferred to a Later Phase_ lists (Phase 1c/1d/1e/1f, reproduced
above) remains deferred unchanged after this follow-up pass; this pass grew real-file test
coverage and confirmed additional already-graceful boundaries, it did not implement any
previously-deferred feature.

#### Phase 2 Follow-Up: Slide/Layout/Master Background Fill (`<p:bg>`)

A later follow-up pass closed the single highest-visual-impact gap left unimplemented by Phase 1f
(and carried forward, unchanged, through the two corpus-hardening passes above): a slide's own
`<p:cSld>`/`<p:bg>` background fill. Before this pass, `Render` painted every slide against a
plain, uniform `PptxRenderOptions.BackgroundColor` (opaque white by default) regardless of what
the real `.pptx` file itself declared - the real-world fixture corpus already contained two
independent files (`pythonpptx-sld-background.pptx`, added specifically for this feature, and
`aiden0z-image-crop-css-reset.pptx`) whose slides declare a branded, non-white background that
rendered as plain white before this pass.

**Resolution algorithm** (`PptxDocument.Background.cs`'s `ResolveSlideBackgroundFill`): mirrors
the Phase 1b placeholder-inheritance "first element present at all wins" precedent exactly (see
_Placeholder Inheritance_ above) - a slide's own `<p:bg>` wins outright when present (even an
empty `<p:bg/>`); otherwise its layout's own `<p:bg>` wins; otherwise its master's own `<p:bg>`
wins; when none of the three declare a `<p:bg>` at all, `Render` falls back unchanged to
`PptxRenderOptions.BackgroundColor`, matching this unit's pre-existing behavior exactly for every
file that declares no background anywhere. The resolved paint, when non-null, is painted as a
single full-slide rectangle (`Path.Rectangle(0, 0, SlideSize.WidthEmu, SlideSize.HeightEmu)`,
transformed by the same base EMU-to-pixel transform every shape uses) immediately after the
surface's own `BackgroundColor` clear and immediately before the shape-tree walk begins - so slide
content continues to draw on top of it unchanged.

A `<p:bg>` element declares either `<p:bgPr>` (an explicit fill - resolved by calling the existing
`ResolveFill` directly, since `<p:bgPr>` has the same "fill-definition child element" shape as
`<p:spPr>`, no new fill-resolution logic was written) or `<p:bgRef idx="…">` (a theme
format-scheme style-matrix reference). A `<p:bgRef>`'s `idx` follows ECMA-376's
`CT_StyleMatrixReference` convention: `idx` 0 or 1000 means "no background fill" (resolved to
`PptxNoFill`); `idx` 1-999 indexes the theme's `<a:fillStyleLst>` (the rare, not-used-for-
backgrounds-in-practice half of the style matrix - throws `PptxUnsupportedFeatureException`,
feature token `pptx-bg-fill-style-ref`); `idx` 1001 and above indexes the theme's own
`<a:fmtScheme>/<a:bgFillStyleLst>` (a new `PptxTheme.BgFillStyleList`, parsed by
`PptxDocument.Theme.cs`'s new `ParseBgFillStyleList`, defaulting to an empty list for the
overwhelming majority of themes that declare no `<a:fmtScheme>` at all), 0-based from that offset.
A matched `<a:bgFillStyleLst>` entry is itself parameterized with an `<a:schemeClr val="phClr"/>`
placeholder-color token; `<p:bgRef>`'s own single color-definition child supplies the concrete
substitution value, resolved via the existing `ResolveColor`. Reusing `ResolveFill` for a style-
list entry (which is itself a bare fill-definition element, not a parent containing one) required
wrapping it in a synthetic, unnamespaced parent `XElement` first - adding an `XElement` that
already has a parent as content to a new element clones it rather than reparenting it, so the
theme's own cached, shared tree is never mutated by this (confirmed by a dedicated regression
test, `ResolveSlideBackgroundFill_BgRefResolution_DoesNotMutateThemeBgFillStyleList`).

**`phClr` substitution is a general mechanism, not special-cased to backgrounds**: `ResolveFill`/
`ResolveGradientFill`/`ResolveColor`/`ResolveBaseColor` each gained a new, optional, trailing
`Rgba32? phClrOverride = null` parameter - when supplied and non-null, a `<a:schemeClr
val="phClr"/>` resolves to that value instead of the pre-existing `ResolveSchemeColor(val,
theme.ColorScheme)` lookup (which still resolves `"phClr"` to `Dark1`, unchanged, for every
pre-existing call site that does not supply an override). This is a purely additive, source-
compatible change - every pre-existing call site continues to compile and behave unchanged.

**Fidelity achieved**:

- **Solid-color fills and theme-indexed `<p:bgRef>` fills**: fully supported, including `phClr`
  substitution - the highest-priority, most-common-in-real-decks case this feature targeted.
- **Linear gradient background fills**: best-effort - inherits Phase 1c's own existing linear-
  only boundary (`ResolveGradientFill` already throws `PptxUnsupportedFeatureException` for a
  radial/path gradient; a `<p:bg>`'s own `<a:gradFill>` is resolved via the same `ResolveFill`/
  `ResolveGradientFill` pipeline every shape fill already uses, so this boundary is inherited
  unchanged, not newly introduced).
- **Pattern and picture background fills remain explicitly deferred** - `<a:pattFill>`/
  `<a:blipFill>` inside a `<p:bgPr>` throws `PptxUnsupportedFeatureException` (feature tokens
  `pptx-pattern-fill`/`pptx-picture-fill`), inherited unchanged from `ResolveFill`'s own
  pre-existing boundary (see _Deferred to a Later Phase (Phase 1c)_ above) - no new background-
  specific handling was added for either kind, and none is currently planned.

**Real-file regression coverage**: both `pythonpptx-sld-background.pptx` and
`aiden0z-image-crop-css-reset.pptx`'s own fixture-corpus tests, previously honestly titled/worded
to describe the background as "not painted" (because no `<p:bg>` parsing existed at all), were
extended with pixel-level assertions confirming their own real, solid-color backgrounds (`FF0000`
and `FAFAF9` respectively) now paint correctly - see
`PptxDocument_Render_SldBackgroundFixture_RendersAndPaintsSlideBackgroundFill` and
`PptxDocument_Render_Aiden0zImageCropCssResetFixture_PaintsVisibleContentAndBackgroundFill` in
`PptxFixturesCorpusTests.cs`.

#### Phase 2 Follow-Up: Master/Layout Decorative Shape Rendering

A still-later follow-up pass closed the remaining visual-fidelity gap left by every prior
pass: a slide master's and slide layout's own non-placeholder shapes (pictures, autoshapes,
groups, freeform/custGeom shapes - anything in a `<p:cSld>/<p:spTree>` that is not itself a
placeholder) were parsed only far enough to find placeholder shapes (for the Phase 1b
inheritance chain) and, after the previous pass, to resolve a `<p:bg>` background - their own
decorative shapes were never independently walked or painted. A real PowerPoint deck frequently
places logos, rules, and other decoration directly on a master or layout's own shape tree
(outside any placeholder), so every such file rendered with that decoration silently missing
before this pass.

**Parsing**: `PptxMaster` and `PptxLayout` (`PptxPlaceholder.cs`) each gained a new
`IReadOnlyList<PptxShapeTreeNode> ShapeTree` property (defaulting to `Array.Empty<
PptxShapeTreeNode>()` via the same "redeclare the positional-record property with an `init`
default" pattern already used elsewhere in this unit), populated by `GetMaster`/`GetLayout`
(`PptxDocument.Masters.cs`/`PptxDocument.Layouts.cs`) calling the pre-existing
`ParseShapeTree` - the same shape-tree parser `GetSlide` already used - against each part's own
`<p:cSld>/<p:spTree>`. `GetLayout` resolves its theme lazily (`() => GetTheme(GetMaster(
masterPartPath).ThemePartPath)`), mirroring `GetSlide`'s own existing lazy-theme pattern exactly;
`GetMaster` resolves its theme eagerly, since a master's own theme part path is already resolved
eagerly by the time its shape tree is parsed.

**Rendering order** (`PptxDocument.Render.cs`'s `Render`): between painting the resolved
background (previous pass, unchanged) and walking the slide's own shape tree (pre-existing,
unchanged), two new loops walk the slide's own master's shape tree, then its own layout's shape
tree, each back-to-front in document order - the documented, intended back-to-front order for a
real slide is background, then master decoration, then layout decoration, then the slide's own
content on top. A slide only ever walks its **own** layout's and that layout's **own** master's
shape tree (the same slide -> layout -> master relationship already resolved for placeholder
inheritance and background), never an unrelated layout or master's shapes.

**Placeholder shapes on a master/layout are still never rendered directly**: a master/layout
placeholder shape is real PowerPoint's own "Click to edit…" prompt/edit-mode-only text, invisible
on an actual slide - rendering it directly would paint phantom prompt content no real exported
slide ever shows. `RenderNode` (`PptxDocument.Render.cs`) gained a new `bool
skipPlaceholderShapes = false` parameter, propagated unchanged into every recursive call
(including group children); the two new master/layout loops pass `skipPlaceholderShapes: true`,
while the pre-existing slide-shape-tree loop continues to pass the parameter's own default
(`false`, unchanged slide behavior) - a `PptxSpShapeNode` whose own `Placeholder` is non-null is
skipped outright when the flag is set, every other node kind (picture, autoshape, group,
freeform, graphic frame) is unaffected and dispatches through the exact same per-shape-type paint
functions a slide-level shape already used, so a picture/autoshape/group on a master or layout
renders pixel-for-pixel identically to the same shape declared directly on a slide.

**A latent relationship-resolution bug was fixed as a prerequisite**: `RenderNode`/
`RenderPicture` previously took a `PptxSlide slide` parameter and used `slide.PartPath` to resolve
a `<a:blip r:embed="…">`'s own image relationship - correct only for a slide's own pictures,
since OPC relationships are always scoped to the owning part's own `.rels` file, not the part
that happens to be rendering. Both methods' `PptxSlide slide` parameter was replaced with a
`string ownerPartPath`, threaded through recursively so a master-owned or layout-owned picture
resolves its own `rId` against its own owning part's own `.rels` file, never the slide's. A
dedicated regression test (`Render_MasterNonPlaceholderPicture_PaintsOnEverySlideUsingThatMasterResolvingOwnRels`)
proves this is load-bearing by giving the slide's own `.rels` a deliberately conflicting,
wrong-colored `rId2` and confirming the master's own picture still resolves its own correct
color.

**Already-deferred features throw the same `PptxUnsupportedFeatureException` for a master/layout
shape as for a slide-level shape, but a master/layout shape's own exception is now caught and
only that single shape skipped**: a master/layout decorative shape that itself uses a feature
this codebase does not yet support (an unsupported preset geometry, a chart/OLE graphic frame, an
unsupported raster picture format, and so on) hits the exact same `PptxUnsupportedFeatureException` boundary
a slide-level shape already hits, because both now flow through the same `RenderNode` dispatch.
Unlike a slide-level shape, however, this exception is now caught at the leaf dispatch point
inside `RenderNode` (gated on the existing `skipPlaceholderShapes` flag, which this fix repurposed
to also mean "this is a master/layout decorative-shape walk") and only that one shape is skipped -
rendering continues with the shape's own siblings (including, for a shape nested inside a
`<p:grpSp>` group, its siblings within that same group), the rest of that master's or layout's
own shapes, the subsequent layout/slide walks, and `Render` as a whole completes successfully.
This is a deliberate, newly-introduced containment boundary that applies **only** to
master/layout-owned shapes (`skipPlaceholderShapes == true`) - a slide's own shape throwing the
same exception still hard-fails `Render` exactly as before this fix, unchanged. This containment
was added as a regression fix after real-file testing found that, without it, a single
already-deferred feature used by a decorative master/layout shape (for example an EMF picture)
would abort rendering for every slide using that master/layout - a severe regression versus the
pre-existing behavior where master/layout shape trees were never walked at all. See
`Render_MasterNonPlaceholderPictureWithUnsupportedFormat_SkipsThatShapeAndStillRendersSlideContent`
and `Render_SlideOwnPictureWithUnsupportedFormat_StillThrowsPptxUnsupportedFeatureException` in
`PptxRenderTests.cs` for the regression coverage proving both halves of this boundary.

**Fidelity achieved**:

- A master's own non-placeholder pictures, autoshapes, groups, and freeform shapes now paint on
  every slide that uses that master (regardless of which layout the slide itself uses).
- A layout's own non-placeholder pictures, autoshapes, groups, and freeform shapes now paint only
  on a slide that uses that specific layout.
- Correct back-to-front z-order is preserved: a slide's own content always paints on top of its
  own layout's decoration, which always paints on top of its own master's decoration.
- A master/layout's own placeholder shapes remain invisible, exactly as before this pass -
  this pass only changed non-placeholder sibling shapes.

**Real-file regression coverage**: `aiden0z-1-chart-and-complex.pptx`'s own
`ppt/slideLayouts/slideLayout1.xml` contains a real, non-placeholder, `userDrawn="1"`
`<a:custGeom>` freeform shape named "Freeform 5", exercised by
`PptxDocument_Render_Aiden0zChartAndComplexFixture_Slide0PaintsSlide1ThrowsUnsupportedFeature`
(`PptxFixturesCorpusTests.cs`). This fixture's own `bg1` theme color and its own slide master
background both already resolve to pure white, the same color this freeform shape itself is
filled, so a pixel sample alone cannot show a dramatic visual contrast against the background (the
synthetic tests below carry that contrast proof with deliberately contrasting colors) and would
pass whether or not this feature actually walks the layout's own shape tree. This test's primary,
discriminating proof is therefore structural instead: it resolves the layout directly via
`PptxDocument.GetLayout` and asserts its parsed `ShapeTree` genuinely contains "Freeform 5" as a
`PptxSpShapeNode` with a `null` `Placeholder` - proving this real file's own layout shape tree is
actually parsed and that this shape is recognized as non-placeholder decoration, independent of
its own paint color. The near-white pixel sample within the freeform's own bounding box is kept
only as a secondary, no-throw regression check (a tolerance, not an exact match, because the real
custGeom path is a thin decorative flourish rather than a solid-filled rectangle).

Six new synthetic, pixel-level tests in `PptxRenderTests.cs` carry the primary proof, each with
deliberately contrasting colors so a wrong z-order or a missed shape fails visibly rather than
silently: `Render_MasterNonPlaceholderPicture_PaintsOnEverySlideUsingThatMasterResolvingOwnRels`,
`Render_LayoutNonPlaceholderAutoshape_PaintsOnlyOnSlidesUsingThatLayout`,
`Render_MasterAndSlideShapesOverlap_SlideShapePaintsOnTopOfMasterShape`,
`Render_MasterAndLayoutShapesOverlap_LayoutShapePaintsOnTopOfMasterShape`,
`Render_MasterPlaceholderShape_DoesNotRenderItsOwnPromptContent`, and
`Render_LayoutPlaceholderShape_DoesNotRenderItsOwnPromptContent`.

Two further regression tests cover the graceful-skip containment fix described above:
`Render_MasterNonPlaceholderPictureWithUnsupportedFormat_SkipsThatShapeAndStillRendersSlideContent`
(a master's own EMF picture shape is skipped and the slide's own content still paints) and
`Render_SlideOwnPictureWithUnsupportedFormat_StillThrowsPptxUnsupportedFeatureException` (a
slide's own EMF picture shape still hard-fails `Render`, proving the containment is scoped to
master/layout-owned shapes only).

#### Phase 2 Follow-Up: Bullets and Numbering Rendering

A still-later follow-up pass closed Phase 1d's own longest-standing deferred item (see _Deferred
to a Later Phase (Phase 1d)_ above): a paragraph's own bullet or number glyph
(`<a:buChar>`/`<a:buAutoNum>`/`<a:buNone>`, plus the `<a:buFont>`/`<a:buSzPct>`/`<a:buSzPts>`/
`<a:buClr>`/`<a:buClrTx>` modifiers that style it) was never parsed or painted - a bulleted or
numbered list rendered as plain, un-marked paragraphs, a visually obvious gap against a real
PowerPoint export of the same deck.

**Parsing** (`PptxDocument.Text.cs`'s `ParseBulletProperties`): a paragraph's own `<a:pPr>` (or a
placeholder/master level-override element - see _Resolution_ below) is inspected for four
_independent_ choice-groups, each captured as its own raw `XElement?` on a new
`PptxRawBulletProperties` record (`PptxTextBody.cs`): **type** (`<a:buNone>` / `<a:buAutoNum>` /
`<a:buChar>` - mutually exclusive per ECMA-376's own `EG_TextBulletColor`/`EG_TextBulletTypeface`/
`EG_TextBulletSizeGroup`/`EG_TextBullet` choice groups), **color** (`<a:buClrTx>` / `<a:buClr>`),
**font** (`<a:buFontTx>` / `<a:buFont>`), and **size** (`<a:buSzTx>` / `<a:buSzPct>` /
`<a:buSzPts>`). Capturing each choice-group's winning raw element separately (rather than parsing
straight to a single resolved value) is what lets resolution treat each group as its own
independent inheritance chain - see below.

**Resolution** (`PptxDocument.TextInheritance.cs`'s `ResolveEffectiveBulletProperties`): reuses
this unit's own established attribute-level inheritance pattern (own paragraph -> placeholder
level element -> master level element -> hard-coded default) _four times_, once per choice-group,
exactly mirroring how `algn`/`marL`/`indent` are each already resolved independently in
`ResolveEffectiveParagraphProperties`. This is deliberate and load-bearing: a real-world paragraph
commonly declares only its own bullet _character_ while relying on its placeholder/master level
style for the bullet's color/font/size (or vice versa) - resolving bullets as one monolithic
"whichever tier's whole bullet block wins" object, copied wholesale from a single tier, would
silently defeat that common partial-override pattern. A `Kind == None` result (explicit
`<a:buNone>`, or no bullet markup declared anywhere in the chain - the conservative default)
short-circuits color/font/size resolution entirely, returning `PptxEffectiveBulletProperties.
CreateNone`.

`<a:buAutoNum>`'s `type` attribute defaults to `"arabicPeriod"` and `startAt` to `1` per ECMA-376's
own schema defaults, in `ResolveBulletType`. The three "follow text" sentinels - `<a:buClrTx>`,
`<a:buFontTx>`, `<a:buSzTx>` (and, identically, no color/font/size markup at all) - resolve to the
paragraph's own _first run's_ already-resolved effective color/typeface/size (falling back to the
theme's `Dark1`/the placeholder-type default typeface/the hard-coded default font size for a
run-less paragraph), which required `ResolveEffectiveParagraphProperties` to grow a new, optional
`firstRunProperties` parameter so its caller (`ResolveTextLayout`) can resolve a paragraph's first
run's properties before its own paragraph properties, reversing their previous resolution order
without changing any pre-existing call site's behavior (every pre-existing caller omits the new
parameter and gets `null`, the previous, bullet-free behavior). `<a:buSzPct val="…">` resolves to
that `val/100000` fraction of the same "follow text" base size; `<a:buSzPts val="…">` resolves to
an absolute size (hundredths of a point, the same convention as `<a:rPr sz="…">`), independent of
the run's own size entirely.

**Auto-number formatting** (new file `PptxDocument.Bullets.cs`'s `FormatAutoNumber`): formats a
1-based counter value into the bullet string for eleven of ECMA-376's `ST_TextAutonumberScheme`
values - `arabicPeriod`/`arabicParenR`/`arabicPlain`, `alphaLcPeriod`/`alphaUcPeriod`/
`alphaLcParenR`/`alphaUcParenR` (base-26, spreadsheet-column-style, so value 27 formats as
`"aa."`/`"AA."`), and `romanLcPeriod`/`romanUcPeriod`/`romanLcParenR`/`romanUcParenR` (standard
subtractive-notation Roman numerals). An unrecognized/unimplemented scheme (ECMA-376 defines over
twenty, including several circled/parenthesized-digit Unicode-glyph schemes with no straightforward
ASCII-keyboard-font rendering) returns `null` - a deliberate, graceful per-bullet degradation,
matching this codebase's own established convention for an optional, nullable-returning resolver
(`GetFontSizeEmu`, `ParseLineSpacing`, and so on) rather than throwing
`PptxUnsupportedFeatureException` (reserved for a whole-render-aborting unsupported construct):
only that one paragraph's bullet glyph is silently omitted, its own run text, and every other
paragraph's bullet, render normally.

**Auto-number counter sequencing** (`PptxDocument.TextLayout.cs`'s `BuildLines`, new private
`AdvanceBulletCounters`): a small per-call (that is, per-shape/text-body) state machine tracks one
running counter and one "last scheme used" value per indent level (`0`-`8`, mirroring this unit's
own existing `MaxParagraphLevel` bound). For each paragraph, in level order: every level _strictly
deeper_ than the paragraph's own level is reset to zero (closing out any nested list once the
list returns to a shallower level); a `None` or `Char` bullet resets and clears its own level's
counter (breaking any numbered run a literal bullet character interrupts); an `AutoNum` bullet
resets to its own `startAt` the first time that level is used with that particular scheme (or
after an intervening paragraph at that level used a _different_ scheme, or after any deeper level
was closed and reused), otherwise increments by one. This reproduces real PowerPoint's own
behavior for a list that returns to a shallower level after a nested sub-list: the shallower
level's own sequence resumes where it left off, while the (now-closed) deeper level restarts from
its own `startAt` the next time a paragraph uses it.

**Hanging-indent fix** (`PptxDocument.TextLayout.cs`'s `PositionLines`): a previously-undocumented,
closely-related defect, found and fixed alongside the bullet glyph itself because both are needed
together to be visually indistinguishable from PowerPoint - a bulleted paragraph's first line
incorrectly placed its own run text at the full `marL + indent` first-line position (the same
rule a non-bulleted paragraph's first line correctly uses), rather than at `marL`. ECMA-376's own
convention (and every real PowerPoint export) treats `indent` as the _hanging-indent gutter
width_ reserved for the bullet glyph alone, not as an additional first-line indent applied to the
text itself once a bullet occupies that gutter - the bullet glyph paints at `marL + indent` (the
gutter) while the paragraph's own text, even on its first line, starts flush at `marL`, exactly as
every other line of that same paragraph already does. This fix is strictly gated on "this line
has a resolved, non-empty bullet glyph list" (`IsFirstLineOfParagraph && BulletGlyphs.Count > 0`):
a non-bulleted paragraph's first-line indent continues to apply to its own text exactly as
before this phase, unchanged.

**Fidelity achieved**:

- `<a:buChar>` literal-character bullets, `<a:buAutoNum>` auto-numbered bullets (eleven common
  schemes), and explicit `<a:buNone>` suppression all parse, resolve through the full
  placeholder/master inheritance chain, and paint.
- Each of the four bullet choice-groups (type/color/font/size) resolves independently, so a
  paragraph may override only one of the four while inheriting the other three from its
  placeholder/master level style - the same partial-override fidelity this unit's run/paragraph
  property resolution already provides.
- The hanging-indent gutter fix above ensures a bulleted paragraph's own text aligns correctly
  relative to its bullet glyph, matching PowerPoint's own rendering.
- An unrecognized `<a:buAutoNum>` scheme gracefully omits only that one paragraph's bullet glyph,
  never aborting the render.

**Scoped limitations, left as explicit, documented simplifications**:

- Bullet glyphs are never bold or italic (`BuildBulletGlyphs` always resolves its font with
  `bold: false, italic: false`), regardless of the paragraph's own run formatting - a simplifying,
  visually minor deviation real PowerPoint itself rarely exercises for bullet glyphs in practice.
- A bullet glyph is always anchored at the `marL + indent` gutter, independent of the paragraph's
  own horizontal alignment (`ctr`/`r`) - bullets are not re-justified for centered or
  right-aligned paragraphs, a real but rare combination in practice (bulleted lists are
  overwhelmingly left-aligned in real decks).
- Nine of ECMA-376's twenty-plus `ST_TextAutonumberScheme` values remain unimplemented (see
  `FormatAutoNumber`'s own remarks for the full list) - each gracefully omits its own bullet glyph
  per-paragraph rather than aborting the render, per the graceful-degradation policy described
  above.
- **Closed risk (found during visual QA, fixed over two cycles)**: `"sldNum"`/`"dt"`/`"ftr"` field
  placeholder types (slide number, date, and footer) were, for a brief window, susceptible to a
  stray, isolated bullet glyph with no accompanying text whenever a slide master's `bodyStyle`
  declared a bullet for the inherited level - confirmed by independently rendering the
  `aiden0z-1-chart-and-complex.pptx` corpus fixture, which showed exactly this defect at its own
  slide-number placeholder's geometry. Root cause: `SelectMasterTextStyle` routes every
  non-title placeholder type, including these three field types, to the master's `bodyStyle`
  bucket (which commonly declares a list bullet) rather than the bullet-free `otherStyle` bucket
  these types should consult in genuine PowerPoint output - a separate, pre-existing,
  out-of-scope routing defect that also drives font/size/bold/italic/color resolution for these
  placeholder types and was therefore left unchanged. The first-cycle fix was an unconditional
  early-return guard in `ResolveEffectiveBulletProperties` that forced `PptxBulletKind.None`
  whenever `placeholderType` was `"sldNum"`, `"dt"`, or `"ftr"` - this closed the common-case
  defect but, because it short-circuited before the paragraph's own `<a:pPr>` was ever consulted,
  also incorrectly suppressed a paragraph's own explicit `<a:buChar>`/`<a:buAutoNum>` override on
  these same placeholder types, a regression caught by a follow-up quality review. The corrected,
  narrower fix instead applies a **master-tier type exclusion** solely within the TYPE
  choice-group's own `raw ?? placeholder ?? master ?? default` chain: the master text-style
  bucket's bullet-type element is skipped for these three placeholder types only, while the
  paragraph's own `raw.TypeElement` and the placeholder's own level-indexed element are consulted
  exactly as normal and still win whenever present, matching the fact that an own-paragraph
  explicit bullet choice always takes precedence over any style-bucket default, regardless of
  placeholder type, per real OOXML/PowerPoint semantics - while PowerPoint's own UI never exposes
  bullet/list formatting for these field placeholder types, which is why the master's inherited
  bucket is excluded rather than the paragraph's own markup. Status: closed, proven by
  `ResolveEffectiveParagraphProperties_SldNumDtFtrPlaceholderType_SuppressesMasterBodyStyleBullet`
  (the master-tier exclusion still suppresses an inherited-only bullet) and
  `ResolveEffectiveParagraphProperties_SldNumDtFtrPlaceholderTypeWithOwnBuChar_StillResolvesOwnBullet`
  (an own-paragraph explicit override on the same three placeholder types still resolves and
  wins), plus the pre-existing extended `aiden0z-1-chart-and-complex.pptx` fixture assertion (see
  "Test coverage" below), which continues to confirm no regression on the original fix.

**Test coverage**: a new `PptxBulletTests.cs` covers raw parsing (all three type choices plus all
three color/font/size "follow text" vs. explicit modifier choices), inheritance (own-paragraph vs.
placeholder-level vs. master-level wins for each of the four independent choice-groups, explicit
`<a:buNone>` suppressing an inherited bullet, the four choice-groups resolving independently of
each other, `<a:buAutoNum>`'s schema defaults, `<a:buSzPct>`/`<a:buSzPts>` size-modifier
arithmetic, and `"sldNum"`/`"dt"`/`"ftr"` placeholder types applying the master-tier type
exclusion described above - both suppressing an inherited-only master `bodyStyle` bullet and
still resolving an own-paragraph explicit override - the closed-risk regression guards above),
`FormatAutoNumber`'s own direct unit tests (all eleven supported schemes at representative values,
including alphabetic rollover and several Roman-numeral edge cases, plus an unsupported scheme
returning `null`), and layout-level tests via `ResolveTextLayout` (the hanging-indent fix
contrasted against its own pre-existing non-bulleted-paragraph regression case, auto-number
sequencing across consecutive paragraphs, the reset/resume behavior across a nested
then-returned-to shallower level, `<a:buNone>` suppression, and an unsupported auto-number scheme
gracefully skipping only its own bullet). `PptxFixturesCorpusTests.cs`'s own
`aiden0z-1-chart-and-complex.pptx` fixture test gained a further pixel-level assertion confirming
this real file's own "Rectangle 5" shape (two consecutive `<a:buChar char="•">`-bulleted
paragraphs) actually paints visible ink in its own bullet gutter column, distinct from the
surrounding, un-inked inset, plus a second new assertion confirming no stray bullet ink appears at
this same fixture's own slide-number placeholder geometry (the exact real-world location the
closed risk above was found).

**Closed risk (empty/spacer-paragraph bullet suppression)**: `BuildLines` painted a bullet glyph
for any paragraph whose resolved bullet properties were non-`None`, regardless of whether that
paragraph had any `<a:r>` run children at all - a run-less (blank/spacer) paragraph that merely
inherited a list's bullet properties (for example a blank line left between two bulleted items,
or a trailing blank paragraph closing out a list) rendered a stray, isolated bullet glyph with no
accompanying text, a visually obvious defect against real PowerPoint output, which never shows a
bullet next to an empty line. The fix computes `hasRuns = paragraph.Items.Any(item => item is
PptxRunItem)` once per paragraph (not per line) and gates only the glyph itself -
`bulletGlyphs = isFirstLine && hasRuns ? BuildBulletGlyphs(...) : []` - deliberately leaving
`AdvanceBulletCounters`'s own counter-state advancement untouched, so a run-less auto-numbered
paragraph still consumes its own position in the sequence (matching real PowerPoint's own counter
semantics) even though nothing is painted for it. **Known, accepted limitation**: a run whose
text is whitespace-only (for example a single run containing only `" "`) still counts as "has a
run" under this simpler zero-runs rule, so such a paragraph's bullet still paints - PowerPoint's
own true rule (zero _non-whitespace-only_ runs) is not resolved by this fix and remains a
documented, minor gap.

**Closed risk (bullet/text gutter clearance)**: a further, visually similar defect was reported
against real-world content - a `buAutoNum` paragraph appeared to render its auto-number marker
stacked/overlapping its own first word, as if two markers had painted on top of each other.
Investigation (direct code inspection plus numeric probes against the actual pipeline, since
reverted) refuted both of the plausible duplicate-paint/type-precedence hypotheses: there is
exactly one bullet-glyph-painting call site, and the TYPE choice-group's own `raw ?? placeholder
?? master` chain never merges across bullet kinds - a paragraph's own `<a:buAutoNum>` fully
supersedes an inherited master `<a:buChar>` default, never painting both. The actual root cause
was a **bullet-gutter/text-start-X collision** in `PositionLines`: when a bulleted paragraph's
resolved `IndentEmu` was zero (or not negative enough to clear the bullet glyph's own rendered
width), the bullet glyph and the paragraph's own first-line text were painted at the same (or an
overlapping) X coordinate - a positioning defect, not a bullet-type defect, equally capable of
affecting `buChar` and `buAutoNum` bullets alike. Confirmed numerically on a real-world slide's
exact attribute set (`marL="320040"`, `lvl="1"`, no `indent` attribute, a plain non-placeholder
`<p:sp>` TextBox contributing no indent from any placeholder/master tier, `<a:buAutoNum
type="arabicPeriod"/>`): the bullet glyph and the paragraph's own first run glyph resolved to the
identical X coordinate. The fix: `BuildBulletGlyphs` now also returns the bullet string's own
total measured advance width (already computed internally as its running cursor position, simply
surfaced), threaded through a new `LineBox.BulletWidthEmu` field; `PositionLines` then clamps the
bulleted first line's own text-start-X to `MathF.Max(marL, bulletGutterX + BulletWidthEmu)`,
guaranteeing the bullet and the paragraph's own text never share an X range. This clamp is a
no-op whenever the existing gutter already clears the bullet's own width (the already-correct,
sufficiently-negative-indent case), so it introduces no regression there; it does not attempt to
reproduce any additional visual padding PowerPoint's own renderer may add beyond the bullet's own
measured width, a minor, acceptable, documented simplification. Separately, and **not fixed
here**: a visually similar "number squished against text" symptom observed on another real-world
slide was found, on inspection, to not involve `buAutoNum` at all - its "numbers" are literal
typed digits separated from the following word by a literal tab character, and this unit's
`Tokenize`/`MeasureTokenWidthEmu` treat a tab as ordinary collapsible whitespace with no OOXML
tab-stop (`defTabSz`/`tabLst`) expansion; this is a distinct, pre-existing, out-of-scope defect
(tab-stop support), flagged for a separate unit of work rather than folded into this fix (closed by
the _Phase 2 Follow-Up: Default Tab-Stop Expansion_ section below). Status:
closed, proven by `ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth` (exact
numeric clamp proof), `PaintTextLayout_BulletedParagraphWithZeroIndent_BulletAndTextInkDoNotOverlap`
(pixel-level disjoint-ink proof), and
`ResolveTextLayout_MasterBuCharDefault_OwnBuAutoNumOverride_PaintsOnlyAutoNumberMarker` (permanent
regression guard confirming the type-precedence hypothesis remains refuted), alongside the
pre-existing `ResolveTextLayout_BulletedParagraph_TextStartsAtMarLGutterHoldsBullet` and
`ResolveTextLayout_BuNone_PaintsNoGlyphBeyondRunText`, both confirmed unaffected.

**Closed risk (`buAutoNum` gutter-clearance minimum gap)**: a follow-up real-world report against
the same gutter-clearance clamp above found it, while correct for `buChar`, still insufficient for
`buAutoNum`: CanvasNet rendered an auto-numbered marker immediately touching the paragraph's own
first word (e.g. `"1.3 custom tip geometries"`, `"2.QO Reagent durable probe (J18604)"`), whereas
real PowerPoint shows a clear, word-space-sized gap after the marker (e.g. `"1."` then a visible
gap then `"3 custom tip geometries"` - the `"3"` is the paragraph's own typed first word, not part
of the marker). Investigation confirmed `BuildBulletGlyphs` already measures a multi-character
auto-number marker's full string width correctly (refuting a suspected "single-glyph width"
regression) - the actual gap was the prior fix's own explicitly documented simplification: its
clamp reserves a "just touching, zero extra padding" minimum uniformly for every bullet kind,
which the design document above already flagged as not reproducing PowerPoint's own additional
visual padding. The fix: `BuildBulletGlyphs` now also returns a `BulletGlyphsResult.TrailingGapEmu`

- one space character's own advance width, measured in the bullet's already-resolved font/size -
but **only** for `PptxBulletKind.AutoNum` (`0` for `Char`/`None`, leaving the already-correct,
already-verified `buChar` "touching, zero extra gap" behavior unaffected); threaded through a new
`LineBox.BulletTrailingGapEmu` field, `PositionLines`'s existing clamp now folds it in additively:
`MathF.Max(marL, bulletGutterX + BulletWidthEmu + BulletTrailingGapEmu)`. This is a best-effort,
font-metric-derived approximation of PowerPoint's own tab-stop-like spacing, not full OOXML
`defTabSz`/`tabLst` tab-stop support (a distinct, pre-existing, out-of-scope limitation already
noted above) - a reasonable, documented simplification, since a single space's advance width
closely approximates ordinary inter-word spacing without requiring new tab-stop geometry. Status:
closed, proven by the updated
`ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth` (exact numeric clamp
proof, now including the trailing gap term), a new
`ResolveTextLayout_BulletedParagraphWithZeroIndent_AutoNumTwoDigitMarker_TextClearsBulletWidthPlusGap`
(proves the gap is additive to a genuinely multi-character marker's own full width - e.g. `"12."`
- rather than a fixed constant, using a digit-first run text reproducing the exact real-world
ambiguous pattern), and a new
`PaintTextLayout_BulletedParagraphWithZeroIndent_AutoNumMarker_BulletAndTextHaveVisibleGap`
(pixel-level proof of a genuinely blank column range between the marker's own measured-width
boundary and the clamped text start, stronger than mere non-overlap).

#### Phase 2 Follow-Up: Color Map (`<p:clrMap>`/`<p:clrMapOvr>`) Resolution

A further visual-fidelity defect was found and fixed: `ResolveSchemeColor` (`PptxDocument.
Paint.cs`) hard-coded `bg1` -> `Light1`, `tx1` -> `Dark1`, `bg2` -> `Light2`, `tx2` -> `Dark2`,
entirely ignoring the slide master's own required `<p:clrMap>` and any layout/slide `<p:clrMapOvr>`
override - per ECMA-376, these four aliases are an **indirection**, not a fixed mapping: a slide
master's own `<p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" .../>` (the identity mapping,
true of the overwhelming majority of real-world themes) happens to agree with the previous
hard-coded behavior, but a master, layout, or slide that declares a non-identity map - most
commonly a "dark" layout/master variant that inverts `bg1`/`tx1` to `dk1`/`lt1` - was rendered
with every `bg1`/`tx1`/`bg2`/`tx2` scheme color silently wrong.

**Data model** (`PptxTheme.cs`): a new `PptxColorMap(string Bg1, string Tx1, string Bg2, string
Tx2)` record captures a resolved `<p:clrMap>`/effective `<p:clrMapOvr>`'s own four indirection
targets (each one of the theme's twelve canonical slot names, in practice always one of
`dk1`/`lt1`/`dk2`/`lt2`), with a `Default` static instance holding the identity mapping -
deliberately omitting `accentN`/`hlink`/`folHlink`, which a real-world `<p:clrMap>` always maps to
themselves.

**Parsing**: `<p:clrMap>` is a **required** child of `<p:sldMaster>` per ECMA-376's own schema;
`GetMaster` (`PptxDocument.Masters.cs`) now parses it via a new `ParseColorMap` helper, throwing
`InvalidDataException` if the element or any of its four required attributes is missing, and
exposes the result as a new `PptxMaster.ColorMap` property (an optional trailing constructor
parameter defaulting to `PptxColorMap.Default`, so every pre-existing positional
`new PptxMaster(...)` test call site keeps compiling unchanged). `<p:clrMapOvr>` is optional on
both `<p:sldLayout>` and `<p:sld>`; `GetLayout`/`GetSlide` (`PptxDocument.Layouts.cs`/
`PptxDocument.Slides.cs`) capture its raw, unparsed `XElement?` as `PptxLayout.ClrMapOvr`/
`PptxSlide.ClrMapOvr` respectively - deferred, unparsed, because a `<p:clrMapOvr>` can wrap either
an `<a:overrideClrMapping .../>` (an actual override) or an `<a:masterClrMapping/>` (an explicit
"no override at this tier" marker), and only the resolver (below) needs to distinguish them.

**Resolution** (`PptxDocument.Theme.cs`'s new `ResolveEffectiveColorMap`): implements the
documented slide -> layout -> master fallback chain - a slide's own `<p:clrMapOvr>` wins if it
wraps `<a:overrideClrMapping>`; otherwise the layout's own `<p:clrMapOvr>` wins under the same
condition; otherwise the master's own parsed `<p:clrMap>` applies. A `<p:clrMapOvr>` wrapping
`<a:masterClrMapping/>` at either tier is treated as "no override at this tier" and falls through
to the next tier, exactly matching real PowerPoint's own semantics for that marker.

**Threading**: an optional `PptxColorMap? colorMap = null` parameter (defaulting via
`colorMap ??= PptxColorMap.Default` at the top of each method body) was threaded through every
color-resolving entry point this unit already has - `ResolveFill`/`ResolveGradientFill`/
`ResolveColor`/`ResolveBaseColor`/`ResolveLineStyle` (`PptxDocument.Paint.cs`),
`ResolveSlideBackgroundFill`/`ResolveBackgroundElement`/`ResolveBackgroundStyleReference`
(`PptxDocument.Background.cs`), `ResolveEffectiveRunProperties`/`GetRunColor`/
`ResolveEffectiveParagraphProperties`/`ResolveBulletColor` (`PptxDocument.TextInheritance.cs`),
and `ResolveTextLayout` (`PptxDocument.TextLayout.cs`). `ResolveSchemeColor` itself was rewritten
so `bg1`/`tx1`/`bg2`/`tx2` first indirect through `colorMap.Bg1`/`.Tx1`/`.Bg2`/`.Tx2` to their own
target slot name, then resolve that target through a new, non-recursive `ResolveNamedSlot` helper
(the same switch as before, minus the `bg`/`tx` aliasing arms, so a non-identity map can never
recurse back through another alias) - every other slot (`accent1`-`accent6`/`hlink`/`folHlink`/
`dk1`/`lt1`/`dk2`/`lt2`/`phClr`) is unaffected.

**Render-time wiring** (`PptxDocument.Render.cs`): the public `Render` method computes
`ResolveEffectiveColorMap(slide.ClrMapOvr, layout.ClrMapOvr, master.ColorMap)` exactly once per
render call, then threads the result as a new **required** parameter through the private
`RenderNode`/`RenderShape`/`RenderGraphicFrame` helpers and into every nested
`ResolveSlideBackgroundFill`/`ResolveFill`/`ResolveLineStyle`/`ResolveTextLayout`/`PaintTable`
call - safe to make required (rather than optional) since these helpers are `private`/`internal`
and have no external or test call sites that bypass `Render` itself.

**Scoped limitation, left as an explicit, documented simplification**: a table cell's own fill
and border colors (`ParseTableCell`, `PptxDocument.Tables.cs`) are resolved once, eagerly, at
parse/load time - before any per-slide effective color map is known in the general case - so a
cell's `<a:schemeClr>`-referenced colors could always effectively use `PptxColorMap.Default`,
regardless of any real `<p:clrMapOvr>` in effect for that slide. A table cell's own **text**, by
contrast, is not baked at parse time: `PaintTable` calls `ResolveTextLayout` for each cell's text
at paint time, so cell text color correctly resolves through the real, per-render effective color
map like any other text run.

**Narrowed by Review Follow-Up, for slide-owned tables only**: `GetSlide`'s own slide cache
(`_slideCache`, `PptxDocument.Slides.cs`) is keyed 1:1 by slide index - a cached `PptxSlide`'s own
`ShapeTree` is never consumed by any other slide - so it is sound for `GetSlide` to supply
`ParseShapeTree` a lazy `Func<PptxColorMap> colorMapResolver` computing
`ResolveEffectiveColorMap(clrMapOvr, layout.ClrMapOvr, master.ColorMap)` from that slide's own,
already-in-scope inheritance chain. `ParseShapeTree` (`PptxDocument.ShapeTree.cs`) threads the new,
optional `colorMapResolver` parameter through its recursive `<p:grpSp>` self-call and invokes it
lazily - only when a `<p:graphicFrame>` table is actually found - passing the resolved
`PptxColorMap` into `ParseTable`'s own `colorMap` parameter, so a slide-owned table's cell fills
now correctly resolve `<a:schemeClr>` references against that slide's real effective color map,
not always `PptxColorMap.Default`.

`GetMaster`/`GetLayout`'s own part caches, by contrast, are keyed by part path and shared across
every slide that references that master/layout - each of which may have its own, different
`<p:clrMapOvr>` - so a single cached master/layout `ShapeTree` cannot soundly bake in any one
particular slide's effective color map. `PptxDocument.Masters.cs`/`PptxDocument.Layouts.cs`
therefore intentionally omit `colorMapResolver` at their own `ParseShapeTree` call sites, each with
an inline comment explaining why; a **master- or layout-owned table's** own `<a:schemeClr>`-filled
cells remain the one residual, narrower case of this limitation - still effectively
`PptxColorMap.Default`-resolved - left as an explicitly documented, architecturally-larger
follow-up (it would require per-consuming-slide cache keys or fully deferring color resolution to
paint time, mirroring how cell text already works, rather than this fix's narrower, safe
per-slide-cache threading).

**Test coverage**: `PptxPaintTests.cs` gained direct `ResolveSchemeColor`/`ResolveColor` unit
tests proving the default (identity) map's `tx1` -> `Dark1` behavior is preserved, a non-identity
map correctly redirects `tx1` to an overridden slot, and the indirection is applied before any
`<a:lumMod>`/`<a:lumOff>`-style color transform. A new `PptxColorMapTests.cs` covers
`GetMaster`'s required `<p:clrMap>` parsing (including the `InvalidDataException` thrown when
absent), `GetLayout`/`GetSlide`'s optional `<p:clrMapOvr>` parsing, and `ResolveEffectiveColorMap`'s
full slide -> layout -> master fallback chain, including both `<a:masterClrMapping/>`
fall-through cases and the `InvalidDataException` thrown for a malformed
`<a:overrideClrMapping>` missing a required attribute. `PptxRenderTests.cs` gained an end-to-end
render-level pixel test confirming a slide-level `<p:clrMapOvr>/<a:overrideClrMapping bg1="dk1"
.../>` makes an `<a:schemeClr val="bg1"/>`-filled shape actually paint the theme's `Dark1` pixel
color rather than the previously hard-coded `Light1`, alongside a companion test confirming the
pre-existing, no-override baseline behavior is unchanged.

#### Phase 2 Follow-Up: Shape Style References (`<p:style>`)

A further visual-fidelity gap was closed: a shape built from PowerPoint's own "Shape Styles"
gallery (the ribbon gallery that applies a theme-coordinated fill/line/effect/font combination to
a shape without the user ever touching an explicit `<a:solidFill>`/`<a:ln>`) declares that choice
purely via `<p:spPr>`'s sibling `<p:style>` element - `<a:fillRef idx="N">`/`<a:lnRef idx="N">`
indexing the theme's own `<a:fmtScheme>/<a:fillStyleLst>`/`<a:lnStyleLst>`, each entry's own
`<a:schemeClr val="phClr"/>` tokens substituted with the ref's own declared color child. Before
this fix, `<p:style>` was parsed by nothing in this unit at all, so a gallery-styled shape with no
explicit `<p:spPr>` fill/line silently rendered with no fill and no stroke whatsoever.

**Data model** (`PptxTheme.cs`): two new `IReadOnlyList<XElement> FillStyleList`/
`IReadOnlyList<XElement> LnStyleList` properties, each an optional trailing constructor parameter
defaulting to `[]`, mirroring the existing `BgFillStyleList` property exactly (so every
pre-existing positional `new PptxTheme(...)` call site keeps compiling unchanged).

**Parsing** (`PptxDocument.Theme.cs`): new `ParseFillStyleList`/`ParseLnStyleList` private helpers,
parallel to the existing `ParseBgFillStyleList`, read `<a:fmtScheme>/<a:fillStyleLst>` and
`<a:fmtScheme>/<a:lnStyleLst>` respectively into their raw, unparsed child elements in document
order, each tolerant of an absent `<a:fmtScheme>` or absent list (returns `[]`). Wired into
`GetTheme`'s `new PptxTheme(...)` construction alongside the existing `bgFillStyleList`.

**Resolution** (`PptxDocument.Paint.cs`): two new internal resolvers, `ResolveShapeStyleFill`
and `ResolveShapeStyleLineStyle`, reuse the exact phClr-substitution pattern
`ResolveBackgroundStyleReference` (`PptxDocument.Background.cs`) already establishes for
`<p:bgRef>` - resolve the ref's own single color-definition child (when present) via
`ResolveColor`, then pass it as a `phClrOverride` into the matched style-list entry's own
resolution. Unlike `<p:bgRef>`, which indexes `BgFillStyleList` with a `+1000`/`+1001` offset
matrix (a background-specific "other half"), ordinary `<a:fillRef>`/`<a:lnRef>` index
`FillStyleList`/`LnStyleList` **directly, 1-based, with no offset**: `idx="0"` means "none"
(`PptxNoFill.Instance`/`null` respectively); `idx` in `[1,3]` maps to `list[idx-1]`; any other
value - including an `idx` that would index past a theme's own (always-3-entry, or in a
synthetic/test theme, possibly empty) list - throws `InvalidDataException` rather than being
silently clamped, since a real theme's `<a:fillStyleLst>`/`<a:lnStyleLst>` always declares exactly
3 entries. A shared private `ParseStyleRefIdx` helper parses the required `idx` attribute,
throwing `InvalidDataException` for a missing or non-numeric value (a `PptxDocument.Paint.cs`-
local analog to the inline `idx`-parsing logic `ResolveBackgroundStyleReference` already has -
not refactored, per this fix's minimum-necessary-change scope). `FillStyleList`'s entries are
themselves fill-definition elements (e.g. `<a:solidFill>`), so `ResolveShapeStyleFill` wraps the
matched entry in a synthetic parent element before calling the existing `ResolveFill` (mirroring
`ResolveBackgroundStyleReference`'s own synthetic-wrapper precedent exactly); `LnStyleList`'s
entries are already `<a:ln>`-shaped, so `ResolveShapeStyleLineStyle` instead feeds the matched
entry directly into a new optional `phClrOverride` parameter added to the existing
`ResolveLineStyle` (threaded into its own internal `ResolveFill` call), with every pre-existing
call site remaining source-compatible since the new parameter is optional and appended last.

**Render-time wiring** (`PptxDocument.Render.cs`'s `RenderShape`): a shape's own `<p:style>` is
read directly from the slide shape's own element (`node.ShapeElement`) - **never** inherited from
a layout/master placeholder, unlike this same method's `<a:xfrm>`/geometry/fill-position
resolution, which is placeholder-aware. This is a deliberate, narrower-scope limitation (see
"Known limitations" below). An explicit fill-definition child on `<p:spPr>` (`<a:noFill>`/
`<a:solidFill>`/`<a:gradFill>`/`<a:pattFill>`/`<a:blipFill>`, detected by a new
`HasExplicitFillChild` helper) always wins over a style `<a:fillRef>` - needed because
`ResolveFill` already collapses "no recognized fill child at all" and "an explicit `<a:noFill/>`"
to the identical `PptxNoFill.Instance` return value, so that collapsed return value alone cannot
distinguish "shape explicitly declared no fill, which wins over any style ref" from "shape
declared nothing at all, so falls back to the style ref".

The line side is resolved by a new `ResolveShapeLineStyle` (`PptxDocument.Paint.cs`), covering
four cases: (1) an explicit fill-definition child on the shape's own `<a:ln>` (including
`<a:noFill/>`) wins outright over the style `<a:lnRef>`, reusing the same `HasExplicitFillChild`
helper and delegating to the existing `ResolveLineStyle` unchanged; (2) is the same check's
`<a:noFill/>` sub-case, which `ResolveLineStyle` already correctly resolves to "no stroke",
distinguishing it from case 3 below; (3) a present `<a:ln>` that declares no recognized
fill-definition child of its own (for example `<a:ln w="76200"/>` - width/dash/cap attributes
only) keeps its own width and dash from that `<a:ln>` verbatim, but defers only its _color_ to the
style `<a:lnRef>` via `ResolveShapeStyleLineStyle`, falling back to "no stroke" when the style
itself supplies no usable color (no `<p:style>` at all, no `<a:lnRef>`, an `<a:lnRef idx="0"/>`,
or a style entry that itself resolves to `PptxNoFill`); (4) a fully absent `<a:ln>` defers
entirely to the style `<a:lnRef>` for both width and color, via `ResolveShapeStyleLineStyle`
unchanged.

Before this fix, the line side used a much simpler, incorrect "is `<a:ln>` present at all" check:
a present `<a:ln>` - even one with no recognized fill child of its own - always won over
`<a:lnRef>` in full, silently discarding the style's color and resolving to "no stroke" for case
3's shapes. This was a genuine visual-fidelity bug: a real-world shape commonly declares an
`<a:ln>` that carries only width/dash/cap attributes, relying entirely on its `<p:style>/
<a:lnRef>` for its own visible color - exactly PowerPoint's own "Shape Styles" gallery usage
pattern for an outline-only style. `ResolveShapeLineStyle`'s case-3 "no style color available ->
no stroke" default is not a newly invented behavior: it is the exact, pre-existing default every
other fill-less-line call site in this unit already produces (table-cell borders in
`PptxDocument.Tables.cs`; `ResolveShapeStyleLineStyle` itself consulting an absent/`idx="0"`
`<a:lnRef>`), and it mirrors `ResolveConnectorLineStyle`'s own already-shipped paint-fallback
default (`styleLineStyle?.Paint ?? PptxNoFill.Instance`) for its connector-specific, broader
per-attribute merge (see "Phase 2 Follow-Up: Connector Shape Rendering" below). `ResolveShapeLineStyle`
is deliberately **narrower** than `ResolveConnectorLineStyle`: only color ever falls back to
style - width and dash always come from the shape's own `<a:ln>` whenever it is present at all,
never merged from style, even when that `<a:ln>` itself declares no `w`/`<a:prstDash>` - connectors
and ordinary shapes are intentionally documented as having different, scoped merge policies.

**Known limitations, left as explicit, documented simplifications**:

- **`<a:fontRef>` is out of scope.** Unlike `<a:fillRef>`/`<a:lnRef>`, its own `idx` is one of
  `major`/`minor`/`none` (selecting the theme's font scheme, not a style-list position) - a
  different resolution mechanism entirely, not merely an extension of this fix's pattern.
- **`<a:effectRef>` is out of scope.** This unit does not resolve shape effects (shadows, glows,
  etc.) at all yet, independent of this fix.
- **Placeholder-inherited `<p:style>` is out of scope.** A placeholder shape that declares no
  `<p:style>` of its own does not inherit one from its matched layout/master placeholder - only
  the slide shape's own, directly-declared `<p:style>` is consulted. Fixing this would require
  extending `PptxPlaceholderProperties`'s existing per-tier inheritance resolution to a fifth
  property, mirroring `EffectiveSpPr`/`EffectiveXfrmElement`/`EffectiveGeometrySpPr` - deferred as
  a candidate follow-up, not required by the reported symptom (a non-placeholder shape using the
  Shape Styles gallery).

**Test coverage**: `PptxPaintTests.cs` gained direct `ResolveShapeStyleFill`/
`ResolveShapeStyleLineStyle` unit tests covering a null/style-ref-less styleElement, `idx="0"`
("none"), the no-offset pin (`idx="1"` resolves list entry `0`, not `1` and not a `+1000`-offset
entry), `phClr` substitution from the ref's own color child, and `InvalidDataException` for an
out-of-range or missing/non-numeric `idx`. `PptxBackgroundTests.cs` gained `GetTheme` parsing
tests for `FillStyleList`/`LnStyleList` (empty-by-default absent any `<a:fmtScheme>`/list, and
parsed in document order when present). `PptxRenderTests.cs` gained end-to-end render-level pixel
tests: a shape with only a `<p:style>/<a:fillRef>` (no explicit `<p:spPr>` fill) renders the
resolved, phClr-substituted style-list color; an explicit `<p:spPr>` fill wins over a
simultaneously-present `<a:fillRef>` on the same shape; a shape with only a `<p:style>/<a:lnRef>`
(no explicit `<a:ln>`) renders the resolved stroke color; and an explicit `<a:ln><a:noFill/></a:ln>`
wins over a simultaneously-present `<a:lnRef>` on the same shape.

`PptxPaintTests.cs` additionally gained direct `ResolveShapeLineStyle` unit tests for all four
cases above - a null `<a:ln>` deferring entirely to `ResolveShapeStyleLineStyle`; an explicit
`<a:solidFill>`/`<a:noFill/>` on the shape's own `<a:ln>` winning outright over a visible style
`<a:lnRef>`; a fill-less `<a:ln w="76200"/>` keeping its own width and dash while adopting the
style's own color; and that same fill-less `<a:ln>` resolving to "no stroke" when no
`<p:style>` element is present at all, and separately when its `<a:lnRef idx="0"/>` resolves to
"no line" - plus an edge case proving a fill-less, width-less `<a:ln/>` never pulls a width from
style either. `PptxRenderTests.cs` gained one further end-to-end render-level pixel regression
test, `Render_ShapeWithLnWidthOnlyNoFillChildAndStyleLnRef_UsesOwnWidthAndStyleColor`, reproducing
the exact reported construct (`<a:ln w="76200"/>`, no fill child, alongside a `<p:style>/<a:lnRef>`
resolving to a distinct color) and asserting, at the pixel level, both that the style's own color
paints the stroke and that the painted band matches the shape's own (narrower) width rather than
the style's own (much wider) line-style-list entry width.

#### Phase 2 Follow-Up: Connector Shape Rendering (`<p:cxnSp>`)

A `<p:cxnSp>` connector shape - the straight/elbow/curved lines PowerPoint draws between other
shapes in flowcharts and diagrams - was, until this fix, never represented in the parsed shape
tree at all (see _Shape Tree_ in the Phase 1e section above): `ParseShapeTree` silently dropped
every `<p:cxnSp>` it encountered, so a slide containing one rendered with that connector simply
absent, no error, no visible line. This closes that gap.

**Shape tree** (`PptxShapeTree.cs`/`PptxDocument.ShapeTree.cs`): a new
`PptxConnectorShapeNode(XElement CxnSpElement) : PptxShapeTreeNode` record, dispatched from
`ParseShapeTree`'s own `<p:cxnSp>` case alongside the existing `<p:sp>`/`<p:pic>`/
`<p:graphicFrame>`/`<p:grpSp>` cases - so a connector's position in document order (and therefore
its z-order among siblings), and its ancestry under a `<p:grpSp>` (and therefore its own group
transform composition), are handled identically to every other shape kind, with no special-casing
anywhere in the tree walk itself.

**Preset geometry** (`PptxPresetGeometry.cs`): ten connector-specific preset names - `line`/
`straightConnector1` (a plain open 2-point diagonal), `bentConnector2`-`bentConnector5` (a
fixed-proportion "staircase" elbow, alternating horizontal/vertical segments starting
horizontal, evenly dividing the available width/height across however many segments run in each
axis), and `curvedConnector2`-`curvedConnector5` (the same elbow vertices rounded via the
existing `CornerRoundEffect.Apply`, radius `Min(|w|,|h|) * 0.15`, with the true start/end
vertices never rounded) - are recognized by a new `IsConnectorPreset` check and built by a new
`BuildConnectorPreset` dispatch. Critically, this check also **bypasses** `Build`'s own
pre-existing "non-positive bounding box renders as `Path.Empty`" guard for these ten preset names
only (every other preset keeps that guard unchanged): a real-world connector frequently declares
a zero-height or zero-width `<a:ext>` (a perfectly horizontal or vertical line), confirmed in the
`pythonpptx-shp-connector-props.pptx` fixture's own second slide, and such a connector must still
paint its line rather than silently vanishing. `Ellipse` (previously `private`, used internally
by `oval`) was changed to `internal` so the new `oval` arrowhead kind (below) could reuse it
without duplicating ellipse-path-construction logic.

**Arrowheads** (`PptxArrowheadStyle.cs`, new; `PptxArrowheadGeometry.cs`, new): a new
`PptxArrowheadKind` enum (`None`/`Triangle`/`Stealth`/`Diamond`/`Oval`/`Arrow`) and
`PptxArrowheadStyle(Kind, WidthKey, LengthKey)` record capture a resolved `<a:headEnd>`/
`<a:tailEnd>` declaration. `PptxArrowheadGeometry.Build(style, lineWidthEmu)` builds the
arrowhead's own **local-space** geometry with its tip fixed at the local origin, pointing along
local +X, body extending toward local -X (the caller orients/translates it later): `Triangle` is
a 3-point closed polygon; `Stealth` a 4-point closed polygon with a concave notch cut into its
back edge; `Diamond` a 4-point closed rhombus straddling the origin; `Oval` reuses
`PptxPresetGeometry.Ellipse`; `Arrow` is an open, stroked (not filled) 2-segment chevron; `None`
is `Path.Empty`. Sizing is relative to the connector's own stroke width
(`lineWidthEmu`): half-width = `lineWidthEmu * 1.5 * SizeScale(widthKey)`, length =
`lineWidthEmu * 3.6 * SizeScale(lengthKey)`, where `SizeScale` maps PowerPoint's own `sm`/`med`/
`lg` size keywords to `0.75`/`1`/`1.5` respectively (a reasonable, documented approximation of
PowerPoint's own visual scaling - no normative EMU/pixel table for these keywords is published).

**Line style and arrowhead resolution** (`PptxDocument.Connectors.cs`, new partial class file):

- `ResolveConnectorLineStyle` merges a connector's own `<a:ln>` with its `<p:style>/<a:lnRef>`
  fallback **per attribute** (width, paint, dash), not as an all-or-nothing choice the way
  `RenderShape`'s own "any present `<a:ln>` wins outright over `<a:lnRef>`" policy works for
  ordinary shapes. This divergence is deliberate and was proven necessary by the real
  `pythonpptx-shp-connector-props.pptx` fixture: its own connector declares a bare
  `<a:ln><a:tailEnd type="arrow"/></a:ln>` - no width, no fill - relying entirely on its sibling
  `<p:style>/<a:lnRef idx="2">` for its actual visible color and width. Under the ordinary
  shape's own policy this would resolve to an invisible line; the per-attribute merge instead
  reuses the width/paint/dash from the style reference whenever the connector's own `<a:ln>`
  doesn't itself declare that specific attribute. An explicit `<a:noFill/>` on the connector's
  own `<a:ln>` always wins outright over the style reference, regardless of any other merging.
- `ResolveArrowhead` reads a connector's own `<a:headEnd>`/`<a:tailEnd>` child (by element name),
  recognizing `triangle`/`stealth`/`diamond`/`oval`/`arrow` type values (`none` and any
  unrecognized value both resolve to no arrowhead at all - not rendered as an unknown/placeholder
  marker), defaulting an absent `w`/`len` attribute to `"med"`.
- `ComputeEndpointsAndTangents` walks a resolved connector geometry path's first subpath to
  extract its start/end points and start/end tangent directions (via the existing
  `PathCommand.ComputeTangents`), used to orient the arrowhead geometry at each endpoint.

**Render-time wiring** (`PptxDocument.Render.cs`): a new `RenderConnector` method, dispatched
from `RenderNode`'s switch for `PptxConnectorShapeNode` (with the same `skipPlaceholderShapes`-
gated outer catch every other node kind already has). Unlike every other node kind, connector
geometry resolution is **additionally** wrapped in its own unconditional inner try/catch
(regardless of `skipPlaceholderShapes`): an exotic or future preset name this phase does not
implement degrades to "this one connector paints nothing" rather than aborting the rest of the
slide's own render - satisfying this fix's own graceful-degradation requirement without weakening
the existing, stricter failure behavior for every other shape kind. Painting order is fill (only
if the connector's own `<p:spPr>` explicitly declares one via `<a:solidFill>`/etc. - connectors
have no fill by default, unlike an ordinary autoshape, and `RenderConnector` never synthesizes
one), then stroke (via the merged line style above), then arrowheads. `<a:headEnd>` paints at the
geometry path's **start** point, oriented along the **negated** outgoing tangent (pointing back
toward the line's own start, matching PowerPoint's own visual convention that an arrowhead points
away from the line it terminates); `<a:tailEnd>` paints at the path's **end** point, oriented
along the incoming tangent (continuing the line's own direction of travel) - this head/tail
semantic split was confirmed against the real fixture's own `<a:tailEnd type="arrow"/>`
declaration. `PaintArrowhead` rotates/translates the arrowhead's local-space geometry (tip at
local origin, pointing +X) to the target point/direction entirely within the connector's own
local coordinate space, **before** applying `localToSurface` - consistent with how the
connector's own stroked line outline is built/transformed, so any non-uniform scale or flip
baked into `localToSurface` (from `flipH`/`flipV`/group nesting) applies identically to both the
line and its arrowheads. A connector has no text body, so none of `RenderConnector`'s own
painting steps ever attempt to resolve or paint one.

**Known limitations, left as explicit, documented simplifications**:

- **Only `line`/`straightConnector1` and the four `bentConnectorN`/four `curvedConnectorN`
  presets are implemented.** Any other connector preset name (for example a custom/exotic one, or
  one of the less common named connector presets this phase did not prioritize) degrades to "this
  one connector paints nothing", per the inner-try/catch policy above - not a crash, but also not
  a visible line.
- **Arrowhead size-keyword-to-EMU mapping is an approximation**, not derived from any published
  PowerPoint-internal constant table (none is publicly documented); it was chosen to be visually
  reasonable relative to the connector's own line width, not pixel-matched against a specific
  PowerPoint export.
- **Connection-site (`<a:stCxn>`/`<a:endCxn>`) auto-routing is out of scope.** A connector's
  endpoints are taken purely from its own resolved `<a:xfrm>`/preset-geometry local space -
  PowerPoint's own behavior of re-routing a connector's endpoints to track a moved/resized
  connected shape (via `<a:stCxn idx="N">`/`<a:endCxn idx="N">` referencing the connected shape's
  own connection-site index) is not implemented; a connector whose source document relies on this
  live-routing behavior renders at its own last-saved, static `<a:xfrm>` position instead.

**Test coverage**: `PptxConnectorTests.cs` (new) covers preset geometry path resolution for
`line`/`straightConnector1` (both preset-name aliases) across `flipH`/`flipV`/both/neither
combinations (via `ResolveShapeFrame` + `Build` + `.Transform`), the zero-width/zero-height
bug-fix proof (contrasted against an ordinary, non-connector preset still correctly degrading to
`Path.Empty` under the same zero-size input), bent/curved connector endpoint-reaching proof,
`ResolveConnectorLineStyle`'s own merge-precedence rules (own-value-wins, style-fallback,
`noFill`-always-wins), `ResolveArrowhead`'s type/size-key resolution (including the `none`/
unrecognized/absent-element cases), `PptxArrowheadGeometry.Build`'s per-kind shape/sizing
properties (tip-at-origin, `lg` larger than `sm`), and `ComputeEndpointsAndTangents`'s tangent
directions for horizontal/vertical/diagonal/bent connectors. `PptxRenderTests.cs` gained
end-to-end render-level pixel tests: a straight connector paints its diagonal line at the
expected pixel positions; a zero-height connector still paints its horizontal line (the bug-fix
proof, at the render level); and a connector with a `tailEnd type="triangle"` arrowhead paints
ink well outside the plain line's own stroke width, flared out just above its tip at the
connector's own endpoint. `PptxFixturesCorpusTests.cs`'s own
`PptxDocument_Render_ShpConnectorPropsFixture_ConnectorsPaintVisibleLines` test (renamed from
its prior "connectors skipped silently" name) now asserts both of the fixture's slides paint at
least one non-background pixel, where previously only slide 0's dimensions were asserted and
slide 1 was untested.

#### Phase 2 Follow-Up: Underline Rendering (`<a:rPr u="…">`)

A real-world corpus fixture ("ERF IWF Breadboard Motion System Overview.pptx", slide 23
"Configurations") contains text runs declaring `<a:rPr u="sng"/>` that PowerPoint's own
ground-truth rendering shows with a visible single underline, while CanvasNet rendered no
underline at all. Root cause: `GetUnderline` (in `PptxDocument.TextInheritance.cs`) parsed the
`u` attribute into a boolean `PptxEffectiveRunProperties.Underline`, but nothing downstream (text
layout or paint) ever consumed it - it was silently dropped before reaching `PptxTextLayout`/
`PaintTextLayout`. This closes that gap.

**Style model** (`PptxEffectiveTextProperties.cs`): a new `PptxUnderlineStyle` enum -
`None`/`Single`/`Double`/`Other` - replaces the prior boolean. Every ECMA-376 `u` value other than
`none`/`sng`/`dbl` (`heavy`, `dotted`, `dottedHeavy`, `dash`, `dashHeavy`, `dashLong`,
`dashLongHeavy`, `dotDash`, `dotDashHeavy`, `dotDotDash`, `dotDotDashHeavy`, `wavy`, `wavyHeavy`,
`wavyDbl`), plus any unrecognized string value, resolves to `Other` and is rendered as a single
solid line - a documented simplification rather than a distinct visual rendering per variant.
`PptxEffectiveRunProperties.Underline` is replaced by `UnderlineStyle` (`PptxUnderlineStyle`), and
a new `UnderlineColor` (`Rgba32`) is added alongside it.

**Inheritance resolution** (`PptxDocument.TextInheritance.cs`): `GetUnderline` is replaced by
`GetUnderlineStyle(XElement?)`, returning `PptxUnderlineStyle?` (`null` when the element declares
no `u` attribute at all, so the existing four-tier run/paragraph-defRPr/lstStyle-level/
master-txStyles inheritance chain keeps falling through exactly as it does for every other text
property); `"none"` resolves to `None`, `"sng"` to `Single`, `"dbl"` to `Double`, and every other
recognized/unrecognized non-empty value to `Other`. A new `GetUnderlineColor(XElement?, PptxTheme,
PptxColorMap?)` resolves a run's own underline color: it returns `null` (meaning "inherit, keep
searching the chain") whenever no `<a:uFill>` child element exists at all - this single condition
also covers the explicit `<a:uFillTx/>` marker ("use the text's own fill"), since `<a:uFillTx/>`
is a sibling element name, not a child of `<a:uFill>`; otherwise it resolves `<a:uFill>/
<a:solidFill>`'s color child via the same `ResolveColor` helper `GetRunColor` already uses.
`ResolveEffectiveRunProperties` gains two four-tier inheritance chains, mirroring the pattern used
for every other property: `GetUnderlineStyle(...) ?? ... ?? PptxUnderlineStyle.None` for the
style, and `GetUnderlineColor(...) ?? ... ?? color` for the color, where `color` is the run's own
already-resolved text-fill color computed earlier in the same method - so a run with no `<a:uFill>`
anywhere in its own inheritance chain renders its underline in its own text color, matching
PowerPoint's own default behavior.

**Per-run span construction** (`PptxDocument.TextLayout.cs`): a new `LineUnderlineSpan(StartXEmu,
EndXEmu, RunProperties)` record struct accumulates one contiguous underline span per underlined
run as `BuildLines` walks a line's own tokens: each token's `tokenStartX`/`tokenEndX` (the cursor
position immediately before/after that token's own characters are advanced - exactly where its
glyphs are emitted) extends a pending span when the token's own `RunProperties` is the same run
instance (via `ReferenceEquals`, reliable because `BuildLines` creates one scaled `RunProperties`
instance per run and reuses it across every token belonging to that run) as the span currently
being accumulated; a token belonging to a different run, or one whose own `UnderlineStyle` is
`None`, flushes any pending span and either starts a new one or clears it. This means a single
run's own interior whitespace remains part of its own contiguous span (the span is never broken
at a word boundary within one run), while an underline never bridges two separate runs even when
both happen to be underlined - matching PowerPoint's own visual behavior. `PositionLines`
converts each flushed `LineUnderlineSpan` into a `PptxUnderlineSegment` (`StartXEmu`, `EndXEmu`,
`BaselineYEmu`, `SizeEmu`, `Style`, `Color`) once the line's own final `startX`/`baselineY` are
known, threading an accumulating list of these segments alongside the existing glyph list all the
way to `ResolveTextLayout`'s own final `new PptxTextLayout(glyphs, fontScale, underlines)` call.

**Painting** (`PptxDocument.TextRender.cs`): `PaintTextLayout` iterates `layout.Underlines` after
its own existing glyph-painting loop. For each segment, `thicknessEmu = segment.SizeEmu *
UnderlineThicknessRatio` and `offsetEmu = segment.SizeEmu * UnderlineOffsetRatio` (two new named
`const float` fields, `0.05f`/`0.08f`) compute a pragmatic, `SizeEmu`-proportional approximation
of the underline's own thickness and vertical offset below the baseline - this phase does not
parse the target font's own OpenType `post` table `underlinePosition`/`underlineThickness`
fields, a documented limitation; true font-metric-derived values are deferred to a later phase.
A rectangle spanning `[StartXEmu, BaselineYEmu + offsetEmu]` to `[EndXEmu, BaselineYEmu +
offsetEmu + thicknessEmu]`, built directly in shape-local, y-down space (no glyph-space Y-flip,
unlike glyph outlines which are authored in a y-up font coordinate space), is transformed through
`shapeToSurfaceTransform` and filled with the segment's own resolved color. A `Double`-style
segment paints two such rectangles, each half as thick, separated by a gap proportional to
`SizeEmu` (a third new constant, `DoubleUnderlineGapRatio = 0.06f`); every other non-`None` style
(`Single`, `Other`, or any unrecognized value) paints exactly one rectangle - this painting step
never throws regardless of style value.

**Test coverage**: `PptxTextTests.cs` covers the style resolution theory (`sng`/`none`/`dbl`/
`wavy`/`heavy`) and the underline-color inheritance rules (explicit `<a:uFill>` override, default-
to-text-color with no fill markup, default-to-text-color with explicit `<a:uFillTx/>`).
`PptxTextLayoutTests.cs` covers per-run span construction: a single underlined run emits one
segment spanning its own measured width; a non-underlined run emits none; of two runs on one
line, only the first underlined, exactly one segment is emitted bounded to that first run; and a
single underlined run with an interior space emits one segment spanning that space too.
`PptxTextRenderTests.cs` covers painting: a `Single`-style segment paints a visible stroke below
the baseline at the expected position/color with no ink above the baseline; a layout with no
segments paints no extra ink (a regression guard); and `Double`/`Other`-style segments paint
without throwing. Visual verification (a hand-built, in-memory `.pptx` package containing a
single `<a:rPr u="sng"/>` run, rendered end-to-end through the public `PptxDocument.Render` API
and saved to PNG) confirms a single straight horizontal line appears beneath the rendered word's
own baseline, spanning its full rendered width, in the same color as the text itself.

#### Phase 2 Follow-Up: Picture Preset-Geometry Clipping (`<a:prstGeom>` on `<p:pic>`)

A real-world corpus fixture ("ERF IWF and Reagent Probe Breadboard Motion System Overview.pptx")
contains `<p:pic>` picture shapes whose own `<p:spPr>/<a:prstGeom prst="...">` declares a
non-`rect` preset (for example `ellipse` or `roundRect`) - PowerPoint's own ground-truth rendering
visibly crops the embedded image to that preset's own shape, while CanvasNet previously painted
every picture as a plain, unclipped full rectangle regardless of its own declared geometry. Root
cause: `RenderPicture` (`PptxDocument.Render.cs`) never read a picture's own `<a:prstGeom>`/
`<a:custGeom>` at all, and `PaintPicture` (`PptxDocument.Images.cs`) had no clip/mask mechanism of
any kind - every picture's footprint was always its full `(0,0)-(1,1)` unit square mapped through
`shapeToSurfaceTransform`, with no notion of a non-rectangular outline.

**Fix - reusing, not reimplementing, existing geometry and fill infrastructure**:

- **Clip-path resolution** (`PptxDocument.Images.cs`'s new `ResolvePictureClipPath(XElement
  spPrElement, float widthEmu, float heightEmu)`): a thin, picture-specific wrapper around the
  exact same preset/custom-geometry dispatch `ResolveShapeGeometry` already uses for an ordinary
  auto-shape (`PptxPresetGeometry.Build`/`ResolveCustomGeometry`, both in `PptxDocument.Geometry.cs`)
  - not a second, divergent geometry resolver. It returns `null` (meaning "no clip - paint the
    full bounding-box rectangle", this package's original behavior, with zero overhead) when
  `<p:spPr>` declares neither `<a:prstGeom>` nor `<a:custGeom>` at all (a schema-valid, historically
  unclipped picture - not a malformed document, unlike `ResolveShapeGeometry`'s own all-or-nothing
  contract for an auto-shape), or when `<a:prstGeom>` explicitly names the `rect` preset (clipping
  to a `rect` would be a pure no-op, since that preset's own resolved geometry is already the full
  bounding-box rectangle a "no clip" paint produces). Otherwise it delegates outright to
  `PptxPresetGeometry.Build`/`ResolveCustomGeometry` and returns their resolved `Path`, propagating
  their own `InvalidDataException`/`PptxUnsupportedFeatureException` unchanged - an unsupported
  preset on a picture fails exactly the same way an unsupported preset on an auto-shape already
  does, a deliberate, consistent fail-closed posture rather than a silent unclipped fallback.
- **Clip-to-mask compositing** (`PptxDocument.Images.cs`'s `PaintPicture`, new trailing optional
  `Path? clipPath = null` parameter): no generic "clip an arbitrary raster draw to a path"
  primitive exists anywhere in this repository's core Canvas/Drawing API - the actually-reusable
  primitive is the existing anti-aliased path-fill rasterizer, `Drawing.PathFiller.Fill(Surface,
  Path, Rgba32, FillRule, float)` (already used by every shape/table fill in this codebase),
  repurposed as a mask-builder rather than a dedicated clip API, mirroring
  `DemaConsulting.CanvasNet.Svg`'s own `SvgCodec.ClippingAndMasking.cs`'s `ApplyClipPath` (an
  opaque-white path fill onto a fresh, fully-transparent `Surface`), with one deliberate
  difference: instead of post-multiplying an already-fully-painted offscreen buffer's own alpha by
  that coverage mask (`ApplyClipPath`'s own `ApplyCoverageClip` approach - unsuitable here, since
  `PaintPicture` composites directly onto the live destination surface pixel-by-pixel, not into an
  isolated offscreen buffer), `PaintPicture` instead scales each _sampled source pixel's own
  alpha_ by that same pixel's mask coverage (`0` outside the clip geometry, `255` fully inside it,
  an anti-aliased in-between value exactly on its edge) immediately before compositing via the
  existing `Rgba32.CompositeOver` - a zero-pixels-touched early-out skips a destination pixel
  outright when its own mask coverage is `0`. When `clipPath` is non-null, it is transformed by
  the same `shapeToSurfaceTransform` the picture itself is already painted through (not the
  unit-square `CreateScale(widthEmu, heightEmu) * shapeToSurfaceTransform` used for image
  sampling - `ResolvePictureClipPath`'s own resolved `Path`, exactly like `ResolveShapeGeometry`'s
  for an auto-shape, is already sized to the shape's own local `(0,0)-(widthEmu,heightEmu)` box).
- **Wiring** (`PptxDocument.Render.cs`'s `RenderPicture`): resolves `ResolvePictureClipPath` from
  the picture's own `<p:spPr>` immediately alongside its existing `ResolvePictureSurface`/
  `ResolveSrcRect` calls, and passes the result as `PaintPicture`'s new trailing argument.
- **`<a:custGeom>` on a picture is supported, not scoped out**: `ResolveCustomGeometry` is already
  a fully general, already-tested resolver with no auto-shape-specific assumptions baked in -
  supporting it for a picture via the same dispatch costs one additional branch, not new geometry
  machinery, so it is deliberately not deferred to a later phase.

**Known limitations, left as explicit, documented simplifications**:

- The clip mask's own anti-aliased edge inherits `PathFiller.Fill`'s default `flattenTolerance`
  (`0.25f`) and `PptxPresetGeometry`'s own fixed-segment-count curve approximation - the identical
  characteristic every other preset-geometry shape fill in this codebase already carries, not a
  new limitation this fix introduces.
- A same-size scratch mask `Surface` is allocated per clipped picture paint, rather than a
  clip-bounds-restricted smaller surface - consistent with `PaintPicture`'s own existing
  full-bounding-box-footprint iteration style (`Surface`'s documented max-dimension bound keeps
  worst-case allocation cost small and bounded).

**Test coverage**: `PptxImagesTests.cs` gained `ResolvePictureClipPath` unit tests (`rect` preset
and absent geometry both resolve to `null`; `ellipse`/`roundRect` presets and `<a:custGeom>` each
resolve to a non-null `Path`; a `<a:prstGeom>` missing its own `prst` attribute throws
`InvalidDataException`; an unsupported preset name propagates `PptxUnsupportedFeatureException`
unchanged; a null argument throws `ArgumentNullException`) and `PaintPicture` clip-path tests (an
ellipse clip path leaves a bounding-box corner unpainted while the shape's own center paints the
image's color; a `null` clip path - the default - continues painting the full rectangle
unclipped, a direct regression guard for every pre-existing `PaintPicture` call in this same
file). `PptxRenderTests.cs` gained end-to-end render-level pixel tests: a `<p:pic>` with
`<a:prstGeom prst="ellipse">` clips the image to the elliptical region; one with an explicit
`<a:prstGeom prst="rect">` still paints the full bounding-box rectangle including its own corner
(strengthening the existing center-only `Render_Picture_PaintsEmbeddedImageAtExpectedLocation`
into an explicit no-regression proof); one with no `<a:prstGeom>`/`<a:custGeom>` at all also still
paints unclipped (a defensive, schema-edge-case regression guard); and one with
`<a:prstGeom prst="roundRect">` clips its own bounding-box corners while its center remains
painted. A non-permanent visual-verification generator (`GeneratePictureEllipseClipReproPng`,
mirroring `PptxTextLayoutTests.cs`'s own `GeneratePlusMinusDegreeTofuReproPng` precedent) renders
the same ellipse-clipped picture end-to-end and saves it to
`.agent-logs/pptx-picture-ellipse-clip-repro.png` for a human reviewer to open and visually
confirm the circular photo-crop effect. `Render_PictureEllipseGeometry_NearSquareRealWorldAspect_ClipsAllFourBoundingBoxCorners`
(added later, in response to a bug report alleging an `ellipse`-clipped picture at a near-square,
real-world corpus aspect ratio rendered with an unclipped rectangular top) adds regression coverage
sampling all four bounding-box corners rather than one diagonal pair. A direct mask-boundary-trace
comparison against the real-world document's own rendered output confirmed the clip mask itself
was already pixel-correct and fully symmetric on all four sides - this remains correct and
unchanged. **A prior investigation pass incorrectly concluded from that same comparison that the
bug report's own underlying asymmetry complaint was entirely "a visual illusion" with no code
defect at all, and made no further source change.** That conclusion was wrong: the bug report's
own fixture additionally declared a red `<a:ln>` on the same `<p:pic>`, and a from-scratch,
independently re-verified investigation (direct code inspection plus a fresh rendered repro PNG -
see `.agent-logs/planning-picture-ln-stroke-fix-7f2a4d.md`) proved `RenderPicture` never read or
painted ANY `<p:pic>`'s own `<a:ln>` stroke at all - 0% of the ellipse's perimeter rendered any
stroke color, not merely a partial arc. The image-content clip itself (this section, above) was
correctly re-confirmed unaffected and required no further change; only the separate, previously
undiagnosed missing-stroke defect was real. See "Phase 2 Follow-Up: Picture Own-Stroke Outline
Rendering" below for the actual root cause and fix.

#### Phase 2 Follow-Up: Picture Own-Stroke Outline Rendering (`<a:ln>` on `<p:pic>`)

**Root cause**: `RenderPicture` (`PptxDocument.Render.cs`) resolved and painted a `<p:pic>`'s own
embedded image content (via `ResolvePictureSurface`/`ResolveSrcRect`/`ResolvePictureClipPath`/
`PaintPicture`, see the preceding section), but never read its own `<p:spPr>/<a:ln>` child at all,
and never called `ResolveShapeLineStyle`/`ResolveStrokeOutline`/`FillPaint` for a picture node -
unlike `RenderShape` (`<p:sp>`), `RenderConnector` (`<p:cxnSp>`), and `PaintTableBorder`
(`PptxDocument.Tables.cs`, for table cell borders), which all already resolve and paint their own
node kind's stroke via that exact same trio of resolvers. `PptxPictureShapeNode` is a closed,
separate case in `RenderNode`'s switch that dispatches only to `RenderPicture` - there was no
secondary pass anywhere else in the codebase that painted a `<p:pic>`'s own stroke. The result: a
picture declaring an `<a:ln>` (for example a red outline around an `ellipse`-cropped photo) always
rendered with that stroke entirely absent, regardless of its own resolved geometry - confirmed by
an isolated, from-scratch visual repro (the bug report's own exact `<p:pic>` XML, rendered and
visually inspected before this fix: a plain pale ellipse with zero red anywhere on its perimeter).

**Fix - reusing, not reimplementing, existing line-style and stroke infrastructure**:

- **Geometry resolution for stroking, deliberately separate from the clip-path resolver above**
  (`PptxDocument.Images.cs`'s new `ResolvePictureGeometryPath(XElement spPrElement, float
  widthEmu, float heightEmu)`): `ResolvePictureClipPath` collapses "no geometry at all" and an
  explicit `<a:prstGeom prst="rect">` to `null`, a pure optimization valid only for its own
  image-content-clipping use case (clipping to a full bounding-box rectangle is a no-op). A stroke
  outline has no equivalent no-op shortcut - an implicit or explicit full-rectangle picture with an
  `<a:ln>` must still stroke an actual, closed rectangle boundary, exactly as an ordinary `<p:sp>`
  auto-shape with an implicit/explicit `rect` preset already does via `ResolveShapeGeometry`. So
  `ResolvePictureGeometryPath` always returns a concrete `Path`: the resolved preset geometry
  (`PptxPresetGeometry.Build`, with `rect` NOT special-cased to `null`) when `<a:prstGeom>` is
  present; the resolved custom geometry (`ResolveCustomGeometry`) when `<a:custGeom>` is present;
  or the implicit full-rectangle boundary (`PptxPresetGeometry.Build("rect", widthEmu, heightEmu)`)
  when neither is present - reusing the identical dispatch `ResolvePictureClipPath` itself already
  reuses, not reimplementing it.
- **Line-style resolution and stroke painting** (`PptxDocument.Render.cs`'s `RenderPicture`, after
  `PaintPicture`): reads the picture's own `<p:spPr>/<a:ln>` and sibling `<p:style>` (mirroring
  `RenderShape`'s identical sibling-element lookup pattern), resolves
  `ResolveShapeLineStyle(lnElement, styleElement, theme, colorMap)` - the exact same resolver
  `RenderShape` already uses, requiring no new line-style-resolution code - and, when non-null,
  strokes `ResolvePictureGeometryPath`'s resolved geometry via `ResolveStrokeOutline(...).
  Transform(localToSurface)` followed by `FillPaint(surface, strokedOutline, lineStyle.Paint)`,
  mirroring `RenderShape`'s exact fill-then-stroke order (image content first, stroke frames it on
  top, matching real-world PowerPoint's own visual stacking).
- **Signature extension**: `RenderPicture` gained `PptxTheme theme` and `PptxColorMap colorMap`
  parameters, threaded through from its one call site in `RenderNode`'s `case
  PptxPictureShapeNode pic:`, which already held both values in scope (used by the sibling
  `RenderShape` call in the same switch).
- **The image-content clip (`ResolvePictureClipPath`/`PaintPicture`, from the preceding section)
  was not touched** - it was independently re-verified correct and unaffected by this defect (see
  this section's own root-cause remarks above); only the missing stroke pass was added.

**Test coverage**: `PptxImagesTests.cs` gained `ResolvePictureGeometryPath` unit tests mirroring
`ResolvePictureClipPath`'s own test structure (an explicit `rect` preset and absent geometry both
now resolve to a non-null rectangle `Path`, unlike `ResolvePictureClipPath`'s `null`; `ellipse`
preset and `<a:custGeom>` each resolve to a non-null `Path`; a `<a:prstGeom>` missing its own
`prst` attribute throws `InvalidDataException`; an unsupported preset name propagates
`PptxUnsupportedFeatureException` unchanged; a null argument throws `ArgumentNullException`).
`PptxRenderTests.cs` gained `Render_PictureEllipseGeometry_WithRedLnStroke_RendersCompleteClosedEllipseOutline`
(the bug report's own exact geometry/stroke, sampling all four of the ellipse's own perimeter
midpoints - top/bottom/left/right-center, not just corners - for the stroke's own red color; a
defect covering 0% of the perimeter would fail at all four) and
`Render_PictureNoPrstGeom_WithLnStroke_StrokesRectangleOutline` (a picture with no geometry child
at all but a present `<a:ln>` now strokes a complete rectangle, not nothing). The pre-existing
`Render_PictureEllipseGeometry_NearSquareRealWorldAspect_ClipsAllFourBoundingBoxCorners` test was
left unchanged - it remains valid, correct coverage for the separate image-content-clip concern.
A before/after visual repro (the bug report's own exact geometry, rendered and visually inspected)
confirmed the fix: before, a plain pale ellipse with no red anywhere on its perimeter; after, a
complete, correctly-closed red ellipse outline fully framing the same pale ellipse.

#### Phase 2 Follow-Up: Table Style/Banding Resolution (`<a:tableStyleId>`)

**Bug**: a `<a:tbl>` whose `<a:tblPr>` declares `<a:tableStyleId>{GUID}</a:tableStyleId>`
(optionally with `firstRow="1"`/`bandRow="1"`) renders in real PowerPoint with a shaded header
row, alternating banded row fills, and style-defined cell borders, driven by the presentation
package's own `ppt/tableStyles.xml` part. `ParseTable`/`ParseTableCell` (Phase 1e, above) never
resolved `<a:tableStyleId>` against that part at all - every cell's fill/border came from its own
`<a:tcPr>` only, so a table declaring a style but no per-cell overrides (the common case for a
plain data table) rendered as a bare, mostly-invisible grid of thin border lines with no shading
at all, a visibly incorrect rendering confirmed against a real-world ground-truth PowerPoint COM
export (`.agent-logs/planning-table-style-apply-7f3a2c.md`).

**Fix - a new resolver unit, threaded through unchanged parsing/rendering call sites**:

- **`PptxDocument.TableStyles.cs`** (new file) implements the table-style lookup and the core
  precedence cascade:
  - `TryResolveTableStyle(string styleId)` resolves `styleId` against the presentation part's own
    `/tableStyles` relationship (via a new, non-throwing `TryResolveRelationshipByType` sibling of
    the existing, throwing `ResolveRelationshipByType` in `PptxDocument.Package.cs`) and the
    resulting `ppt/tableStyles.xml` part's `<a:tblStyleLst>/<a:tblStyle styleId="...">` children -
    tolerating every cause of "not found" (no `/tableStyles` relationship at all, a missing or
    not-well-formed `ppt/tableStyles.xml`, or no matching `styleId`) by returning `null`, mirroring
    `PptxDocument.Theme.cs`'s own `ParseBgFillStyleList`/`ParseFillStyleList`/`ParseLnStyleList`
    leniency precedent for optional, rarely-consulted style-sheet content - a table-style-sheet
    must never fail an otherwise well-formed table parse closed.
  - `GetTableStyles(string tableStylesPartPath)` parses and caches (`_tableStylesCache`, keyed by
    part path) the part's full `styleId -> <a:tblStyle>` lookup on first access, the same
    lazy-parse-then-cache shape every other auxiliary-part resolver in this unit already uses.
  - `ResolveTableCellStyle(...)` implements the precedence cascade itself: a cell's own explicit
    `<a:tcPr>` fill/border always wins outright - the same "explicit always wins over
    style/theme fallback" pattern `ResolveShapeLineStyle` already established for shape-stroke
    resolution, mirrored here rather than reinvented; otherwise the matched `<a:tblStyle>`'s
    `<a:firstRow>` tier (only for the header row, when `<a:tblPr firstRow="1">`) wins over its
    `<a:band1H>`/`<a:band2H>` banding tier (only when `<a:tblPr bandRow="1">`), which in turn wins
    over its `<a:wholeTbl>` base tier - the only non-null tier applied when neither `firstRow` nor
    `bandRow` select a higher tier for a given cell. A tier missing its own `<a:fill>` (for
    example a style whose `<a:band2H>` declares borders but no fill) falls through to the next
    lower tier's fill, not to "no fill" - confirmed against the ground-truth render, where an
    unfilled band tier visibly shows the table's base shading, not a transparent gap.
  - **Row-parity convention** (confirmed against the real-world ground-truth render): a styled
    header row (`firstRow="1"`, row index `0`) is excluded from band-row counting entirely, and
    `<a:band1H>` is applied to the first row after the header, alternating with `<a:band2H>`
    thereafter (`bandRowIndex = firstRowEnabled ? rowIndex - 1 : rowIndex`; even `bandRowIndex`
    selects `band1H`, odd selects `band2H`) - the same convention ECMA-376 itself documents for a
    styled, banded table.
  - **Border edge-name mapping**: each tier's own `<a:tcStyle>/<a:tcBdr>` declares up to six named
    edges (`left`/`right`/`top`/`bottom`/`insideH`/`insideV`, each itself wrapping a nested
    `<a:ln>`). A cell edge on the table's own true outer boundary (determined structurally from
    the cell's own column/row index, span, and the table's total column/row counts) consults the
    literal edge name; every interior edge instead consults `insideV` (left/right) or `insideH`
    (top/bottom) - required so a style that visually differentiates its outer frame from its
    interior gridlines (unlike the one available real-file fixture, whose style uses an identical
    line for all six edges) renders correctly.
  - **Color resolution is entirely delegated, not reimplemented**: a tier's `<a:fill>` is resolved
    via the existing `ResolveFill`, and a tier's border `<a:ln>` via the existing
    `ResolveLineStyle` - both already resolve `<a:schemeClr>` against the supplied theme/color
    map, so a table style's own `schemeClr`-templated colors (the common case for a theme-matched
    style) resolve correctly with zero new color-resolution code.
- **`PptxDocument.Tables.cs`**: `ParseTable` gained a new, optional `tableStyleResolver` parameter
  (`Func<string, XElement?>?`, defaulting to `null`) and now parses `<a:tbl>/<a:tblPr>`'s
  `tableStyleId`/`firstRow`/`bandRow`, resolving the matched `<a:tblStyle>` once per table (not
  once per cell) when a non-empty `<a:tableStyleId>` is present. Row materialization changed from
  a lazy `.Select(...)` chain to an eagerly-indexed list, needed so each cell's own `rowIndex`/
  `totalRows` context (required by the row-parity and outer-edge logic above) is known before the
  per-row loop runs. `ParseTableCell` gained matching new optional parameters (`matchedTblStyle`,
  `bandRowEnabled`, `firstRowEnabled`, `rowIndex`, `totalRows`, `columnIndex`, `totalColumns`, each
  defaulted) and now delegates its fill/border resolution to `ResolveTableCellStyle` instead of
  calling `ResolveFill`/`ResolveLineStyle` directly - every pre-existing call site (including every
  pre-existing unit test) keeps compiling and behaving identically, since omitting the new
  parameters degrades the cascade to exactly this method's own pre-existing cell-only behavior.
- **Signature threading**: `ParseShapeTree` (`PptxDocument.ShapeTree.cs`) gained the same optional
  `tableStyleResolver` parameter, threaded to its own recursive self-call and to `ParseTable`;
  `PptxDocument.Masters.cs`/`PptxDocument.Layouts.cs`/`PptxDocument.Slides.cs`'s own
  `ParseShapeTree` call sites now each pass `tableStyleResolver: TryResolveTableStyle`.

**Explicitly deferred** (fall back to the `<a:wholeTbl>` tier, or to no special treatment, exactly
as if no table style were matched): `firstCol`/`lastCol`/`lastRow`/corner-cell (`neCell`/`nwCell`/
`seCell`/`swCell`) style parts; column banding (`band1V`/`band2V`, gated on `<a:tblPr
bandCol="1">`); and table-style-driven font color (`<a:tcTxStyle>`) - a cell's own text-run font
color resolution is entirely unchanged. Each is a separable, independently-scoped refinement with
no bearing on the primary header-row/row-banding defect this fix addresses.

**Test coverage**: `PptxTablesTests.cs` gained
`ParseTable_FirstRowTblPrWithMatchingTableStyle_HeaderRowCellUsesFirstRowStyleFill` (a matched
style's `<a:firstRow>` tier fill applies to the header row, falling back to `<a:wholeTbl>` for the
data row),
`ParseTable_BandRowTblPrWithMatchingTableStyle_AlternatesBand1HAndBand2HFillStartingAfterHeaderRow`
(a five-row, header-plus-banded table alternates `band1H`/`band2H` starting immediately after the
header row, with an unfilled `band2H` tier falling through to `wholeTbl`),
`ParseTable_TblPrWithNoTableStyleId_FallsBackToPlainCellOnlyBorderAndNoFill` and
`ParseTable_TblPrWithUnresolvableTableStyleId_FallsBackToPlainCellOnlyBorderAndNoFillWithoutThrowing`
(no `<a:tableStyleId>` at all never even invokes the resolver delegate; an unresolvable one
resolves via a resolver returning `null`; both degrade to today's pre-existing plain, style-less
cell-only rendering without throwing),
`ParseTableCell_TcPrExplicitFillAndBorder_OverridesTableStyleFillAndBorder` (a cell's own explicit
`<a:tcPr>` fill and a single explicit border edge take final precedence over a matched style's own
fill/borders, while the cell's remaining, non-overridden edges still fall back to the style), and
`ParseTableCell_InteriorColumnBorder_UsesInsideVNotLeftRightTcBdrEdge` (a three-column table's
interior column boundaries resolve against the style's own `insideV` edge rather than its `left`/
`right` edges, while the table's two true outer-boundary edges still resolve against `left`/
`right`).

#### Phase 2 Follow-Up: Default Tab-Stop Expansion

**Bug**: a literal U+0009 TAB character inside a run's text (e.g. a typed `"1\tReference fluid
evaporation..."` run, the pattern a real-world "numbered list" paragraph actually uses when it is
not a `buAutoNum` bullet at all) was measured by this unit's `Tokenize`/`MeasureTokenWidthEmu`/
`BuildLines` exactly like any other whitespace character - resolved via the shared `ResolveGlyph`
helper and measured by its primary font's own `cmap`/`GetAdvanceWidth`. Most fonts have no `cmap`
entry for U+0009, so this resolved to glyph index `0` (`.notdef`), whose advance width is
typically `0` or near-zero - producing a near-zero gap instead of PowerPoint's wide, tab-stop-sized
gap, jamming the numbered-list text together and corrupting word-wrap width calculations for any
line containing a tab. This was previously flagged, but explicitly deferred as out of scope, in
the "Bullets and Numbering Rendering" follow-up above (`"its 'numbers' are literal typed digits
separated from the following word by a literal tab character... a distinct, pre-existing,
out-of-scope defect (tab-stop support)"`) - this follow-up closes that deferred item.

**Root cause confirmation and chosen default interval**: a real-world corpus file ("ERF IWF
Breadboard Peer Review.pptx", slide 9, the single highest-divergence slide in a full 73-slide
pixel-diff pass) contains 19 literal tab characters, each between a literal digit run and its
sentence text, with no `<a:tabLst>` anywhere in the slide, its layout, or its master. Both the
slide master's `<p:txStyles>` and the presentation's own `<p:defaultTextStyle>` declare
`defTabSz="914400"` (exactly 1 inch) on every list level. Independent ground-truth pixel
measurement of the rendered slide (three different bullet-number widths - "1", "2", "10") each
showed the sentence text starting within 1px of the position a single 914400-EMU tab-stop
expansion predicts, confirming both the root cause and the chosen interval from two independent
sources.

**Fix - a tab becomes a position-dependent, dynamically-expanding token, not a glyph-measured
one**: `PptxDocument.TextLayout.cs` gained a `DefaultTabStopEmu = 914400f` constant and a
`GetNextTabStopEmu(currentXEmu, tabStopEmu)` helper implementing standard tab semantics - the
smallest multiple of `tabStopEmu` strictly greater than `currentXEmu`, guaranteeing a tab always
advances by at least a minimal, non-zero amount even when already exactly on a stop boundary.
`Tokenize` now always isolates a `'\t'` character as its own single-character token - never merged
with adjacent whitespace of another kind, nor with another adjacent tab - since a tab's effective
width is never a fixed, pre-computable glyph advance. `ResolvedToken` gained a matching `IsTab`
flag (mirroring the existing `IsLineBreak` flag's convention); a tab token's `WidthEmu` is left
unused (`0`) rather than glyph-measured, since its true width depends entirely on where it falls in
the line.

Because a tab's width is position-dependent, it must be computed twice, from two different running
positions, and the two computations must stay formula-identical: `PackTokensIntoLines`'s
wrap/fit-decision loop computes a tab token's effective width as
`GetNextTabStopEmu(currentWidth, DefaultTabStopEmu) - currentWidth`, read from its own running
`currentWidth` position at that point in the token stream - this is what makes the wrap decision
correctly reflect the tab's true, expanded width rather than its near-zero glyph-measured width,
so an early tab in a long line no longer silently under-counts the line's true occupied width and
mis-places the wrap point. `BuildLines`'s own per-line rendering loop special-cases an `IsTab`
token: instead of resolving a glyph/advance via `resolveGlyph`/`GetAdvanceWidth` (already skipped
for every whitespace-kind token's own glyph-emission guard), it computes
`cursorX = GetNextTabStopEmu(cursorX, DefaultTabStopEmu)` directly, expanding the cursor to the
next tab stop with no glyph painted. The line's own natural width (`LineBox.LineWidthEmu`) is now
taken as a single `lineWidth = cursorX` read once after the per-token loop completes, rather than a
separate `lineWidth += token.WidthEmu` running accumulator - `cursorX`'s own per-character
accumulation is already the authoritative total for every other token kind, so this removes a
redundant, now-inconsistent-for-tabs duplicate computation rather than introducing a new one.

**Explicitly out of scope**: full OOXML `<a:tabLst>` explicit tab-stop support (per-paragraph,
per-level, multiple named tab stops with alignment/leader options) remains unimplemented - no
slide in the investigated corpus (slide 9, its layout, or its master) declares `<a:tabLst>`
anywhere, so default-interval-only expansion fully resolves the observed defect, and there is
currently no `tabLst` parsing/inheritance infrastructure anywhere in
`PptxDocument.TextInheritance.cs`/`PptxEffectiveTextProperties.cs` to build on. An explicit
`<a:tabLst>` paragraph simply continues to fall back to this same default-interval behavior today,
no regression - a reasonable, documented simplification, flagged as a still-open, separate,
future unit of work rather than folded into this fix.

**Test coverage**: `PptxTextLayoutTests.cs` gained
`ResolveTextLayout_TabCharacter_ExpandsToNextDefaultTabStop` (a run containing `"A\tB"` proves the
glyph after the tab lands at the exact `914400`-EMU tab-stop X coordinate, not the near-zero
position a `.notdef` glyph advance would produce),
`ResolveTextLayout_TabCharacter_WrapDecisionUsesExpandedWidthNotGlyphWidth` (a long run with an
early tab, sized so the glyph-measured near-zero tab width would keep both words on one line but
the true, tab-stop-expanded width correctly wraps the second word onto a new line, proving
`PackTokensIntoLines` uses the dynamically-computed expanded width for its fit decision, not the
static, unused `ResolvedToken.WidthEmu`), and
`ResolveTextLayout_MultipleTabCharacters_EachAdvancesToItsOwnNextTabStop` (a run containing
`"A\t\tB"` proves the second tab's expansion is computed from the cursor position after the first
tab's own expansion - including correctly advancing by a full interval, never zero, when the
second tab starts exactly on an already-aligned tab-stop boundary).

#### Review Follow-Up: Table Gradient Sizing, Gradient Transform Composition, and ColorMap Threading

A focused code-review pass surfaced three true-positive defects in `PptxDocument.Tables.cs` and
its immediately collaborating files, all touching the same gridSpan/rowSpan/colorMap code paths
and fixed together.

**1. Merged-cell gradient sizing (`ParseTable`)**: `ParseTable` previously called
`ParseTableCell(tcElement, theme, columnWidthEmu, rowHeightEmu, ...)` using only the cell's own
single column's width and single row's height - always a span-1 size, regardless of the cell's own
declared `gridSpan`/`rowSpan`. A cell's gradient fill (`ResolveGradientFill`, `PptxDocument.
Paint.cs`) builds its `Start`/`End` stop positions directly from the `widthEmu`/`heightEmu` it is
given, so a merged cell's gradient was always sized against its first column/row alone - visibly
too narrow/short, with the gradient's own end stop landing partway across the merged cell's true
rectangle instead of at its far edge. **Fix**: `ParseTable` now precomputes a `rowHeightsEmu` list
once, up front, before its per-row loop (replacing the previous once-per-row inline parse); for
each `<a:tc>`, it peeks the cell's own `gridSpan`/`rowSpan` attributes (via the existing
`ParseOptionalIntAttribute` helper) _before_ calling `ParseTableCell`, and passes a merged
(summed) `cellWidthEmu`/`cellHeightEmu` computed via a new `SumConsecutive(IReadOnlyList<float>
valuesEmu, int startIndex, int count)` private helper - a generalization of the pre-existing
`SumColumnWidths`, which now delegates to it as a one-line wrapper, reused here for the new
row-height peek as well. This peek is a sizing hint only, not a validation step: `ParseTableCell`
still independently re-parses and authoritatively validates the same `gridSpan`/`rowSpan`
attributes (throwing `InvalidDataException` for a non-positive value), so a malformed value can
never silently bypass that check via the peek.

**2. Gradient transform composition (`FillPaint`)**: every other transform-sensitive paint
operation in this unit composes the shape's own local-to-surface transform into whatever it paints
before filling; a gradient fill's own `Gradient`, however, was always filled with its `Transform`
left at the default `Matrix3x2.Identity`, regardless of any non-identity `shapeToSurfaceTransform`
in scope - the one precedent for this kind of composition elsewhere in the codebase is
`Rendering/Canvas.cs`'s own `paint.WithTransform(_current)` call. For an ordinary (non-rotated,
non-skewed, 1 EMU-per-pixel) shape this defect is invisible, since the identity transform is a
no-op; it becomes visible for a rotated/scaled/skewed shape (or, for a table, any non-identity
`shapeToSurfaceTransform` the table itself is painted through), where the gradient's own ramp
stays fixed in the shape's local coordinate space instead of following the shape into surface
space. **Fix, scoped to table cells only**: `FillPaint` (a single, shared private method also
called from eight-plus non-table sites in `PptxDocument.Render.cs`) gained a second, four-
parameter overload - `FillPaint(Surface, Path, PptxPaint, Matrix3x2 shapeToSurfaceTransform)` -
that composes `gradientFill.Gradient.WithTransform(shapeToSurfaceTransform)` before filling; the
pre-existing three-parameter overload is **unchanged** and now simply delegates to the new one
with `Matrix3x2.Identity`, preserving every pre-existing (non-table) call site's exact current
behavior bit-for-bit. `PaintTable`'s cell-fill call and `PaintCellBorder`'s stroked-border call
(both of which already have `shapeToSurfaceTransform` in scope) now use the new four-argument
overload. **Explicitly out of scope**: the identical gap exists at every one of `FillPaint`'s
other (non-table) call sites in `PptxDocument.Render.cs` - an ordinary shape's own gradient fill
is, today, filled exactly as before this fix, still without transform composition. This is a real,
visually-observable gap for a rotated/skewed shape with a gradient fill, but it is not
table-specific, is already fully pre-existing (not introduced or worsened by this fix), and
closing it for every non-table call site is a separable, independently-scoped follow-up left
explicitly for a future unit of work, not folded into this table-focused fix.

**3. ColorMap threading for slide-owned tables (`ParseShapeTree`/`GetSlide`)**: see the "Color Map
(`<p:clrMap>`/`<p:clrMapOvr>`) Resolution" follow-up above - its own "Scoped limitation" text has
been updated in place to describe this fix (an optional `Func<PptxColorMap>? colorMapResolver`
parameter added to `ParseShapeTree`, invoked lazily only for a `<p:graphicFrame>` table and
threaded through its own recursive `<p:grpSp>` call; `GetSlide` supplies a lazy resolver computing
that slide's own real `ResolveEffectiveColorMap(...)`, safe because `_slideCache` is keyed 1:1 per
slide; `GetMaster`/`GetLayout`'s own shared caches are intentionally left unchanged, narrowing - not
eliminating - the pre-existing known limitation to master/layout-owned tables only).

**Test coverage**: `PptxTablesTests.cs` gained
`ResolveCellRects_GridSpanMissingHMergePlaceholder_CompensatesColumnAdvance` (see "Cell-Rect
Resolution" above), `ParseTable_GridSpanCellWithGradientFill_UsesMergedWidth` and
`ParseTable_RowSpanCellWithGradientFill_UsesMergedHeight` (each comparing a merged cell's own
resolved gradient extent against an equivalent span-1 cell's, proving the merged cell's own
gradient spans its true summed dimension), and
`PaintTable_GradientFilledCellUnderNonIdentityTransform_PositionsGradientInSurfaceSpace` (a
gradient-filled cell painted through a 2x-scale `shapeToSurfaceTransform` samples the correct
stop colors at the surface-space-mapped ends of the ramp). `PptxColorMapTests.cs` gained
`GetSlide_TableCellSchemeColorFillWithSlideClrMapOvr_UsesEffectiveColorMapNotDefault`, an
integration-style test proving `GetSlide` itself (not a direct `ParseTable` call) threads a real
slide's effective color map - computed from a `<p:clrMapOvr>` overriding `bg1="dk1"` - into a
table cell's `<a:schemeClr val="bg1"/>` fill resolution, which resolves to the theme's `dk1` color
rather than the default `lt1`.
