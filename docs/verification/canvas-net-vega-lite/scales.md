## Scales Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scales` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Scales`), containing six units: `ScaleModel` (the linear, band, point, ordinal,
sequential, diverging, quantize, quantile, threshold, and bin-ordinal mappings), `ScaleResolver` (the inference of
scales from a specification and its data), `Ticks` (the tick, tick step, and nice algorithms), `NumberFormat` (the
d3-format subset that labels axes), `ColorSchemes` (the 68 Vega color schemes and the configuration of a color range),
and `ColorInterpolator` (the color interpolation).

### Verification Approach

The `Scales` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `scales/`)
verify each unit in isolation. The subsystem-level scenarios verify the units working together in the ways later stages
use them: the resolver reads a parsed specification, its loaded table (and, for the aggregating features, the encoding
plan and the Raw and Main tables of the implicit stage), and the resolved configuration, and produces
`ResolvedScale` objects whose `CreateModel` builds a `ScaleModel` for the data rectangle (`ScaleResolverTests` runs this
whole chain with the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleModel`, and asserts the mapped
positions, the domains, and the ranges); and the axis stage derives its tick values and labels from a scale model's
domain with `Ticks` and `NumberFormat` (`AxisBuilderTests` runs this chain and asserts the tick values and label texts
of the whole axis against values derived by hand). The colors of a color scale come from `ColorSchemes`
through the resolver (`ScaleResolverColorTests` resolves color specifications with the real units and compares the
colors with the samples of the scheme ramps at hand-derived positions); the colors of a scheme are interpolated by
`ColorInterpolator` (`ColorSchemesTests` runs this chain with the real unit and compares the colors with hand-derived
values).

The algorithmic units (`Ticks`, `NumberFormat`, `ScaleModel`) are verified with vector tables whose expected values are
derived by hand from the published algorithms and from arithmetic, never by running d3 or Vega and pasting the output:
each test states its derivation in comments next to the vectors, and each unit verification design summarizes the
derivation. The subsystem uses no mocking or stubbing, performs no file or network input, and does not depend on the
current culture (a test runs the formats under a `de-DE` culture). The subsystem is internal, so its tests reach it
through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None; every specification is an in-memory string
- **Isolation**: Each test builds its own specification, table, and scales; the culture scenario restores the current
  culture in a `finally` block

### Acceptance Criteria

The `Scales` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the five unit verification documents passes without error or unexpected exception.

### Test Scenarios

- **Specification To Scale Model: Positions Follow The Resolved Domain And Range**:
  `ScaleResolverTests.ScaleResolver_Resolve_AppliesZeroNiceDomainRangeAndPadding`,
  `ScaleResolverTests.ScaleResolver_Resolve_DiscreteSizesComeFromTheStep` — resolves scales for bar and scatter
  specifications from inline data and asserts the domains (zero and nice applied), the band steps and bandwidths of
  the created models, and the mapped positions in a data rectangle, using the model arithmetic of the _ScaleModel Unit
  Verification Design_.
- **Axis Ticks And Labels Come From The Scale Domain**:
  `AxisBuilderTests.AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels`,
  `AxisBuilderTests.AxisBuilder_Build_ValuesCountFormatAndFlushAreHonored` — the tick values come from `Ticks` for the
  domain and the Vega-Lite tick count (a 300 pixel axis over 0 to 100 gives ticks 0, 10, ..., 100) and the labels
  from `NumberFormat` (the default `,f`, a `$,.2f` format, and the Unicode minus), asserted against values derived by
  hand from the tick algorithm.
- **Discrete Domains Are Ordered And Bounded**:
  `ScaleResolverTests.ScaleResolver_Resolve_DiscreteDomainsFollowTheSortProperty`,
  `ScaleResolverTests.ScaleResolver_Resolve_InvalidInputsFailWithLocation` — categories are sorted ascending by
  default or follow the data order, a descending sort, or an explicit array; a domain over `MaxScaleDomainSize` and
  invalid scale properties fail with a located specification or render exception.
- **Planned Fields, Sorts, And Domains Follow The Implicit Pipeline**:
  `ScaleResolverPlanTests.ScaleResolver_Resolve_SortsByAnotherChannelsAggregate`,
  `ScaleResolverPlanTests.ScaleResolver_Resolve_PreBinnedDomainIsTheUnionOfStartAndEnd`,
  `ScaleResolverPlanTests.ScaleResolver_Resolve_UnaggregatedDomainReadsTheRawRows`,
  `AxisBuilderTests.AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels` — the resolver reads the fields, bins, and
  stacks that the plan derived (the Raw and the Main table) and the axis stage takes its ticks from the resulting
  domain: categories are sorted by the sum, count, or other aggregate of another channel over the Raw rows, a
  pre-binned field has the union of its start and end as its domain with no zero and no nice, an `unaggregated` domain
  reads the rows before the aggregate (1 to 12 rather than the means 2 to 8), and a binned axis asks for one tick per ten
  pixels (the ticks 0, 2, ..., 34 for the bins 0 to 35).
- **Colors Come From The Configured Ranges**:
  `ScaleResolverTests.ScaleResolver_Resolve_OrdinalColorsUseTheConfiguredRanges`,
  `ColorSchemesTests.ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors` — the ordinal color scale of a nominal
  field uses the ten `tableau10` colors in domain order, an ordinal field the blues ramp sampled for its categories, and
  an explicit range, a scheme, or a configured named range wins in that order; a quantitative color is a sequential or
  diverging scale, and the quantize, quantile, and threshold scales classify (`ScaleResolverColorTests`).
- **Tick Limits Fail Closed Through The Axis**:
  `TicksTests.Ticks_Ticks_ResultOverTheLimitFailsClosed`,
  `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations` — a tick result larger than the limit
  throws before it is allocated and the axis reports it with the location of the axis in the specification.
- **Formats Are Culture-Independent**:
  `NumberFormatTests.NumberFormat_Format_IgnoresTheCurrentCulture` — the label text is identical under a culture with
  a decimal comma.
