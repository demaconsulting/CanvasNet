## Scales

![Scales Structure](VegaLiteScalesView.svg)

### Overview

The `Scales` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Scales`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Scales/`) is the numeric heart of the renderer: it infers a scale for each
encoding channel from the specification and the data, maps data values to pixels, colors, sizes, and opacities, chooses
axis ticks, and formats tick labels. Most of the ported d3 and Vega logic lives here. It is **internal**: its types are
visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains
five units:

- **ScaleModel** - the internal `ScaleModel` and `ScaleKind`: the immutable linear, band, point, and ordinal mappings;
  see _ScaleModel Unit Design_ (`scales/scale-model.md`)
- **ScaleResolver** - the internal static partial `ScaleResolver` (three files) with `ResolvedScales`, `ResolvedScale`,
  and
  `RangeEnd`: the Vega-Lite inference of scale types, domains (including the binned, stacked, ranged, and unaggregated
  ones), the order of discrete domains, ranges, and paddings; see _ScaleResolver Unit Design_
  (`scales/scale-resolver.md`)
- **Ticks** - the internal static `Ticks`: the d3-array tick algorithms, the linear `nice`, and the Vega tick-count
  rules; see _Ticks Unit Design_ (`scales/ticks.md`)
- **NumberFormat** - the internal `NumberFormat` and `FormatSpecifier`: the d3-format number formatter and the
  `tickFormat` precision rule; see _NumberFormat Unit Design_ (`scales/number-format.md`)
- **ColorSchemes** - the internal static `ColorSchemes`: the `tableau10` category palette; see _ColorSchemes Unit
  Design_ (`scales/color-schemes.md`)

The subsystem's namespace documentation is in `Scales/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Scales-*` requirements in `docs/reqstream/canvas-net-vega-lite/scales.yaml`.

### Interfaces

**Exposed** (internal only): `ScaleResolver.Resolve(SpecNode, EncodingPlan, EncodingData, ResolvedConfig, string?,
VegaLiteLimits?, CancellationToken)` (the pipeline entry), the convenience overload `ScaleResolver.Resolve(SpecNode,
DataTable, ResolvedConfig, string?, VegaLiteLimits?, CancellationToken)` (which plans and runs the implicit pipeline
itself), and `ScaleResolver.InferOrient`; `ResolvedScale.CreateModel(extent)`; the `ScaleModel` factories
(`CreateLinear`, `CreateBand`, `CreatePoint`, `CreateOrdinal`), mappings (`Map`, `MapCenter`, `MapOrdinal`, `Invert`,
`IndexOf`), and `BandSpace`; `Ticks.GetTicks`, `TickStep`, `TickIncrement`, `Nice`, `DefaultAxisTickCount`,
`DefaultAxisTickMinStep`, `ResolveTickCount`, and `AxisTicks`; `NumberFormat.Create`, `CreatePrefix`, `TickFormat`,
`ParseSpecifier`, and the precision helpers; and `ColorSchemes.Tableau10`, `TryGetScheme`, and `CategoryColor`.

**Consumed**: `SpecNode` and `ResolvedConfig` from the `SpecModel` subsystem; `DataTable` and `DataValue` from the
`Data` subsystem; `EncodingPlan`, `EncodingData`, `MeasureCell`, `WorkMeter`, `BinSpec`, and `AggregateMeasures` from the
`Transforms` subsystem (the plan names the field every channel reads, and the accumulators order a discrete domain by an
aggregate); and the public `VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteSpecException`, and
`VegaLiteRenderException` types of the `VegaLiteDocument` unit. The .NET base class library supplies
`System.Numerics`, `System.Text.RegularExpressions`, and `System.Globalization`.

**Consumers**: `VegaLiteDocument` (`ScaleResolver.Resolve`), the `Transforms` subsystem (`EncodingPlan` uses
`ScaleResolver.InferOrient`), the `SpecModel` subsystem (`FeatureCatalog` uses `InferOrient`), the `Guides` subsystem
(`AxisBuilder` uses `ScaleModel`, `Ticks`, and `NumberFormat`, and reads the `ResolvedScale`), and the `Marks` subsystem
(`MarkBuilder` uses the scales to position and paint marks).

### Design

**Inference is separate from arithmetic.** `ScaleResolver` decides what a scale is (type, domain, range, padding, and
whether it is reversed or rounded) from the encoding, the data, the mark, and the configuration, and returns a
`ResolvedScale` description that is independent of pixel extents; `ResolvedScale.CreateModel(extent)` then builds the
`ScaleModel` for a concrete extent (the data-rectangle width or height). This separation is what lets the layout, which
needs the discrete step to size the view, run before the extent exists, and lets the axes iterate against a changing
data rectangle. `ScaleModel` itself is pure arithmetic with no knowledge of the specification.

**Vega-Lite's rules, not an approximation.** The scale type is chosen per field type, channel, and mark (position
channels of discrete fields are band scales for `rect`, `bar`, `image`, `rule`, and `tick` marks and point scales for the
others); zero, nice, padding, and the default ranges follow the Vega-Lite 6 rules and configuration; Vega's order of
applying zero, pixel padding, and nice is preserved. Anything outside the supported subset is already reported by the
`FeatureCatalog`; the resolver ignores or falls back deliberately (an ordinal color field uses `tableau10`, a quantitative
color has no scale) and the deviations are listed in _CanvasNetVegaLite System Design_.

**Ported algorithms are exact and tested by hand.** `Ticks` and `NumberFormat` are ports of the d3-array and d3-format
algorithms and `ScaleModel` of the d3-scale arithmetic, all carrying attribution headers; test vectors are derived by
hand, not copied from any upstream test suite. The number formatter reproduces JavaScript's `toFixed`, `toExponential`,
and `toPrecision` digits from the exact decimal expansion of the double (using `BigInteger`), so labels do not depend on
the platform's floating-point formatting.

**Determinism and limits.** Every unit is culture-invariant and free of shared mutable state; the models are immutable
and thread-safe. A discrete domain larger than `MaxScaleDomainSize` and an axis with more than `MaxTicks` ticks fail
closed with a located `VegaLiteRenderException` before memory is spent, and the resolver polls the cancellation token.
