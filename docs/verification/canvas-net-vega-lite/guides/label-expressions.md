### LabelExpressions Unit Verification Design

This document describes the unit-level verification strategy for the `LabelExpressions` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal sealed `LabelExpressions` class (the constructor,
`Datum`, `Resolve`, and `Evaluate`) with the `LabelText` record struct, which evaluates the `labelExpr` of axes, legends,
and facet headers on the shared expression evaluator of the call: the datum that each guide gives the expression, the
coercion of the result to label text like Vega's text mark, the compilation of configured text, and the limits of the
evaluation. The unit is also verified through its three callers, the axes of `AxisBuilder`, the legends of
`LegendBuilder`, and the facet headers of `VegaLiteDocument`, whose label-expression tests are named below; the
parser and the feature catalog behavior for `labelExpr` is verified in the _SpecParser_ and _FeatureCatalog Unit
Verification Designs_.

#### Verification Approach

The unit is verified in three parts. The unit itself is called directly (`LabelExpressionsTests`): `Datum` builds the
datum, `Resolve` compiles the text of a configured expression with the real `ExpressionParser`, and `Evaluate` runs it on
a real `ExpressionEvaluator` (the unit makes one of its own when it is given no `RowExpressions`; one test passes the
real expressions of a parsed chart so that a parameter and the step counter of the call are involved), and the result is
asserted as a `LabelText`. The axes and the legends are verified by parsing a small, original specification with the real
`SpecParser`, resolving the configuration, data, scales, and ticks with the real `ConfigResolver`, `DataLoader`,
`ScaleResolver`, and `Ticks` (through the shared helper `ChartTestHelpers`), building the guide with the real
`AxisBuilder` or `LegendBuilder` and the real `TextMeasurer`, and asserting on the label texts, the extents, and the
scene items (`AxisBuilderLabelExprTests`, `LegendBuilderLabelExprTests`). The facet headers and the whole path are
verified by laying out whole charts through `VegaLiteDocument` (`VegaLiteLabelExprIntegrationTests`) and, for strict
mode with no diagnostics, through the command line (the `RenderCommand` test named below). No dependency is mocked or
stubbed, and the internal types are reached through the test assembly's `InternalsVisibleTo` access. The expected values
are derived by hand:

- **The datum.** An axis with five ticks gives `datum.index` 0, 0.25, 0.5, 0.75, and 1 (`i / (n - 1)`) and
  `datum.tickIndex` 0 to 4; the third tick therefore reads `0.5/2` for `index/tickIndex`; a guide that has no `perc`
  gives null for it (a missing member reads as `null`, which the expression `datum.index + '/' + datum.perc` turns into
  the text `0.5/null`). A symbol legend gives the whole-number `index`; with a `symbolLimit` of 2 over five entries the
  extra `...3 entries` row has the index 2. A gradient legend gives `perc` along the bar and a discrete gradient also
  `perc2`, the end of the class.
- **Coercion.** `1e21` is the JavaScript text `1e+21`; `datum.value > 0` is `true`; a null result and a missing member
  are the empty text; an array `[' one ', null, 2]` is the lines `one`, ``, and `2`, which join as `one`, an empty line,
  and `2` with line feeds; the text `'  \u00A0a b\t '` is trimmed with JavaScript's white space (the no-break space
  included) to `a b`; a date is the ISO text `2024-03-05T00:00:00.000Z` in this engine, and `year(datum.value)` and
  `timeFormat(datum.value, '%b')` read it as a date (`2024/Mar`).
- **Limits.** A `MaxEvaluationSteps` of 5 with the eight-term sum `1 + 2 + ... + 8` is a render exception located at the
  expression that names `MaxEvaluationSteps`; a `MaxStringLength` of 8 with `datum.label + datum.label` over the label
  `12345` (ten characters) is a render exception that names `MaxStringLength`.
- **Axis effects.** `datum.label + '%'` makes every label of the y axis end with a percent sign and grows the axis
  extent; a conditional that blanks some labels removes them from the overlap removal, so the neighbors of a blank label
  stay; `labelLimit` truncates the replaced text, not the original.
- **Config precedence.** For a y axis: `config.axis` applies to every axis, `axisY` beats it, `axisQuantitative` beats
  both, and the `labelExpr` of the axis object beats all.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The Liberation fonts bundled with the core package, so every measured label width is independent of
  installed fonts
- **Files**: None; every specification is an in-memory string
- **Isolation**: Each test parses its own specification and builds its own guide. No test reads the clock, the host's
  zone, or the host's culture; the culture scenario runs the same chart under `tr-TR` and compares the labels with those
  of the invariant culture

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any label that differs from the hand-derived expectation, a datum member that is present when the guide does
not give it or missing when it does, an expression that is evaluated for an axis without labels, a replaced label that
is measured or thinned after the replacement instead of before, an expression that runs beyond the step or string limit
without a located exception, or a result that depends on the host's culture constitutes a failure.

#### Unit-Level Test Scenarios

##### The Datum Has The Members Of The Guide

**Tests**: `LabelExpressionsTests.LabelExpressions_Evaluate_ValueAndLabelAreBound`,
`LabelExpressionsTests.LabelExpressions_Evaluate_AxisIndexIsFractionAndTickIndexIsInteger`

Asserts that `datum.label + ':' + datum.value` over the value 20 and the label `twenty` is `twenty:20` with no lines;
and that for the third of five ticks (`index` 0.5, `tickIndex` 2) the datum reads `0.5/2` with the members a guide does
not give (`perc` and an unknown name) reading `null`.

##### The Result Is Coerced Like The Text Mark

**Tests**: `LabelExpressionsTests.LabelExpressions_Evaluate_NullAndMissingMemberGiveEmptyText`,
`LabelExpressionsTests.LabelExpressions_Evaluate_NumberBooleanAndDateAreCoercedLikeJavaScript`,
`LabelExpressionsTests.LabelExpressions_Evaluate_ArrayGivesLinesWithElementsTrimmed`,
`LabelExpressionsTests.LabelExpressions_Evaluate_ResultIsTrimmed`

Asserts that null and a missing member are the empty text; that `1e21`, a comparison, and a date are the texts `1e+21`,
`true`, and `2024-03-05T00:00:00.000Z` and that a date is still a date for the time functions (`2024/Mar`); that an array
gives the lines `one`, ``, `2` and the joined text with line feeds; and that the white space of JavaScript's `trim` is
removed from both ends.

##### Configured Text Is Compiled Once

**Tests**: `LabelExpressionsTests.LabelExpressions_Resolve_ConfiguredTextIsCompiledOnceAndCached`,
`LabelExpressionsTests.LabelExpressions_Resolve_ConfiguredSyntaxErrorIsSpecException`

Asserts that the same configured text returns the same compiled expression (the cache), that no text and an empty text
give no expression, and that a text with a syntax error (`datum.label +`) is a `VegaLiteSpecException` located at the
location given.

##### The Evaluation Shares The Limits And Parameters Of The Call

**Tests**: `LabelExpressionsTests.LabelExpressions_Evaluate_ParameterIsReadAndStepsAreChargedToTheCall`,
`LabelExpressionsTests.LabelExpressions_Evaluate_StepBudgetIsEnforcedAsRenderException`,
`LabelExpressionsTests.LabelExpressions_Evaluate_StringLimitIsEnforcedAsRenderException`,
`LabelExpressionsTests.LabelExpressions_Evaluate_UnsupportedFunctionIsNullAndEmpty`

Asserts that an expression reads a variable parameter of the chart (`datum.label + unit` is `5 kg`) and that the steps
it takes are added to the counter of the shared evaluator; that a step budget of 5 and a string limit of 8 are located
`VegaLiteRenderException`s that name `MaxEvaluationSteps` and `MaxStringLength`, at the location of the expression; and
that a function outside the whitelist (`format`) evaluates to null and so to the empty label.

##### Axis Labels Are Replaced Before They Are Laid Out

**Tests**: `AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprAppendsPercent`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprConditionalBlanksTicks`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprLongerLabelWidensTheAxisExtent`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprOverlapRemovalUsesTheReplacedText`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprLabelLimitAppliesToReplacedText`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprArrayResultIsMultiLine`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprIsNotUsedWhenLabelsAreOff`

Asserts that `datum.label + '%'` gives every tick label a percent sign, that blank labels are not drawn and do not take
part in the overlap removal, that a longer replaced label grows the extent of the axis, that `labelLimit` truncates the
replaced text, that an array result is a multi-line label, and that an axis with `labels: false` neither evaluates nor
compiles a configured expression (an invalid configured text is not an error).

##### Axis Values Follow The Scale

**Tests**: `AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprIndexIsFractionAndTickIndexIsInteger`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprTemporalAxisUsesDateValue`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprBandAxisGivesCategoryAsValueAndLabel`

Asserts that `index` is a fraction of the way along the ticks and `tickIndex` a whole number, that the value of a time
axis is a date that the time functions can read, and that the value and the label of a band axis are the category text.

##### Axis Configuration Precedence And Errors

**Tests**: `AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprConfigPrecedenceFollowsTheAxisChain`,
`AxisBuilderLabelExprTests.AxisBuilder_Build_LabelExprInvalidConfiguredTextIsSpecException`

Asserts the precedence of `config.axis`, `axisY`, `axisQuantitative`, and the axis object, and that an invalid
expression of the configuration that reaches the builder is a `VegaLiteSpecException`.

##### Legend Labels Are Replaced Before The Entries Are Measured

**Tests**: `LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprSymbolLegendGivesIndexValueAndLabel`,
`LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprAppliesToTheEllipsisEntry`,
`LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprBlankLabelsChangeTheMeasuredWidth`,
`LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprConfigIsIgnoredAndExplicitWins`

Asserts the `index`, `value`, and `label` of a symbol legend, that the `...N entries` row of `symbolLimit` is replaced
with the index after the last entry shown, that blank labels change the measured width of the legend, and that the
`labelExpr` of `config.legend` is not read while the explicit `legend.labelExpr` is.

##### Gradient Legends Give The Position Along The Gradient

**Tests**: `LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprGradientLegendGivesPerc`,
`LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprDiscreteGradientGivesPercAndPerc2`,
`LegendBuilderLabelExprTests.LegendBuilder_Build_LabelExprTemporalLegendGivesDate`

Asserts that `perc` is the position of the tick along the bar and `value` the tick, that a discrete gradient also gives
`perc2` (the end of the class), and that the value of a temporal legend is a date.

##### Facet Headers And Whole Charts Use Label Expressions

**Tests**: `VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_AxisLabelsAreReplacedAndStrictModeHasNothingToReport`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_TemporalAxisUsesTheDateValue`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_LegendLabelsAreReplaced`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderLabelsAreReplacedWithPrecedence`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_WrappedFacetTitlesAreReplaced`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_ParameterCanBeUsed`

Asserts that a chart with a `labelExpr` on its axes, legends, and headers is laid out with the replaced labels and no
diagnostics in strict mode; that the header of a facet takes `header.labelExpr` over `headerRow` or `headerColumn` over
`config.header`; that the cell titles of a wrapped facet use `config.header`; and that an expression reads a parameter.

##### Label Expressions Are Bounded And Deterministic

**Tests**: `VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_BudgetExceededIsARenderException`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_ResultDoesNotDependOnTheCulture`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_FormatIsReportedAndEvaluatesToEmptyLabels`

Asserts that an expression that needs more than the step budget of the call is a render exception located at the
expression, that the labels are the same under `tr-TR` as under the invariant culture, and that the number `format()`
function is reported as `vl-expr-function-format` at the expression text, gives empty labels, and fails strict mode.

##### Label Expressions Are Reported Only When They Cannot Be Evaluated

**Tests**: `FeatureCatalogLabelExprAndOffsetTests.FeatureCatalog_Visit_SupportedLabelExpressionsAreNotReported`,
`FeatureCatalogLabelExprAndOffsetTests.FeatureCatalog_Visit_LabelExpressionConstructsAreReportedAtTheText`,
`FeatureCatalogLabelExprAndOffsetTests.SpecParser_Parse_LabelExpressionErrorIsASpecExceptionAtTheText`,
`FeatureCatalogLabelExprAndOffsetTests.FeatureCatalog_Visit_ConfigLegendLabelExprIsIgnored`,
`RenderCommandVegaLiteTests.RenderCommand_Run_VegaLiteOffsetAndLabelExpr_AreDrawnWithStrictAndNoDiagnostics`

Asserts that a supported label expression is reported nowhere (the retired `vl-expr-label` is never produced); that an
unsupported function or view state is reported at the expression text with `vl-expr-function-<name>` or
`vl-expr-view-state`, also for `config.axis*` and `config.header*`; that a syntax error is a located
`VegaLiteSpecException` when the document is opened; that `config.legend.labelExpr` is neither compiled nor reported; and
that the command line renders a chart with label expressions and offset channels in strict mode with no diagnostics.
