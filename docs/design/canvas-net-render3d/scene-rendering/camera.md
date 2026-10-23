### Camera

<!-- cspell:ignore azimuth -->

The `Camera` unit is the first software unit in the `SceneRendering` subsystem. It comprises two
public types in the `DemaConsulting.CanvasNet.Render3D.SceneRendering` namespace plus one internal
type:

- `Camera` (sealed class): an immutable perspective or orthographic camera
- `CameraProjection` (enum): `Perspective`, `Orthographic`
- `ViewTransform` (internal readonly struct): the camera-relative view transform and projection
  scalars the renderer uses for one render

#### Purpose

`Camera` describes the viewpoint and projection and offers two convenient ways to obtain one:
`LookAt`, and `FitToBounds`, which frames a bounding box from a chosen azimuth and elevation
without the caller computing a camera distance. `ViewTransform` turns a `Camera` plus an aspect
ratio into the matrices the renderer applies in double precision.

#### Data Model

`Camera` has init-only properties, each validated when initialized:

| Property | Type | Default | Validation |
| -------- | ---- | ------- | ---------- |
| `Eye` | `Vector3D` | `(0, -1, 0)` | Finite |
| `Target` | `Vector3D` | origin | Finite |
| `Up` | `Vector3D` | `+Z` | Finite |
| `Projection` | `CameraProjection` | `Perspective` | Must be defined (`Validate()`) |
| `FieldOfViewDegrees` | `double` | 22.5 (vertical) | Strictly between 0 and 180 |
| `OrthographicHeight` | `double?` | `null` | When set: finite and greater than zero |
| `NearPlane` | `double?` | `null` (automatic) | When set: finite and greater than zero |
| `FarPlane` | `double?` | `null` (automatic) | When set: finite and greater than zero |

The public constants `DefaultFieldOfViewDegrees` (22.5) and `DefaultAzimuthDegrees` (45) and the
static `DefaultElevationDegrees` (`atan(1/sqrt 2)`, about 35.26 degrees, the true isometric
elevation) define the default view used by `SceneRenderer.RenderFitted`. A default-constructed
`Camera` is itself valid (eye on -Y looking at the origin). Rules that span properties — eye must
differ from target, an orthographic camera needs an `OrthographicHeight`, and `NearPlane` must be
less than `FarPlane`, and `Projection` must be a defined `CameraProjection` value — cannot be
checked at initialization and are enforced by the internal
`Camera.Validate()` (`ArgumentException`), which `ViewTransform`'s constructor calls when rendering starts.

`ViewTransform` holds the eye, the translation `-eye`, the view rotation, the projection kind, the
aspect ratio, `tan(fov/2)`, and the orthographic half-height. View space is right-handed with the
camera looking down -Z and +Y up.

#### Key Methods

- `Camera.LookAt(eye, target, up)`: validates that all three are finite, eye differs from target,
  and `up` is non-zero (`ArgumentException`), then returns a camera whose stored `Up` is already
  the alternative up vector when the view direction is parallel to `up` (see below).
- **Degenerate up**: when the view direction is parallel to the up hint (for example a camera
  looking straight down), the basis builder substitutes `-Y` when looking up or `+Y` when looking
  down rather than failing; this keeps a top or bottom view of a Z-up scene well defined.
- `Camera.FitToBounds(bounds, azimuthDegrees, elevationDegrees, aspectRatio, projection,
  fieldOfViewDegrees, marginFraction)`: frames a bounding box tightly. Arguments are validated
  (`ArgumentOutOfRangeException` for a non-finite angle, a non-positive aspect ratio, an undefined
  `projection` value, a field of
  view outside (0, 180), or a negative margin). The orbit direction from the center toward the eye
  is
  `(sin az cos el, -cos az cos el, sin el)`. Empty or degenerate (zero-diagonal) bounds fall back to
  a unit cube at the origin. The half-extents are inflated by `1 + marginFraction`. Each of the eight
  inflated corners is projected into the view basis to obtain lateral offsets `(x, y)` and depth
  offset `z` toward the camera. For a perspective camera the eye distance from the center is
  `max over corners of (z + |x| / tanH, z + |y| / tanV)` with `tanV = tan(fov/2)` and
  `tanH = tanV * aspect`, which is the smallest distance at which every inflated corner is inside the
  frustum, and is never less than the largest `z` plus a tenth of the box radius (so the eye stays
  outside the box for extreme fields of view). For an orthographic camera the eye is placed twice the
  box radius from the center and `OrthographicHeight` is `2 * max(max|y|, max|x| / aspect)`. The
  result always looks at the center with `Up = +Z`.
- `Camera.GetViewMatrix()`: `translate(-Eye)` followed by the view rotation (rows are the camera
  right, up, and backward axes), mapping the eye to the origin, the target to `(0, 0, -distance)`,
  and the up direction to +Y.
- `ViewTransform.ComposeModelToView(world)`: `world * translate(-eye) * rotation`, evaluated in
  double precision; `WorldToView`: `translate(-eye) * rotation`. `ClipScaleX` and `ClipScaleY` give
  the factors that map view-space X and Y to normalized device coordinates (the reciprocal of
  `tan(fov/2)` times the aspect ratio, or of the orthographic half-height times the aspect ratio, for
  X; the same without the aspect ratio for Y).

The near and far clip distances are **not** computed in this unit: a `null` `NearPlane` or
`FarPlane` means "automatic", and `SceneRenderer` derives the values from the scene's depth range
(see _SceneRenderer Unit Design_, `scene-renderer.md`).

#### Error Handling

`ArgumentException` for non-finite vectors, a zero up vector, eye equal to target, an orthographic
camera without a height, and near not below far; `ArgumentOutOfRangeException` for an out-of-range
field of view, non-positive height or clip distance, and invalid `FitToBounds` arguments. Property
checks occur when the camera is created; cross-property checks occur when a render begins and
surface as `ArgumentException` from `SceneRenderer.Render`. Nothing is caught locally.

#### Dependencies

The `DoublePrecisionMath` unit (`Vector3D`, `Matrix4x4D`, `BoundingBox3D`) and the .NET base class
library (`System.Math`).

#### Callers

`SceneRenderer` constructs a `ViewTransform` from the camera for every render and calls
`Camera.FitToBounds` from `RenderFitted`; callers construct cameras directly.
