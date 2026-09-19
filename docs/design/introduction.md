# Introduction

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

CanvasNet is organized into two subsystems under the system level: the `Canvas` subsystem
(the `Surface` and `Rgba32` units, namespace `CanvasNet.Canvas`) and the `Codecs` subsystem
(the `BmpCodec`, `PngCodec`, `TiffCodec`, and `JpegCodec` units, namespace `CanvasNet.Codecs`,
flat — no further nesting). A third subsystem, `Drawing`, is reserved for future work (shapes,
brushes, pens, transforms) and has no folder, namespace, or documentation yet. As additional
functionality is added, further subsystems and nested subsystems would organize related units and
provide architectural boundaries with well-defined interfaces and responsibilities.

## Folder Layout

The source code folder structure mirrors the software structure organization, with file paths
and descriptions as follows:

```text
src/DemaConsulting.CanvasNet/
├── Canvas/
│   ├── Surface.cs               — Mutable, in-memory 32-bit RGBA pixel buffer
│   ├── Rgba32.cs                — Single 32-bit RGBA pixel value type
│   └── NamespaceDoc.cs          — Namespace-level XML documentation
└── Codecs/
    ├── BmpCodec.cs               — Uncompressed 24-bit/32-bit Windows BMP loader/saver
    ├── PngCodec.cs               — 8-bit Truecolor/Truecolor-with-alpha PNG loader/saver
    ├── TiffCodec.cs              — 8-bit RGB/RGBA/Grayscale, strip-based TIFF loader/saver
    ├── JpegCodec.cs              — Baseline/progressive JPEG loader and baseline JPEG saver
    └── NamespaceDoc.cs           — Namespace-level XML documentation
```

This two-subsystem folder structure reflects the small number of subsystems in the system today.
As the system grows with additional subsystems and units (including the reserved `Drawing`
subsystem), the folder structure will expand further to mirror the software architecture.

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
