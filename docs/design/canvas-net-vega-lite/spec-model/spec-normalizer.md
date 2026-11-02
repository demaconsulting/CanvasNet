### SpecNormalizer

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `SpecNormalizer` unit is the fourth software unit in the `SpecModel` subsystem. It is four partial files of the
internal static class `SpecNormalizer` in the `DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace:
`SpecNormalizer.cs` (the layer tree), `SpecNormalizer.Composite.cs` (`Expand`, `ExpandedLayers`, `CompositeAxis`, and the
`CompositeBuilder` helper), `SpecNormalizer.BoxPlot.cs`, and `SpecNormalizer.ErrorBar.cs` (the expansion of the composite
marks, with the `ErrorInput` enumeration). The tree types `LayerTree`, `LayerNode`, `LayerResolve`, and `ResolveMode` are
declared in `SpecNormalizer.cs`. Its supporting node factories, `SpecNode.Overlay` and `SpecNode.Synthetic`, are in
`SpecNode.cs`, and `SpecExpressions.With` in `SpecExpressions.cs`. The three files of the composite marks carry the
provenance header comment for the Vega-Lite normalization rules they port.

#### Purpose

`SpecNormalizer` reduces the views of a specification to the **unit views that are drawn**. A specification without
`layer` is one unit view, the root itself. A layered view (`layer`) is a tree of layers, each of which inherits the
`encoding`, `data`, `transform`, and size of the views above it; a composite mark (`boxplot`, `errorbar`, `errorband`)
is a layered view of the primitive layers that Vega-Lite normalizes it to. Every stage after the parser draws unit views,
so the renderer needs no special case for layers or composite marks. The unit exists so that the inheritance and
expansion rules are defined once, and so that no node of the specification is copied or modified: a diagnostic about an
inherited channel still points at the text the author wrote.

The unit's behavior is specified by the `CanvasNetVegaLite-SpecModel-SpecNormalizer-*` requirements in
`docs/reqstream/canvas-net-vega-lite/spec-model/spec-normalizer.yaml`.

#### Data Model

**`LayerNode`** (sealed class) is a view of the tree: `Source` (the view object of the specification), `Parent`, `Depth`
(the number of layered views above it; the root is 0), `Resolve` (the `LayerResolve` of a layered view), `Children`,
and the flags `IsLayer`, `IsExpansion` (a composite mark replaced by its layers), `IsSynthetic` (written by an
expansion, not by the author), and `UndrawnComposition` (the name of a `concat`, `hconcat`, `vconcat`, `facet`, or
`repeat` that the view holds). `Data` and `Transform` are the view's **own** `data` and `transform` (the data stage passes
the table down), and `Unit` is the unit view of a leaf (`IsUnit`): for a view without `layer`, the root object itself,
and for a layer, an **overlay** node that carries the layer's `mark`, the merged `encoding`, and the `width` and `height`
of the top-level view.

**`LayerTree`** (sealed class) is `Root`, `Units` (the leaves that are drawn, in document order), `Undrawn` (the
compositions that are not drawn yet that were met), `ViewCount`, and `HasUndrawn`.

**`LayerResolve`** (sealed class, immutable) is the `resolve` of a layered view: `Scale(channel)`, `Axis(channel)`, and
`Legend(channel)` give `ResolveMode.Shared` or `ResolveMode.Independent`. A channel that `resolve` does not name has a
shared scale; as in Vega-Lite, the axis or legend of a channel whose scale is independent is independent as well, and an
axis or legend that is not named follows the scale. `LayerResolve.Default` is the resolve of a view that has none.

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

- `Layers(SpecTree tree, VegaLiteLimits? limits)` - builds the `LayerTree` with an explicit stack (so nesting cannot
  overflow the call stack). A view that holds a composition that is not drawn becomes an undrawn node and is not
  entered. A view without `layer` is a unit; with `layer`, its object items become its children (an item that is not an
  object is ignored; the parser has validated the shape). The unit of a layer is created by merging the encodings from the
  outermost layered view to the layer: a channel that an inner view repeats replaces the outer channel **in place**, a new
  channel is appended, and when only one encoding exists it is used as is (no overlay). A composite mark is left as a
  unit view with that mark. **Limits**: `MaxViews` counts every view as it is popped (an excess is a
  `VegaLiteRenderException` located at that view), and `MaxCompositionDepth` bounds the number of nested layered views
  (the root layered view is level 1; the exception is located at the `layer` array). A view written by an expansion
  counts against `MaxViews` but not against the depth.
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
number of views is bounded before the next view is queued). A composite mark whose orientation cannot be decided, which
has
no continuous channel, whose measure is not a field, or whose range and error channels cannot be combined is a
`VegaLiteSpecException` located at the mark (`CompositeBuilder.Error`); these are mistakes of the specification, so they
are
not reports. A null argument is an `ArgumentNullException`. The unit performs no file or network access and holds no state;
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
(`VegaLiteDocument.Layers.cs`). `FeatureCatalog` calls `Layers` (with generous limits, so that an over-deep specification
yields no layer encoding visit instead of a catalog failure) to visit the merged encoding of each layer, and
`TryOrientation` to decide which channel of a composite mark groups the rows. `ScaleResolver.ResolveLayers` reads the
`LayerNode` chain of each layer (the nested `resolve`). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/SpecNormalizerTests.cs`, `SpecNormalizerBoxPlotTests.cs`, and
`SpecNormalizerErrorBarTests.cs` call the unit directly through `InternalsVisibleTo`.
