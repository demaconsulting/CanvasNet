### ScaleResolver Unit Verification Design

This document describes the unit-level verification strategy for the `ScaleResolver` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace: the internal static `ScaleResolver` class (`Resolve` and
`InferOrient`) and the `ResolvedScales`, `ResolvedScale`, and `RangeEnd` types of its file. The resolver reads the view
of a parsed specification, its loaded `DataTable`, and the resolved configuration, and infers for each of the channels
`x`, `y`, `color`, `fill`, `stroke`, `size`, and `opacity` the scale kind, the domain, the range, and the paddings
according to the Vega-Lite rules for this release (linear, band, point, and ordinal scales; the types `quantitative`,
`nominal`, and `ordinal`).

#### Verification Approach

The unit is verified by parsing small, original specifications, loading their inline data with the real `SpecParser`,
`ConfigResolver`, and `DataLoader`, resolving the scales, and asserting on the resolved scales and on the models they
create for a data rectangle of a chosen size. The collaborators are real (the unit's contract is how the specification,
the data, and the configuration combine into scales), and no mocking or stubbing is used; the internal types are reached
through the test assembly's `InternalsVisibleTo` access.

The expected values are derived by hand from the Vega-Lite rules and arithmetic, with the derivation in the tests:

- A bar chart with a nominal x, a quantitative y of 3 to 12, and a nominal color: the nominal x of a bar is a band, the
  quantitative y is linear and includes zero (bars), nice with a count of 10 has step 1 so the domain is 0 to 12 (6
  maps to pixel 100 and 12 to 0 in a 200 pixel range), and the range runs from the bottom up; the band scale of three
  categories in a step of 20 takes 20 x 3 = 60 pixels (band space 3 - 0.1 + 2 x 0.05 = 3 steps), so the first band
  starts at (60 - 20 x 2.9) / 2 = 1 and is 18 wide, giving starts 1, 21, 41.
- A scatter plot includes zero by the configuration; x of 10 to 35 is nice as 0 to 35 (step 5, so 35 maps to pixel 300
  of 300) and y of 3 to 12 as 0 to 12; 5 to 15 is nice as 0 to 16 (step 2, because 15 / 10 = 1.5 is above the square
  root of 2, so 10 maps to pixel 60 of 160); and with `zero: false`, 10 to 35 has step 2 so it is nice as 10 to 36
  while 3 to 12 stays 3 to 12 (step 1).
- Pixel padding widens the domain around its middle: 0 to 100 in 200 pixels with 10 pixels of padding leaves 180 pixels
  for 100 units, so the domain grows by 10 / 1.8 = 5.56 on each side (-5.56 to 105.56), and 0 lands on pixel 10 and 100
  on pixel 190; a vertical bar on a continuous x has the configured 5 pixels of padding and no zero, and the padded
  domain 0.89 to 3.11 is extended by nice to 0.8 to 3.2 (step 0.2 at 10 ticks).
- A point mark's size range is from 4 to (0.95 x 20)^2 = 361 with zero in the domain; opacity runs from 0.3 to 0.8
  over the data extent without zero (a value in the middle of the extent gets 0.55); bars and ticks get sizes from 2
  to the step minus 1, rules from 1 to 4; a step of 10 limits the largest point size to (0.95 x 10)^2 = 90.25 and the
  bar size to 9; three categories on a point size scale are spread as 4, 182.5, and 361.
- Step-driven discrete sizes: three bars take 20 x (3 - 0.1 + 0.1) = 60 pixels, a specification step of 30 gives 90, four
  points with half a step of padding take 20 x 4 = 80, five bars on y take 20 x 5 = 100, and the configured step of 25
  gives 25 x 3 = 75; a fixed width, an explicit range, a continuous scale, and a `container` size are not step-driven.

Unit tests reside in `ScaleResolverTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scales` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own specification and data; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong scale kind, domain, range, padding, step, discrete size, color, orientation, or wrong exception type
or location constitutes a failure.

#### Unit-Level Test Scenarios

##### Scale Kinds Are Inferred From Field Type, Channel, And Mark

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_InfersQuantitativeNominalOrdinal`

Asserts that the nominal x of a bar is a band, the quantitative y is linear, and the nominal color is ordinal; that a
discrete x is a band for `rect`, `bar`, `rule`, and `tick` and a point scale otherwise; that an ordinal field is
discrete like a nominal one while keeping its type for the color range; that size and opacity follow the field type
(linear for numbers, point for categories); that fill and stroke are separate color scales; that an explicit scale type
counts when the channel and the field type accept it and the default applies otherwise; that there is no scale for a
quantitative color, a temporal field, a value definition, a datum-less definition, or `scale: null`; that a field
without a type is read as quantitative and a datum takes the type of its value; and that a view without a mark or
encoding has no scales and no orientation.

##### Orientation Of Bars, Ticks, And Rules

**Tests**: `ScaleResolverTests.ScaleResolver_InferOrient_FollowsVegaLite`

Asserts, over a table of `(mark, specified orient, x type, y type, expected orient)` rows, that a bar extends along the
measure, a tick is the opposite, a rule with one position channel runs across the view, and marks without a direction
have none.

##### Zero, Nice, Domain, Range, And Padding

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_AppliesZeroNiceDomainRangeAndPadding`

Asserts the derived domains above, that an explicit domain is used as given (no nice, and zero only when it already
spans zero) while an explicit `zero: true` overrides the rule, that `nice` may be a tick count or false, the pixel
padding example, that a horizontal bar has the measure on x with zero and no padding there, that a reversed scale or
`sort: "descending"` swaps the range ends, that an explicit range replaces the default and `"width"` stands for the
extent, that a datum has a one-value domain extended by zero to 0 to 5, that no data gives the default domain 0 to 1,
and that invalid values (null, non-numeric text) are ignored, numeric text is read, and booleans are 1 and 0.

##### Band And Point Paddings

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_PaddingsFollowMarkAndScaleProperties`

Asserts that the paddings follow the mark (bar inner 0.1, tick inner 0.25, rect and rule 0, the outer padding half the
inner), that an explicit inner padding makes the outer half of it, that a point scale has half a step outside by default
(or `padding` or `paddingOuter`) and an inner padding of 1, that the configuration overrides the mark defaults, and that
alignment and rounding are passed on.

##### Discrete Domain Order

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_DiscreteDomainsFollowTheSortProperty`

Asserts, for the sample with categories b, a, c, a in data order, the ascending default, the data order, descending,
and an explicit array (values missing from the array come first, as Vega-Lite sorts by an index of -1 for them), that an
explicit scale domain is used as given including values the data lacks, that numbers sort numerically, numeric text
sorts as text, and null sorts first, and that a field the data does not have is one null category.

##### Size And Opacity Ranges

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_SizeAndOpacityUseTheDefaultRanges`

Asserts the derived size and opacity ranges above, including the dependence on the mark, the smaller step, a mark
without sizes (`rect`), and an explicit range.

##### Colors

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_ColorsUseTableau10AndFallBackForOtherSchemes`

Asserts that a nominal field gets the `tableau10` colors in domain order with no fallback; that the named scheme
`tableau10`, a scheme object, an unknown scheme (with the fallback flag set), and an explicit range behave as
documented; that an ordinal field falls back because Vega-Lite's blue ramp is not available; and that the configured
category range (a list or a scheme) wins over the default.

##### Discrete Sizes Come From The Step

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_DiscreteSizesComeFromTheStep`

Asserts the derived step-driven sizes above, that a continuous scale has no step size, and that the model of a
step-sized scale fills the extent given by the layout.

##### Invalid Inputs Fail With A Location

**Tests**: `ScaleResolverTests.ScaleResolver_Resolve_InvalidInputsFailWithLocation`

Asserts that three categories over a `MaxScaleDomainSize` of two (at the pointer `/encoding/x`), and an explicit domain
over the limit, throw `VegaLiteRenderException` naming the limit; that a range whose second value is text
(`/encoding/x/scale/range/1`), a range of one value (`/encoding/x/scale/range`), a domain whose second value is text
(`/encoding/x/scale/domain/1`), and a linear domain of one value throw `VegaLiteSpecException` naming the offending
pointer; and that null arguments throw `ArgumentNullException` and a canceled token over nine thousand categories
throws `OperationCanceledException`.
