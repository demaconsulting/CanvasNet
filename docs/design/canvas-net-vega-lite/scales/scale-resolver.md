### ScaleResolver

![Scales Structure](VegaLiteScalesView.svg)

The `ScaleResolver` unit is the second software unit in the `Scales` subsystem. It is one source file,
`ScaleResolver.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines four `internal` types:
`ScaleResolver` (static class), `ResolvedScales` (sealed class), `ResolvedScale` (sealed class), and `RangeEnd`
(readonly record struct). The file carries the provenance header comment for the Vega-Lite and Vega rules and the d3-scale
behavior it ports.

#### Purpose

`ScaleResolver` infers, for each encoding channel of a single view, the scale that Vega-Lite would build: its kind, its
domain, its range, its padding, and whether it is reversed, rounded, clamped, or extended by nice. It exists so the rules
of Vega-Lite's scale inference (type by field type, channel, and mark; the zero, nice, and padding rules; default ranges;
step sizing of discrete views) live in one place, separate from the pixel arithmetic of `ScaleModel` and from the
guides and marks that consume the result.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ScaleResolver-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/scale-resolver.yaml`.

#### Data Model

**`ResolvedScales`** holds the `MarkType`, the inferred `Orient` (horizontal, vertical, or null), the scales by channel
(an indexer by channel name and an enumeration in the fixed channel order x, y, color, fill, stroke, size, opacity), and
the `DiscreteWidth` and `DiscreteHeight` that a step-driven discrete position scale needs.

**`ResolvedScale`** (immutable) describes one channel before the size of the data rectangle is known: `Channel`, `Kind`,
`FieldPath` (null for a `datum`), `DataType`, `Location` (the channel definition), the linear `DomainStart` and
`DomainEnd` (after the zero rule, before pixel padding and nice), `NiceCount`, `PaddingPixels`, `Clamp`, `Round`,
`Reverse`, the `DiscreteDomain`, `PaddingInner`, `PaddingOuter`, `Align`, `Step`, the `ExplicitRange` (a list of
`RangeEnd` values, each a number or the keyword `width` or `height`), the `ValueRange` of a size or opacity scale, the
`ColorRange` and `ColorFallback`, `IsPosition`, and `NaturalExtent` (the step times the band space of a step-driven
discrete scale). `CreateModel(extent)` builds the `ScaleModel` for a data-rectangle width (x) or height (y); the extent
is ignored for color, size, and opacity.

**Inference rules** (Vega-Lite 6 behavior, ported with attribution):

- **Type.** Quantitative position channels are linear; discrete position channels are band scales for `rect`, `bar`,
  `image`, `rule`, and `tick` marks and point scales for the others; `color`, `fill`, and `stroke` fields are ordinal;
  `size` and `opacity` are point-style value scales. A quantitative color has no scale, and a temporal field has none. An
  explicit `scale.type` of linear, band, or point is used when the channel and field type accept it.
- **Domain.** A linear domain is the extent of the valid values (numbers, booleans as 1 and 0, numeric strings; null and
  NaN are ignored; an empty extent is 0 to 1), then the `zero` rule pulls an end to zero (true for the measure axis of a
  bar and for quantitative sizes, the configured default otherwise, and only when a custom domain already spans zero). A
  discrete domain is the distinct values in order of first appearance, sorted ascending by default (`sort` may be
  ascending, descending, null for data order, or an array); an explicit `scale.domain` is used as given. An explicit
  quantitative domain keeps **all** its numbers (`ResolvedScale.Domain` when there are more than two): with an explicit
  range of more than two entries the scale is piecewise as in d3 (see _ScaleModel_), `zero`, pixel padding and `nice`
  change only the first and the last entry as Vega's `configureDomain` does (so `nice` changes the last entry of the
  complete domain, as d3's `nice` does), and a domain of three entries over the default two-entry range uses its first
  two entries only.
- **Padding and nice.** A band scale has inner padding by mark from the configuration (bar 0.1, tick 0.25, others 0) and
  outer padding half of it; a point scale 0.5; `scale.padding` sets both. A continuous position scale gets nice with a
  count of 10 unless it has a custom domain; a bar on its continuous axis gets 5 pixels of padding.
- **Range.** x is 0 to the width and y is the height to 0 (continuous) or 0 to the height (discrete). Size ranges follow
  the mark (point-like marks 4 to the square of 0.95 steps, bar and tick 2 to step minus 1, rule 1 to 4); opacity is 0.3
  to 0.8; colors are the configured category scheme (`tableau10`). A discrete position scale without an explicit range
  or a fixed size takes its size from `step` (20 pixels by default), which is what `DiscreteWidth` and `DiscreteHeight`
  report so that the layout can size the view. An explicit numeric range keeps all its entries for a linear scale (`reverse`
  flips the whole array, as Vega does); a band or point scale uses the first two entries, as d3's `scaleBand` does.
- **Sort by the aggregate of a field.** A `sort` object with an `op` of `count`, `sum`, `mean`, `average`, `median`, `min`,
  or `max`, an optional `field` (a valid field path; the field of the channel when absent), and an optional `order`
  (`ascending` or `descending`) orders a discrete domain by that aggregate over the rows of each category in the
  **transformed** table (so a calculated, flattened, or looked-up field can be the sort field). `count` counts the rows,
  the other operations read the valid numbers of the sort field; a category without a valid number has no value and sorts
  first (except `sum`, which is 0, and `count`); categories with the same value keep the order of first appearance, so the
  sort is stable and independent of the culture. `IsSupportedFieldSort` is shared with the `FeatureCatalog`, so
  the report and the sort agree.
- **Orientation.** `InferOrient` follows Vega-Lite: no orientation for point, circle, square, and rect marks; a bar is
  horizontal when only x is a measure and vertical when only y is, a tick the other way round, and a rule is vertical
  with only x and horizontal with only y; an explicit `orient` of the mark wins where Vega-Lite lets it.

**Deviations** (also listed in _CanvasNetVegaLite System Design_): values of different kinds in one discrete domain
are ordered by kind; a scheme other than `tableau10`, and the ordinal range of an ordinal color field, fall back to
`tableau10` with `ColorFallback` set; `scale.domain` forms other than an array are ignored; a sort by another encoding
channel, by an operation other than the
seven below, or as a shorthand is ignored (the catalog reports it); a `sort` array puts values missing from it first. A
`sort` array is read once into a
dictionary from each listed value to its first position (a value listed twice counts at its first position), so
ordering a domain costs one lookup per value rather than a search of the list per value.

#### Key Methods

- `Resolve(view, table, config, sourceName, limits, token)` - requires non-null view, table, and configuration; reads the
  mark and its orient, returns an empty result for a view without a mark or an encoding, builds the shared context (data,
  configuration, mark, orient, encoding, limits, token, and the width and height steps), and resolves each present
  channel in the fixed order. The data is scanned with the token polled every 4,096 rows.
- `InferOrient(markType, specified, encoding)` - the orientation rule above; a measure is an unbinned quantitative field.
- `ResolvedScale.CreateModel(extent)` - applies what depends on the extent: the range of a position scale (including an
  explicit range with `width` and `height` keywords, all of whose entries are kept for a linear scale), the pixel
  padding of a continuous scale (the domain is widened so that the extreme values keep that distance from the edge),
  then `nice` (after the padding, as Vega does), and builds a linear, band, point, or ordinal `ScaleModel`; a non-finite
  domain after nice keeps the padded domain.
- `ResolvedScale.NaturalExtent` - `step * BandSpace(count, inner, outer)`, the size a step-driven discrete scale needs.
- `IsSupportedFieldSort(sort)` (static) - whether a `sort` object is a sort by the aggregate of a field that the resolver
  can compute (see _Inference rules_); `SortByField` groups the rows of the table by category in one pass (polling the token
  every 1,024 rows), computes the aggregate of each group (`count`, `sum`, `mean`, `average`, `median`, `min`, `max`), and
  orders the categories by it with a stable sort.

**Preconditions and postconditions**: the view was validated by `SpecParser`; on return every present, supported channel
has a resolved scale, and the result is immutable.

#### Error Handling

A `scale.domain` or `scale.range` of the wrong shape (a range that is not an array of numbers or the keywords, with fewer
than two values; a quantitative domain with fewer than two numbers or with a non-number) is a `VegaLiteSpecException`
located at the offending value. A discrete domain with more than `MaxScaleDomainSize` values is a
`VegaLiteRenderException` located at the channel. A canceled token is an `OperationCanceledException`. A null argument is
`ArgumentNullException`, which is not converted. Constructs outside the supported subset are never errors here: they are
reported by the `FeatureCatalog`, and the resolver falls back or ignores them as listed above.

#### Dependencies

- **`ScaleModel`**, **`Ticks`**, and **`ColorSchemes`** - the models, the tick-based `nice` rule, and the category palette
- **`DataTable`** and **`DataValue`** - the data and values of the `Data` subsystem
- **`SpecNode`** and **`ResolvedConfig`** - the tree and the configuration of the `SpecModel` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and **`VegaLiteRenderException`** - the public
  types of the `VegaLiteDocument` unit
- **.NET base class library** - `System`, `System.Collections.Generic`, `System.Globalization`

#### Callers

`VegaLiteDocument` calls `Resolve` once per render, over the table that the row transforms produced. `FeatureCatalog` calls
`IsSupportedFieldSort`. `AxisBuilder` and `MarkBuilder` read the `ResolvedScale` (field path,
kind, domain, padding) and the models built from it. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleResolverTests.cs` call the resolver directly through
`InternalsVisibleTo`.
