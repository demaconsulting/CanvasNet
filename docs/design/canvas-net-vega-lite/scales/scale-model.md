### ScaleModel

![Scales Structure](VegaLiteScalesView.svg)

The `ScaleModel` unit is the first software unit in the `Scales` subsystem. It is one source file, `ScaleModel.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines two `internal` types: `ScaleKind` (enumeration) and
`ScaleModel` (sealed class). The file carries the provenance header comment for the d3-scale arithmetic it ports.

#### Purpose

`ScaleModel` maps data values to positions, and positions back to values, for the scale kinds of the first delivery:
linear, band, point, and ordinal. It exists so that the arithmetic of scales (normalizing, interpolating, stepping,
padding, rounding, reversing) is defined in one pure, immutable, easily tested place with no knowledge of the
specification; the `ScaleResolver` unit decides which scale to build, and the axes and marks only ask for a position.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ScaleModel-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/scale-model.yaml`.

#### Data Model

**`ScaleKind`** is `Linear`, `Band`, `Point`, or `Ordinal`.

**`ScaleModel`** (sealed class, immutable) exposes its `Kind` and the parameters that define it: `DomainStart` and
`DomainEnd` (linear: the first and last entry of the complete domain), `RangeStart` and `RangeEnd` (the first and last
entry of the complete range for a linear scale), `Clamp`, `Round`, `PaddingInner`, `PaddingOuter`, `Align`, `Step` and
`Bandwidth` (band and point), `Domain` (the categories of a discrete scale as a read-only `DataValue` list), and
`OrdinalRange` (the colors of an ordinal scale). The factories are `CreateLinear(domainStart, domainEnd, rangeStart,
rangeEnd, clamp, round)`, `CreateLinear(double[] domain, double[] range, clamp, round)` (the piecewise form),
`CreateBand(domain, rangeStart, rangeEnd, paddingInner, paddingOuter, align, round)`, `CreatePoint(domain, rangeStart,
rangeEnd, padding, align, round)`, and `CreateOrdinal(domain, range)`. The static `BandSpace(count, paddingInner,
paddingOuter)` is Vega's `bandspace`: the number of steps a discrete scale needs.

**Invariants**: a model never changes after it is built; a discrete model's domain is deduplicated, keeps its order, and
has an index per value; a linear model with a domain of zero width maps every value to the middle of its range, and NaN
maps to NaN.

#### Key Methods

- `Map(double)` - the linear mapping: the value is clamped when `Clamp` is set (to the first and the last used domain
  entry), normalized against the segment that holds it, interpolated into the range, and rounded when `Round` is set.
  The mapping is d3-scale's `continuous.js` piecewise map: `min(domain length, range length)` entries are used; two
  entries are one segment (`bimap`: a descending pair is swapped, entries beyond the first two are ignored); more are
  one segment between each pair of neighbors (`polymap`) chosen by `bisectRight(domain, x, 1, n - 1)`, so a value
  outside the domain continues the first or last segment, and a descending domain reverses both complete arrays before
  the first `n` entries are used (d3's own behavior when the lengths differ is kept); a segment of zero width maps to the
  middle of its output segment. `Invert` is the same map from range to domain, clamped to the domain when `Clamp` is set.
- `Map(DataValue)` - for a linear scale the quantitative reading of the value (null when it has none); for a band or
  point scale the **start** of the value's band (or the point), null for a value outside the domain.
- `MapCenter(DataValue)` - the center of a band (start plus half the bandwidth); for a point or linear scale the same
  as `Map`.
- `MapOrdinal(DataValue)` - the range entry of the value's domain index, cycling through the range; null for an unknown
  value or an empty range.
- `Invert(double)` and `IndexOf(DataValue)` - the inverse mapping for a linear scale and the domain index of a value.
- Band and point arithmetic (in the factories): the step is the range extent divided by `max(1, n - paddingInner + 2 *
  paddingOuter)` for `n` categories, the bandwidth is the step times one minus the inner padding, the first band starts
  after the alignment share of the space left over, a range that runs backwards lays the bands out backwards, and
  `Round` floors the step and rounds the start and the bandwidth as d3 does.
- `BandSpace` - Vega's `bandspace`: `count - paddingInner + 2 * paddingOuter` steps, or one step when that is not
  positive, and zero for an empty domain.

**Preconditions and postconditions**: a discrete factory requires a non-null domain (and range for an ordinal scale);
a mapping method requires a model of the matching kind; the results are deterministic for the same inputs.

#### Error Handling

Programmer errors fail fast: a null domain or range is `ArgumentNullException`, and calling a mapping that belongs to
another kind of scale (for example `Invert` on a band scale) is an `InvalidOperationException` that names the expected and
actual kinds. Data-dependent conditions are not errors: a value outside the domain maps to null, a domain of zero width
maps to the middle of the range, and NaN maps to NaN. The unit performs no I/O.

#### Dependencies

- **`DataValue`** - the value model of the `DataTable` unit, for discrete domains and mapped values
- **.NET base class library** - `System` (`Math`), `System.Collections.Generic`

#### Callers

`ResolvedScale.CreateModel` (in the `ScaleResolver` unit) builds the models. `AxisBuilder` reads the domain, ticks, and
positions; `MarkBuilder` maps rows to positions, sizes, opacities, and colors. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleModelTests.cs` call the factories and mappings directly through
`InternalsVisibleTo`.
