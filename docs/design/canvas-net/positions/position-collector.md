### PositionCollector

![Positions Structure](PositionsView.svg)

The `PositionCollector` unit keeps the positions of one render that a selection wants. It is one `public` sealed class,
`PositionCollector`, in `PositionCollector.cs` in the namespace `DemaConsulting.CanvasNet.Positions`.

#### Purpose

A renderer reports every candidate feature once; a caller wants only those its selectors match, a bounded number of
them, and to know which selectors matched nothing. The collector is the one place that applies the selection, the cap,
and the bookkeeping of unmatched selectors, so that the renderers stay unaware of selectors and every consumer gets the
same result for the same selection.

The unit's behavior is specified by the `CanvasNet-Positions-PositionCollector-*` requirements (`Union`, `Cap`, and
`Unmatched`) in `docs/reqstream/canvas-net/positions/position-collector.yaml`.

#### Data Model

- `Selection` (`PositionSelection`) - the selection given to the constructor.
- `MaxCount` (int) - the cap on the kept positions, at least 1; `MaxPositions` = 1,000 is the default.
- `_positions` (`List<ImagePosition>`) - the kept positions, exposed read-only as `Positions`, in delivery order.
- `_matched` (`bool[]`) - whether each selector of the selection matched a candidate, by its index in the selection.
- `Truncated` (bool) - whether a matching position was not kept because of the cap.

**Invariants**: `Positions.Count` never exceeds `MaxCount`; a position is kept at most once per `Offer`; `_matched` has
one entry per selector. The collector is mutable, belongs to one render, and is not thread-safe.

#### Key Methods

- **`PositionCollector(selection, maxCount = MaxPositions)`** - stores the selection and the cap and allocates one
  matched flag per selector.
- **`Offer(position)`** - evaluates **every** selector against the candidate (not only up to the first match), setting
  the matched flag of each selector that matches, so each selector knows it matched even when the cap was already
  reached. A candidate no selector matches is dropped (`false`). A match is kept (`true`) while fewer than `MaxCount`
  positions are kept; a later match is not kept, sets `Truncated`, and returns `false`. Because the matches are kept in
  the order they are offered and each candidate is offered once, the result is the union of the selectors in the
  renderer's deterministic order, a position matched by several selectors appears once, and the order of the selectors
  never changes the order of the positions. The cap applies after the selection, so the structural features that a
  renderer offers first and a caller asks for are kept even on a picture of many features.
- **`GetUnmatchedSelectors()`** - a new array of the selectors whose matched flag is not set, in selection order; a
  selector whose matches were all cut by the cap still matched.
- **`IncludesKind(kind)`** - `Selection.IncludesKind(kind)`: whether a selector is `all` or names the kind. A renderer
  that receives this method as its `PositionKindFilter` builds only the kinds that can be wanted.
- **`ToString()`** - the count of kept positions and whether the list was truncated, for a debugger.

#### Error Handling

The constructor throws `ArgumentNullException` for a null selection and `ArgumentOutOfRangeException` for a cap below 1;
`Offer` throws `ArgumentNullException` for a null position. The exceptions of a selector's `Matches` (none for a valid
position) propagate. Nothing is handled locally; whether an unmatched selector is a warning or an error is the decision
of the caller.

#### Dependencies

`PositionSelection` and `PositionSelector` of the `PositionSelector` unit, `ImagePosition` of the `ImagePosition` unit,
and the .NET base class library (LINQ, `System.Globalization`).

#### Callers

The `SourceLoader` unit of the `CanvasNetViewer` system creates one collector per load of a Mermaid diagram or a
Vega-Lite chart, passes `Offer` as the `PositionHandler` and `IncludesKind` as the `PositionKindFilter` of the render
options, and reads `Positions`, `Truncated`, and `GetUnmatchedSelectors()` after the render; the `HelpCatalog` unit of
the `CanvasNetViewer` system reads `MaxPositions` for the `positionsMax` token of its help cards; and applications.
