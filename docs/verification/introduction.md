# Introduction

<!-- cspell:ignore glyf sfnt -->
<!-- cspell:ignore Visio -->

This document provides the verification design for CanvasNet, a .NET library
providing a canvas-based drawing and rendering API.

## Purpose

The purpose of this document is to serve as the verification design entry point and document how
requirements will be tested across all software items in the CanvasNet system. This
documentation enables formal review by mapping every requirement to named test scenarios, supports
compliance auditing by providing clear traceability from requirements through verification design
to tests, and ensures test completeness can be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the CanvasNet system and its
constituent software items, specifically:

- **CanvasNet (System)** — The complete .NET library system
- **Canvas (Subsystem)** — Pixel-buffer primitives: the `Surface` unit (mutable, in-memory
  32-bit RGBA pixel buffer with span-based row access) and the `Rgba32` unit
- **Codecs (Subsystem)** — Image format codecs: `BmpCodec`, `PngCodec`, `TiffCodec`,
  `JpegCodec`, and `GifCodec`, each converting to and from a `Surface` pixel buffer
- **Geometry (Subsystem)** — Vector-geometry primitives: `Rect`, `Path`, `BezierFlattening`,
  `SvgArcConverter`, and `CornerRoundEffect`
- **Drawing (Subsystem)** — Vector rasterization and stroke conversion: `PathFiller`,
  `PathStroker`, `GradientPaint`, and `TilePaint`
- **Fonts (Subsystem)** — TrueType (`glyf`-based) SFNT font loading and glyph-outline extraction:
  `TrueTypeFont` and `SystemFontCatalog`
- **CanvasNetSvg (System)** — A separate, independently-distributed software system providing
  SVG (Scalable Vector Graphics) rasterization, containing a single unit, `SvgCodec` — see
  _CanvasNetSvg System Verification_ (`canvas-net-svg.md`)
- **CanvasNetPdf (System)** — A separate, independently-distributed software system providing
  PDF page-rendering support, containing a single unit, `PdfDocument` — see
  _CanvasNetPdf System Verification_ (`canvas-net-pdf.md`)
- **CanvasNetCharts (System)** — A separate, independently-distributed software system providing
  chart support, being delivered incrementally. The current release ships two subsystems:
  `ChartModel`, containing the `ChartDocument` and `ChartRenderer` units, and `OpenXmlChart`,
  containing the `OpenXmlChartParser` unit — see _CanvasNetCharts System Verification_
  (`canvas-net-charts.md`)
- **CanvasNetPptx (System)** — A separate, independently-distributed software system providing
  PowerPoint (`.pptx`) presentation-rendering support, containing a single unit,
  `PptxDocument` — see _CanvasNetPptx System Verification_ (`canvas-net-pptx.md`)
- **CanvasNetVsdx (System)** — A separate, independently-distributed software system providing
  Microsoft Visio (`.vsdx`) diagram-rendering support, containing a single unit,
  `VsdxDocument` — see _CanvasNetVsdx System Verification_ (`canvas-net-vsdx.md`)

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — SysML v2 model-to-documentation tool
- **System.Numerics.Tensors** — vectorized bulk pixel-arithmetic runtime library
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

This verification documentation covers the same software items as the design documentation.

Version applicability: This verification design applies to all versions of CanvasNet.

The following topics are explicitly excluded from this verification documentation:

- Build pipeline and CI/CD process testing
- Infrastructure and hosting environment testing

## Companion Artifact Structure

Each software item covered by this document has corresponding artifacts in parallel directory
trees. In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}/.../{item}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/.../{item}.md` (kebab-case)
- Verification design: `docs/verification/{system}/.../{item}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- CanvasNet User Guide — the compiled User Guide document for this repository.
- CanvasNet Repository — the CanvasNet source repository hosted on
  GitHub.
