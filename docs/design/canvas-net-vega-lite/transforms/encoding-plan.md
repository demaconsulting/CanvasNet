### EncodingPlan

![Transforms Structure](VegaLiteTransformsView.svg)

The `EncodingPlan` unit is the third software unit in the `Transforms` subsystem. It comprises two source files
in the `DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, all `internal`:

- `EncodingPlan.cs` - `EncodingPlan` (sealed class: the pure plan of a view) and its records `ChannelPlan`,
  `BinPlan`, `AggregatePlan`, `ArgField`, and `StackPlan`, and the enumeration `BinKind`
- `EncodingTransforms.cs` - `EncodingTransforms` (sealed class: the execution of a plan on a table) and
  `EncodingData` (the Raw and Main tables and the bins)

#### Purpose

`EncodingPlan` turns the encoding of a view into the **implicit pipeline** that Vega-Lite compiles from it: the
bins of the binned fields, the aggregate of the aggregating definitions, and the stack of the stacked channel;
and it names the field that every channel then reads, its second field, and its default title.
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
element by element and `x2` and `y2` included), `Bins` (the distinct `BinPlan`s), `Aggregate` (an
`AggregatePlan` or null), `Stack` (a `StackPlan` or null), `NeedsRaw`, and `IsIdentity` (no bin, no aggregate,
no stack). `Channel(name)` finds the first definition of a channel. A view without a mark or an encoding has an
identity plan with no channels.

**`ChannelPlan`** `(Channel, Definition, SourceField, ValueField, RawField, FieldPath, Field2Path, Bin, BinPlan,
Measure, IsStacked, Title)` - one definition and what the pipeline makes of it:

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

**Derived field names** (Vega-Lite's `vgField`; the names are literal and are escaped with `FieldPath.Escape`
when used as paths):

- bin: `bin` followed by `_name_value` for every parameter of the bin object **in the order written**, with
  every character that is not a letter, a digit, or an underscore replaced by an underscore, then `maxbins_10`
  when the parameters give neither `maxbins` nor `step`, then `_` and the field: `bin_maxbins_10_x`,
  `bin_maxbins_20_x`, `bin_step_5_x`. The upper boundary adds `_end` and the text of a bin on a discrete channel
  adds `_range`. A bin is shared by the channels that name the same field and parameters, and computed once.
- aggregate: `sum_x` (the operation as written, so `average_x`, not `mean_x`), `__count` for a count,
  `argmax_t` for `{"argmax": "t"}` (a measure that holds the winning row; the channel reads the scalar field
  `argmax_t.v` that `EncodingTransforms` copies out of it), and `exponential_x` for `{"exponential": 0.5}`.
- stack: `<measure>_start` and `<measure>_end`, where the measure is the derived name of an aggregate or the
  field itself.

**Default titles** (Vega-Lite's verbal titles, with `config.fieldTitle` of `verbal`, `plain`, or `functional`):
`x` for a plain field, `x (binned)` for a bin (`BIN(x)` functional), `Sum of x` for an aggregate (`SUM(x)`, `x`
plain), `Q1 of x` (the first letter is upper-cased), `x for max t` for `argmax`, and `config.countTitle`
(`Count of Records` by default) for a count in every mode. A range merges its two titles as `a, b` (distinct
titles only).

**`BinPlan`** `(StartName, EndName, RangeName, SourceField, Settings, Node, Format)`: the three literal names
(the range name only when a discrete channel shows the bin as text), the `BinSettings`, the `bin` value that
located the first request, and the number format of the boundaries in the text (the `axis.format` of the first
discrete channel, else `config.numberFormat`, else the default specifier).

**`AggregatePlan`** `(GroupBy, Measures, ArgFields)`: the group-by is **every other field definition of every
channel** (array channels element by element; a definition that has no field contributes nothing; a binned
definition contributes its start, its end, and its range text), without duplicates; the measures are the
aggregating definitions, one per distinct output name, in channel order.

**`StackPlan`** `(Channel, Offset, Field, StartName, EndName, GroupBy, Sort, Definition)`: the stacked channel,
the offset, the field that is stacked (the value field of the channel), the two literal output names, the
group-by (the value fields of the other position channel and its secondary, plus the end of a bin), and the
**order of the layers** as `SortField`s: the fields of the `order` channel (each with its own `sort`, ascending
by default) when the encoding has one, otherwise the stack-by fields (see `StackRules` in
_AggregateTransforms_) **descending when the stacked channel is `y` and ascending otherwise**. The decision
itself is `StackRules.Decide(markType, orient, encoding)`.

**`NeedsRaw`** is true when a channel (other than `x2` and `y2`) sorts its categories by an object or a channel
shorthand (not `ascending`, `descending`, null, or an array of values) or has `scale.domain` of `unaggregated`:
only then does a scale read the table as it was before the aggregate.

**`EncodingData`** `(Raw, Main, Bins, Evaluation)` is the result of the execution: `Raw` is the table after the
explicit transforms and the implicit bins, before the aggregate; `Main` is the table after the aggregate and the
stack, which the marks and every other scale read; `Bins` maps the literal start name of a bin to its `BinSpec`
(a field without a valid value has none); `Evaluation` is the evaluation context of the call, which the scales
charge their work to. `Raw` and `Main` are the **same instance** when the encoding neither aggregates nor
stacks, or when no scale needs `Raw`. `EncodingData.Identity(table, evaluation)` is the data of an identity plan.

**Invariants**: the plan reads no data; two calls with the same view and configuration give equal plans; nothing
is read from the clock, the environment, or the current culture.

#### Key Methods

- `EncodingPlan.Create(view, config)` - requires a non-null view and configuration; reads the mark (a string or
  the `type` of a definition), infers the orientation, plans every definition (`PlanDefinition`: the aggregate,
  the bin, the fields, the title), shares the bins by start name, applies `StackRules` and plans the stack
  (marking the stacked channel: its `FieldPath` becomes the end and its `Field2Path` the start), links each `x`
  and `y` to its secondary channel (`LinkRanges`: not for a stacked channel and not for a bin the encoding
  computes), then plans the aggregate and decides `NeedsRaw`. A `bin` is planned on `x` and `y` only, only for a
  definition that has a field and does not aggregate; the other channels keep reporting `vl-encoding-bin`. A
  nominal or ordinal binned channel reads the `_range` field and has no second field.
- `EncodingPlan.BinStartName(bin, field, defaultMaxBins)` - the literal name rule above.
- `EncodingTransforms.Apply(plan, table, context)` (static) - requires non-null arguments; returns
  `EncodingData.Identity` for an identity plan; otherwise runs the stages in **Vega-Lite's order: explicit
  transforms (done by the caller), bin, Raw, aggregate, stack, Main**:
  1. **Bin** (`BinFields`): for every distinct bin the extent of the finite numeric values of the field (or the
     `extent` parameter) goes to `BinAlgorithm.Resolve` with the `MaxScaleDomainSize` limit; then one pass
     writes the start, the end, and, for a discrete channel, the range text (the two boundaries formatted and
     joined with an en dash, formatted once per distinct bin, and the text `null` for a row without a valid
     value) with one `TableLayout`.
  2. **Aggregate**: one call of the grouping engine of _AggregateTransforms_ with the plan's group-by and
     measures, `releaseInput: false` (the rows stay counted while `Raw` is in use), seeded from the pointer of
     the first aggregating definition; the winning rows of `argmax` and `argmin` measures are copied into their
     scalar fields (`ReadArgFields`); when no scale needs `Raw`, its rows are released and `Raw` becomes `Main`.
  3. **Stack**: the stack engine with the plan's field, group-by, layer order, and offset; the rows keep their
     place.
  4. **Main**. The invalid-row filter of Vega-Lite's last stage is done by the marks, which skip a row whose
     position is not a number.
- `EncodingTransforms.ApplyStandalone(plan, table, view, sourceName, limits, token)` - builds a private context
  (evaluator, loader with an empty row budget, no parameters) for callers that have only a view and a table;
  used by the convenience entry of `ScaleResolver` and by tests.

**Row accounting.** The bin stage replaces its input by a table of the same size (no change). The aggregate
reserves one row per group as the group appears (the input stays counted, so the peak of the input and the
groups is bounded) and releases the input rows only when `Raw` is not needed. A stack replaces its input (no
change), except that **a stack next to a kept `Raw` table without an aggregate reserves its own rows**
(`ReserveDerivedRows`), because both tables are alive. The work of every row is charged to the evaluation-step
budget by the engines and the cancellation token is polled every 1,024 rows.

**Preconditions and postconditions**: the view was validated by `SpecParser` and `table` holds the rows after
the explicit transforms; on return `Main` holds the fields the plan names, the loader's row count equals the
rows of the tables that are alive, and the explicit-transform tables that are no longer used have been released.

#### Error Handling

The plan never fails for a valid specification: a construct it cannot plan (a bin on another channel, a measure
without a field, an operation that is not an aggregate) is skipped and is the feature catalog's to report. The
execution reports everything that depends on the data as a `VegaLiteRenderException` located at the construct
that asked for it - the `bin` value (a bin count above `MaxScaleDomainSize`, an extent that cannot be binned, a
field limit), the first aggregating definition (a group, cell, step, or string limit), or the stacked
definition - with the limit named in the message, as _AggregateTransforms_ does; an `ExpressionRuntimeException`
of the engines is converted with the same message. A canceled token is an `OperationCanceledException`, never
wrapped. A null argument is an `ArgumentNullException`. The unit performs no I/O and produces no warning.

**Differences from Vega-Lite**: `bin` is planned on `x` and `y` only (the other channels need the binned scale
types of a later phase); a count is titled by `countTitle` in every `fieldTitle` mode; the name of an aggregate
keeps the operation as written (`average_x`); and a non-finite stack value is silently invalid, as in
Vega-Lite.

#### Dependencies

- **`AggregateTransforms`**, **`BinAlgorithm`** and `BinSettings`, **`StackRules`**, **`AggregateMeasures`** and
  `AggregateSpec`, **`WorkMeter`**, and **`TransformContext`** - the engines and records of
  _AggregateTransforms_ and the shared context; **`TableLayout`** and **`FieldReader`** of _RowTransforms_
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
`ScaleResolver` reads the plan for every channel (field, second field, title, bin kind, stacked or not, measure,
raw field) and the data (`Main` for domains, `Raw` for a sort by an aggregate and for `unaggregated`, `Bins` for
a binned domain, `Evaluation` for the work charge); its results (`ResolvedScale.FieldPath`, `Field2Path`,
`Title`, `Bin`, `BinSpec`, and `Stack`) are what `MarkBuilder` and `AxisBuilder` consume, and the marks draw the
`Main` table. `FeatureCatalog` does not use the plan: it shares `StackRules` and `ScaleResolver.InferOrient`
with it, so that the report (`order` accepted only for an active stack) and the plan decide alike. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/EncodingPlanTests.cs` and `EncodingTransformsTests.cs`
call the unit directly through `InternalsVisibleTo`.
