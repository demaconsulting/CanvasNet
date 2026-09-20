# OTS Integration Design

This document describes the overall Off-The-Shelf (OTS) integration strategy for the CanvasNet
repository.

## Overview

CanvasNet has one shipped runtime NuGet package dependency, `System.Numerics.Tensors` (used by the
`Canvas.Surface` unit's vectorized bulk pixel operations — see _Surface Unit Design_
(`canvas-net/canvas/surface.md`) for details), and otherwise implements every unit exclusively
against the .NET Base Class Library. The OTS items listed below are a separate category: they are
build-time and quality-pipeline tools, not runtime library dependencies of the shipped package.
Each OTS item provides one stage of the documentation, requirements-traceability, testing, and
quality-reporting pipeline invoked by `build.ps1`, `lint.ps1`, and the `.github/workflows/build.yaml`
CI workflow. None of these tools are linked into, or shipped with, the compiled NuGet package.

## OTS Items

CanvasNet's OTS items fall into two categories: build-time/quality-pipeline tools (used to build,
document, and verify the repository, but never shipped) and the one runtime library dependency
that is shipped as a transitive dependency of the compiled NuGet package.

### Build-Time and Quality-Pipeline Tools

| OTS Item    | Purpose                                                              |
|-------------|----------------------------------------------------------------------|
| BuildMark   | Generates build-notes documentation from GitHub Actions metadata     |
| FileAssert  | Validates generated documents (HTML/PDF) against acceptance criteria |
| Pandoc      | Converts Markdown documentation to HTML                              |
| ReqStream   | Enforces requirements-to-test traceability                           |
| ReviewMark  | Enforces file review coverage and currency                           |
| SarifMark   | Converts CodeQL SARIF results into a markdown report                 |
| SonarMark   | Generates a SonarCloud quality report                                |
| SysML2Tools | Validates the SysML2 architecture model and renders its views to SVG |
| VersionMark | Captures and publishes tool-version information                      |
| WeasyPrint  | Converts HTML documentation to PDF                                   |
| xUnit       | Discovers and executes unit and integration tests                    |

### Runtime OTS Dependency

| OTS Item                | Purpose                                                                                    |
|-------------------------|--------------------------------------------------------------------------------------------|
| System.Numerics.Tensors | Vectorized numerics used by `Canvas.Surface`'s bulk pixel operations; shipped transitively |

Each item's individual design document (`docs/design/ots/{ots-name}.md`) records its Purpose,
Features Used, and Integration Pattern. Each item's requirements and verification evidence are
recorded in `docs/reqstream/ots/{ots-name}.yaml` and `docs/verification/ots/{ots-name}.md`
respectively.
