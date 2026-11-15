## Positions

![Positions Structure](PositionsView.svg)

The `Positions` subsystem is the tenth software subsystem in CanvasNet. It describes where the features of a rendered
picture are (a node of a diagram, an edge, a bar of a chart, an axis), so that a caller can place a callout or a leader
line on the picture without probing its pixels. It consists of three units: `ImagePosition` (the immutable record of
one feature with its geometry box and attachment pixel, and the value types it is made of), `PositionSelector` (the
grammar that picks features, and the union of several selectors), and `PositionCollector` (the per-render list of the
features a selection wants, up to a cap).

### Overview

The renderers that know their features (the `CanvasNetMermaid` and `CanvasNetVegaLite` systems) report them through a
`PositionHandler` callback of their render options; the `CanvasNetViewer` system collects them with a selection, maps
them through the image steps that follow the render, and hands them to the `CanvasNetTool` system, which prints them
with `render --positions`. The model and the selector grammar live in the core package once, so every renderer reports
positions the same way and every consumer reads them the same way, without depending on a renderer.

The dependency direction is one-way: the renderers and the viewer depend on the subsystem; the subsystem depends only
on `ImageParameterValues` of the `ImageSources` subsystem (the scalar grammar of a bare selector value, so that a
selector value reads exactly like a `--param` value) and on the .NET base class library. No other core subsystem
depends on it. There is no global state.

### Interfaces

The public types of the subsystem are in the namespace `DemaConsulting.CanvasNet.Positions`:

- **`ImagePosition`** - one feature: `Kind`, `Id`, `Text`, `Line`, `View`, `ViewName`, `Mark`, `From`, `To`, `Index`,
  `Values`, `Data`, `ChannelNames`, `Box`, and `Pixel`, with `TryGetValue(name, out value)`,
  `TryGetData(name, out value)`, and the limit `MaxDataFields` (32).
- **`PositionBox`, `PositionPoint`** - the geometry box (`X`, `Y`, `Width`, `Height`, `Center`, `Corners()`,
  `Enclose(points)`, `IsValid()`) and a point (`X`, `Y`, `IsValid()`) in the continuous pixel coordinates of the
  picture.
- **`PositionValue`, `PositionValueKind`** - a typed scalar (null, boolean, finite number, or text) with typed
  equality.
- **`PositionSelector`** - one parsed selector: `Parse`, `TryParse`, `Matches`, `Text`, `Kind`, `IsAll`, and the limits
  `MaxSelectorLength` (256) and `MaxPredicates` (16).
- **`PositionSelection`** - the union of several selectors: `Parse(texts)`, `All`, `Selectors`, `IncludesKind`,
  `Matches`, and the limit `MaxSelectors` (32).
- **`PositionCollector`** - the positions of one render: `Offer`, `Positions`, `Truncated`, `GetUnmatchedSelectors()`,
  `IncludesKind`, `Selection`, `MaxCount`, and the default cap `MaxPositions` (1,000).

The subsystem consumes `ImageParameterValues.TryParseParameter` of the `ImageSources` subsystem and `System.Text.Json`
(`Utf8JsonReader`, to read a quoted selector string), and nothing else.

### Design

**One neutral record.** A position carries only what identifies a feature and where it is: its kind and id, a few
facts that a selector can compare, its geometry box (the bounds of its geometry, stroke not included, as SVG `getBBox`
measures it), and its attachment pixel (a deterministic point a leader line can end on, chosen by the aim rule of the
kind). Both are in the pixels of the picture the caller receives; a later image step that cannot follow them sets them
to `null`. The record validates every fact in its `init` setters, so an invalid position cannot exist.

**Select, then cap.** A renderer offers every candidate position once, in its own deterministic order (structural
features such as titles, views, and nodes first). The collector evaluates every selector against every candidate, keeps
the union of the matches in delivery order, and applies the cap only after the selection, so a caller who asks for a few
features of a chart of many marks still gets them. A selector is unmatched only when no candidate matched it, so the
caller can turn a typo into a warning even when the cap cut the list.

**Skip what nobody asked for.** `PositionSelection.IncludesKind` and `PositionCollector.IncludesKind` tell whether any
selector can match a kind; a renderer that receives that function as its `PositionKindFilter` does not build the
positions of the other kinds at all.

**Bounded work.** A selector is at most 256 characters with at most 16 predicates, a selection at most 32 selectors, and
the glob matcher takes at most (pattern length) x (id length) steps, so parsing and matching an untrusted command line
costs a bounded amount. The model types, the selector, and the selection are immutable and thread-safe; a collector is
mutable, belongs to one render, and is not thread-safe.

### Dependencies

The subsystem depends on the `ImageSources` subsystem (`ImageParameterValues`, for the bare values of a selector) and
on the .NET base class library (`System.Text.Json`, `System.Globalization`). No other CanvasNet subsystem depends on
it. Its consumers are the `CanvasNetMermaid` and `CanvasNetVegaLite` systems (they create `ImagePosition` values), the
`CanvasNetViewer` system (it takes a selection, collects, and maps the positions), the `CanvasNetTool` system (it
parses the selectors and prints the positions), and applications.

### Callers

The `ScenePositions` units of the `CanvasNetMermaid` and `CanvasNetVegaLite` systems create the positions; the
`RenderCommand` unit of the `CanvasNetTool` system parses the `--positions` selectors with `PositionSelection.Parse`
and writes the positions; the `SourceLoader` unit of the `CanvasNetViewer` system creates the `PositionCollector` of a
render and passes its `Offer` and `IncludesKind` to the render options; and the `ImagePipeline` unit maps the kept
positions with `PositionBox.Corners` and `PositionBox.Enclose`.
