# System Verification Design

This document describes the system-level verification strategy for CanvasNetCharts.

## Verification Approach

As of this release (Phase 1), `CanvasNetCharts` contains a single subsystem (`ChartModel`)
containing a single unit (`ChartDocument`); there is no cross-unit or cross-subsystem integration
surface yet for a dedicated system-level integration test to exercise (compare `CanvasNetSvg`,
whose single `SvgCodec` unit itself integrates several of the core `CanvasNet` system's
subsystems — see _CanvasNetSvg System Verification_, `canvas-net-svg.md`). System-level
verification for this release is therefore provided entirely by `ChartDocument`'s own
comprehensive unit-level test suite — see _ChartDocument Unit Verification_
(`canvas-net-charts/chart-model/chart-document.md`) — which exercises every public type's
constructor directly through its own public API, with no mocking or stubbing required.

A dedicated system-level integration test suite is expected to be introduced in Phase 2, once
`ChartRenderer` exists: at that point, a system test can exercise a realistic scenario spanning
both `ChartModel` (constructing a `Chart` via `ChartBuilder`) and `ChartRenderer` (painting that
`Chart` onto a core `CanvasNet.Canvas.Surface`), the same way `CanvasNetPptx`'s own system tests
exercise its multi-phase pipeline end to end.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own test fixtures; no shared state between tests

## External Interface Simulation

The system has no external network, service, or file-system interfaces as of this release: every
public API accepts and returns only in-memory values (`Chart`, `ChartSeries`, `ChartAxis`,
`ChartLegend`, `ChartTitle`, `ChartType`, `ChartLegendPosition`, and the core `CanvasNet` system's
`Rgba32`). No simulation of external interfaces is required.

## System-Level Test Scenarios

See _ChartModel Subsystem Verification_ (`canvas-net-charts/chart-model.md`) and
_ChartDocument Unit Verification_ (`canvas-net-charts/chart-model/chart-document.md`) for the
complete set of test scenarios, each already exercising the system's full public API surface
directly (there is no additional system-level wrapping or composition to verify beyond what the
unit-level tests already exercise, given the single-unit shape described above).

## Acceptance Criteria

A system-level test run passes when every scenario referenced above passes without error or
exception beyond those explicitly asserted. Any unexpected exception, wrong exception type, or
wrong return/property value constitutes a failure.
