## Marks

![Marks Structure](VegaLiteMarksView.svg)

### Overview

The `Marks` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Marks`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Marks/`) turns data rows, resolved scales, and mark properties into scene items:
the geometry and paint of every drawn mark. It is **internal**: its types are visible only to the `VegaLiteDocument` unit
and, through `InternalsVisibleTo`, to the test assembly. It contains two units:

- **MarkBuilder** - the internal `MarkBuilder`, a sealed partial class in six files, for the marks `bar`, `rect`,
  `point`, `circle`, `square`, `rule`, `tick`, `line`, `area`, `trail`, `text`, and `arc`, with the point and line
  overlays of the path marks; see _MarkBuilder Unit Design_ (`marks/mark-builder.md`)
- **CurveBuilder** - the internal static `CurveBuilder` with `CurveKind`, `CurvePoint`, and `AreaPoint`: the sixteen
  d3-shape curves, the `line` and `area` generators, and the upper bound of the number of path segments, for the
  paths of the `line` and `area` marks; see _CurveBuilder Unit Design_ (`marks/curve-builder.md`)

The subsystem's namespace documentation is in `Marks/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Marks-*` requirements in `docs/reqstream/canvas-net-vega-lite/marks.yaml`. Marks other than the twelve
(`image`, `geoshape`, and the composite marks `boxplot`, `errorbar`, and `errorband`) are not drawn and are reported by
the `FeatureCatalog` as `vl-mark-<type>`.

### Interfaces

**Exposed** (internal only): `MarkBuilder.IsSupported(string?)`, which tells whether a mark type is drawn, and
`new MarkBuilder(ResolvedConfig, VegaLiteLimits?, SceneBudget?, string?, RowExpressions?).Build(SpecNode view, DataTable,
ResolvedScales, double width, double height, CancellationToken)`, which returns a `SceneGroup` (role `marks`) in the
coordinates of the data rectangle; the table is the **Main** table of the implicit pipeline (the fields that the scales
name). The `CurveBuilder` surface (`TryParse`, `SupportsArea`, `MaxSegments`, `Line`, and `Area`) is consumed by
`MarkBuilder` only.

**Consumed**: `SpecNode`, `ConfigNode`, and `ResolvedConfig` from the `SpecModel` subsystem; `DataTable`, `DataValue`,
`TimeCalendar`, and `JsDateParser` from the `Data` subsystem (the last two for the date of a temporal `datum`);
`ResolvedScales`, `ResolvedScale`, and `ScaleModel` from the `Scales` subsystem; `NumberFormat` from the `Scales`
subsystem and `TimeLabels` from the `Guides` subsystem (the formats of the text of a text mark); `RowExpressions`,
`EncodingPlan`, `BinKind`, `WorkMeter`, and `StableSorter` from the `Transforms` subsystem; `SceneGroup`, `SceneRect`,
`SceneRule`, `SceneSymbol`, `ScenePath` with `ScenePathSegment`, `SceneTrail` with `SceneTrailPoint`, `SceneText`,
`SceneArc`, `SceneClip`, and `SceneBudget` from the `Scenegraph` subsystem; and the public `VegaLiteLimits`,
`VegaLiteLocation`, `VegaLiteRenderException`, and `VegaLiteSpecException` of the `VegaLiteDocument` unit.

**Consumers**: `VegaLiteDocument`, which builds the marks once for the final data rectangle and places the marks group
between the axes behind them and the axes in front.

### Design

**Geometry from the scales, paint from the configuration.** For each row the builder maps the channels through the scale
models to positions, sizes, opacities, and colors, resolves the mark properties through the configuration precedence chain
(the mark definition, then style, `config.<mark>`, `config.mark`, and the Vega styles), and emits one scene item per
visible mark. The scene items carry resolved geometry and CSS color strings, so the result can be asserted analytically
without pixels.

**Vega-Lite's rules.** Bars, rectangles, and ticks on a discrete position fill the band; on a continuous position they are
centered on the value with default thickness; the measure axis runs from the value to 0 (or, for a stacked channel and a
secondary channel `x2` or `y2`, from the first to the second end); symbols sit at the center of
their band or value (the upper end of a stack, the middle of a bin); a rule is a full-width or full-height line, a stem
from
the value to zero, or a range; a bar or rect on a binned channel spans its bin, and on a time-unit channel of a time
scale it spans its unit (a month, say; a point, circle, square, or rule sits at the start of its unit unless a band
position says otherwise, and a tick at the middle). Orientation comes
from `ScaleResolver.InferOrient`. A row with an invalid value on a continuous channel is not drawn (Vega-Lite 6 `filter`
behavior) by every mark except the path marks, and a value outside an explicit discrete domain is not drawn.

**Path marks, text, and arcs.** A `line`, `area`, or `trail` is one scene item per group of rows (the fields that
`EncodingPlan` names, in order of first appearance), its rows sorted by position or by the `order` channel; the outline
of a line or an area is the curve that the `interpolate` property names, generated by `CurveBuilder` from the pixel
points; an invalid row **breaks** the path (the default of the `invalid` property) or is dropped (`filter`). A `point`
or `line` overlay adds the points and the top line of these marks. A `text` mark is one text item per row at its
position (plus a polar offset), with the label formatted from the field, the datum, the value, or the `text` property.
An `arc` mark is one sector per row, a rectangle in polar coordinates whose angles come from `theta` and `theta2` and
whose radii come from `radius` and `radius2`; the polar position of a stacked `text` is the middle of its slice, so
that the labels of a pie sit in their slices. The `order` channel decides the drawing order of every other mark that is
not stacked. See _MarkBuilder Unit Design_ for the rules.

**Bounded.** The item budget is charged for each item as it is created (a row that draws nothing costs nothing), so a
huge or hostile data set fails with a located `VegaLiteRenderException` before more memory is spent, and the
cancellation token is polled every 1,024 rows. The paths are bounded in the same way, in proportion to their points:
the scene items of a path are charged with an upper bound of its segments (`CurveBuilder.MaxSegments`) **before** the
segments are generated, and the grouping, the sorting, and the generation are charged to the evaluation-step budget; the
label of a text mark is bounded by `MaxStringLength` and all the labels of one mark by sixteen times that, before the
labels are formatted, and every line of a label is a scene item. Marks are clipped to the data rectangle only when the
mark definition says `"clip": true`; an explicit scale domain does not clip them.
