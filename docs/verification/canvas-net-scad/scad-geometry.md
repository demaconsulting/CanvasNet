## ScadGeometry Subsystem Verification Design

<!-- cspell:ignore evaluator minkowski multmatrix polyhedron extrusions -->

This document describes the subsystem-level verification strategy for the `ScadGeometry` subsystem (internal
namespace `DemaConsulting.CanvasNet.Scad.Geometry`), containing five units: `ScadGeometryBuilder`,
`ScadPrimitives`, `ScadTransforms`, `ScadExtrusions`, and `ScadTextGeometry`.

### Verification Approach

The `ScadGeometry` subsystem is verified at two tiers. The unit tests (see _ScadGeometryBuilder_,
_ScadPrimitives_, _ScadTransforms_, _ScadExtrusions_, and _ScadTextGeometry Unit Verification Design_ under
`scad-geometry/`) verify
each unit through the subsystem's internal entry point with focused programs and hand-built instantiation trees,
including the internal types made visible to the test assembly through `InternalsVisibleTo`. The subsystem-level
tests in `ScadGeometryTests.cs` verify the five units working together: each test evaluates a small program with
the real evaluator (through `ScadGeometryTestSupport.Build`, which runs `ScadEvaluator` into an
`ScadEvaluationSink` and then calls `ScadGeometryBuilder.Build` with the same sink) and asserts on the built
`Scene` — the closed, outward-oriented meshes and their analytic bounds and volumes, the world placement and
appearance of the leaves (computed by an iterative scene walker that composes transforms child-first), the
structure of the boolean nodes, the warnings and unsupported-feature reports with their locations, the
exceptions of the triangle budget and strict mode, and the node-to-location map. The deep-model scenario runs
the whole subsystem on a caller thread with a 256 KiB stack and on a thread with a large stack and compares the
mesh count, the nesting depth, and the total volume.

The renderer is not involved at this tier: the pixels are verified by the `ScadDocument` unit and system tests
(see _ScadDocument Unit Verification Design_ and _CanvasNetScad System Verification_). No mocking or stubbing is
used: the subsystem has no collaborators outside `ScadEvaluation` and the public Render3D and core types, all of
which are used for real. The subsystem is internal, so its tests reach it through the test assembly's
`InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every target framework
- **Mocking**: None required
- **Files**: None; every program is an in-memory string
- **Threads**: The deep-model scenario runs the build on dedicated threads with a 256 KiB and a 256 MiB stack and
  joins each before asserting
- **Isolation**: Each test evaluates and builds its own program and settings; no state is shared between tests

### Acceptance Criteria

The `ScadGeometry` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the five unit verification documents passes without error or unexpected exception, on every target
framework.

### Test Scenarios

- **Implicit Union In Document Order** (requirements `CanvasNetScad-ScadGeometry-Model` and
  `CanvasNetScad-ScadGeometry-Primitives`):
  `ScadGeometryTests.ScadGeometry_Build_PrimitiveSolids_AreClosedOutwardMeshesInDocumentOrder` — builds a cube, a
  translated cylinder, and a translated sphere in one program and asserts three leaves in source order, each
  closed with a positive volume, the exact volume of the cube, and the analytic bounds of the cylinder and (with a
  tolerance for the faceting) the sphere, with no warning and no unsupported feature.
- **Show-Only And Highlight Modifiers** (requirement `CanvasNetScad-ScadGeometry-Model`):
  `ScadGeometryTests.ScadGeometry_Build_Modifiers_ShowOnlyReplacesTheModelAndHighlightIsOverlaid` — builds a model
  whose marked cube sits beside an unmarked one under a translation and asserts that the only leaf is the marked
  cube at the origin (the translation of its ancestor does not apply); builds a highlighted cube and asserts one
  mesh, an overlay beside it, and no report.
- **Transparency Is Rendered, Not Reported** (requirement `CanvasNetScad-ScadGeometry-Transparency`):
  `ScadGeometryTests.ScadGeometry_Build_ColorAlphaAndModifiersOnSolids_AreRenderedNotReported` — builds a cube with
  a color alpha of 0.5, a highlighted cube, and a ghost cube and asserts no warning and no unsupported feature, the
  alpha 128 in the appearance of the first leaf, and two overlays in the scene.
- **Transforms And Color Compose** (requirement `CanvasNetScad-ScadGeometry-Transforms`):
  `ScadGeometryTests.ScadGeometry_Build_TransformsAndColorsOverPrimitives_ComposeWorldPlacementAndAppearance` —
  builds a red box under a translation and a quarter-turn rotation and asserts its exact world bounds and the red
  appearance of the leaf.
- **Difference Of Primitives** (requirement `CanvasNetScad-ScadGeometry-Booleans`):
  `ScadGeometryTests.ScadGeometry_Build_DifferenceOfPrimitives_BuildsDifferenceNodeWithMinuendFirst` — builds a
  cube minus a cylinder and asserts a `CsgNode` of operation `Difference` with two operands, the first being the
  cube.
- **Extrusions Of Shapes** (requirement `CanvasNetScad-ScadGeometry-Extrusions`):
  `ScadGeometryTests.ScadGeometry_Build_ExtrusionsOfShapes_BuildClosedSolids` — builds a linear extrusion of a
  square (exact volume 500 and bounds) and a revolved rectangle (closed, positive volume, height 0 to 3).
- **Top-Level Flat Shape** (requirement `CanvasNetScad-ScadGeometry-FlatShapes`):
  `ScadGeometryTests.ScadGeometry_Build_TopLevelFlatShape_BecomesASlabOfTheConfiguredThickness` — builds a square
  with a thickness of 0.5 and asserts a closed slab of volume 50 and height 0.5.
- **Hull And Minkowski** (requirement `CanvasNetScad-ScadGeometry-HullMinkowski`):
  `ScadGeometryTests.ScadGeometry_Build_HullAndMinkowski_BuildClosedSolids` — builds, in one program, the hull of
  two cubes, a rounded box from a Minkowski sum, a 2D hull that is extruded, and a block from which a hull is
  subtracted, and asserts five closed meshes of positive volume, the analytic bounds of the hull and of the
  extrusion, the bounds of the rounded box, a boolean node for the cut, and no warning or report.
- **Text Is A 2D Shape** (requirement `CanvasNetScad-ScadGeometry-Text`):
  `ScadGeometryTests.ScadGeometry_Build_TextRegion_ParticipatesIn2DBooleanAndLinearExtrusion` — builds a
  `linear_extrude` of a difference of an `H` of size 10 and a square and asserts no warning and no unsupported
  feature, one boolean node of two closed extrusions, and for the first the exact Y extent 0 to 10 of the glyph
  and the Z extent 0 to 2 of the extrusion.
- **Reporting Never Skips Silently** (requirement `CanvasNetScad-ScadGeometry-Reporting`):
  `ScadGeometryTests.ScadGeometry_Build_InvalidAndUnsupportedConstructs_AreReportedAtTheirLocations` — builds a
  program with a valid cube, an invalid cube, `surface`, `roof`, and an `offset` on consecutive lines
  and asserts one mesh, one warning at the invalid cube's line, and the three reports `scad-module-surface`,
  `scad-module-roof`, and `scad-module-offset` at lines 3 to 5 in source order;
  `ScadGeometryTests.ScadGeometry_Build_StrictMode_ThrowsAtTheFirstUnsupportedFeature` — asserts that with strict
  mode the first unsupported feature (`projection` at line 2) throws a `ScadUnsupportedFeatureException` with its
  identifier and location and that the later `surface` is never reached.
- **Triangle Budget** (requirement `CanvasNetScad-ScadGeometry-Limits`):
  `ScadGeometryTests.ScadGeometry_Build_TriangleBudgetExceeded_ThrowsLocatedEvaluationException` — builds a cube
  and a sphere of 64 fragments with a budget of 100 triangles and asserts a `ScadEvaluationException` located at
  the sphere's line, thrown before the sphere's mesh is created.
- **Deep Model On A Small Stack** (requirement `CanvasNetScad-ScadGeometry-StackSafety`):
  `ScadGeometryTests.ScadGeometry_Build_DeepNestedModel_OnSmallStack_MatchesBigStack` — builds a recursive module
  nested 2,000 levels deep (a translation and a cube per level, with the recursion limit raised) on a 256 KiB and
  a 256 MiB caller stack and asserts equal descriptions: 2,001 meshes, a nesting depth over 2,000, and the same
  total volume.
- **Scene Map** (requirement `CanvasNetScad-ScadGeometry-SceneHandoff`):
  `ScadGeometryTests.ScadGeometry_Build_MeshNodes_MapBackToTheirModuleLocations` — builds a cube on line 1 and a
  translated sphere on line 2 and asserts that the scene map answers with the line of each module for its mesh
  node.
