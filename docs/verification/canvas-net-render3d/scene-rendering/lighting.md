### Lighting Unit Verification Design

<!-- cspell:ignore Lambert Blinn Phong unlit Overbright -->

This document describes the unit-level verification strategy for the `Light`, `AmbientLight`, and
`DirectionalLight` types and the internal `LightingModel`.

#### Verification Approach

The light types are verified through their initializers and the shared default rig. The internal
`LightingModel` is verified through the test assembly's `InternalsVisibleTo` access by calling
`Shade` and `ShadeBackFace` with hand-picked normals, lights, and base colors and comparing the
returned color with the value computed independently from the lighting equation (for example, an
ambient light of 0.5 on a base color of 180 yields exactly 90, and a 60-degree tilt under a
headlight halves its contribution). The unit depends only on `DoublePrecisionMath` and `Rgba32`, so
no mocking or stubbing is required.

Unit tests reside in `LightTests.cs` and `LightingModelTests.cs` within the
`DemaConsulting.CanvasNet.Render3D.Tests` project, in the `SceneRendering` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test constructs its own lights and inputs

#### Unit-Level Test Scenarios

##### CanvasNetRender3D-SceneRendering-Lighting-LightTypes: Light Types Validate And Normalize

**Tests**: `LightTests.Light_Constructors_NegativeIntensity_Throws`,
`LightTests.DirectionalLight_ZeroDirection_Throws`,
`LightTests.DirectionalLight_NormalizesDirection`,
`LightTests.DirectionalLight_LargeDirection_NormalizesToUnitVector`

Asserts that a negative intensity and a zero direction are rejected, and that a directional light's
direction is normalized when set (including a direction whose squared length overflows).

##### CanvasNetRender3D-SceneRendering-Lighting-DefaultRig: Default Rig Has The Documented Lights

**Test**: `LightTests.Light_DefaultRig_HasAmbientHeadlightAndKey_WithDocumentedIntensities`

Asserts that the default rig is an ambient light of 0.35, a camera-relative headlight of 0.55, and a
camera-relative key light of 0.25.

##### CanvasNetRender3D-SceneRendering-Lighting-Ambient: Ambient Light Scales The Base Color

**Tests**: `LightingModelTests.LightingModel_Shade_AmbientOnly_ScalesBaseColor`,
`LightingModelTests.LightingModel_Shade_ColoredLight_TintsChannels`

Asserts that an ambient light of 0.5 on a base color of 180 gives 90, and that a colored light tints
the channels as expected.

##### CanvasNetRender3D-SceneRendering-Lighting-Lambert: Diffuse Shading Follows The Cosine Law

**Tests**: `LightingModelTests.LightingModel_Shade_HeadlightFacingViewer_AppliesFullIntensity`,
`LightingModelTests.LightingModel_Shade_SixtyDegreeTilt_AppliesHalfIntensity`,
`LightingModelTests.LightingModel_Shade_FaceTurnedAwayFromLight_ReceivesOnlyAmbient`,
`LightingModelTests.LightingModel_Shade_AmbientPlusHeadlight_AddsContributions`

Asserts full intensity for a face toward a headlight, half intensity at a 60-degree tilt, only the
ambient contribution for a face turned away from the light, and that ambient and directional
contributions add.

##### CanvasNetRender3D-SceneRendering-Lighting-Specular: Specular Highlights Are Optional

**Tests**: `LightingModelTests.LightingModel_Shade_SpecularEnabled_BrightensFaceAlignedWithHalfVector`,
`LightingModelTests.LightingModel_Shade_SpecularZero_MatchesDiffuseOnly`

Asserts that a nonzero specular strength brightens a face aligned with the half vector, and that
zero strength reproduces the diffuse-only result.

##### CanvasNetRender3D-SceneRendering-Lighting-CameraRelative: Light Direction Frames

**Tests**: `LightingModelTests.LightingModel_Shade_WorldLight_RotatesWithViewTransform`,
`LightingModelTests.LightingModel_Shade_CameraRelativeLight_IgnoresViewRotation`

Asserts that a world-space light direction is rotated by the world-to-view transform while a
camera-relative light is unaffected by it.

##### CanvasNetRender3D-SceneRendering-Lighting-BackFace: Back Faces Use The Flipped Normal And Back Color

**Test**: `LightingModelTests.LightingModel_ShadeBackFace_UsesFlippedNormalAndBackColor`

Asserts that `ShadeBackFace` equals `Shade` with the negated normal and the back-face color.

##### CanvasNetRender3D-SceneRendering-Lighting-NoLights: An Empty Light List Gives Black

**Test**: `LightingModelTests.LightingModel_Shade_EmptyLightList_ReturnsBlack`

Asserts that shading with no lights returns opaque black.

##### CanvasNetRender3D-SceneRendering-Lighting-Clamp: Overbright Lighting Clamps To The Displayable Range

**Test**: `LightingModelTests.LightingModel_Shade_OverbrightLights_ClampsTo255`

Asserts that lights whose combined radiance exceeds one produce channel values clamped to 255.

#### Acceptance Criteria

A unit-level test run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong exception type, or wrong color channel
value (beyond the stated rounding tolerance) constitutes a failure.
