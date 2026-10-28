### ColorSchemes

![Scales Structure](VegaLiteScalesView.svg)

The `ColorSchemes` unit is the fifth unit in the `Scales` subsystem. It is one source file, `ColorSchemes.cs`, in
the `DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines one `internal` type, the static class
`ColorSchemes`. The file carries the provenance header comment for the palette it contains.

#### Purpose

`ColorSchemes` holds the category colors of the Vega-Lite default configuration and looks up a scheme by name. In the
first delivery the only scheme is `tableau10`, the default categorical palette of Vega-Lite, so that charts with a nominal
color field are drawn with distinct, Vega-Lite-compatible colors. It exists so that palette data is kept apart from the
scale inference, with its provenance recorded, and so that further schemes (and their license notices) can be added in a
later phase without touching the resolver.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-ColorSchemes-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/color-schemes.yaml`.

#### Data Model

The unit has no mutable state. It holds the constant name `Tableau10Name` (`"tableau10"`) and the ten colors of the
scheme as CSS hex strings in category order: `#4c78a8`, `#f58518`, `#e45756`, `#72b7b2`, `#54a24b`, `#eeca3b`,
`#b279a2`, `#ff9da6`, `#9d755d`, and `#bab0ac`. `Tableau10` exposes them as a read-only list. No ColorBrewer scheme is
used, so no ColorBrewer notice applies (see _Third-Party Provenance And Attribution_ in _CanvasNetVegaLite System
Design_).

#### Key Methods

- `TryGetScheme(string? name, out IReadOnlyList<string> colors)` - true only for `tableau10`, compared ignoring case, as
  Vega does; any other name returns false with an empty list, and the `ScaleResolver` then falls back to `tableau10` and
  records the fallback.
- `CategoryColor(IReadOnlyList<string> scheme, int index)` - the color at `index mod count`, so that categories beyond the
  length of the scheme cycle through it deterministically.
- `Tableau10Color(int index)` - `CategoryColor` over `tableau10`.

**Preconditions and postconditions**: a scheme is non-null and not empty and an index is not negative; results are
deterministic.

#### Error Handling

A null scheme is `ArgumentNullException`, an empty scheme is `ArgumentException`, and a negative index is
`ArgumentOutOfRangeException`; these are programming errors and are not converted. An unknown scheme name is
not an error: `TryGetScheme` returns false. The unit performs no I/O.

#### Dependencies

N/A - the unit uses only the .NET base class library (`System`, `System.Collections.Generic`) and depends on no other unit.

#### Callers

`ScaleResolver` calls `TryGetScheme` to build the color range of an ordinal color scale. `MarkBuilder` obtains the
category colors from the resulting ordinal `ScaleModel`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/ColorSchemesTests.cs` call the unit directly through
`InternalsVisibleTo`.
