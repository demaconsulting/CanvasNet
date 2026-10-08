# VSDX Test Fixtures

<!-- cspell:ignore vsdx davehoward jgreywolf visio stencil nurbsto themeguard -->
<!-- cspell:ignore jgreywolfvsdxjs Greywolf Lucidchart basicshapes diagramwithstyles flowchartshapes -->

This folder holds a small, real-world corpus of `.vsdx` files intended to exercise
`VsdxDocument`'s rendering against real-world Visio documents nobody at this repository
hand-authored, complementing (not replacing) the synthetic, hand-built `.vsdx` packages any
fine-grained, single-construct `Vsdx*Tests.cs` unit test uses. Every file here is used unmodified,
exactly as published by its own upstream source - nothing in this folder was edited, re-saved, or
re-compressed after download.

Two independent sources are represented:

- **`dave-howard/vsdx`** (nine files, the `davehoward-*.vsdx` prefix): real test fixture files
  taken unmodified from the [dave-howard/vsdx](https://github.com/dave-howard/vsdx) project's own
  `tests/` directory. `dave-howard/vsdx` is distributed under the **BSD 3-Clause License** (SPDX
  identifier `BSD-3-Clause`, confirmed via `gh api repos/dave-howard/vsdx/license`, copyright Dave
  Howard); the full license text, with a short attribution header, is reproduced in
  `DaveHowardVsdx.LICENSE` in this same folder. Each file's exact upstream source URL is listed in
  its own table row below.
- **`jgreywolf/vsdx-js`** (five files, the `jgreywolfvsdxjs-*.vsdx` prefix): real test fixture
  files taken unmodified from the [jgreywolf/vsdx-js](https://github.com/jgreywolf/vsdx-js)
  project's own `tests/` directory. `vsdx-js` is distributed under the **MIT License** (SPDX
  identifier `MIT`, confirmed via `gh api repos/jgreywolf/vsdx-js/license`, copyright Justin
  Greywolf); the full license text, with a short attribution header, is reproduced in
  `JgreywolfVsdxJs.LICENSE` in this same folder. Each file's exact upstream source URL is listed in
  its own table row below.

These 14 files are the **complete** set of `.vsdx` fixtures checked into this repository - there
is no other fixture source committed here. In particular, the separate 12-file/16-page
Apache-POI-derived corpus (Apache-licensed, from the
[apache/poi](https://github.com/apache/poi) project's own test-data) used during Milestones
10-12's real-world bug-fix work for visual comparison against Microsoft Visio (COM)-rendered
ground truth, and the ad hoc smoke-test scratch tooling that drove that comparison, were both
external, scratch tooling used only during development - neither was ever checked into this
repository, and neither should be expected in this folder. Every fixture-driven test in this test
project (`VsdxFixtureShapeResolutionTests.cs`, `VsdxFixtureTextResolutionTests.cs`,
`VsdxRenderFixtureTests.cs`, `VsdxConnectorFixtureTests.cs`) exercises only the 14 files listed
below.

## Included Files

| File | Upstream source URL | Exercises |
| ------ | ----------- | ----------- |
| `davehoward-test1.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test1.vsdx> | Baseline multi-shape page, basic property sections. |
| `davehoward-test3-house.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test3_house.vsdx> | Group master with `MasterShape` children, multi-section geometry. |
| `davehoward-test4-connectors.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test4_connectors.vsdx> | Primary connector worked example, dynamic-connector master, whole-shape glue. |
| `davehoward-test5-master.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test5_master.vsdx> | Group master with `NURBSTo`, rotation, and a `Property` section (Lucidchart-authored). |
| `davehoward-test6-shape-properties.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test6_shape_properties.vsdx> | `Section N="Property"` rows; `Type="Guide"` master with `InfiniteLine`. |
| `davehoward-test9-rect-and-line.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test9_rect_and_line.vsdx> | 1-D line shape transform derivation, straight connector. |
| `davehoward-test10-nested-shapes.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test10_nested_shapes.vsdx> | Three-level nested/grouped shapes. |
| `davehoward-test11-rotate.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test11_rotate.vsdx> | Multiple distinct `Angle` values - rotation transform/rotation-math validation. |
| `davehoward-test12-colors.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test12_colors.vsdx> | Direct line/text/fill color overrides, `THEMEGUARD`/`SHADE` formulas. |
| `jgreywolfvsdxjs-basicshapes.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/BasicShapes.vsdx> | Large stencil (`master1`-`master33`+); basic shapes, `EllipticalArcTo` circle master with explicit `U='IN'` units. |
| `jgreywolfvsdxjs-connectors.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/Connectors.vsdx> | `theme1.xml` present; connectors/connection-point-indexed glue worked example. |
| `jgreywolfvsdxjs-diagramwithstyles.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/DiagramWithStyles.vsdx> | `theme1.xml` present; group master/stencil with cross-reference (`Sheet.N!Cell`) child-style inheritance. |
| `jgreywolfvsdxjs-drawing.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/Drawing.vsdx> | Additional master/stencil variety - general drawing coverage. |
| `jgreywolfvsdxjs-flowchartshapes.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/FlowchartShapes.vsdx> | Large flowchart stencil with styled shapes; page 1 has no `<Connects>` section (confirms it is optional). |

## Exact Source URLs

Each `davehoward-*.vsdx` file here is byte-for-byte the upstream file at the corresponding URL
below (verified by exact file size against the upstream file before inclusion):

| This folder's file | Upstream source URL |
| ------ | ----------- |
| `davehoward-test1.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test1.vsdx> |
| `davehoward-test3-house.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test3_house.vsdx> |
| `davehoward-test4-connectors.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test4_connectors.vsdx> |
| `davehoward-test5-master.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test5_master.vsdx> |
| `davehoward-test6-shape-properties.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test6_shape_properties.vsdx> |
| `davehoward-test9-rect-and-line.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test9_rect_and_line.vsdx> |
| `davehoward-test10-nested-shapes.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test10_nested_shapes.vsdx> |
| `davehoward-test11-rotate.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test11_rotate.vsdx> |
| `davehoward-test12-colors.vsdx` | <https://raw.githubusercontent.com/dave-howard/vsdx/master/tests/test12_colors.vsdx> |

Each `jgreywolfvsdxjs-*.vsdx` file here is byte-for-byte the upstream file at the corresponding URL
below (verified by exact file size against the upstream file before inclusion):

| This folder's file | Upstream source URL |
| ------ | ----------- |
| `jgreywolfvsdxjs-basicshapes.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/BasicShapes.vsdx> |
| `jgreywolfvsdxjs-connectors.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/Connectors.vsdx> |
| `jgreywolfvsdxjs-diagramwithstyles.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/DiagramWithStyles.vsdx> |
| `jgreywolfvsdxjs-drawing.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/Drawing.vsdx> |
| `jgreywolfvsdxjs-flowchartshapes.vsdx` | <https://raw.githubusercontent.com/jgreywolf/vsdx-js/main/tests/FlowchartShapes.vsdx> |
