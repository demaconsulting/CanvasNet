### LegendLayout

![Layout Structure](VegaLiteLayoutView.svg)

The `LegendLayout` unit is the second software unit in the `Layout` subsystem. It is one source file,
`LegendLayout.cs`, in the `DemaConsulting.CanvasNet.VegaLite.Layout` namespace, and defines the `internal static` class
`LegendLayout` and the `LegendPlacement` record. The file carries the provenance header comment `Portions derived from
Vega (BSD-3-Clause)`: the placement is a port of the legend layout of Vega's view layout.

#### Purpose

`LegendLayout` decides where the built legends of a view go and how far they reach. The `LegendBuilder` of the `Guides`
subsystem builds each legend relative to its own origin and measures its box; this unit turns the boxes into positions
next to the data rectangle and the axes, so that the placement rule of Vega is defined once and can be tested with
synthetic boxes, without text or fonts. The unit's behavior is specified by the
`CanvasNetVegaLite-Layout-LegendLayout-*` requirements in `docs/reqstream/canvas-net-vega-lite/layout/legend-layout.yaml`.

#### Data Model

- **`LegendPlacement`** `(Groups, Bounds)` - the legend groups in the order of the legends that were given, positioned in
  the coordinates of the view, and the union of their bounds (empty when there are none).
- The input is the list of `LegendResult` records of the `LegendBuilder` (orient, requested offset, `legendX` and
  `legendY`, the size of the box, and the bounds, which include the border), the `ResolvedConfig` (for
  `config.legend.layout`), the data rectangle united with the left and right axes (`horizontal`) and with the top and bottom
  axes (`vertical`), and the size of the data rectangle.

#### Key Methods

- `AxisBounds(axes, width, height)` - the two union boxes that the legends are measured from: the data rectangle with the
  bounds of the left and right axes, and the data rectangle with the bounds of the top and bottom axes.
- `Place(legends, config, horizontal, vertical, width, height)` - groups the legends by orient (in the order of their first
  appearance), builds the grid options of each group, and positions the boxes with the grid layout of the `LegendBuilder`
  (alignment `each`, flush boxes unless `bounds` is `full`, the `margin` between boxes, the `center` option, rounding half
  up):
  - `right` and `left` legends form a column (the `direction` is `vertical` for these orients and `horizontal` for the
    others, from the default configuration); a `right` grid starts at the rounded-up right edge of `horizontal` plus
    the offset, a `left` grid ends at its rounded-down left edge minus the offset, `top` and `bottom` rows likewise
    above and below `vertical`. The offset is the largest `offset` that a legend asks for, else `layout.offset` (18).
  - The place along the side comes from the `anchor` (`start`, `middle`, `end`; the fraction 0, 0.5, 1) of the data
    rectangle, or of the axis bounds when the `frame` is `bounds`.
  - `top-left`, `top-right`, `bottom-left`, and `bottom-right` legends are anchored at the offset from the corners of the
    data rectangle, and a legend with the orient `none` stays at its own `legendX` and `legendY`.
  - The properties of `config.legend.layout` are found as Vega finds them: the object of the orient, then the layout
    object, then the default.
- `Combine(axes, legends, width, height)` - joins the extents of the axes with the bounds of the legends by the larger
  reach on each side.

#### Error Handling and Limits

The unit throws only `ArgumentNullException` for null arguments. It loops over the legends of a view (at most one per
legend channel) and never over data, so it needs no limit and no cancellation point; every scene item of a legend was
charged and checked by the `LegendBuilder` before the unit sees it, and `CreateGroup` creates only the group of a legend
already counted.

#### Interactions

The unit is called by `VegaLiteDocument` in each pass of the layout iteration, after the axes and the legends were built,
and its `Combine` result goes to `ViewLayout.Measure`. It consumes `AxisResult` and `LegendResult` (including the grid
layout) from the `Guides` subsystem and `ResolvedConfig` from the `SpecModel` subsystem. It is stateless and safe for
concurrent use.
