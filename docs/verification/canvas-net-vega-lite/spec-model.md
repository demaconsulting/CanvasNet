## SpecModel Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `SpecModel` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.SpecModel`), containing three units: `SpecParser` (together with the node, tree,
and metadata types of its files), `FeatureCatalog`, and `ConfigResolver`.

### Verification Approach

The `SpecModel` subsystem is verified at two tiers. The unit tests (see _SpecParser Unit Verification Design_,
_FeatureCatalog Unit Verification Design_, and _ConfigResolver Unit Verification Design_ under `spec-model/`) verify
each unit in isolation, including its internal types. The subsystem-level scenarios verify the three units working
together: the parser's immutable tree, with its JSON pointers and one-based line and column positions, is the input
of both the feature catalog (which reports the recognized-but-unsupported constructs at those locations) and the
configuration resolver (which merges the specification's `config` with the Vega-Lite defaults and the caller's
configuration). The units' tests already run the real parser to build their input, and the document-level tests in
`VegaLiteDocumentSpecModelTests.cs` verify the subsystem's contract through the public `VegaLiteDocument`: that the
metadata is read from the tree, that a wrong value fails at its pointer, that parse warnings and catalog reports are
merged into one document-ordered, once-per-call delivery, and that rendering resolves the configuration.

No mocking or stubbing is used: the subsystem depends only on the core types `VegaLiteLocation`, `VegaLiteLimits`, and
`VegaLiteSpecException` of the root namespace, on the real `ExpressionParser` unit of the `Expressions` subsystem
(which compiles the expressions of the tree), on the time rules of the `Data` subsystem (`TimeUnitSpec`,
`DateTimeObject`, and `JsDateParser`, which validate the time units, the date-time objects, and the date texts), and
on the BCL `System.Text.Json` reader. The subsystem is internal, so
its tests reach it through the test assembly's `InternalsVisibleTo` access. Specifications in these tests are
original, small, and written for the test; no Vega-Lite example specification is used.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string or byte array
- **Isolation**: Each test parses its own sources

### Acceptance Criteria

The `SpecModel` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the three unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Parse Then Report: Locations Flow From The Parser To The Catalog**:
  `FeatureCatalogTests.FeatureCatalog_Visit_RecognizedUnsupportedPropertiesReturnsStableIds`,
  `FeatureCatalogTests.FeatureCatalog_Visit_MarkReportCarriesLineAndColumn`,
  `FeatureCatalogTests.FeatureCatalog_Visit_ReportsAreInDocumentOrder` — parses original specifications with the real
  parser and asserts that every report carries the stable identifier, the JSON pointer, and the one-based line and
  column of the offending value as the parser recorded them, in document order.
- **Parse Then Configure: The Specification's Configuration Feeds The Resolver**:
  `ConfigResolverTests.ConfigResolver_Resolve_VegaLiteV6DefaultsAndOverrides`,
  `ConfigResolverTests.ConfigResolver_Resolve_CallerConfigOverridesSpecConfig` — parses a specification with a
  `config` object and asserts the merged values: Vega-Lite defaults, then the specification's configuration, then the
  caller's.
- **Document Metadata And Reports Are Read From The Tree**:
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Open_SupportedChartHasMetadataAndNoReports`,
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder` —
  opens documents and asserts that the schema, title, marks, data references, and parameter names are read from the
  parser's tree, that a supported chart has no reports, and that parser warnings (duplicate and unknown keys) and
  catalog reports are merged and delivered in document order.
- **Wrong Values Fail At Their Pointer**:
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Parse_WrongTypeFailsWithPointerAndSourceName`,
  `SpecParserTests.SpecParser_Parse_FirstWrongValueInDocumentOrderIsReported` — a wrong value in a checked property
  fails when the document is opened, with the pointer and source name, and the first problem in the text is the one
  reported.
- **Strict Mode Uses The Merged Reports**:
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_StrictModeIgnoresWarningsAndThrowsAtTheFirstUnsupportedFeature`
  — warnings never stop rendering and the first unsupported feature in document order is thrown.
- **Configuration Reaches The Rendered Chart**:
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_ResolvesConfigurationFromSpecificationAndOptions` — the
  caller's configuration and the specification's configuration change the size and background of the rendered chart.
- **Large Inline Data Is Read In One Pass**:
  `VegaLiteDocumentSpecModelTests.SpecParser_Parse_LargeInlineDataIsReadInOnePass` — a fifty-thousand-row inline data
  set is parsed once into the tree with its pointers intact, no warnings, and the metadata reads the row count from
  the tree without visiting the rows again.
- **Hostile Input Fails Closed Across The Subsystem**:
  `SpecParserTests.SpecParser_Parse_DepthAndSourceLimitsFailClosed`,
  `FeatureCatalogTests.FeatureCatalog_Visit_CompositionsAreReportedAndNestedViewsExamined` — hostile nesting and an
  oversized source fail in the parser with a typed exception before the catalog or the resolver run, and for a
  specification that parses, the catalog reports every composition and examines the views nested in it, with their
  full pointers.
- **Expressions, Transforms, And Parameters Fail Fast At Open, Unsupported Parts Are Reported**:
  `SpecParserTests.SpecParser_Parse_ExpressionErrorsCarryTheLocationOfTheTextAndTheOffset`,
  `SpecParserTests.SpecParser_Parse_CompilesTheExpressionsOfSupportedSitesOnly`,
  `SpecParserTests.SpecParser_Parse_Phase5TransformsAndParametersAreAccepted`,
  `FeatureCatalogTests.FeatureCatalog_Visit_SupportedPhase5TransformsAndParametersAreNotReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedExpressionConstructsAreReportedAtTheExpression`,
  `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedPartsOfSupportedTransformsAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_NestedViewTransformsAndVariableParametersAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_TransformsExpressionsAndParametersAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_ExpressionReferencesAreReportedOnlyAtDeferredSites`,
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder` — the
  parser compiles each expression of a supported site once and hands the compiled expressions to the stages that need
  them, so the errors of an expression (a syntax error, a length or nesting over the limit, an unknown parameter, a
  parameter cycle) and the wrong shape of a supported transform are `VegaLiteSpecException` errors at the offending
  value when the document is opened, with the position of the string and the offset inside it, while the catalog reads
  the same compiled expressions and reports what they list but the engine does not run (`vl-expr-function-<name>`,
  `vl-expr-view-state`, `vl-expr-construct-<name>`) at the expression text, the parts of the supported transforms that
  are not computed (`vl-transform-filter-param`, `-pivot-op`, and
  `-lookup-param`; the former `-filter-timeunit` and `-filter-datetime` are retired because time predicates are
  evaluated), the transforms of nested views and later phases, and a supported specification of Phase 5
  constructs has no report; the parse warnings and the reports are delivered together in document order.
- **Aggregating Transforms And Encoding Properties Are Validated And Reported By What Is Computed**:
  `SpecParserAggregatesTests.SpecParser_Parse_AggregateTransformShapesArePinned`,
  `SpecParserAggregatesTests.SpecParser_Parse_EncodingAggregateBinStackSortShapesArePinned`,
  `SpecParserAggregatesTests.SpecParser_Parse_ValidAggregatingTransformsAreAccepted`,
  `FeatureCatalogTests.FeatureCatalog_Visit_Phase6FeaturesAreNotReportedAndRemainingPartsAre`,
  `FeatureCatalogTests.FeatureCatalog_Visit_NestedViewAggregatingTransformsAreStillReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_StackOrderChannelIsAcceptedOnlyForAStack`,
  `FeatureCatalogTests.FeatureCatalog_Visit_SecondaryPositionChannelsAreAcceptedForRanges`,
  `FeatureCatalogTests.FeatureCatalog_Visit_StackOnANonLinearScaleIsReported`,
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder` — the
  parser validates the shape of the `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` transforms and
  of the `aggregate`, `bin`, `stack`, and `sort` properties of a channel, so a wrong operation name, field, `as`, or
  parameter is a located `VegaLiteSpecException` when the document is opened, while the catalog no longer reports the
  features that the renderer computes for the drawn view (the aggregating transforms, the encoding aggregate, bin, stack,
  and sort, secondary position channels of a range, and the `order` of a stack) and still reports the transforms of
  nested views, the remaining parts (a bin extent that is a parameter, a `bin: "binned"` on a channel other than `x`
  and `y`, a stack
  on a non-linear scale (not applied: Vega-Lite does not stack on a log, pow, sqrt, or symlog scale), an `order`
  without a stack, and a secondary channel that a mark cannot draw), and the transforms
  of later phases; the document-level test now uses a `regression` transform as its example of an unsupported transform.
- **Time Constructs Are Validated At Open, Written As Expressions, And Reported Only Where They Are Not Applied**:
  `SpecParserTimeTests.SpecParser_TimeUnit_AcceptsEveryName`,
  `SpecParserTimeTests.SpecParser_TimeUnit_UnknownUnitIsLocated`,
  `SpecParserTimeTests.SpecParser_DateTimeObject_InDomainPredicateAndValues_Validated`,
  `SpecParserTimeTests.SpecParser_TickCountInterval_Validated`,
  `SpecParserTimeTests.SpecParser_TimeFormat_SpecifierLimitsAreSpecificationErrors`,
  `SpecParserTimeTests.SpecParser_TimePredicates_AreCompiledOnceUnderThePredicate`,
  `PredicateSourceTimeTests.PredicateSource_TimeUnit_Equal_Month`,
  `PredicateSourceTimeTests.PredicateSource_WeekPart_IsOmittedAndWarned`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_TemporalFieldsAndTimeUnitsAreSupported`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_BinnedTimeUnitIsReportedAtTheValue`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_WeekAndDayOfYearPartsOfPredicatesAreWarnings`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_TimeIdsAreDeliveredAndStrictModeThrows`,
  `FeatureCatalogTests.FeatureCatalog_Visit_CustomDateFormatIsSupportedAndReportsNothing`,
  `FeatureCatalogTests.FeatureCatalog_Visit_TimeUnitAndDateTestsAreSupported` — the parser checks the time constructs
  with the rules of the `Data` subsystem (an unknown time unit, a date-time object with an unknown key, a month, quarter,
  or day that is not one, a `tickCount` or `nice` interval that is not one of the eight, a time format of more than 1,024
  characters or 4,096 pieces, and a `date:` or `utc:` parse
  entry without a quoted format are `VegaLiteSpecException` errors at the offending member), compiles each time
  predicate once into an expression written with the date and time functions (so it is evaluated in the calendar of the
  chart), and the catalog, which reads the same units with `TimeUnitSpec`, reports nothing for a supported
  specification that uses temporal fields, time units, time and UTC scales, date-time objects, a custom date format, and
  the `timeUnit` transform, while the parts that are not applied are reported at the exact value: a binned unit
  (`vl-timeunit-binned`, an unsupported feature, so strict mode throws at the first), a `maxbins` unit in a predicate,
  the `week` and
  `dayofyear` parts of a predicate (`vl-predicate-timeunit-part`, a warning that strict mode delivers without
  stopping), a band position of a rect or bar, a date-time `datum`, intervals on a scale that has no time scale, a
  `zero` on a time scale, and the scale types that a temporal field cannot use. The identifiers of the earlier phases
  that said time was unsupported (`vl-type-temporal`, `vl-encoding-timeunit`, `vl-transform-timeunit` for a drawn view,
  `vl-transform-filter-timeunit`, `vl-transform-filter-datetime`, `vl-encoding-condition-test`,
  `vl-data-parse-date-format`, `vl-scale-time`, and `vl-scale-utc`) are never produced, and the end-to-end render of a
  time chart through the public API is verified at the system tier.
