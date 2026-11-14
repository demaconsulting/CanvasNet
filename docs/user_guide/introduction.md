# Introduction

## Purpose

This document is the user guide for CanvasNet, a .NET library providing a
canvas-based drawing and rendering API.

CanvasNet is a family of packages. The diagram below shows them and how they fit together: install the core package
alone for images and drawing, add the format packages you need, use the Viewer to open any supported file, or run the
`canvasnet` command-line tool, which bundles all of them.

![CanvasNet package architecture: applications use the Viewer and the canvasnet tool uses it too; the Viewer opens
the seven format packages (PDF, SVG, PowerPoint, Visio, OpenSCAD, Vega-Lite, and Mermaid); PowerPoint also uses the
SVG and Charts packages and OpenSCAD uses the Render3D package; every package builds on the DemaConsulting.CanvasNet
core with its canvas, codecs, drawing, fonts, and file access](architecture.svg)

## Scope

This user guide covers:

- Installation of the library and of the `canvasnet` command-line tool
- Basic usage and examples
- API reference
- Rendering Mermaid flowcharts, sequence diagrams, class diagrams, state diagrams, entity relationship diagrams, pie
  charts, gantt charts, mindmaps, packet diagrams, and git graphs (`DemaConsulting.CanvasNet.Mermaid`), with a gallery
  of original diagrams
- The `canvasnet` command-line tool: its commands, options, JSON output, error codes, and self-validation

## References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)

# Continuous Compliance

CanvasNet follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: markdownlint, TextLint, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build
- **API Documentation**: Markdown API reference for all public types and members is generated
  automatically and distributed in the `api/` folder of the NuGet package

# Installation

Install the library using the .NET CLI:

```bash
dotnet add package DemaConsulting.CanvasNet
```

To use CanvasNet from the command line instead (to inspect, render, convert, and transform images, documents,
OpenSCAD models, Vega-Lite charts, and Mermaid diagrams, from scripts or AI agents), install the `canvasnet` .NET tool,
which bundles the libraries. The chapter on the command-line tool describes it in full:

```bash
dotnet tool install --global DemaConsulting.CanvasNet.Tool
```
