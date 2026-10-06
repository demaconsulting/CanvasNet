### ChartDocument Unit Verification Design

This document describes the unit-level verification strategy for the `Chart`, `ChartSeries`,
`ChartAxis`, `ChartLegend`, `ChartTitle`, `ChartType`, `ChartLegendPosition`, and `ChartBuilder`
types.

#### Verification Approach

The `ChartDocument` unit is verified through unit tests that exercise each type's constructor (or,
for `ChartBuilder`, its fluent methods and `Build()`) directly, asserting on the resulting
instance's exposed properties and on thrown exception types. Because `ChartDocument`'s only
dependency is the core `CanvasNet` system's `Rgba32` value type (an in-house, already-verified
unit, not an external service), no mocking or stubbing is required. Tests supply controlled
constructor arguments — valid combinations, every documented invalid-argument case, and
mutable collection instances used to prove defensive copying — and assert on the constructed
instance's properties or the thrown exception's type.

Unit tests reside in `ChartTests.cs`, `ChartSeriesTests.cs`, `ChartAxisTests.cs`,
`ChartLegendTests.cs`, `ChartTitleTests.cs`, `ChartTypeTests.cs`, and `ChartBuilderTests.cs`
within the `DemaConsulting.CanvasNet.Charts.Tests` project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `ChartDocument`'s only dependency is the in-house `Rgba32` unit
- **Isolation**: Each test method constructs its own instances and, where relevant, its own
  mutable source `List<T>`; no shared state between tests

#### Unit-Level Test Scenarios

##### CanvasNetCharts-ChartModel-ChartDocument-ChartConstruction: Chart Constructs From Required and Optional Members

**Tests**: `ChartTests.Constructor_MinimalMembers_SetsRequiredAndLeavesOptionalsUnset`,
`ChartTests.Constructor_AllMembers_SetsAllMembers`

Constructs a `Chart` with only its required `ChartType` and series, asserting every optional
property (`CategoryAxis`, `ValueAxis`, `Legend`, `Title`, `ColorPalette`) is `null`; and
separately constructs a `Chart` supplying every optional member, asserting each is stored and
returned by reference (`CategoryAxis`/`ValueAxis`/`Legend`/`Title` via `Assert.Same`,
`ColorPalette` via value equality).

##### CanvasNetCharts-ChartModel-ChartDocument-ChartTypeValidation: Chart Rejects an Undefined ChartType

**Test**: `ChartTests.Constructor_UndefinedType_ThrowsArgumentOutOfRangeException`

Constructs a `Chart` with an out-of-range `(ChartType)99` cast value. Asserts
`ArgumentOutOfRangeException` is thrown.

##### CanvasNetCharts-ChartModel-ChartDocument-SeriesCollectionValidation: Chart Rejects an Invalid Series Collection

**Tests**: `ChartTests.Constructor_NullSeries_ThrowsArgumentNullException`,
`ChartTests.Constructor_EmptySeries_ThrowsArgumentException`,
`ChartTests.Constructor_NullSeriesEntry_ThrowsArgumentException`

Constructs a `Chart` with, respectively, a `null` series argument, an empty series collection,
and a series collection containing a `null` entry alongside a valid one. Asserts
`ArgumentNullException` for the `null` argument case and `ArgumentException` for the empty and
null-entry cases.

##### CanvasNetCharts-ChartModel-ChartDocument-CategoryLengthValidation: Chart Validates Series/Category-Axis Length Consistency

**Tests**: `ChartTests.Constructor_CategoryBasedTypeWithMatchingLength_Succeeds`,
`ChartTests.Constructor_CategoryBasedTypeWithMismatchedLength_ThrowsArgumentException`,
`ChartTests.Constructor_ProportionalType_IsExemptFromLengthCheck`,
`ChartTests.Constructor_CategoryAxisWithNoLabels_DoesNotTriggerLengthCheck`

Constructs a `Chart` for each category-based `ChartType` (`Bar`, `Column`, `Line`, `Area`) with a
series whose `Values.Count` matches its category axis's label count (asserting success) and with
a mismatched count (asserting `ArgumentException`); separately constructs a `Chart` for each
proportional-share `ChartType` (`Pie`, `Doughnut`) with a mismatched count (asserting success,
proving the check does not apply), and a category-based `Chart` whose category axis has no
labels (asserting success, proving a value-only axis does not trigger the check).

##### CanvasNetCharts-ChartModel-ChartDocument-ColorPaletteValidation: Chart Rejects an Empty Color Palette

**Test**: `ChartTests.Constructor_EmptyColorPalette_ThrowsArgumentException`

Constructs a `Chart` with a non-null, empty `colorPalette` argument. Asserts
`ArgumentException` is thrown.

##### CanvasNetCharts-ChartModel-ChartDocument-SeriesValidation: ChartSeries Constructs From Required and Optional Members

**Tests**: `ChartSeriesTests.Constructor_NameAndValuesOnly_SetsMembersAndLeavesOptionalsUnset`,
`ChartSeriesTests.Constructor_AllMembers_SetsAllMembers`

Constructs a `ChartSeries` with only its required name and values, asserting every optional
property (`PointColors`, `PointLabels`, `Color`) is `null`; and separately constructs a
`ChartSeries` supplying every optional member, asserting each is stored and returned correctly.

##### CanvasNetCharts-ChartModel-ChartDocument-SeriesNameValidation: ChartSeries Rejects an Invalid Name

**Tests**: `ChartSeriesTests.Constructor_NullName_ThrowsArgumentNullException`,
`ChartSeriesTests.Constructor_EmptyOrWhitespaceName_ThrowsArgumentException`

Constructs a `ChartSeries` with a `null` name, and separately with an empty string and a
whitespace-only string (via `[Theory]`). Asserts `ArgumentNullException` for the `null` case and
`ArgumentException` for the empty/whitespace cases.

##### CanvasNetCharts-ChartModel-ChartDocument-ValuesValidation: ChartSeries Rejects an Invalid Values Collection

**Tests**: `ChartSeriesTests.Constructor_NullValues_ThrowsArgumentNullException`,
`ChartSeriesTests.Constructor_EmptyValues_ThrowsArgumentException`,
`ChartSeriesTests.Constructor_NonFiniteValueEntry_ThrowsArgumentException`

Constructs a `ChartSeries` with, respectively, a `null` values argument, an empty values
collection, and a values collection containing `NaN`/`PositiveInfinity`/`NegativeInfinity` (via
`[Theory]`). Asserts `ArgumentNullException` for the `null` case and `ArgumentException` for the
empty and non-finite cases.

##### CanvasNetCharts-ChartModel-ChartDocument-PerPointCollectionValidation: ChartSeries Rejects Mismatched Per-Point Collections

**Tests**: `ChartSeriesTests.Constructor_PointColorsCountMismatch_ThrowsArgumentException`,
`ChartSeriesTests.Constructor_PointLabelsCountMismatch_ThrowsArgumentException`,
`ChartSeriesTests.Constructor_NullPointLabelEntry_ThrowsArgumentException`

Constructs a `ChartSeries` with a `pointColors` collection whose count does not match `values`'s
count, a `pointLabels` collection whose count does not match, and a `pointLabels` collection
containing a `null` entry. Asserts `ArgumentException` for each case.

##### CanvasNetCharts-ChartModel-ChartDocument-AxisValidation: ChartAxis Constructs From Labels, Range, and Title

**Tests**: `ChartAxisTests.Constructor_LabelsOnly_SetsLabelsAndLeavesRangeUnset`,
`ChartAxisTests.Constructor_ValueRangeOnly_SetsRangeAndLeavesLabelsUnset`,
`ChartAxisTests.Constructor_LabelsAndRangeAndTitle_SetsAllMembers`

Constructs a `ChartAxis` with only labels (asserting `Minimum`/`Maximum`/`TickInterval`/`Title`
are `null`), with only a value range (asserting `Labels` is `null`), and with every member
supplied (asserting each is stored).

##### CanvasNetCharts-ChartModel-ChartDocument-AxisLabelValidation: ChartAxis Validates Label Entries

**Tests**: `ChartAxisTests.Constructor_EmptyStringLabel_IsAccepted`,
`ChartAxisTests.Constructor_NullLabelEntry_ThrowsArgumentException`

Constructs a `ChartAxis` with an empty-string label entry (asserting success, proving a blank
category label is legitimate) and with a `null` label entry (asserting `ArgumentException`).

##### CanvasNetCharts-ChartModel-ChartDocument-AxisRangeValidation: ChartAxis Validates Minimum/Maximum/TickInterval

**Tests**: `ChartAxisTests.Constructor_NonFiniteMinimum_ThrowsArgumentOutOfRangeException`,
`ChartAxisTests.Constructor_NonFiniteMaximum_ThrowsArgumentOutOfRangeException`,
`ChartAxisTests.Constructor_MinimumEqualToMaximum_ThrowsArgumentOutOfRangeException`,
`ChartAxisTests.Constructor_MinimumGreaterThanMaximum_ThrowsArgumentOutOfRangeException`,
`ChartAxisTests.Constructor_InvalidTickInterval_ThrowsArgumentOutOfRangeException`

Constructs a `ChartAxis` with a non-finite minimum, a non-finite maximum (both via `[Theory]`
over `NaN`/`PositiveInfinity`/`NegativeInfinity`), a minimum equal to the maximum, a minimum
greater than the maximum, and an invalid tick interval (`[Theory]` over `0`, a negative value,
and `NaN`/`PositiveInfinity`). Asserts `ArgumentOutOfRangeException` for every case.

##### CanvasNetCharts-ChartModel-ChartDocument-AxisTitleValidation: ChartAxis Rejects an Invalid Title

**Tests**: `ChartAxisTests.Constructor_WhitespaceTitle_ThrowsArgumentException`,
`ChartAxisTests.Constructor_EmptyTitle_ThrowsArgumentException`

Constructs a `ChartAxis` with a whitespace-only title and an empty title. Asserts
`ArgumentException` for both cases.

##### CanvasNetCharts-ChartModel-ChartDocument-LegendValidation: ChartLegend Applies Defaults, Rejects Undefined State

**Tests**: `ChartLegendTests.Constructor_Defaults_IsVisibleAndRightPositioned`,
`ChartLegendTests.Constructor_EachDefinedPosition_IsAccepted`,
`ChartLegendTests.Constructor_IsVisibleFalse_IsStored`,
`ChartLegendTests.Constructor_UndefinedPosition_ThrowsArgumentOutOfRangeException`

Constructs a `ChartLegend` with no arguments (asserting `Position == Right` and
`IsVisible == true`), with each defined `ChartLegendPosition` value (`[Theory]`, asserting
success), with `isVisible: false` (asserting it is stored), and with an out-of-range
`(ChartLegendPosition)99` cast value (asserting `ArgumentOutOfRangeException`).

##### CanvasNetCharts-ChartModel-ChartDocument-TitleValidation: ChartTitle Constructs From Text and an Optional Font Size

**Tests**: `ChartTitleTests.Constructor_TextOnly_SetsTextAndLeavesFontSizeUnset`,
`ChartTitleTests.Constructor_TextAndFontSize_SetsBothMembers`,
`ChartTitleTests.Constructor_NullText_ThrowsArgumentNullException`,
`ChartTitleTests.Constructor_EmptyText_ThrowsArgumentException`,
`ChartTitleTests.Constructor_WhitespaceText_ThrowsArgumentException`,
`ChartTitleTests.Constructor_InvalidFontSize_ThrowsArgumentOutOfRangeException`

Constructs a `ChartTitle` with only text, with text and a font size, with a `null` text argument,
with an empty text argument, with a whitespace-only text argument, and with an invalid font size
(`[Theory]` over `0`, a negative value, and `NaN`/`PositiveInfinity`/`NegativeInfinity`). Asserts
the success cases store the expected properties, `ArgumentNullException` for the `null` case,
`ArgumentException` for the empty/whitespace cases, and `ArgumentOutOfRangeException` for the
invalid font size cases.

##### CanvasNetCharts-ChartModel-ChartDocument-DefensiveCopies: Every Collection Argument Is Defensively Copied

**Tests**: `ChartTests.Constructor_MutatingOriginalSeriesList_DoesNotAffectStoredSnapshot`,
`ChartTests.Constructor_MutatingOriginalColorPaletteList_DoesNotAffectStoredSnapshot`,
`ChartSeriesTests.Constructor_MutatingOriginalValuesList_DoesNotAffectStoredSnapshot`,
`ChartSeriesTests.Constructor_MutatingOriginalPointColorsList_DoesNotAffectStoredSnapshot`,
`ChartSeriesTests.Constructor_MutatingOriginalPointLabelsList_DoesNotAffectStoredSnapshot`,
`ChartAxisTests.Constructor_MutatingOriginalLabelsList_DoesNotAffectStoredSnapshot`

Constructs each type from a mutable `List<T>`, then mutates (adds to and/or overwrites an
element of) the original list after construction. Asserts the constructed instance's exposed
property still reflects the original, pre-mutation contents, proving the constructor copied the
collection rather than wrapping or referencing it directly.

##### CanvasNetCharts-ChartModel-ChartDocument-BuilderFluentChaining: ChartBuilder Methods Chain and Preserve Series Order

**Tests**: `ChartBuilderTests.FluentMethods_EachReturnSameBuilderInstance`,
`ChartBuilderTests.Build_WithMultipleSeriesAddedViaChartSeriesOverload_PreservesOrder`

Calls every `ChartBuilder` fluent method and asserts (`Assert.Same`) each returns the same
builder instance; separately adds two series via the pre-built `ChartSeries` overload of
`AddSeries` and asserts `Build()`'s resulting `Chart.Series` preserves insertion order.

##### CanvasNetCharts-ChartModel-ChartDocument-BuilderDelegatesValidation: ChartBuilder Forwards Constructor Exceptions

**Tests**: `ChartBuilderTests.AddSeries_InvalidName_PropagatesChartSeriesConstructorException`,
`ChartBuilderTests.AddSeries_NullChartSeries_PropagatesArgumentNullException`,
`ChartBuilderTests.WithCategoryAxis_NullChartAxis_PropagatesArgumentNullException`,
`ChartBuilderTests.WithValueAxis_NullChartAxis_PropagatesArgumentNullException`,
`ChartBuilderTests.WithValueAxis_InvalidRange_PropagatesChartAxisConstructorException`,
`ChartBuilderTests.WithLegend_NullChartLegend_PropagatesArgumentNullException`,
`ChartBuilderTests.WithTitle_WhitespaceText_PropagatesChartTitleConstructorException`,
`ChartBuilderTests.WithColorPalette_Null_ThrowsArgumentNullException`,
`ChartBuilderTests.Build_SeriesLengthMismatchedWithCategoryAxis_PropagatesChartConstructorException`

Calls each `ChartBuilder` method with an argument the corresponding model constructor would
itself reject (an invalid series name, a `null` pre-built `ChartSeries`/`ChartAxis`/
`ChartLegend`, an invalid axis range, whitespace-only title text, a `null` color palette, and a
configuration that produces a series/category-axis length mismatch only once `Build()` calls
`Chart`'s own constructor). Asserts each surfaces the identical exception type the corresponding
model constructor documents.

##### CanvasNetCharts-ChartModel-ChartDocument-BuilderIncompleteBuildFails: ChartBuilder.Build Fails on Incomplete Input

**Tests**: `ChartBuilderTests.Build_TypeNeverSet_ThrowsInvalidOperationException`,
`ChartBuilderTests.Build_NoSeriesAdded_ThrowsInvalidOperationException`

Calls `Build()` after adding a series but never calling `OfType`, and separately after calling
`OfType` but never adding a series. Asserts `InvalidOperationException` is thrown in both cases.

##### CanvasNetCharts-ChartModel-ChartDocument-TypeEnumeration: ChartType Defines Exactly the Phase 1 Chart Kinds

**Tests**: `ChartTypeTests.AllV1Members_AreDefined`, `ChartTypeTests.UndefinedValue_IsNotDefined`,
`ChartTypeTests.UndefinedValue_RejectedByChartConstructor`

Asserts `Enum.IsDefined` is `true` for each of `Bar`, `Column`, `Line`, `Pie`, `Doughnut`, and
`Area` (via `[Theory]`); asserts `Enum.IsDefined` is `false` for an out-of-range cast value; and
asserts `Chart`'s constructor rejects that same out-of-range value with
`ArgumentOutOfRangeException`, cross-checking the enumeration test against the consuming type's
own validation.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong property
value constitutes a failure.
