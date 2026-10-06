## OpenXmlChart Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `OpenXmlChart`
subsystem, as of this release containing one unit: `OpenXmlChartParser`.

### Verification Approach

The `OpenXmlChart` subsystem is verified entirely through its one unit's own unit tests (see
_OpenXmlChartParser Unit Verification Design_, `open-xml-chart/open-xml-chart-parser.md`). No
separate subsystem-level tests otherwise exist: with a single unit, there is no cross-unit
integration surface within this subsystem to additionally verify, mirroring the same rationale
already documented for the `ChartModel` subsystem (see _ChartModel Subsystem Verification
Design_, `chart-model.md`). Several of `OpenXmlChartParser`'s own tests additionally hand its
parsed output directly to `ChartModel`'s `ChartRenderer.Render`, which exercises the one
cross-subsystem call this subsystem's unit makes, within the same unit-level test suite.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `OpenXmlChartParser`'s dependencies are `System.Xml.Linq`,
  `System.Globalization`, and the `ChartModel` subsystem's own already-verified, in-house
  `ChartDocument` unit

### Acceptance Criteria

The `OpenXmlChart` subsystem's verification passes when every unit test scenario described in
_OpenXmlChartParser Unit Verification Design_ (`open-xml-chart/open-xml-chart-parser.md`) passes
without error or unexpected exception.

### Test Scenarios

The `OpenXmlChart` subsystem's test scenarios are those named in
_OpenXmlChartParser Unit Verification Design_ (`open-xml-chart/open-xml-chart-parser.md`); see
that document for the complete list.
