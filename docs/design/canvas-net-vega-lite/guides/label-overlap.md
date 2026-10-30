### LabelOverlap

![Guides Structure](VegaLiteGuidesView.svg)

The `LabelOverlap` unit is the fourth unit in the `Guides` subsystem. It is one source file, `LabelOverlap.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines the `internal static` class `LabelOverlap`. The file
carries the provenance header comment `Portions derived from Vega (BSD-3-Clause)`. The code was extracted unchanged
from the `AxisBuilder` unit, where it was private, so that the labels of a gradient legend can share it; the
extraction changed no behavior (the existing `AxisBuilder` tests pass unchanged).

#### Purpose

`LabelOverlap` decides which labels of a guide are hidden because they overlap their neighbors, with the parity and
greedy methods of Vega's `Overlap` transform. It is the single place for that rule, so that an axis and a legend
thin their labels the same way. The unit's behavior is specified by the `CanvasNetVegaLite-Guides-LabelOverlap-*`
requirements in `docs/reqstream/canvas-net-vega-lite/guides/label-overlap.yaml`.

#### Data Model

The unit is stateless. Its input is the list of `SceneText` labels of a guide in guide order, the `TextMeasurer` that
gives the box of a label, the method (the constants `Parity` and `Greedy`, or null for none), and the separation in
pixels; its output is the set of labels to hide (compared as records, so two labels at different positions never
collide). `FromSetting` maps a `labelOverlap` configuration node to a method: a boolean true is parity, the strings
`parity` and `greedy` name their method, and everything else (false, other text, absent) is none.

#### Key Methods

- `Hidden(labels, measurer, method, separation, cancellationToken)` - Vega's overlap removal. Only labels whose box
  is wider and taller than one pixel take part (a label without content is skipped and never hidden). With fewer than
  three such labels, or when no two neighbors are closer than the separation (`separation > max(gap in x, gap in y)`
  of the two boxes), nothing is hidden. Otherwise the method runs and repeats while at least three labels remain and
  some neighbors still overlap: **parity** keeps the labels at even positions and hides the odd ones; **greedy** keeps
  the first label and hides each later label that overlaps the last label that stayed. When the rounds end with fewer
  than three labels and the last label of the guide was hidden, it is made visible again and the last label that
  stayed is hidden in its place (when more than one label stayed), so that the end of an axis keeps its label.
- `FromSetting(node)` - the reading of the setting described above.

**Preconditions and postconditions**: labels and measurer are not null; the result never contains a label that was
not given; every round hides at least one label or ends the loop, so the work is at most the logarithm of the number
of labels rounds for parity and one pass per round for greedy, each pass linear in the labels that stay.

#### Error Handling

A null argument is `ArgumentNullException`; a canceled token is `OperationCanceledException`, polled before each round
(the work between two polls is bounded by the number of labels). The unit validates nothing else and performs no I/O.
**Limits**: the number of labels is bounded by the callers before any label exists (`VegaLiteLimits.MaxTicks` for an
axis, the legend entry limits for a legend), and the unit allocates only the list and set of that size.
**Determinism**: the result depends only on the labels, the measurer, the method, and the separation; the order of
the hidden set is not observable because callers test membership.

#### Dependencies

The `TextMeasurer` unit (the box of a label), `SceneText` and `SceneBounds` of the `Scenegraph` subsystem, and
`ConfigNode` of the `SpecModel` subsystem for `FromSetting`.

#### Callers

`AxisBuilder` (the labels of an axis, with the method it resolves from `labelOverlap` and the discrete or continuous
default) and `LegendBuilder` (the labels of a gradient or discrete gradient legend). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/LabelOverlapTests.cs` call the unit directly through
`InternalsVisibleTo`.
