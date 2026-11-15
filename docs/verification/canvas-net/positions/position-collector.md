### PositionCollector Unit Verification Design

This document describes the unit-level verification strategy for the `PositionCollector` unit, in the
`DemaConsulting.CanvasNet.Positions` namespace: the collection of the positions of one render that a selection
matches.

#### Verification Approach

The unit is verified by offering positions built by hand to collectors over real selections, in the order a renderer
would deliver them, and asserting the kept positions, the `Truncated` flag, and the unmatched selectors. The unit
depends only on the `ImagePosition` and `PositionSelector` units, which are real, so no mocking or stubbing is
required. Tests reside in `PositionCollectorTests.cs` in the `Positions` folder of the `DemaConsulting.CanvasNet.Tests`
project.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Mocking**: None required
- **Isolation**: Each test builds its own collector and positions; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A position kept twice or out of its delivery order, a cap that is exceeded or applied before the selection, a
`Truncated` flag set when nothing was left out, or an unmatched selector that is not named constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNet-Positions-PositionCollector-Union: Each Match Once, In Delivery Order

**Tests**: `PositionCollector_Offer_SeveralSelectors_KeepsUnionInDeliveryOrder`

Offers the nodes `a` and `b`, the edge `e`, and the note `n` to a collector over `node:a`, `node`, `edge` and to one
over the same selectors in reverse order, and asserts that both keep `a`, `b`, and `e` once each and in that order,
with no unmatched selector and no truncation.

##### CanvasNet-Positions-PositionCollector-Cap: At Most The Cap, Truncated Only When A Match Was Cut

**Tests**: `PositionCollector_Offer_MoreMatchesThanTheCap_KeepsFirstAndTruncates`,
`PositionCollector_Offer_ExactlyTheCap_IsNotTruncated`, `PositionCollector_Constructor_BadArguments_Throw`

Offers a title and then 1,500 marks interleaved with 1,500 axes to a collector over `title` and `mark` with the
default cap, and asserts that `MaxPositions` (1,000) positions are kept, the first the title and the last `bar-998`,
and that `Truncated` is set. Asserts that a cap of 3 offered three nodes and an edge is truncated while one offered
exactly three nodes is not and keeps them; and that a null selection throws `ArgumentNullException`, a cap of 0
throws `ArgumentOutOfRangeException`, and `IncludesKind` follows the selection.

##### CanvasNet-Positions-PositionCollector-Unmatched: Selectors That Matched Nothing Are Named

**Tests**: `PositionCollector_GetUnmatchedSelectors_MatchedButCapped_IsNotUnmatched`

Offers a node and an edge to a collector over `node`, `edge`, and `node:Missing` with a cap of 1, and asserts that one
position is kept, that `Truncated` is set, and that only `node:Missing` is unmatched: the edge selector, whose match was
cut by the cap, is not.
