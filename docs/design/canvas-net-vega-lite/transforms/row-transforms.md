### RowTransforms

![Transforms Structure](VegaLiteTransformsView.svg)

The `RowTransforms` unit is the only software unit in the `Transforms` subsystem. It comprises nine source files in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace, all `internal`:

- `RowTransforms.cs` - `RowTransforms` (sealed partial class): the `Apply` entry point, the run loop, `calculate`, `filter`
  (the row selection), and the warning helpers
- `RowTransforms.Predicates.cs` - the predicates of `filter`
- `RowTransforms.Shape.cs` - `fold`, `flatten`, `sample`, and `pivot`
- `RowTransforms.Lookup.cs` - `lookup`
- `RowSampler.cs` - the deterministic reservoir sampler
- `TableLayout.cs` - `TableLayout` (the field layout and size bounds of a derived table) and `FieldReader` (the reader that
  rebuilds arrays and objects from flattened fields)
- `ParameterEnvironment.cs` - `ParameterEnvironment` (the static parameter values) and `SpecValues` (JSON value to
  `DataValue`)
- `TransformContext.cs` - `TransformContext` and `RowExpressions`
- `NamespaceDoc.cs` - the namespace documentation of the subsystem

#### Purpose

`RowTransforms` produces the rows a chart draws from the rows it loaded: it applies the supported row transforms of the
`transform` array in order and computes the values of the static variable parameters. It exists
so that the semantics of each
transform (what a row, a missing value, a repeated key, or an empty array means), the
accounting of the row budget, and the
determinism of `sample` are defined in one place, separate from the loader that reads the data and from the scales that
consume the result.

The unit's behavior is specified by the `CanvasNetVegaLite-Transforms-RowTransforms-*` requirements in
`docs/reqstream/canvas-net-vega-lite/transforms/row-transforms.yaml`.

#### Data Model

An instance of `RowTransforms` serves **one call on one thread**: it holds the `TransformContext` (the `SpecTree` with its
compiled expressions, the `DataLoader` of the call, the `ExpressionEvaluator`, the parameter values, the limits, the
cancellation token, and the list that receives the warnings). Tables are immutable `DataTable` values; a derived table is
built with `DataTable.FromSortedCells` over a `TableLayout`.

**Table layout.** A table keeps its fields in ordinal order, so a layout fixes where every surviving input field and every
added field goes. An added field replaces an input field of the same name **and the fields nested below it** (adding `tags`
removes `tags.0` and `tags.1`, as a property of a row object is replaced as a whole). The names an `as` property gives are
**literal** (a dot is part of the name, escaped in the canonical field name), and `flatten` without `as` uses the canonical
path of the field. The field count is checked against `MaxFieldCount` and the cell count against 2^25 before anything is
allocated. `FieldReader` reads a field of a row: the cell of that exact name when it holds a value, otherwise an array
rebuilt from `name.0`, `name.1`, ... (trailing nulls are dropped because a missing element and a null one are the same
cell; a null cell whose elements are all null stays null) or an object rebuilt from the fields below, otherwise null. The
rebuilding is the `FlatRowNode` tree of the data unit, which the expression evaluator shares for `datum`.

**The transforms** (in the order of the `transform` array; an entry that names a transform of a later phase is skipped):

- **`calculate`** - `calculate` (the expression) and `as` (a non-empty text). The expression is evaluated once per row with
  `datum` bound to the row; the result of any kind (scalar, array, object) is stored in `as`, replacing a field of that
  name. A regular expression result cannot be stored: the cell is null and one `vl-expr-runtime` warning per transform
  names the count and the first row index.
- **`filter`** - an expression text (the row is kept when the result is truthy, by JavaScript truthiness) or a predicate:
  `equal` (strict equality), `lt`, `lte`, `gt`, `gte` (JavaScript relational comparison, so `null < 3` is true), `range`
  (an array of two values, inclusive; a null bound is open; reversed bounds are swapped),
  `oneOf` (strict equality with any
  member), and `valid` (`true` keeps the rows whose value is neither null nor NaN, `false` the others), each on a `field`;
  composed with `and`, `or`, and `not` to the nesting depth the parser accepts; and `param`, the truthiness of a variable
  parameter (every row passes or none). A predicate on a selection parameter or a `selection` keeps every row; a `timeUnit`
  of a predicate is ignored; a predicate that contains a date object is ignored (the row passes); all three are reported
  by the catalog.
- **`fold`** - an array of field names and an optional `as` of one or two names (`key` and `value` by default): every row
  becomes one row per folded field, in the order of the rows and then of the fields, with the field name as written in
  `as[0]` and the cell in `as[1]` (null for a field the table lacks). The folded fields stay in the rows.
- **`flatten`** - an array of field names and an optional `as` with at most as many names: every row becomes as many rows
  as its longest array; the arrays of several fields expand **in parallel** (shorter ones padded with null), a scalar counts
  as an array of one value, null, a missing field, and an empty array as none, and a row whose fields are all empty
  produces no row. The element goes to `as[i]` (default the field itself).
- **`sample`** - a number of rows to keep (a non-negative number). See _RowSampler_ below.
- **`pivot`** - the pivot field, `value`, optional `groupby`, `limit`, and `op`. Rows are grouped by the `groupby`
  values in first-seen order; the columns are the distinct pivot values as text (the JavaScript text of
  `ExpressionEvaluator.ToJsString`, bounded by `MaxStringLength`: a longer text, such as that of an array value, is a
  located failure before the column is created) in ordinal order, at most `limit` of them (the first ones) when given. The
  `limit` is applied **before** the field bound: while the rows are read, only the smallest `limit` distinct texts are kept
  (a larger one is counted as cut and dropped), so the working set never exceeds `limit` texts, and `MaxFieldCount` is
  enforced on the columns that would be output (the group fields plus the kept texts). A pivot with many distinct values
  and a small `limit` therefore succeeds with exactly `limit` columns; a located failure is raised only when the kept
  columns would exceed the bound, that is for a pivot without a `limit` (the safety net), or with a `limit` above the
  bound, and more distinct values than the bound allows. A cell is the sum of the valid quantitative `value`s of the group
  (booleans and numeric text count as numbers) for `op`
  `sum` (the default) or the number of rows for `count`; a combination without rows is 0. The result has only the group
  fields and the pivot columns. Rows whose pivot value is null are skipped, a `limit` that cuts columns off, and a pivot
  value that equals a group field name each give one `vl-transform-runtime` warning. An `op` other than `sum` and
  `count` is reported (`vl-transform-pivot-op`) and sum is used.
- **`lookup`** - the field to look up, and `from` with `data` (a data object loaded by the **loader of the call**: inline
  values, a named dataset, a local file, or a sequence), `key`, and optional `fields`; plus `as` and `default`. The secondary
  rows are indexed by the **text** of the key (so `1` matches `"1"`); null and composite keys never match; for a repeated
  key **the first row wins** (a deliberate deviation from Vega, which keeps the last). With
  `fields`, field `i` is copied to
  `as[i]` (default the canonical name of the field); without `fields`, the whole matching row goes into the one field `as`
  (`data` by default) as an object. An array or object that a field copies is rebuilt from the flattened fields of the
  secondary row **once per secondary row and copied field** and shared (composites are immutable) by every output row
  that matches that row, so the memory of the copied values is bounded by the secondary rows and fields and not by the
  output rows. A row without a match gets `default` (a JSON scalar; null by default). `from.param`
  (the data of a selection) is not applied and is reported (`vl-transform-lookup-param`).

**`RowSampler`.** `RowSampler.Select(count, size, pointer, token)` is a pure function of the
number of rows, the size, and the
JSON pointer of the transform entry (for example `/transform/0`): the seed is the 64-bit
FNV-1a hash of the UTF-8 bytes of
the pointer (offset basis `0xCBF29CE484222325`, prime `0x100000001B3`), the generator is SplitMix64, and the algorithm is
reservoir sampling (algorithm R): the first `size` indexes fill the reservoir, and for every later index `i` the next
output modulo `i + 1` is the slot to replace when it is below `size`. The selected indexes are **sorted ascending**, which
restores the source order. A size of at least the row count returns every row (the same table), and zero returns none. Two
sample transforms of one chart therefore draw independent samples, and a chart edited elsewhere keeps the sample of an
unchanged transform. A hand-computed vector (ten rows, size four, pointer `/transform/0`, rows
2, 3, 6, and 9) is pinned by a
test.

**`ParameterEnvironment`.** `Populate` evaluates the variable parameters of the root view into the dictionary the
evaluator reads: the order is the topological order of `ParameterGraph.Sort` (dependencies first, otherwise the
declaration order, so the result does not depend on hashing); for each parameter the caller's override wins and is used
as given (converted by `DataLoader.FromObject`; the expression is not evaluated), then the expression, then the `value`
(converted by `SpecValues`, so an array or an object becomes an immutable composite value), else null. A text of an
override or of a `value` (also inside a composite) that is longer than the `MaxStringLength` of the call is a located
`VegaLiteRenderException` at the parameter, because no function has measured it; the result of an expression is checked
by the evaluator. A selection parameter is skipped and an override of its name is accepted and ignored. `bind` and
`empty` are ignored.

**`RowExpressions`** is the pair of the compiled expressions of the tree and the evaluator that `MarkBuilder` uses for the
row-bound paint values; `TransformContext` is the bundle of everything one run shares.

**Invariants**: the output of a transform is a new table within `MaxDataRows`, `MaxFieldCount`, and 2^25 cells; the rows
counted in the loader's budget are the rows of the tables that are alive; nothing is read from
the clock, the environment, or
the current culture.

#### Key Methods

- `RowTransforms.Apply(input, transforms, context)` - requires a non-null input and context; returns the input itself when
  `transforms` is null, not an array, or empty; otherwise runs the entries in order: it checks the token, finds the operation
  property of the entry (`SpecParser.FindTransformOperation`), skips entries of later phases
  (`SpecParser.IsSupportedTransform`), and dispatches to the transform. The expressions were
  compiled when the document was
  opened, so no text is parsed here.
- `Calculate`, `Filter` (with `BuildPredicate`, the predicate classes, and `SelectRows`, which builds the table of the kept
  rows and releases the dropped ones), `Fold`, `Flatten`, `Sample`, `Pivot`, and `Lookup` - one method per transform; each
  reserves or releases rows in the loader's budget as described above, checks the token every 1,024 rows, and builds its
  output with a `TableLayout`.
- `RowSampler.Select`, `RowSampler.Seed` (the FNV-1a hash), and `RowSampler.Next` (SplitMix64) - the sampler.
- `ParameterEnvironment.Populate` and `SpecValues.ToDataValue` - as above.

**Preconditions and postconditions**: the specification was validated by `SpecParser` (transform shapes, predicates,
parameter names, no cycles) and its expressions were compiled; on return the table is complete and within the limits, the
loader's row count equals the rows of the returned table (plus any table that is still referenced), and the warnings
list holds the findings of this run, or an exception was thrown.

#### Error Handling

Everything that depends on the data and exceeds a limit is a `VegaLiteRenderException` located at the construct that caused
it: `MaxDataRows` (checked **before** the output is allocated, also for a lookup source; located at the transform entry),
`MaxFieldCount` and the 2^25 cell bound (the transform entry), `MaxStringLength` and the evaluation step budget (the expression
string), a failing regular expression (the expression string), and a lookup source that the sandbox or the file layer refuses
(the `from.data` node, with the loader's message). A canceled token is an `OperationCanceledException`, never wrapped, and
the warnings of a canceled call are not delivered. Recoverable findings are warnings: `vl-expr-runtime` and
`vl-transform-runtime`, located at the expression string or the transform entry. A malformed transform is not an error of
this unit: `SpecParser` rejected it when the document was opened. A null argument is an `ArgumentNullException`. The unit
performs no I/O itself; a lookup source is read only through the `DataLoader`.

#### Dependencies

- **`DataLoader`** - the row budget (`ReserveDerivedRows`, `ReleaseRows`), the loading of a
  lookup source (`Load`), and the
  conversion of a caller value (`FromObject`)
- **`DataTable`**, **`DataValue`**, **`DataObject`**, and **`FieldPath`** - the value and table
  model of the `DataTable` unit
- **`ExpressionEvaluator`** and **`CompiledExpression`** - the evaluation of expressions and the shared coercion helpers
  (`IsTruthy`, `StrictEquals`, `RelationalLess`)
- **`SpecTree`**, **`SpecNode`**, **`SpecParser`** (`FindTransformOperation`,
  `IsSupportedTransform`, `ParameterGraph`), and
  **`FeatureReport`** - the tree, the helpers, and the report record of the `SpecModel` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the `VegaLiteDocument`
  unit
- **.NET base class library** - `System.Globalization` (invariant-culture text) and `System.Text` (the UTF-8 bytes of the
  pointer)

#### Callers

`VegaLiteDocument` creates the `TransformContext`, calls `ParameterEnvironment.Populate`, then
`RowTransforms.Apply` once per
`Measure` or `Render` call for a single view with a supported mark, and delivers the transform warnings after the loader
warnings; `MarkBuilder` uses `RowExpressions`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Transforms/RowTransformsTests.cs` call `Apply`, `Populate`, and `RowSampler`
directly through `InternalsVisibleTo`.
