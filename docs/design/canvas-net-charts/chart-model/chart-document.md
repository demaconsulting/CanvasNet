### ChartDocument

The `ChartDocument` unit is the first software unit in the `ChartModel` subsystem. It comprises
six public types, all in the `DemaConsulting.CanvasNet.Charts` namespace, each a `sealed class`
with a single validating constructor and read-only auto-properties — not a C# `record` — plus one
public enum and one fluent builder:

- `ChartType` (enum): `Bar`, `Column`, `Line`, `Pie`, `Doughnut`, `Area`
- `ChartAxis` (sealed class): an optional ordered category label collection, an optional value
  range, and an optional title
- `ChartLegendPosition` (enum): `Top`, `Bottom`, `Left`, `Right`, `None`
- `ChartLegend` (sealed class): a placement position and a visibility flag
- `ChartTitle` (sealed class): title text and an optional font-size hint
- `ChartSeries` (sealed class): a name, at least one finite value, and optional per-point colors/
  labels and a series-level color
- `Chart` (sealed class): the root aggregate — a `ChartType`, a non-empty series collection, and
  optional category axis, value axis, legend, title, and color palette
- `ChartBuilder` (sealed class): a fluent, mutable-until-`Build` API for constructing a `Chart`

#### Purpose

`ChartDocument` lets callers construct a fully validated, immutable description of a chart,
either directly via each type's own constructor or ergonomically via `ChartBuilder`'s fluent
API, with identical validation behavior either way. It is implemented entirely against the .NET
base class library (`System.Collections.ObjectModel.ReadOnlyCollection<T>` for defensive copies)
and the core `CanvasNet` system's `Rgba32` color type, with no third-party dependency.

Every type's constructor validates its complete argument set eagerly, rejecting invalid data with
a specific, documented exception rather than silently coercing or clamping it:

- A `null` required reference-type argument → `ArgumentNullException`
- An empty or whitespace-only name/title → `ArgumentException`
- A non-finite (`NaN`/`Infinity`) numeric value, where a finite value is required →
  `ArgumentOutOfRangeException` (for a single scalar argument, e.g. `ChartAxis.Minimum`) or
  `ArgumentException` (for an entry within a collection argument, e.g. a non-finite
  `ChartSeries.Values` entry)
- A collection argument that is empty where at least one entry is required (`ChartSeries.Values`,
  `Chart.Series`), or that is empty where supplied-but-empty is itself invalid
  (`Chart.ColorPalette`) → `ArgumentException`
- A collection argument containing a `null` entry where none is permitted (`Chart.Series`,
  `ChartAxis.Labels`, `ChartSeries.PointLabels`) → `ArgumentException`
- A collection argument whose count does not exactly match a related collection's count
  (`ChartSeries.PointColors`/`PointLabels` vs. `Values`; a category-based chart type's series
  `Values` vs. `ChartAxis.Labels`) → `ArgumentException`
- An enum argument that is not a defined member of its enumeration (`ChartType`,
  `ChartLegendPosition`) → `ArgumentOutOfRangeException`
- A value range where the minimum is not strictly less than the maximum, when both are supplied
  (`ChartAxis.Minimum`/`Maximum`) → `ArgumentOutOfRangeException`

Every caller-supplied collection argument (`Chart.Series`, `Chart.ColorPalette`,
`ChartSeries.Values`/`PointColors`/`PointLabels`, `ChartAxis.Labels`) is copied into an immutable
`ReadOnlyCollection<T>` snapshot during construction, so mutating the caller's original
collection after construction has no effect on the constructed instance's exposed property —
see `Constructor_MutatingOriginal*List_DoesNotAffectStoredSnapshot` in
`test/DemaConsulting.CanvasNet.Charts.Tests/` for the test coverage proving this for every such
argument.

`Chart`'s constructor additionally validates one cross-member invariant: for a category-based
`ChartType` (`Bar`, `Column`, `Line`, `Area`) whose supplied `CategoryAxis` has non-null `Labels`,
every series' `Values.Count` must exactly equal that label count. This check is skipped for a
proportional-share `ChartType` (`Pie`, `Doughnut`, whose single series' values represent wedge
shares rather than per-category values), and skipped when no category axis — or a category axis
with null `Labels` (a pure value axis) — is supplied.

`ChartBuilder` performs no independent data-shape validation of its own: every chainable method
either stores already-valid caller state (`OfType`, `AddSeries(ChartSeries)`,
`WithCategoryAxis(ChartAxis)`, `WithValueAxis(ChartAxis)`, `WithLegend(ChartLegend)`,
`WithColorPalette`) or constructs a model type and lets that type's own constructor validate the
arguments (the convenience overloads of `AddSeries`, `WithCategoryAxis`, `WithValueAxis`,
`WithLegend`, and `WithTitle`). Its only independent behavior is `Build()` throwing
`InvalidOperationException` when `OfType` was never called, or when no series was added via
`AddSeries`, before delegating to `Chart`'s own constructor for every other invariant.

#### Design

- `ChartType.cs` — the `ChartType` enum
- `ChartAxis.cs` — the `ChartAxis` sealed class
- `ChartLegend.cs` — the `ChartLegendPosition` enum and the `ChartLegend` sealed class
- `ChartTitle.cs` — the `ChartTitle` sealed class
- `ChartSeries.cs` — the `ChartSeries` sealed class
- `Chart.cs` — the `Chart` sealed class (the root aggregate)
- `ChartBuilder.cs` — the `ChartBuilder` sealed class

Each model type's constructor performs its validation inline (or via a small private static
helper, for a collection argument needing both validation and a defensive copy in the same pass,
e.g. `ChartAxis.CopyLabels`/`ChartSeries.CopyValues`/`ChartSeries.CopyPointLabels`/
`Chart.CopySeries`), then assigns every property from already-validated local state. No type in
this unit exposes a public mutator; every property is a read-only auto-property (`{ get; }`) set
only from the constructor.

The `ChartModel` subsystem depends on the core `CanvasNet` system's `Canvas` subsystem's `Rgba32`
unit — see _ChartModel Subsystem Design_ (`../chart-model.md`)'s Dependencies section.
