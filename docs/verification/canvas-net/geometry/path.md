## Path Unit Verification Design

This document describes the unit-level verification strategy for the `Path` and `PathBuilder`
classes (and their supporting `Subpath`, `PathCommand`, and `PathCommandType` data types).

### Verification Approach

The `Path`/`PathBuilder` unit is verified through unit tests that exercise every fluent builder
method, the `InvalidOperationException` guards, `Build()`'s snapshot semantics, `Clear()`,
`Path.Empty`, and both modes of `Path.GetBounds`, in isolation. Expected bounding rectangles are
hand-computed independently of the implementation for both the conservative (control-point
convex-hull) mode and the flattening-based tighter mode.

Unit tests reside in `PathBuilderTests.cs` within the `DemaConsulting.CanvasNet.Tests.Geometry`
project namespace.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `Path`/`PathBuilder` have no injectable dependencies

### Unit-Level Test Scenarios

#### CanvasNet-Geometry-Path-BuildEveryCommandType / -ArcToStoresRawParameters: Every Command Recorded

**Test**: `PathBuilder_Build_EveryCommandType_RecordsCommandsInOrder`

Issues one of each command type (`LineTo`, `QuadraticBezierTo`, `CubicBezierTo`, `ArcTo`, `Close`)
in sequence and asserts `Build()` returns a `Path` whose single `Subpath.Commands` list contains
each command, in order, with every field (including the raw, unconverted SVG arc parameters on
the `ArcTo` command) exactly matching the values supplied.

#### CanvasNet-Geometry-Path-MultipleSubpaths: Independent Subpaths

**Tests**: `PathBuilder_Build_MultipleSubpaths_PreservesEachIndependently`,
`PathBuilder_MoveTo_WhilePreviousSubpathOpen_CommitsPreviousSubpathAsOpen`

Issues two separate `MoveTo`-started subpaths, each with its own distinct commands, and asserts
`Build()` returns two subpaths whose commands do not cross-contaminate. Separately, issues a
`MoveTo` while the previous subpath is still open (no `Close`) and asserts the previous subpath is
committed with `IsClosed == false`.

#### CanvasNet-Geometry-Path-Close: Close Marks the Subpath Closed

**Test**: `PathBuilder_Close_OpenSubpath_MarksSubpathClosedWithCloseCommand`

Issues `Close` on an open subpath and asserts the resulting `Subpath.IsClosed` is `true` and its
final command is a `Close` command.

#### CanvasNet-Geometry-Path-MoveToAfterClose: MoveTo After Close Starts a New Subpath

**Test**: `PathBuilder_MoveTo_AfterClose_StartsNewSubpath`

Issues `Close`, then `MoveTo`, and asserts a new, independent subpath begins at the new `MoveTo`
point.

#### CanvasNet-Geometry-Path-GuardBeforeMoveTo: Guards Before the First MoveTo

**Tests**: `PathBuilder_LineTo_BeforeFirstMoveTo_ThrowsInvalidOperationException`,
`PathBuilder_EveryDrawingCommand_BeforeFirstMoveTo_ThrowsInvalidOperationException`

Calls `LineTo` on a freshly constructed builder (before any `MoveTo`) and asserts
`InvalidOperationException` is thrown. Separately, repeats this for every drawing command
(`LineTo`, `QuadraticBezierTo`, `CubicBezierTo`, `ArcTo`, `Close`) and asserts each throws the same
exception.

#### CanvasNet-Geometry-Path-GuardAfterCloseWithoutMoveTo: Guards After Close Without an Intervening MoveTo

**Test**: `PathBuilder_LineTo_AfterCloseWithoutMoveTo_ThrowsInvalidOperationException`

Calls `Close`, then `LineTo` without an intervening `MoveTo`, and asserts
`InvalidOperationException` is thrown.

#### CanvasNet-Geometry-Path-BuildSnapshotIndependence: Build Returns an Independent Snapshot

**Test**: `PathBuilder_Build_ThenMutateBuilder_DoesNotAffectPreviouslyBuiltPath`

Builds a `Path`, then issues further commands (including a new `MoveTo`/subpath) on the same
builder, and asserts the previously built `Path` instance's `Subpaths` are unchanged.

#### CanvasNet-Geometry-Path-BuildEmpty: Build With No Commands Returns No Subpaths

**Test**: `PathBuilder_Build_NoCommandsIssued_ReturnsPathWithNoSubpaths`

Calls `Build()` on a freshly constructed builder and asserts the returned `Path.Subpaths` is
empty.

#### CanvasNet-Geometry-Path-Clear: Clear Resets the Builder

**Test**: `PathBuilder_Clear_AfterBuildingAPath_ResetsBuilderToEmptyState`

Builds a non-trivial `Path`, calls `Clear()`, then calls `Build()` again and asserts the second
`Path` has no subpaths, confirming the builder returned to its initial empty state.

#### CanvasNet-Geometry-Path-EmptySingleton: Path.Empty Has No Subpaths and Empty Bounds

**Test**: `Path_Empty_HasNoSubpathsAndEmptyBounds`

Asserts `Path.Empty.Subpaths` is empty and `Path.Empty.GetBounds()` equals `Rect.Empty`.

#### CanvasNet-Geometry-Path-GetBoundsConservative: Conservative Mode Returns the Control-Point Convex Hull

**Test**: `Path_GetBounds_ConservativeMode_CubicWithControlPointsOutsideChord_ReturnsControlPointConvexHull`

Builds a path containing a cubic Bezier curve whose control points extend outside the chord
between its endpoints, calls `GetBounds()` with the default (non-positive) tolerance, and asserts
the result exactly equals the hand-computed bounding rectangle of all four control
points/endpoints (not the tighter true curve bounds).

#### CanvasNet-Geometry-Path-GetBoundsFlattened: Flattening Mode Is Tighter Than Conservative Mode

**Test**: `Path_GetBounds_FlattenMode_TighterThanConservativeMode_ForCurveWithWideControlPolygon`

Builds a path containing a curve with a wide control polygon (control points well outside the
true curve), calls `GetBounds()` both with the default tolerance and with a small positive
tolerance, and asserts the flattening-mode result is strictly smaller (tighter) than the
conservative-mode result while still containing the true curve.

#### CanvasNet-Geometry-Path-GetBoundsMultipleSubpaths: Bounds Union Multiple Subpaths

**Test**: `Path_GetBounds_MultipleSubpaths_UnionsAllSubpaths`

Builds a path with two disjoint subpaths at different locations, calls `GetBounds()`, and asserts
the result exactly equals the hand-computed union of each subpath's own bounds.

#### CanvasNet-Geometry-Path-Immutability: Subpaths and Commands Resist Downcast Mutation

**Tests**: `Path_Subpaths_DowncastToIList_ThrowsNotSupportedExceptionOnMutation`,
`Subpath_Commands_DowncastToIList_ThrowsNotSupportedExceptionOnMutation`

Builds a `Path`, downcasts its `Subpaths` (respectively a `Subpath`'s `Commands`) from the
compile-time `IReadOnlyList<T>` view to the mutable `IList<T>` interface that its underlying
`ReadOnlyCollection<T>` still implements, and asserts every mutating member exercised (`Add`,
`RemoveAt`, `Clear`) throws `NotSupportedException` regardless of the cast - proving `Path` and
`Subpath` cannot be mutated in place even by a caller deliberately bypassing the read-only
compile-time type.

### Acceptance Criteria

A unit test run passes when every scenario above passes without error or unexpected exception.
