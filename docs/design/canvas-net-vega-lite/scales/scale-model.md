### ScaleModel

![Scales Structure](VegaLiteScalesView.svg)

The `ScaleModel` unit is the first software unit in the `Scales` subsystem. It is one source file, `ScaleModel.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines five `internal` types: `ScaleKind` (enumeration),
`ScaleTransformKind` (enumeration), `ScaleTransform` (sealed class), `ScaleRange` (readonly struct), and `ScaleModel`
(sealed class). The file carries the provenance header comment for the
d3-scale arithmetic and the Vega scale it ports.

#### Purpose

`ScaleModel` maps data values to positions, and positions back to values, for the scale kinds of the first delivery:
linear (also with a log, pow, sqrt, or symlog transform), band, point, and ordinal; it also maps numbers to colors
(sequential and diverging) and classifies them
(quantize, quantile, threshold, and bin-ordinal). It exists so that the arithmetic of scales (normalizing,
interpolating, stepping, padding, rounding, reversing, classifying) is defined in one pure, immutable, easily tested
place with no knowledge of the
specification; the `ScaleResolver` unit decides which scale to build, and the axes and marks only ask for a position.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ScaleModel-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/scale-model.yaml`.

#### Data Model

**`ScaleKind`** is `Linear`, `Band`, `Point`, `Ordinal`, `Sequential`, `Diverging`, `Quantize`, `Quantile`, `Threshold`,
or `BinOrdinal`. A log, pow, sqrt, or symlog scale is not a kind of its own: it is a `Linear` (or, for colors,
`Sequential` or `Diverging`) model that carries a transform, so that everything that works on a continuous scale (stacks,
bin ends, ranges, ticks, gradient legends) keeps working.

**`ScaleTransform`** (sealed class, immutable) is the transform of such a scale: `Kind` (`ScaleTransformKind`: `Linear`,
`Log`, `Pow`, or `Symlog`), `Parameter` (the base, the exponent, or the constant), `IsLinear` (the identity, also the pow
transform with the exponent 1), the factories `Log(base)`, `Pow(exponent)` (a sqrt scale is `Pow(0.5)`), and
`Symlog(constant)`, and `Forward(x, reflect)` and `Inverse(y, reflect)`, which are the functions of d3's `log.js`,
`pow.js`, and `symlog.js`: the natural logarithm (the base only matters for ticks and `nice`; a log scale whose domain
starts below zero is reflected, `-ln(-x)` and `-exp(-y)`), the sign-preserving power (`Math.Sqrt` and its square for
0.5), and `sign(x) * log1p(|x| / c)` with the inverse `sign(y) * expm1(|y|) * c`; `log1p` and `expm1` use Kahan's
correction so that a tiny number keeps its digits (a naive `ln(1 + x)` would lose them).

**`ScaleRange`** (readonly struct) is the output of an ordinal or discretizing scale: texts (colors, shape names),
numbers (sizes, opacities, angles), or only a count (the caller maps the index itself, as for dash patterns). It copies
its values on creation and offers `Count`, `Texts`, `Numbers`, `TextAt(index)`, and `NumberAt(index)`.

**`ScaleModel`** (sealed class, immutable) exposes its `Kind` and the parameters that define it: `DomainStart` and
`DomainEnd` (the first and last entry of the complete domain of a numeric scale) and `DomainMid` (diverging),
`RangeStart` and `RangeEnd` (the first and last
entry of the complete range for a linear scale), `Clamp`, `Round`, `PaddingInner`, `PaddingOuter`, `Align`, `Step` and
`Bandwidth` (band and point), `Domain` (the categories of a discrete scale as a read-only `DataValue` list), `OrdinalRange`
(the texts of an ordinal range), `Range` (its `ScaleRange`), `Interpolator` (the color function of a sequential or
diverging scale), and
`Thresholds` (the interval boundaries of a quantize, quantile, or threshold scale, or the bin edges of a bin-ordinal
scale), and `Transform` (the `ScaleTransform` of a continuous scale). The factories are `CreateLinear(domainStart,
domainEnd, rangeStart, rangeEnd, clamp, round, transform)`, `CreateLinear(double[] domain, double[] range, clamp, round,
transform)` (the piecewise form),
`CreateBand(domain, rangeStart, rangeEnd, paddingInner, paddingOuter, align, round)`, `CreatePoint(domain, rangeStart,
rangeEnd, padding, align, round)`, `CreateOrdinal(domain, range)` (texts or a `ScaleRange`),
`CreateSequential(domain, interpolator, clamp, transform)`
(two entries sequential, three diverging), `CreateQuantize(start, end, range)`, `CreateQuantile(sortedValues, range)`,
`CreateThreshold(thresholds, range)`, and `CreateBinOrdinal(edges, range)`. The static `BandSpace(count, paddingInner,
paddingOuter)` is Vega's `bandspace`: the number of steps a discrete scale needs.

**Invariants**: a model never changes after it is built; a discrete model's domain is deduplicated, keeps its order, and
has an index per value; a linear model with a domain of zero width maps every value to the middle of its range, and NaN
maps to NaN; the thresholds of a discretizing scale are fixed at creation.

#### Key Methods

- `Map(double)` - the linear mapping: the value is clamped when `Clamp` is set (to the first and the last used domain
  entry), normalized against the segment that holds it, interpolated into the range, and rounded when `Round` is set.
  The mapping is d3-scale's `continuous.js` piecewise map: `min(domain length, range length)` entries are used; two
  entries are one segment (`bimap`: a descending pair is swapped, entries beyond the first two are ignored); more are
  one segment between each pair of neighbors (`polymap`) chosen by `bisectRight(domain, x, 1, n - 1)`, so a value
  outside the domain continues the first or last segment, and a descending domain reverses both complete arrays before
  the first `n` entries are used (d3's own behavior when the lengths differ is kept); a segment of zero width maps to the
  middle of its output segment. `Invert` is the same map from range to domain, clamped to the domain when `Clamp` is set.
  With a transform (d3's `transformer`) the value is clamped in the domain, then transformed, and the piecewise map runs
  over the transformed domain; `Invert` undoes the transform of the result. A number the transform cannot take (the
  logarithm of a number that is not above zero) maps to NaN as a number and to null as a data value, so that a mark
  that would sit at a position that is not a number is not drawn.
- `Fraction(double)` - the position of a value between the ends of the domain along the transformed domain, 0.5 for a
  domain without span (Vega's `scaleFraction`: for a log scale `ln(v / start) / ln(end / start)`); gradient legends place
  their labels and stops with it.
- `Map(DataValue)` - for a linear scale the quantitative reading of the value (null when it has none); for a band or
  point scale the **start** of the value's band (or the point), null for a value outside the domain.
- `MapCenter(DataValue)` - the center of a band (start plus half the bandwidth); for a point or linear scale the same
  as `Map`.
- `MapOrdinal(DataValue)` - the range entry of the value's domain index, cycling through the range; null for an unknown
  value or an empty range.
- `MapText(DataValue)`, `MapColor(double)`, and `MapIndex(DataValue)` - the text output of any ordinal, discretizing,
  sequential, or
  diverging scale; the color of a number through a sequential or diverging scale; and the range position of a value
  (the index for
  outputs that are neither text nor number, such as dash patterns). `Map(DataValue)` gives the number output of an
  ordinal or discretizing scale.
- Sequential and diverging arithmetic (d3's `sequential.js` and `diverging.js`): the position is `(x - x0) / (x1 - x0)`
  (the middle of the
  interpolator for a domain of zero width), or `0.5 + (x - x1) * k` with the slope of the side of the middle value that
  the number lies
  on (`0.5 / (x1 - x0)` below, `0.5 / (x2 - x1)` above, zero for equal entries, the sides swapped for a descending
  domain), clamped to 0 to 1
  on request, and not otherwise (the interpolator extrapolates); with a transform the numbers are transformed first
  (d3's `sequentialLog` and friends: the domain entries and the number go through the transform, the clamp applies to the
  position), and a position that is not a finite number (a number the transform cannot take) has no color.
- Discretizing arithmetic: quantize thresholds are `((i + 1) * x1 - (i - n) * x0) / (n + 1)` for `n = range.Count - 1`
  (d3's
  `quantize.js`), quantile thresholds the R-7 quantiles `i / n` of the sorted values (`AggregateMeasures.Quantile`,
  d3's `quantileSorted`), a threshold
  scale uses the first `min(thresholds, range.Count - 1)` explicit thresholds, and all three find the interval with
  `bisectRight`
  (a value on a threshold goes up); a bin-ordinal scale (Vega's `scaleBinOrdinal`) gives
  `range[(bisectRight(edges, x) - 1) % range.Count]`,
  nothing below the first edge.
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
- **`RgbColor`** (the `ColorInterpolator` unit) - the color an interpolator returns
- **`AggregateMeasures`** (the `Transforms` subsystem) - the R-7 `Quantile` that the quantile scale shares
  with the aggregate transform
- **.NET base class library** - `System` (`Math`), `System.Collections.Generic`

#### Callers

`ResolvedScale.CreateModel` (in the `ScaleResolver` unit) builds the models. `AxisBuilder` reads the domain, ticks, and
positions; `MarkBuilder` maps rows to positions, sizes, opacities, and colors. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ScaleModelTests.cs` call the factories and mappings directly through
`InternalsVisibleTo`.
