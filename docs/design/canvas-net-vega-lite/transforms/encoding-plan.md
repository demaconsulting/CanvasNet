### EncodingPlan

![Transforms Structure](VegaLiteTransformsView.svg)

The `EncodingPlan` unit is the third software unit in the `Transforms` subsystem. It comprises two source files
in the `DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, all `internal`:

- `EncodingPlan.cs` - `EncodingPlan` (sealed class: the pure plan of a view) and its records `ChannelPlan`,
  `BinPlan`, `TimeUnitPlan`, `AggregatePlan`, `ArgField`, `StackPlan`, `ImputePlan`, and `StackImputePlan`, and the
  enumeration `BinKind`
- `EncodingTransforms.cs` - `EncodingTransforms` (sealed class: the execution of a plan on a table) and
  `EncodingData` (the Raw and Main tables and the bins)

#### Purpose

`EncodingPlan` turns the encoding of a view into the **implicit pipeline** that Vega-Lite compiles from it: the
bins of the binned fields, the time units of the fields that have one, the aggregate of the aggregating definitions,
the imputes of the path marks, and the stack of the stacked channel (on the Cartesian or on the polar channels);
and it names the field that every channel then reads, its second field, and its default title. For the path marks
(line, area, and trail) it also says which fields split the rows into paths and in which order the rows of a path
are drawn.
`EncodingTransforms` runs that pipeline on the rows that the explicit transforms left. The plan is the **single
source of truth** for the derived field names and titles, so that the scales, the marks, and the axes agree on
them without knowing how they were computed. Splitting the pure analysis from the execution lets the scale
resolver and the tests ask what a channel reads without a table.

The unit's behavior is specified by the `CanvasNetVegaLite-Transforms-EncodingPlan-*` requirements in
`docs/reqstream/canvas-net-vega-lite/transforms/encoding-plan.yaml`. The rules are those of the Vega-Lite
compiler (`bin`, `aggregate`, `stack`, field naming, and titles), re-implemented from their public description.

#### Data Model

**`EncodingPlan`** is immutable and safe for concurrent use. It is created by
`EncodingPlan.Create(view, config)` and holds `MarkType`, `Orient` (the orientation Vega-Lite infers, from
`ScaleResolver.InferOrient`), `Channels` (a `ChannelPlan` for every definition of the encoding, array channels
element by element and `x2`, `y2`, `theta2`, and `radius2` included), `Bins` (the distinct `BinPlan`s), `TimeUnits`
(the distinct `TimeUnitPlan`s), `Aggregate` (an
`AggregatePlan` or null), `Stack` (a `StackPlan` or null), `NeedsRaw`, `PathGroupFields`, `OrderFields`, `Impute` (an
`ImputePlan` or null), `StackImpute` (a `StackImputePlan` or null), and `IsIdentity` (no bin, no time unit, no
aggregate, no impute, no stack). `Channel(name)` finds the first definition of a channel. A view without a mark or an
encoding has an
identity plan with no channels.

**`ChannelPlan`** `(Channel, Definition, SourceField, ValueField, RawField, FieldPath, Field2Path, Bin, BinPlan,
Measure, IsStacked, Title, FieldDefinition, TimeUnit, HasBandEnd)` - one definition and what the pipeline makes of it:

- `Definition` is always the channel node (the guides and marks find the plan by it). `FieldDefinition` is the object
  that carries the field when that is not the channel node: a channel whose own definition is a value, with a condition
  on a test that holds a field, reads that field (Vega-Lite's `getFieldOrDatumDef`); `FieldDef` is the
  `FieldDefinition` or else the `Definition`, and the aggregate, bin, type, scale, sort, and legend of the channel are
  read from it. A condition that holds only values, or a parameter, gives the channel no field.
- `SourceField` is the field path as written; `ValueField` is the field the channel reads **before the stack**
  (the derived field of a bin or an aggregate, else the source field); `RawField` is the field in the **Raw**
  table (the source field of an aggregate, none for a count); `FieldPath` is the field in the **Main** table (the
  value field, or the upper end of its stack); `Field2Path` is the second end (the lower end of a stack, the end
  of a bin, the field of a linked `x2` or `y2`).
- `Bin` is a `BinKind`: `None`; `Binning` (the encoding bins the field: `bin` true or an object); `Binned` (the
  data is already binned: `bin: "binned"` or `{"binned": true}`, the field is the start of a bin and the
  secondary channel its end).
- `Measure` is the `AggregateSpec` of an aggregating definition; `Title` is the default title (null when the
  channel reads no field).
- `TimeUnit` is the `TimeUnitPlan` of a definition with a time unit (shared by every definition of the same field and
  unit; null for any other), whose start field the channel reads; `HasBandEnd` is true when the channel **spans** its
  unit, in which case `Field2Path` is the end field of the unit (see below).

**Derived field names** (Vega-Lite's `vgField`; the names are literal and are escaped with `FieldPath.Escape`
when used as paths):

- bin: `bin` followed by `_name_value` for every parameter of the bin object **in the order written**, with
  every character that is not a letter, a digit, or an underscore replaced by an underscore, then `maxbins_N`
  when the parameters give neither `maxbins` nor `step` (`N` is Vega-Lite's `autoMaxBins` of the channel: 10 for a
  position channel, for `angle`, and for `text`, 6 for `color`, `fill`, `stroke`, `size`, the opacities, `strokeWidth`,
  and
  `shape`, and 4 for `strokeDash`), then `_` and the field: `bin_maxbins_10_x`, `bin_maxbins_6_x`,
  `bin_maxbins_20_x`, `bin_step_5_x`. The upper boundary adds `_end` and the text of a bin on a discrete channel
  adds `_range`. A bin is shared by the channels that name the same field and parameters, and computed once.
- aggregate: `sum_x` (the operation as written, so `average_x`, not `mean_x`), `__count` for a count,
  `argmax_t` for `{"argmax": "t"}` (a measure that holds the winning row; the channel reads the scalar field
  `argmax_t.v` that `EncodingTransforms` copies out of it), and `exponential_x` for `{"exponential": 0.5}`.
- time unit: the name of the unit and the field, `TimeUnitSpec.FieldName(field)`: `yearmonth_date` for the unit
  `yearmonth` of the field `date`, `month_step_2_date` for the object form with a `step`, `utcmonth_date` for a UTC
  unit, and `timeunit_maxbins_10_date` for the `maxbins` form; the end adds `_end` (`yearmonth_date_end`). A unit is
  shared by the channels that name the same field and unit, and computed once. A unit that is `binned` is skipped (the
  field is used as it is), and so is a definition that has an aggregate or a `bin` (the bin wins over the unit).
- stack: `<measure>_start` and `<measure>_end`, where the measure is the derived name of an aggregate or the
  field itself.

**Default titles** (Vega-Lite's verbal titles, with `config.fieldTitle` of `verbal`, `plain`, or `functional`):
`x` for a plain field, `x (binned)` for a bin (`BIN(x)` functional), `Sum of x` for an aggregate (`SUM(x)`, `x`
plain), `Q1 of x` (the first letter is upper-cased), `x for max t` for `argmax`, and `config.countTitle`
(`Count of Records` by default) for a count in every mode. A range merges its two titles as `a, b` (distinct
titles only).

**Time-unit titles.** For a definition with a time unit (and no bin or aggregate; a bin wins, and the verbal title of a
unit is tested before the aggregate one as in Vega-Lite): verbal `date (year-month)` (the field and the parts of the unit
joined with a hyphen; the field alone for the `maxbins` form, which has no parts), functional `YEARMONTH(date)`
(`UTCYEARMONTH(date)` for a UTC unit, and `TIMEUNIT(date)` for the `maxbins` form), and plain the field.

**`TimeUnitPlan`** `(StartName, EndName, SourceField, Spec, Node, HasBandEnd)`: the two literal names, the field of the
dates, the `TimeUnitSpec` as written (the `maxbins` form is resolved against the data when the transform runs and the
resolved unit is handed over in `EncodingData.TimeUnits`), the `timeUnit` value of the first definition that asked for
it (the location of failures), and whether any channel that shares it spans it (`HasBandEnd`). A unit **spans**
(Vega-Lite's `hasBandEnd`) when the channel is `x` or `y`, the type
of the field is `temporal` (the type a time unit implies when none is written), the encoding has **no secondary
channel** for it (`x2` or `y2`), and a band position is defined: a numeric `bandPosition` of the definition, a numeric
`timeUnitBandPosition` of the mark definition, or one of the configuration layers of the mark (`config.<mark>` and its
`style`s, then `config.mark`, resolved with `ResolvedConfig.ResolveMarkProperty`; the bar, rect, and tick have 0.5 by
default, a point has none and sits at the start of its unit). A spanning channel reads the end field as its second field
(`Field2Path`), so a bar covers the whole unit; the marks and scales then read both ends.

**`BinPlan`** `(StartName, EndName, RangeName, SourceField, Settings, Node, Format)`: the three literal names
(the range name only when a discrete channel shows the bin as text), the `BinSettings`, the `bin` value that
located the first request, and the number format of the boundaries in the text (the `axis.format` of the first
discrete position channel or the `legend.format` of the first discrete channel of another kind, else
`config.numberFormat`, else the default specifier).

**`AggregatePlan`** `(GroupBy, Measures, ArgFields)`: the group-by is **every other field definition of every
channel** (array channels element by element; a definition that has no field contributes nothing; a binned
definition contributes its start, its end, and its range text; a definition with a time unit contributes its start
field and, when the channel spans its unit, its end field), without duplicates; the measures are the
aggregating definitions, one per distinct output name, in channel order. The end of a unit follows from its start, so
including it adds no groups, but it keeps the end field alive after the aggregate for the marks that span the unit
(without it `Field2Path` would read null); a mark that does not span (a point) groups by the start only.

**`StackPlan`** `(Channel, Offset, Field, StartName, EndName, GroupBy, Sort, Definition)`: the stacked channel
(`x`, `y`, `theta`, or `radius`),
the offset, the field that is stacked (the value field of the channel), the two literal output names, the
group-by (the value fields of the dimension channel and its secondary, plus the end of a bin and the end of a
unit that the channel spans), and the
**order of the layers** as `SortField`s: the fields of the `order` channel (each with its own `sort`, ascending
by default) when the encoding has one, otherwise the stack-by fields (see `StackRules` in
_AggregateTransforms_) **descending when the stacked channel is `y` and ascending otherwise** (so a stack on `theta`
or `radius` is ascending, like one on `x`). The dimension channel is the other channel of the pair: `y` for `x`, `x`
for `y`, `radius` for `theta`, and `theta` for `radius` (`StackRules.DimensionChannel`), and **a dimension that reads
the stacked field itself does not split the stack** (a radial chart reads its `theta` and its `radius` from one field;
grouping by that field would start every slice at 0). The decision itself is
`StackRules.Decide(markType, orient, encoding)`.

**`NeedsRaw`** is true when a channel (other than `x2`, `y2`, `theta2`, and `radius2`, which create no scale) sorts its
categories by an object or a channel
shorthand (not `ascending`, `descending`, null, or an array of values) or has `scale.domain` of `unaggregated`:
only then does a scale read the table as it was before the aggregate.

**`PathGroupFields`** (Vega-Lite's `pathGroupingFields`; the canonical field paths in channel order, without
duplicates) are the fields that split the rows of a line, area, or trail into paths. A channel groups when it is
`detail` or `key`; `order` (for every mark but the line and the trail: the order of a stack of areas is a group, the
order of a line is its drawing order); `size` (for every mark but the trail: the width of a line or area is per path,
the width of a trail is per point); or one of the paint channels `color`, `fill`, `stroke`, the three opacities,
`strokeDash`, and `strokeWidth`. A definition that aggregates never groups, and neither do the position channels, the
polar channels, `text`, `shape`, `angle`, the tooltip, and the link channels. The field of a channel is the one it
reads before the stack (the start field of a bin, or the range text of a bin on a discrete channel). The plan computes
the list for every mark; `MarkBuilder` reads it for the path marks only (see _MarkBuilder_).

**`OrderFields`** (a list of `SortField`) are the fields of the `order` channel with their direction: one entry for
every definition that reads a field, in order, descending when the definition's `sort` is `descending` (a definition
without a field, such as `{"value": null}`, adds none). They are the order of the layers of a stack, of the points of a
path, and of the drawing of the other marks.

**`ImputePlan`** `(Field, Key, GroupBy, Definition)` is the impute that the `impute` object of the `x` or `y` channel
asks for (Vega-Lite's `ImputeNode.makeFromEncoding`): `Field` is the value field of the channel that has the object,
`Key` the value field of the other position channel, `GroupBy` the `PathGroupFields`, and `Definition` the channel
definition (its `impute` object gives the `method`, `value`, `frame`, and `keyvals` when the transform runs, and its
location a failure). The plan exists only when **both** `x` and `y` are field definitions; `x` wins when both have an
`impute` object. An imputed channel that aggregates is imputed through its derived name after the aggregate (Vega-Lite
would use the raw field).

**`StackImputePlan`** `(Field, Key, GroupBy, Definition)` is the impute that precedes the stack of a line, area, or
trail (Vega-Lite's stack node with an impute): every layer of the stack gets a zero for each key of the dimension it
lacks, so that the stacked paths have a point at every key. `Field` is the stacked field, `Key` the value field of the
dimension channel, `GroupBy` the fields of the stack-by definitions in channel order (the layers), and `Definition`
the stacked definition. The plan exists when the mark is a line, area, or trail, the marks are stacked, the stacked
definition does not say `impute: null`, the dimension is an unbinned field, and there is at least one stack-by field
(without one there is a single layer, which has every key already, so the impute would add nothing). It is **skipped
for a binned dimension** (Vega-Lite imputes it through a computed mid-point field, which this renderer does not
compute; the feature catalog warns with `vl-encoding-stack-impute-bin`), for any enabled `bin` including `"binned"`,
and for a dimension whose time unit spans its unit (the stack also groups by the end field).

**`EncodingData`** `(Raw, Main, Bins, Evaluation, TimeUnits)` is the result of the execution: `Raw` is the table after the
explicit transforms, the implicit bins, and the implicit time units, before the aggregate; `Main` is the table after the
aggregate, the imputes, and the
stack, which the marks and every other scale read; `Bins` maps the literal start name of a bin to its `BinSpec`
(a field without a valid value has none); `TimeUnits` (the optional last constructor parameter; empty when there is none)
maps the literal start name of a time unit to its **resolved** `TimeUnitSpec` (the `maxbins` form is the unit that the
extent of the data chose, or the one as written when the data has no valid date), which the scales, the axis labels,
and the marks read for the domain, the minimum tick step, the label format, and the band geometry; `Evaluation` is the
evaluation context of the call, which the scales
charge their work to (and which carries the calendar of the chart). `Raw` and `Main` are the **same instance** when
the encoding neither aggregates, imputes, nor
stacks, or when no scale needs `Raw`. `EncodingData.Identity(table, evaluation)` is the data of an identity plan.

**Invariants**: the plan reads no data; two calls with the same view and configuration give equal plans; nothing
is read from the clock, the environment, or the current culture.

#### Key Methods

- `EncodingPlan.Create(view, config)` - requires a non-null view and configuration; reads the mark (a string or
  the `type` of a definition), infers the orientation, plans every definition (`PlanDefinition`: the aggregate,
  the bin, the time unit and whether it spans, the fields, the title), shares the bins and the time units by start name,
  applies `StackRules` and plans the stack
  (marking the stacked channel: its `FieldPath` becomes the end and its `Field2Path` the start), links each `x`,
  `y`, `theta`, and `radius` to its secondary channel (`LinkRanges`: `x2`, `y2`, `theta2`, and `radius2`; not for a
  stacked channel and not for a bin the encoding
  computes), then plans the aggregate, decides `NeedsRaw`, and plans the path grouping fields, the order fields, and
  the two imputes. A `bin` is planned on `x`, `y`, and the channels that
  have a scale or show the bin as text (`CanBin`: `color`, `fill`, `stroke`, `size`, the opacities, `strokeWidth`,
  `strokeDash`, `shape`, `angle`, and `text`), only for a definition that has a field and does not aggregate;
  `bin: "binned"` is read
  on `x` and `y` only, and the other channels keep reporting `vl-encoding-bin` for it. A `bin` on `theta` or `radius`
  is **not planned** (no polar bin span exists, so the feature catalog keeps reporting `vl-encoding-bin` for it). A
  nominal or ordinal binned channel reads the `_range` field and has no second field; a quantitative one reads the
  start and has the end as its second field, which the scale resolver uses to find the bins. A binned `text` channel
  reads the start and the end, which the text mark formats as `start – end`.
- `EncodingPlan.BinStartName(bin, field, defaultMaxBins)` - the literal name rule above; `EncodingPlan.AutoMaxBins(channel)`
  gives the default number of bins of a channel.
- `EncodingTransforms.Apply(plan, table, context)` (static) - requires non-null arguments; returns
  `EncodingData.Identity` for an identity plan; otherwise runs the stages in **Vega-Lite's order: explicit
  transforms (done by the caller), bin, time unit, Raw, aggregate, impute, stack, Main**:
  1. **Bin** (`BinFields`): for every distinct bin the extent of the finite numeric values of the field (or the
     `extent` parameter) goes to `BinAlgorithm.Resolve` with the `MaxScaleDomainSize` limit; then one pass
     writes the start, the end, and, for a discrete channel, the range text (the two boundaries formatted and
     joined with an en dash, formatted once per distinct bin, and the text `null` for a row without a valid
     value) with one `TableLayout`.
  2. **Time unit** (`TimeUnitFields`): one call of `TimeUnitTransform.Apply` (see _Time Unit Transform Design_) with a
     request per distinct unit (the source field, the start and end names, the unit, and the `timeUnit` value, which
     locates a failure, for example at `/encoding/x/timeUnit`) in the calendar of the chart, which adds every start and
     end field as dates and returns the unit that was resolved for each request; the resolved units are recorded in
     `EncodingData.TimeUnits`. The fields are written before the aggregate, so `Raw` has them and every scale reads them.
  3. **Aggregate**: one call of the grouping engine of _AggregateTransforms_ with the plan's group-by and
     measures, `releaseInput: false` (the rows stay counted while `Raw` is in use), seeded from the pointer of
     the first aggregating definition; the winning rows of `argmax` and `argmin` measures are copied into their
     scalar fields (`ReadArgFields`); when no scale needs `Raw`, its rows are released and `Raw` becomes `Main`.
  4. **Impute** (`Impute`): the encoding impute first (`plan.Impute`, when the plan has one), then the stack impute
     (`plan.StackImpute`). Both call `AggregateTransforms.ImputeField` on the aggregated table (or on `Raw` when there
     was no aggregate), appending a row for every missing combination of series (the plan's `GroupBy`) and key. The
     encoding impute reads its `method`, `value`, `frame`, and `keyvals` from the `impute` object of the channel
     (`AggregateTransforms.ReadImputeParameters`, the same reader as the `impute` transform), is seeded from the pointer
     of
     that definition, and is located at the object; the stack impute has no parameters (the method `value` with the
     constant 0, no `keyvals`) and is located at the stacked definition. An appended row holds the group fields, the key,
     and the imputed field (the constant 0 for the stack impute), and null in every other field.
  5. **Stack**: the stack engine with the plan's field, group-by, layer order, and offset; the rows keep their
     place.
  6. **Main**. The invalid-row filter of Vega-Lite's last stage is done by the marks, which skip a row whose
     position is not a number.
- `EncodingTransforms.ApplyStandalone(plan, table, view, sourceName, limits, token)` - builds a private context
  (evaluator and loader both with the UTC calendar, loader with an empty row budget, no parameters) for callers that
  have only a view and a table;
  used by the convenience entry of `ScaleResolver` and by tests.

**Row accounting.** The bin stage and the time unit stage replace their input by a table of the same size, with the
added fields (no change in the row count). The aggregate
reserves one row per group as the group appears (the input stays counted, so the peak of the input and the
groups is bounded) and releases the input rows only when `Raw` is not needed. An impute reserves its result through
the engine (see _AggregateTransforms_) and gives its input rows back, **except that it keeps them counted when the
table it imputes is the `Raw` table that a scale still reads** (the encoding passes `releaseInput: false`); an
imputed table replaces `Raw` as well when no scale needs `Raw`. A stack replaces its input (no
change), except that **a stack next to a kept `Raw` table reserves its own rows** (`ReserveDerivedRows`), because both
tables are alive; when the stacked table was the aggregated or imputed one (not `Raw` itself), its rows are given back
once the stacked copy exists. The work of every row is charged to the evaluation-step
budget by the engines and the cancellation token is polled every 1,024 rows.

**Preconditions and postconditions**: the view was validated by `SpecParser` and `table` holds the rows after
the explicit transforms; on return `Main` holds the fields the plan names, the loader's row count equals the
rows of the tables that are alive, and the explicit-transform tables that are no longer used have been released.

#### Error Handling

The plan never fails for a valid specification: a construct it cannot plan (a `bin: "binned"` on another channel, a measure
without a field, an operation that is not an aggregate) is skipped and is the feature catalog's to report. The
execution reports everything that depends on the data as a `VegaLiteRenderException` located at the construct
that asked for it - the `bin` value (a bin count above `MaxScaleDomainSize`, an extent that cannot be binned, a
field limit), the `timeUnit` value (a `maxbins` above `MaxScaleDomainSize`, the step budget of the unit computation, a
field limit), the first aggregating definition (a group, cell, step, or string limit), the `impute` object of a channel
(a `keyvals` count above the row limit, a combination, row, cell, or step limit), or the stacked
definition (also for the impute that precedes a stack) - with the limit named in the message, as _AggregateTransforms_
does; an `ExpressionRuntimeException`
of the engines is converted with the same message. A canceled token is an `OperationCanceledException`, never
wrapped. A null argument is an `ArgumentNullException`. The unit performs no I/O and produces no warning.

**Differences from Vega-Lite**: `bin: "binned"` is read on `x` and `y` only (a bin of the other channels is
computed from the data), and a `bin` on `theta` or `radius` is not planned; a count is titled by `countTitle` in every
`fieldTitle` mode; the name of an aggregate
keeps the operation as written (`average_x`); a non-finite stack value is silently invalid, as in
Vega-Lite; a `binned` time unit is skipped in an encoding (the field is used as it is; the catalog reports it as
`vl-timeunit-binned`); a channel that spans its unit covers the whole unit (the geometry applies 0.5, so another
`bandPosition` is reported by `vl-encoding-bandposition-rect`); the aggregate groups by the end of a unit that a
channel spans (it adds no groups; see `AggregatePlan`); an imputed channel that aggregates is imputed through its
derived name after the aggregate (Vega-Lite uses the raw field); and the impute that precedes a stack is skipped for a
binned dimension (Vega-Lite imputes it through a computed mid-point field) and when there is no stack-by field.

#### Dependencies

- **`AggregateTransforms`**, **`BinAlgorithm`** and `BinSettings`, **`StackRules`**, **`AggregateMeasures`** and
  `AggregateSpec`, **`WorkMeter`**, and **`TransformContext`** - the engines and records of
  _AggregateTransforms_ and the shared context; **`TableLayout`** and **`FieldReader`** of _RowTransforms_;
  **`TimeUnitTransform`** and `TimeUnitRequest` - the time unit stage (see _Time Unit Transform Design_)
- **`TimeUnitSpec`** - the time unit grammar, the field names, and the parts of the `Data` subsystem (see
  _Time Calendar Design_)
- **`ScaleResolver.InferOrient`** - the orientation rule of the `Scales` subsystem (the plan reuses it so that
  the stack and the geometry agree on the orientation), and **`NumberFormat`** for the text of a bin
- **`DataLoader`** - the row budget; **`DataTable`**, **`DataValue`**, and **`FieldPath`** of the `Data`
  subsystem
- **`SpecNode`**, **`SpecTree`**, and **`ResolvedConfig`** - the tree and the configuration of the `SpecModel`
  subsystem
- **`ExpressionEvaluator`** and **`ExpressionFunctionContext`** - the evaluator of the call (step budget)
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit

#### Callers

`VegaLiteDocument` creates the plan once per `Measure` or `Render` call for a single view with a supported mark,
right after `RowTransforms.Apply`, runs `EncodingTransforms.Apply` on the same `TransformContext` (so the
warnings and the row budget flow through one place), and keeps the plan and the data in its chart inputs.
`ScaleResolver` reads the plan for every channel (field, second field, title, bin kind, time unit, whether it spans,
stacked or not, measure,
raw field) and the data (`Main` for domains, `Raw` for a sort by an aggregate and for `unaggregated`, `Bins` for
a binned domain, `TimeUnits` for the unit of a temporal domain, `Evaluation` for the work charge and the calendar); its
results (`ResolvedScale.FieldPath`, `Field2Path`,
`Title`, `Bin`, `BinSpec`, and `Stack`) are what `MarkBuilder` and `AxisBuilder` consume, and the marks draw the
`Main` table (the path marks also read `PathGroupFields` and `OrderFields`; `MarkBuilder` asks `PlanPathGroupFields`
directly for the rows of an overlay, which group as a line). `FeatureCatalog` does not use the plan: it shares
`StackRules` and `ScaleResolver.InferOrient` with it, so that the report (the stack that is not linear, and the warning
`vl-encoding-stack-impute-bin` for a binned dimension that the stack impute skips) and the plan decide alike. The tests
in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/EncodingPlanTests.cs`, `EncodingPlanTimeTests.cs`,
`EncodingPlanPolarTests.cs`, `EncodingPlanPathTests.cs`, `EncodingTransformsTests.cs`, `EncodingTransformsTimeTests.cs`,
and `EncodingTransformsImputeTests.cs`
call the unit directly through `InternalsVisibleTo`.
