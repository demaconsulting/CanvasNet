## Guides Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Guides` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Guides`), containing three units: `TextMeasurer` (font resolution, text
measurement, truncation, line splitting, and Vega text bounds), `TitleBuilder` (the title and subtitle scene items and
their extent), and `AxisBuilder` (the axis grid, ticks, labels, domain, and title, and the extent they need).

### Verification Approach

The `Guides` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `guides/`)
verify each unit in isolation. The subsystem-level scenarios verify the units working together and with the units they
are fed by: both `TitleBuilder` and `AxisBuilder` measure all their text through one real `TextMeasurer` (the bundled
Liberation fonts), so that every extent a test derives by hand from a measured text width, a font size, and the Vega
text constants is checked through the whole chain. `AxisBuilderTests` additionally runs the real `SpecParser`,
`ConfigResolver`, `DataLoader`, `ScaleResolver`, `Ticks`, and `NumberFormat` (through the shared helper
`ChartTestHelpers`) to produce the scale and the axis request, and asserts the scene items and the extent of the whole
axis. `VegaLiteDocumentLayoutTests` and the chart integration tests verify the guides as the layout and the painter see
them: the title is painted inside the band that the layout reserves for it.

No mocking or stubbing is used: font measurement is real, which is what makes the layout independent of installed
fonts (the unit tests assert that generic and Arial-like family names resolve to the very same bundled font object
whether or not system fonts are allowed). The subsystem performs no network or data-file input; font files are read
only from the bundled resources and, in the one scenario that exercises `AdditionalDirectories`, from a temporary
directory created and deleted by the test. The subsystem is internal, so its tests reach it through the test
assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The Liberation fonts bundled with the core package; the single test that needs an installed non-generic
  font skips itself (`Assert.Skip`) on a machine without one, and every other test is independent of installed fonts
- **Files**: A per-test temporary folder holding a copy of a bundled font file, deleted by the test
- **Isolation**: Each test builds its own specification, measurer, and builders

### Acceptance Criteria

The `Guides` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario in
the three unit verification documents passes without error or unexpected exception. A skipped scenario that is
documented above as machine-dependent is not a failure.

### Test Scenarios

- **Titles And Axes Measure Through The Same Font Resolution**:
  `TitleBuilderTests.TitleBuilder_Build_OrientAnchorAndSubtitleProduceExpectedExtent`,
  `AxisBuilderTests.AxisBuilder_Build_TitleAndExtentMatchAnalyticLayout` — the title extent equals the measured width
  of the title text at 13 pixels bold and its height equals the font size; the axis extent equals the tick length, the
  label padding, the label height, the title padding, and the 11 pixel title, with every text box measured by the same
  real measurer.
- **Axis Of A Parsed Specification**:
  `AxisBuilderTests.AxisBuilder_Build_LinearAxisUsesExpectedTickValuesAndLabels`,
  `AxisBuilderTests.AxisBuilder_Build_BandAxisUsesExpectedCategoriesAndGrid` — a parsed specification's scale produces
  an axis whose tick values, label texts, and positions equal the values derived by hand from the tick algorithm and
  the scale arithmetic.
- **Axis Titles And Ticks Follow The Aggregating Encoding**:
  `AxisBuilderTests.AxisBuilder_Build_TitlesFollowThePlanAndACountHasAGrid`,
  `AxisBuilderTests.AxisBuilder_Build_BinnedAxisAsksForATickPerTenPixels`,
  `AxisBuilderTests.AxisBuilder_Build_NormalizedStackUsesTheNormalizedNumberFormat`,
  `VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_PreBinned_RangesAreBarsOverTheirRanges` — the
  axis title of an aggregated, binned, or counted channel is the plan's title (`Sum of v`, `v (binned)`, `Count of
  Records`) unless the specification gives one, a binned axis has no grid and asks for a tick per ten pixels, a
  normalized stack is labeled in percent, and the merged x title of pre-binned ranges names both fields.
- **Guide Configuration Precedence**:
  `AxisBuilderTests.AxisBuilder_Build_ConfigurationPrecedenceFollowsVegaLite`,
  `TitleBuilderTests.TitleBuilder_Build_PropertiesLinesConfigurationAndLimit` — the configuration, styles, and the
  explicit properties of the specification combine in Vega-Lite's order for both guide kinds.
- **Guides Fail Closed With A Location**:
  `AxisBuilderTests.AxisBuilder_Build_InvalidSettingsAndLimitsFailWithLocations` — an invalid orient or format is a
  specification error and too many ticks a render error, each pointing into the specification, and cancellation is
  honored.
- **Guides Are Painted Where The Layout Reserves Room**:
  `VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_TitleIsPaintedAboveThePlotAndCenteredOnIt`,
  `CanvasNetVegaLite_SystemIntegration_Render_InlineBarHasExpectedSamplePixels` — the title and the axis texts leave ink
  in the bands that the title and axis extents reserve, and the padding is untouched.
- **Fonts Do Not Depend On The Machine**:
  `TextMeasurerTests.TextMeasurer_Resolve_DefaultUsesBundledFonts`,
  `CanvasNetVegaLite_SystemIntegration_Fonts_DefaultUsesBundledLiberation` — layout and pixels of text are identical
  with system fonts allowed or not, for every generic and Arial-like family name.
