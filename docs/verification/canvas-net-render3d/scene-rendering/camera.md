### Camera Unit Verification Design

<!-- cspell:ignore azimuth -->

This document describes the unit-level verification strategy for the `Camera` class,
`CameraProjection` enumeration, and the internal `ViewTransform` struct.

#### Verification Approach

The unit is verified by constructing cameras through initializers, `LookAt`, and `FitToBounds` and
asserting on the resulting properties, on the view matrix applied to known points, and on the
projection of the bounding-box corners through the fitted camera (a corner is inside the frustum
when its lateral offset does not exceed the depth times the tangent of the half field of view).
`ViewTransform` is verified through the test assembly's `InternalsVisibleTo` access. The unit has no
dependencies other than `DoublePrecisionMath`, so no mocking or stubbing is required. The
renderer-side rejection of an invalid cross-property camera (for example an orthographic camera
without a height) is verified under the _SceneRenderer Unit Verification Design_.

Unit tests reside in `CameraTests.cs` within the `DemaConsulting.CanvasNet.Render3D.Tests` project,
in the `SceneRendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test constructs its own cameras

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneRendering-Camera-LookAt: LookAt Sets The Camera And Validates

**Tests**: `CameraTests.Camera_LookAt_SetsEyeTargetUp`,
`CameraTests.Camera_LookAt_EyeEqualsTarget_Throws`,
`CameraTests.Camera_LookAt_TinyOffsetAndUp_IsAccepted`,
`CameraTests.Camera_LookAt_HugeOffsetAndUp_IsAccepted`,
`CameraTests.Camera_LookAt_TinyUpParallelToForward_SubstitutesAlternateUp`,
`CameraTests.Camera_GetViewMatrix_TinyOffsetAndUp_AcceptedAndZeroRejected`

Asserts that `LookAt` stores the eye, target, and up vectors, rejects an eye equal to the target,
and accepts tiny or huge (but non-zero) view offsets and up hints, including a scale-invariant
parallel up/forward substitution.

##### CanvasNetRender3D-SceneRendering-Camera-ViewMatrix: View Matrix Maps Eye, Target, And Up

**Tests**: `CameraTests.Camera_GetViewMatrix_MapsEyeToOriginAndTargetToNegativeZ`,
`CameraTests.Camera_GetViewMatrix_UpMapsToPositiveY`

Asserts that the view matrix maps the eye to the origin, the target to the negative Z axis at the
eye-target distance, and the up direction to positive Y.

##### CanvasNetRender3D-SceneRendering-Camera-Projection: Projection Defaults And Orthographic Height

**Tests**: `CameraTests.Camera_Defaults_PerspectiveWithDocumentedFieldOfView`,
`CameraTests.Camera_Orthographic_WithHeight_RetainsHeight`

Asserts the documented defaults (perspective projection, field of view 22.5 degrees) and that an
orthographic camera retains its height.

##### CanvasNetRender3D-SceneRendering-Camera-FitToBounds: FitToBounds Frames The Bounds Tightly

**Tests**: `CameraTests.Camera_FitToBounds_FrontView_EyeOnNegativeYAxis`,
`CameraTests.Camera_FitToBounds_AzimuthAndElevation_FollowOrbitConvention`,
`CameraTests.Camera_FitToBounds_Perspective_AllCornersProjectInsideFrame`,
`CameraTests.Camera_FitToBounds_Perspective_TightestCornerTouchesMargin`,
`CameraTests.Camera_FitToBounds_Orthographic_HeightCoversBounds`,
`CameraTests.Camera_FitToBounds_EmptyBounds_FallsBackToUnitCube`,
`CameraTests.Camera_FitToBounds_NonPositiveAspect_Throws`

Asserts the orbit convention (azimuth 0 and elevation 0 place the eye on the negative Y axis),
that every bounding-box corner projects inside the frame, that the tightest corner touches the
margin-inflated frame boundary, that the orthographic height covers the bounds, the unit-cube
fallback for empty bounds, and the rejection of a non-positive aspect ratio.

##### CanvasNetRender3D-SceneRendering-Camera-Validation: Camera Properties Are Validated

**Tests**: `CameraTests.Camera_FieldOfView_OutOfRange_Throws`,
`CameraTests.Camera_NearFar_Invalid_Throws`,
`CameraTests.Camera_OrthographicHeight_NonPositive_Throws`,
`CameraTests.Camera_Vectors_NonFinite_Throw`,
`CameraTests.Camera_Validate_UndefinedProjection_Throws`,
`CameraTests.Camera_FitToBounds_UndefinedProjection_Throws`,
`SceneRendererTests.SceneRenderer_Render_UndefinedProjection_ThrowsArgumentException`

Asserts the documented exceptions for a field of view outside (0, 180), a non-positive or
non-finite clip distance, a non-positive orthographic height, and non-finite eye, target, or up
vectors. The `Camera_NearFar_Invalid_Throws` scenario also covers the cross-property rule that
the near plane must be less than the far plane. The undefined-projection scenarios assert that a
`CameraProjection` value outside the enumeration (for example 42) is rejected with
`ArgumentException` by `Camera.Validate()` and the render path, and with
`ArgumentOutOfRangeException` by `Camera.FitToBounds`, while both defined values are accepted.

##### CanvasNetRender3D-SceneRendering-Camera-DegenerateUp: Parallel Up Vector Is Handled

**Tests**: `CameraTests.Camera_LookAt_ForwardParallelToUp_SubstitutesAlternateUp`,
`CameraTests.Camera_FitToBounds_Elevation90_DoesNotThrow`

Asserts that a view direction parallel to the up hint selects an alternate up vector rather than
failing, and that a 90-degree elevation `FitToBounds` returns a usable camera.

##### CanvasNetRender3D-SceneRendering-Camera-ProjectPoint: A World Point Is Located Where The Renderer Draws It

**Tests**: `CameraTests.Camera_TryProject_OrthographicTop_MapsTargetToCenterAndEdgesToImageEdges`,
`CameraTests.Camera_TryProject_Perspective_MatchesAnalyticValue`,
`CameraTests.Camera_TryProject_BehindEye_ReturnsFalseOnlyForPerspective`,
`CameraTests.Camera_TryProject_NonFinitePoint_ReturnsFalse`,
`CameraTests.Camera_TryProject_InvalidCameraOrSize_Throws`,
`CameraTests.Camera_TryProject_MatchesRasterizer`

Asserts the pixel convention with an orthographic top camera (the target at the image center, the
visible-height edge at the top of the image, +X to the right), the perspective projection against the
analytic value `W/2 + X / (d tan(fov/2) aspect) W/2`, that a perspective point behind or at the eye
returns `false` and an orthographic one does not, that a non-finite point returns `false` with a zero
pixel, and the documented exceptions for an invalid camera and a size outside the range. The rasterizer
scenario renders a flat box without antialiasing with an orthographic and a perspective camera on a non-square image
and requires the extent of the covered pixels to match the extremes of the eight projected corners within
one pixel, which ties `TryProject` to the pixels the renderer really writes rather than to a second
implementation of the math.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong vector, matrix,
or projection value constitutes a failure.
