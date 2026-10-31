### Ticks

![Scales Structure](VegaLiteScalesView.svg)

The `Ticks` unit is the third software unit in the `Scales` subsystem. It comprises two source files, `Ticks.cs` and
`Ticks.Time.cs`, in the
`DemaConsulting.CanvasNet.VegaLite.Scales` namespace, and defines two `internal` types: the static partial class `Ticks`
(with a private `TickSpec` record struct) and the readonly record struct `TimeTickRequest`. The files carry the
provenance header comments for the d3-array, d3-time, d3-scale, and Vega behavior they port.

#### Purpose

`Ticks` chooses the tick values of continuous axes: the "nice" numbers (1, 2, or 5 times a power of ten) that d3 and
Vega choose, the calendar boundaries (seconds to years) that d3-time and the time scale choose for time and UTC axes,
the extension of a domain to nice boundaries, and the tick-count rules of Vega and Vega-Lite. It exists so
that tick selection is a faithful, exact port of the d3 algorithms, with hand-derived tests, separate from layout.

The unit's behavior is specified by the `CanvasNetVegaLite-Scales-Ticks-*` requirements in
`docs/reqstream/canvas-net-vega-lite/scales/ticks.yaml`.

#### Data Model

The unit has no state. It holds three error thresholds (the square roots of 50, 10, and 2, which decide whether the
step is rounded to a multiple of 10, 5, or 2) and a private `TickSpec(I1, I2, Inc)` record struct: the first and last
integer multiples and the increment in d3's signed encoding, where a negative increment stands for the reciprocal of the
step (-10 means a step of 0.1), which keeps the arithmetic exact (0.1 times 3 never shows up as 0.30000000000000004,
because the ticks are divided rather than multiplied for fractional steps).

`TimeTickRequest(Count, Interval, Step, Utc)` (readonly record struct, in `Ticks.Time.cs`) is what a time axis or a
`nice` asks for, in the three forms Vega accepts for `tickCount` and `nice` of a time scale: a number (`Count`, the
approximate number of ticks the d3 interval table is searched with; 10 by default, `Ticks.DefaultTimeTickCount`), an
interval name (`Interval`, a `TimeIntervalKind`, the boundaries of that interval), and `{interval, step}` (`Interval`
with `Step`, every `Step`-th boundary). `Utc` records the `utc` prefix of an interval name, which uses the UTC calendar
whatever the calendar of the scale is. `TimeTickRequest.Default` is the ten-tick request and
`TimeTickRequest.TryFromInterval(name, step, out request)` reads a name through `TimeInterval.TryParseName` (the eight
interval names, the `utc` forms, and the quarter, which is three months, so that its step counts quarters). The unit
also holds the d3 interval table of `(kind, step, length)` entries for 1, 5, 15, and 30 seconds, 1, 5, 15, and 30
minutes, 1, 3, 6, and 12 hours, 1 and 2 days, 1 week, 1 and 3 months, and 1 year, where a month is 30 days and a year
365 days.

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
- `ResolveTickCount(domainStart, domainEnd, count, minStep, logarithmic, temporal)` - Vega's `tickCount`: the count is
  limited to `floor((hi - lo) / minStep) + 1` and, for a scale that is neither logarithmic nor temporal, lowered while
  the tick step is less than the minimum step (Vega skips that loop for a log scale, whose steps are not even; a time
  scale skips it too, because the d3 tick step describes numbers, not calendar intervals, so only the first limit
  applies to a time axis).
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
- `TimeTickInterval(start, stop, count, calendar, utcScale)` - d3-time's `tickInterval`: the target length is
  `|stop - start| / count`; the table entry that follows the target is found by bisection (`bisector.right`: the entries
  not longer than the target are skipped), and the entry before it is chosen instead when the target is nearer to it by
  ratio (`target / previous < next / target`; a target of 10 minutes, with the ratios 10 / 5 = 2 and 15 / 10 = 1.5,
  picks 15 minutes). A target below the first entry (1 second) gives whole milliseconds, `max(tickStep(start, stop,
  count), 1)` through the calendar's millisecond interval, and a target beyond the last (1 year) gives whole years by
  `tickStep` over the years. The result is the calendar's interval made `Every(step)`; it is null for a count that is not
  positive or a NaN span. For a **UTC** scale (`utcScale`) the two-day entry uses `TimeInterval.EveryEpochDay`: d3's UTC
  table uses `unixDay` there, so its two-day ticks count the days since the epoch (a UTC domain that starts on 2024-01-01,
  day 19,723, gets the ticks of January 2, 4, and so on), while a local `timeDay.every(2)` counts the day of the month
  (1, 3, 5, and so on); the other entries use the calendar's `Every` in both cases.
- `ResolveTimeInterval(request, start, stop, calendar, utcScale)` - turns a request into the interval: a count goes to
  `TimeTickInterval`; an interval name is the interval of the calendar (of the UTC calendar when the name had the `utc`
  prefix), and a step makes it every `Step`-th boundary; a step that gives no interval (below one, or negative) falls back
  to the default of about ten ticks, as d3 does when the scale has no interval.
- `TimeTicks(model, request, maxTicks, cancellationToken)` and `TimeTicks(domainStart, domainEnd, request, calendar,
  utcScale, maxTicks, cancellationToken)` - d3-time's `ticks` of the domain: the boundaries of the resolved interval from
  the first one at or after the start of the domain up to **and including** the stop (d3 asks the range for `stop + 1`),
  in descending order for a reversed domain; empty when there is no interval. The model overload reads the domain and
  the calendar of a `Time` or `Utc` model (an `ArgumentException` for another kind) and delegates; the instant overload
  serves a domain that no time scale model holds, the instants of a **temporal color scale**. The interval's range is
  asked for at most `maxTicks` values and its `TimeRangeLimitException` becomes a `VegaLiteRenderException` that names
  the limit (without a location); a degenerate domain gives its single instant as one tick (the d3 millisecond interval
  `every(max(tickStep, 1))`).
- `TimeNice(start, stop, request, calendar, utcScale)` - the `nice` method of d3's time scale: the start is floored and
  the end ceiled to the boundaries of the resolved interval (the count form picks the interval with `TimeTickInterval`,
  so `nice: true` is a count of 10), keeping the direction of a reversed domain; the domain is returned unchanged when
  there is no interval or a boundary leaves the years 1 to 9999 (the calendar returns NaN there). It is two interval
  operations, so it needs no limit.

**Preconditions and postconditions**: arguments are doubles (NaN and infinity are tolerated and give empty or unchanged
results); the results are deterministic and culture-independent.

#### Error Handling

The only exception is the `VegaLiteRenderException` for a tick count above `maxTicks` (without a location, because the
unit does not know the axis; `AxisBuilder` attaches the location of the channel by converting or checking the count
itself; the message contains `the limit of`, which the CLI maps to a budget error). For time ticks the same message is
made from the `TimeRangeLimitException` of the interval range, so a request for a millisecond interval over a year fails
at the first tick beyond `maxTicks` rather than after generating them all. `TimeTicks(model, ...)` throws an
`ArgumentException` for a model that is not a time or UTC scale, and the time methods throw `ArgumentNullException` for
a null calendar or model. Inputs that are not numbers give an
empty array or an unchanged domain rather than an exception, as in d3. The log ticks and the time ticks poll the
cancellation token. The unit performs no I/O and reads no clock, host zone, or culture: the time arithmetic is that of
the `TimeCalendar` it is given.

#### Dependencies

- **`VegaLiteRenderException`** and the `MaxTicks` default of **`VegaLiteLimits`** - the public types of the
  `VegaLiteDocument` unit
- **`TimeCalendar`**, **`TimeInterval`**, **`TimeIntervalKind`**, and **`TimeRangeLimitException`** (the `TimeCalendar`
  unit of the `Data` subsystem) - the calendar arithmetic of the time ticks and `nice`; see _Time Calendar Design_
- **.NET base class library** - `System` (`Math`), `System.Globalization` (the invariant parse of `"1e" + x`)

The unit depends on `ScaleModel` (the `ModelTicks` dispatch reads its transform and `TimeTicks` its calendar) within the
`Scales` subsystem and, for the calendar only, on the `Data` subsystem.

#### Callers

`ScaleResolver` (through `CreateModel`) calls `Nice` and `LogNice` (and `IsLogDomain` to refuse a log domain) and
`TimeNice` for a time scale with `nice`; `AxisBuilder`
and `LegendBuilder` call `DefaultAxisTickCount`, `DefaultAxisTickMinStep`, `ResolveTickCount` (with `temporal` for a time
axis), `ModelTicks`, `LogLabelTest`, and `TimeTicks` (the model overload for a time axis, the instant overload for the
gradient legend of a temporal color); the `TimeTickRequest` they pass is read from `tickCount` by the guides (see
_Time Labels Design_).
`NumberFormat.TickFormat` takes the same start, stop, and count. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Scales/TicksTests.cs` and `TicksTimeTests.cs` call the methods directly
through `InternalsVisibleTo`.
