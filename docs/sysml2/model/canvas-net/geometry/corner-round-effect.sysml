package CanvasNet {
    part def CornerRoundEffect {
        doc /* Path-level pre-processing unit that returns a new Path in which polyline corners (LineTo followed by LineTo) are replaced by tangent-radius arcs via PathBuilder.TangentArcTo, using kappa=0.5522847498. Corners involving curved segments are left unchanged; per-corner radius is clamped to half the shorter adjacent segment. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet/Geometry/CornerRoundEffect.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Tests/Geometry/CornerRoundEffectTests.cs */
        comment designRef /* Design: docs/design/canvas-net/geometry/corner-round-effect.md */
        comment verificationRef /* Verification: docs/verification/canvas-net/geometry/corner-round-effect.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net/geometry/corner-round-effect.yaml */
    }
}
