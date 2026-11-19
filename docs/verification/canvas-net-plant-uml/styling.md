## Styling Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Styling` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Styling`): the `SkinTheme` and `SkinParams` units working together.

### Verification Approach

The subsystem is verified by reading the `skinparam` statements of a full original diagram (preprocessed by the
Syntax subsystem, used as is) and applying them over the default look, then asserting the changed and unchanged
styles and the reports. No mocking is required; the internal types are reached through the test assembly's
`InternalsVisibleTo` access. Subsystem tests reside in `Styling/StylingTests.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. A style value or a report that differs from the
expected literal constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Styling-DiagramLook: Skin Parameters Change The Default Look

**Tests**: `StylingTests.PlantUmlStyling_SkinParamsOverDefaultLook_GiveDiagramLook`

Applies a note block (background and font color), `shadowing false`, and the unsupported `handwritten` parameter,
and asserts the note's colors, the shadowing flag, an unchanged participant style, and one warning at line 7.
