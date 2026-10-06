# System Verification Design

This document describes the system-level verification strategy for CanvasNetCharts.

## Verification Approach

As of this release (Phase 3), `CanvasNetCharts` contains two subsystems: `ChartModel`
(containing `ChartDocument` and `ChartRenderer`) and `OpenXmlChart` (containing
`OpenXmlChartParser`). Every `ChartRenderer` test necessarily exercises both `ChartModel` units
together: each test builds its input `Chart` via `ChartBuilder` (or the model constructors
directly) before rendering it, so `ChartRenderer`'s own unit-level test suite already provides
the cross-unit integration coverage a dedicated system-level test would otherwise exist to
provide (compare `CanvasNetSvg`, whose single `SvgCodec` unit itself integrates several of the
core `CanvasNet` system's subsystems — see _CanvasNetSvg System Verification_,
`canvas-net-svg.md`). Several of `OpenXmlChartParser`'s own tests additionally feed its parsed
`Chart` output directly into `ChartRenderer.Render`, providing the one cross-subsystem
integration this release's two subsystems share, within that same unit-level test suite. System-
level verification for this release is therefore provided entirely by `ChartDocument`'s,
`ChartRenderer`'s, and `OpenXmlChartParser`'s own comprehensive unit-level test suites — see
_ChartDocument Unit Verification_ (`canvas-net-charts/chart-model/chart-document.md`),
_ChartRenderer Unit Verification_ (`canvas-net-charts/chart-model/chart-renderer.md`), and
_OpenXmlChartParser Unit Verification_
(`canvas-net-charts/open-xml-chart/open-xml-chart-parser.md`) — which exercise every public
type's constructor and every `ChartRenderer.Render`/`OpenXmlChartParser.Parse` overload directly
through their own public API, with no mocking or stubbing required.

A dedicated system-level integration test suite remains expected in a later phase, once
`CanvasNetPptx` integration (Phase 4) exists: at that point, a system test can exercise the
complete pipeline from an opened PPTX chart part, through `OpenXmlChartParser.Parse`, to a
rendered `Surface`, the same way `CanvasNetPptx`'s own system tests exercise its multi-phase
pipeline end to end.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external network, service, or file-system interfaces as of this release: every
public API accepts and returns only in-memory values (`Chart`, `ChartSeries`, `ChartAxis`,
`ChartLegend`, `ChartTitle`, `ChartType`, `ChartLegendPosition`, `ChartRenderOptions`,
`System.Xml.Linq`'s `XDocument`/`XElement`, `ChartUnsupportedFeatureException`, and the core
`CanvasNet` system's `Rgba32`/`Surface`). `OpenXmlChartParser` reads a caller-supplied in-memory
XML element directly - it performs no file-system or network access of its own to obtain it, so
no real untrusted-input channel is simulated; its tests instead construct the XML fragment
in-process via `System.Xml.Linq` object construction (or, for one scenario, embed a real-fixture-
derived excerpt as an inline fragment - see _OpenXmlChartParser Unit Verification_). `ChartRenderer`'s
tests verify pixel output by sampling specific `Surface` coordinates (and, where another
always-present painted feature such as a gridline could contaminate an absolute region scan, by a
differential pixel-count comparison between the same chart rendered with and without the feature
under test) — the same coordinate-sampling convention `CanvasNetSvg`/`CanvasNetPdf`/
`CanvasNetPptx`'s own render tests already use. No simulation of external interfaces is required.

## System-Level Test Scenarios

See _ChartModel Subsystem Verification_ (`canvas-net-charts/chart-model.md`),
_ChartDocument Unit Verification_ (`canvas-net-charts/chart-model/chart-document.md`),
_ChartRenderer Unit Verification_ (`canvas-net-charts/chart-model/chart-renderer.md`),
_OpenXmlChart Subsystem Verification_ (`canvas-net-charts/open-xml-chart.md`), and
_OpenXmlChartParser Unit Verification_
(`canvas-net-charts/open-xml-chart/open-xml-chart-parser.md`) for the complete set of test
scenarios, each already exercising the system's full public API surface directly (there is no
additional system-level wrapping or composition to verify beyond what the unit-level tests
already exercise, given the shape described above).

## Acceptance Criteria

A system-level test run passes when every scenario referenced above passes without error or
exception beyond those explicitly asserted. Any unexpected exception, wrong exception type, or
wrong return/property value constitutes a failure.
