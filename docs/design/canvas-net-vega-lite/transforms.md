## Transforms

![Transforms Structure](VegaLiteTransformsView.svg)

### Overview

The `Transforms` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Transforms`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Transforms/`) turns the rows a chart loads into the rows it draws. It runs the
**row transforms** of the specification (`calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot`, and `lookup`) and
the **aggregating transforms** (`aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute`), runs the
**implicit pipeline** that the encoding of the view asks for (its bins, its aggregate, and its stack), and computes the
**static parameter environment** that expressions read, after the `DataLoader` has produced its table and before any scale
exists. It is **internal**: its types are visible only to the `VegaLiteDocument` unit and the other subsystems and, through
`InternalsVisibleTo`, to the test assembly. It contains three units:

- **RowTransforms** - the internal `RowTransforms` (with its predicates, shape, and lookup parts), `RowSampler`,
  `TableLayout` and `FieldReader`, `ParameterEnvironment` and `SpecValues`, `TransformContext`, and `RowExpressions`; see
  _RowTransforms Unit Design_ (`transforms/row-transforms.md`)
- **AggregateTransforms** - the internal `AggregateTransforms` (with its aggregate, bin, window, stack, and impute parts),
  `AggregateMeasures` and `MeasureCell` (the operations), `BinAlgorithm`, `StackRules`, `WorkMeter`, and `StableSorter`;
  see
  _AggregateTransforms Unit Design_ (`transforms/aggregate-transforms.md`)
- **EncodingPlan** - the internal `EncodingPlan` (the pure plan of a view's bins, aggregate, stack, field names, and
  titles) and `EncodingTransforms` with `EncodingData` (its execution); see _EncodingPlan Unit Design_
  (`transforms/encoding-plan.md`)

The subsystem's namespace documentation is in `Transforms/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Transforms-*` requirements in `docs/reqstream/canvas-net-vega-lite/transforms.yaml`.

### Interfaces

**Exposed** (internal only): `RowTransforms.Apply(DataTable, SpecNode?, TransformContext)`, which returns the transformed
table (the input itself when nothing changes) and dispatches the aggregating transforms to
`AggregateTransforms.Apply(DataTable, SpecNode, SpecNode, TransformContext)`;
`ParameterEnvironment.Populate(SpecTree, overrides, values, evaluator)`, which
fills the dictionary of parameter values that the evaluator reads;
`RowSampler.Select(count, size, pointer, token)`; `EncodingPlan.Create(SpecNode, ResolvedConfig)`, which plans the implicit
pipeline of a view, and `EncodingTransforms.Apply(EncodingPlan, DataTable, TransformContext)`, which runs it and
returns the
`EncodingData` (the Raw and Main tables and the bins); the accumulators and engines that other subsystems reuse
(`MeasureCell`, `WorkMeter`, `BinAlgorithm`, `StackRules`, and the aggregate and stack engines of `AggregateTransforms`);
and the `TransformContext` and `RowExpressions` records that carry what the stages share.

**Consumed**: `DataTable`, `DataValue`, `DataLoader` (the row budget, the sandbox, and the
loading of a lookup source), and
`FieldPath` of the `Data` subsystem; `ExpressionEvaluator` and `CompiledExpression` of the `Expressions` subsystem;
`SpecTree`, `SpecNode`, `SpecParser` (the transform and parameter helpers), `ResolvedConfig`, and `FeatureReport` of the
`SpecModel` subsystem; `ScaleResolver.InferOrient` and `NumberFormat` of the `Scales` subsystem (the plan reuses the
orientation rule and formats the text of a bin); and the public `VegaLiteLimits`, `VegaLiteLocation`,
`VegaLiteRenderOptions` (the `Parameters` overrides), and `VegaLiteRenderException` types of the `VegaLiteDocument` unit.

**Consumers**: `VegaLiteDocument` (which builds the `TransformContext` once per `Measure` or `Render` call, populates the
parameters, applies the transforms to the loaded table, plans and runs the implicit pipeline, and delivers the transform
warnings after the loader warnings), the `Scales` subsystem (`ScaleResolver` reads the plan and the Raw and Main
tables, and
reuses `MeasureCell`, `WorkMeter`, and `BinSpec`), the `Marks` subsystem (`MarkBuilder` receives `RowExpressions` to
evaluate the row-bound paint values and draws the Main table), and the `SpecModel` subsystem (`SpecParser` and
`FeatureCatalog` use the operation registry of `AggregateMeasures` and `StackRules`).

### Design

**Between the loader and the scales.** The pipeline is `DataLoader` -> parameters -> explicit transforms in the written
order -> `EncodingPlan` -> implicit bins -> **Raw** -> implicit aggregate -> implicit stack -> **Main** -> scales -> marks.
Only the `transform` array of the drawn view (the root view without a composition) runs; the transforms of nested
views of a
composition are not drawn and are reported. Because the scales are resolved over the transformed tables, a domain and every
mark see the rows that the transforms produced, never the loaded ones; a sort of a discrete domain by an aggregate and a
`scale.domain` of `unaggregated` read the **Raw** table, as Vega-Lite does, and everything else reads **Main**.

**Eager and immutable.** Every transform reads an immutable `DataTable` and builds a new one; a transform that keeps every
row in order may return its input. Nothing in the pipeline ever sees a table change. A transform that the renderer does
not
compute (`timeUnit`, `regression`, `loess`, `density`, `quantile`, `extent`) is **skipped, not approximated**, and the
`FeatureCatalog` reports it.

**One row budget, shared with the loader.** The loader of the call is the single owner of the
`MaxDataRows` total. Fold and
flatten reserve their output rows (`ReserveDerivedRows`) before they allocate anything, while the input table still counts,
so the peak of an input and an output table is what is bounded; the rows of an input are released (`ReleaseRows`) once the
output is complete; filter, sample, and pivot give back the rows they drop; a lookup source is loaded through the same loader
instance, so it has the same sandbox, file cache, file and byte limits, and row total as the
main data. `aggregate` reserves one row per group as the group appears, `impute` reserves its result (the combinations of
groups and keys first) before it allocates anything, the transforms that keep every row (`joinaggregate`, `window`, `bin`,
and `stack`) change nothing, and the implicit aggregate keeps the Raw rows counted while a scale needs them (see
_AggregateTransforms_ and _EncodingPlan_ for the accounting of each). The field count of
every derived table is bounded by `MaxFieldCount` and its size by a bound of 2^25 cells (33,554,432), which keeps the memory
of one table to a few hundred megabytes whatever the other limits allow; the state of the groups and the values that an
operation keeps are counted in the same cells and checked up front. Strings are bounded by `MaxStringLength`. All the work
that is proportional to rows, to sorts, to window frames, and to the bootstrap of a confidence interval is charged to the
**evaluation-step budget** (`WorkMeter`, one step per 64 elements), so a hostile specification fails closed and located
instead of running for minutes.

**Parameters are static values.** A variable parameter of the drawn view has a `value` or an `expr` (the expression wins);
an expression is evaluated once per call, in the dependency order that the `SpecParser` fixed (cycles and unknown references
were rejected when the document was opened), and a value in `VegaLiteRenderOptions.Parameters` replaces the whole definition
of that parameter (the expression is not evaluated). The environment is a dictionary of `DataValue` by name that the
evaluator reads. Selection parameters have no value in a static chart and are reported.

**Warnings, not failures, for recoverable data.** A calculate that produces a regular
expression (which cannot be stored in a
field) stores null and warns `vl-expr-runtime`; a pivot that skips rows with a null pivot
value, cuts columns at `limit`, or
meets a pivot value that equals a group field name warns `vl-transform-runtime`. The aggregating transforms and the implicit
pipeline have no warnings: a value that cannot be used is null or NaN in the output and is invalid for the marks. The
warnings are collected in the order they are found and delivered after the loader's warnings, also when a later stage
fails. Everything that exceeds a limit is a located `VegaLiteRenderException` instead.

**Deterministic by construction.** The subsystem uses no clock, no random source, no culture, and no unordered iteration:
`sample` is seeded from the JSON pointer of its transform and so is the bootstrap of `ci0` and `ci1` (FNV-1a and
SplitMix64), the fields of a derived table are in ordinal order, and groups (structural keys), pivot columns, impute keys,
and lookup keys are ordered by first appearance or ordinal text; sorts are stable. The same
specification and data give the same rows
on every call, thread, machine, and platform.

**Cancellation.** The token is checked at the start of every transform, at least every 1,024 rows inside one (also inside
the sorts, the bootstrap, and the stages of the implicit pipeline), and by every expression evaluation;
cancellation is an
`OperationCanceledException`, never wrapped, and no report is delivered.
