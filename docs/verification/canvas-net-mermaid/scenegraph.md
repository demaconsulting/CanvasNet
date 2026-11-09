## Scenegraph Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scenegraph` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Scenegraph`), containing two units: `SceneModel` (the immutable scene records, their
bounds, and the conversion of their geometry to a core path) and `ScenePainter` (the painter of a scene onto a
`Surface`, with its placement and its picture-loader contract).

### Verification Approach

The `Scenegraph` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`scenegraph/`) verify each unit in isolation, including the pictures, the painted-pixel limit, the skipping of
unbounded geometry, and cancellation. The subsystem-level scenario in `ScenegraphTests` verifies the units working
together: a scene of one node - a filled and stroked rectangle with a centered label inside a translated group, built
from `SceneModel` records as the layout builds it - is measured with `Scene.BoundsOf` and painted by `ScenePainter`
with the font resolver of a call, twice at scale 1 and once at scale 2. The expectations are analytic: the bounds are
the rectangle moved by the group translation; the fill color is found at the node center at both scales; the label
leaves ink at the center of the node; and the two paintings at scale 1 are identical pixel by pixel. No golden images
are used.

The subsystem uses no mocking or stubbing; the core `Surface`, `PathFiller`, `PathStroker`, `TextRenderer`, the font
resolver, and the bundled fonts are real. The subsystem is internal, so its tests reach it through the test
assembly's `InternalsVisibleTo` access. The painting of complete diagrams, the pictures of the caller's resolver, the
painted-pixel limit, and cancellation through the public API are further verified by the system tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts, through the resolver of `ScenePainter.CreateFontResolver`
- **Isolation**: Each test builds its own scene, painter, and surface; no files are read

### Acceptance Criteria

The `Scenegraph` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Scenegraph-Model: The Scene Describes The Diagram

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`

Builds a node scene from a group with an offset of (48, 28) holding a rectangle from (-40, -20) to (40, 20) with a
role and Mermaid's element id and a label line, and asserts that the bounds of the scene are the rectangle (8, 8, 80,
40), the stroke excluded.

#### CanvasNetMermaid-Scenegraph-Painting: The Scene Is Painted At A Placement

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`

Paints the node scene over a transparent background at scale 1 and at scale 2 and asserts that the node fill is at
the same scene point of both surfaces and that the label, drawn in the bundled font for Mermaid's default family
stack, leaves ink at the center of the node.

#### CanvasNetMermaid-Scenegraph-Pictures: Pictures Come Only From The Scene's Loader

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`

Paints a scene that carries no picture loader with a painter built without one, as the system does for a diagram
without pictures, and asserts the expected pixels. The loading at device size, the stretching to the box, the
opacity, and the absence of a picture without a loader are verified by the unit scenario
`CanvasNetMermaid-Scenegraph-ScenePainter-Images`.

#### CanvasNetMermaid-Scenegraph-BoundedPainting: Painting Is Bounded And Cancelable

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`

Paints the node scene with the cancellation token of the test run and the default limit and asserts that the
painting completes with the expected pixels. The charge against the painted-pixel limit, the located
`mermaid-limit-exceeded` failure, the skipping of unbounded geometry, and cancellation are verified by the unit
scenarios `CanvasNetMermaid-Scenegraph-ScenePainter-PaintedPixelLimit`, `-UnboundedGeometry`, and `-Cancellation`.

#### CanvasNetMermaid-Scenegraph-Determinism: The Same Scene Gives The Same Pixels

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`

Paints the node scene twice at scale 1 with two painters and two font resolvers and asserts that the two surfaces are
identical pixel by pixel.
