## Transforms Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Transforms` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Transforms`). In this release the subsystem contains a single unit, `RowTransforms`,
which executes the row transforms of the drawn view (`calculate`, `filter`, `fold`, `flatten`, `sample`, `pivot`, and
`lookup`) over an immutable `DataTable`, evaluates the static parameters of the root view, and samples
deterministically; the transforms of later phases (`aggregate`, `joinaggregate`, `window`, `bin`, `stack`, `impute`,
`timeUnit`, `regression`, `loess`, `density`, `quantile`, and `extent`) are added to this subsystem by later releases.

### Verification Approach

The `Transforms` subsystem has one unit, so its unit verification (see the _RowTransforms Unit Verification Design_
under `transforms/`) is complete for the subsystem's own behavior and there are no unit-to-unit interactions inside
the subsystem to verify. The subsystem-level scenarios verify the unit working with the units that feed it and consume
its output: the real `SpecParser` compiles the expressions of the transforms and the parameters and validates the shape
of every transform at the time the document is opened; the real `DataLoader` supplies the source table, the sources of
the lookups, the file cache, the sandbox, and the one row budget of the call (the transforms reserve and release rows
through it); the real `ExpressionEvaluator` runs the expressions under the one step budget of the call; the real
`ScaleResolver`, `MarkBuilder`, `ViewLayout`, and `ScenePainter` use the transformed table, so the scenarios assert on
the pixels of a chart whose rows were filtered, calculated, reshaped, looked up, sampled, or sorted; and the real
`FeatureCatalog` reports the parts that are not supported. The system-level scenarios use charts of 100 by 100 pixels
without padding, axes, and title, in which the position of every mark is derived by hand from a scale domain of 0 to 10,
so a dot is found by one pixel.

No mocking or stubbing is used. The subsystem is internal, so its tests reach it through the test assembly's
`InternalsVisibleTo` access. The subsystem reads files only through the sandbox of the data loader and never reads the
network.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: Per-test temporary directories (created and deleted by the test) for the lookups in files and for the
  sandbox scenario; every other specification is an in-memory string; no network
- **Isolation**: Each test parses its own specification and renders it with its own options. The determinism scenario
  renders one document from twelve concurrent tasks and compares each result with a single-threaded baseline

### Acceptance Criteria

The `Transforms` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the unit verification document passes without error or unexpected exception. Any row that is kept, dropped,
reordered, or computed differently from the hand-derived expectation, a different sample for the same data and
pointer, a pixel outside its analytic position, a limit that does not fail closed with the location of the transform,
a read outside the sandbox, a measured size that differs from the rendered size, or a render that is not byte-identical
to the baseline constitutes a failure.

### Test Scenarios

- **Calculate And Filter Decide Which Dots Are Drawn**:
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_FilterAndCalculateSelectTheDrawnDots`,
  `RowTransformsTests.RowTransforms_Apply_CalculateAddsAndReplacesFields`,
  `RowTransformsTests.RowTransforms_Apply_FilterExpressionKeepsTruthyRows` — a calculated field `y = 2 x` is scaled and
  the filter `4 < y < 10` leaves exactly the dots of x = 3 and 4 at (30, 40) and (40, 20) while the positions of the
  dropped rows are the white background.
- **Fold And Flatten Reshape The Rows**:
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_FoldAndFlattenReshapeTheRows`,
  `RowTransformsTests.RowTransforms_Apply_FoldProducesKeyAndValueRows`,
  `RowTransformsTests.RowTransforms_Apply_FlattenExpandsArraysAndScalars` — a fold of two fields makes the rows `(a, 2)`
  and `(b, 6)` with a point scale of two categories at 25 and 75 pixels, and a flatten of two arrays makes the three
  dots `(p, 1)`, `(p, 3)`, and `(q, 5)`, each other position being blank.
- **Pivot And Lookup Build The Scaled Fields**:
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_PivotAndLookupBuildTheScaledFields`,
  `RowTransformsTests.RowTransforms_Apply_PivotSumsValuesPerGroup`,
  `RowTransformsTests.RowTransforms_Apply_LookupCopiesFieldsOfTheFirstMatch` — a pivot gives the columns `s` and `t` per
  group with 0 for the missing combination and its dots at (20, 60) and (60, 100), and a lookup copies the field of the
  first matching row of a second inline source (the later row for the same key is not used), giving the dots at
  (20, 20) and (50, 70).
- **Sample And Sort Are Deterministic**:
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_SampleAndSortAreDeterministic`,
  `RowTransformsTests.RowSampler_Select_MatchesTheHandComputedReservoir`,
  `RowTransformsTests.RowTransforms_Apply_SampleIsIdenticalWhenRepeatedAndConcurrent`,
  `RowTransformsTests.ScaleResolver_Resolve_SortByFieldUsesTheTransformedRows` — a sample of four of ten rows on the
  diagonal draws exactly the rows 2, 3, 6, and 9 (the hand-computed reservoir for the pointer `/transform/0`) on two
  renders with identical pixels, and a discrete channel sorted by the `sum` of a field puts the categories with sums 1,
  5, and 10 in that order from left to right, and in the reverse order when descending.
- **Parameters: Defaults, Overrides, And Filter By Parameter**:
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_DefaultsOverridesAndFilterParam`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_OverridesReachTheTransforms`,
  `RowTransformsTests.RowTransforms_Apply_ParametersAreEvaluatedInDependencyOrderAndOverridden`,
  `RowTransformsTests.RowTransforms_Apply_FilterOnAParameterReadsItsTruthiness` — a parameter that is an expression of
  another gives its default (only x = 5 passes `x > cut + 1` with `cut` 3), a caller's value for `cut` moves the filter
  (x = 3, 4, and 5), a caller's value for the expression parameter `limit` replaces the expression, a `false` value of a
  parameter that a `filter` names hides every dot, the parameter names are listed in declaration order, naming a
  selection parameter changes nothing, and a name that the document does not declare is refused with an
  `ArgumentException`.
- **Invalid Parameter Definitions And Transforms Fail When The Document Is Opened**:
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_InvalidDefinitionsFailWhenTheDocumentIsOpened`,
  `RowTransformsTests.RowTransforms_Apply_ParameterErrorsAreLocatedSpecificationErrors`,
  `SpecParserTests.SpecParser_Parse_Phase5TransformsAndParametersAreAccepted`,
  `SpecParserTests.ParameterGraph_Sort_OrdersDependenciesFirstThenDeclarationOrder` — a cycle, a reference to an unknown
  name, and a repeated name are `VegaLiteSpecException` errors located at `/params/0/expr`, `/params/0/expr`, and
  `/params/1/name` when the document is opened, parameters are ordered dependencies first and then by declaration, and
  the supported shapes of every Phase 5 transform and predicate are accepted without a warning.
- **Measure Equals Render And Renders Are Byte-Identical**:
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_MeasureMatchesRenderAndRendersAreByteIdentical`
  — a chart through a flatten, a calculate with a parameter, a filter, a lookup, and a sample measures the size that it
  renders, renders the same bytes from twelve concurrent tasks as on the calling thread, and differs from the chart whose
  sample takes every row, so the sample is not a no-op.
- **Limits, Sandbox, And Cancellation Fail Closed**:
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_LimitsSandboxAndCancellationFailClosed`,
  `RowTransformsTests.RowTransforms_Apply_FoldOutputIsBoundedByTheRowLimit`,
  `RowTransformsTests.RowTransforms_Apply_FlattenOutputIsBoundedByTheRowLimitAndFiltersReleaseRows`,
  `RowTransformsTests.RowTransforms_Apply_LookupSourceSharesTheRowBudget`,
  `RowTransformsTests.RowTransforms_Apply_LookupInFilesUsesTheSandboxAndTheFileCache`,
  `RowTransformsTests.RowTransforms_Apply_CanceledTokenStopsTheTransforms`,
  `DataLoaderTests.DataLoader_ReserveDerivedRows_SharesTheRowBudgetOfTheCall`,
  `DataLoaderTests.DataLoader_Load_LookupSourcesShareTheSandboxCacheAndBudgetOfTheCall` — a fold of three rows by two
  fields renders with `MaxDataRows` 9 and fails with 8 at `/transform/0` naming the limit, a lookup of a file outside
  the folder of the opened specification (`../outside.csv`) fails at its `url` with "outside the allowed directories",
  a lookup source
  counts against the same row budget and file cache as the main data, and a canceled token makes measuring and
  rendering throw `OperationCanceledException` with no event delivered.
- **Unsupported Parts Are Reported And Never Executed**:
  `FeatureCatalogTests.FeatureCatalog_Visit_SupportedPhase5TransformsAndParametersAreNotReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_LaterPhaseTransformsConditionsAndSelectionsStayReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedPartsOfSupportedTransformsAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_NestedViewTransformsAndVariableParametersAreReported`,
  `RowTransformsTests.RowTransforms_Apply_TransformsOfLaterPhasesAreSkipped`,
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Deferred_FeaturesAreStillReported` — the transforms
  of later phases, the selection parameters, the conditional encodings, and the parts of the supported transforms that are
  not computed (a selection or a time unit in a filter, a pivot operation other than `sum` and `count`, a lookup in a
  parameter) are reported once each in document order, are skipped by the executor, and the chart is still drawn.
