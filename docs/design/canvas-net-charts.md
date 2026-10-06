# System Design

This document provides the system-level design for CanvasNetCharts.

![CanvasNetCharts Structure](CanvasNetChartsView.svg)

## Architecture

CanvasNetCharts is a .NET library providing chart support, distributed as its own NuGet package
(`DemaConsulting.CanvasNet.Charts`, namespace `DemaConsulting.CanvasNet.Charts`), independent of,
but depending on, the core `CanvasNet` system (its own separate package,
`DemaConsulting.CanvasNet`) — see the Dependencies section below.

`CanvasNetCharts` is being delivered incrementally. As of this release (Phase 2), the system
consists of a single subsystem:

- **ChartModel** (folder `src/DemaConsulting.CanvasNet.Charts/`, flat — no further nesting): a
  public, immutable, validating chart data model (`Chart`/`ChartSeries`/`ChartAxis`/
  `ChartLegend`/`ChartTitle`/`ChartType`), the `ChartBuilder` fluent construction API, and a
  pixel-rendering engine (`ChartRenderer`/`ChartRenderOptions`/`ChartColorPalette`) that paints a
  `Chart` onto a core `CanvasNet.Canvas.Surface`, contained in two implemented units,
  `ChartDocument` and `ChartRenderer`. See _ChartModel Subsystem Design_
  (`canvas-net-charts/chart-model.md`).

No Subsystem tier would normally be needed for a system containing only one or two units
(compare `CanvasNetSvg`'s `SvgCodec`, which has no interposed subsystem — see _CanvasNetSvg
System Design_, `canvas-net-svg.md`). A `ChartModel` subsystem was nonetheless introduced from
Phase 1 onward, ahead of `CanvasNetCharts` containing more than one unit, to avoid a disruptive
re-model of the system's tier structure partway through its incremental delivery. Phase 2's
`ChartRenderer` was added as a second unit within the existing `ChartModel` subsystem — rather
than under a new subsystem, as originally anticipated in Phase 1 — because it shares the same
single flat source folder and the same `ChartModel` Purpose statement ("what chart to draw" and
"how to draw it" are two facets of the same data-model-plus-rendering whole); Phase 3's OOXML
`chart1.xml` parser remains expected to land under a new subsystem, since it introduces a
genuinely distinct concern (untrusted-input parsing).

Not yet implemented, landing in later phases:

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
- **`ChartRenderer`**: A public static entry point that paints a `Chart` onto a new
  `CanvasNet.Canvas.Surface`, via `Render(Chart, int, int, ChartRenderOptions?)` (direct pixel
  dimensions) or `Render(Chart, float, float, float, ChartRenderOptions?)` (physical
  width/height/DPI).
- **`ChartRenderOptions`**: An immutable options bag for `ChartRenderer.Render` — background
  color, an optional font override, per-element default font sizes, and an optional color
  palette override — with a `Default` singleton, mirroring `PptxRenderOptions`'s own shape.
- **`ChartColorPalette`**: A fixed, documented 10-entry categorical color palette, used by
  `ChartRenderer` when neither `Chart.ColorPalette` nor `ChartRenderOptions.ColorPalette` supplies
  one, resolved by index with wraparound.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| ------------------------ | ---------------- | -------------------------- | --------------------------------------------------------------- |
| `Chart` constructor | Inbound | Method call / `Chart` instance | At least one non-null series; see _ChartModel Subsystem Design_ |
| `ChartBuilder.Build()` | Inbound/Outbound | Method call / `Chart` return | A chart type and at least one series must be configured first |
| `ChartRenderer.Render(...)` | Inbound/Outbound | Method call / `Surface` return | `chart` must not be null; pixel/physical dimensions must be positive and within `Surface`'s supported range |
<!-- markdownlint-enable MD013 -->

See _ChartModel Subsystem Design_ (`canvas-net-charts/chart-model.md`), _ChartDocument Unit
Design_ (`canvas-net-charts/chart-model/chart-document.md`), and _ChartRenderer Unit Design_
(`canvas-net-charts/chart-model/chart-renderer.md`) for every type's complete constructor
parameter and exception detail.

## Dependencies

`CanvasNetCharts` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Charts/DemaConsulting.CanvasNet.Charts.csproj`), specifically:

- The `Canvas` subsystem's `Rgba32` and `Surface` units — representing a series/point/palette
  color and the pixel buffer `ChartRenderer` paints onto and returns
- The `Drawing` subsystem — path filling/stroking and tile-paint primitives `ChartRenderer` uses
  to paint bars/columns/lines/areas/wedges/axes/legend/title
- The `Geometry` subsystem — transforms and rectangles used throughout layout and painting
- The `Fonts` subsystem — TrueType text layout/metrics, used to measure and draw every text label
- The `Rendering` subsystem — the bundled Liberation Sans fallback font used when
  `ChartRenderOptions.Font` is not supplied

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

**`Chart` → rendered `Surface` path (added in Phase 2):**

1. **Input**: A validated `Chart` instance, caller-chosen pixel (or physical width/height/DPI)
   dimensions, and an optional `ChartRenderOptions`
2. **Validation**: `ChartRenderer.Render` throws `ArgumentNullException` when `chart` is null, and
   `ArgumentOutOfRangeException` when the requested (or DPI-computed) pixel dimensions are not
   positive/finite or exceed `Surface.MaxDimension`
3. **Processing**: `ChartRenderer` computes a title/legend/plot-area layout (skipping a band that
   would not fit the render target), resolves every series'/point's color (`Chart.ColorPalette`,
   then `ChartRenderOptions.ColorPalette`, then `ChartColorPalette.Default`, wrapping by index),
   and dispatches per `Chart.Type` to paint bars/columns/lines/areas or pie/doughnut wedges, plus
   axes (for category-based types), a legend, a title, and any opt-in per-point data labels
4. **Output**: A new `Surface` containing the rendered chart, which the caller owns and disposes

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
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
