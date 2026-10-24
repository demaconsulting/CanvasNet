package CanvasNet {
    part def Mesh {
        doc /* The public Mesh triangle-mesh type, the MeshBuilder welding builder, and the internal MeshTopology analyzer: a validated, defensively copied vertex/triangle-index store with a counter-clockwise-outward winding contract, cached bounds, closed/orientable-manifold detection and per-defect topology classification (boundary edge, non-manifold edge, inconsistent winding, degenerate triangle) computed on position-welded indices (so an unwelded STL-style mesh is closed), per-shell inverted-mesh detection (connected shells judged by signed volume against nesting-depth parity, so hollow solids are valid and a disjoint inside-out shell is reported), signed volume and surface area, winding-correct Transform (a negative-determinant transform reverses every triangle), and Reverse. */

        comment sourceRef /* Source: src/DemaConsulting.CanvasNet.Render3D/SceneModel/Mesh.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/MeshBuilder.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/MeshTopology.cs, src/DemaConsulting.CanvasNet.Render3D/SceneModel/MeshTopologyDefects.cs */
        comment testRef /* Test: test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/MeshTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/MeshBuilderTests.cs, test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/MeshTopologyTests.cs */
        comment designRef /* Design: docs/design/canvas-net-render3d/scene-model/mesh.md */
        comment verificationRef /* Verification: docs/verification/canvas-net-render3d/scene-model/mesh.md */
        comment reqRef /* Requirements: docs/reqstream/canvas-net-render3d/scene-model/mesh.yaml */
    }
}
