## Positions Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Positions` subsystem (namespace
`DemaConsulting.CanvasNet.Positions`): the `ImagePosition`, `PositionSelector`, and `PositionCollector` units and their
supporting types (`PositionBox`, `PositionPoint`, `PositionValue`, `PositionValueKind`, and `PositionSelection`).

### Verification Approach

The `Positions` subsystem is verified through its three units' tests (see *ImagePosition Unit Verification Design*,
*PositionSelector Unit Verification Design*, and *PositionCollector Unit Verification Design*, in the `positions`
folder): every public constructor, property, and method is exercised with positions built by hand, as a renderer would
build them, so no format package is needed in the core test project. The subsystem-level requirements reuse those unit
tests as verification evidence, because the units' only collaboration - `PositionCollector` evaluating the
`PositionSelector` instances of a `PositionSelection` against `ImagePosition` candidates - is exercised by the
`PositionCollector` tests with real selections and real positions, and the typed predicates of a selector are
exercised by the `PositionSelector` tests against real `ImagePosition` values.

The positions that real renders report (Mermaid diagrams and Vega-Lite charts), their mapping through the Viewer's
pipeline, and their output by the tool are verified by the tests of the `CanvasNetMermaid`, `CanvasNetVegaLite`,
`CanvasNetViewer`, and `CanvasNetTool` systems, because only those systems produce or consume them.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the positions, selections, and collectors are the real types
- **Isolation**: Each test builds its own positions, selectors, and collector; no files are read

### Acceptance Criteria

The `Positions` subsystem's verification passes when every scenario below and every unit test scenario described in
the three unit verification designs passes without error or unexpected exception.

### Test Scenarios

#### CanvasNet-Positions-Model: A Feature Is An Immutable Position

**Tests**: `ImagePosition_Constructor_ValidKindAndId_KeepsEveryFact`,
`ImagePosition_Values_CallerListChanges_PositionKeepsSortedCopy`,
`PositionValue_Equals_TypedEquality_ComparesKindAndValue`

Asserts that a position keeps its kind, id, text, source line, index, box, and pixel; that its values are a copy
sorted by name, unchanged when the caller's list is cleared and looked up by their exact name; and that two values are
equal only when their kinds and values are equal (the number 3 equals 3.0 and not the text "3").

#### CanvasNet-Positions-Selectors: Selectors Are Parsed, Refused, And Joined

**Tests**: `PositionSelector_Matches_Examples_MatchAsDocumented`,
`PositionSelector_Parse_Malformed_ThrowsFormatException`,
`PositionSelection_Parse_DuplicatesAndKinds_UnionAndKindGate`

Asserts, as theories, that every documented example selector (all, a kind, an id glob, a quoted exact id, and typed
predicates on channel values and on the `@id`, `@view`, `@from`, `@to`, and `@index` facts) matches exactly the
positions it should among a node, a node with glob characters in its id, an edge, and a chart mark; that every
malformed selector throws `FormatException` whose message names the selector; and that a selection keeps a repeated
selector once, matches a position that any selector matches, and tells which kinds it can match.

#### CanvasNet-Positions-Collection: The Matches Of One Render Are Kept In Order Up To The Cap

**Tests**: `PositionCollector_Offer_SeveralSelectors_KeepsUnionInDeliveryOrder`,
`PositionCollector_Offer_MoreMatchesThanTheCap_KeepsFirstAndTruncates`,
`PositionCollector_GetUnmatchedSelectors_MatchedButCapped_IsNotUnmatched`

Asserts that a position matched by several selectors is kept once and in the order it was offered whatever the order
of the selectors; that of a title and 1,500 marks interleaved with 1,500 unwanted items, the title and the first 999
marks are kept (the cap of 1,000 applied after the selection) and `Truncated` is set; and that a selector whose matches
were all cut by the cap is not unmatched while one that matched nothing is named.
