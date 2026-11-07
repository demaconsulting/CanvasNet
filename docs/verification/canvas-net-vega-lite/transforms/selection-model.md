### SelectionModel Unit Verification Design

This document describes the unit-level verification strategy for the `SelectionModel` unit in the
`DemaConsulting.CanvasNet.VegaLite.Transforms` namespace: the internal `SelectionModel` class (`Declares`, `Create`, `Find`,
the stores, the resolved values, and the warnings), the `SelectionComponent` class, and the `SelectionBind` enumeration.
The
unit takes the layer tree of a specification, its configuration, the values of the caller, and the calendar of the call,
and
gives the initial state of the selections that the expression engine and the later stages read.

#### Verification Approach

The unit is verified with small, original specifications that are parsed by the real `SpecParser`, normalized by the real
`SpecNormalizer`, and given to `SelectionModel.Create` exactly as the render pipeline does (the model is built from the
layer
tree of the specification as written, with the configuration resolved from the tree and a UTC calendar). The assertions
read
the components (the projection as `field/channel/type` texts, the tuples, the unit index and name, the bind kind and the
resolve), the stores, the resolved values, and the warnings. No mocking is used. Every expected value is derived by hand
from
the rules of Vega-Lite's projection and of the selection store: an interval over a linear scale stores a range (`R`),
over a
point scale the values at its ends (`E`), a binned field of a point selection stores a range that excludes its end
(`R-RE`), a time unit projects over the derived field (`month_d`), and a date-time object is the number of milliseconds
of
the instant (2020-01-02T00:00Z is 18,263 days after 1970-01-01, 1,577,923,200,000 ms; 2020-03-01 is 60 days after
January 1
2020, 1,583,020,800,000 ms). The behavior of the selection in a chart (which rows a condition or a filter selects) is
verified by the system tests `CanvasNetVegaLite_SystemIntegration_Selection_*` in
`VegaLiteSelectionIntegrationTests.cs`, whose expectations are derived from the arithmetic of the scales and of the rows.

Unit tests reside in `SelectionModelTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Transforms` folder. The internal types are reached through the test assembly's `InternalsVisibleTo` access.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own specification text and builds its own model with its own evaluation state; the
  calendar is UTC, no files are read, and no state is shared between tests

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly asserted.
A wrong projection, tuple type, or tuple, a selection declared for the wrong unit, a store taken from the wrong unit, a
value
of the caller that does not replace the value of the specification, a warning that is missing or not located, or a resolved
value that differs from the hand-derived one constitutes a failure.

#### Unit-Level Test Scenarios

##### Default Projection And Tuple Types

**Tests**: `SelectionModelTests.SelectionModel_Create_DefaultProjectionOfEachType`,
`SelectionModelTests.SelectionModel_Create_TupleTypeFollowsTheScaleAndTheBin`

Asserts that an interval selection without a projection is projected over `x` and `y` as ranges (`a/x/R`, `b/y/R`) and a
point selection over the identity of the rows (`_vgsid_`), and that the tuple type follows the scale and the bin: values
over
a point scale, a range that excludes its end for a binned field of a point selection, a range for an interval over a binned
linear axis, and a range over the derived field of a time unit.

##### Projection From The Value

**Tests**: `SelectionModelTests.SelectionModel_Create_ProjectionIsInferredFromTheKeysOfTheValue`,
`SelectionModelTests.SelectionModel_Create_ScalarAndMappingValues`,
`SelectionModelTests.SelectionModel_Create_UnprojectableChannelsAreReported`

Asserts that a key of the value that names a channel is an encoding (the field of that channel) and any other key a
field of
a point selection, that a list of mappings gives one tuple each, that a scalar applies to every projected field, that the
value of a channel wins over the value of its field, that a channel without a field and an aggregated channel are skipped
with a `vl-param-selection-projection` warning at the declaration, and that an interval initialized with a key that is no
channel reports once and takes the default encodings (the tuple holds null where the mapping has no entry).

##### Initial Value And Dates

**Tests**: `SelectionModelTests.SelectionModel_Create_IntervalValueAndDates`,
`SelectionModelTests.SelectionModel_Create_CallerValueReplacesTheValueOfTheSpecification`

Asserts that the date-time objects of an interval are the milliseconds of the instants in UTC, that the value of the caller
(a map of arrays, a list of maps, `DateTime` values) replaces the value of the specification, that a null clears it (the
store has no tuple), and that the stored tuple and the resolved value hold the numbers.

##### Declaration For Unit Views

**Tests**: `SelectionModelTests.SelectionModel_Create_TopLevelSelectionIsDeclaredForEveryUnitAndStoredFromTheFirst`,
`SelectionModelTests.SelectionModel_Create_UnitSelectionBelongsToItsUnit`,
`SelectionModelTests.SelectionDeclarations_UnitNameOf_NamesUnitsByPosition`,
`SelectionModelTests.SelectionModel_Declares_FindsSelectionsInAnyView`

Asserts that a top-level selection of a concatenation is declared for each of its units and its store is the one of the
first
(whose name it carries), that `views` narrows the units by name, that the selection of one view is declared for that view
only, that an unnamed unit is named by its position (`concat_0`, `layer_1`), and that the scan finds a selection in any
view
or in a legacy `selection`.

##### Layers, Legacy Selections, Bind, And Row Identity

**Tests**: `SelectionModelTests.SelectionModel_Create_SelectionOfTwoLayersIsDropped`,
`SelectionModelTests.SelectionModel_Create_LegacySelectionsAreMapped`,
`SelectionModelTests.SelectionModel_Create_KeepsBindAndResolve`,
`SelectionModelTests.SelectionModel_Create_RowIdentitySelectionHasNoTuples`

Asserts that a selection that two layers of a layered view receive is dropped (an empty store and an empty resolved value)
while naming one layer in `views` keeps it, that `single`, `multi`, and `interval` map to a point and an interval selection
with `init` as the value, that the kind of the binding and the resolve (from the selection or the configuration) are
kept, that
the model of a specification without a selection is the shared empty model, and that a row-identity selection has no tuples,
with a warning only for a value of the caller.

##### Stores And Resolved Values

**Tests**: `SelectionModelTests.SelectionModel_Create_StoresAndResolvedValues`

Asserts that each selection has its store (empty for a selection without a value), that the resolved value of an
interval is
its range with the ends in order, of a point selection its values and the `vlPoint` query, and of an empty selection an
empty
object.

##### Queries: Units, Extents, And Marks

**Tests**: `SelectionModelQueryTests.SelectionModel_ComponentsOf_AreTheSelectionsDeclaredForTheUnit`,
`SelectionModelQueryTests.SelectionModel_ComponentsOf_ANodeOfAnotherTreeHasNone`,
`SelectionModelQueryTests.SelectionModel_FindComponent_WalksUpToTheViewsAbove`,
`SelectionModelQueryTests.SelectionModel_BoundExtentOf_IsAnIntervalBoundToAContinuousScale`,
`SelectionModelQueryTests.SelectionModel_ResolveExtent_ReadsTheSelectedRangeOfAField`,
`SelectionModelQueryTests.SelectionModel_ResolveExtent_AVariableParameterIsNotASelection`,
`SelectionModelQueryTests.SelectionModel_Create_ABindingInAConcatenationHoldsNothing`,
`SelectionModelQueryTests.SelectionModel_Create_TheMarkIsTheConfigurationMergedUnderTheSelection`,
`SelectionModelQueryTests.SelectionModel_Create_TheResolutionComesFromTheConfiguration`

Asserts that the model, once bound to a layer tree, lists the selections of a unit (and none for a node of another
tree), that a view sees the selection of its own units or else of the nearest view above it, that only an interval
selection resolved globally and bound to the scales gives a channel an extent (the last one winning), that the selected
range of a field is read by the field, else the encoding, else the first projected field (with the projection warning
for the guess, once), that a name that is a variable parameter is not a selection, that the value of a scale-bound
selection in a concatenation is empty, that the mark is the `config.selection.interval.mark` merged under the mark of
the selection, and that the resolution is the merged one.

##### Time Units Of Projections

**Tests**: `VegaLiteSelectionTimeUnitIntegrationTests.CanvasNetVegaLite_SystemIntegration_TimeUnitSelection_AFilterTestsTheDerivedField`,
`VegaLiteSelectionTimeUnitIntegrationTests.CanvasNetVegaLite_SystemIntegration_TimeUnitSelection_AFilterInAnotherViewAddsTheTimeUnit`,
`VegaLiteSelectionTimeUnitIntegrationTests.CanvasNetVegaLite_SystemIntegration_TimeUnitSelection_AConditionAddsTheTimeUnitToItsView`

Asserts that a filter on a selection projected over a month keeps the rows whose derived month start lies in the selected
range (the instants are in 2012, the year of a month-only unit), that a filter in a view that does not write the time
unit derives the field itself, and that a condition adds the unit to the data of its own view.
