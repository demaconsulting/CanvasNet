### SceneModel Unit Verification Design

This document describes the unit-level verification strategy for the `SceneModel` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the immutable scene records (`SceneItem` and its subtypes
`SceneGroup`, `SceneRect`, `SceneRule`, `ScenePath`, `SceneSymbol`, and `SceneText`, with `ScenePoint` and `SceneClip`),
the `Scene` record with its deterministic enumeration, role lookup, and item count (`SceneEntry`), the `SceneBounds`
bounding box helper, and the internal scene item budget. The records are the assertion surface of the stages before the
painter: they carry resolved geometry and paint as plain data, with colors as CSS strings.

#### Verification Approach

The unit is verified by building small scenes from the records and asserting on their structure, order, defaults, and
immutability. No mocking or stubbing is required: the unit has no dependencies other than the BCL; the internal types
are reached through the test assembly's `InternalsVisibleTo` access.

The expected enumeration is derived by hand from the rule that parents come before children and siblings keep their list
order, using the scene root > view (at 10, 20) > [bar, axis group (at 0, 100) > [tick, tick, label], symbol]: the roles
in drawing order are `root`, `view`, `mark-bar`, `axis-x`, `axis-tick`, `axis-tick`, `axis-label`, `mark-point`, the
depths are 0, 1, 2, 2, 3, 3, 3, 2, and the offsets accumulate the translations of the enclosing groups only (the view
adds (10, 20), the axis group adds (0, 100), so the ticks and the label are at offset (10, 120) while the item's own
position is not included). The bounds expectations are exact arithmetic: the box (0, 0, 10, 4) translated by (5, 6) is
(5, 6, 15, 10); rotated by 90 degrees about the origin it is (-4, 0, 0, 10), by -90 or 270 degrees (0, -10, 4, 0), and
by 180 degrees (-10, -4, 0, 0), with no rounding noise because quarter turns are exact; the empty box is the identity
of the union.

Unit tests reside in `SceneModelTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure construction and traversal of in-memory records; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong order, depth, offset, default value, copy semantics, bounds value, or exception type constitutes a
failure.

#### Unit-Level Test Scenarios

##### Deterministic Order, Offsets, And Role Lookup

**Tests**: `SceneModelTests.SceneModel_Create_GroupsAndItemsPreservesDeterministicOrder`

Asserts the derived enumeration order (identical on every call), item count 8, depths, and accumulated offsets; that
items are found by role (the first match, all matches, by type, and nothing for a missing role); and that the canvas
width, height, and background are kept.

##### Lists Are Copied And Read-Only

**Tests**: `SceneModelTests.SceneModel_Create_ListsAreCopiedAndReadOnly`

Asserts that the children, dash, text lines, and path points are copied when set, so that later changes to the source
lists have no effect; that the text of a multi-line item is the lines joined by a line feed; and that the exposed lists
throw `NotSupportedException` when changed through a cast, so that a scene never changes after it is built.

##### Defaults Follow Vega And With-Copies Do Not Alias

**Tests**: `SceneModelTests.SceneModel_With_DefaultsAndCopiesFollowVega`

Asserts the defaults (no paint, full opacity, fill opacity, and stroke opacity, the default stroke width unset, a corner
radius of 0; text of 11 pixels, left aligned, with an alphabetic baseline, no limit, no angle, and the ellipsis
character; a circle symbol of size 64) and that a `with` copy changes only what it names and leaves the original
unchanged.

##### Invalid Arguments

**Tests**: `SceneModelTests.SceneModel_Create_InvalidArgumentsAreRejected`

Asserts that a blank role throws `ArgumentException` and a null role `ArgumentNullException`, that a negative or NaN
canvas size throws `ArgumentOutOfRangeException`, and that a null root and a null group to enumerate throw
`ArgumentNullException`.

##### Scene Bounds

**Tests**: `SceneModelTests.SceneBounds_UnionTranslateAndRotate_AreExact`

Asserts the derived union, translation, and quarter-turn results, that the empty box has zero width and stays empty
under translation and rotation, and that it is the identity of the union.

##### Scene Item Budget

**Tests**: `MarkBuilderTests.MarkBuilder_Build_LimitsCancellationAndUnsupportedMarks`,
`CanvasNetVegaLite_SystemIntegration_Limits_ChartFailuresCarryLocations`

The budget (`SceneBudget`) is an internal helper that counts the scene items a builder creates against `MaxSceneItems`;
it has no behavior of its own beyond that counting, so it is verified through its users: a mark builder with a limit of
3 fails with a `VegaLiteRenderException` that names the limit and points at the mark, and a document with more rows than
the budget allows fails with the same exception, with the reports found before the failure delivered, while rows that
draw nothing cost nothing (`CanvasNetVegaLite_SystemIntegration_Limits_RowsThatDrawNothingCostNoBudget`).
`SceneBudgetTests` also verifies the exact count (the limit itself is allowed) and that the count saturates at the
largest `long` rather than wrapping around (`SceneBudget_Add_CountsExactlyAndFailsBeyondTheLimit`,
`SceneBudget_Add_SaturatesInsteadOfOverflowing`). Every scene item is charged before it is created and a limit of N
allows exactly N items; that boundary is verified through the whole pipeline by the system tests listed in
_CanvasNetVegaLite System Verification_ ("Limits: Scene Item Limit Is Exact And Charged Before Creation"), and the fork
and adopt used to charge the repeated axis passes by
`CanvasNetVegaLite_SystemIntegration_Limits_BudgetForkIsAdoptedOnlyWhenKept`.
