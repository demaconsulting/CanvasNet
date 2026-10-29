## Marks

![Marks Structure](VegaLiteMarksView.svg)

### Overview

The `Marks` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Marks`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Marks/`) turns data rows, resolved scales, and mark properties into scene items:
the geometry and paint of every drawn mark. It is **internal**: its types are visible only to the `VegaLiteDocument` unit
and, through `InternalsVisibleTo`, to the test assembly. It contains one unit:

- **MarkBuilder** - the internal `MarkBuilder` for the marks `bar`, `rect`, `point`, `circle`, `square`, `rule`, and
  `tick`; see _MarkBuilder Unit Design_ (`marks/mark-builder.md`)

The subsystem's namespace documentation is in `Marks/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Marks-*` requirements in `docs/reqstream/canvas-net-vega-lite/marks.yaml`. Marks other than the seven
(`line`, `area`, `trail`, `text`, `arc`, the composite marks, and the rest) are later phases and are reported by the
`FeatureCatalog` as `vl-mark-<type>`.

### Interfaces

**Exposed** (internal only): `MarkBuilder.IsSupported(string?)`, which tells whether a mark type is drawn, and
`new MarkBuilder(ResolvedConfig, VegaLiteLimits?, SceneBudget?, string?).Build(SpecNode view, DataTable, ResolvedScales,
double width, double height, CancellationToken)`, which returns a `SceneGroup` (role `marks`) in the coordinates of
the data rectangle.

**Consumed**: `SpecNode` and `ResolvedConfig` from the `SpecModel` subsystem; `DataTable` and `DataValue` from the `Data`
subsystem; `ResolvedScales`, `ResolvedScale`, and `ScaleModel` from the `Scales` subsystem; `SceneGroup`, `SceneRect`,
`SceneRule`, `SceneSymbol`, `SceneClip`, and `SceneBudget` from the `Scenegraph` subsystem; and the public `VegaLiteLimits`,
`VegaLiteLocation`, and `VegaLiteRenderException` of the `VegaLiteDocument` unit.

**Consumers**: `VegaLiteDocument`, which builds the marks once for the final data rectangle and places the marks group
between the axes behind them and the axes in front.

### Design

**Geometry from the scales, paint from the configuration.** For each row the builder maps the channels through the scale
models to positions, sizes, opacities, and colors, resolves the mark properties through the configuration precedence chain
(the mark definition, then style, `config.<mark>`, `config.mark`, and the Vega styles), and emits one scene item per
visible mark. The scene items carry resolved geometry and CSS color strings, so the result can be asserted analytically
without pixels.

**Vega-Lite's rules.** Bars, rectangles, and ticks on a discrete position fill the band; on a continuous position they are
centered on the value with default thickness; the measure axis runs from the value to 0; symbols sit at the center of
their band or value; a rule is a full-width or full-height line, or a stem from the value to zero. Orientation comes
from `ScaleResolver.InferOrient`. A row with an invalid value on a continuous channel is not drawn (Vega-Lite 6 `filter`
behavior), and a value outside an explicit discrete domain is not drawn.

**Bounded.** The item budget is charged for each item as it is created (a row that draws nothing costs nothing), so a
huge or hostile data set fails with a located `VegaLiteRenderException` before more memory is spent, and the
cancellation token is polled every 1,024 rows. Marks are clipped to the data rectangle only when the mark definition
says `"clip": true`.
