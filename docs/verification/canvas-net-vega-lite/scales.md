## Scales Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Scales` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Scales`), containing five units: `ScaleModel` (linear, band, point, and ordinal
mappings), `ScaleResolver` (the inference of scales from a specification and its data), `Ticks` (the tick, tick step,
and nice algorithms), `NumberFormat` (the d3-format subset that labels axes), and `ColorSchemes` (the `tableau10`
category colors).

### Verification Approach

The `Scales` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `scales/`)
verify each unit in isolation. The subsystem-level scenarios verify the units working together in the ways later stages
use them: the resolver reads a parsed specification, its loaded table, and the resolved configuration, and produces
`ResolvedScale` objects whose `CreateModel` builds a `ScaleModel` for the data rectangle (`ScaleResolverTests` runs this
whole chain with the real `SpecParser`, `ConfigResolver`, `DataLoader`, and `ScaleModel`, and asserts the mapped
positions, the domains, and the ranges); and the axis stage derives its tick values and labels from a scale model's
domain with `Ticks` and `NumberFormat` (`AxisBuilderTests` runs this chain and asserts the tick values and label texts
of the whole axis against values derived by hand). The colors of an ordinal color scale come from `ColorSchemes`
through the resolver.

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
- **Colors Come From The Default Palette, With A Documented Fallback**:
  `ScaleResolverTests.ScaleResolver_Resolve_ColorsUseTableau10AndFallBackForOtherSchemes`,
  `ColorSchemesTests.ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors` — the ordinal color scale of a nominal
  field uses the ten `tableau10` colors in domain order, an unknown scheme or an ordinal field falls back to them with
  the fallback flag set, and an explicit range or configured category range wins.
- **Tick Limits Fail Closed Through The Axis**:
  `TicksTests.Ticks_Ticks_ResultOverTheLimitFailsClosed`,
  `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations` — a tick result larger than the limit
  throws before it is allocated and the axis reports it with the location of the axis in the specification.
- **Formats Are Culture-Independent**:
  `NumberFormatTests.NumberFormat_Format_IgnoresTheCurrentCulture` — the label text is identical under a culture with
  a decimal comma.
