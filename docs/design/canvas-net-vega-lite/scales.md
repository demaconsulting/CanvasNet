## Scales

![Scales Structure](VegaLiteScalesView.svg)

### Overview

The `Scales` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Scales`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Scales/`) is the numeric heart of the renderer: it infers a scale for each
encoding channel from the specification and the data, maps data values to pixels, colors, sizes, and opacities, chooses
axis ticks, and formats tick labels. Most of the ported d3 and Vega logic lives here. It is **internal**: its types are
visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to the test assembly. It contains
six units:

- **ScaleModel** - the internal `ScaleModel`, `ScaleKind`, and `ScaleRange`: the immutable linear, time, UTC, band,
  point, ordinal, sequential, diverging, quantize, quantile, threshold, and bin-ordinal mappings;
  see _ScaleModel Unit Design_ (`scales/scale-model.md`)
- **ScaleResolver** - the internal static partial `ScaleResolver` (six files) with `ResolvedScales`, `ResolvedScale`,
  and
  `RangeEnd`: the Vega-Lite inference of scale types, domains (including the binned, stacked, ranged, unaggregated, and
  temporal ones), the order of discrete domains (also over the dates of a time unit), ranges, paddings, the colors and
  schemes of color scales, and the ranges of the non-position channels; see _ScaleResolver Unit Design_
  (`scales/scale-resolver.md`)
- **Ticks** - the internal static partial `Ticks` (two files): the d3-array tick algorithms, the linear `nice`, the Vega
  tick-count rules, and the d3-time calendar ticks and `nice` of time and UTC scales; see _Ticks Unit Design_
  (`scales/ticks.md`)
- **NumberFormat** - the internal `NumberFormat` and `FormatSpecifier`: the d3-format number formatter and the
  `tickFormat` precision rule; see _NumberFormat Unit Design_ (`scales/number-format.md`)
- **ColorSchemes** - the internal static partial `ColorSchemes` (two files) with `ColorScheme`, `SchemeRange`, and
  `SchemeUse`: the 68 Vega color schemes as data, the lookup by name, and the configuration of a color range; see
  _ColorSchemes Unit Design_ (`scales/color-schemes.md`)
- **ColorInterpolator** - the internal static `ColorInterpolator` with `RgbColor`, `InterpolationSpace`, and
  `ColorRamp`: the sRGB, HSL, CIELAB, and HCL color interpolation; see _ColorInterpolator Unit Design_
  (`scales/color-interpolator.md`)

The subsystem's namespace documentation is in `Scales/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Scales-*` requirements in `docs/reqstream/canvas-net-vega-lite/scales.yaml`.

### Interfaces

**Exposed** (internal only): `ScaleResolver.Resolve(SpecNode, EncodingPlan, EncodingData, ResolvedConfig, string?,
VegaLiteLimits?, CancellationToken)` (the pipeline entry), the convenience overload `ScaleResolver.Resolve(SpecNode,
DataTable, ResolvedConfig, string?, VegaLiteLimits?, CancellationToken)` (which plans and runs the implicit pipeline
itself), `ScaleResolver.InferOrient`, `ScaleResolver.AcceptsScaleType`, and `ScaleResolver.ChannelIsDrawn`;
`ResolvedScale.CreateModel(extent)`; the `ScaleModel` factories (`CreateLinear`, `CreateTime`, `CreateBand`,
`CreatePoint`,
`CreateOrdinal`, `CreateSequential`, `CreateQuantize`, `CreateQuantile`, `CreateThreshold`, `CreateBinOrdinal`) with the
`ScaleTransform` of log, pow, sqrt, and symlog scales,
mappings (`Map`, `MapCenter`, `MapOrdinal`, `MapText`, `MapColor`, `MapIndex`, `Invert`, `IndexOf`, `Fraction`), and `BandSpace`;
`Ticks.GetTicks`, `TickStep`, `TickIncrement`, `Nice`, `DefaultAxisTickCount`, `DefaultAxisTickMinStep`,
`ResolveTickCount`, `AxisTicks`, `ModelTicks`, `LogTicks`, `LogNice`, and `LogLabelTest`, and the time versions
`TimeTickRequest`, `TimeTickInterval`, `ResolveTimeInterval`, `TimeTicks` (two overloads), and `TimeNice`;
`NumberFormat.Create`, `CreatePrefix`,
`TickFormat`, `FormatFloat`, `ParseSpecifier`, and the
precision helpers; `ColorSchemes.TryGetColorScheme`, `TryGetScheme`, `Configure`, `Tableau10`, and `CategoryColor`; and
`ColorInterpolator.Interpolate`, `Range`, and `Quantize` with `ColorRamp`.

**Consumed**: `SpecNode` and `ResolvedConfig` from the `SpecModel` subsystem; `DataTable` and `DataValue` from the
`Data` subsystem, together with its calendar units (`TimeCalendar`, `TimeInterval`, `TimeUnitSpec`, `DateTimeObject`,
and `JsDateParser`: the zone, the calendar intervals of ticks and `nice`, the time unit of a field, and the reading of
dates in a specification; see _Time Calendar Design_ and _Time Format Design_); `EncodingPlan`, `EncodingData`,
`MeasureCell`, `WorkMeter`, `BinSpec`, and `AggregateMeasures` from the
`Transforms` subsystem (the plan names the field every channel reads and the time unit it carries, and the accumulators
order a discrete domain by an aggregate); and the public `VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteSpecException`,
and
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
`FeatureCatalog`; the resolver ignores or falls back deliberately (a property that the scale type or channel does not
support is ignored; a color domain of four or more entries is a specification error) and the deviations are listed in
_CanvasNetVegaLite System Design_.

**Time scales are linear scales with a calendar.** A temporal field (and any field with a time unit) has values that
are instants, milliseconds since the Unix epoch. `ScaleKind.Time` and `ScaleKind.Utc` are kinds of their own, not a flag
on `Linear`, so that every site that means "linear" had to be revisited deliberately (the audit is recorded in
_ScaleResolver Unit Design_); they reuse the piecewise linear arithmetic of `ScaleModel` unchanged and carry the
`TimeCalendar` that `Ticks` needs for the d3-time interval table, the tick boundaries, and `nice`. The calendar is the
zone of `VegaLiteRenderOptions.TimeZone` (UTC when none is given), or UTC for a `utc` scale or a `utc` time unit; it is
read from the evaluation facts of the render, so the `ScaleResolver.Resolve` signature did not change. No unit of the
subsystem reads the host zone, the clock, or the culture.

**Ported algorithms are exact and tested by hand.** `Ticks` and `NumberFormat` are ports of the d3-array, d3-time, and
d3-format algorithms and `ScaleModel` of the d3-scale arithmetic, all carrying attribution headers; test vectors are
derived by hand, not copied from any upstream test suite. The number formatter reproduces JavaScript's `toFixed`, `toExponential`,
and `toPrecision` digits from the exact decimal expansion of the double (using `BigInteger`), so labels do not depend on
the platform's floating-point formatting.

**Determinism and limits.** Every unit is culture-invariant and free of shared mutable state; the models are immutable
and thread-safe. A discrete domain larger than `MaxScaleDomainSize` and an axis (numeric or time) with more than
`MaxTicks` ticks fail closed with a located `VegaLiteRenderException` before memory is spent (a time tick range stops at
the first tick beyond the limit), and the resolver and the tick generators poll the cancellation token.
