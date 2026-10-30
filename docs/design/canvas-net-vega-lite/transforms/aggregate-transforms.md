### AggregateTransforms

![Transforms Structure](VegaLiteTransformsView.svg)

The `AggregateTransforms` unit is the second software unit in the `Transforms` subsystem. It comprises thirteen
source files in the `DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, all `internal`:

- `AggregateTransforms.cs` - `AggregateTransforms` (sealed partial class): the `Apply` entry point, the shared
  grouping keys (`GroupKey`), the sort-key reading, the sort helpers, and the located-failure conversion
- `AggregateTransforms.Aggregate.cs` - `aggregate` and `joinaggregate`, and the grouping and accumulation passes
  that the other grouped transforms and the encoding stage share
- `AggregateTransforms.Bin.cs` - the `bin` transform and the names of its two output fields
- `AggregateTransforms.Window.cs` - the `window` transform and the window engine (`WindowRun`, `WindowSpec`)
- `AggregateTransforms.Stack.cs` - the `stack` transform and the stack engine, `StackOffset`, and `SortField`
- `AggregateTransforms.Impute.cs` - the `impute` transform
- `AggregateMeasures.cs` - `AggregateOp` and `WindowOp` (the operation names) and the `AggregateMeasures`
  registry (parsing, canonical names, which operation needs a field or a parameter) and the R-7 `Quantile`
- `AggregateMeasures.Cell.cs` - `MeasureCell` (the accumulator of one operation over a group or a frame),
  `AggregateSpec`, and `MeasureEnvironment`
- `BinAlgorithm.cs` - `BinSettings`, `BinSpec`, and `BinAlgorithm` (the port of the Vega bin algorithm)
- `StackRules.cs` - `StackRules` and `StackDecision` (Vega-Lite's decision that a view stacks, from the
  specification alone)
- `WorkMeter.cs` - `WorkMeter` (charges work proportional to a number of elements to the step budget)
- `StableSorter.cs` - `StableSorter` (a stable, cancelable merge sort of row numbers)
- `TransformContext.cs` - shared with _RowTransforms_; it gained `Locate`, `Tick`, and `CheckText` for these
  transforms

#### Purpose

`AggregateTransforms` computes the six transforms of the specification that group, rank, bucket, stack, or fill
rows: `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute`. It also exposes the **engines** that the
implicit encoding stage reuses (the grouping aggregate, the stack, the bin algorithm, and the accumulators of the
operations), and the **accumulators** that the scale resolver reuses to sort the categories of a discrete scale by
an aggregate. It exists so that the meaning of a group, of a missing value, of a frame, and of a stack, the
accounting of the row budget and of the work, and the determinism of the confidence interval are defined in one
place, whether the aggregation was written as a transform or implied by an encoding.

The unit's behavior is specified by the `CanvasNetVegaLite-Transforms-AggregateTransforms-*` requirements in
`docs/reqstream/canvas-net-vega-lite/transforms/aggregate-transforms.yaml`. The behavior rules are those of the
Vega and Vega-Lite transforms, re-implemented from their public description (see _CanvasNetVegaLite System
Design_, Third-Party Provenance and Attribution).

#### Data Model

An instance serves **one call on one thread**: it holds the `TransformContext`, a `WorkMeter` on the step budget
of the evaluator, and the `MeasureEnvironment` that the accumulators work in. Tables are immutable `DataTable`
values built with `TableLayout` (see _RowTransforms_ for the layout rules: ordinal field order, an added field
replaces a field of the same name and the fields nested below it, `MaxFieldCount` and 2^25 cells checked before
allocation).

**Groups.** A group is the set of rows whose `groupby` values are equal **structurally** (`GroupKey`): numbers by
value (NaN equals NaN, zero equals negative zero), a number and a text with the same spelling are different
groups, and an array or object is compared by its content (an iterative, length-prefixed canonical text with the
members in ordinal order, bounded by `MaxStringLength` and charged to the step budget). Groups are numbered in
**order of first appearance**. No `groupby` is one group of all rows; an input without rows has no group, so
`aggregate` of nothing is no row even without a `groupby`. A group field is written with its field path, so a
nested path stays nested.

**Value classification** (`MeasureCell.Add`). A value is _missing_ when it is null or the empty text, and _valid_
unless it is missing or the number NaN; NaN is neither. The operations see only the valid values, except `count`
(every row), `missing`, `distinct` (the JavaScript text of every value, nulls included), and `values` (every value
in row order). A number is read like JavaScript's unary plus (`ToNumber`, charged by the text length): numeric
text counts, and text that is not a number is NaN and poisons `sum`, `product`, `mean`, and the measures of
spread, while the order statistics (`median`, `q1`, `q3`, `ci0`, `ci1`) skip a NaN.

**Operations** (`AggregateOp`; `average` is another name of `mean`; the default output name is `op_field`, with
the canonical operation name, and `count` for a count of rows):

- `count`, `valid`, `missing`, `distinct`, `values`.
- `sum`, `product`, `mean`, `variance`, `variancep`, `stdev`, `stdevp`, `stderr` over the valid values, with
  Welford's update for the spread; a result that Vega leaves undefined is **null**: no valid value for `sum`,
  `product`, and `mean`, and fewer than two for the spread. Numbers follow IEEE 754 (a sum can overflow to
  infinity).
- `median`, `q1`, `q3` - the d3 R-7 quantile (position `(n - 1) * p`, linear between ranks) of the sorted
  non-NaN numbers.
- `min`, `max` - JavaScript relational comparison, so text and dates work and the original value is returned;
  `argmin`, `argmax` return the **whole first row** that holds the extreme value (ties keep the first) as an
  object.
- `exponential` (`exp * (1 - r) / (1 - r^n)`, the mean at `r = 1`) and `exponentialb` (`exp * (1 - r)`), with
  `exp = exp * r + v` per valid value. The weight `r` is the `param`; **a missing `param` is 0.5** (deviation).
- `ci0`, `ci1` - the 2.5% and 97.5% R-7 quantiles of the means of 1,000 bootstrap resamples (see _Key Methods_).

**Window operations** (`WindowOp`): `row_number`, `rank`, `dense_rank`, `percent_rank`, `cume_dist`, `ntile`,
`lag`, `lead`, `first_value`, `last_value`, `nth_value`, `prev_value`, and `next_value`, plus every aggregate
operation over the frame. `ntile` and `nth_value` need a `param` (an integer of at least 1) and `lag` and `lead`
default it to 1.

**Frames and peers.** A frame is two row offsets from the current row (null is unbounded; **default
`[null, 0]`**), clamped to the partition. With a `sort` and without `ignorePeers`, each end of the frame is
widened to include the peers (the rows with equal sort values) of the row at that end. The rows keep their
**input order** in the output.

**Stack.** A group is stacked in the order of the sort fields (a stable sort: null first, NaN next, then by
value; no sort keeps the table order); the first row is nearest to zero. `zero` stacks a value below 0 downwards
from 0, separately from the others (a NaN goes to the upward stack and poisons it, null counts as 0); `center`
stacks the absolute values from `(widest group sum - group sum) / 2` (a NaN sum never counts as the widest);
`normalize` stacks the absolute values scaled by `1 / group sum`, so a group that sums to 0 or NaN gets NaN ends
exactly as in Vega. The two output fields are the lower and the upper end.

**Impute.** The key domain is the `keyvals` (an array, or the sequence `start` 0, `stop`, `step` 1 with
`max(0, ceil((stop - start) / step))` values) followed by the keys observed in first-seen order; equal keys count
once (structurally). For each group and each key the group lacks, a row is **appended after the input rows**, in
group order and then key order, with the group fields, the key, and the imputed field; every other field is null.
The imputed field is the `value` (default 0; an explicit null is honored) or, for `mean`, `median`, `min`, and
`max`, that operation over the `frame` (default `[null, null]`) of the group in data order, and replaces every
null of the field, including the nulls that were already in the rows (the pair of a window and a formula in
Vega-Lite).

**Bin.** `BinSettings` holds the parameters (`maxbins` 20 when the algorithm is used raw, `base` 10, `divide`
`[5, 2]`, `step`, `steps`, `minstep`, `nice` true, `anchor`, `extent`; the `bin` transform and the position
channels ask for 10 bins). `BinSpec(Start, Stop, Step)` is the result. The `bin` transform writes the lower and
the upper boundary of the bin of every row to the two names of `as` (a name gives `name` and `name_end`; none
gives `bin0` and `bin1`); a row whose value is null or not a finite number gets null in both, and values outside
an explicit `extent` fall in the bins at minus and plus infinity.

**Stack rules.** `StackRules.Decide(markType, orient, encoding)` returns the `StackDecision` (stacked channel,
offset, stack-by definitions) or null. A mark is stackable when it is `arc`, `area`, `bar`, `rule`, `point`,
`circle`, `square`, `line`, `text`, or `tick`; `arc`, `area`, and `bar` stack by default (offset `zero`) and the
others only with an explicit `stack`. The stacked channel is the unbinned quantitative one of `x` and `y` (the
aggregating one wins when both are, then the `orient` of a bar or area). The group-by is the other position
channel. The stack-by channels are the non-position channels with a field that does not aggregate (the tooltip
is excluded; `order` counts). There is no stack when an aggregating encoding has no stack-by field, when the
stacked channel has a range (`x2` or `y2` with a field, datum, or aggregate), or when `stack` is false or null.

**Invariants**: the output of a transform is a new table within `MaxDataRows`, `MaxFieldCount`, and 2^25 cells;
the rows counted in the loader's budget are the rows of the tables that are alive; nothing is read from the
clock, the environment, a random source, or the current culture.

#### Key Methods

- `AggregateTransforms.Apply(input, entry, operation, context)` (static) - requires non-null arguments, creates
  the instance, and dispatches on the name of the operation member to `Aggregate`, `JoinAggregate`, `Window`,
  `Bin`, `Stack`, or `Impute`; any other name returns the input. `RowTransforms` calls it for the six names.
- `Aggregate(input, groupBy, measures, location, seed, releaseInput)` (internal) - the engine of `aggregate` and
  of the implicit aggregate of an encoding. Pass 1 (`GroupRows`) assigns every row to a group with `BuildKey`;
  pass 2 (`AccumulateGroups`) feeds each row to the `MeasureCell` of every measure of its group; pass 3 writes
  one output row per group (the group fields, then one field per measure; a later field of the same name wins).
  `joinaggregate` runs the first two passes, computes the result of every group **once**, and writes it to every
  row of the group (so a `values` array, or an `argmin` row, is the same shared value for all rows of the group);
  rows and their order are unchanged.
- `MeasureCell.Add` / `Result` - the accumulator protocol. `Result` of the order statistics sorts the kept
  numbers (charged as n times the ceiling of log2 n elements). The **deterministic bootstrap** of `ci0` and `ci1`
  draws 1,000 resamples of n values with SplitMix64: the seed of a group is
  `transformSeed XOR ((group + 1) * 0x9E3779B97F4A7C15)` (`MeasureCell.GroupSeed`) where `transformSeed` is the
  64-bit FNV-1a hash of the JSON pointer of the entry (`RowSampler.Seed`), each draw takes `values[next % n]`,
  and the bounds are the R-7 quantiles of the sorted means. `ci0` and `ci1` of one field in one group start from
  the same seed, so they share their resamples and `ci0 <= ci1`. The bootstrap is charged 1,000 times n elements
  and polls the cancellation token per resample.
- `Window(...)` (internal; `WindowRun`) - the engine of `window`, also used by `impute`. It partitions with
  `GroupRows` (partitions are not output rows), sorts each partition with `SortRows` (the sort keys are read
  once), finds the peer runs, and walks the partition once. The **cumulative fast path** applies when the frame
  has no start: the accumulators are extended incrementally to the end of the frame (`O(n)` for the partition);
  any other frame is recomputed from fresh cells for each row. Consecutive rows with the same frame share the
  result. The window-only operations use the peer runs: `rank` is the position of the first peer plus one,
  `dense_rank` the number of the run, `percent_rank` is `(rank - 1) / (n - 1)`, `cume_dist` is the position
  after the last peer over n, `ntile` is `ceil(param * cume_dist)`, `lag` and `lead` read a row `param`
  positions away in the sorted partition (null beyond the ends; the arithmetic is on 64-bit integers),
  `first_value`, `last_value`, and `nth_value` read the frame, and `prev_value` and `next_value` read the
  nearest non-null value at or before and at or after the row.
- `Stack(input, field, groupBy, sort, offset, startName, endName, location)` (internal) - the engine of `stack`
  and of the implicit stack: it groups, reads the field once as numbers, sorts each group, and stacks it as
  described above. The caller supplies the **order** as a list of `SortField`; the transform reads it from
  `sort`, the encoding stage builds it from the `order` channel or from the stack-by fields (see _EncodingPlan_).
  The default output names of the `stack` transform are `field_start` and `field_end`; an `as` of one name gives
  `name` and `name_end`.
- `Impute(...)` - bounds the result before allocating it, in this order: the number of `keyvals` against
  `MaxDataRows` (before any value is materialized); `groups * keys` rows reserved in the row budget (a lower
  bound of the result) and checked against 2^25; one bit per combination marks the present ones; the exact
  remainder of the rows is reserved; `MaxFieldCount` and the cell count are checked; the table is allocated; the
  nulls are replaced for the window methods; the input rows are released.
- `BinAlgorithm.Compute(min, max, settings, maxBinsLimit, location)` - the port of Vega's `bin`. The span is
  `max - min` (the magnitude of a single value, 1 for zero); the start step is
  `max(minstep, base^(round(log_base(span)) - ceil(log_base(maxbins))))`, multiplied by the base while
  `ceil(span / step) > maxbins`, then divided by each divisor where `span / (step / divisor) <= maxbins` and the
  step stays at or above `minstep`; `steps` picks the largest listed step smaller than `span / maxbins` (the
  first when none is); `nice` rounds the extent out to whole steps with a precision-based epsilon. For the
  extent 0 to 100 and 10 bins the bins are 0 to 100 in steps of 10; for 20 bins the step is 5; for the extent
  5 to 5 the bins are 5 to 5.5. `Align` rounds the stop up to a whole step and applies the `anchor` shift;
  `Resolve` is `Compute` then `Align` plus a finiteness check. `Assign(value, spec)` returns the lower boundary:
  minus infinity below the start, plus infinity above the stop, the last bin closed (the stop belongs to the bin
  below it), and a `1e-14` tolerance so a value such as 0.3 stays out of the bin below; `End` is
  `start + step * (1 + (binStart - start) / step)`.
- `WorkMeter.Add(elements)` - adds elements and pays one step per 64 as the total grows, **carrying the
  remainder** (`ExpressionFunctionContext.ChargeSize` rounds down, so a loop that charged a few elements per row
  would pay nothing); paying also polls the cancellation token.
- `StableSorter.Sort(items, start, count, buffer, compare, token)` - a bottom-up merge sort (the left run wins a
  tie), so equal items keep their order, with one buffer and a cancellation check per merge block. `Array.Sort`
  is neither stable nor cancelable.
- `StackRules.Decide`, `AggregateMeasures.TryParse`, `TryParseWindow`, `GetName`, `RequiresField`,
  `TakesParameter`, `CanOrderCategories` (false for `argmin`, `argmax`, `values`, and the exponential
  operations), `IsArgOp`, and `IsExponential` - the pure registry functions that the parser, the catalog, the
  plan, and the scale resolver share, so that validation, reports, and execution agree on what an operation is.

**Charging matrix.** Every row loop calls `TransformContext.Tick(row)` (cancellation every 1,024 rows) and
charges the `WorkMeter`; every limit message contains `the limit of` or `VegaLiteLimits.` (or names
`MaxDataRows`) so that the tool classifies it as a budget failure.

<!-- markdownlint-disable MD013 -->
| Transform | Row budget (`MaxDataRows`) | Cells (2^25) and field limits | Work charged to the step budget |
| --------- | -------------------------- | ----------------------------- | ------------------------------- |
| `aggregate` | One row reserved per group as the group appears; the whole input is released when the output is complete (peak: input plus groups) | Each new group checks `groups * (output width + 4 * measures)`; `rows * value-keeping operations` checked up front; `TableLayout` | 1 + group fields per row, measures per row, 1 per stored value, n log n per sort, 1,000 n per bootstrap, text length per `distinct` key and per text-to-number conversion |
| `joinaggregate` | Unchanged (groups are not rows) | Per group `measures * (4 + 1) + group fields`; same value-state check; `TableLayout` | As `aggregate`, plus measures + 1 per output row |
| `window` | Unchanged; the `values` arrays are bounded by `MaxDataRows` in total | Partition keys 2 + group fields per group; sort keys `rows * sort fields`; value-state check; `TableLayout` | operations + 1 per row; n log n per sort; **4 per row of every recomputed frame per aggregate** (cumulative frames are not recomputed); `values` length |
| `bin` | Unchanged | `TableLayout`; bin count at most `MaxScaleDomainSize` (also for an explicit `step`) | 1 per row for the extent, 1 per row for the assignment |
| `stack` | Unchanged | 2 + group fields per group; sort keys; `TableLayout` | 1 per row for the copy and 1 for the number, the group size per group, n log n per sort |
| `impute` | `groups * keys` reserved, then the exact remainder; the input is released at the end (peak: input plus output) | `keyvals` count against `MaxDataRows`; one bit per combination; `MaxFieldCount`; output cells | 1 or 2 per row, 1 + group fields per imputed row, the window engine for the other methods |
<!-- markdownlint-enable MD013 -->

**Preconditions and postconditions**: `SpecParser` validated the transform shapes (operation names, parameters,
frames, bin parameters, key values) and the texts of the specification fit the limits it was parsed with; on
return the table is complete and within the limits and the loader's row count equals the rows of the tables that
are alive, or an exception was thrown.

#### Error Handling

Everything that depends on the data and exceeds a limit is a `VegaLiteRenderException` located at the transform
entry (or, for the engines the encoding stage calls, at the channel definition that asked for them):
`MaxDataRows` (checked before the output is allocated), `MaxFieldCount` and the 2^25 cell bound (also for the
group states and the value stores), the step budget (the `ExpressionRuntimeException` of the meter is converted
with the **same message**), a text longer than `MaxStringLength` (a name or a value of the specification, which
`TransformContext.CheckText` checks again because the call can have a lower limit than the parse; a group value
or a distinct key at run time), a bin count above `MaxScaleDomainSize`, and a bin that cannot be computed.
`BinAlgorithm` fails with "The values cannot be binned" when the extent is not two finite ordered numbers, when
no finite positive step exists, or when the anchor moves the boundaries out of range; the growth loop of the
step is **capped at 2,048 iterations** (Vega can loop for ever when the base is close to 1 or the step
underflows). A canceled token is an `OperationCanceledException`, never wrapped. A malformed transform is not an
error of this unit: `SpecParser` rejected it when the document was opened. A null argument is an
`ArgumentNullException`. Nothing here produces a warning: a value that cannot be used is null or NaN in the
output, as documented per operation. The unit performs no I/O.

**Documented deviations from Vega and Vega-Lite** (each pinned by a test):

1. **Deterministic bootstrap** (`ci0`, `ci1`): SplitMix64 seeded from the FNV-1a hash of the JSON pointer and the
   group (Vega draws with `Math.random`), so a chart renders identically on every call.
2. **Structural group keys, first-seen order**: Vega keys groups by the text of the values joined with `|` (so 1
   and `"1"` collide) and lists them in the order of a JavaScript object.
3. **Default `as` of `stack`**: `field_start` and `field_end` (Vega's own default is `y0` and `y1`); an `as`
   string gives `name` and `name_end`.
4. **Empty frames**: `first_value`, `last_value`, and `nth_value` give null for an empty frame (Vega reads past
   the partition), and aggregates over an empty frame give the results of an empty cell (count 0, sum null).
5. **`cume_dist` and `ntile` count the peers of the first rows of a partition**: Vega under-reports the first
   row when it has peers; the documented meaning is used. `percent_rank` of a one-row partition is NaN (0
   divided by 0), as in Vega.
6. **Exponential default parameter**: a missing `param` is 0.5 (Vega-Lite does not require one).
7. **Bin growth cap** and located failures for an extent that no finite step covers, and the bin count bound by
   `MaxScaleDomainSize`; JavaScript's `Math.round` (half toward positive infinity) is used.
8. **Impute and `keyvals`**: keys are deduplicated structurally; a sequence is bounded by `MaxDataRows` before
   it is built.
9. **`argmin` and `argmax`** return the whole first row as an object; the default name is `argmax_field`.

#### Dependencies

- **`TableLayout`** and **`FieldReader`** - the layout, bounds, and field reading of derived tables (the
  `RowTransforms` unit), and **`TransformContext`**
- **`RowSampler`** - `Seed` (FNV-1a) and `Next` (SplitMix64) for the bootstrap seeds
- **`DataLoader`** - the row budget (`ReserveDerivedRows`, `ReleaseRows`)
- **`ExpressionEvaluator`**, **`ExpressionFunctionContext`**, and **`ExpressionRuntimeException`** - coercion
  (`ToNumber`, `ToJsString`, `RelationalLess`), the step budget, and the failure that is converted
- **`DataTable`**, **`DataValue`**, **`FieldPath`**, and **`JsNumber`** - the value and table model of the
  `Data` subsystem
- **`SpecNode`** and **`SpecTree`** - the tree of the `SpecModel` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System.Numerics` (`BitOperations`) and `System.Globalization`
  (invariant-culture text)

#### Callers

`RowTransforms.Apply` dispatches the six transform names to `AggregateTransforms.Apply`. `EncodingTransforms`
calls the `Aggregate` and `Stack` engines and `BinAlgorithm` for the implicit pipeline of a view (see
_EncodingPlan_), and `EncodingPlan` uses `StackRules`, `BinSettings`, `AggregateSpec`, and `AggregateMeasures`.
`ScaleResolver` uses `MeasureCell`, `MeasureEnvironment`, `WorkMeter`, `BinSpec`, and `AggregateMeasures` to sort
categories by an aggregate and to read the bins. `SpecParser` (`SpecParser.Aggregates.cs`) uses
`AggregateMeasures` to validate operation names, and `FeatureCatalog` calls `StackRules.Decide` (with the
orientation from `ScaleResolver.InferOrient`) to decide whether the `order` channel orders a stack and
`StackRules.IsEnabled` for the stack of a non-linear scale. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/` (`AggregateTransforms*Tests.cs`,
`AggregateMeasuresTests.cs`, `BinAlgorithmTests.cs`, `StackRulesTests.cs`, and `StableSorterTests.cs`) call the
types directly through `InternalsVisibleTo`.
