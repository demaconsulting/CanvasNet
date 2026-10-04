# System Design

This document provides the system-level design for CanvasNetPptx.

![CanvasNetPptx Structure](CanvasNetPptxView.svg)

<!-- cspell:ignore ooxml pptx srgb xfrm prst cust -->

## Architecture

CanvasNetPptx is a .NET library providing PowerPoint (`.pptx`) presentation-rendering support,
distributed as its own NuGet package (`DemaConsulting.CanvasNet.Pptx`, namespace
`DemaConsulting.CanvasNet.Pptx`), independent of, but depending on, the core `CanvasNet` system
(its own separate package, `DemaConsulting.CanvasNet`) — see the Dependencies section below. The
system consists of a single implemented unit:

- **PptxDocument** (namespace `DemaConsulting.CanvasNet.Pptx`, folder
  `src/DemaConsulting.CanvasNet.Pptx/`, flat — no further nesting): a hand-rolled OOXML (Office
  Open XML) package parser and, in later phases, presentation-rendering codec. See _PptxDocument
  Unit Design_ (`canvas-net-pptx/pptx-document.md`).

`CanvasNetPptx` is modeled as its own top-level software system (rather than as a further unit of
the `CanvasNet` system's `Codecs` subsystem) because it is distributed as its own
independently-versioned NuGet package — per `software-items.md`'s rule that a software package
contains exactly one software system, a system whose entire content ships in a distinct package
must itself be modeled as a distinct system, not folded into the package it depends on.
`CanvasNetPptx` contains exactly one unit, so no Subsystem tier is interposed between the system
and `PptxDocument`: inserting one here would not separate anything (there is nothing else in the
system to separate it from), mirroring `CanvasNetPdf`'s own single-unit precedent (see
_CanvasNetPdf System Design_, `docs/design/canvas-net-pdf.md`).

**Phased delivery — this is an in-progress, multi-phase feature.** A `.pptx` file is an OOXML
package: a ZIP archive whose parts (`[Content_Types].xml`, `_rels/.rels`, `ppt/presentation.xml`,
each slide/layout/master XML part, and so on) are discovered by following a chain of
relationships from the package root. **Phase 1a** implemented only this underlying package layer:
`PptxDocument.Open` opens a `.pptx` file as a read-only `ZipArchive`, resolves
`[Content_Types].xml`'s default extension-to-content-type mappings and part-specific overrides,
and resolves package-level (`_rels/.rels`) and per-part (`{dir}/_rels/{partName}.rels`)
relationships, including OPC's relative-target resolution rules (a `"../"` parent-directory
traversal segment, resolved relative to the referencing part's own directory, not the package
root). `Open` succeeds on any well-formed OOXML/ZIP package, even one that is not actually a
presentation, since nothing beyond the package layer was validated that phase.

**Phase 1b (this release)** adds the presentation/theme/master/layout/slide model and the
placeholder property-inheritance resolver, building on the Phase 1a package layer: `Open` now
additionally locates `ppt/presentation.xml` (via the package's `/officeDocument` relationship),
parses its declared slide size and ordered slide list (exposed as the new public `SlideSize`/
`SlideCount` properties and `GetSlideSizeInPixels` conversion method), and rejects a package that
is not a navigable presentation with at least one declared slide. Each slide master/layout/slide's
placeholder shapes, and a slide master's theme (color/font scheme, resolved into
`DemaConsulting.CanvasNet.Canvas.Rgba32` values), are resolved lazily on first access through new
`internal` members. A new `internal` resolver (`ResolvePlaceholderProperties`) implements the
verified ECMA-376 (ISO/IEC 29500) §19.3.1.36 placeholder matching algorithm, resolving a slide
placeholder's effective `<p:spPr>`/`<p:txBody>/<a:lstStyle>` property fragments through its
layout and master. No shape geometry/paint rendering, non-placeholder (freeform) shape parsing,
font loading, or rendering surface was implemented that phase - deferred to later phases.

**Phase 1c** adds DrawingML shape geometry and paint resolution, building on the
Phase 1b model: a shape's `<a:xfrm>` position/rotation/flip resolves to a transform; a
`<p:grpSp>` group's `chOff`/`chExt` child coordinate space composes correctly with its children's
own transforms; a shape's `<a:prstGeom>` (24 supported preset names) or `<a:custGeom>` (parsed
`moveTo`/`lnTo`/`cubicBezTo`/`quadBezTo`/`close` path commands) resolves to a concrete path; and a
shape's `<a:solidFill>`/`<a:gradFill>`/`<a:noFill>` fill and `<a:ln>` stroke resolve to concrete
paint, including scheme-color lookup through the resolved theme, the `lumMod`/`lumOff`/`shade`/
`tint`/`alpha` color-transform pipeline, and a stroked outline realized via the core
`PathStroker`. Pattern/picture fill, radial/path gradients, `<a:avLst>` adjustment-value parsing,
and full group-shape rendering semantics beyond transform composition remain explicitly deferred
(see _PptxDocument Unit Design_'s "Geometry and Paint (Phase 1c)" section for the complete
supported/deferred boundary). **No non-placeholder (freeform) shape _enumeration_ from a slide's
full `<p:spTree>`, font loading, or rendering surface is implemented yet** - all deferred to later
phases, which will build on this model to actually render a presentation's slides.

**Phase 1d (this release)** adds DrawingML text body parsing, attribute-level run/paragraph
property inheritance (including a slide master's own `<p:txStyles>` title/body/other style
buckets), word-wrap/alignment/vertical-anchor/autofit text layout, and glyph-ink rendering onto a
core `Canvas.Surface`, building on the Phase 1b/1c models: a shape's `<p:txBody>` resolves to an
ordered paragraph/run structure; each run's/paragraph's effective properties resolve through the
placeholder/layout/master/theme inheritance chain, attribute-by-attribute; the resolved text is
word-wrapped, aligned, vertically anchored, and (where a `normAutofit` element is present)
autofit within the owning shape's own bounding box; and the resulting glyphs are painted onto a
`Canvas.Surface` via the core `Fonts.TrueTypeFont`/`SystemFontCatalog` and `Drawing.PathFiller`,
mirroring `CanvasNetPdf`'s own glyph-painting pattern. Bullets/numbering, full text justification,
`<a:spAutoFit>` shape-resize autofit, kerning, text clipping on overflow, and a full per-slide
rendering entry point remain explicitly deferred (see _PptxDocument Unit Design_'s "Text Layout
and Rendering (Phase 1d)" section for the complete supported/deferred boundary). **No non-
placeholder (freeform) shape _enumeration_ from a slide's full `<p:spTree>`, nor a full per-slide
public rendering entry point, is implemented yet** - deferred to a later phase.

## External Interfaces

The system exposes the following public API to external consumers, all on the sealed,
`IDisposable` `PptxDocument` class:

- **PptxDocument.Open(Stream stream)** / **PptxDocument.Open(string path)**: Opens and parses a
  `.pptx` package exactly once, from a stream or a file path, buffering the input fully in
  memory. `Open(Stream)` never takes ownership of (or disposes) the caller's stream. `Open(string)`
  opens, reads, and closes its own internal `FileStream` before returning. Throws
  `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an
  empty/whitespace-only `path`, and `InvalidDataException` for an unreadable/corrupt ZIP, a
  package missing `[Content_Types].xml`/`_rels/.rels`, or (as of Phase 1b) a package that is not
  a navigable presentation with a valid slide size and at least one declared slide.
- **PptxDocument.SlideCount** (`int`, Phase 1b): The presentation's declared slide count.
- **PptxDocument.SlideSize** (`PptxSlideSize`, Phase 1b): The presentation's declared slide size,
  in EMU.
- **PptxDocument.GetSlideSizeInPixels(float dpi)** (Phase 1b): Converts `SlideSize` to pixels at
  the given resolution. Throws `ArgumentOutOfRangeException` for a non-positive/non-finite `dpi`.
- **PptxDocument.Dispose()**: Idempotent; releases the underlying `ZipArchive` and its backing
  buffer. No other public member may be called afterward without throwing
  `ObjectDisposedException` (enforced, as of Phase 1b, by `SlideCount`/`SlideSize`/
  `GetSlideSizeInPixels`).

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| -------------------------------------- | ---------------- | ------------------------------------ | ------------------------------- |
| `PptxDocument.Open(...)` | Inbound/Outbound | Method call / `PptxDocument` return | Valid `.pptx`/OPC presentation stream or path |
| `PptxDocument.SlideCount` / `SlideSize` | Outbound | Property access | Document must not be disposed |
| `PptxDocument.GetSlideSizeInPixels(...)` | Inbound/Outbound | Method call / `(int, int)` return | Positive, finite `dpi`; document must not be disposed |
| `PptxDocument.Dispose()` | Inbound | Method call | Safe to call more than once |
<!-- markdownlint-enable MD013 -->

The package layer's `ResolvePart`/`ResolveRelationship` methods, and every Phase 1b
presentation/theme/master/layout/slide/inheritance member, are `internal` (not part of the
public surface), used by later phases of `PptxDocument` itself to navigate the package and
resolve placeholder properties - see _PptxDocument Unit Design_
(`canvas-net-pptx/pptx-document.md`) for their complete contract.

## Dependencies

`CanvasNetPptx` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Pptx/DemaConsulting.CanvasNet.Pptx.csproj`), matching every other
package-bounded system in this repository's architecture convention of depending on the core
system for its shared types (`Canvas.Surface`, `Codecs.UnsupportedImageFeatureException`, and so
on, as later phases add presentation rendering).

**As of Phase 1b, `CanvasNetPptx` uses the `CanvasNet` core system's `Canvas` subsystem** -
specifically `Canvas.Rgba32`, to represent a theme's resolved 12-slot color scheme (each
`<a:srgbClr>`/`<a:sysClr>` slot resolved to a concrete `Rgba32` value). **As of Phase 1c,
`CanvasNetPptx` additionally uses the `CanvasNet` core system's `Geometry` and `Drawing`
subsystems** - `Geometry.Path`/`PathBuilder` to build and compose resolved shape geometry, and
`Drawing.PathStroker`/`StrokeStyle` to realize a resolved stroke's outline, plus `Canvas.Gradient`/
`LinearGradient` to represent a resolved gradient fill. **As of Phase 1d (this release),
`CanvasNetPptx` additionally uses the `CanvasNet` core system's `Fonts` subsystem** -
`Fonts.TrueTypeFont`/`SystemFontCatalog` to resolve a DrawingML typeface name hint to a concrete
font and extract its glyph outlines - and the core `Drawing.PathFiller`/`Canvas.Surface` types to
paint resolved glyph ink onto a rendering surface, mirroring `CanvasNetPdf`'s own
font-resolution/glyph-painting pattern. The package layer itself still relies
solely on the .NET base class library's `System.IO.Compression.ZipArchive` (reading
the `.pptx` ZIP container) and `System.Xml.Linq.XDocument`/`XElement` (parsing
`[Content_Types].xml`, each `.rels` part, and, as of Phase 1b, every presentation/theme/master/
layout/slide part). The `ProjectReference` to `CanvasNet` now has corresponding `dependency`
edges in this system's own SysML2 model (`docs/sysml2/model/canvas-net-pptx.sysml`):
`dependency usesCanvas from CanvasNetPptxSystem to Canvas;` (added at Phase 1b, the phase that
actually first uses a `Canvas` subsystem type) and, added at Phase 1c,
`dependency usesGeometry from CanvasNetPptxSystem to Geometry;`/
`dependency usesDrawing from CanvasNetPptxSystem to Drawing;`, mirroring `CanvasNetPdf`'s and
`CanvasNetSvg`'s own precedent of adding a `dependency` edge only at the phase that actually
first uses the referenced subsystem (for example `CanvasNetPdf`'s `Fonts` dependency was added
only at its own Phase 4, not at Phase 1). Added this phase (Phase 1d), mirroring that same
precedent: `dependency usesFonts from CanvasNetPptxSystem to Fonts;`. No other `CanvasNet`
subsystem (`Codecs`) is used yet; a future phase that implements picture fill may add a further
`dependency` edge at that time.

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetPptx` are produced by this repository, so it is neither an OTS Software Item (not a
third-party/external-program dependency) nor a Shared Package (that category is scoped to a
package produced by a _different_ repository within the same program). `CanvasNetPptx`
introduces no new OTS Software Item beyond those already used to build and verify the
`CanvasNet` system (BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark,
VersionMark, WeasyPrint, xUnit) — see _OTS Integration Design_ (`docs/design/ots.md`).

## Risk Control Measures

`PptxDocument.Open` parses an externally-supplied, potentially untrusted `.pptx` package (a ZIP
archive whose structure, `[Content_Types].xml`, relationship parts, and - as of Phase 1b -
presentation/theme/master/layout/slide XML content may be malformed, incomplete, or
adversarially constructed). Risk control remains deliberately simple and entirely fail-closed,
with no tolerated-recovery path (unlike `CanvasNetPdf`'s linear-scan fallback): an
unreadable/corrupt ZIP, a missing `[Content_Types].xml` or `_rels/.rels`, an unresolvable
relationship ID, an external-target relationship, a relationship target that would traverse past
the package root, a package with no navigable presentation part, or a presentation with no valid
slide size/slide list are all immediately rejected with a thrown `InvalidDataException` rather
than silently producing an incomplete or incorrect package view. Phase 1b's per-slide/layout/
master/theme validation is deliberately lazy (only performed when that specific part is first
accessed via `GetSlide`/`GetLayout`/`GetMaster`/`GetTheme`), matching Phase 1a's own eager
(package-level)/lazy (per-part) validation split - a documented scope boundary, not an oversight.
Phase 1c's geometry/paint resolvers extend this same fail-closed philosophy to shape content: a
malformed `<a:xfrm>`/`<a:custGeom>`/color-definition element throws `InvalidDataException`
exactly like a malformed Phase 1a/1b part, and a recognized-but-unsupported DrawingML construct
(an unsupported preset geometry name, color-definition kind, gradient kind, or pattern/picture
fill) throws the new `PptxUnsupportedFeatureException` rather than being silently substituted
with incorrect geometry or paint - the same fail-closed-over-silent-approximation choice Phase
1a/1b already made for the package layer.

## Data Flow

**`.pptx` open/parse path:**

1. **Input**: A `.pptx` package (stream or file path)
2. **Validation**: `Open` rejects a null `stream`/`path` with `ArgumentNullException` and an
   empty/whitespace `path` with `ArgumentException`
3. **Processing**: Fully buffers the input into memory, opens it as a read-only `ZipArchive`
   (rejecting an unreadable/corrupt archive with `InvalidDataException`), parses
   `[Content_Types].xml` into its default extension and part-specific override content-type
   mappings, eagerly parses the package-level `_rels/.rels` relationships part (rejecting a
   missing or malformed part with `InvalidDataException`), then locates and parses
   `ppt/presentation.xml` (via the package's `/officeDocument` relationship), reading its
   declared slide size and resolving its ordered slide list (rejecting a non-navigable or
   structurally invalid presentation with `InvalidDataException`)
4. **Output**: A new `PptxDocument` instance exposing `SlideCount`/`SlideSize`/
   `GetSlideSizeInPixels` directly, and whose internal `ResolvePart`/`ResolveRelationship`/
   `GetTheme`/`GetMaster`/`GetLayout`/`GetSlide`/`ResolvePlaceholderProperties` methods can now
   navigate the package's content-type, relationship, and placeholder-inheritance graph — the
   presentation-level state is parsed once at `Open()` time; each slide/layout/master/theme is
   parsed lazily, on first access, and cached, reused across every subsequent call on that
   instance

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **One-time parse, reused across calls**: `Open` performs the full package-layer parse exactly
  once; later phases' presentation-specific members will all read from that already-resolved
  state rather than re-parsing on every call
- **Decode-only**: `PptxDocument` provides no encode/`Save` direction, matching every other
  decode-only codec in this repository

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs and the `CanvasNet` system's own public API, available across all target frameworks. No
platform-specific native interop, OS-specific APIs, or framework-version-specific features are
used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, referencing the
  `CanvasNet` package as an ordinary NuGet dependency
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
