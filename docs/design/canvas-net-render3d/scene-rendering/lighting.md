### Lighting

<!-- cspell:ignore Lambertian Blinn Phong unlit -->

The `Lighting` unit is the second software unit in the `SceneRendering` subsystem. It comprises
three public types in the `DemaConsulting.CanvasNet.Render3D.SceneRendering` namespace plus one
internal type:

- `Light` (abstract class): base of the light hierarchy; owns the shared properties and the
  default rig
- `AmbientLight` (sealed class): non-directional light
- `DirectionalLight` (sealed class): a light that travels in one direction
- `LightingModel` (internal static class): the flat shading function

#### Purpose

`Lighting` describes the lights a caller can adjust and computes the shaded color of a triangle from
its normal. Shading is **flat**: each triangle is shaded once for its front face and once for its
back face, so the per-sample cost during rasterization is a color lookup.

#### Data Model

- **`Light`**: `Rgba32 Color` (init-only; defaults to opaque white; its alpha is ignored) and
  `float Intensity` (init-only; defaults to 1; must be finite and not negative, otherwise
  `ArgumentOutOfRangeException`). The constructor is `private protected`, so the hierarchy is
  closed. `Light.DefaultRig` is a shared, immutable list: an `AmbientLight` of intensity 0.35, a
  camera-relative `DirectionalLight` along `(0, 0, -1)` (a headlight) of intensity 0.55, and a
  camera-relative `DirectionalLight` along `(0.5, -0.6, -0.62)` (a key light from the upper left)
  of intensity 0.25.
- **`AmbientLight`**: no additional state.
- **`DirectionalLight`**: `Vector3D Direction` (init-only; the direction the light **travels**;
  must be finite and non-zero (tested with the overflow/underflow-safe `Vector3D.Length`),
  otherwise `ArgumentException`; normalized when set; defaults to
  `(0, 0, -1)`) and `bool IsCameraRelative` (init-only; defaults to false). When true the
  direction is expressed in view space (+X right, +Y up, -Z forward) and so moves with the camera;
  when false it is a world-space direction.

#### Key Methods

`LightingModel.Shade(normal, baseColor, lights, specularStrength, specularPower, worldToView)` takes
a unit view-space normal that faces the viewer and returns an opaque `Rgba32`:

1. For each light, with `C` the light color scaled to `[0, 1]` per channel and `I` its intensity:
   an ambient light adds `I * C` to the diffuse radiance; a directional light computes the
   direction it travels in view space (the stored direction when camera-relative, otherwise the
   world direction transformed by `worldToView` and re-normalized), takes
   `nDotL = max(0, N . -travel)`, and adds `I * nDotL * C`.
2. When `specularStrength` is greater than zero and `nDotL` is greater than zero, a directional
   light also adds a Blinn-Phong highlight `I * specularStrength * max(0, N . H)^specularPower * C`,
   where `H` is the normalized sum of the direction to the light and a **constant viewer vector**
   `(0, 0, 1)`. The constant viewer is a documented flat-shading approximation that keeps the
   color constant across a triangle.
3. Each output channel is `baseColor * diffuseRadiance + specular`, rounded to the nearest byte
   (midpoints away from zero) and clamped to `[0, 255]`; alpha is 255. Base color and light
   colors are used as sRGB byte values without gamma conversion, consistent with the rest of
   `CanvasNet`.

`LightingModel.ShadeBackFace(frontNormal, backFaceColor, ...)` shades with the **flipped** normal
and the back-face color, so a surface seen from behind is lit as if its normal pointed at the
viewer.

The shades are always opaque: transparency is not part of lighting. `SceneRenderer` sets the alpha of
the owning `NodeAppearance` on the shades of a fragment-path triangle after shading, and the alpha of
the base, light and back-face colors passed to `Shade` is ignored.

An empty light list is valid and produces black (unlit) surfaces; supplying an explicit
`AmbientLight` of intensity 1 produces a flat color. A null light list in `SceneRenderOptions`
selects `Light.DefaultRig`.

#### Error Handling

`Light.Intensity` and `DirectionalLight.Direction` validate when initialized (see Data Model).
`LightingModel` is total: it performs no validation of its inputs (the renderer passes validated
values) and cannot fail. Nothing is caught locally.

#### Dependencies

The `DoublePrecisionMath` unit (`Vector3D`, `Matrix4x4D`) and the core `CanvasNet` system's
`Canvas` subsystem (`Rgba32`).

#### Callers

`SceneRenderOptions` stores a light list and exposes `Light.DefaultRig` as the fallback;
`SceneRenderer` calls `LightingModel.Shade` and `ShadeBackFace` once per triangle during triangle
preparation; callers construct lights directly.
