## SpecModel Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `SpecModel` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.SpecModel`), containing five units: `SpecParser` (together with the node, tree,
and metadata types of its files), `FeatureCatalog`, `ConfigResolver`, `SpecNormalizer`, and `ExpressionReferenceFolder`.

### Verification Approach

The `SpecModel` subsystem is verified at two tiers. The unit tests (see _SpecParser Unit Verification Design_,
_FeatureCatalog Unit Verification Design_, _ConfigResolver Unit Verification Design_, _SpecNormalizer Unit
Verification Design_, and _ExpressionReferenceFolder Unit Verification Design_ under `spec-model/`) verify
each unit in isolation, including its internal types. The subsystem-level scenarios verify the five units working
together: the parser's immutable tree, with its JSON pointers and one-based line and column positions, is the input
of the feature catalog (which reports the recognized-but-unsupported constructs at those locations), of the
configuration resolver (which merges the specification's `config` with the Vega-Lite defaults and the caller's
configuration), and of the normalizer (which reduces the views, layers and compositions, to the tree of unit views that
are drawn and expands the composite marks,
over the original nodes). The units' tests already run the real parser to build their input, and the document-level
tests in
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
scenario in the five unit verification documents passes without error or unexpected exception.

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
  `FeatureCatalogTests.FeatureCatalog_Visit_ConcatFacetAndRepeatAreDrawnAndNestedViewsExamined` — hostile
  nesting and an oversized source fail in the parser with a typed exception before the catalog or the resolver run, and
  for a specification that parses, the catalog reports nothing for a concatenation, a facet or a repeat (all are drawn)
  and examines the views nested in every composition, with their full pointers.
- **Expressions, Transforms, And Parameters Fail Fast At Open, Unsupported Parts Are Reported**:
  `SpecParserTests.SpecParser_Parse_ExpressionErrorsCarryTheLocationOfTheTextAndTheOffset`,
  `SpecParserTests.SpecParser_Parse_CompilesTheExpressionsOfSupportedSitesOnly`,
  `SpecParserTests.SpecParser_Parse_Phase5TransformsAndParametersAreAccepted`,
  `FeatureCatalogTests.FeatureCatalog_Visit_SupportedPhase5TransformsAndParametersAreNotReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedExpressionConstructsAreReportedAtTheExpression`,
  `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedPartsOfSupportedTransformsAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_NestedViewTransformsAreAppliedAndVariableParametersAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_TransformsExpressionsAndParametersAreReported`,
  `FeatureCatalogTests.FeatureCatalog_Visit_ExpressionReferencesAreReportedOnlyWhereTheyAreNotEvaluated`,
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder` — the
  parser compiles each expression of a supported site once and hands the compiled expressions to the stages that need
  them, so the errors of an expression (a syntax error, a length or nesting over the limit, an unknown parameter, a
  parameter cycle) and the wrong shape of a supported transform are `VegaLiteSpecException` errors at the offending
  value when the document is opened, with the position of the string and the offset inside it, while the catalog reads
  the same compiled expressions and reports what they list but the engine does not run (`vl-expr-function-<name>`,
  `vl-expr-view-state`, `vl-expr-construct-<name>`) at the expression text, the parts of the supported transforms that
  are not computed (`-lookup-param`; the former `-pivot-op` is retired in Phase 13 because a pivot computes every
  aggregate operation, and the former `-filter-timeunit` and `-filter-datetime` are retired because time predicates are
  evaluated, and `-filter-param` is retired in Phase 12 because a filter on a selection is evaluated), the transforms of
  nested views, and a supported specification of Phase 5
  constructs has no report; the parse warnings and the reports are delivered together in document order.
- **Aggregating Transforms And Encoding Properties Are Validated And Reported By What Is Computed**:
  `SpecParserAggregatesTests.SpecParser_Parse_AggregateTransformShapesArePinned`,
  `SpecParserAggregatesTests.SpecParser_Parse_EncodingAggregateBinStackSortShapesArePinned`,
  `SpecParserAggregatesTests.SpecParser_Parse_ValidAggregatingTransformsAreAccepted`,
  `FeatureCatalogTests.FeatureCatalog_Visit_Phase6FeaturesAreNotReportedAndRemainingPartsAre`,
  `FeatureCatalogTests.FeatureCatalog_Visit_NestedViewAggregatingTransformsAreAppliedInConcatAndFacet`,
  `FeatureCatalogTests.FeatureCatalog_Visit_OrderChannelIsAcceptedForEveryMark`,
  `FeatureCatalogTests.FeatureCatalog_Visit_SecondaryPositionChannelsAreAcceptedForRanges`,
  `FeatureCatalogTests.FeatureCatalog_Visit_StackOnANonLinearScaleIsReported`,
  `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder` — the
  parser validates the shape of the `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, and `impute` transforms and
  of the `aggregate`, `bin`, `stack`, and `sort` properties of a channel, so a wrong operation name, field, `as`, or
  parameter is a located `VegaLiteSpecException` when the document is opened, while the catalog no longer reports the
  features that the renderer computes for the drawn view (the aggregating transforms, the encoding aggregate, bin,
  stack,
  and sort, secondary position channels of a range, and the `order` channel, which every mark accepts since Phase 9: it
  orders a stack, the points of a path, or the drawing) and still reports the transforms of
  nested views, the remaining parts (a bin extent that is a parameter, a `bin: "binned"` on a channel other than `x`
  and `y`, a stack
  on a non-linear scale (not applied: Vega-Lite does not stack on a log, pow, sqrt, or symlog scale), and a secondary
  channel that a mark cannot draw); the statistical transforms are computed since Phase 13, so the document-level
  test now uses a `projection` as its example of an unsupported construct.
- **Path, Text, And Arc Marks Are Drawn, Validated At Open, And Reported Only For What They Do Not Draw**:
  `SpecParserMarkTests.SpecParser_Parse_RejectsABadInterpolateOrInvalidOrPointValue`,
  `SpecParserMarkTests.SpecParser_Parse_ValidatesTextMarkProperties`,
  `SpecParserImputeTests.SpecParser_Parse_RejectsABadImputeObjectWithItsLocation`,
  `FeatureCatalogTests.FeatureCatalog_Report_LineAreaTrailAreSupportedAndGeoshapeImageAreNot`,
  `FeatureCatalogTests.FeatureCatalog_Report_ArcIsSupportedAndPolarChannelsAreGated` — the parser checks the properties
  of the line, area, trail, text, and arc marks and the `impute` object of a position definition when the document is
  opened (a name that is not one of the fifteen interpolations, a text `point` other than `transparent`, a `text` of the
  wrong kind, a `keyvals` list of more values than the scale-domain limit, and a `method` that is not one of the five
  are
  `VegaLiteSpecException` errors at the offending member), and the catalog, which reads the same tree, reports nothing
  for the five marks that are drawn (with every property that they read) while the marks that stay unsupported
  (`geoshape` and `image`) are reported at the mark, a channel that a mark does not draw (`theta` on a line) is reported
  at the channel and ignored, and the invalid data modes, the bundle interpolation of an area, and the impute of a
  stacked path mark over a binned dimension are reported at their values.
- **Unreadable `config.scale.invalid` Entries Are Reported, And Ranged Offsets And Clip Heights Are Not (Phase 14)**:
  `FeatureCatalogLabelExprAndOffsetTests.FeatureCatalog_Visit_UnreadableScaleInvalidEntriesAreReported`,
  `FeatureCatalogLabelExprAndOffsetTests.FeatureCatalog_Visit_OffsetsAreReportedOnlyWhenTheyAreNotDrawn`,
  `FeatureCatalogTests.FeatureCatalog_Visit_LegendsAreDrawnAndOnlyTheirUnsupportedPropertiesAreReported`,
  `VegaLitePhase14SystemTests.CanvasNetVegaLite_SystemIntegration_ScaleInvalid_AnUnreadableEntryIsReported`,
  `VegaLitePhase14SystemTests.CanvasNetVegaLite_SystemIntegration_Offset_ATemporalOffsetOfABarIsStillReported` — the five
  entries of `config.scale.invalid` that are neither `"zero-or-min"` nor an object with a number or text `value` (an
  expression reference is reported by `vl-expr-ref` instead) are
  reported once each, at the entry, as `vl-scale-invalid-value`; a quantitative offset of a bar over a nominal position
  (a ranged offset) is not reported while a temporal one is `vl-offset-ranged`; and a `legend.clipHeight` is not reported
  (`vl-guide-legend-clipheight` is retired) while `legend.encode` still is `vl-guide-encode`.
- **Time Constructs Are Validated At Open, Written As Expressions, And Reported Only Where They Are Not Applied**:
  `SpecParserTimeTests.SpecParser_TimeUnit_AcceptsEveryName`,
  `SpecParserTimeTests.SpecParser_TimeUnit_UnknownUnitIsLocated`,
  `SpecParserTimeTests.SpecParser_DateTimeObject_InDomainPredicateAndValues_Validated`,
  `SpecParserTimeTests.SpecParser_TickCountInterval_Validated`,
  `SpecParserTimeTests.SpecParser_TimeFormat_SpecifierLimitsAreSpecificationErrors`,
  `SpecParserTimeTests.SpecParser_TimePredicates_AreCompiledOnceUnderThePredicate`,
  `SpecParserTimeTests.SpecParser_TimePredicates_OneOfIsBoundedByTheDomainSizeAndTheGeneratedBudget`,
  `SpecParserTimeTests.SpecParser_TimePredicates_LongFieldFailsFastWithTheGeneratedBudgetError`,
  `SpecParserConditionTests.SpecParser_Conditions_LargeOneOfAndOrPredicatesAreNotBoundByTheUserExpressionLength`,
  `SpecParserConditionTests.SpecParser_Conditions_LongFieldTimesLargeOneOfFailsFastWithTheGeneratedBudgetError`,
  `SpecParserConditionTests.SpecParser_PredicateExpressionOperands_AreBoundByTheUserExpressionLength`,
  `PredicateSourceTimeTests.PredicateSource_TimeUnit_Equal_Month`,
  `PredicateSourceTimeTests.PredicateSource_WeekPart_IsOmittedAndWarned`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_TemporalFieldsAndTimeUnitsAreSupported`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_BinnedTimeUnitIsNotReported`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_MaxBinsTimeUnitIsNotReported`,
  `PredicateSourceTimeTests.PredicateSource_BinnedUnitComparesTheInstantOfTheField`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_WeekAndDayOfYearPartsOfPredicatesAreWarnings`,
  `FeatureCatalogTimeTests.FeatureCatalog_Visit_TimeIdsAreDeliveredAndStrictModeThrows`,
  `FeatureCatalogTests.FeatureCatalog_Visit_CustomDateFormatIsSupportedAndReportsNothing`,
  `FeatureCatalogTests.FeatureCatalog_Visit_TimeUnitAndDateTestsAreSupported` — the parser checks the time constructs
  with the rules of the `Data` subsystem (an unknown time unit, a date-time object with an unknown key, a month,
  quarter,
  or day that is not one, a `tickCount` or `nice` interval that is not one of the eight, a time format of more than
  1,024
  characters or 4,096 pieces, and a `date:` or `utc:` parse
  entry without a quoted format are `VegaLiteSpecException` errors at the offending member), compiles each time
  predicate once into an expression written with the date and time functions (so it is evaluated in the calendar of the
  chart; the text that is written for a predicate or a condition is bound by `MaxScaleDomainSize` values and eight times
  `MaxSourceLength`, not by the 4,096 characters of a user-written expression, so a membership list of a thousand values
  compiles), and the catalog, which reads the same units with `TimeUnitSpec`, reports nothing for a supported
  specification that uses temporal fields, time units, time and UTC scales, date-time objects, a custom date format, and
  the `timeUnit` transform, and for a binned unit and the `maxbins` form of a predicate (applied since Phase 14),
  while the parts that are not applied are reported at the exact value: a band position of a rect or bar, a date-time
  `datum`, intervals on a scale that has no time scale, a
  `zero` on a time scale, and the scale types that a temporal field cannot use; the
  `week` and
  `dayofyear` parts of a predicate (`vl-predicate-timeunit-part`) are a warning that strict mode delivers without
  stopping. The identifiers of the earlier phases
  that said time was unsupported (`vl-type-temporal`, `vl-encoding-timeunit`, `vl-transform-timeunit` for a drawn view,
  `vl-transform-filter-timeunit`, `vl-transform-filter-datetime`, `vl-encoding-condition-test`,
  `vl-data-parse-date-format`, `vl-scale-time`, and `vl-scale-utc`) are never produced, and the end-to-end render of a
  time chart through the public API is verified at the system tier.
- **Layers And Compositions Are Reduced To Unit Views, Validated At Open, And Reported By What Is Not Drawn**:
  `SpecNormalizerTests.SpecNormalizer_Layers_LayersAreUnitsInDocumentOrder`,
  `SpecNormalizerTests.SpecNormalizer_Layers_EncodingIsMergedInPlace`,
  `SpecNormalizerCompositionTests.SpecNormalizer_Layers_HConcatBuildsConcatNodeWithChildrenInOrder`,
  `SpecNormalizerCompositionTests.SpecNormalizer_Layers_UnitWithRowColumnChannelsBecomesFacetWithBody`,
  `SpecNormalizerRepeatTests.SpecNormalizer_Layers_ArrayRepeatMakesConcatWithColumns`,
  `SpecParserResolveTests.SpecParser_Parse_ValidResolveIsAccepted`,
  `SpecParserResolveTests.SpecParser_Parse_MalformedResolveReportsThePointer`,
  `SpecParserResolveTests.SpecParser_Parse_ExpressionsOfComposedViewsAreCompiled`,
  `SpecParserCompositionTests.SpecParser_Parse_FacetWithoutSpecIsError`,
  `SpecParserCompositionTests.SpecParser_Parse_RepeatRefIsAcceptedAsField`,
  `FeatureCatalogLayerTests.FeatureCatalog_Visit_TopLevelTransformOfLayerSpecIsNotReported`,
  `FeatureCatalogLayerTests.FeatureCatalog_Visit_TransformOfComposedViewIsAppliedAndNotReported`,
  `FeatureCatalogLayerTests.FeatureCatalog_Visit_TransformOfFacetViewIsAppliedAndNotReported`,
  `FeatureCatalogLayerTests.FeatureCatalog_Visit_InheritedEncodingIsVisitedPerLayerAndReportedOnce`,
  `FeatureCatalogLayerTests.FeatureCatalog_Visit_LayerPropertiesOfTheTopLevelViewAreWarnedAboutInALayer`,
  `FeatureCatalogCompositionTests.FeatureCatalog_Visit_CompositionPropertiesIgnoredAreWarnedAbout` — the parser
  validates the `resolve` of a view, and the properties of the compositions (a `facet` and `repeat` need a `spec`, the
  shapes of `columns`, `spacing`, `align`, `center`, and `header`, the repeat references), when the document is opened
  (a
  located `VegaLiteSpecException` at the offending member), and compiles the expressions of every view, those of a
  concatenation, facet, or repeat included; the normalizer, which reads the same tree, reduces the views to the tree of
  unit views in document order with the encoding that each inherits, makes a concatenation, a facet (also from the
  `row`, `column`, and `facet` channels of a unit view), and the copies of a `repeat` nodes of the tree with their
  layout
  settings and resolve defaults, and bounds their number; the catalog, which reads the same tree and the same layer
  tree,
  no longer reports a `layer`, a `concat`, `hconcat`, `vconcat`, a `facet`, or a `repeat`, or the transform of a view
  that the renderer draws (every view), reports a `row`, `column`, or `facet` channel only where the unit cannot be
  faceted and the parts of a facet that are not drawn (`vl-header-orient`; the `bin`, `aggregate`, and `timeUnit` of a
  facet field are drawn since Phase 14),
  visits the encoding that a layer or the chart of a concatenation inherits once for each layer with that layer's mark
  (reporting a construct once, at the place where it is written), and warns about the properties that a layer or a
  composition ignores.
- **Composite Marks Are Expanded And Reported By What The Expansion Does Not Apply**:
  `SpecParserResolveTests.SpecParser_Parse_ValidCompositeMarkIsAccepted`,
  `SpecParserResolveTests.SpecParser_Parse_MalformedCompositeMarkReportsThePointer`,
  `SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_TukeyBoxPlotIsOutlierWhiskerAndBoxViews`,
  `SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarDefaultIsTheRuleOfTheMeanAndStandardError`,
  `SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBandTwoDimensionalIsAnAreaWithLineBorders`,
  `FeatureCatalogCompositeTests.FeatureCatalog_Visit_BoxPlotIsSupportedWithItsChannels`,
  `FeatureCatalogCompositeTests.FeatureCatalog_Visit_BinOrTimeUnitOrAggregateOfAGroupingChannelIsNotReported`,
  `SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_GroupingChannelsAddBinTimeUnitAndAggregateTransforms`,
  `FeatureCatalogCompositeTests.FeatureCatalog_Visit_ErrorChannelsOfOtherMarksAreReported`,
  `FeatureCatalogCompositeTests.ConfigResolver_Resolve_CarriesTheBoxPlotDefaults` — the parser checks the extent, the
  center, and the parts of a composite mark; the configuration carries the defaults of the three marks; the normalizer
  expands
  each mark into the layers Vega-Lite normalizes it to, with the transforms and the properties of the parts, using the
  same orientation rule as the catalog; and the catalog reports nothing for a composite mark that is drawn (with the
  channels that it uses) and for the bin, time unit, or aggregate of a grouping channel (the expansion applies them as
  Vega-Lite's `extractTransformsFromEncoding` does), while reporting the
  error
  channels of a mark that does not use them.
- **Expression References Of The Static Sites Are Compiled At Open, Folded Once, And Reported Only Where They Are Not
  Evaluated**:
  `SpecParserTests.SpecParser_Parse_CompilesTheExpressionsOfSupportedSitesOnly`,
  `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_ReplacesAReferenceByItsValue`,
  `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_FoldsTheConfiguration`,
  `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_KeepsUntouchedSubtreesAndLocatesTheValues`,
  `FeatureCatalogTests.FeatureCatalog_Visit_ExpressionReferencesAreReportedOnlyWhereTheyAreNotEvaluated`,
  `FeatureCatalogTests.FeatureCatalog_VisitCallerConfig_ReportsEveryExpressionReferenceAtItsPointer`,
  `VegaLiteParamsIntegrationTests.CanvasNetVegaLite_SystemIntegration_Params_ExpressionReferencesDriveTitleBackgroundAndPadding`
  — the parser compiles the `{"expr": ...}` references of the title, the padding, the mark properties that no row reads,
  and the channel values at open (a syntax error or an undeclared parameter is a `VegaLiteSpecException` at the text)
  and lists them as static references in document order, without the row-bound ones; the folder, which reads that list
  and
  the parameters of the call, replaces each reference by the value of its expression in a copy of the tree whose
  untouched subtrees are the original nodes, so that the configuration resolver and the later stages read the value;
  and the catalog, which reads the same list, reports an expression reference only where the schema allows none or where
  a static site reads `datum`, scans the `config` of the top-level view as well, and reports the references of the
  `Config` text of the caller's render options, which nothing evaluates. The
  end-to-end effect on the title, the background, and the padding of a rendered chart is verified through the public
  API.
