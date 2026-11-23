package CanvasNet_CornerRoundEffect {
    private import SoftwareItemMetadata::*;

    part def CornerRoundEffect {
        doc /* Path-level pre-processing unit that returns a new Path in which polyline corners (LineTo followed by LineTo) are replaced by tangent-radius arcs via PathBuilder.TangentArcTo, using kappa=0.5522847498. Corners involving curved segments are left unchanged; per-corner radius is clamped to half the shorter adjacent segment. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.CanvasNet/Geometry/CornerRoundEffect.cs");
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Geometry/CornerRoundEffectTests.cs");
            designFiles = ("docs/design/canvas-net/geometry/corner-round-effect.md");
            verificationFiles = ("docs/verification/canvas-net/geometry/corner-round-effect.md");
            requirementFiles = ("docs/reqstream/canvas-net/geometry/corner-round-effect.yaml");
        }
    }
}
