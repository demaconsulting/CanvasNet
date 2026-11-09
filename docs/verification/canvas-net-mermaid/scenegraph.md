## Scenegraph Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scenegraph` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Scenegraph`), containing two units: `SceneModel` (the immutable scene records, their
bounds, and the conversion of their geometry to a core path) and `ScenePainter` (the painter of a scene onto a
`Surface`, with its placement and its picture-loader contract).

### Verification Approach

The `Scenegraph` subsystem is verified at two tiers. The unit tests (see the unit verification designs under
`scenegraph/`) verify each unit in isolation, including the pictures, the painted-pixel limit, the skipping of
unbounded geometry, and cancellation. The subsystem-level scenarios in `ScenegraphTests` verify the units working
together: a scene of one node - a filled and stroked rectangle with a centered label inside a translated group, built
from `SceneModel` records as the layout builds it, with the location of its construct - is measured with
`Scene.BoundsOf` and painted by `ScenePainter` with the font resolver of a call, twice at scale 1 and once at scale 2,
under a painted-pixel limit smaller than the node, and with a canceled token; and a scene of one picture inside a
translated group is painted at scale 2 with and without a picture loader; and a scene of a box with a drop shadow in a
translated group is measured and painted at scale 2; and a line of two text runs, the first underlined, is painted at
scale 2. The expectations are analytic: the bounds
are the rectangle moved by the group translation; the fill color is found at the node center at both scales; the
label leaves ink at the center of the node; the two paintings at scale 1 are identical pixel by pixel; the picture is
asked for at the device size of its box and covers exactly the device pixels of the box; the shadow is the box moved
by its scaled offset, under the box and outside its bounds; the underline bar lies under the advance of the
underlined run only; and the failures carry the
code and the location of the node. No golden images are used.

The scenarios use a test picture loader that gives a red picture of the requested size and records the requests, as
the caller's resolver would through the `NodeImages` loader of a diagram; otherwise the subsystem uses no mocking or
stubbing, and the core `Surface`, `PathFiller`, `PathStroker`, `TextRenderer`, the font resolver, and the bundled
fonts are real. The subsystem is internal, so its tests reach it through the test assembly's `InternalsVisibleTo`
access. The painting of complete diagrams, the pictures of the caller's resolver, the painted-pixel limit, and
cancellation through the public API are further verified by the system tests.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; a recording test picture loader stands in for the loader of a diagram's pictures
- **Fonts**: The bundled Liberation fonts, through the resolver of `ScenePainter.CreateFontResolver`
- **Isolation**: Each test builds its own scene, painter, and surface; no files are read

### Acceptance Criteria

The `Scenegraph` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the two unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Scenegraph-Model: The Scene Describes The Diagram

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`,
`ScenegraphTests.Scenegraph_PaintBoxScene_ShadowIsUnderTheBoxAndOutsideTheBounds`,
`ScenegraphTests.Scenegraph_PaintUnderlinedRun_BarsOnlyThatRun`

Builds a node scene from a group with an offset of (48, 28) holding a rectangle from (-40, -20) to (40, 20) with a
role and Mermaid's element id and a label line, and asserts that the bounds of the scene are the rectangle (8, 8, 80,
40), the stroke excluded; and builds a white 20 by 20 box with a sharp black drop shadow 4 to the right and 4 down in a
group at (10, 10), and asserts that its bounds are the box (10, 10, 20, 20), the shadow excluded. Builds a text line of
two runs of `HHH` in Mermaid's default family stack at 32 pixels on the baseline y = 20, the first marked underlined,
which the painter then draws with its bar (see the next scenario).

#### CanvasNetMermaid-Scenegraph-Painting: The Scene Is Painted At A Placement

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`,
`ScenegraphTests.Scenegraph_PaintBoxScene_ShadowIsUnderTheBoxAndOutsideTheBounds`,
`ScenegraphTests.Scenegraph_PaintUnderlinedRun_BarsOnlyThatRun`

Paints the node scene over a transparent background at scale 1 and at scale 2 and asserts that the node fill is at
the same scene point of both surfaces and that the label, drawn in the bundled font for Mermaid's default family
stack, leaves ink at the center of the node; and paints the box scene at scale 2 and asserts that the box is white
inside, that its shadow is black in the 8 device pixels to the right of and below the box (moved by the scaled
offset) and under it, and that the pixels beyond the shadow are transparent. Paints the line of two runs at scale 2
and asserts that the device row y = 44, under the baseline and below the capitals, is fully covered for 130 to 140
pixels under the first run (twice its advance of about 69 units) and has no ink at all under the second run.

#### CanvasNetMermaid-Scenegraph-Pictures: Pictures Come Only From The Scene's Loader

**Tests**: `ScenegraphTests.Scenegraph_PaintPictureScene_PicturesComeOnlyFromTheLoader`

Paints a scene with a 10 by 6 picture centered in a group at (10, 5) at scale 2 with a recording loader and asserts
that the loader is asked once for `pic.png` at 20 by 12 device pixels, that the picture covers the device pixels from
(10, 4) to (29, 15) and nothing to its left or right, and that the same scene painted without a loader is fully
transparent. The opacity and the stretching of a picture that comes back at another size are verified by the unit
scenario `CanvasNetMermaid-Scenegraph-ScenePainter-Images`.

#### CanvasNetMermaid-Scenegraph-BoundedPainting: Painting Is Bounded And Cancelable

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_OverPaintedPixelLimit_FailsAtTheNode`,
`ScenegraphTests.Scenegraph_PaintNodeScene_CanceledToken_Throws`

Paints the node scene, whose node fills 80 by 40 device pixels, with a painted-pixel limit of 1000 and asserts that
the painting fails with `mermaid-limit-exceeded` located at the node (`n.mmd`, line 2, column 3) and naming
`MermaidLimits.MaxPaintedPixels`, and that a limit of 100000 paints the node fill. Paints the node scene with a
canceled token and asserts an operation-canceled error. The skipping of unbounded geometry is verified by the unit
scenario `CanvasNetMermaid-Scenegraph-ScenePainter-UnboundedGeometry`.

#### CanvasNetMermaid-Scenegraph-Determinism: The Same Scene Gives The Same Pixels

**Tests**: `ScenegraphTests.Scenegraph_PaintNodeScene_TwoScales_DeterministicAndScaled`

Paints the node scene twice at scale 1 with two painters and two font resolvers and asserts that the two surfaces are
identical pixel by pixel.
