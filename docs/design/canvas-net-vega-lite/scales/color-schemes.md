### ColorSchemes

![Scales Structure](VegaLiteScalesView.svg)

The `ColorSchemes` unit is the fifth unit in the `Scales` subsystem. It is two source files in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: `ColorSchemes.cs`, which holds the `internal` types
`ColorScheme`, `SchemeRange`, the enumeration `SchemeUse`, and the static partial class `ColorSchemes` (lookup and
range configuration), and `ColorSchemeData.cs`, the other part of `ColorSchemes`, which holds the palette tables as
data only. Both files carry the provenance header comment for the palettes they contain.

#### Purpose

`ColorSchemes` holds the 68 color schemes of Vega and turns one of them into the range of a scale. A scheme is
either a categorical list (15 schemes, such as `tableau10`, the default category palette of Vega-Lite, `category20`,
and the ColorBrewer qualitative sets) or a continuous ramp (53 schemes, such as `blues`, `viridis`, `turbo`, and
`redYellowBlue`). It exists so that palette data is kept apart from the scale inference, with its provenance and its
license notices recorded, and so that the scale resolver can read the colors of a named scheme without knowing how
they are stored.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ColorSchemes-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/color-schemes.yaml`.

#### Data Model

The unit has no mutable state. `ColorSchemeData.cs` holds one constant string per scheme: a run of six-digit
hexadecimal colors (`rrggbb`, no separator) in category order for a categorical scheme, and in stop order for a
continuous ramp (a ramp has between 7 and 64 stops). The catalog lists the 68 schemes with their Vega names (for
example `blueGreen`; the lookup lower-cases the name, as Vega does) and says whether each is continuous. A
`ColorScheme` decodes its run on first use (a thread-safe lazy value) into `RgbColor` values, `#rrggbb` CSS texts,
and a `ColorRamp` that interpolates piecewise through the colors in sRGB (see _ColorInterpolator_). A scheme built
from user colors with `ColorScheme.FromColors` is continuous, carries the interpolation space and gamma that the
caller chose, and is complete at creation. The catalog and its lookup table are built once, under a thread-safe lazy
initializer, and never change.

`SchemeUse` names the kind of scale a range is for: `Continuous`, `Ordinal`, `Quantile`, `Quantize`, `Threshold`, and
`BinOrdinal`. A `SchemeRange` is the result of a configuration: an interpolator over 0 to 1 for a continuous scale, or
a list of colors for a discrete one.

**Provenance and licenses.** The values are facts of published palettes, transcribed from the palette tables of Vega
(`vega-scale`) and, for the ColorBrewer qualitative sets, `category10` and `observable10`, from the categorical files
of d3-scale-chromatic; no code was copied. The origin and the license of every group of schemes are recorded in
`src/DemaConsulting.CanvasNet.VegaLite/third-party/README.md`: the ColorBrewer colors are Apache-2.0
(`LICENSE-colorbrewer.txt` with the Apache License in `LICENSE-apache-2.0.txt`), Turbo is Apache-2.0
(`LICENSE-turbo.txt`), cividis carries the permissive Battelle Memorial Institute license of its authors'
repository (`LICENSE-cividis.txt`), viridis, magma, inferno, and plasma are CC0 (credited in the record), and the d3
and Vega palettes are covered by `LICENSE-d3.txt` and `LICENSE-vega.txt`. All notice files are packed under
`licenses/` in both NuGet packages by the existing `third-party\LICENSE-*.txt` glob of the two project files. A test
(`ColorSchemes_Notices_ShipWithTheTablesAndNameEveryLicense`) checks that the notices exist and that the record names
every scheme and every notice file.

#### Key Methods

- `TryGetColorScheme(string? name, out ColorScheme scheme)` - looks a scheme up by name, ignoring case, as Vega does;
  an unknown, empty, or null name returns false. The caller decides how to fail (Vega raises an error for an
  unrecognized scheme name).
- `TryGetScheme(string? name, out IReadOnlyList<string> colors)` - the categorical lookup used by the
  `ScaleResolver` for ordinal color ranges: true only for a categorical scheme, with its `#rrggbb` colors; a continuous
  scheme such as `blues` and an unknown name give false, and the resolver then falls back to `tableau10` and records the
  fallback.
- `Configure(ColorScheme scheme, SchemeUse use, int? schemeCount, int domainSize, (double Start, double End)? extent,
  bool reverse, int maxColors, CancellationToken)` - the port of the scheme handling of the Vega `scale` transform.
  For `Continuous` it returns an interpolator over the extent (a sub-range of 0 to 1; a start above the end reverses),
  with the ends swapped when `reverse` is set. For the other uses it returns a list. The number of colors is the
  explicit count or 5 for `Quantile` and `Quantize`, one more than the thresholds for `Threshold`, one less than the
  bin edges for `BinOrdinal`, and for `Ordinal` the explicit count, else the domain size, else 5. A continuous scheme
  is sampled with `ColorInterpolator.Quantize` over the extent (so the two end colors are not used); a categorical
  scheme is used whole for an ordinal scale and cut to the count by the other uses, ignoring the extent; `reverse`
  reverses the resulting list.
- `CategoryColor`, `Tableau10`, and `Tableau10Color` - the category color at `index mod count`, so that categories
  beyond the length of the scheme cycle deterministically, and the `tableau10` list and its color.

**Preconditions and postconditions**: a scheme is not null; results depend only on the arguments and are
deterministic; `Configure` returns exactly the number of colors described above, or fewer when a categorical scheme
is shorter than the count.

#### Error Handling

A null scheme is `ArgumentNullException`, an empty list for `CategoryColor` is `ArgumentException`, and a negative
index is `ArgumentOutOfRangeException`; these are programming errors and are not converted. An unknown scheme name is
not an error here: the lookups return false. **Limits**: a discrete range that would have more colors than the
`maxColors` argument is a `VegaLiteRenderException` whose message names the count, the limit, and
`VegaLiteLimits.MaxScaleDomainSize` (the tool maps it to a budget error); the caller adds the location of the
scheme in the specification. A categorical scheme used whole for an ordinal scale has at most 20 colors and is
exempt. The count of a threshold scale at `int.MaxValue` does not overflow. A long list polls the cancellation
token every 4,096 colors (`OperationCanceledException`). A corrupt table (not a run of six-digit colors) is an
`InvalidOperationException`, which the tests exclude by decoding every scheme. The unit performs no I/O.

#### Dependencies

`ColorInterpolator` (`ColorRamp`, `RgbColor`, `InterpolationSpace`, `Quantize`, and `Range`) of the same subsystem;
the public `VegaLiteRenderException`; and the .NET base class library (`System`, `System.Collections.Generic`,
`System.Threading`).

#### Callers

`ScaleResolver` calls `TryGetScheme` to build the color range of an ordinal color scale; the later phases of the
`ScaleResolver` call `TryGetColorScheme` and `Configure` for the color ranges of continuous, discretizing, and ordinal
scales, and `LegendBuilder` reads the colors of the resulting scale. `FeatureCatalog` calls `TryGetScheme` to decide
whether a `scheme` is supported. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ColorSchemesTests.cs` call the unit directly through
`InternalsVisibleTo`.
