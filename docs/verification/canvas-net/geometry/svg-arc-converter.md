## SvgArcConverter Unit Verification Design

This document describes the unit-level verification strategy for the `SvgArcConverter` class.

### Verification Approach

The `SvgArcConverter` unit is verified through unit tests exercising both documented degenerate
cases, a true semicircle golden scenario, all four `largeArc`/`sweep` flag combinations on a
circular arc, and a rotated elliptical arc.

Verification against sampled points uses an independently written, double-precision
implementation of the SVG endpoint-to-center parameterization (reimplemented directly from the
SVG 1.1 specification text, not by reading `SvgArcConverter`'s own implementation) as an oracle,
so that a shared bug in the algorithm under test cannot also hide itself in the verification. This
oracle computes the arc's center, start angle, and angular sweep in `double` precision; test
assertions then check that every sampled point along the resulting Bezier chain lies within a
small tolerance of the corresponding circle/ellipse, and that the chain connects continuously
from the declared start point to the declared end point.

Unit tests reside in `SvgArcConverterTests.cs` within the
`DemaConsulting.CanvasNet.Tests.Geometry` project namespace.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; `SvgArcConverter` has no injectable dependencies

### Unit-Level Test Scenarios

#### CanvasNet-Geometry-SvgArcConverter-StartEqualsEnd: Zero-Length Arc Emits Nothing

**Test**: `SvgArcConverter_ToBeziers_StartEqualsEnd_EmitsNoSegments`

Calls `ToBeziers` with `start` equal to `end` and asserts the output list remains empty, per the
SVG specification's documented zero-length-arc case.

#### CanvasNet-Geometry-SvgArcConverter-ZeroRadius: Zero Radius Emits a Synthetic Straight-Line Cubic

**Tests**: `SvgArcConverter_ToBeziers_ZeroXRadius_EmitsSingleStraightLineEquivalentCubic`,
`SvgArcConverter_ToBeziers_ZeroYRadius_EmitsSingleStraightLineEquivalentCubic`

Calls `ToBeziers` with a zero x-radius, and separately a zero y-radius, and asserts exactly one
cubic Bezier segment is emitted, whose control points lie exactly at one-third and two-thirds
along the `start`-`end` chord and whose end point equals the declared `end`.

#### CanvasNet-Geometry-SvgArcConverter-ToBeziers: Golden Semicircle, All Flag Combinations, and a Rotated Ellipse

**Test**: `SvgArcConverter_ToBeziers_Semicircle_ProducesContinuousChainOnExpectedCircle`

Converts a true semicircular arc (a chord length exactly equal to the diameter, so the arc spans
exactly 180 degrees for any flag combination), asserts the resulting Bezier chain connects
continuously from `start` to `end`, that the independently computed center is equidistant from
both endpoints, that every densely sampled point along the chain lies within a small tolerance of
that circle, and that the independently computed angular sweep is exactly 180 degrees.

**Test**: `SvgArcConverter_ToBeziers_AllFourFlagCombinations_ProducesContinuousChainOnExpectedCircle`

For each of the four `largeArc`/`sweep` combinations, on a circular arc where two candidate
circles exist (so each combination produces a distinct, well-defined arc), asserts the chain
connects continuously, every sampled point lies on the independently computed circle, and the
independently computed angular sweep is at least 180 degrees when `largeArc` is `true` and at
most 180 degrees when `largeArc` is `false`.

**Test**: `SvgArcConverter_ToBeziers_RotatedEllipticalArc_ConnectsAndMatchesIndependentEllipse`

Converts a rotated, non-circular elliptical arc (`rx != ry`, 30-degree x-axis rotation), asserts
the chain connects continuously from `start` to `end`, and that the independently computed
ellipse's point at the expected end angle matches the arc's declared end point.

**Test**: `SvgArcConverter_ToBeziers_RadiiSmallerThanChord_ScalesUpAndReachesEndpoint`

Converts an arc whose requested radii are smaller than the half-chord distance between `start`
and `end` (`rx=20`, `ry=10` for a 100-unit chord along the x-axis), forcing the SVG specification's
`lambda > 1` radius scale-up correction. Asserts the resulting chain connects continuously to the
declared `end` point, and that every sampled point along the chain lies on the mathematically
derived, corrected ellipse (`rx=50`, `ry=25` - the analytically computed `sqrt(lambda) = 2.5`
scale factor applied to the requested radii), proving the scale-up correction was actually applied
rather than the original, too-small radii. This is a regression test: the existing golden
scenarios above all use radii already larger than their chord's half-distance, so none of them
previously exercised this correction path.

#### CanvasNet-Geometry-SvgArcConverter-NeverThrows: Never Throws for SVG-Valid Input

Covered by every test above: none of them expects or catches an exception, and each supplies
SVG-valid input (including both degenerate cases, every flag combination, and out-of-range radii
requiring the scale-up correction), so a passing test run is itself evidence that `ToBeziers`
never throws for these inputs.

### Acceptance Criteria

A unit test run passes when every scenario above passes without error or unexpected exception.
