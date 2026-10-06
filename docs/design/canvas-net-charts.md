# System Design

This document provides the system-level design for CanvasNetCharts.

![CanvasNetCharts Structure](CanvasNetChartsView.svg)

## Architecture

CanvasNetCharts is a .NET library providing chart support, distributed as its own NuGet package
(`DemaConsulting.CanvasNet.Charts`, namespace `DemaConsulting.CanvasNet.Charts`), independent of,
but depending on, the core `CanvasNet` system (its own separate package,
`DemaConsulting.CanvasNet`) — see the Dependencies section below.

`CanvasNetCharts` is being delivered incrementally. As of this release (Phase 1), the system
consists of a single subsystem:

- **ChartModel** (folder `src/DemaConsulting.CanvasNet.Charts/`, flat — no further nesting): a
  public, immutable, validating chart data model (`Chart`/`ChartSeries`/`ChartAxis`/
  `ChartLegend`/`ChartTitle`/`ChartType`) and the `ChartBuilder` fluent construction API,
  contained in a single implemented unit, `ChartDocument`. See
  _ChartModel Subsystem Design_ (`canvas-net-charts/chart-model.md`).

No Subsystem tier would normally be needed for a system containing only one unit (compare
`CanvasNetSvg`'s `SvgCodec`, which has no interposed subsystem — see _CanvasNetSvg System
Design_, `canvas-net-svg.md`). A `ChartModel` subsystem is nonetheless introduced here, ahead of
`CanvasNetCharts` containing more than one unit, because Phase 2 is already planned to add a
second unit (`ChartRenderer`, under a new `ChartRendering` subsystem) and Phase 3 a third
(an OOXML `chart1.xml` parser, under a new `ChartInterchange` subsystem); modeling `ChartModel`
as a subsystem from Phase 1 onward avoids a disruptive re-model of the system's tier structure
partway through its incremental delivery.

Not yet implemented, landing in later phases:

- **ChartRenderer** (Phase 2) — paints a `Chart` onto a core `CanvasNet.Canvas.Surface`
- **OOXML chart1.xml parser** (Phase 3) — produces a `Chart` from an embedded PowerPoint chart
  part
- `CanvasNetPptx` integration (Phase 4) — wires chart rendering into `CanvasNetPptx`'s own slide
  rendering, so a `<p:graphicFrame>` containing a chart reference renders its chart content

## External Interfaces

The system exposes the following public API, all in the `DemaConsulting.CanvasNet.Charts`
namespace:

- **`Chart`**: An immutable, validated description of a chart — its `ChartType`, its `Series`,
  and optional `CategoryAxis`/`ValueAxis`/`Legend`/`Title`/`ColorPalette`. Constructed directly,
  or via `ChartBuilder`.
- **`ChartSeries`**: An immutable, validated named series of numeric values, with optional
  per-point colors, per-point labels, and a series-level color.
- **`ChartAxis`**: An immutable, validated axis description — an optional ordered category label
  collection, an optional value range (minimum/maximum/tick interval), and an optional title.
  Used for both a chart's category axis and its value axis.
- **`ChartLegend`**: An immutable, validated legend description — a placement position
  (`ChartLegendPosition`) and a visibility flag.
- **`ChartTitle`**: An immutable, validated title description — text and an optional font-size
  hint.
- **`ChartType`**: An enumeration of the chart kinds Phase 1 models: `Bar`, `Column`, `Line`,
  `Pie`, `Doughnut`, `Area`.
- **`ChartLegendPosition`**: An enumeration of legend placements: `Top`, `Bottom`, `Left`,
  `Right`, `None`.
- **`ChartBuilder`**: A fluent, mutable-until-`Build` API for constructing a `Chart` ergonomically,
  delegating all data-shape validation to the model types' own constructors.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| ------------------------ | ---------------- | -------------------------- | --------------------------------------------------------------- |
| `Chart` constructor | Inbound | Method call / `Chart` instance | At least one non-null series; see _ChartModel Subsystem Design_ |
| `ChartBuilder.Build()` | Inbound/Outbound | Method call / `Chart` return | A chart type and at least one series must be configured first |
<!-- markdownlint-enable MD013 -->

See _ChartModel Subsystem Design_ (`canvas-net-charts/chart-model.md`) and
_ChartDocument Unit Design_ (`canvas-net-charts/chart-model/chart-document.md`) for every type's
complete constructor parameter and exception detail.

## Dependencies

`CanvasNetCharts` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Charts/DemaConsulting.CanvasNet.Charts.csproj`), specifically:

- The `Canvas` subsystem's `Rgba32` unit — representing a series/point/palette color

As of this release (Phase 1), `CanvasNetCharts` depends on no other subsystem of `CanvasNet`: it
has no pixel buffer to construct or paint onto yet (no `Surface` dependency), no path geometry to
build (no `Geometry` dependency), no rasterization to perform (no `Drawing` dependency), and no
text to lay out (no `Fonts` dependency). Phase 2's `ChartRenderer` is expected to add the
`Canvas.Surface`, `Geometry`, `Drawing`, and `Fonts` subsystem dependencies that an actual
rendering unit requires.

**`CanvasNetCharts` must never reference `DemaConsulting.CanvasNet.Pptx`,
`DemaConsulting.CanvasNet.Pdf`, `DemaConsulting.CanvasNet.Svg`, or a future
`DemaConsulting.CanvasNet.Vsdx`.** This is a hard architectural constraint, not a Phase 1-specific
limitation: `CanvasNetCharts` is designed to be a reusable chart library any consumer of the core
`CanvasNet` system can use directly, independent of any specific document format. `CanvasNetPptx`
is instead expected to depend on `CanvasNetCharts` in Phase 4 (the opposite direction), the same
way it already depends on the core `CanvasNet` system — never the other way around.

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetCharts` are produced by this repository, so it is neither an OTS Software Item (not a
third-party/external-program dependency) nor a Shared Package (that category is scoped to a
package produced by a _different_ repository within the same program). `CanvasNetCharts`
introduces no new OTS Software Item beyond those already used to build and verify the `CanvasNet`
system (BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, VersionMark,
WeasyPrint, xUnit) — see _OTS Integration Design_ (`docs/design/ots.md`).

## Risk Control Measures

Construction-time validation is the sole risk control measure for this system as of this release:
every `ChartModel` subsystem type validates its complete set of constructor arguments eagerly
and fails closed (via `ArgumentException`/`ArgumentNullException`/`ArgumentOutOfRangeException`)
rather than silently coercing, clamping, or accepting invalid data — see
_ChartModel Subsystem Design_ (`canvas-net-charts/chart-model.md`) for the complete set of
validation rules. No untrusted-input parsing exists yet: `CanvasNetCharts` does not yet read any
file format or externally-authored document (that risk surface is introduced by Phase 3's OOXML
`chart1.xml` parser, and its own risk control measures will be documented when that phase lands,
following `CanvasNetSvg`'s own precedent for untrusted-XML parsing — see _CanvasNetSvg System
Design_, `canvas-net-svg.md`, Risk Control Measures).

## Data Flow

**`ChartBuilder` → validated `Chart` construction path (the only data flow Phase 1 implements):**

1. **Input**: A sequence of fluent `ChartBuilder` method calls (`OfType`, `AddSeries`,
   `WithCategoryAxis`, `WithValueAxis`, `WithLegend`, `WithTitle`, `WithColorPalette`), each either
   storing already-valid state or delegating construction of a model type to that type's own
   validating constructor
2. **Validation**: Each `ChartBuilder` method that constructs a model type propagates that type's
   own constructor exceptions unchanged; `Build()` additionally throws
   `InvalidOperationException` when no chart type was set via `OfType`, or when no series was
   added via `AddSeries`
3. **Processing**: `Build()` calls `Chart`'s own constructor with the builder's accumulated state,
   which performs the complete set of cross-member validation (for example, series/category-axis
   length matching for category-based chart types) and defensively copies every collection
4. **Output**: A new, fully validated, immutable `Chart` instance

A caller may equivalently construct a `Chart` (and its constituent `ChartSeries`/`ChartAxis`/
`ChartLegend`/`ChartTitle` instances) directly, without `ChartBuilder`, with identical validation
behavior, since `ChartBuilder` enforces no rule the model constructors do not already enforce
themselves.

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **No rendering yet**: `CanvasNetCharts` provides no pixel output as of this release (see
  Architecture above)
- **Dependency direction**: `CanvasNetCharts` must never reference `CanvasNetPptx`,
  `CanvasNetPdf`, `CanvasNetSvg`, or a future `CanvasNetVsdx` (see Dependencies above)

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs and the `CanvasNet` system's own public API, available across all target frameworks. No
platform-specific native interop, OS-specific APIs, or framework-version-specific features are
used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, referencing the
  `CanvasNet` package as an ordinary NuGet dependency
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
