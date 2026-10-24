package CanvasNet {
    part def Extruder {
        doc /* The public static partial Extruder class: Linear (extrusion of a PolygonRegion along +Z with optional centering, counter-clockwise twist, per-axis or uniform scale, and a single apex when the scale is zero) and Rotate (revolution of a profile about the Z axis, profile X as radius and Y as height, full or partial sweep, axis vertices welded, a profile crossing the axis rejected), each producing a closed, outward-oriented, welded mesh that is a valid constructive-solid-geometry operand; a hole is extruded as one connected shell with a through-hole. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/Tessellation/Extruder.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/Extruder.Linear.cs, src/DemaConsulting.CanvasNet.Render3D/Tessellation/Extruder.Rotate.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/ExtruderLinearTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/ExtruderRotateTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/tessellation/extruder.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/tessellation/extruder.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/tessellation/extruder.yaml */
    }
}
