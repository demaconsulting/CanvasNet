package CanvasNetRender3D_SceneModelSubsystem {
    private import SoftwareItemMetadata::*;
    public import CanvasNetRender3D_DoublePrecisionMath::*;
    public import CanvasNetRender3D_Mesh::*;
    public import CanvasNetRender3D_SceneGraph::*;

    part def SceneModel {
        doc /* The format-agnostic, immutable 3D data model in the DemaConsulting.CanvasNet.Render3D.SceneModel namespace: double-precision Vector2D/Vector3D/Matrix4x4D/BoundingBox3D math, validated triangle Mesh instances with the MeshBuilder welding builder, and the Scene graph of MeshNode/GroupNode/CsgNode nodes carrying double-precision transforms and optional NodeAppearance colors, where a CsgNode combines its children as a union, difference, or intersection (constructive solid geometry). As of this release, its constituent units are DoublePrecisionMath, Mesh, and SceneGraph. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.CanvasNet.Render3D.Tests/SceneModel/SceneModelTests.cs");
            designFiles = ("docs/design/canvas-net-render3d/scene-model.md");
            verificationFiles = ("docs/verification/canvas-net-render3d/scene-model.md");
            requirementFiles = ("docs/reqstream/canvas-net-render3d/scene-model.yaml");
        }

        part doublePrecisionMath : DoublePrecisionMath;
        part mesh : Mesh;
        part sceneGraph : SceneGraph;
    }
}
