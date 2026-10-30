### Ticks

![Scales Structure](VegaLiteScalesView.svg)

The `Ticks` unit is the third software unit in the `Scales` subsystem. It is one source file, `Ticks.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines one `internal` type, the static class `Ticks` (with a
private `TickSpec` record struct). The file carries the provenance header comments for the d3-array, d3-scale, and Vega
behavior it ports.

#### Purpose

`Ticks` chooses the tick values of continuous axes: the "nice" numbers (1, 2, or 5 times a power of ten) that d3 and
Vega choose, the extension of a domain to nice boundaries, and the tick-count rules of Vega and Vega-Lite. It exists so
that tick selection is a faithful, exact port of the d3 algorithms, with hand-derived tests, separate from layout.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-Ticks-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/ticks.yaml`.

#### Data Model

The unit has no state. It holds three error thresholds (the square roots of 50, 10, and 2, which decide whether the
step is rounded to a multiple of 10, 5, or 2) and a private `TickSpec(I1, I2, Inc)` record struct: the first and last
integer multiples and the increment in d3's signed encoding, where a negative increment stands for the reciprocal of the
step (-10 means a step of 0.1), which keeps the arithmetic exact (0.1 times 3 never shows up as 0.30000000000000004,
because the ticks are divided rather than multiplied for fractional steps).

#### Key Methods

- `GetTicks(start, stop, count, maxTicks)` - about `count` evenly spaced ticks in the closed interval, in the direction
  from `start` to `stop` (descending when `stop` is below `start`); empty for a count that is not positive or is NaN,
  the single value for equal ends (empty when that value is not finite); a span that overflows the double range
  (for example -1e308 to 1e308) or has an infinite end has no ticks, and a generated tick that is not finite is
  dropped, so that no NaN or infinite value reaches the layout; a result of more than `maxTicks` (the `MaxTicks`
  limit, 1,000 by default) values is a
  `VegaLiteRenderException` thrown before the array is allocated.
- `TickIncrement(first, second, count)` and `TickStep(start, stop, count)` - the encoded increment and the signed step
  (positive when `stop` is at or above `start`).
- `Nice(start, stop, count)` - the `nice` method of a d3-scale linear scale: the interval is rounded outward to multiples
  of the tick step for the count and the rounding repeated (at most ten rounds) until the step settles; a reversed domain
  keeps its direction; a domain that does not settle, or whose step is not finite (zero width), is returned unchanged.
- `DefaultAxisTickCount(size)` - Vega-Lite's default of one tick per 40 pixels, `ceil(size / 40)`.
- `DefaultAxisTickMinStep(format)` - 1 for the integer format `d`, otherwise none.
- `ResolveTickCount(domainStart, domainEnd, count, minStep, logarithmic)` - Vega's `tickCount`: the count is
  limited to `floor((hi - lo) / minStep) + 1` and, for a scale that is not logarithmic, lowered while the tick step is
  less than the minimum step (Vega skips that loop for a log scale, whose steps are not even).
- `AxisTicks(domainStart, domainEnd, count, minStep, maxTicks)` - `GetTicks` with the resolved count.
- `ModelTicks(model, count, maxTicks, cancellationToken)` - the ticks of a continuous scale model by its transform: the
  log ticks for a log scale, the ordinary `GetTicks` of the domain for linear, pow, sqrt, and symlog, as in d3 (only
  the log scale has a `ticks` method of its own).
- `LogTicks(start, stop, count, logBase, maxTicks, cancellationToken)` - the `ticks` method of d3's `scaleLog`. With a
  whole-number base and a domain that spans fewer powers than the count, the ticks are the multiples `1 .. base - 1` of
  every power inside the domain (a power below one is divided, which keeps the decimals exact; the multiples run downward
  for a domain below zero, which d3 reflects through `-log(-x)` and `-pow(-x)`); when they are fewer than half the count
  the ordinary ticks of the domain are used; otherwise the ticks are the `GetTicks` of the exponents turned into powers
  (`Pow10` parses `"1e" + x` for a whole exponent, as d3 does, which is exact where `Math.Pow` is not guaranteed to be).
  A descending domain gives descending ticks. A domain that contains or crosses zero, an infinite end, or a base that is
  not above zero or is 1 has no ticks. The work is bounded: more than `maxTicks` ticks, or more than a hundred times
  `maxTicks` multiples looked at (a huge whole-number base), is a `VegaLiteRenderException` that names the limit, and the
  token is polled every 4,096 multiples.
- `LogNice(start, stop, logBase)` - the `nice` method of d3's `scaleLog`: the first end is floored and the last
ceiled to a
  power of the base (the same reflected functions below zero), keeping the direction; the count plays no part; a
domain the
  logarithm is not defined on is returned unchanged.
- `LogLabelTest(logBase, count, tickCount)` - the label filter of `scaleLog.tickFormat` that Vega applies (`tickLog`): a
  tick is labeled when `d / base^round(log(d) / log(base))` (multiplied by the base when below `1 - 0.5 / base`) is not
  above `max(1, base * count / ticks)`. Vega computes it with `Math.log(d)`, which is NaN for a negative tick and so hides
  every label of a negative log axis; the unit uses the magnitude (a documented deviation) so that a negative log axis is
  labeled by the same rule.
- `IsLogDomain(start, stop)` - whether the two ends make a domain the logarithm is defined on (finite, not zero, on the
  same side of zero).

**Preconditions and postconditions**: arguments are doubles (NaN and infinity are tolerated and give empty or unchanged
results); the results are deterministic and culture-independent.

#### Error Handling

The only exception is the `VegaLiteRenderException` for a tick count above `maxTicks` (without a location, because the
unit does not know the axis; `AxisBuilder` attaches the location of the channel by converting or checking the count
itself; the message contains `the limit of`, which the CLI maps to a budget error). Inputs that are not numbers give an
empty array or an unchanged domain rather than an exception, as in d3. The log ticks poll the cancellation token. The
unit performs no I/O.

#### Dependencies

- **`VegaLiteRenderException`** and the `MaxTicks` default of **`VegaLiteLimits`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System` (`Math`), `System.Globalization` (the invariant parse of `"1e" + x`)

The unit depends on `ScaleModel` (the `ModelTicks` dispatch reads its transform) within the `Scales` subsystem and on
no unit of another subsystem.

#### Callers

`ScaleResolver` (through `CreateModel`) calls `Nice` and `LogNice` (and `IsLogDomain` to refuse a log domain); `AxisBuilder`
and `LegendBuilder` call `DefaultAxisTickCount`, `DefaultAxisTickMinStep`, `ResolveTickCount`, `ModelTicks`, and `LogLabelTest`.
`NumberFormat.TickFormat` takes the same start, stop, and count. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/TicksTests.cs` call the methods directly through `InternalsVisibleTo`.
