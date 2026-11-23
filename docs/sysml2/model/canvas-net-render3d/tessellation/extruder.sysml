package CanvasNetRender3D_Extruder {
    private import SoftwareItemMetadata::*;

    part def Extruder {
        doc /* The public static partial Extruder class: Linear (extrusion of a PolygonRegion along +Z with optional centering, counter-clockwise twist, per-axis or uniform scale, and a single apex when the scale is zero) and Rotate (revolution of a profile about the Z axis, profile X as radius and Y as height, full or partial sweep, axis vertices welded, a profile crossing the axis rejected), each producing a closed, outward-oriented, welded mesh that is a valid constructive-solid-geometry operand; a hole is extruded as one connected shell with a through-hole. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/Extruder.cs",
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/Extruder.Linear.cs",
                "src/DemaConsulting.CanvasNet.Render3D/Tessellation/Extruder.Rotate.cs"
            );
            testFiles = (
                "test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/ExtruderLinearTests.cs",
                "test/DemaConsulting.CanvasNet.Render3D.Tests/Tessellation/ExtruderRotateTests.cs"
            );
            designFiles = ("docs/design/canvas-net-render3d/tessellation/extruder.md");
            verificationFiles = ("docs/verification/canvas-net-render3d/tessellation/extruder.md");
            requirementFiles = ("docs/reqstream/canvas-net-render3d/tessellation/extruder.yaml");
        }
    }
}
