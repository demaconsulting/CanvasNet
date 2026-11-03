### SpecNormalizer Unit Verification Design

This document describes the unit-level verification strategy for the `SpecNormalizer` unit in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: the view tree of a specification (`SpecNormalizer.Layers`,
`LayerTree`, `LayerNode`, `LayerResolve`: layers, concatenations, facets, and the copies of repeats), the expansion of the
composite marks `boxplot`, `errorbar`, and `errorband`
into layers (`SpecNormalizer.Expand`, `ExpandedLayers`, `CompositeBuilder`, `SpecNormalizer.TryOrientation`), and the node
factories and the expression extension that they use (`SpecNode.Overlay`, `SpecExpressions.With`). The unit applies the
inheritance and normalization rules of Vega-Lite and bounds the number and the nesting of the views.

#### Verification Approach

The unit is verified by parsing small, original specifications, building the layer tree or the expansion, and asserting
on the structure it returns: the unit views and their order, the merged encoding of each layer, the mark types of the
layers and compositions that are drawn, the transform entries of each view (their operations, fields, and the text of their
expressions), and the properties of the marks of the parts. No mock or stub is needed: the unit reads only the parsed tree
(`SpecParser`), the resolved configuration (`ConfigResolver`), and the expression compiler (`ExpressionParser`), all of
which
are used as they are, and it does no data, file, or network access. The expansion is the Vega-Lite normalization, so the
expected structure is written into the tests from the public documentation of the composite marks and the upstream
normalization sources that the third-party notice names, and never from the output of Vega-Lite. The numbers that these
structures make the later stages compute (the quartiles, fences, means, and positions) are verified in the system
scenarios (_CanvasNetVegaLite System Verification Design_), where they are derived by hand.

The inheritance rules are asserted with specifications that give the same property at several levels, so that the winner
and the order are both visible: a channel of an inner view replaces the outer one in place and a new channel follows, at
every
nesting level; the size is that of the top-level view; and the data and the transform are each view's own. Identity is
asserted as well: a merged view reads like the layer it stands for (the same pointer, line, and column), and every value
in
it is the original node with the pointer and parent that it had, because a diagnostic about an inherited channel must still
point at the text of the author. The limits are asserted at the exact boundary (the limit is accepted, one more fails
with a
located `VegaLiteRenderException`), including that the views of an expansion count as views and not as nesting levels. The
composite mark rules are asserted for the Tukey and the min-max box plot, the three kinds of error bar input (observations,
ends, and errors), the one- and two-dimensional error band, the part switches in the mark and in the configuration, and
the
orientation rules, with the specification errors of the combinations that cannot be decided.

Unit tests reside in `SpecNormalizerTests.cs`, `SpecNormalizerCompositionTests.cs`, `SpecNormalizerRepeatTests.cs`,
`SpecNormalizerFacetCellTests.cs`,
`SpecNormalizerBoxPlotTests.cs`, and `SpecNormalizerErrorBarTests.cs`
within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `SpecModel` folder; the internal types are reached
through the test assembly's `InternalsVisibleTo` access.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own specification text; no files are read and no state is shared

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong unit view, order, or merged channel, a node that is copied or has lost its pointer or parent, a
limit that
is not enforced at its exact boundary or is reported without a location, a wrong layer, transform, expression, or mark
property of an expansion, an expansion that changes a view that is not a composite mark or the parsed tree, or a missing
specification error constitutes a failure.

#### Unit-Level Test Scenarios

##### Layer Tree: Single View, Layers, And Compositions

**Tests**: `SpecNormalizerTests.SpecNormalizer_Layers_SingleViewIsTheRootItself`,
`SpecNormalizerTests.SpecNormalizer_Layers_LayersAreUnitsInDocumentOrder`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_HConcatBuildsConcatNodeWithChildrenInOrder`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetFieldDefIsWrapFacet`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_LayersInFacetSpecShareTheChart`,
`SpecNormalizerTests.SpecNormalizer_Layers_NullTreeThrows`

Asserts that a specification without a composition is one unit view that is the root object itself (so a single view takes
its usual path); that the layers are the unit views in document order, layers of layers included, with the parent links
and
the depths that follow the nesting; that an `hconcat` is a concatenation node whose children are its views in order, the
leaves being the view objects themselves with their own chart; that a `facet` is a node over its `spec` with the wrap
definition of its field; that the layers of the `spec` of a facet share one chart; and that a null tree throws
`ArgumentNullException`.

##### Inheritance: Encoding, Size, Data, And Transform

**Tests**: `SpecNormalizerTests.SpecNormalizer_Layers_EncodingIsMergedInPlace`,
`SpecNormalizerTests.SpecNormalizer_Layers_NestedLayersMergeEveryLevel`,
`SpecNormalizerTests.SpecNormalizer_Layers_UnitTakesSizeOfTopLevelView`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ChartRootSizeComesFromTheChartNotTheTreeRoot`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ConcatHandsNoEncodingDown`,
`SpecNormalizerTests.SpecNormalizer_Layers_DataAndTransformAreEachViewsOwn`,
`SpecNormalizerTests.SpecNormalizer_Layers_NullDataOfALayerInheritsLikeAbsentData`

Asserts that a layer reads the encoding of the layered views above it with the parent channels in place, the layer's channel
replacing the parent's in place and new channels after them, that the outermost encoding comes first through every nesting
level, and that every value is the original node with its own pointer; that a layer takes the `width` and `height` of the
top-level view and ignores its own while the merged view keeps the pointer and position of the layer, and that in a
concatenation the size comes from the chart (the child of the concatenation), not from the root of the tree; that a
concatenation hands no encoding down; and that the
`data` and
the `transform` of a view are its own (the document applies the layered view's transform first) and that a layer without
them has none; and that a layer whose `data` is an explicit `null` has no data of its own either and so inherits, as
Vega-Lite's `parseRoot` does (`model.data || !model.parent`; only the top-level `null` gives an empty source).

##### Concatenations And Their Layout Settings

**Tests**: `SpecNormalizerCompositionTests.SpecNormalizer_Layers_HConcatBuildsConcatNodeWithChildrenInOrder`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_VConcatHasOneColumn`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ConcatReadsColumnsSpacingAlignCenterBounds`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ConcatSpacingObjectFallsBackToConfig`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetChannelIsWrapWithItsOwnLayout`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_DefaultConfigGivesSpacingTwenty`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_CompositeMarkInsideConcatIsExpanded`

Asserts, with the numbers written into the specifications, that an `hconcat` has no columns, a `vconcat` one (whatever
`columns` says) and a `concat` the `columns` of the view; that the spacing is 20 for both directions by default, a number
for both, an object whose missing entry is the configured spacing (12 in the test), with the alignment each for a
concatenation, no centering and full bounds by default; that a view's `columns`, `spacing` object (5 and 7),
`align` object, `center` object, and `bounds: "flush"` are read as written; that the `columns` and `spacing` of the
`config` apply and that `hconcat` ignores the configured columns; that a `facet` channel's own `columns` (4), `align`
(none) and `spacing` win over the view and the configuration (9 and 11); and that a composite mark in a concatenation is
expanded into layers that belong to its chart.

##### Facets And Faceted Unit Views

**Tests**: `SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetFieldDefIsWrapFacet`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetRowColumnMappingKeepsColumnsOnlyForWrap`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetSpecThatIsACompositionIsSpecError`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_LayerChildThatIsACompositionIsSpecError`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_UnitWithRowColumnChannelsBecomesFacetWithBody`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetBodyDoesNotReapplyDataTransformOrTitle`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_RowWinsOverFacetChannel`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_LayerChildWithRowChannelIsNotFaceted`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_UnitInConcatIsFacetedButNotInFacetSpec`

Asserts that a facet of a `row` and a `column` has no wrap columns and drops the `columns` of the view with one
warning at
that property; that a composition as the `spec` of a facet or as a view of a `layer` is a specification error located at
it; that a unit view with `row` and `column` channels is a facet over the same view object and one synthetic body whose
unit has the mark, the width (100), and only the `x` channel (the same node as the original), with the facet and its
body counting two views; that the data, the transform, and the title belong to the facet and the body reads none; that
`row` wins over `facet`; and that a unit view in a `layer` or in the `spec` of a facet keeps its `row` or `column` channel
while a unit view that is a child of a concatenation is faceted.

##### Facet Cells And Data Sites

**Tests**: `SpecNormalizerFacetCellTests.LayerTree_InstantiateFacet_ReplacesTheTemplateWithOneCopyPerCell`,
`SpecNormalizerFacetCellTests.LayerTree_InstantiateFacet_LayeredBodyCopiesEachLayerAndMapsChartRoots`,
`SpecNormalizerFacetCellTests.LayerTree_InstantiateFacet_FacetedUnitCopiesTheSyntheticBody`,
`SpecNormalizerFacetCellTests.LayerTree_FacetViews_CountsTheViewsOfOneCellAndTheOthers`,
`SpecNormalizerFacetCellTests.LayerTree_InstantiateFacet_RefusesTwiceAndNonFacetsAndAllowsNoCells`,
`SpecNormalizerFacetCellTests.LayerTree_DataSites_CopiesOfARepeatShareOneSite`,
`SpecNormalizerFacetCellTests.LayerTree_DataSites_FacetCellsShareTheRootSiteAndItsFacetFieldsAreCollected`,
`SpecNormalizerFacetCellTests.LayerTree_DataSites_ViewWithOwnDataIsItsOwnSiteAndNeededFiltersViews`

Asserts, with the counts written into the specifications, that a facet whose template is one view (a facet and a body
are two views) has three copies after three cells (four views), that the copies are the units in order with the cell
(index 0 to 2, and the cell value) on each, have the facet as parent, share the `Source` and `Unit` objects of the template,
are their own charts and leave the template without a cell; that a template that is a layered view with two layers
(four views with the facet, three per cell) has seven views after two cells, the units `0, 0, 1, 1` by cell,
all at depth 2,
each leaf a child of the copy of the layered view of its cell, which is its chart (none an original), and the
resolve of the layered view kept; that the synthetic body of a unit view with a `column` channel is copied as a body
(no own
data or transform; the same unit); that for a concatenation of a facet and a bar
(four views) one cell holds 1 view and 3
are outside, before and after two cells (five views), and that a facet without cells holds none (0 and 1); that
a facet with no cell has no child and no unit, is still a composition, refuses a second instantiation
(`InvalidOperationException`), and that a concatenation is refused (`ArgumentException`).
For the sites it asserts that the
three copies of a repeated view with its own data are one site (after the site of the root without data) whose views, read
as date fields, give `a`, `b`, `c`; that the cells of a facet by a temporal field `d` are views of the root's site, whose
facet is listed once whether or not the cells exist and gives the date field `d`;
and that a child with its own data is a
site of its own (with the views of its layers), that the root's site holds the root and the first child, and that a view
outside the needed set leaves its site out.

##### Repeats

**Tests**: `SpecNormalizerRepeatTests.SpecNormalizer_Layers_ArrayRepeatMakesConcatWithColumns`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RowColumnRepeatIsRowMajorCrossProduct`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RowOnlyRepeatHasOneColumn`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_LayerRepeatMakesLayerNode`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RowAndLayerRepeatNestsLayers`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RepeatColumnsWithMappingIsIgnoredWithWarning`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RepeatDataStaysOnTheConcatAndChildDataWins`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RepeatCopiesKeepOriginalPointers`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_EmptyRepeatHasNoViews`

Asserts that a repeat over three values is a concatenation of three copies (four views) with the columns of the view and
the value of each copy; that rows `r1`, `r2` and columns `c1`, `c2`, `c3` give six copies in the order `r1/c1`, `r1/c2`,
`r1/c3`, `r2/c1`, `r2/c2`, `r2/c3` with three columns; that a row-only repeat has one column and a column-only repeat as
many columns as values; that a repeat of layers is a layered node (not a composition) of copies sharing its chart, and
that rows and layers (2 and 2) make a concatenation of two layered cells with four units (seven views, depth 2); that the
`columns` of the view next to rows or columns is dropped with a warning at it; that the data of the repeat stays on the
concatenation and the data of the `spec` is the copies' own; that a copy with nothing to replace is the `spec` object
itself with its pointer; and that an empty list has no views.

##### Repeat References

**Tests**: `SpecNormalizerRepeatTests.SpecNormalizer_Layers_RepeatReplacesRefInFieldSortFieldDatumAndCondition`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_MissingRepeatValueDropsChannelWithWarning`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_FacetWithUnresolvedRepeatFieldIsDropped`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_MissingRepeatValueInSortOrConditionDropsOnlyThat`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RepeatOfFacetedUnitFacetsByTheRepeatedField`

Asserts that a reference in a field, a sort field, a datum, and a condition field becomes the repeated value (`q`) as a
string located at the reference while the rest of each definition is kept; that a reference with no value drops the
channel and is warned about once at the reference even when two copies have it; that an unresolved reference in a sort
drops only the sort and one in a condition drops only that condition (two warnings, at the two references); and that the
facet field of a faceted unit view in a repeat is replaced (`a`, `b`); and that a
`facet` whose only field is a reference with no value is dropped (its `spec` is a unit view in a layer of one, in
each of the two copies) with one warning at `/spec/facet/field`.

##### Node Overlays

**Tests**: `SpecNormalizerTests.SpecNode_Overlay_KeepsOriginIdentityAndChildrenParents`,
`SpecNormalizerTests.SpecNode_Overlay_ManyPropertiesAreFoundByName`,
`SpecNormalizerTests.SpecNode_Overlay_NullArgumentsThrow`

Asserts that an overlay reads like its origin (name, position, pointer, and parent) with other properties, that it does
not
take over the properties from their parents (each keeps its own parent and pointer), that an overlay with many
properties is
still searched by name, and that null arguments throw.

##### Resolve

**Tests**: `SpecNormalizerTests.LayerResolve_Read_FollowsScaleForGuides`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ConcatDefaultsPositionScalesIndependentAndOthersShared`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ConcatAxisIsAlwaysIndependent`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ExplicitSharedConcatAxisIsReported`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetDefaultsSharedExceptTheta`

Asserts that everything is shared when a view has no `resolve`, that an independent scale makes the axis and the legend
of its
channel independent, that an explicit guide mode applies to a shared scale, and that the modes of a channel that is not
named
are the default; that the scales of `x`, `y`, `xOffset`, `yOffset`, `theta`, and `radius` of a concatenation are
independent and `color`, `size`, `shape`, `opacity`, and `stroke` shared (with their legends), an explicit mode winning;
that the axes of a concatenation are independent whatever `resolve` says, with a warning at an explicit `shared` axis (one
for `x`, none for an `independent` entry); and that a facet shares everything except `theta`.

##### Limits

**Tests**: `SpecNormalizerTests.SpecNormalizer_Layers_DepthLimitIsEnforced`,
`SpecNormalizerTests.SpecNormalizer_Layers_ViewLimitIsEnforced`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_ExpandedViewsCountAgainstMaxViewsNotTheDepth`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarViewsAreCounted`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_NestedCompositionsCountTowardsMaxCompositionDepth`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ConcatChildrenCountTowardsMaxViews`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_ChildListIsCheckedBeforeTheChildrenAreMade`,
`SpecNormalizerCompositionTests.SpecNormalizer_Layers_FacetedUnitCountsFacetAndBody`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_RepeatProductIsCheckedBeforeExpansion`,
`SpecNormalizerRepeatTests.SpecNormalizer_Layers_NestedRepeatCountsLevelsAndInnerValueWins`

Asserts that nesting deeper than `MaxCompositionDepth` and more views than `MaxViews` are located
`VegaLiteRenderException`s and that exactly the limit is accepted; that the views of a box plot expansion count as views
(nine
for a default box plot: the composite, two groups, the whisker view, and five unit views) and not as nesting levels; and
that
the views of an error bar count (three: the composite, its view, and the rule; five with the ticks); that a `vconcat`
of an
`hconcat` is two levels (a limit of one is refused at the inner `hconcat`, a limit of two accepted), and a `facet`
over a
`layer` two (refused at the `layer`); that the root and three children of a concatenation are four views (a limit of
three is refused at the list, `/hconcat`); that the children of a concatenation or a layer are counted before any is
made (2,000 children against a limit of five are refused at the list, and the queued sibling of a layer counts); that a
faceted
unit view is two views (a limit of one is refused at the view itself); and that a repeat of three rows and four columns
is 13 views (a limit of twelve is refused at the `repeat`), that a repeat of a thousand rows and a thousand columns is
refused with the number one million in the message without making the copies, that the layers of every cell count (two
rows and three layers are nine views, a limit of eight is refused), and that a repeat in a repeat is two levels, the inner
value overriding the outer.

##### Expansion: Derived Tree, Unchanged Views, And Arguments

**Tests**: `SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_ExpressionsAreInTheDerivedTreeOnly`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_OtherMarksAreUnchangedAndArgumentsChecked`,
`SpecNormalizerTests.SpecExpressions_With_AddsEntriesToACopy`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_BoxPlotLayerMergesTheEncodingOfItsParents`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarInALayerInheritsTheEncoding`,
`SpecNormalizerTests.TemporalFields_CollectAll_IsTheUnionOverTheViews`

Asserts that the expansion does not touch the parsed tree (the expressions it writes are only in the derived tree, and a
specification without a composite mark gives back the very tree that was parsed); that the marks that are not composite
are
left as unit views and that null arguments throw; that the expressions of a collection can be extended without changing
the
collection that is extended; that a composite mark in a layer takes the encoding of the layered views above it and the expanded
parts do not inherit the encodings again; and that the temporal fields of several views are collected together, each once.

##### Box Plot: Tukey And Min-Max Layers

**Tests**: `SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_TukeyBoxPlotIsOutlierWhiskerAndBoxViews`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_TukeyTransformsFollowVegaLite`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_MinMaxIsOneAggregateView`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_ExtentComesFromTheMarkThenTheConfiguration`

Asserts the layers of a default box plot in the order of Vega-Lite's normalization (the view of the outliers and of the
whiskers whose rows have their quartiles, then the view of the box); the transforms (the quartiles by a `joinaggregate`
grouped by the grouping field, the whisker view that keeps the rows inside the fences of 1.5 interquartile ranges and takes
their extremes, the box view that aggregates the three quartiles) with all the expressions compiled; the min-max form as
one
view whose single aggregate adds the minimum and the maximum, with the whiskers, the box, and the median and no
outliers; and
that the extent is a multiple of the interquartile range in the fences, from the mark, else the configuration, else 1.5.

##### Box Plot: Parts, Properties, And Switches

**Tests**: `SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_PartsDrawTheComputedFields`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_PartMarksHaveTheDefaults`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_MedianColorFallsBackToTheBoxColorOnAFlatBox`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_PartsCanBeSwitched`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_EndTicksAndSizes`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_PartPropertiesOverrideInOrder`

Asserts the fields that each part draws (the whiskers between the lower whisker and the lower box and between the upper
box
and the upper whisker, the box between the quartiles, the median at the middle, and the style `boxplot-<part>` on every
part); the default marks (the box and the median 14 wide, a bar along the orientation and a white tick across it, rules
for
the whiskers, points for the outliers); that the median color is a condition on the quartiles that gives the box color
to a
flat box; that each part can be turned off in the mark and the end ticks turned on, in the mark and in the
configuration; that
the end ticks are black, opaque, and across the box and the box takes its size from the mark, the configuration, or 14;
and the
order in which properties reach the parts (the configuration of a part first, then the `color`, `opacity`, and `clip` of
the
mark for every part, then the part's own object).

##### Box Plot: Orientation, Channels, And Errors

**Tests**: `SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_OrientFollowsTheContinuousChannel`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_HorizontalBoxPlotUsesXAndX2`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_TitleOfTheMeasureDefaultsToTheField`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_SharedChannelsFollowVegaLite`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_GroupingChannelsDropBinTimeUnitAndAggregate`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_SingleBoxHasNoGroupBy`,
`SpecNormalizerBoxPlotTests.SpecNormalizer_Expand_UndecidableBoxPlotIsASpecificationError`

Asserts that the measure is the continuous channel (a horizontal plot has it on `x` and `x2`, with vertical ticks), that
an
`orient` decides, and that two continuous channels are vertical unless only `y` is temporal; that the title of the
measure is the
field when none is written, on every part; that the color and the size go to the box (the median takes the size and a
conditional color), the outliers get the color, the whiskers neither, a grouping channel is added to the group fields
and the
tooltip is left out; that a bin, a time unit, or an aggregate on a grouping channel is not applied (the raw field groups
the
rows and an aggregating channel is left out); that a box plot without a grouping channel groups by nothing; and the
specification errors of no continuous channel, one channel that is not continuous, and a measure that is not a field.

##### Error Bar: Inputs, Extents, And Parts

**Tests**: `SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarDefaultIsTheRuleOfTheMeanAndStandardError`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarExtentAndCenterChooseTheAggregates`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ConfiguredCenterApplies`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_RangeChannelsAreCopiedWithoutAggregate`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorChannelsAreAddedToTheValue`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarTicksComeBeforeTheRule`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarPartsFollowMarkAndConfiguration`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBarSharesTheOtherChannels`

Asserts the layers of a default error bar (one view whose transforms aggregate the standard error and the mean per group
and
compute the ends, and whose only layer is the rule between them; the ticks off); the aggregates of every extent and center
(the center follows the extent when only that is given, and the extent follows the center), and that the center of the
configuration applies when the mark gives neither; that a pre-aggregated range copies the ends by `calculate` without
aggregating and that errors are added to the value (and subtracted, or the second error added, for the lower end); that
the
ticks that are switched on are a tick at each end across the rule, before the rule, with the size and thickness of the mark;
that the mark's setting beats the configuration, a part with no setting follows the configuration, and a mark whose
parts are
all off has no layer; and that the grouping channels, a color, and a detail reach the rule while the size channel is dropped
and the written `title`, `scale`, and `axis` of the measure go to the parts.

##### Error Band: Dimensions, Borders, And Configuration

**Tests**: `SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBandOneDimensionalIsARectWithRuleBorders`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBandTwoDimensionalIsAnAreaWithLineBorders`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorBandPartsAndConfiguration`

Asserts that a band with only the measure encoded is a `rect` between the ends at 30 percent opacity with no borders, that
borders that are switched on are `rule` marks at the lower and upper ends drawn after the band, that a band with both
`x` and
`y` is an `area` with `line` borders that take the interpolation and, only with an interpolation, the tension, and the part
switches and properties from the mark and the configuration, with the size channel kept.

##### Orientation And Specification Errors Of The Error Marks

**Tests**: `SpecNormalizerErrorBarTests.SpecNormalizer_TryOrientation_DecidesFromTheChannels`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ExplicitOrientDecidesObservationsOnly`,
`SpecNormalizerErrorBarTests.SpecNormalizer_Expand_ErrorChannelWithoutFieldIsASpecificationError`

Asserts, as a theory over the channel combinations, that a range or error channel decides the orientation, then (for
observations) the continuous channel, and that a mark that cannot be decided reports the problem without throwing from
`TryOrientation`; that an explicit `orient` decides observations whose channels are both continuous but not a range or
errors; and that a range channel that is not a field is a specification error located at the mark.
