### TimeAxis

![MermaidGantt Structure](MermaidGanttView.svg)

The `TimeAxis` unit is the fourth software unit in the `Gantt` subsystem. It comprises three source files in the
`DemaConsulting.CanvasNet.Mermaid.Gantt` namespace: `TimeAxis.cs` (the readonly struct `JsDate`, a JavaScript `Date`
whose local time zone is UTC, and the sealed class `TimeInterval`, a d3-time interval), `TimeAxis.Scale.cs` (the
intervals of d3-time, its tick generator, d3-array's `tickStep`, `min`, and `max`, JavaScript's `Math.round`, and the
sealed class `TimeScale`, d3-scale's time scale with `rangeRound`), and `TimeAxis.Format.cs` (d3-time-format's
`timeFormat` with the en-US locale). `TimeAxis` is an `internal static partial` class, so its two files are one type.
Each file carries the provenance header comment for the parts of d3 it ports.

#### Purpose

`TimeAxis` is the time arithmetic that Mermaid's gantt charts are built on: the JavaScript dates of Mermaid's gantt
database and of the dayjs functions it bundles, and the d3 time intervals, ticks, time scale, and time format of its
gantt renderer, all in a browser whose time zone is UTC. It exists so that every date, tick, pixel, and label of a gantt
chart is computed as JavaScript and d3 compute it, over the whole range of JavaScript dates, and so that each function
can be compared with its original on its own, apart from the grammar and the drawing.

The unit's behavior is specified by the `CanvasNetMermaid-Gantt-TimeAxis-*` requirements in
`docs/reqstream/canvas-net-mermaid/gantt/time-axis.yaml`.

#### Data Model

**`JsDate`** is a JavaScript `Date` as a value: its time value, the milliseconds since 1970-01-01T00:00Z as a double,
or NaN for an invalid date. The local time zone is UTC (no daylight saving time), so every local getter and setter is
its UTC counterpart and the time zone offset is zero. The range is ECMAScript's ±8.64e15 milliseconds (about ±275,760
years), wider than `DateTime`, because charts with years such as 202024 occur; a result beyond it is invalid
(`TimeClip`). Its constants are the milliseconds of a day, an hour, a minute, and a second, and the bounds that V8's
`MakeDay` refuses (±1,000,000 years and ±10,000,000 months). Where JavaScript mutates a date, `JsDate` is immutable and
each setter returns the changed date. Two dates compare as JavaScript's relational operators compare them (false when
either is invalid); equality is by time value, two invalid dates being equal.

**`TimeInterval`** is a d3-time interval: its floor and offset functions, which take a step callback so that the
caller can meter the walks of a filtered interval, its count function and the field function of `every` (both absent
for a filtered interval), and a replacement of `every` for the millisecond and year intervals. It is immutable.

**The intervals and tables of `TimeAxis`**: d3's `timeMillisecond`, `timeSecond`, `timeMinute`, `timeHour`, `timeDay`,
`timeSunday` to `timeSaturday`, `timeMonth`, and `timeYear`; d3-time's 18-row tick interval table, from 1 second to
1 year, with the approximate durations of a week (7 days), a month (30 days), and a year (365 days); d3-array's
thresholds of a nice step (√50, √10, and √2); and the en-US names of the days and months, long and short.

**`TimeScale`** is `scaleTime().domain([domain0, domain1]).rangeRound([range0, range1])`: two domain time values (NaN
for an end that is `undefined`) and two range values. It is immutable.

#### Key Methods

- **Dates.** `JsDate.FromTime` is `new Date(number)`, clipped and truncated. `FromComponents` is the constructor
  `new Date(year, month, date, hours, minutes, seconds, ms)`: the years 0 to 99 are 1900 to 1999 and overflowing
  components carry (ECMAScript's `MakeDay`, `MakeTime`, and `MakeDate`, with the civil-day conversions of the proleptic
  Gregorian calendar). The getters (`FullYear`, `Month`, `Date`, `Day`, `Hours`, `Minutes`, `Seconds`, `Milliseconds`,
  and `TimezoneOffset`) are NaN for an invalid date. The setters (`SetFullYear`, `SetMonth`, `SetDate`, `SetHours`,
  `SetMinutes`, `SetSeconds`, and `SetMilliseconds`) carry overflowing values and keep an invalid date invalid, except
  `SetFullYear`, which starts an invalid date from time zero, as in JavaScript. `ToString` is `toISOString`, with a
  six-digit signed year outside 0 to 9999, or `Invalid Date`, for messages and tests.
- **Intervals.** `Floor` and `Ceil` give the latest boundary at or before and the earliest at or after a date (the
  ceiling floors the time less one millisecond, offsets it by one, and floors it again, as d3 does); `Offset` moves a
  date by whole intervals, the step floored, without flooring the date; `Range` gives the boundaries from the ceiling of
  a start up to, not including, a stop, every so many, while time advances; `Count` gives the boundaries after a start
  and up to an end, floored. `Every` gives the interval of the boundaries whose field (the seconds, the day of the month
  less one, the month, the year) or count from the epoch is a multiple of the step, through `Filter`; the interval
  itself for a step of one, no interval for a step that is not positive or finite, and the special intervals of d3 for
  milliseconds (a multiple of the time value) and years (a multiple of the year, without count). A filtered interval
  walks its base interval until its test passes and calls the step callback for each step.
- **Ticks.** `Ticks(start, stop, count)` is d3-time's `timeTicks`: the boundaries, from the start through the stop, of
  the interval that `TickInterval` chooses, reversed for a descending domain, and none when no interval fits.
  `TickInterval` is `timeTickInterval`: the table row whose duration is nearest, by ratio, to the length of the domain
  over the count (a bisection of the table), a multiple of years by `TickStep` beyond the table, or a multiple of
  milliseconds below it. `Ticks(start, stop, interval)` gives the boundaries of a given interval. `TickStep` is
  d3-array's `tickStep` through its `tickSpec`: a power of ten times 1, 2, or 5, negative for a descending range.
- **`Min` and `Max`** are d3-array's `min` and `max` over dates: the earliest or latest valid date, or null
  (`undefined`) when there is none.
- **`JsRound`** is JavaScript's `Math.round`: halves toward positive infinity, NaN and the infinities unchanged.
- **`TimeScale.Map`** is the scale's map: d3-scale's `bimap` (a descending domain swaps both ends), its normalization (a
  domain of zero length maps to the middle of the range), and d3-interpolate's `interpolateRound` with `JsRound`; NaN
  for a time or a domain that is not a number, and no clamping. `TimeScale.Ticks` gives the ticks of its domain, ten by
  default or at an interval.
- **`Format(specifier, date)`** is d3-time-format's `timeFormat(specifier)(date)` with the en-US locale: the literal
  text between the directives and the text of each directive (`%a`, `%A`, `%b`, `%B`, `%c`, `%d`, `%e`, `%f`, `%g`,
  `%G`, `%H`, `%I`, `%j`, `%L`, `%m`, `%M`, `%p`, `%q`, `%Q`, `%s`, `%S`, `%u`, `%U`, `%V`, `%w`, `%W`, `%x`, `%X`,
  `%y`, `%Y`, `%Z`, and `%%`), each with an optional pad modifier (`-` none, `_` space, `0` zero) and the
  default pad zero, a space for `%e`. `%c`, `%x`, and `%X` are the locale's `%x, %X`, `%-m/%-d/%Y`, and `%-I:%M:%S %p`;
  `%Z` is `+0000`; the ISO week and year directives use the Thursday of the date's week. An unknown directive gives its
  own character and a `%` at the end gives nothing. An invalid date formats as JavaScript formats it: numbers as `NaN`
  (padded), names as empty texts, `%I` as 12, `%p` as `AM`, and `%q` as 1.

#### Error Handling

- The functions report no errors of their own: an invalid date, NaN, and the infinities flow through them as they flow
  through JavaScript and d3 (getters of NaN, empty ticks, a NaN map, `NaN` in a label).
- Where d3 would loop for ever on an invalid date (a filtered interval whose walk leaves the range of dates), the walk
  stops and gives the invalid date.
- `Count` and `Every` throw `InvalidOperationException` for an interval that has no count (a filtered interval or a
  multiple of years), where d3 has no such function and JavaScript would throw a `TypeError`.
- `Ticks` with an interval, `Min`, `Max`, and `Format` throw `ArgumentNullException` for null arguments.
- An exception of the step callback (the work meter's limit or cancellation) propagates.

#### Dependencies

- **System** - the .NET base class library only (`System`, `System.Globalization`, and `System.Text`); the unit has no
  dependency on another unit or subsystem.

#### Callers

- **GanttModel** - keeps every task date as a `JsDate`, computes the dates of dayjs (`Dayjs`) and of Mermaid's gantt
  database with its getters and setters and `JsRound`, and makes today from `MermaidRenderOptions.Now` (`GanttClock`).
- **GanttRenderer** - scales the dates with `TimeScale` over `Min` and `Max`, makes the ticks of the axes with the
  intervals, `Every`, and `TimeScale.Ticks`, and formats the tick labels with `Format`.
- The unit tests call the functions directly and compare them with the vectors recorded from d3.

#### Known Differences From Mermaid

- **The time zone.** Mermaid computes the dates in the viewer's local time zone; the port fixes it to UTC, without
  daylight saving time, so that a render does not depend on the machine. A chart drawn by Mermaid in a browser whose
  time zone is UTC has the same dates.
- **Bounds of V8.** `MakeDay` refuses the years and months that V8 refuses before computing, which only turns values
  that would be clipped anyway into an invalid date earlier.
- **No endless walks.** A filtered interval stops at an invalid date, where d3 would loop for ever.
- **Not ported.** The scale's `nice` and `clamp`, which Mermaid's gantt chart never calls, and the locales other than
  en-US, which Mermaid never sets.
