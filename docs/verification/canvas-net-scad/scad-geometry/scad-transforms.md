### ScadTransforms Unit Verification Design

<!-- cspell:ignore multmatrix newsize mislocated RRGGBBAA -->

This document describes the unit-level verification strategy for the `ScadTransforms` unit: the internal
`ScadTransforms` (`translate`, `rotate`, `scale`, `mirror`, `multmatrix`, `resize`) and `ScadColors` (`color` and
color parsing on top of the core `CssColor` table), in the `DemaConsulting.CanvasNet.Scad.Geometry`
namespace.

#### Verification Approach

The unit is verified by building small programs through the real evaluator and the real `ScadGeometryBuilder`
(helper `ScadGeometryTestSupport.Build`) and asserting on the resulting Render3D `Scene`, on the warnings, and on
the unsupported-feature reports recorded for the build. The assertions are analytic, not image based:

- **Transforms** are applied to the asymmetric solid `cube([1, 2, 3])`, whose extents of 1, 2, and 3 identify every
  axis, so a wrong rotation order, a wrong axis, or a transposed matrix changes the expected bounds. The test
  support flattens the scene into leaves with their composed world transforms and mirror-corrected world meshes
  (`SceneLeaf.WorldMesh`); the tests assert the world bounds and, with `AssertSolidVolume`, that the mesh is still
  closed and outward oriented with the volume of the box times the scale (so a mirror that left the mesh inside out
  would fail). The scene structure is asserted where it is the requirement (an identity leaves a `MeshNode` as the
  scene root; a negative determinant on the leaf's world transform).
- **2D shapes** become slabs of the default flat thickness 0.1, so the expected bounds are the transformed
  rectangle plus the thickness, and the volume is the transformed area times 0.1 (a polygon with a hole checks that
  the hole follows the outline).
- **Resize** asserts the exact result extent per axis, the auto proportions, the scaling about the origin, the exact
  extent of rotated children by a hexagonal cylinder turned by 30 degrees, and the 2D case.
- **Colors** are read from the appearance of each scene leaf; the full named-color table is checked entry by entry
  (the loop renders `color("<name>")` for every one of the 149 entries and compares it with the table), and the
  hexadecimal and vector forms with exact byte values.
- **Reports** are asserted by feature identifier and source location (line and column of the instance), and the
  absence of warnings and reports is asserted wherever the behavior is silent. Strict mode is verified through the
  public `ScadUnsupportedFeatureException` for the alpha report.

No mocks are used: the evaluator, the builder, and Render3D are real; only the tiny `Build` helper and the
scene-walking assertions are test code.

Unit tests reside in `ScadTransformsTests.cs`, `ScadResizeTests.cs`, and `ScadColorsTests.cs` within the
`DemaConsulting.CanvasNet.Scad.Tests` project, in the `Geometry` folder, with the shared helpers (`Build`,
`Leaves`, `AssertBounds`, `AssertSolidVolume`, and the settings factory) in `ScadGeometryTestSupport.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the evaluator, builder, and Render3D scene model are real implementations
- **Files**: None; every program is an in-memory string
- **Platform**: The same results on every platform and target framework; no clock, culture, or file-system
  dependency (color parsing uses the invariant culture)
- **Isolation**: Each test builds its own program and scene

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. A wrong bound, a wrong volume or orientation, a missing, extra, or mislocated warning or report, a
wrong color byte, or an exception from a module for any input of the scenarios constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadGeometry-ScadTransforms-Translate: Translation

**Tests**: `ScadTransformsTests.ScadTransforms_Translate_Forms_MoveTheSolid`,
`ScadTransformsTests.ScadTransforms_Translate_SeveralChildren_MovesThemTogether`,
`ScadTransformsTests.ScadTransforms_Translate_NoChildren_IsSilentlyEmpty`

Asserts that a three-, two-, and one-component vector, the named `v`, a negative offset, a zero offset, and
`translate()` give the expected bounds and volume without warnings; that several children move together; and that
a translate without children builds nothing and reports nothing.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Rotate: Rotation

**Tests**: `ScadTransformsTests.ScadTransforms_Rotate_EulerVector_AppliesXThenYThenZ`,
`ScadTransformsTests.ScadTransforms_Rotate_Scalar_RotatesAboutZ`,
`ScadTransformsTests.ScadTransforms_Rotate_AxisAngle_RotatesAboutTheAxis`,
`ScadTransformsTests.ScadTransforms_Rotate_ZeroAxis_WarnsAndDoesNotRotate`,
`ScadTransformsTests.ScadTransforms_RotateAndMirror_AxisWhoseLengthOverflows_IsADirection`,
`ScadTransformsTests.ScadTransforms_Rotate_TinyAxis_IsADirection`,
`ScadTransformsTests.ScadTransforms_Rotate_HugeAngle_EqualsTheReducedAngle`,
`ScadTransformsTests.ScadTransforms_Rotate_NonFiniteOrUndefinedValues_WarnAndRemoveInstance`,
`ScadTransformsTests.ScadTransforms_Rotate_VectorAngleWithAxis_WarnsAndUsesEuler`,
`ScadTransformsTests.ScadTransforms_Rotate_NoAngle_IsIdentity`

Asserts that every combination of 90-degree turns in the vector form identifies the order X, then Y, then Z
(for example `[90, 0, 90]` and `[0, 90, 90]` give different bounds), that short vectors pad with zero, that a
scalar angle turns about Z in both directions, that the axis-angle form follows the right-hand rule for coordinate
and oblique axes, that an axis of any length works and a zero angle or a full turn is the identity, that a zero
axis warns at the instance and leaves the box untouched, that an axis given with a vector angle warns and the Euler
rotation applies, and that `rotate()` changes nothing. It also asserts that an axis or a mirror normal whose length
overflows (`[1.7e308, 1.7e308, 0]`) or whose square rounds to zero (`[0, 0, 1e-300]`) is a direction rather than a zero
vector, that angles of 1e20, -1e20, and 1e308 degrees rotate exactly like the same angle modulo 360 in the scalar,
Euler, and axis-angle forms, and that a non-finite or undefined angle or axis component warns and removes the
instance.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Scale: Scaling

**Tests**: `ScadTransformsTests.ScadTransforms_Scale_Forms_ScaleAboutTheOrigin`,
`ScadTransformsTests.ScadTransforms_Scale_Negative_MirrorsAndKeepsOrientation`

Asserts that a scalar, the named `v`, a three-, two-, and one-component vector, and `scale()` give the expected
bounds and volume, and that a negative factor yields a leaf whose world transform has a negative determinant while
the world mesh keeps its bounds, closure, and volume.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Mirror: Reflection

**Tests**: `ScadTransformsTests.ScadTransforms_Mirror_Planes_ReflectAndKeepOrientation`,
`ScadTransformsTests.ScadTransforms_Mirror_ZeroNormal_WarnsAndDoesNotMirror`,
`ScadTransformsTests.ScadTransforms_Mirror_InvalidAndAbsent_AreHandled`

Asserts the reflection through the three coordinate planes, a normal of a different length, an oblique plane, and a
one-component normal (a negative determinant, the expected bounds, volume 6, no warnings); that a zero normal warns
and keeps the box; and that `mirror()` is the identity without a warning.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Multmatrix: Matrix Transform

**Tests**: `ScadTransformsTests.ScadTransforms_Multmatrix_TranslationColumn_IsTransposedCorrectly`,
`ScadTransformsTests.ScadTransforms_Multmatrix_RotationMatrix_EqualsRotate`,
`ScadTransformsTests.ScadTransforms_Multmatrix_ThreeRows_IsAffine`,
`ScadTransformsTests.ScadTransforms_Multmatrix_Shear_ShearsTheSolid`

Asserts that a translation in the fourth column moves the asymmetric box by exactly `[5, 6, 7]`, that a rotation
matrix gives the same bounds as `rotate(90)`, that the first three rows alone are accepted (scale and offset),
and that a shear gives the sheared bounds with an unchanged volume.

##### CanvasNetScad-ScadGeometry-ScadTransforms-MultmatrixProjective: Projective Matrix

**Tests**: `ScadTransformsTests.ScadTransforms_Multmatrix_ProjectiveRow_IsReportedAndOmitted`

Asserts, for four bottom rows other than `[0, 0, 0, 1]`, that the scene is empty, that exactly one report with the
feature `scad-multmatrix-projective` exists at the line and column of the instance, and that no warning is
recorded.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Identity: Identity Adds No Wrapper

**Tests**: `ScadTransformsTests.ScadTransforms_Translate_Identity_AddsNoWrapper`,
`ScadTransformsTests.ScadTransforms_Multmatrix_Identity_AddsNoWrapper`

Asserts that the root of the scene is the `MeshNode` itself (no `GroupNode`) for a zero translation and for the
identity matrix.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Composition: Nesting Order

**Tests**: `ScadTransformsTests.ScadTransforms_Nesting_AppliesInnermostFirst`,
`ScadTransformsTests.ScadTransforms_Nesting_ScaleIsAboutTheOrigin`,
`ScadTransformsTests.ScadTransforms_Transform_AppliesThroughModulesAndLoops`,
`ScadTransformsTests.ScadTransforms_Translate_SeveralChildren_MovesThemTogether`

Asserts that `translate` around `rotate` and `rotate` around `translate` give different bounds that follow the
child-first rule, that `scale(2)` around a translated cube and a translate around a scaled cube differ because the
scale is about the origin, that a transform applies through a user module and a `for` loop to all three iterations,
and that several children move together.

##### CanvasNetScad-ScadGeometry-ScadTransforms-InvalidArguments: Invalid And Surplus Arguments

**Tests**: `ScadTransformsTests.ScadTransforms_Translate_InvalidVector_WarnsAndRemovesInstance`,
`ScadTransformsTests.ScadTransforms_RotateAndScale_InvalidArguments_WarnAndRemoveInstance`,
`ScadTransformsTests.ScadTransforms_Mirror_InvalidAndAbsent_AreHandled`,
`ScadTransformsTests.ScadTransforms_Multmatrix_Malformed_WarnsAndRemovesInstance`,
`ScadTransformsTests.ScadTransforms_Arguments_UnknownAndSurplus_Warn`,
`ScadTransformsTests.ScadTransforms_Arguments_SpecialVariable_IsIgnoredSilently`,
`ScadResizeTests.ScadResize_InvalidArguments_WarnAndRemoveInstance`

Asserts that a string, a vector with a non-number, a vector that is too long or empty, a scalar where a vector is
required, a division by zero (infinity), a malformed matrix, a negative or mistyped `newsize`, and a bad `auto`
each give exactly one warning, no mesh, and for `translate` the location of the instance and the text `translate:
v must be a vector`; that an unknown and a surplus argument warn while both instances still apply; and that
`$fn` given to a transform is silent.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Singular: Singular And Non-Finite Transforms

**Tests**: `ScadTransformsTests.ScadTransforms_Scale_Singular_WarnsAndRemovesInstance`,
`ScadTransformsTests.ScadTransforms_Multmatrix_Singular_WarnsAndRemovesInstance`,
`ScadTransformsTests.ScadTransforms_NonFiniteMatrix_WarnsAndRemovesInstance`

Asserts that a zero scale (three forms, with the instance location), an all-zero matrix, a matrix with two equal
rows, and a matrix with proportional rows each warn (the text names `singular`), build nothing, and do not throw,
and that a matrix whose determinant overflows (huge scales) also warns and builds nothing.

##### CanvasNetScad-ScadGeometry-ScadTransforms-FlatTransforms: Transforms Of 2D Shapes

**Tests**: `ScadTransformsTests.ScadTransforms_Flat_TransformsBecomeTheExpectedSlab`,
`ScadTransformsTests.ScadTransforms_Flat_OutOfPlaneParts_AreIgnoredWithWarning`,
`ScadTransformsTests.ScadTransforms_Flat_ShapeWithHole_KeepsItsHole`

Asserts that translate, rotate (scalar and about Z), scale (including a zero Z factor, which is silent), mirror,
and `multmatrix` of a `square` give the expected slab bounds and volume without warnings; that a Z translation warns
with the text `Z part` at the instance and still moves the shape in the plane; that a rotation about X that
collapses the plane warns `singular` and removes the shape; and that a polygon with a hole keeps area 12 after a
translation.

##### CanvasNetScad-ScadGeometry-ScadTransforms-Resize: Resize Sizing

**Tests**: `ScadResizeTests.ScadResize_AllAxes_GivesTheRequestedSize`,
`ScadResizeTests.ScadResize_AutoAndZero_KeepOrFollowProportions`,
`ScadResizeTests.ScadResize_OffsetChild_ScalesAboutTheOrigin`,
`ScadResizeTests.ScadResize_Convexity_IsAcceptedSilently`

Asserts that `resize([2, 4, 6])` gives volume 48 with no warning or report; that a zero component keeps its axis
and that `auto` as a flag, as a vector, and as a short vector follows the proportions of the first requested axis
(eleven rows, including short `newsize` vectors and `auto` with nothing to follow); that an offset child scales with
the factor about the origin; and that `convexity` is accepted silently.

##### CanvasNetScad-ScadGeometry-ScadTransforms-ResizeBounds: Exact Extent

**Tests**: `ScadResizeTests.ScadResize_RotatedChild_UsesExactVertexExtent`,
`ScadResizeTests.ScadResize_Union_UsesTheWholeExtent`,
`ScadResizeTests.ScadResize_ExactChildren_ReportNothing`,
`ScadResizeTests.ScadResize_Flat_ResizesInThePlane`,
`ScadGeometryBuilderModifierTests.ScadGeometryBuilder_Modifiers_Background_ContributesNoBounds_ButIsInReach`

Asserts that a hexagonal cylinder rotated by 30 degrees (whose rotated bounding box is loose) is resized to exactly
10 along X within 1e-9 with no report, that two cubes three apart are sized as one union (factor 2.5), that a
nested chain of translate, rotate, union, cube, and sphere reports neither a warning nor an unsupported feature,
that 2D resizing uses X and Y only (a third component is ignored), and that a `%` ghost 50 units away from a
rotated cube does not take part in the exact extent (the cube is resized to exactly 4 by 4 by 4).

_Additional evidence, not cited by the requirement:_ the approximate-bounds report is exercised at the builder
level by `ScadGeometryBuilderBooleanTests.ScadBooleans_Resize_OverBoolean_ReportsApproximateBoundsAndStillResizes`
(`scad-resize-approximate` at the instance location for a difference, an intersection, and a 2D difference, with
geometry still produced), `ScadBooleans_Resize_OverDifference_UsesTheFirstOperandExtent`, and
`ScadBooleans_Resize_OverIntersection_UsesTheOverlap`.

##### CanvasNetScad-ScadGeometry-ScadTransforms-ResizeDegenerate: Resize Edge Cases

**Tests**: `ScadResizeTests.ScadResize_AxisWithoutExtent_WarnsAndSkipsIt`,
`ScadResizeTests.ScadResize_NoSizeOrNoChildren_IsHarmless`

Asserts that a flat polyhedron without Z extent warns `no extent along Z` at the instance, that `resize()` keeps the
box, and that `resize([1, 2, 3]) { }` builds nothing and is silent.

##### CanvasNetScad-ScadGeometry-ScadTransforms-ColorForms: Color Values

**Tests**: `ScadColorsTests.ScadColors_Names_RenderAsTheSharedCssTable`,
`ScadColorsTests.ScadColors_Forms_GiveTheExpectedOpaqueColor`

Asserts that named colors come from the shared core table with spot values (`red`, `RED`, `RebeccaPurple`,
`aliceblue`, `yellowgreen`, the equal `gray` and `grey` pairs, and `transparent` with alpha 0), and that names in any
case, positional and named `c`, `#rgb`, `#rrggbb` in either case, vectors with clamping (`[2, -1, 0.5]` gives 255,
0, 128), and rounding give the expected opaque bytes with no warning or report. The table itself (148 names and
every hexadecimal form) is verified by the core `CssColor` unit tests.

##### CanvasNetScad-ScadGeometry-ScadTransforms-ColorInvalid: Invalid Color

**Tests**: `ScadColorsTests.ScadColors_InvalidColor_WarnsAndLeavesChildrenUncolored`,
`ScadColorsTests.ScadColors_InvalidAlpha_WarnsAndAppliesTheColor`,
`ScadColorsTests.ScadColors_NoColorOrNoChildren_IsSilent`

Asserts that an unknown name, an empty string, a name with a leading blank, `none`, a CSS function string
(`rgb(255, 0, 0)`, `rgba(255, 0, 0, 0.5)`, `hsl(0, 100%, 50%)`: the narrow Scad grammar does not take the wider CSS
forms), hexadecimal strings of the wrong length or with non-hexadecimal characters or a sign, a number, a boolean,
a vector of the wrong length or with a non-number, and an infinite
channel each give one warning at the instance beginning with `color:` and a cube without an appearance; that a
non-numeric alpha warns while the color applies; and that `color()` and `color("red") { }` are silent.

##### CanvasNetScad-ScadGeometry-ScadTransforms-ColorNesting: Color Scope

**Tests**: `ScadColorsTests.ScadColors_Nesting_InnermostWinsAndSiblingsAreUnaffected`,
`ScadColorsTests.ScadColors_SeveralChildren_AreAllColored`,
`ScadColorsTests.ScadColors_ThroughTransforms_IsKept`,
`ScadColorsTests.ScadColors_FlatShape_ColorsTheSlab`,
`ScadColorsTests.ScadColors_AroundTransformedFlatShape_ColorsTheSlab`

Asserts that the inner blue wins over the outer red and that a sibling and a following cube keep red and no
appearance respectively, that all three children of one color are colored, that the color is kept through a
translate and a scale in either order, and that a 2D shape (alone, and a union of a translated square and
circle) gives colored slabs.

##### CanvasNetScad-ScadGeometry-ScadTransforms-ColorAlpha: Alpha Is Rendered

**Tests**: `ScadColorsTests.ScadColors_AlphaBelowOne_SetsAppearanceAlphaWithoutReport`,
`ScadColorsTests.ScadColors_FullAlpha_AlphaArgumentOverridesColorAlpha`,
`ScadColorsTests.ScadColors_AlphaOnly_WarnsAndDoesNotColor`,
`ScadColorsTests.ScadColors_NestedColor_InnermostAlphaWins`,
`ScadColorsTests.ScadColors_AlphaOn2DShape_ReachesTheExtrusion`,
`ScadColorsTests.ScadColors_AlphaBelowOne_DoesNotThrowInStrictMode`

Asserts that an alpha below 1 through the positional alpha (128 for 0.5), the named alpha (0), a vector (0.25 gives
64), `#RRGGBBAA`, `#RGBA`, `transparent` (alpha 0), and an `alpha` that overrides a vector alpha of 1 (0.1 gives 26)
becomes the alpha channel of the appearance color with the color channels unchanged and neither a warning nor a
report; that alpha 1 or more, `#ff0000ff`, and an `alpha` of 1 overriding a vector alpha of 0.2 keep alpha 255,
while an `alpha` of 0.2 overriding an opaque vector gives 51; that an alpha without a color warns `alpha needs a
color` at the instance and colors nothing, while an alpha with a color but no children is silent; that the
innermost color wins with its alpha in both nesting orders; that the alpha of a color on a 2D shape reaches the
extruded solid and the slab of a bare shape; and that strict mode does not throw for an alpha.
