## Svek Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Svek` subsystem of CanvasNetPlantUml
(namespace `DemaConsulting.CanvasNet.PlantUml.Svek`): the `SvekModel`, `SvekSyntax`, `SvekLayout`, `SvekGeometry`, and
`SvekRenderer` units working together.

### Verification Approach

The subsystem is verified independently of the diagram types that lower their models to it (the Class subsystem's
verification covers class and object diagrams end to end, and the State subsystem's state diagrams), the way the graph
diagrams use it: the tests read small original diagram texts with `SvekSyntax`, lower them to a `SvekDiagram` with test
entity images (boxes of a given size with a rectangle, ellipse, or diamond outline), lay them out with `SvekLayout` over
the dagre layout of the CanvasNetLayout system (used as is), draw them with `SvekRenderer`, and assert the scene and the
placement: the sizes, the boxes, the routes, the decorations, the labels, the notes, and the clusters. The Syntax,
Styling, and Scenegraph subsystems are used as is. No mocking is required; the internal types are reached through the
test assembly's `InternalsVisibleTo` access. Subsystem tests reside in `Svek/SvekTests.cs`; the shared helpers (test
entity images, the context of a layout call, the statements of a text, the box and route of a placed element, overlap
and containment checks) are in `Svek/SvekTestHelpers.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory values; no files, threads, or shared state

### Acceptance Criteria

A subsystem-level test run passes when every scenario below passes. A size, a box, a route, a count, a code, or a
message that differs from the expected value constitutes a failure.

### Subsystem-Level Test Scenarios

#### CanvasNetPlantUml-Svek-Reading: The Shared Statements Are Read

**Tests**: `SvekTests.PlantUmlSvek_ReadLayoutRender_LinksNotesAndLabels`

Reads a diagram text with a title, an extension, a composition with an end label and a label with its direction mark, a
dashed arrow, a note attached to an entity, and a floating note linked to an entity, lowers it with test entity images,
lays it out, and draws it; asserts the title, the four entities, and the decorations, labels, and notes that the drawing
shows (see the Drawing scenario).

#### CanvasNetPlantUml-Svek-Layout: The Diagram Is Laid Out In Ranks

**Tests**: `SvekTests.PlantUmlSvek_ClustersAndRegions_DrawnWithoutOverlaps`,
`SvekTests.PlantUmlSvek_ReadLayoutRender_LinksNotesAndLabels`

Lays out, left to right, a package that no link crosses, a node cluster that a link crosses, and a cluster with two
concurrent regions side by side; asserts that every entity is inside all its clusters, that no two entities overlap,
that the package and the node do not overlap, and that the regions have one separator; lays out the read diagram of the
Reading scenario and asserts that the floating note points at its entity instead of drawing its link.

#### CanvasNetPlantUml-Svek-Drawing: Links, Clusters, And Notes Are Drawn

**Tests**: `SvekTests.PlantUmlSvek_ReadLayoutRender_LinksNotesAndLabels`,
`SvekTests.PlantUmlSvek_ClustersAndRegions_DrawnWithoutOverlaps`

Asserts the decoration at the tail of the extension, the filled diamond of the composition, the dashed line of the
dotted arrow, the label `owns` with its direction triangle, the end label `1..*`, the texts of the two notes, and that
drawing the diagram twice gives the same view box and items; asserts the titles of the three clusters in paint order
and a finite view box.

#### CanvasNetPlantUml-Svek-Limits: The Layout Is Bounded

**Tests**: `SvekTests.PlantUmlSvek_LayoutLimits_FailClosedAndCancel`

Lays out a lowered diagram of five links under a work budget of 100 and asserts a `plantuml-limit-exceeded` error
located at the first statement that names `PlantUmlLimits.MaxLayoutWork`; asserts that a canceled call throws
`OperationCanceledException`.
