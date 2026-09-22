# Introduction

<!-- cspell:ignore glyf sfnt cmap loca hmtx hhea codepoint -->

This document provides the detailed design for CanvasNet, a .NET library
providing a canvas-based drawing and rendering API.

## Purpose

The purpose of this document is to serve as the design entry point and provide detailed design
specifications for the CanvasNet system. This documentation enables formal code
review by providing implementation specifications, supports compliance auditing by maintaining
clear traceability from requirements through design to code, aids maintenance by documenting
system structure and interactions, and ensures quality assurance through detailed technical
specifications.

This document is intended for:

- Software developers implementing and maintaining the system
- Code reviewers validating implementation against design
- Compliance auditors tracing requirements through design to implementation
- Quality assurance teams validating system behavior

## Scope

This document covers the detailed design of the CanvasNet system and its constituent
software items, specifically:

- **CanvasNet (System)** — The complete .NET library system
- **Canvas (Subsystem)** — Pixel-buffer primitives: the `Surface` unit (mutable, in-memory
  32-bit RGBA pixel buffer with span-based row access) and the `Rgba32` unit
- **Codecs (Subsystem)** — Image format codecs: `BmpCodec`, `PngCodec`, `TiffCodec`, and
  `JpegCodec`, each converting to and from a `Surface` pixel buffer
- **Geometry (Subsystem)** — Vector-geometry primitives, distinct from the `Drawing`
  subsystem (which covers rasterization built on top of these primitives): the `Rect` unit
  (axis-aligned bounding rectangle), the `Path` unit (immutable vector path and its
  `PathBuilder`, covering the supporting `Subpath`, `PathCommand`, and `PathCommandType` types
  inline), the `BezierFlattening` unit (adaptive Bezier curve flattening), and the
  `SvgArcConverter` unit (SVG-style elliptical arc to Bezier conversion)
- **Drawing (Subsystem)** — An antialiased scanline-coverage fill rasterizer for closed
  `Geometry.Path` geometry with solid-color or gradient paint: the `PathFiller` unit (a public
  static `Fill` entry point, covering the supporting `FillRule` enum and the internal
  `EdgeFlattener`/`ScanlineRasterizer` helpers inline), the `PathStroker` unit (a public static
  `Stroke` entry point, covering the supporting `LineCap`/`LineJoin`/`StrokeStyle` types and the
  internal `StrokePathFlattener`/`DashSplitter`/`StrokeOutliner` helpers inline), and the
  `GradientPaint` unit (the public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/
  `GradientSpread` types and the internal `GradientEvaluator` helper)
- **Fonts (Subsystem)** — TrueType (`glyf`-based) SFNT font support: the `TrueTypeFont` unit and
  its internal `SfntContainer`/`CmapTable`/`GlyfLocaReader`/`HmtxHheaReader`/`KernTable` helpers,
  producing `Geometry.Path` glyph outlines plus metrics and kerning

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — architecture model lint and diagram rendering tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

Version applicability: This design applies to all versions of CanvasNet.

The following topics are explicitly excluded from this design documentation:

- External library internals and third-party OTS components
- Build pipeline configuration and CI/CD processes
- Deployment, packaging, and distribution mechanisms
- Infrastructure and hosting environment details
- Test projects and test infrastructure

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` and rendered to the
diagram below by SysML2Tools as part of the build pipeline. AI agents should query the
SysML2 model directly (see the `sysml2tools-query` skill) rather than parsing this
diagram or the prose below.

![Software Structure](SoftwareStructureView.svg)

CanvasNet is organized into five subsystems under the system level: the `Canvas` subsystem
(the `Surface` and `Rgba32` units, namespace `DemaConsulting.CanvasNet.Canvas`), the `Codecs`
subsystem (the `BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec` units, namespace
`DemaConsulting.CanvasNet.Codecs`, flat — no further nesting), the `Geometry` subsystem (the
`Rect`, `Path`, `BezierFlattening`, and `SvgArcConverter` units, namespace
`DemaConsulting.CanvasNet.Geometry`, flat — no further nesting), the `Drawing` subsystem (the
`PathFiller` unit, covering the supporting `FillRule` enum and the internal
`EdgeFlattener`/`ScanlineRasterizer` helpers inline, the `PathStroker` unit, covering the
supporting `LineCap`/`LineJoin`/`StrokeStyle` types and the internal
`StrokePathFlattener`/`DashSplitter`/`StrokeOutliner` helpers inline, and the `GradientPaint`
unit, covering the public `Gradient`/`LinearGradient`/`RadialGradient`/`GradientStop`/
`GradientSpread` types and the internal `GradientEvaluator` helper inline, namespace
`DemaConsulting.CanvasNet.Drawing`, flat — no further nesting), and the `Fonts` subsystem (the
`TrueTypeFont` unit, covering the internal `SfntContainer`/`CmapTable`/`GlyfLocaReader`/
`HmtxHheaReader`/`KernTable` helpers inline, namespace `DemaConsulting.CanvasNet.Fonts`, flat —
no further nesting). As additional functionality is added, further subsystems and nested
subsystems would organize related units and provide architectural boundaries with well-defined
interfaces and responsibilities.

## Folder Layout

The source code folder structure mirrors the software structure organization, with file paths
and descriptions as follows:

```text
src/DemaConsulting.CanvasNet/
├── Canvas/
│   ├── Surface.cs               — Mutable, in-memory 32-bit RGBA pixel buffer
│   ├── Rgba32.cs                — Single 32-bit RGBA pixel value type
│   └── NamespaceDoc.cs          — Namespace-level XML documentation
├── Codecs/
│   ├── BmpCodec.cs               — Uncompressed 24-bit/32-bit Windows BMP loader/saver
│   ├── PngCodec.cs               — 8-bit Truecolor/Truecolor-with-alpha PNG loader/saver
│   ├── TiffCodec.cs              — 8-bit RGB/RGBA/Grayscale, strip-based TIFF loader/saver
│   ├── JpegCodec.cs              — Baseline/progressive JPEG loader and baseline JPEG saver
│   └── NamespaceDoc.cs           — Namespace-level XML documentation
├── Drawing/
│   ├── FillRule.cs                — Nonzero/even-odd fill-rule enumeration
│   ├── EdgeFlattener.cs           — Converts a Path's subpaths into closed polygons
│   ├── ScanlineRasterizer.cs      — Analytic coverage-accumulation scanline rasterizer
│   ├── PathFiller.cs              — Public entry point: fills a Path onto a Surface
│   ├── LineCap.cs                 — Stroke end-cap enumeration
│   ├── LineJoin.cs                — Stroke corner-join enumeration
│   ├── StrokeStyle.cs             — Immutable stroke-style configuration snapshot
│   ├── StrokePathFlattener.cs     — Flattens subpaths while preserving open/closed state
│   ├── DashSplitter.cs            — Applies dash-array and dash-offset semantics
│   ├── StrokeOutliner.cs          — Converts stroked polylines into outline polygons
│   ├── PathStroker.cs             — Public entry point: strokes a Path into outline geometry
│   ├── GradientSpread.cs          — Gradient repeat-beyond-extent mode enumeration
│   ├── GradientStop.cs            — Single offset/color stop within a gradient
│   ├── Gradient.cs                — Abstract base for gradient paint definitions
│   ├── LinearGradient.cs          — Gradient paint that varies along a straight axis
│   ├── RadialGradient.cs          — Gradient paint that varies radially from a center point
│   ├── GradientEvaluator.cs       — Resolves a gradient definition to a color at a point
│   └── NamespaceDoc.cs            — Namespace-level XML documentation
├── Fonts/
│   ├── TrueTypeFont.cs            — Public TrueType font loader/query entry point
│   ├── SfntContainer.cs           — SFNT offset-table and directory parser
│   ├── CmapTable.cs               — Unicode codepoint-to-glyph-index lookup
│   ├── GlyfLocaReader.cs          — Glyph location parsing and outline decoding
│   ├── HmtxHheaReader.cs          — Horizontal metrics parsing and advance-width lookup
│   ├── KernTable.cs               — Format-0 horizontal kerning lookup
│   └── NamespaceDoc.cs            — Namespace-level XML documentation
└── Geometry/
    ├── Rect.cs                    — Axis-aligned bounding rectangle (position plus size)
    ├── PathCommandType.cs         — Enumeration of path drawing command kinds
    ├── PathCommand.cs             — Tagged-union path drawing command value
    ├── Subpath.cs                 — One independent contour of a path
    ├── Path.cs                    — Immutable vector path (ordered collection of subpaths)
    ├── PathBuilder.cs             — Mutable, fluent builder that produces a Path
    ├── BezierFlattening.cs        — Adaptive quadratic/cubic Bezier curve flattening
    ├── SvgArcConverter.cs         — SVG-style elliptical arc to cubic Bezier conversion
    └── NamespaceDoc.cs            — Namespace-level XML documentation
```

This five-subsystem folder structure reflects the small number of subsystems in the system
today. As the system grows with additional subsystems and units, the folder structure will
expand further to mirror the software architecture. `Canvas/Surface.cs` also gained a
`CompositeOverSpan` method used internally by `Drawing/PathFiller.cs`, and the `Drawing`
subsystem now includes the additional stroking files listed above, along with the gradient
paint files added for linear/radial gradient support in `PathFiller`.

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## Companion Artifact Structure

Each software item has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)
- SysML2 model: `docs/sysml2/model/{system}/.../{item}.sysml` (kebab-case)
- Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasNet User Guide — the compiled User Guide document for this repository.
- CanvasNet Repository — the CanvasNet source repository hosted on
  GitHub.
<!-- cspell:ignore Outliner -->
