### ImagePosition Unit Verification Design

This document describes the unit-level verification strategy for the `ImagePosition` unit, in the
`DemaConsulting.CanvasNet.Positions` namespace: the `ImagePosition` record and its value types `PositionBox`,
`PositionPoint`, `PositionValue`, and `PositionValueKind`.

#### Verification Approach

The unit is verified by building positions, boxes, points, and values with the public constructors, initializers, and
factories and comparing the results with values worked out by hand: the facts a position keeps, the refusals of its
init setters, the sorted copy of its values, the typed equality and the text form of a value, and the center, corners,
and enclosing box of a box. The unit has no dependencies beyond the .NET base library, so no mocking or stubbing is
required. Tests reside in `ImagePositionTests.cs` in the `Positions` folder of the `DemaConsulting.CanvasNet.Tests`
project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None required
- **Isolation**: Pure values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A fact that is not kept, an invalid value that is accepted, a value list that follows the caller's list, an
equality that ignores the kind, or a wrong exception type constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Positions-ImagePosition-Validation: Kind, Id, Line, Index, And Geometry Are Checked

**Tests**: `ImagePosition_Constructor_ValidKindAndId_KeepsEveryFact`, `ImagePosition_Constructor_BadKind_Throws`,
`ImagePosition_Properties_InvalidValues_Throw`

Asserts that a `node` position with the id `send` keeps its text, line 10, index 0, box, and pixel and has no values;
that, as a theory, an empty kind, `Node`, `1node`, `node id`, and a kind ending with a line feed throw
`ArgumentException`; and that a null id throws `ArgumentNullException`, a line of 0 and an index of -1 throw
`ArgumentOutOfRangeException`, and a box of negative width, a box with a NaN coordinate, and a pixel with an infinite
coordinate throw `ArgumentException`.

##### CanvasNet-Positions-ImagePosition-Values: Values Are A Sorted Copy

**Tests**: `ImagePosition_Values_CallerListChanges_PositionKeepsSortedCopy`,
`ImagePosition_Values_DuplicateOrEmptyName_Throws`

Asserts that values given as `y` then `x` are kept as `x` then `y` after the caller's list is cleared, that `y` is
found with the number 43 and `Y` is not found; and that a duplicate name and an empty name throw `ArgumentException`.

##### CanvasNet-Positions-ImagePosition-TypedEquality: Values Compare By Kind And Value

**Tests**: `PositionValue_Equals_TypedEquality_ComparesKindAndValue`,
`PositionValue_FromNumber_NonFinite_ThrowsAndTextIsInvariant`,
`PositionValue_TryFromObject_Scalars_ConvertAndListsAreRefused`

Asserts that the number 3 equals 3.0 and not the text "3", that null equals the default value and not the text
"null", that `Nov` differs from `nov`, that `true` differs from the number 1, that 0 equals -0 with the same hash
code, and the `==` and `!=` operators; that a NaN number throws `ArgumentOutOfRangeException` and that the text forms
are `1.5`, `true`, `null`, and `a b`; and that a double, an integer, null, a string, and a boolean are converted with
their kinds and values while a list and a NaN number are refused.

##### CanvasNet-Positions-ImagePosition-Geometry: Boxes And Points Are Pixel Geometry

**Tests**: `PositionBox_EncloseAndCenter_ComputesBounds`, `ImagePosition_Properties_InvalidValues_Throw`

Asserts that the box (10, 20, 30, 40) has the center (25, 40) and the third corner (40, 60), that the points (5, 9),
(-1, 12), and (3, 2) are enclosed in the box (-1, 2, 6, 10), and that enclosing no point throws `ArgumentException`;
and that a position refuses a box of negative size or with a NaN coordinate and a pixel that is not finite.
