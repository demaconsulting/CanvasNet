# System Verification Design

<!-- cspell:ignore rasterizer tessellator supersampling Lambertian unmirrored isometric -->

This document describes the system-level verification strategy for CanvasNetRender3D.

## Verification Approach

`CanvasNetRender3D` is verified at three tiers. The system tier, described here, exercises the
library purely through its public API from the perspective of a consumer: it builds scenes with the
public node types, `MeshBuilder`, `PrimitiveTessellator`, and `Extruder`, renders them with `SceneRenderer`, and
asserts on observable outputs — returned values, thrown exceptions, and pixel values of the returned
`Surface`. No mocking or stubbing is used. The subsystem tier (see _SceneModel Subsystem
Verification_, _Tessellation Subsystem Verification_, and _SceneRendering Subsystem Verification_
under `canvas-net-render3d/`) verifies each subsystem's cross-unit behavior, and the unit tier
verifies each unit in isolation, including the internal types made visible to the test assembly
through `InternalsVisibleTo`.

Because a software rasterizer's correctness is a numeric property, the system scenarios use
**analytic oracles** rather than golden images: pixel coverage and colors are compared with values
computed independently from the projection formula, the lighting equation, or the geometry (for
example, a cube viewed head-on from a known distance must cover a number of pixels within a
tolerance of the analytically projected area, and a face lit by an ambient light and a headlight
must have the exact color `round(180 * (0.25 + 0.5))`). Where the contract is exact (determinism,
mirrored equivalence, numeric precision, exactly-once edge coverage) the scenarios compare entire
surfaces byte for byte. Constructive solid geometry is verified the same way: pixels identify the
visible surface through flat shading, and analytic oracles (a disc area, the clipped silhouette,
exact byte equality across degrees of parallelism) replace golden images; its budget and
operand-reporting contracts are verified through the public options and exceptions.

System tests reside in `Render3DSystemIntegrationTests.cs` within the
`DemaConsulting.CanvasNet.Render3D.Tests` project; the shared helpers (pixel counting, color-region
assertions, closed-mesh and outward-orientation assertions) are in `Render3DTestHelpers.cs`.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every target framework
  (`net8.0`, `net9.0`, `net10.0`) and on the Windows, Linux, and macOS CI runners
- **Dependencies**: No external services, databases, fixture files, or network access required
- **Isolation**: Each test method constructs its own scene, camera, and options; no shared state
  between tests. Rendering tests are small (at most a few hundred pixels per side), and any test
  that renders passes the xUnit test context's cancellation token to the renderer

## External Interface Simulation

The system has no external network, service, or file-system interfaces: every public API accepts
and returns only in-memory values (`Vector3D`, `Matrix4x4D`, `Mesh`, `Scene`, `Camera`, lights,
`SceneRenderOptions`, and the core `CanvasNet` `Rgba32` and `Surface`). No simulation of external
interfaces is required. Rendered output is verified by sampling specific `Surface` coordinates, by
counting pixels of a given color, and by byte-for-byte comparison of two surfaces — the same
coordinate-sampling convention the sibling systems' render tests use.

## System-Level Test Scenarios

### Scene: Transformed Group Bounds Match Analytic

**Test**: `CanvasNetRender3D_SystemIntegration_Scene_TransformedGroupBoundsMatchAnalytic`

Builds a unit-cube mesh scaled by two, rotated 90 degrees about Z, and translated, nested through
two group levels, and asserts that `Scene.ComputeBounds` equals the analytically transformed box.
Confirms double-precision transform composition through the public scene API.

### Scene: Invalid Input Is Rejected

**Test**: `CanvasNetRender3D_SystemIntegration_Scene_InvalidInputIsRejected`

Asserts the documented exceptions for a null scene root, a null mesh, a null group child, a
non-finite node transform, and a malformed (non-multiple-of-three) index list.

### Primitive Meshes: Box, Sphere, And Cylinder Are Closed With Analytic Volume

**Test**: `CanvasNetRender3D_SystemIntegration_PrimitiveMeshes_BoxSphereCylinderAreClosedWithAnalyticVolume`

Creates a box, a sphere, a cylinder, and a cone and asserts that each is closed with positive signed
volume matching the analytic (or polygon-corrected analytic) volume within the expected tessellation
error.

### Primitive Meshes: Polyhedron Preserves Supplied Winding

**Test**: `CanvasNetRender3D_SystemIntegration_PrimitiveMeshes_PolyhedronPreservesSuppliedWinding`

Builds a tetrahedron with every face listed clockwise from outside and asserts that it is closed
with a negative signed volume (the supplied winding is preserved, not corrected) that
`Mesh.Reverse` repairs, and that the same faces with one omitted yield an open mesh of three
triangles (no face is added to close it).

### Polygon Extrusion: Hole Extrusion Renders Through The Hole

**Test**: `CanvasNetRender3D_SystemIntegration_Extrusion_HoleExtrusionRendersThroughHole`

Extrudes a 20 by 20 square plate with a 48-gon hole of radius 5 (a single bridged shell) and
renders it on the z-buffer path from above, orthographically at 10 pixels per unit with flat colors.
Asserts that the center pixel is the background (the hole is open), that a pixel on the ring is the
node color, and that the covered-pixel count is within one percent of the analytic area (the square
minus the 48-gon, times the pixels per square unit). A second, oblique perspective render with the
default lighting rig asserts that the far inner wall of the hole is visible and shaded differently
from the top face.

### Polygon Extrusion: Extruded Shapes Are Valid CSG Operands

**Test**: `CanvasNetRender3D_SystemIntegration_Extrusion_ExtrudedShapesAreValidCsgOperands`

Renders the difference of the plate with a hole and a box through the fragment-list path with an
unsupported-feature handler and `ThrowOnUnsupportedFeature` set. Asserts that no exception is thrown
and no report is delivered (the extruded mesh is a valid operand), that the pocket floor appears in
the subtracted operand's color within three percent of the analytic pixel count, that the rest of
the plate keeps its color, and that the hole remains open.

### Render: Cube Silhouette Matches Analytic Coverage

**Test**: `CanvasNetRender3D_SystemIntegration_Render_CubeSilhouetteMatchesAnalyticCoverage`

Renders a two-unit cube viewed head-on from ten units away and asserts that the covered-pixel
count matches the analytically projected front-face area within tolerance. This is also the shared
platform-proof test referenced by every `CanvasNetRender3D-Platform-*` requirement, run on each
operating system and target framework.

### Render: Empty Scene Produces Background Only

**Test**: `CanvasNetRender3D_SystemIntegration_Render_EmptySceneProducesBackgroundOnly`

Renders an empty group and asserts a surface of the requested size filled with the background
color.

### Render: Invalid Arguments Throw

**Test**: `CanvasNetRender3D_SystemIntegration_Render_InvalidArgumentsThrow`

Asserts `ArgumentNullException` for a null scene or camera, `ArgumentOutOfRangeException` for a zero
width and for a height above `Surface.MaxDimension`, and `ArgumentException` for an orthographic
camera without a height.

### Camera: Fit To Bounds Frames Cube Within Margin

**Test**: `CanvasNetRender3D_SystemIntegration_Camera_FitToBoundsFramesCubeWithinMargin`

Renders a cube with a `FitToBounds` camera and asserts that nothing touches the frame border and
that the farthest silhouette point from the center sits near the 5 % margin boundary.

### Camera: Orthographic And Perspective Center Ray

**Test**: `CanvasNetRender3D_SystemIntegration_Camera_OrthographicAndPerspectiveCenterRay`

Renders a small cube with one camera of each projection and asserts that the center pixel shows the
cube and the corner pixels show the background in both.

### Lighting: Custom Lights Produce Analytic Shade

**Test**: `CanvasNetRender3D_SystemIntegration_Lighting_CustomLightsProduceAnalyticShade`

Renders a face with an ambient light plus a headlight (asserting the exact analytic color), with a
pure red ambient light (only the red channel survives), and with a world-space light traveling away
from the face (no diffuse contribution).

### Lighting: Default Rig Shades Faces In Expected Order

**Test**: `CanvasNetRender3D_SystemIntegration_Lighting_DefaultRigShadesFacesInExpectedOrder`

Renders an isometric view of a cube with no explicit lights and asserts the expected brightness
ordering of the top, left, and right faces produced by the default rig.

### Appearance: Default, Node, And Back-Face Colors Applied

**Test**: `CanvasNetRender3D_SystemIntegration_Appearance_DefaultNodeAndBackFaceColorsApplied`

Renders one cube without an appearance and one with a red appearance, plus an open quad seen from
behind, and asserts the neutral default color, the node color, and the back-face color.

### Appearance: Transparent Background Preserved

**Test**: `CanvasNetRender3D_SystemIntegration_Appearance_TransparentBackgroundPreserved`

Renders over a fully transparent background and asserts transparent corners, an opaque center, and
partially transparent edge pixels whose color channels are intact.

### Visibility: Nearer Surface Occludes Regardless Of Order

**Test**: `CanvasNetRender3D_SystemIntegration_Visibility_NearerSurfaceOccludesRegardlessOfOrder`

Renders a near and a far quad listed in both orders and asserts that the near quad is visible in
both.

### Visibility: Coincident Faces Resolve By Traversal Order

**Test**: `CanvasNetRender3D_SystemIntegration_Visibility_CoincidentFacesResolveByTraversalOrder`

Renders two overlapping coplanar quads of different colors and asserts that the overlap shows the
first-listed quad's color throughout, with no speckle.

### Visibility: Mirrored Cube Renders Identically To Original

**Test**: `CanvasNetRender3D_SystemIntegration_Visibility_MirroredCubeRendersIdenticallyToOriginal`

Renders a symmetric cube and the same cube under a mirror transform and asserts byte-identical
surfaces (a missing winding correction would paint every face with the back-face color).

### Visibility: Ground Plane Crossing Near Plane Renders Horizon

**Test**: `CanvasNetRender3D_SystemIntegration_Visibility_GroundPlaneCrossingNearPlaneRendersHorizon`

Places the camera above a large ground plane that extends behind the camera and asserts that all
rows below the horizon are ground, all rows above are background, and nothing else appears.

### Constructive Solid Geometry: Cube Minus Sphere Shows Cavity

**Test**: `CanvasNetRender3D_SystemIntegration_Csg_CubeMinusSphereShowsCavity`

Satisfies `CanvasNetRender3D-ConstructiveSolidGeometry`. Builds, with the public API only, a
10-unit blue cube and a red sphere of radius 3 centered on its top face, and renders the
`CsgNode` difference and intersection of the two from above with an orthographic camera at ten
pixels per unit and flat shading, so that a pixel's color identifies the visible surface. Asserts
that the cavity center of the difference is the cut surface in the subtrahend's own (red) color,
that a pixel on the cube's top ring is blue and the corner is the background, that the red area is
within the analytic disc-area range, and that the intersection shows the clipped silhouette (blue
inside the disc, background outside it). Confirms acceptance scenarios A1 (cube minus sphere cavity
pixels) and A2 (cube intersect sphere silhouette) end to end.

### Constructive Solid Geometry: Budget And Operand Reporting

**Test**: `CanvasNetRender3D_SystemIntegration_Csg_BudgetAndOperandReporting`

Satisfies `CanvasNetRender3D-ConstructiveSolidGeometry`. Renders a cube minus an open quad (not a
solid) and asserts, through the public API, that a small `MaxFragmentsPerTile` throws
`RenderBudgetExceededException` carrying the exceeded budget; that an
`UnsupportedFeatureHandler` receives exactly one report with the feature token
`render3d-csg-open-mesh` while the render succeeds; that `ThrowOnUnsupportedFeature` throws
`SceneRenderUnsupportedFeatureException` whose `Node` is the open quad; and that the same meshes in
a union-only group render normally even with a budget of one fragment and strict mode, proving that
union-only scenes use neither the budget nor the validator. Confirms acceptance scenarios A6
(budget) and A8 (operand reporting) and the union-only part of A9 end to end.

### Determinism: Constructive Solid Geometry Is Byte Identical

**Test**: `CanvasNetRender3D_SystemIntegration_Csg_DeterministicAcrossParallelismAndRuns`

Satisfies `CanvasNetRender3D-Determinism` for the CSG path. Renders a scene combining a cube minus
a flush (coincident-face) box minus a mirrored asymmetric solid, next to an intersected pair of
spheres, with a `FitToBounds` camera, for supersampling 1, 2, and 3, and asserts byte-identical
surfaces for a maximum degree of parallelism of 1, 2, and -1 and for a repeated automatic render
(acceptance scenario A10).

### Antialiasing: Half-Covered Pixel Blends To Mid Color

**Test**: `CanvasNetRender3D_SystemIntegration_Antialiasing_HalfCoveredPixelBlendsToMidColor`

Renders with 2 x 2 supersampling so that a quad edge bisects a pixel and asserts the exact blended
value `(2 * 180 + 2 * 255 + 2) / 4`.

### Numeric Precision: Object One Million Units Away Renders Identically

**Test**: `CanvasNetRender3D_SystemIntegration_NumericPrecision_ObjectOneMillionUnitsAwayRendersIdentically`

Renders the same cube and camera at the origin and translated by one million units and asserts
byte-identical surfaces.

### Determinism: Parallel And Sequential Renders Are Byte Identical

**Test**: `CanvasNetRender3D_SystemIntegration_Determinism_ParallelAndSequentialRendersAreByteIdentical`

Renders a multi-primitive scene with a maximum degree of parallelism of 1, 2, and -1 and asserts
byte-identical surfaces.

### Determinism: Repeated Render Is Byte Identical

**Test**: `CanvasNetRender3D_SystemIntegration_Determinism_RepeatedRenderIsByteIdentical`

Renders the same scene twice and asserts byte-identical surfaces.

### Cancellation: Pre-Cancelled Token Throws Operation Canceled

**Test**: `CanvasNetRender3D_SystemIntegration_Cancellation_PreCancelledTokenThrowsOperationCanceled`

Passes an already-cancelled token and asserts `OperationCanceledException` carrying that token and
not an `AggregateException`.

### Cancellation: Invalid Parallelism Is Rejected

**Test**: `CanvasNetRender3D_SystemIntegration_Cancellation_InvalidParallelismIsRejected`

Asserts `ArgumentOutOfRangeException` when constructing options with a maximum degree of
parallelism of zero or of a value below negative one.

## Acceptance Criteria

A system-level test run passes when every scenario above passes without error or unexpected
exception, on every target framework. Every requirement in `docs/reqstream/canvas-net-render3d.yaml`
and `docs/reqstream/canvas-net-render3d/platform-requirements.yaml` links to at least one passing
test listed above. Any unexpected exception, wrong exception type, or wrong return, pixel, or byte
value constitutes a failure.
