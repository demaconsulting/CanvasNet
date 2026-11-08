### StatTransforms

![Transforms Structure](VegaLiteTransformsView.svg)

The `StatTransforms` unit is the sixth software unit in the `Transforms` subsystem. It comprises eleven source files in
the `DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, all `internal`:

- `StatTransforms.cs` - `StatTransforms` (sealed class: the dispatcher, the partition of the rows into groups, the
  reading of the options, the output table, and the budgets); the file carries the attribution header comment for the
  Vega operators it re-implements
- `StatTransforms.Regression.cs`, `StatTransforms.Loess.cs`, `StatTransforms.Density.cs`, `StatTransforms.Quantile.cs`,
  `StatTransforms.Extent.cs` - the five transforms as partial members of the class
- `RegressionFit.cs` - `RegressionFit` (static class) and `RegressionModel` (record): the regression models of
  `vega-statistics`
- `CurveSampler.cs` - `CurveSampler` (static class): the adaptive sampling of a curve (`sampleCurve`)
- `LoessFit.cs` - `LoessFit` (static class): the locally weighted regression (`vega-statistics` `loess`)
- `KernelDensity.cs` - `KernelDensity` (sealed class): the Gaussian kernel density, the cumulative distribution, and
  Scott's rule (`vega-statistics` `kde`, `bandwidth`, `normal`)
- `StatisticsPrimitives.cs` - `StatisticsPrimitives` (static class): the number conversion, quantile, deviation,
  extent,
  and range rules of `d3-array` and `vega-util` that the five transforms share

#### Purpose

`StatTransforms` computes the statistical transforms of Vega-Lite, which the Vega dataflow runs as the operators
`regression`, `loess`, `kde`, `quantile`, and `extent`: a trend line through the points of a group (`regression`:
linear, log, exp, pow, quad, or poly), a locally weighted smoothing (`loess`), the density or the cumulative
distribution of a field (`density`), the quantiles of a field (`quantile`), and the minimum and maximum of a field
stored in a parameter (`extent`). Every transform except `extent` **replaces the table** with the rows it generates;
`extent` leaves the table as it is. The unit exists so that a chart can show a statistic of its data without the data
being prepared outside the specification, and so that the numbers are those of the reference: the transforms are ported
literally, including the details of JavaScript (the text keys of the groups, which values are numbers, and the order of
the floating-point operations), because a fit that differs in the last digits is no longer comparable and a fit that
differs in the first digits is a different chart.

The unit's behavior is specified by the `CanvasNetVegaLite-Transforms-StatTransforms-*` requirements in
`docs/reqstream/canvas-net-vega-lite/transforms/stat-transforms.yaml`. The behavior rules are those of the sources
listed in _CanvasNetVegaLite System Design_, Third-Party Provenance and Attribution: `vega-regression`'s `Regression`
and `Loess` transforms; `vega-transforms`' `KDE`, `Quantile`, and `Extent` transforms and `partition`;
`vega-statistics`' `regression/*`, `loess`, `kde`, `bandwidth`, `normal`, `quantiles`, `numbers`, and `sampleCurve`;
`vega-util`'s `extent`; and `d3-array`'s `deviation`, `quantile`, and `range`; plus the way Vega-Lite 6 compiles the
transforms (`regression.ts`, `loess.ts`, `density.ts`, `quantile.ts`, and `extent.ts`), which supplies the defaults.

#### Data Model

An instance serves one call on one thread: it holds the `TransformContext` (the budgets, the token, the warnings, the
parameters) of the call. The input is an immutable `DataTable`; the output is built with `TableLayout` (see
_RowTransforms_: ordinal field order, `MaxFieldCount` and the 2^25 cells checked before allocation), and the field
names
are the literal names of `as` or of the defaults (a name with a dot is one field, not a path).

**The groups** are those of Vega's `partition`. With no `groupby` there is one group (an empty one when the table has
no
rows, because Vega's operators run once per group and the group exists). Otherwise the key of a row is `String(array)`
of the raw values of the group fields: the values are joined with a comma, `null` and a missing field are the empty
text, so the number 1 and the text `"1"` are one group, and so are `null` and a missing field. The groups are in order
of first appearance; the **dimensions** of a group are the values of its first row, and are written before the output
fields of every output row (an output field with the name of a group field wins). `n` is the number of rows of the
group, whether or not they hold valid values.

**Regression** (`RegressionFit`). The pairs whose x and y are not null and convert to finite numbers (the `d3`/`vega`
validity rule of `StatisticsPrimitives.TryNumber`) are the points. The models are the ports of `regression/*`: `linear`
(the closed form by the centered sums), `log`, `exp`, `pow` (the fit of the logarithms of the points that have positive
coordinates), `quad` and `poly` of the order `k` (the least squares of the centered points by Gaussian elimination, the
coefficients moved back to the uncentered x), and the constant for the order 0. `poly` with the order 0, 1, or 2
delegates to the constant, the linear, and the quadratic models, as Vega does (the default order is 3). The result is a
`RegressionModel` with the coefficients, the coefficient of determination (`rSquared`, NaN for no points), and
`Predict`. The degrees of freedom of a method (`order` for `poly`, 2 for `quad`, otherwise 1) decide that a group is
skipped: a group with `n` not more than the degrees of freedom gives no rows and the warning. The order is limited to
`MaxPolynomialOrder` (64) so that the matrix of the fit stays small.

**Curves** (`CurveSampler`). A `linear` model (and the constant) gives the two end points of the extent. Every other
method is sampled adaptively over the extent with `sampleCurve(f, extent, 25, 200)`: a uniform grid of the minimum
number of steps, each interval subdivided while the angle between the segments is more than a threshold, up to the
maximum number of points; a step count that is not an integer follows the JavaScript loop. The **extent** is that of
the
`extent` option or, without it, the minimum and maximum of the valid x of the group; a log model ignores an extent
whose
minimum is not positive (the transform warns). With `params: true` there is no curve: the group gives one row with
`keys` (the group values as an array), `coef`, and `rSquared`.

**Loess** (`LoessFit`). The port of `science.js`'s `loess` as it is in `vega-statistics`: the points sorted by x with
the centered values, a window of `max(2, floor(bandwidth * n))` neighbors moved with the point, the tricube weights,
the
weighted linear fit per point, and two robustness iterations that scale the weights by the residuals (stopping when the
median residual is below 1e-12), with the repeated x merged into the first output point (the later values replace it)
and the NaN that a window reaching past the data reads. The default bandwidth is 0.3 (a bandwidth of 0 is the default
too, as in Vega's `||`). The output is sorted by x.

**Density** (`KernelDensity`). The estimator is built on the **raw values** of the group (every row of the group, a
null
reading as 0, a text that is not a number giving NaN, `n` being the number of rows) with a Gaussian kernel of the
bandwidth. When the bandwidth is 0 or not given, Scott's rule of `vega-statistics` `bandwidth` estimates it: `1.06 *
min(sd, iqr / 1.34) * n^(-1/5)` with the fallbacks of Vega's `||` chain (`min || d || |q0| || 1` where the pieces are
the minimum of the standard deviation and the interquartile range scaled, the deviation, and the absolute first
quartile). `cumulative` gives the cumulative distribution (the approximation of West, 2009, for the normal CDF). The
sample points follow Vega-Lite's default resolution `shared`: the extent is that of the whole column (or of `extent`),
the grid is uniform with `steps` (default `maxsteps`, 200) intervals, hence `steps + 1` points, and every group uses
it;
with `resolve: independent` each group has its own extent and the adaptive sample of `minsteps` (25) to `maxsteps`
(200), `steps` overriding both (`minsteps` above `maxsteps` raises the maximum). `counts` multiplies the density by
`n`.
The output fields are `value` and `density` by default.

**Quantile.** The values of the group that are numbers (null, the empty text, and NaN are skipped; the rest converted)
are sorted, and every probability `p` gives the R-7 quantile of `d3-array` (`quantileSorted`: linear interpolation at
`(n - 1) p`), null for no numbers. The probabilities are `probs` or `range(step / 2, 1 - 1e-14, step)` (the default
step
0.01 gives the 100 probabilities 0.005, 0.015, ..., 0.995, counted by `StatisticsPrimitives.RangeCount`, which saturates
at `long.MaxValue` for a quotient that no integer holds, so the count is checked against the row budget and then
against `TableLayout.MaxCells` before the array is allocated). The output
fields are `prob` and `value`.

**Extent.** `StatisticsPrimitives.Extent` is the minimum and maximum of the field by `vega-util`'s `extent` (null and
the empty text skipped, a NaN ignored); the result is stored with `TransformContext.SetParameter` in the parameter
`param` as the array `[min, max]`. A field without a finite value gives `[null, null]` and the warning. The table is
returned unchanged, and the input rows are not released.

**Budgets.** Every output row is reserved in the row budget of the loader before it is written (`ReserveDerivedRows`);
the points of a curve are reserved as they are found (a callback of the sampler), and the grid of a density and the
probabilities of a quantile are reserved before they are computed. The rows of the input are released (`ReleaseRows`)
when the output is complete. The work is charged to the step budget through the `WorkMeter` (one step per 64 elements,
an element weighted to about three nanoseconds, so that the default budget stops each transform in about ten seconds,
phase 14): `n` times (the coefficients squared plus 8) times 16 for a fit, `LoessFit.Cost` (points times window times
iterations times 3, plus 16 a point for the sort) up front for a loess, the
rows times 4 for every density sample point, and the sort for a quantile. The token is polled through
`TransformContext.Tick`
every 1,024 rows and in the long loops.

#### Key Methods

- **`StatTransforms.Apply(input, entry, operation, context)`** - runs the transform that `operation` names (the member
  of the transform entry: `regression`, `loess`, `density`, `quantile`, or `extent`) and returns the new table; any
  other member returns `input`. It is called by `RowTransforms.Run` for the five names.
- **`StatTransforms.Partition`** - the groups of Vega's `partition` (key by the JavaScript text, first-seen order,
  dimensions of the first row); used by the four transforms that generate rows.
- **`RegressionFit.Fit(method, order, x, y)`** - fits a model to the valid pairs and returns a `RegressionModel`.
- **`CurveSampler.Sample(f, minX, maxX, minSteps, maxSteps, progress)`** - the points of a curve over an extent;
  `MaximumPoints` bounds the number before the sampling starts.
- **`LoessFit.Fit(x, y, bandwidth, progress)`** and **`LoessFit.Cost(n, bandwidth)`** - the smoothed points of a group
  and the work that they need.
- **`KernelDensity.Pdf`, `Cdf`, `EstimateBandwidth`** - the density, the cumulative distribution, and Scott's rule.
- **`StatisticsPrimitives`** - `TryNumber`, `Numbers`, `Quantiles`, `Deviation`, `Extent`, and `RangeCount`.

#### Error Handling

A failure of a limit (a curve or a grid that would exceed the row budget (`MaxRows`), an output with more fields than
`MaxFieldCount` or more cells than the layout allows, work beyond `MaxEvaluationSteps`, or a text beyond
`MaxStringLength`) is a `VegaLiteRenderException` located at the transform (and the budgets that were reserved are
returned). A canceled
token throws `OperationCanceledException`. Malformed options (a wrong type, a method that does not exist, an order that
is not an integer from 0 to 64, a bandwidth out of range) never reach the unit: `SpecParser` rejects them when the
document is opened. The recoverable findings are the warning `vl-transform-runtime`, one per transform with the number
of groups concerned: a regression skipped because the group has no more rows than the model has parameters, a log
regression whose extent starts at or below zero (the extent is ignored), and an extent without a finite value.

**Deviations from the reference**, all documented in the user guide: Vega throws when a regression group has no valid x
(a `null` extent of an empty set); the unit generates two points with a null x and a NaN y instead; a date field is
read
as a number of milliseconds, as Vega does, and the curve is sampled in milliseconds; the order of a polynomial is
limited to 64 and the options that Vega would turn into NaN or an infinite loop (a negative bandwidth, `steps`, or
`step`) are rejected by the parser.

#### Dependencies

- `Data` subsystem (`DataTable`, `DataValue`, `DataColumn`) - the tables and the values
- `Expressions` subsystem - `ExpressionEvaluator.NumbersEqual`, the text of a value as JavaScript gives it, and the
  step
  budget
- `RowTransforms` unit - the table layout (`TableLayout`), the field readers, and the `TransformContext` (`Tick`,
  `CheckText`, `SetParameter`, `Locate`, the loader's `ReserveDerivedRows` and `ReleaseRows`, the evaluator whose step
  budget the `WorkMeter` charges, and the list of warnings)
- `SpecModel` subsystem - `SpecNode` and the located exceptions

#### Callers

- `RowTransforms.Run` dispatches the transform entries `regression`, `loess`, `density`, `quantile`, and `extent` to
  `StatTransforms.Apply`.
- `AggregateTransforms` does not call the unit; the `bin` transform and `scale.domain` read the extent parameter
  through
  the parameter store that `TransformContext.SetParameter` writes.
