## Scenegraph Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scenegraph` subsystem of
CanvasNetPlantUml (namespace `DemaConsulting.CanvasNet.PlantUml.Scenegraph`): the `SceneModel`, `TextLayout`, and
`ScenePainter` units working together.

### Verification Approach

The subsystem is verified by laying out a label with the bundled fonts, placing it in a scene with a box as PlantUML
sizes a participant, painting the scene at a scale, and asserting the box size and the sampled pixels. No mocking is
required. Subsystem tests reside in `Scenegraph/ScenegraphTests.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values and surfaces; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. A size or a pixel that differs from the expected
value constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Scenegraph-Drawing And Text: A Label In A Box

**Tests**: `ScenegraphTests.PlantUmlScenegraph_LayoutAndPaint_LabelInsideBox`

Lays out `Alice` at 14 pixels in a box padded 7, asserts the box is 44.345 by 31.609 (PlantUML's participant), and
that the painted box shows its fill at a corner and dark text ink in its middle at scale 2.
