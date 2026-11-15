package CanvasNet {
    part def PositionCollector {
        doc /* The collection of the positions of one render: a PositionCollector is created over a PositionSelection with a cap MaxCount (at least 1, MaxPositions 1,000 by default). A renderer offers every candidate once, in its deterministic order; Offer evaluates every selector against the candidate (so a position matched by several selectors is kept once and the union is taken), keeps the first MaxCount matches in delivery order in Positions, and counts a later match without keeping it, setting Truncated. GetUnmatchedSelectors returns, in selection order, the selectors that matched no candidate (a selector whose matches were all cut by the cap still matched), and IncludesKind lets a renderer skip the kinds nobody asked for. The collector is mutable, belongs to one render, and is not thread-safe. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Positions/PositionCollector.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Positions/PositionCollectorTests.cs */
        comment designRef /* Design: docs/design/canvas-net/positions/position-collector.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/positions/position-collector.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/positions/position-collector.yaml */
    }
}
