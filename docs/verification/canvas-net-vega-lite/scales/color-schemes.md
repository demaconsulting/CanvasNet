### ColorSchemes Unit Verification Design

This document describes the unit-level verification strategy for the `ColorSchemes` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static partial `ColorSchemes` class (the catalog of
68 schemes, `TryGetColorScheme`, `TryGetScheme`, `Configure`, `CategoryColor`, `Tableau10`, and `Tableau10Color`) and the
types `ColorScheme`, `SchemeRange`, and `SchemeUse`. `tableau10` is the default category palette of Vega-Lite, and
category `i` receives color `i` modulo the number of colors.

#### Verification Approach

The unit is verified against the **published palette tables**, not against its own source. The theory rows in
`ColorSchemesTests` (name, kind, number of colors, first color, last color) were produced from a fresh download of
Vega's `palettes.js` and of the categorical files of d3-scale-chromatic, so a transcription error in the table of the
unit shows up as a failing row. The same download was compared color by color with the data file when it was written
(all 68 schemes matched). Interior stops are spot-checked for every family: the ColorBrewer qualitative sets (the whole
of Set1, whose nine colors are the well-known red, blue, green, purple, orange, yellow, brown, pink, and grey, and an
interior color of accent, dark2, paired, pastel1, pastel2, set2, and set3), the d3 and Vega categorical sets, the
single-hue, multi-hue, and diverging ramps (the middle stop of blues, oranges, redYellowGreen, and blueOrange, a stop of
spectral), the matplotlib colormaps at the middle of their 31 stops (viridis, magma, inferno, plasma), and the middle
of cividis and Turbo.

The interpolation and the range configuration are verified with values derived by hand. The sixth of the eleven stops of
`blues` is `#5ba3cf`, so position 0.5 is exactly that color; the middle of the first segment (position 0.05) is the mean
of `#cfe1f2` (207, 225, 242) and `#bed8ec` (190, 216, 236), that is 198.5, 220.5, and 239, which round half away from zero
to `#c7ddef`. A discrete range of three colors from `blues` is taken at 0.25, 0.5, and 0.75 (stop positions 2.5, 5, and
7.5 of ten): between `#a8cee5` and `#8fc1de` (155.5, 199.5, 225.5), on `#5ba3cf`, and between `#3181bd` and `#206fb2`
(40.5, 120, 183.5), giving `#9cc8e2`, `#5ba3cf`, `#2978b8`. The counts follow the Vega rules: threshold with three
thresholds gives four colors, bin-ordinal with five edges four, quantile and quantize five unless a count is given,
and ordinal the count, else the domain size, else five. The unit has no outputs of another implementation to compare
with, and no dependency needs mocking: the sibling unit `ColorInterpolator` is exercised for real, and the internal
types are reached through the test assembly's `InternalsVisibleTo` access. The use of the unit by the scale resolver,
with the documented fallback for other schemes, is verified in the _ScaleResolver Unit Verification Design_.

The notice test copies the files of the `third-party` folder next to the test assembly (the test project includes them
as linked files) and checks, as strict UTF-8, that each notice carries the text of its license and that the provenance
record names every scheme of the catalog and every notice file. That the project files pack the notices into both NuGet
packages is verified by listing the packages (see the system verification).

Unit tests reside in `ColorSchemesTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scales` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of names and numbers; the notice test reads the copied files of the test output
  folder; no network, no clock, and no host fonts

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any palette entry, color, count, lookup result, notice text, or exception type that differs from the
expectation constitutes a failure.

#### Unit-Level Test Scenarios

##### Tableau10 Holds The Documented Palette And Cycles

**Tests**: `ColorSchemesTests.ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors`

Asserts that `Tableau10` equals the ten documented colors in order, that its name is `tableau10`, that each index below
ten returns the color at that position, that larger indexes cycle (10, 24, and 107 give the colors derived by hand, also
through `CategoryColor` over a user list), and that a negative index, an empty list, and a null list are rejected with the
documented exception types.

##### The Catalog Has Every Scheme

**Tests**: `ColorSchemesTests.ColorSchemes_Catalog_HasSixtyEightSchemesWithUniqueNames`,
`ColorSchemesTests.ColorSchemes_Catalog_HasEverySchemeWithItsStopCount` (a theory over the 68 schemes),
`ColorSchemesTests.ColorSchemes_Catalog_FirstAndLastStopsMatchTheTable` (the same theory: first and last color, opaque
colors, and a ramp that starts and ends on them), and `ColorSchemesTests.ColorSchemes_Catalog_InteriorStopsMatchThePublishedValues`

Asserts the 68 names (15 categorical and 53 continuous, unique ignoring case, equal to the published list), the kind and
the number of colors of each, the first and last color, and the interior values named above.

##### Scheme Lookup

**Tests**: `ColorSchemesTests.ColorSchemes_TryGetScheme_IgnoresCaseAndRejectsUnknownNames`

Asserts that a scheme is found in any letter case (`tableau10` and `TABLEAU10`; `BlueGreen`, `BLUEGREEN`, and
`bluegreen` give the same scheme), that the categorical lookup finds `Category20` but not the continuous `blues` (and
gives an empty list), and that an unknown name, an empty name, a null name, and `cubehelix` are not found.

##### Continuous Interpolation

**Tests**: `ColorSchemesTests.ColorSchemes_Interpolate_IsPiecewiseLinearInSrgb`

Asserts the stop and mid-segment colors of `blues` derived above, the end colors of `viridis`, and a scheme made from
red and blue interpolated in HSL (magenta at the middle), including its name, count, kind, colors, and the rejection of
an empty list.

##### Discrete Ranges Sample Without The End Points

**Tests**: `ColorSchemesTests.ColorSchemes_Sample_UsesQuantizeInterpolatorAtIOverCountPlusOne`

Asserts the three colors derived above and that neither end color of `blues` appears.

##### Range Configuration

**Tests**: `ColorSchemesTests.ColorSchemes_Configure_ThresholdBinOrdinalOrdinalQuantileCounts`,
`ColorSchemesTests.ColorSchemes_Configure_ExtentAndReverse`

Asserts the number of colors of each scale kind (including an empty range for a bin-ordinal scale with fewer than two
edges), the whole categorical list for an ordinal scale however large the domain, the cut of a categorical scheme to
the count, the extent of a continuous range (a sub-range, a descending extent, the reverse flag, both together, and a
reversed sub-range), the reversed list of a discrete range, and that a categorical scheme ignores the extent.

##### Limits And Cancellation

**Tests**: `ColorSchemesTests.ColorSchemes_Configure_EnforcesTheColorLimitAndCancellation`

Asserts that a count over the limit is a `VegaLiteRenderException` whose message contains `the limit of 5`,
`VegaLiteLimits.MaxScaleDomainSize`, and the scheme name; that exactly the limit passes; that a threshold domain at
`int.MaxValue` does not overflow; that a pre-cancelled token stops a list of 20,000 colors; that the permitted list of
20,000 colors is produced in order; and that a null scheme is rejected.

##### Determinism

**Tests**: `ColorSchemesTests.ColorSchemes_Catalog_IsDeterministicUnderConcurrentUse`

Asserts that reading every scheme and configuring a seven-color range from several threads at once (the first use of the
lazy tables) gives the same text as a sequential second pass.

##### Notices

**Tests**: `ColorSchemesTests.ColorSchemes_Notices_ShipWithTheTablesAndNameEveryLicense`

Asserts that the ColorBrewer, Apache, Turbo, cividis, and d3 notices exist as strict UTF-8 and carry their license text
(the copyright lines and the key sentences), that the Vega license file is not empty, and that the provenance record
names every notice file, every scheme of the catalog, and the CC0 credit.
