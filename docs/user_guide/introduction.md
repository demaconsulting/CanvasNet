# Introduction

## Purpose

This document is the user guide for CanvasNet, a .NET library providing a
canvas-based drawing and rendering API.

## Scope

This user guide covers:

- Installation of the library and of the `canvasnet` command-line tool
- Basic usage and examples
- API reference
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
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
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
OpenSCAD models, and Vega-Lite charts, from scripts or AI agents), install the `canvasnet` .NET tool, which bundles the
libraries. The
chapter on the command-line tool describes it in full:

```bash
dotnet tool install --global DemaConsulting.CanvasNet.Tool
```
