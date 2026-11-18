# System Verification Design

This document describes the system-level verification strategy for CanvasNetPlantUml.

## Verification Approach

`CanvasNetPlantUml` is verified at three tiers with xUnit v3 in the test project
`test/DemaConsulting.CanvasNet.PlantUml.Tests/`, which references only the PlantUml package and, through it, the core
`CanvasNet` package (no other format package, so the tests stay independent of the Viewer):

- **System tests** (described here) use only the public API, as a consumer would: they open original-work diagrams
  from in-memory text, streams, and temporary files with `PlantUmlDocument.Parse` and `PlantUmlDocument.Open`, supply
  `PlantUmlOpenOptions` and `PlantUmlLimits`, and assert on observable outputs: the metadata properties and the
  exceptions with their codes, locations, and hints, the sizes and sampled pixels of rendered surfaces, and the reports
  delivered to the handlers. The system test classes are `PlantUmlSystemIntegrationTests` (reading, metadata, errors,
  preprocessor, limits) and `PlantUmlUnsupportedDiagramTests` (the types that are not drawn, strict mode).
- **Subsystem tests** exercise each internal subsystem through its own entry points (see the subsystem verification
  documents, for example *Syntax Subsystem Verification Design*).
- **Unit tests** exercise each unit in isolation (see the unit verification documents).

The test methods of the units and subsystems are prefixed `PlantUml` (`PlantUmlPreprocessor_...`,
`PlantUmlSyntax_...`), and the system test methods `CanvasNetPlantUml_...`, so that the requirements trace never mixes
the evidence of this system with the Mermaid system's units of the same short names.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on Windows, Linux, and macOS, for net8.0, net9.0, net10.0, and
  net11.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: temporary files written and deleted by the tests; no network, no Java, no Graphviz
- **Mocking**: None

## Acceptance Criteria

A system-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted, on every platform and runtime of the platform requirements. A type, a count, a code, a location, or a hint
that differs from the expected literal constitutes a failure.

## Test Scenarios

### CanvasNetPlantUml-Open: The Entry Points Agree

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Open_StringStreamAndPath_Agree`

Opens the same sequence diagram (with a comment before `@startuml` and a variable) from a string, a stream, and a
temporary file, and asserts that all three are sequence diagrams.

### CanvasNetPlantUml-LocatedErrors: An Invalid Statement Fails Located

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Open_InvalidText_FailsLocated`

Parses a diagram with a statement no type accepts under a source name, and asserts the code `plantuml-syntax-error`,
the location `bad.puml:3:1`, and a hint.

### CanvasNetPlantUml-DiagramTypes: Every Type Is Recognized

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_OtherDiagramTypes_AreRecognized`

Opens a class diagram, a state diagram, a use case diagram, a mindmap, a gantt chart, and a JSON diagram, and asserts
the type name of each and that none is drawn by this version.

### CanvasNetPlantUml-Preprocessor: The Subset Is Applied

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Preprocessor_SubsetIsApplied`

Parses a diagram whose type is decided by a macro and a variable after a comment, and asserts the detected type.

### CanvasNetPlantUml-Limits: An Oversized Text Is Refused

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Limits_OversizedText_IsRefused`

Parses a text of fifty messages under a size limit of one hundred characters and asserts an `InvalidDataException`.

### CanvasNetPlantUml-Render: Measuring Reports What Rendering Reports

**Tests**: `PlantUmlUnsupportedDiagramTests.CanvasNetPlantUml_UnsupportedType_MeasureReportsAsRender`

Measures a mindmap and asserts the size of 20 by 20 and the one report that rendering delivers.

### CanvasNetPlantUml-UnsupportedTypes: An Empty Picture And A Report

**Tests**: `PlantUmlUnsupportedDiagramTests.CanvasNetPlantUml_UnsupportedType_RendersEmptyPictureAndReports`

Renders a class diagram and asserts a fully transparent picture of 20 by 20, no warning, and one
`plantuml-diagram-unsupported` report at `car.puml:2:1` that names the type and has a hint.

### CanvasNetPlantUml-StrictMode: The First Unsupported Construct Throws

**Tests**: `PlantUmlUnsupportedDiagramTests.CanvasNetPlantUml_UnsupportedType_StrictThrows`

Renders a class diagram in strict mode and asserts the exception's feature and that its report was delivered first.

### CanvasNetPlantUml-Platform: Every Platform And Runtime

**Tests**: `PlantUmlSystemIntegrationTests.CanvasNetPlantUml_Open_StringStreamAndPath_Agree` with the `windows@`,
`ubuntu@`, `macos@`, `net8.0@`, `net9.0@`, `net10.0@`, and `net11.0@` source filters

The representative test is evidence for each platform and runtime only from a test run on that platform or runtime.
