### ExpressionReferenceFolder Unit Verification Design

This document describes the unit-level verification strategy for the `ExpressionReferenceFolder` unit in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: the internal static `ExpressionReferenceFolder` class (`Fold`,
which evaluates the static `{"expr": ...}` references of a specification once and rewrites the tree), its `FoldResult`
record, and the internal static `ExpressionReferenceSites` class (the table of the properties that take an expression
reference, owner by owner, and the walk `Find` that finds the references). The unit takes the `StaticReference` list
that
the `SpecParser` unit compiled and the `ExpressionEvaluator` of a call, and gives the tree that every later stage reads.

#### Verification Approach

The unit is verified with small, original specifications that are parsed by the real `SpecParser`, whose parameters
are evaluated by the real `ParameterEnvironment` into a real `ExpressionEvaluator` (a test helper does exactly what the
render pipeline does: parse, populate the parameters, fold), and with assertions on the folded tree: the value at a
pointer, the kind of the node, its location, and the identity of the subtrees. No mocking is used; every expected value
is derived by hand
from the expressions and the parameters of the specification text (for example 6 is 3 doubled, the font size 16 is 6
plus
10, and 2020-01-01 is 1,577,836,800,000 milliseconds). The folding of every property of the table of sites is checked
with one theory that, for each pair of an owner and a property, writes a specification that holds a reference to `7` at
that property, parses and folds it, and asserts that the reference is a static site, that the value 7 is in its place at
the same pointer, and (for the properties that take a text) that it is the text `7`. The table itself is pinned by the
number of properties of each owner in the version 6 schema, so that a change of the table is a deliberate decision.
The limits are verified with a small step budget and a small string limit given to the evaluator.

Unit tests reside in `ExpressionReferenceFolderTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project,
in
the `SpecModel` folder. The internal types are reached through the test assembly's `InternalsVisibleTo` access. The
integration of the fold with the later stages (the title, the padding, the axes, the scales, the configuration, and
the marks reading values) is verified by the system tests `CanvasNetVegaLite_SystemIntegration_Params_*` and is not
repeated here.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own specification text and evaluates it in its own evaluator; the evaluation is
  in UTC, no files are read, and no state is shared between tests

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A reference that is not replaced at a site of the table, a row-bound reference that is folded, a copy of a
subtree that
holds no reference, a value of the wrong kind or at the wrong location, a missing warning, or a limit that is not
enforced constitutes a failure.

#### Unit-Level Test Scenarios

##### Specification Without References Is Returned As It Is

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_WithoutReferences_ReturnsTheSameTree`

Asserts that a specification that holds no static reference gives back the same tree object with no warning.

##### Reference Is Replaced By Its Value

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_ReplacesAReferenceByItsValue`,
`ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_ReadsTheValuesOfTheCaller`

Asserts that a reference is replaced by the value of its expression with the parameters at their initial state, that the
expression of a parameter wins over its value and a parameter can read another one, that a value of the caller replaces
the whole definition of the parameter (its expression is not evaluated), also a structured value (an array of two
numbers), and that a folded reference reads it.

##### Every Site Of The Table Folds

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_EverySiteOfTheTableFolds`,
`ExpressionReferenceFolderTests.ExpressionReferenceSites_PropertiesOf_MatchesTheSchemaCounts`

Asserts, as a theory over every property of every owner of the table (the title, mark, axis, legend, header, scale,
scale configuration, view background, channel definition, root view, and configuration properties), that a reference
to `7` is found and folded to the number 7 (the text `7` for a property that takes a text) at the pointer of the
reference; and that the table holds the counts of properties of the version 6 schema (a title with the anchor, a
mark, an axis, a legend, a header, a scale, the scale configuration, and a view background).

##### Nested Views And The Configuration

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_NestedViewsFoldTheirOwnSites`,
`ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_FoldsTheConfiguration`,
`ExpressionReferenceFolderTests.ExpressionReferenceSites_Find_ReadsTheConfiguration`

Asserts that the layers, the views of a concatenation, and the template of a facet fold their own title, mark, guide,
and
scale properties while only the top-level view folds a padding, a background, and a configuration (the padding of a
nested
view stays a reference); and that the top-level properties, the title, the axes (every `axis*` key), the legend, the
headers, the view, the scale configuration, the mark configurations (a mark type, `mark`, and a named style), and the
parts of a composite mark
of the `config` are sites, the named color ranges are not, and a configuration of a nested view is not read.

##### Sites That Are Not Walked

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceSites_Find_DoesNotEnterDataTransformsAndParameters`,
`ExpressionReferenceFolderTests.ExpressionReferenceSites_Find_ASelectionHasNoExpressionSite`,
`ExpressionReferenceFolderTests.ExpressionReferenceSites_Find_ASelectionOfANestedViewHasNoExpressionSite`

Asserts that an inline data row with an `expr` key, a predicate, a transform, and a parameter definition are not
walked, so none of them is a site. Asserts also that a selection holds no site, because Vega-Lite's schema (6.5.0) gives
its brush `mark` (`BrushConfig`), events, projection, and initial value plain values only: a reference in
`config.selection`, in the `select` or the value of a selection parameter, in an entry of the legacy `selection`, and in
a selection of a nested view is not listed, not compiled, and left in place by the folder (the same tree, no warning).

##### Row-Bound Sites Keep Their Reference

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_LeavesTheRowBoundSitesToTheMarks`,
`ExpressionReferenceFolderTests.ExpressionReferenceSites_Find_ThePaintPropertiesOfAMarkAreRowBound`,
`ExpressionReferenceFolderTests.ExpressionReferenceSites_Find_AConditionHasNoStaticValue`

Asserts, with a symbol mark, that the color, fill, stroke, opacity, stroke width, and size of the mark and the value
of a
color channel keep their reference, are not listed as static references, and give back the same tree, as a theory over
the paint properties (the sites include the property, the parser compiled it as a row-bound expression); and that the
`value` and `datum`
of a condition are not static while the scale and the guides of its field are (the `nice` of the scale is folded).

##### Entries Of A Scale Range

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_FoldsTheEntriesOfAScaleRange`

Asserts that an expression reference that is an entry of a scale `range` array is folded (numbers and texts), as are
`rangeMax` as a whole; the Vega-Lite 6.5.0 schema gives the whole `range` no expression reference, so none is a site.

##### Reference That Reads The Datum Stays

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_AReferenceThatReadsTheDatumStaysInPlace`

Asserts that a reference that reads `datum` (a font size) stays a reference, with no warning (the feature catalog
reports it), while another reference of the same specification is folded.

##### Dates And Values With No Form

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_ADateIsFoldedOnlyInADomain`,
`ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_AValueWithNoFormStaysAReference`

Asserts that a date result is folded to its milliseconds in the whole domain and in an entry of the domain of a time
scale, that a date as a font size stays a reference with a `vl-expr-ref` warning located at it, and that a regular
expression (also inside an array) stays a reference with a warning.

##### An Extent Parameter Is Null At A Static Site And Reported

**Tests**:
`ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_AnExtentParameterIsNullAtAStaticSiteAndReported`,
`VegaLiteStatTransformsIntegrationTests.CanvasNetVegaLite_SystemIntegration_StatTransforms_StaticSitesSeeNoExtentAndAreReported`

Asserts that a title property that is an expression reference reading the parameter of an `extent` transform is folded
before the data is read and delivers exactly one `vl-param-extent-static` warning (a warning, not an unsupported
feature)
located at the reference (`/title/fontSize`), that the message names the parameter, and, through a render, that the
title sees null and strict mode does not stop.

##### Kinds Of Values

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_FoldsEveryKindOfValue`,
`ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_TheTextOfATitleIsText`

Asserts that a string, a boolean, null, an array, and an object fold to the node of their kind at the sites of several
owners, that a number that is not finite folds to null, and that the `text` of a title takes the JavaScript text of a
number or a boolean, an array is read line by line, and null stays null.

##### Untouched Subtrees Keep Their Identity

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_KeepsUntouchedSubtreesAndLocatesTheValues`

Asserts that the subtrees that hold no reference are the very same nodes after the fold, that the nodes on the way to a
reference are copies that read like the original, and that a folded value has the location of the reference it replaces.

##### Limits Are Enforced At The Reference

**Tests**: `ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_TheStepBudgetIsEnforcedAtTheReference`,
`ExpressionReferenceFolderTests.ExpressionReferenceFolder_Fold_ALongTextIsALocatedError`

Asserts that an expression that needs more steps than the budget of two is a `VegaLiteRenderException` located at the
reference, and that a parameter holding a text of 50 characters with a string limit of 40 is a
`VegaLiteRenderException` located at the reference.
