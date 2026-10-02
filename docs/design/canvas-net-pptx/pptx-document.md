## PptxDocument

![CanvasNetPptx Structure](CanvasNetPptxView.svg)

<!-- cspell:ignore ooxml pptx navigations hlink srgb -->

`PptxDocument` is distributed as the separate `DemaConsulting.CanvasNet.Pptx` NuGet package
(namespace `DemaConsulting.CanvasNet.Pptx`), which references the core `DemaConsulting.CanvasNet`
package.

The `PptxDocument` class is the sole software unit of the `CanvasNetPptx` system. As of Phase 1a,
its dependencies are limited to the .NET base class library's `System.IO.Compression.ZipArchive`
(reading the `.pptx` ZIP container) and `System.Xml.Linq` (parsing `[Content_Types].xml` and each
`.rels` relationships part) — no `CanvasNet` core-system type is used yet (see _CanvasNetPptx
System Design_'s Dependencies section, `../canvas-net-pptx.md`).

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
