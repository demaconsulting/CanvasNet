### RowTransforms Unit Verification Design

This document describes the unit-level verification strategy for the `RowTransforms` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal static `RowTransforms` class (`Apply`, with its
predicate, shape, and lookup files) that executes the row transforms of the drawn view — `calculate`, `filter`,
`fold`, `flatten`, `sample`, `pivot`, and `lookup` — eagerly over an immutable `DataTable` and returns a new table; and
the internal types of its files: `RowSampler` (the deterministic sample), `TableLayout` and `FieldReader` (the field
layout of a derived table, its cell bound, and the rebuilding of arrays and objects from the flattened fields),
`ParameterEnvironment` and `SpecValues` (the evaluation of the static parameters of the root view and the conversion of
a JSON value to a `DataValue`), and `TransformContext` and `RowExpressions` (the per-call context that carries the tree,
the loader, the evaluator, the parameter values, the limits, the token, and the warnings). The transforms of later
phases (`aggregate`, `window`, `bin`, `stack`, `impute`, `timeUnit`, and the others) are skipped by the executor and are
reported by the feature catalog.

#### Verification Approach

The unit is verified by parsing small, original specification strings with the real `SpecParser`, loading the data
with the real `DataLoader`, populating the parameters with `ParameterEnvironment.Populate`, applying the transforms the
way a render does (the `Run` helper of the test class), and asserting on the resulting table: the fields in ordinal
order, the rows as text, the cell kinds, the warnings, and the exceptions with their location. The unit's collaborators
(`SpecParser` and `FeatureCatalog` for the input and the reports, `DataLoader` and `DataTable` for the source and the
result, `ExpressionParser` and `ExpressionEvaluator` for the expressions, and `ScaleResolver` for the sort scenarios) are
real; the unit's contract is exactly how a parsed transform turns one table into another, and no mocking or stubbing is
used. File sources are real: `DataTempDirectory` (a per-test temporary tree for the base directory of a lookup in a
file). The internal types are reached through the test assembly's `InternalsVisibleTo` access.

The expected tables are derived by hand from the rules of each transform:

- `calculate` of `datum.a * 10` over the rows `a = 1, 3, 5` adds the field `c` with `10, 30, 50`; a second calculate
  of the same name replaces it (`datum.c + 1` gives `11, 31, 51`); a field is replaced together with the fields below
  it (`p.q` and `p.r` vanish when `p` becomes the number 3); an `as` name is literal, so `a.b` is one field named
  `a\.b`; and a result that is an array or an object is stored as one cell.
- `fold` of two rows over two fields has four rows in input-row order with the key and the value (`p|u|1`, `p|v|2`,
  `q|u|3`, `q|v|4`); the folded fields stay; a field that a row lacks folds to null; a single name in `as` renames only
  the key.
- `flatten` of `tags` over `["a","b"]`, `[]`, `"c"`, `null`, and a missing field gives the rows `1|a`, `1|b`, `3|c`: an
  array expands, a scalar is one element, and an empty, null, or missing value gives no row; several fields expand in
  parallel and the shorter ones are padded with null.
- `pivot` of the rows `(a,x,1)`, `(a,y,2)`, `(b,x,3)`, `(a,x,4)` by `k`, value `v`, grouped by `g` gives the rows
  `a|5|2` and `b|3|0`: the groups are in first-seen order, the columns are the distinct pivot texts in ordinal order,
  `sum` adds, a missing combination is 0, and `count` counts rows.
- `lookup` of the ids `1, 2, 3` in the rows `(k 1, n one)`, `(k 2, n two)`, `(k 1, n uno)` gives `one`, `two`, and the
  default for 3: the first match wins and the key is compared by its text, so the string `"2"` finds the number 2.
- `sample` is deterministic: the seed is the FNV-1a 64-bit hash of the UTF-8 JSON pointer of the transform entry (the
  hash of the empty text is the offset basis 14695981039346656037, and of `a` it is 12638187200555641996), the generator
  is SplitMix64 (its first output from state 0 is 16294208416658607535), and the reservoir (algorithm R, with
  `slot = draw % (i + 1)`) over ten rows with a size of four, seeded by `/transform/0`, holds the rows 2, 3, 6, and 9 in
  source order — the vector was computed by hand from those definitions and is asserted step by step (the draws for the
  indexes 4 to 9 and their slots 0, 0, 1, 0, 4, and 0), and the same size at `/transform/1` gives 3, 5, 8, and 9.
- The row budget is counted by eye: a fold of three rows over two fields needs 3 + 6 = 9 rows at its peak, so
  `MaxDataRows = 9` passes and 8 fails with the message "fold transform output has 6 rows … with the 3 rows read
  before"; a flatten of two rows of three elements needs 2 + 6 = 8, so 8 passes and 7 fails; and a filter that drops
  one of the two rows gives it back, so a limit of 4 is enough for the flatten after it (1 row + 3 rows).
- A sort of the categories `a` (weights 2 and 3), `b` (10), and `c` (1) by `sum` of `w` is `c, a, b` (1, 5, 10) and
  descending is `b, a, c`; by `count` it is `b, c, a` (the counts 1, 1, 2 are ordered ascending with ties in first
  appearance order — `b` before `c` — and then `a`); by `max`, `mean`, `average`, and `median` of the same weights it is
  `c, a, b` or `b, a, c` as derived in the test comment.

The parameter scenarios use the same table-free approach: `c = b * 2`, `b = a + 1`, `a = 2` written in the opposite
order evaluate to 6, 3, and 2 and give `6y` for `c + list[1]` with `list = ["x", "y"]`; the caller's `a = 10` gives 22
and `11`; a chain of 2,000 parameters, each defined by the one declared after it, evaluates to 1999, which proves that
the dependency order is computed once for the whole list.

Unit tests reside in `RowTransformsTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Transforms` folder. The same file holds the tests of the sampler, of the parameter environment, and of the sort of a
discrete scale over transformed rows, because the sort needs the transformed table that this unit produces; the
scale resolver's own unit design lists the sort scenarios as well.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: Per-test temporary directories (deleted by the test) for the file lookups; nothing is fetched
- **Isolation**: Each test parses its own specification text and builds its own loader, evaluator, and context. The
  cancellation scenario runs the transforms under a token that is already canceled over 5,000 rows, enough to reach a
  check point; the concurrency scenario samples from eight parallel iterations

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong row, row order, field, field order, or cell kind or value; a different sample for the same
pointer and data; a row budget that is off by one or a derived table that is allocated before its size is checked; a
file read outside the sandbox or more than once per call; a failure without the location of the transform or
expression; an exception of the wrong type; or a warning that is missing, repeated, or at the wrong pointer
constitutes a failure.

#### Unit-Level Test Scenarios

##### Calculate Adds And Replaces Fields

**Tests**: `RowTransformsTests.RowTransforms_Apply_CalculateAddsAndReplacesFields`,
`RowTransformsTests.RowTransforms_Apply_CalculateStoresCompositeValuesAndLiteralNames`,
`RowTransformsTests.RowTransforms_Apply_CalculateOfARegularExpressionWarnsAndStoresNull`

Asserts the derived fields and rows above: a new field, a replaced field, the unchanged other fields and row order, an
array and an object stored as one cell each, a dotted output name that is one literal field, a nested object that is
dropped when its name is reused for a number, and that a regular expression result (which has no cell form) is stored as
null with one warning `vl-expr-runtime` of kind warning at `/transform/0/calculate` that counts the rows (3) and names
the first row index (0).

##### Filter By Expression And By Predicate

**Tests**: `RowTransformsTests.RowTransforms_Apply_FilterExpressionKeepsTruthyRows`,
`RowTransformsTests.RowTransforms_Apply_FilterPredicatesAndCompositions`,
`RowTransformsTests.RowTransforms_Apply_FilterOnAParameterReadsItsTruthiness`

Asserts that an expression filter keeps the rows whose result is truthy (`datum.b` drops the row whose `b` is null;
`datum.a > 1 && datum.a < 5` keeps the row with `a = 3`), and, as a theory of nineteen predicates over the rows
`x = 1, 2, 3, null`, every operator with the comparison rules of JavaScript — `equal` is strict (`2` matches and `"2"`
does not), `lt` and `lte` keep the null row because null converts to zero, `range` is inclusive with a `null` bound
meaning open and reversed bounds swapped, `oneOf`, `valid` true and false, and the compositions `and`, `or` (with an
expression string as a member), and `not`; that a `timeUnit` in a predicate is ignored and a date object as the value
to compare with matches every row (both are reported by the catalog); that a filter on a variable parameter keeps every
row or none depending on its truthiness (`true` keeps 3 rows, an override of `false` or `0` keeps none) while a filter
on a selection parameter, by `param` or by the legacy `selection`, keeps every row.

##### Filter Predicates Are Charged To The Step Budget

**Tests**: `RowTransformsTests.RowTransforms_Apply_FilterLongStringComparisonsAreChargedToTheStepBudget`,
`RowTransformsTests.RowTransforms_Apply_FilterSmallPredicatesStayWithinASmallStepBudget`,
`RowTransformsTests.RowTransforms_Apply_FilterOneOfAndCompositionsAreChargedPerRow`,
`RowTransformsTests.RowTransforms_Apply_FilterCompositeFieldReadsAreCharged`,
`RowTransformsTests.RowTransforms_Apply_FilterOfAnArrayFieldWithLongTextIsALocatedFailure`

The step counts are derived by hand from the rule of one step per 64 characters or members. As a theory over `lt`, `lte`,
`gt`, `gte`, `range`, `equal`, and `oneOf`, a 10,000-character value compared with an equal one costs 156 steps (10,000
divided by 64, rounded down) on top of the step of the field test: the filter keeps the expected rows (0 for `lt` and
`gt`, 1 for the others) under a budget of 1,000, and fails under a budget of 100 at `/transform/0/filter` with the text
`VegaLiteLimits.MaxEvaluationSteps`. A composed filter of three small predicates over four rows passes under a budget of
100. A `oneOf` of 10,000 members costs 156 steps for one row, and 40 `valid` parts of an `and` (or 40 failing parts of an
`or`) over five rows cost 200 steps: each passes under a budget of 10,000 and fails, located at the filter, under 100. A
field `tags` of 300 elements is stored as 300 fields, so rebuilding it costs 1,200 units, 18 steps: a budget of 100
passes and a budget of 10 fails at the filter. Last, an array of three 10-character texts (32 characters as text) compared
with `lt: 5` under a `MaxStringLength` of 25 fails at `/transform/1/filter` with the text
`the limit of 25 characters (VegaLiteLimits.MaxStringLength)` instead of an unlocated internal exception.

##### Parameters Are Evaluated In Dependency Order

**Tests**: `RowTransformsTests.RowTransforms_Apply_ParametersAreEvaluatedInDependencyOrderAndOverridden`,
`RowTransformsTests.RowTransforms_Apply_LongParameterChainsAreOrderedByDependency`,
`RowTransformsTests.RowTransforms_Apply_ParameterErrorsAreLocatedSpecificationErrors`,
`RowTransformsTests.RowTransforms_Apply_ParameterTextLongerThanMaxStringLength_IsLocatedRenderError`,
`RowTransformsTests.RowTransforms_Apply_StaticParameterTextLongerThanRenderLimit_IsLocatedRenderError`,
`RowTransformsTests.RowTransforms_Apply_CalculateOfLongTextFieldIsLocatedRenderError`

Asserts the derived values above (`6y`, `2`, and `3`, the overridden `22y` and `11`, and an override of an expression
parameter that replaces the expression: `b = 100` gives `c = 200` and `"q"[1]` is null, which adds as zero); that the
chain of 2,000 parameters gives 1999; and that a cycle (located at `/params/0/expr`, the lowest declared parameter on
the cycle, and naming "cycle"), a self reference (`/params/1/expr`), a reference to an unknown name (located at the
expression with the name `'zzz'` and "expression offset 4"), a repeated name (`/params/1/name`), a reserved name
(`/params/0/name`), and an unknown name in a transform expression (`/transform/0/calculate`) are located
`VegaLiteSpecException` errors whose message starts with the file name. It also asserts that, with a `MaxStringLength`
of 20, a caller override of 20 characters is kept by `calculate` while one of 21 is a `VegaLiteRenderException` located
at `/params/0` that names the parameter and `the limit of 20 characters (VegaLiteLimits.MaxStringLength)`; that a
`value` text or a text inside a composite `value` that is over a lower render limit is refused the same way; and that a
long text of the data that an expression copies with `calculate` is a failure located at `/transform/0/calculate`.

##### Fold And Flatten Reshape The Rows

**Tests**: `RowTransformsTests.RowTransforms_Apply_FoldProducesKeyAndValueRows`,
`RowTransformsTests.RowTransforms_Apply_FlattenExpandsArraysAndScalars`,
`RowTransformsTests.RowTransforms_Apply_FlattenExpandsSeveralFieldsInParallel`

Asserts the derived rows above: the default names `key` and `value` and the given names, the key-only `as`, a missing
field folded to null, the fields `id, key, u, v, value` of a fold, the replacement of the array fields `tags.0` and
`tags.1` by the field `tags` after a flatten, and the parallel expansion `1|10|x`, `1|20|null`, `1|30|null`, `2|null|y`,
`2|null|z` of two array fields with the output names `n` and `s` (five rows).

##### Sample Is Deterministic

**Tests**: `RowTransformsTests.RowSampler_Select_MatchesTheHandComputedReservoir`,
`RowTransformsTests.RowTransforms_Apply_SampleUsesThePointerAsSeed`,
`RowTransformsTests.RowTransforms_Apply_SampleIsIdenticalWhenRepeatedAndConcurrent`

Asserts the hand-computed hashes, generator output, draws, slots, and selections above (`[2, 3, 6, 9]` at
`/transform/0`, `[3, 5, 8, 9]` at `/transform/1`, and `[0, 6, 11, 13, 16]` for twenty rows and a size of five at
`/transform/2`); that through a specification the same size gives rows 2, 3, 6, 9 at the first position and 3, 5, 8, 9
at the second, a size of at least the number of rows is a copy in source order, and a size of zero is empty; and that
a sample of 100 of 2,000 rows is identical when repeated and from eight parallel runs, has 100 distinct rows, and keeps
them in source order.

##### Pivot Builds Columns

**Tests**: `RowTransformsTests.RowTransforms_Apply_PivotSumsValuesPerGroup`,
`RowTransformsTests.RowTransforms_Apply_PivotCountsAndFallsBackToSum`,
`RowTransformsTests.RowTransforms_Apply_PivotLimitAndNullValuesAreWarnings`,
`RowTransformsTests.RowTransforms_Apply_PivotColumnsAreBoundedByTheFieldLimit`,
`RowTransformsTests.RowTransforms_Apply_PivotArrayKeyTextIsBoundedByMaxStringLength`

Asserts the derived rows above and the fields `g, x, y`; that `count` without a group gives one row `3|1` and an
unsupported `mean` falls back to the sum; that a `limit` of 1 keeps the first pivot value in ordinal order and gives a
warning `vl-transform-runtime` at `/transform/0` ("only the first 1 in ordinal order") and that a row whose pivot value
is null is skipped with a second warning naming the count (1 row) and the first row index (4); and that three pivot
columns plus a group field over a `MaxFieldCount` of three are a located failure at `/transform/0` naming the limit; and
that an array pivot value is the column named by its JavaScript text (`aaaaaa,bbbbbb`, 13 characters) within the default
limit, while under a `MaxStringLength` of 12 it is a located failure at `/transform/0` naming the field and the limit.

##### Lookup Copies Fields Of The First Match

**Tests**: `RowTransformsTests.RowTransforms_Apply_LookupCopiesFieldsOfTheFirstMatch`,
`RowTransformsTests.RowTransforms_Apply_LookupDefaultNamesTextKeysAndWholeRows`,
`RowTransformsTests.RowTransforms_Apply_LookupInNamedDatasetsAndParamSourceIsSkipped`,
`RowTransformsTests.RowTransforms_Apply_LookupInFilesUsesTheSandboxAndTheFileCache`,
`RowTransformsTests.RowTransforms_Apply_LookupSourceSharesTheRowBudget`

Asserts the derived rows above, that the later matching row (`uno`, 99) is never used, that a null key and a key with no
match get the default (`?`), that the output names default to the looked-up field names and go to `as` when given,
that the text `"2"` finds the number 2, that a lookup without `fields` stores the whole matching row as one object in
the field `as` (`data` by default) and null without a match, that a lookup in a named dataset of the specification
works, that a lookup in the data of a parameter (`from.param`) is skipped and leaves the table unchanged, that two
lookups of one file read it once and need a file limit of only one, that `../outside.csv` is refused by the sandbox as
"outside the allowed directories" at `/transform/0/from/data/url`, and that a source of three rows beside three main
rows needs `MaxDataRows` 6 (5 fails at `/transform/0/from/data/values`).

##### Lookup Shares Rebuilt Composites, Calculated Shared Arrays Are Bounded

**Tests**: `RowTransformsTests.RowTransforms_Apply_LookupSharesRebuiltCompositesAmongOutputRows`,
`RowTransformsTests.RowTransforms_Apply_CalculateOfSharedArraysIsBoundedByNodesAndStringLimit`

Asserts that 3,000 output rows matching a secondary row with a 500-element array and an object hold the same array,
object, and whole-row instance for each copied field (reference identity) while a field copied twice has a separate
value per output field; and that a chain of 40 calculates of `[datum.aN, datum.aN]` completes, while a following
`'' + datum.a40` or `toString(datum.a40)` fails at `/transform/40/calculate` naming `MaxStringLength`.

##### Derived Tables Are Bounded Before They Are Allocated

**Tests**: `RowTransformsTests.RowTransforms_Apply_FoldOutputIsBoundedByTheRowLimit`,
`RowTransformsTests.RowTransforms_Apply_FlattenOutputIsBoundedByTheRowLimitAndFiltersReleaseRows`,
`RowTransformsTests.RowTransforms_Apply_FieldStringAndStepLimitsAreLocatedFailures`

Asserts the row budget examples above with the failure located at `/transform/0` and naming the transform and the
rows read before; that a second added field over a `MaxFieldCount` of three fails at `/transform/1` naming the limit; that
a calculated text of 30 characters over a `MaxStringLength` of 24 and a calculation of five instructions over three rows
(fifteen steps) under a `MaxEvaluationSteps` of ten fail at `/transform/0/calculate` with the expression offset; and
that the layout of a table
of more cells than the internal bound of 2^25 is refused before it is allocated, with a message that names the cells.

##### Cancellation, Arguments, And Transforms Of Later Phases

**Tests**: `RowTransformsTests.RowTransforms_Apply_CanceledTokenStopsTheTransforms`,
`RowTransformsTests.RowTransforms_Apply_ChecksArgumentsAndReturnsTheInputWithoutTransforms`,
`RowTransformsTests.RowTransforms_Apply_TransformsOfLaterPhasesAreSkipped`

Asserts that the transforms throw `OperationCanceledException` under a canceled token (and give 4,999 rows of the 5,000
under a live one: the filter keeps `a + 1 > 1`); that a table without transforms is returned as the same instance, that
a null table or context is an `ArgumentNullException`; and that an `aggregate` among the transforms is skipped (only the
later calculate ran: the fields are `a, b, z` and the three rows are intact).

##### Sort By A Field Of The Transformed Rows

**Tests**: `RowTransformsTests.ScaleResolver_Resolve_SortByFieldUsesTheTransformedRows`,
`RowTransformsTests.ScaleResolver_Resolve_SortByFieldIsStableForTies`

Asserts, as a theory of ten sort forms over a table made by a calculate transform (the weights of the categories
`a`, `b`, and `c` above), that the domain follows `sum`, `count`, `max`, `min` (descending), `mean`, `average`
(descending), and `median`, that the order option reverses it, that an operation outside the supported seven
(`valid`) and the `encoding` form keep the ascending default, and, in the second test, that categories with equal
aggregates keep their order of first appearance in both directions (`z, m, a` ascending and descending, so the data
order and not the alphabet decides among ties), and that `IsSupportedFieldSort` accepts an object with an operation of
the supported seven and refuses an invalid order and the shorthand text form.
