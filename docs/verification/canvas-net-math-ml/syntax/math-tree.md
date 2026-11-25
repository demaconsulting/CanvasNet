### MathTree Unit Verification Design

This document describes the unit-level verification strategy for the `MathTree` unit in the
`DemaConsulting.CanvasNet.MathMl.Syntax` namespace.

#### Verification Approach

The unit is verified by building elements, text runs, and attribute values directly (through the test helpers
`Token` and `Node`) with the test assembly's `InternalsVisibleTo` access and asserting their members and their value
semantics. Nothing is mocked; the records have no dependency to replace. Unit tests reside in `Syntax/MathTreeTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A child, a text, an attribute value, or an equality that
differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Syntax-MathTree-Model: Elements, Texts, And Attributes

**Tests**: `MathTreeTests.MathTree_Element_ExposesChildrenTextAndAttributes`,
`MathTreeTests.MathTree_IsTextTokenName_KnowsTheTokenElements`,
`MathTreeTests.MathTree_TextAndAttribute_HaveValueEquality`

Asserts the element children (text skipped), the concatenated text content, the value of a present and of an absent
attribute; that `mi`, `mn`, `mo`, `mtext`, and `ms` are text tokens and `mspace`, `mrow`, `math`, and `MI` (names are
case-sensitive) are not; and that text runs and attribute values with equal content and location compare equal.
