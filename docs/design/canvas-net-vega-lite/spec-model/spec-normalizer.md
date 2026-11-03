### SpecNormalizer

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `SpecNormalizer` unit is the fourth software unit in the `SpecModel` subsystem. It is six partial files of the
internal static class `SpecNormalizer` in the `DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace:
`SpecNormalizer.cs` (the view tree and its walk), `SpecNormalizer.Composition.cs` (concatenations, facets, the faceted
unit view, and the layout settings), `SpecNormalizer.Repeat.cs` (the expansion of `repeat` and the replacement of the
repeat references), `SpecNormalizer.Composite.cs` (`Expand`, `ExpandedLayers`, `CompositeAxis`, and the
`CompositeBuilder` helper), `SpecNormalizer.BoxPlot.cs`, and `SpecNormalizer.ErrorBar.cs` (the expansion of the composite
marks, with the `ErrorInput` enumeration). The tree types `LayerTree`, `LayerNode`, `LayerResolve`, `ResolveMode`, and
`ViewKind` are declared in `SpecNormalizer.cs`; `CompositionSettings`, `CompositionAlign`, and `FacetDefinition` in
`SpecNormalizer.Composition.cs`; `RepeaterValues` and `RepeatLayers` in `SpecNormalizer.Repeat.cs`. Its supporting node
factories, `SpecNode.Overlay` and `SpecNode.Synthetic`, are in `SpecNode.cs`, and `SpecExpressions.With` in
`SpecExpressions.cs`. The files that port Vega-Lite normalization rules (the composite marks, the compositions, and the
repeat) carry the provenance header comment.

#### Purpose

`SpecNormalizer` reduces the views of a specification to a **tree whose leaves are the unit views that are drawn**. A
specification without a composition is one unit view, the root itself. A layered view (`layer`) is a tree of layers, each
of which inherits the `encoding`, `data`, `transform`, and size of the views above it; a composite mark (`boxplot`,
`errorbar`, `errorband`) is a layered view of the primitive layers that Vega-Lite normalizes it to. A `concat`, `hconcat`,
or `vconcat` is a concatenation of charts, a `facet` (and a unit view with a `row`, `column`, or `facet` encoding channel)
is a facet over one chart, and a `repeat` is expanded, without reading data, into a concatenation or a layered view of
copies of its `spec`. Every stage after the parser draws unit views, so the renderer needs no special case for layers,
compositions, or composite marks. The unit exists so that the inheritance and expansion rules are defined once, and so
that no node of the specification is copied or modified: a diagnostic about an inherited channel still points at the text
the author wrote.

The unit's behavior is specified by the `CanvasNetVegaLite-SpecModel-SpecNormalizer-*` requirements in
`docs/reqstream/canvas-net-vega-lite/spec-model/spec-normalizer.yaml`.

#### Data Model

**`LayerNode`** (sealed class) is a view of the tree: `Source` (the view object of the specification), `Parent`, `Depth`
(the number of views above it; the root is 0), `Kind` (`ViewKind`: `Unit`, `Layer`, `Expansion` for a composite mark
replaced by its layers, `Concat`, or `Facet`), `Resolve` (the `LayerResolve` of a view that holds views), `Children`, and
the flags `IsLayer`, `IsComposition`, `IsExpansion`, `IsSynthetic` (written by an expansion, not by the author) and
`IsFacetBody`. `Operator` is `concat`, `hconcat`, `vconcat`, `facet`, or `repeat` (a concatenation made from a `repeat`).
`Layout` (`CompositionSettings`) and `Facet` (`FacetDefinition`) belong to a concatenation or facet; `Repeater`
(`RepeaterValues`: the `row`, `column`, `repeat`, and `layer` values) is the value set that a copy of a `repeat` and
every view below it was made for. `ChartRoot` is the **chart** a view belongs to: the nearest view, itself or above, that
is a unit, layered view, or composite mark whose parent is the root, a concatenation, or a facet (null for a concatenation
or facet); a chart has one size, one set of axes, and one view background. `InComposition` tells whether a concatenation
or facet holds the view. `Data` and `Transform` are the view's **own** `data` and `transform` (the data stage passes the
table down; the synthetic body of a faceted unit view has none, they belong to its facet), and `Unit` is the unit view of
a leaf (`IsUnit`): the view object itself when the chart has no encoding to merge and no repeated value to replace (so a
single view is the root object), an **overlay** of the chart's properties with the merged `encoding` otherwise, and
for a
layer an overlay that carries the layer's `mark`, the merged `encoding`, and the `width` and `height` of its chart.

**`LayerTree`** (sealed class) is `Root`, `Units` (the leaves, in document order), `Compositions` (the concatenations and
facets, in document order), `ViewCount`, `Warnings` (`vl-composition-columns-ignored`, `vl-concat-axis-shared`, and
`vl-repeat-ref-unresolved`, in the order found; the document delivers them once, from the tree that it draws), and
`HasComposition` (a concatenation, a facet, or a repeat is part of the specification). Phase 11 completes the tree with
the data (see
_Facet cells and data sites_ below): `Units`, `Compositions`, and `ViewCount` are recomputed by `Refresh()`.

**`LayerResolve`** (sealed class, immutable) is the `resolve` of a view that holds views: `Scale(channel)`,
`Axis(channel)`, and `Legend(channel)` give `ResolveMode.Shared` or `ResolveMode.Independent`. A channel that `resolve`
does not name has the default of the view's kind: shared for a layered view; for a concatenation the scales of `x`, `y`,
`xOffset`, `yOffset`, `theta`, and `radius` independent and the others shared (`LayerResolve.ConcatDefault`); for a facet
`theta` independent and the others shared (`LayerResolve.FacetDefault`). As in Vega-Lite, the axis or legend of a channel
whose scale is independent is independent as well, and an axis or legend that is not named follows the scale; the axes of
the children of a concatenation are always independent. `LayerResolve.Default` is the resolve of a layered view that has
none.

**`CompositionSettings`** (record) is the layout of a concatenation or a facet: `Columns` (null for one row), `SpacingColumn`
and `SpacingRow`, `AlignColumn` and `AlignRow` (`CompositionAlign`: `All`, `Each`, `None`), `CenterColumn` and `CenterRow`,
and `Flush` (`bounds: "flush"`). **`FacetDefinition`** (record) is the `Row`, `Column`, or `Wrap` field definition
node of a
facet. **`FacetCellInfo`** (record) is one cell of an instantiated facet: `Index` (the position in layout order:
row-major for a `row` and `column` facet, the order of the values for a wrapped one), `RowIndex` and `ColumnIndex` (-1
for a wrapped facet; 0 for a missing row or column field), and `RowValue`, `ColumnValue`, and `WrapValue` (the
`DataValue` of the facet fields that the rows of the cell share; null for a field the facet does not have).
**`DataSite`** (record) is the group of views that load one `data` object: `Data`, `Nodes`, `Views`, and `Facets`.

**`ExpandedLayers`** (record) is the `LayerTree` with the composite marks expanded and the `SpecTree` to read the
expressions from: the parsed tree when nothing was expanded, otherwise a derived tree with the same root, source name,
and warnings whose `SpecExpressions` also holds the expressions that the expansion compiled (`SpecExpressions.With`).

**`SpecNode.Overlay(origin, properties)`** creates an object node with the name, index, position, pointer, and parent of
`origin` whose properties are the given nodes, **without adopting them**: every property keeps its own parent and
pointer, so a merged encoding does not corrupt the pointers of the nodes of the specification. **`SpecNode.Synthetic`**
creates a node written by an expansion, located (pointer, line, and column) at the composite's `mark`, which may hold
nodes of the specification (a channel definition, a `scale`) next to new ones.

**`CompositeBuilder`** (sealed class) writes the synthetic nodes of one expansion and reads the properties that every
composite mark shares: `Obj`, `Arr`, `Str`, `Num`, and `Expr` (compile an expression and register it), `Property(name)`
(the mark definition, else `config.<mark>.<name>`), `HasPart(part)` (a part is on when the mark's value is `true` or an
object, else when the configuration's value is), `PartMark(part, own...)` (the mark of a part: the configuration's part
properties, then the mark definition's `clip`, `color`, and `opacity`, then the part's own properties, then the part
object of the mark definition), `Orient` and `TryOrient`, `ContinuousAxis`, `PositionDefinition`, `Grouping`, and
`Error` (a `VegaLiteSpecException` located at the mark). The builder never modifies a node of the specification.

#### Key Methods

- `Layers(SpecTree tree, VegaLiteLimits? limits, ResolvedConfig? config)` - builds the `LayerTree` with an explicit
  stack (so
  nesting cannot overflow the call stack); the configuration (the defaults when null) gives the `concat` and `facet`
  `spacing` and `columns`. Each view is classified in this order: `concat`/`hconcat`/`vconcat` (a `Concat` node whose
  children are the object items of the array; `vconcat` has one column, `hconcat` none, a `concat` the `columns` of the
  view or the configuration), `facet` (a `Facet` node over its `spec`), `repeat` (see below), `layer` (a `Layer` node whose
  object items are its children; an item that is not an object is ignored, the parser has validated the shape), a unit
  view with a faceting channel (see below), and otherwise a unit. The unit of a layer is created by merging the encodings
  from the outermost layered view **of its chart** to the layer (a concatenation or facet hands no encoding down): a
  channel that an inner view repeats replaces the outer channel **in place**, a new channel is appended, and when only one
  encoding exists it is used as is (no overlay). A composite mark is left as a unit view with that mark. **Limits**:
  `MaxViews` counts every view as it is popped (an excess is a `VegaLiteRenderException` located at that view), and
  `MaxCompositionDepth` bounds the number of nested layers, concatenations, facets, and repeats (the root is level 1; the
  exception is located at the `layer` array, the concatenation array, the `facet`, or the `repeat`). A view written by an
  expansion counts against `MaxViews` but not against the depth.
- **Facet cells and data sites** (phase 11, on `LayerNode` and `LayerTree`; the data stage of one call uses them). The
  cells of a facet depend on the data, so `Layers` leaves a facet with **one template child** (the `spec`, or the synthetic
  body of a faceted unit view) and the data stage completes the tree once the table of the facet is known:
  `FacetViews(facet)` returns the number of views one cell holds (the template and the views below it) and the number of
  views outside the cells (the rest of the tree and the facet itself) so that the caller checks `MaxViews` **before** it
  makes the cells (`EncodingTransforms.CheckFacetViews`); `InstantiateFacet(facet, cells)` replaces the template by
  `LayerNode.CloneFor(facet, cell)` of it for each cell (the clone shares the `Source`, `Unit`, `Resolve`, `Layout`, and
  `Operator` of its original; it has the facet as `Parent`, the cell in `Cell` on every node of the cell,
  `HasCells` is true
  on the facet, a chart root of the template maps to its copy, and the copy is made with an explicit stack), then calls
  `Refresh()` so that `Units` (cell by cell, in layout order), `Compositions`, and `ViewCount` are current. A facet is
  instantiated once (`InvalidOperationException` otherwise), a view that is not a facet is an `ArgumentException`,
  and a facet
  without cells has no child. The tree belongs to one call, so the replacement in place is not shared with another
  thread. `DataSites(needed)` groups the views by the identity of the `data` object they declare - the copies of a repeated
  view and the cells of a facet share it, and the root without `data` is the first site, with `Data` null - and lists
  for each site the declaring `Nodes`, the `Views` (view objects and merged unit views of the declaring views and of the
  views below them up to the next `data`, each once) and the `Facets` among them, restricted to the views in `needed` when
  it is given, so that the data stage loads one table for each site with the union of the fields that
  `TemporalFields.CollectAll(views, facets)` finds.
- **Layout** (`ReadLayout`, after Vega-Lite's `extractCompositionLayout`): the spacing is the configuration's (20), over
  which the view's `spacing` (a number for both directions, or an object whose missing entry is the configured spacing),
  `align`, `center`, and `bounds` apply; the alignment defaults to `each` for a concatenation and `all` for a facet
  and a
  repeat. For a unit view with a `facet` channel the properties of the channel definition win over those of the view.
- **Facets.** A `facet` with a `spec` is a `Facet` node with one child, the `spec`; its `facet` is a field definition
  (`FacetDefinition.Wrap`, whose cells wrap at `columns`) or an object of `row` and `column` field definitions (a
  `columns` of the view is then dropped with the warning `vl-composition-columns-ignored`). A **unit view with a `row`,
  `column`, or `facet` encoding channel** that is the root or a child of a concatenation (not a unit view in a `layer` or
  in the `spec` of a facet) is a `Facet` node over the same view object and one synthetic child with `IsFacetBody`: the
  body's `Unit` has the view's `mark`, `encoding` without the faceting channels, `width`, `height`, `view`, `projection`,
  and `params`, while the `data`, `transform`, and `title` stay with the facet (`row` and `column` win over `facet`). The
  facet and its body are two views.
- **Repeats** (`SpecNormalizer.Repeat.cs`, Vega-Lite's `mapRepeat` and `mapLayerRepeat`). A `repeat` over an array is a
  `Concat` node (operator `repeat`, alignment `all`, the `columns` of the view) with one copy of the `spec` for each value;
  one over `row` and `column` is a `Concat` node of the row-major product (rows outer) whose columns are those of the
  `column` list (one for a row only; a `columns` of the view is dropped with the warning); one over `layer` is a `Layer`
  node of one copy per layer value; with `row` or `column` **and** `layer` every cell is a layered view (a node whose only
  properties are the `width`, `height`, and `view` of the `spec`, located at the repeat; Vega-Lite sizes a layered view
  from its layers, and the document reads the same size for a repeat of layers alone, `VegaLiteDocument.SizeSource`) of
  the layers. A copy is a `LayerNode` over the same `spec` node whose
  `Repeater` holds its values (an inner `repeat` overrides an outer value of the same name, and the children inherit it).
  The product is computed in 64 bits and checked against `MaxViews` **before** any copy is made (the cells, then the cells
  times the layers), so a repeat of a million copies is refused without making them. The data of the repeat stays on its
  node and the `data` of the `spec` is the copies' own. **References**: in the merged encoding of a unit (and in the facet
  fields of a faceted view) a `{"repeat": name}` in a channel's `field` or `datum`, the `field` of a `sort`, and the
  `field` or `datum` of a condition is replaced by a string node located at the reference; a reference with no value is
  reported once as `vl-repeat-ref-unresolved` and drops the channel (or the `sort`, or the condition). A chart with no
  reference keeps its encoding node, so a copy with nothing to replace is the `spec` object itself.
- `Expand(SpecTree tree, ResolvedConfig config, VegaLiteLimits? limits)` - the same walk with an expander: a unit
  view whose mark is `boxplot`, `errorbar`, or `errorband` becomes a layered node (`IsLayer`, `IsExpansion`) whose
  children are the synthetic views of its expansion. The expansion reads the unit's merged encoding, so the expanded layers
  do **not** inherit the encodings above the composite again, but the composite's own `data` and `transform` stay on its
  node and are applied by the data stage.
- `TryOrientation(type, definition, encoding, out orient, out problem)` - decides the orientation of a composite mark
  without throwing, for the catalog and the expansion.

**Boxplot expansion** (Vega-Lite's `normalizeBoxPlot`, without tooltips). The orientation is the continuous
(quantitative or temporal) channel; a plot with both continuous channels is vertical unless only `y` is temporal; none
is an
error. For the Tukey form (an `extent` that is a number, 1.5 by default) the layers are: a view with a `joinaggregate` of
the quartiles by the grouping fields that holds the outlier `point` layer (the rows beyond the fences) and the whisker view
(a `filter` of the rows inside the fences and an `aggregate` of their minimum and maximum, keeping the box bounds) with
the
whisker `rule` layers and the optional end `tick` layers; then a view with an `aggregate` of the first quartile, the third
quartile, and the median that holds the box `bar` and the median `tick`. For `min-max` there is one view whose `aggregate`
adds the minimum and the maximum, with the whiskers, the box, and the median. The median takes the color of a flat box
(the first and third quartile equal, by a condition on the comparison). The parts are `box`, `median`, `rule`, `ticks`
(off by default), and `outliers`; the box and median size is the mark's `size`, else `config.boxplot.size`, else
`config.mark.size`; the whiskers and outliers do not take the `size` and `color` channels. A part with no layers (all of
its parts off) writes no view. A boxplot with all parts on has nine views in the tree: the composite, two groups, the whisker
view, and five unit views.

**Errorbar and errorband expansion** (`errorBarParams`, `normalizeErrorBar`, and `normalizeErrorBand`). The input is raw
observations, a pre-aggregated range (`x2` or `y2`), or pre-aggregated errors (`xError`, `yError`, and the second error);
`TryErrorOrient` decides the orientation (a range or error channel decides it; an explicit `orient` decides only for raw
observations) and the input kind. For raw observations the center is the mark's `center`, else the median when the `extent`
is given as `iqr`, else the configuration's center (the mean); the `extent` is `stderr` (the default), `stdev`, `ci`, or
`iqr`: `stderr` and `stdev` write an `aggregate` of the center and the extent and `calculate` the two ends, `ci` an
`aggregate` of the mean with the `ci0` and `ci1` bounds, and `iqr` the median with the first and third quartiles. A range
copies the two channels as the ends; errors add the error to the value (the second error, when given, is used for the lower
end). The view is `{transform, layer}`; an errorbar has the `ticks` (lower, then upper, off by default) and the `rule`
(thickness from `thickness`), and an errorband the `band` (a `rect`, or an `area` when both `x` and `y` are encoded,
with the
band opacity from the configuration and the `interpolate` and `tension` of a two-dimensional band) and the `borders` (a
`rule` per end, or a `line`). A default errorbar has three views (composite, view, rule); with ticks, five.

**Part marks, titles, and defaults.** The title of a part is the written `title` of the measure, else the field, so the
merged title of a layered axis reads as Vega-Lite's: the title of a part counts as written, and wins over a title that
another layer derives (the document merges the titles). `scale` and `axis` of the
measure are copied to the primary channel of every part. Grouping channels are the remaining channels that have a field;
a `bin`, a `timeUnit`, or an `aggregate` on a grouping channel is not applied (the catalog reports it as
`vl-composite-grouping`): the rows are grouped by the raw field and an aggregating channel is left out.

#### Error Handling

Normalization is a function of an already-validated tree. A limit is a `VegaLiteRenderException` located at the view that
crossed it, thrown while the tree is built (the walk is iterative, so a hostile nesting cannot overflow the stack, and the
number of views is bounded before the next view is queued, and a `repeat` before its copies are made). A composite mark
whose orientation cannot be decided, which
has
no continuous channel, whose measure is not a field, or whose range and error channels cannot be combined is a
`VegaLiteSpecException` located at the mark (`CompositeBuilder.Error`); so is a `facet` or `repeat` with no `spec`
object (the parser has already refused it); these are mistakes of the specification, so they are
not reports. What is dropped or ignored without being a mistake (the `columns` of a facet by `row` and `column` or of a
repeat of rows and columns, a shared axis of a concatenation, a repeat reference with no value) is a warning in
`LayerTree.Warnings`. A null argument is an `ArgumentNullException`. The unit performs no file or network access and
holds no state;
the returned trees are immutable.

#### Dependencies

- **`SpecNode`**, **`SpecTree`**, **`SpecExpressions`** - the tree types of the `SpecParser` unit (`Overlay`, `Synthetic`,
  and `With` were added for this unit)
- **`ConfigResolver`** and **`ResolvedConfig`** - the defaults and the configuration of the composite marks
  (`config.boxplot`, `config.errorbar`, `config.errorband`)
- **`ExpressionParser`** and **`CompiledExpression`** of the `Expressions` subsystem - the expressions of an expansion are
  compiled when they are written
- **`JsNumber`** of the `Data` subsystem - the number text of an expression
- **`VegaLiteLimits`**, **`VegaLiteSpecException`**, **`VegaLiteRenderException`** - the limits and the errors

#### Callers

`VegaLiteDocument` calls `Expand` for every measure and render (`PrepareChart`) and walks the returned `LayerTree`
(`VegaLiteDocument.Layers.cs`, and `VegaLiteDocument.Composition.cs` for a concatenation); it forwards the `Warnings` of
the tree that `Expand` returns (and only of that tree, so that each warning is delivered once) and does not draw the
views below a facet (only its title and background). `FeatureCatalog` calls `Layers` (with a bounded view limit, so that
an over-deep specification yields no layer encoding visit instead of a catalog failure and a large repeat is not
expanded without limit) to visit the merged
encoding of each layer that is not inside a facet, and `TryOrientation` to decide which channel of a composite mark
groups the rows. `ScaleResolver.ResolveLayers` reads the `LayerNode` chain of each layer (the nested `resolve`). The tests
in `test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/SpecNormalizerTests.cs`, `SpecNormalizerCompositionTests.cs`,
`SpecNormalizerRepeatTests.cs`, `SpecNormalizerFacetCellTests.cs`, `SpecNormalizerBoxPlotTests.cs`, and
`SpecNormalizerErrorBarTests.cs` call the unit
directly through `InternalsVisibleTo`.
