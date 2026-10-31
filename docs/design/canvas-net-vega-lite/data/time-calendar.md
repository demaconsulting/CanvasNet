### TimeCalendar

![Data Structure](VegaLiteDataView.svg)

The `TimeCalendar` unit is the fifth software unit in the `Data` subsystem. It comprises five source files in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace, all `internal`, each with the attribution header comment for the
d3-time, d3-time-format, d3-scale, and Vega behavior it ports:

- `TimeCalendar.cs` - `TimeCalendar` (sealed partial class: the instant-to-parts and parts-to-instant conversions and
  the integer civil-date arithmetic) and `DateParts` (readonly record struct)
- `TimeCalendar.Zone.cs` - the zone half of `TimeCalendar`: the offset lookup and the wall-time-to-instant rule
- `TimeCalendar.Intervals.cs` - `TimeIntervalKind` (enumeration), `TimeInterval` (the d3-time interval object),
  `TimeRangeLimitException`, and `TimeCalendar.Interval`
- `TimeUnits.cs` - `TimeUnitSpec`: the Vega-Lite time unit model (names, parts, field names, label specifiers,
  floor, end, `maxbins` resolution, and duration)
- `TimeDateObject.cs` - `DateTimeObject`: the Vega-Lite `DateTime` object and the rule for a bare number or text
  compared with a field that has a time unit

The unit was placed in `Data` instead of `Transforms` or `Scales` (plan decision P8-D1): the data parser needs the
calendar to parse dates, and the `Expressions` subsystem needs it to format, parse, and offset dates, but the
`Expressions` subsystem must not depend on `Scales` or `Transforms`. See _CanvasNetVegaLite System Design_ for the
deviation.

#### Purpose

`TimeCalendar` is the one place that knows what a date is. It converts between an **instant** (a `double` of
milliseconds since the Unix epoch, as a `DataValue` date and as a JavaScript `Date`) and its wall-clock parts in UTC
or in one caller-supplied time zone, and it provides everything that d3-time and Vega build on that conversion: the
calendar intervals (`floor`, `ceil`, `round`, `offset`, `count`, `every`, and `range`), the time units of Vega-Lite
(`yearmonth`, `utcmonthdate`, `{"unit": "month", "step": 3}`, and so on) with their floor, end, and label rules, and the
Vega-Lite `DateTime` object. It exists so that the picture of a time series is the same on every machine: the
JavaScript libraries read the host's zone, the host's clock, and the host's culture, and this unit reads none of them.

The unit's behavior is specified by the `CanvasNetVegaLite-Data-TimeCalendar-*` requirements in
`docs/reqstream/canvas-net-vega-lite/data/time-calendar.yaml`. The rules are those of d3-time, Vega, and Vega-Lite,
re-implemented from their public description (see _CanvasNetVegaLite System Design_, Third-Party Provenance and
Attribution).

#### Data Model

**Instants and the valid range** (plan decision P8-D2). An instant is a `double` of whole milliseconds; a fraction is
truncated toward zero, as in a JavaScript `Date`. The valid range is **exactly the range of `DataValue.FromDate`**, the
years 1 to 9999 (`TimeCalendar.IsValid`: finite and between `DataValue.MinDateMilliseconds` and
`DataValue.MaxDateMilliseconds`). JavaScript's wider range of 8.64e15 milliseconds and its `Invalid Date` are replaced
by one rule, the **NaN policy**: any non-finite input, any parts that make no instant, and any result outside the valid
range is `double.NaN` at the calendar level, and a date that is NaN is null at the `DataValue` level. Every method of
the unit is a total function of its `double` arguments; nothing throws on a hostile number (the only exceptions are
`TimeRangeLimitException` and the cancellation of `TimeInterval.Range`).

**`DateParts`** (immutable record struct) is the broken-down wall time of an instant: `Year`, `Month` (0 to 11), `Date`
(1 to 31), `Day` (0 for Sunday), `DayOfYear` (1 to 366), `Hours`, `Minutes`, `Seconds`, `Milliseconds`, `OffsetMs`
(the zone offset at that instant), and `IsValid`. The fields follow the JavaScript getters, so the ported code reads
like the original. The value for an invalid instant is `default` with `IsValid` false, never garbage; the year can
read 0 or 10000 at the very edge of the range when a zone shifts the wall time.

**`TimeCalendar`** (immutable apart from its cache of interval objects; safe for concurrent use) holds an optional
`TimeZoneInfo` and, for the fast path, a constant offset. `TimeCalendar.Utc` is the singleton UTC calendar (`IsUtc`
true). `TimeCalendar.Create(TimeZoneInfo?)` returns the singleton for null (UTC is the default, and the host zone is
**never** used), the singleton for a zone that is always at offset zero, a fast fixed-offset calendar for a zone that
has no daylight saving time **and no adjustment rules** (a zone with rules may have changed its base offset over the
years, so it is not fixed even without daylight saving time), and otherwise a calendar that asks the zone. The
calendar caches one `TimeInterval` per kind.

**The zone rule** (plan decision P8-D3). The only OS-dependent input is the caller's `TimeZoneInfo`. Offsets come
from `TimeZoneInfo.GetUtcOffset(DateTime)` on UTC times, so `TimeZoneInfo.Local`, `DateTime.Now`, `ToLocalTime`, and
`DateTimeKind.Local` are never involved, and the probe instant is clamped to one day inside the `DateTime` range, so
the call never throws near the edges of the years 1 and 9999. `UtcOffsetMilliseconds(utcMs)` gives the offset at an
instant (the constant for UTC and fixed zones, NaN for a NaN instant in a zone with daylight saving time).

**Wall time to instant** (`WallToUtc`) follows ECMAScript. The offsets one day before and one day after the wall time
(read as if it were an instant) bracket the transition that can be nearby. When they agree the answer is the wall time
minus that offset. Otherwise each offset is a candidate (`wall - offset`), and a candidate is a solution when the
zone reports that same offset at the candidate. Two solutions (the clock repeats, the **overlap**) give the **earlier**
instant; one solution gives it; none (the clock jumps forward, the **gap**) gives the wall time minus the offset from
before the jump. The rule assumes at most one offset change within 48 hours, which holds for every real zone.
Worked example, in a zone at UTC-5 that moves to UTC-4 on 2024-03-10 and back on 2024-11-03: the wall time
2024-03-10 02:30 does not occur, is read with the offset from before the jump (-5 hours), and becomes 07:30Z, which
shows 03:30; the wall time 2024-11-03 01:30 occurs twice and becomes the earlier instant 05:30Z (offset -4 hours).

**Integer civil arithmetic.** The year, month, and day arithmetic is the days-from-civil and civil-from-days
algorithm on `long` values (the year starts in March, so the leap day ends the year; 400-year eras of 146,097 days),
with floor division and modulo for dates before 1970. No `DateTime` is built from an instant, so nothing overflows.
`FromParts` is ECMAScript `MakeDay`, `MakeTime`, and `MakeDate`: a month outside 0 to 11 carries into the year, a day
or a time part that overflows rolls into the next larger part (month 13 is February of the next year, date 0 is the
last day of the previous month), every argument is truncated toward zero, and years and days beyond magnitude 1e7
and 1e15 are NaN before any integer conversion. As a check on the arithmetic: 2024-02-29 is day 19,782 (the day number
of 2024-01-01 is 19,723 = 54 x 365 + 13 leap days, plus 31 + 28), so 2024-02-29T12:34:56.789Z is
19,782 x 86,400,000 + 45,296,789 = 1,709,210,096,789 ms.

**`TimeInterval`** (immutable; safe for concurrent use) is the d3-time interval of a `TimeIntervalKind`
(`Millisecond`, `Second`, `Minute`, `Hour`, `Day`, `Week`, `Month`, `Year`) in one calendar: the zone intervals
(`timeDay`, ...) for a zone and the `utc` intervals for the UTC calendar. Three flavors exist internally: a **plain**
interval; a **filter** interval (the result of `every(k)` that keeps the boundaries whose field is a multiple of the
step: every third hour, every second day, every third month, every second week); and the **closed forms** of
`millisecond.every(k)` and `year.every(k)`. The second, minute, and hour intervals floor by subtracting the wall
time-of-day components and offset in plain time (an hour is always 3,600,000 ms, and a half-hour zone floors to its own
hour); the day, week, month, and year intervals work on the civil date and re-resolve the wall time through the zone
rule, so a day is 23, 24, or 25 hours across a daylight saving change. The week starts on Sunday.

**Bounds that keep every call cheap.** A step of a filter interval is clamped to the range of its field (second and
minute 60, hour 24, day 31, month 12), which keeps the same boundaries; a week filter with a step above 64 is null;
a filter interval offsets by at most `TimeInterval.MaxFilterOffset` (64) steps and gives NaN for more; a filter
operation visits at most 128 underlying boundaries before giving up; nested `every` on a filter or closed form is null
(d3 allows it but Vega and d3-scale never use it). `EveryEpochDay(k)` is the d3 `unixDay.every(k)` that the UTC tick
table uses for its two-day entry: it keeps the days whose number since 1970-01-01 is a multiple of `k` (a UTC scale
starting on 2024-01-01, day 19,723, ticks on January 2, 4, and so on), where `Every` on the day interval keeps the
days whose day of the month is a multiple of `k`; it exists for the UTC calendar only.

**`TimeUnitSpec`** (immutable; safe for concurrent use) is a Vega-Lite time unit:

- **Names.** There are 40 local unit names: 11 single units (`year`, `quarter`, `month`, `week`, `day`, `dayofyear`,
  `date`, `hours`, `minutes`, `seconds`, `milliseconds`) and 29 multi units (`yearquarter`, `yearmonth`,
  `yearmonthdatehours`, `yearweekday`, `monthdate`, `hoursminutes`, `secondsmilliseconds`, ...), each also with the
  `utc` prefix. A `binned` prefix exists for 14 of them and sets `IsBinned`. Matching is case-sensitive, as in
  Vega-Lite. The object form is `{unit, maxbins, step, utc, binned}`; a `unit` or a `maxbins` is required, `step` and
  `maxbins` are positive numbers, and `TryParse(SpecNode, ..., ignoreUnknown)` can skip members that a unit does not
  define (the parser warns about them; the executors must not fail on them).
- **Parts.** `Parts` lists the calendar parts the unit keeps in Vega-Lite's order (`year quarter month week day dayofyear
  date hours minutes seconds milliseconds`); the parts of a name are found by Vega-Lite's containment rule, which
  does not count `seconds` inside `milliseconds`, `day` at the start of `dayofyear`, or `year` at the end of
  `dayofyear`. The `maxbins` form without a unit has no parts until it is resolved against data. A private copy in
  Vega's own order decides the **last part**, which is the part the step and the end refer to.
- **Field names.** `FieldName(field)` is Vega-Lite's `timeUnitToString` naming: `yearmonth_date`, `utcmonth_date`,
  `month_step_2_date`, and `timeunit_maxbins_10_date`, with the members of an object written in document order (so
  `{"step": 2, "unit": "month"}` gives `_step_2month_date`, a quirk of Vega-Lite that is reproduced) and every character
  that is not an ASCII letter, digit, or underscore replaced by an underscore (`VarName`). `Name` is the same without
  the field.
- **Specifiers.** `Specifier()` is Vega's `timeUnitSpecifier` with the Vega-Lite overrides: the longest run of parts
  that has a specifier of its own is taken at each step, the specifiers are joined and trimmed, and the overrides are
  `year-month` as `%b %Y`, `year-month-date` as `%b %d, %Y`, and `hours-minutes` as `%H:%M`. The defaults per part are
  `%Y`, `Q%q`, `%b`, `%d`, `W%U`, `%a`, `%j`, `%H:00`, `00:%M`, `:%S`, and `.%L`, so `yearmonth` is `%b %Y` and
  `minutesseconds` is `00:%M:%S` (no space, because Vega's algorithm joins the keys `00:%M` and `:%S`).
  `TryGetUnitSpecifier(units, overrides, out specifier)` is the expression function `timeUnitSpecifier` with Vega's own
  defaults and the rejection of the combinations Vega rejects (the week, the quarter/month/date, and the day-of-year
  families mixed, or the week with the ISO week).
- **Floor and end.** `Floor(calendar, ms)` is Vega's `timeFloor` and `utcFloor`: the parts below the unit are reset,
  a part the unit does not keep above it takes the reference value (the year 2012, a leap year that starts on a
  Sunday, and January), the weeks are Vega's (the number of Sundays from January 1), a day of the week is a day of
  January of the floor's year, and the step applies to the last part only. `End(calendar, startMs)` is one step of the
  smallest part after the start through the interval of that part (a quarter is three months; a date, a day, and a day
  of the year are days), so the end of a day unit is 23, 24, or 25 hours after the start in a zone. A `utc` unit always
  uses `TimeCalendar.Utc` whatever calendar is passed. An invalid instant, and a unit without parts, give NaN.
- **`maxbins` and `timeBin`.** `ResolveMaxBins(minMs, maxMs)` is Vega's `timeBin`: the target width is the span
  divided by `maxbins`, and the entry of the bin table (17 entries from one second to one year) whose duration is
  nearest in ratio wins, by a right bisection; a target wider than a year takes whole years with the d3-array tick
  step over the years, and a target narrower than a second takes milliseconds with a tick step of at least 1. A
  private copy of the d3-array `tickStep` keeps `Data` free of a dependency on `Scales`. A non-finite extent gives one
  year.
- **Duration.** `DurationMilliseconds()` is Vega-Lite's `durationExpr`, the length of one step of the smallest part
  measured from 2001-01-01 **in UTC** (Vega-Lite measures it in the local zone; this keeps it independent of the zone):
  the axis uses it as the minimum tick step. It is null when the smallest part is the day of the week or the unit has
  no parts.
- `IsSingleLocalUnit` tells the units for which a bare number or text can stand for one part.

**`DateTimeObject`** (immutable; safe for concurrent use) is the Vega-Lite `DateTime` object. The keys are `year`,
`quarter`, `month`, `date`, `day`, `hours`, `minutes`, `seconds`, `milliseconds`, and `utc`. A month is 1 to 12, a
numeric text, or a month name (full or the first three letters, any case); a quarter is 1 to 4 and stands for its first
month (the month wins); a day is 1 (Monday) to 7 or 0, or a day name, and stands for that day of January 2012. Like
Vega-Lite, the object **drops its `day` when it has any other key** (the `utc` flag included, recorded in `DayDropped`),
and a quarter above 4 is recorded in `QuarterOutOfRange`. A missing year is 2012, a missing month January, a missing
date the first, and a missing time part zero. `ToMilliseconds(calendar)` evaluates the object like Vega's `datetime(...)`
or `utc(...)` function: years 0 to 99 mean 1900 to 1999 (`FullYear`, as in the JavaScript constructor), and parts that
are too large roll over (`FromParts`); `ToExpression` writes the same call as expression source text. The static
`FromScalar` is Vega-Lite's `valueExpr` rule for a bare number or text compared with a field that has a time unit: a
number below 10,000 or a text such as `Mar`, against a single local unit, is that part of the reference year (the number
3 against `month` is March 2012); otherwise a number is milliseconds and a text goes through a caller-supplied
`Func<string, double>` (the `Date.parse` rules of `JsDateParser`, plugged in by the callers). `TryUnitPartExpression`
writes the same single part as a `datetime(...)` expression for the predicate source.

**Limits** (every loop or allocation of the unit; the caller charges the rows or elements):

- _Calendar arithmetic, one conversion_: O(1), at most 1 offset lookup for `ToParts` and 4 for `FromParts` in a zone;
  charged by the caller (transform `WorkMeter`: 2 steps per row for UTC, 8 for a zone).
- _Filter interval operation_: at most 128 underlying boundaries, and offsets of at most 64 steps; nothing to charge.
- _`TimeInterval.Range`_: at most `maxCount` items, then `TimeRangeLimitException`; cancellation is polled every 1,024
  items.
- _`TimeUnitSpec` parse and field name_: linear in the text, which the callers bound by their text limits.
- _`maxbins` resolution_: a bisection over 17 entries.
- _Offset probes_: clamped one day inside the `DateTime` range.

**Determinism.** Only the passed calendar, the English names, and invariant ASCII digits reach any result; no
host zone, clock, or culture is read (a grep gate over the time sources for `TimeZoneInfo.Local`, `DateTime.Now`,
`UtcNow`, `ToLocalTime`, and `CurrentCulture` is empty, and the tests run under `tr-TR` and `de-DE`). Results are
byte-identical across repeated and concurrent calls. The only OS-dependent input is the caller's `TimeZoneInfo`, whose
rules differ between operating system time zone databases for historical dates; a fixed-offset or custom zone
reproduces exactly.

#### Key Methods

- `TimeCalendar.Create(zone)` and `TimeCalendar.Utc` - build the calendar of a chart (null is UTC); `IsUtc`, `Zone`,
  and `IsValid(ms)` describe it.
- `ToParts(ms)` and `FromParts(year, month, date, hours, minutes, seconds, milliseconds)` - the two directions of the
  conversion, with the gap and overlap rule and the NaN policy; `UtcOffsetMilliseconds(ms)` - the offset at an instant.
- `Interval(kind)` - the `TimeInterval` of a kind; `TimeInterval.Floor`, `Ceil`, `Round`, `Offset(ms, step)`,
  `Count(start, end)`, `Every(step)`, `EveryEpochDay(step)`, and `Range(start, stop, step, maxCount, token)`.
- `TimeInterval.TryParseName(name, out kind, out stepMultiplier, out forceUtc)` - reads an interval name of a
  specification: `millisecond`, `second`, `minute`, `hour`, `day`, `week`, `month`, `year`, the Vega time-unit
  spellings the code also uses (`milliseconds`, `seconds`, `minutes`, `hours`, `date`, `dayofyear`, and `quarter`,
  which is the month kind with a multiplier of 3), each optionally with the `utc` prefix (`forceUtc`); the match
  ignores case without consulting the culture.
- `TimeUnitSpec.TryParse` (text and object forms), `TryRead` (a validated `timeUnit` node), `FieldName`, `Specifier`,
  `TryGetUnitSpecifier`, `Floor`, `End`, `ResolveMaxBins`, and `DurationMilliseconds`.
- `DateTimeObject.TryRead(node, out value, out error, out at)`, `ToMilliseconds(calendar)`, `ToExpression`, and the
  `FromScalar` overloads (a number, and a text with a date-parsing delegate).

**Preconditions and postconditions**: every argument may be any `double`; a result is either a valid instant or NaN
and no method depends on global state. `TryParse` and `TryRead` return false with a reason, never throw, for an
invalid name or object.

#### Error Handling

No method throws for any value of its numeric arguments: NaN, infinity, and out-of-range values give NaN (or an
invalid `DateParts`, or an empty range). `TimeInterval.Range` throws `TimeRangeLimitException` (an
`InvalidOperationException` with the largest allowed count) when the list would hold more than `maxCount` items, and
`OperationCanceledException` when the token is cancelled; callers map the first to a located
`VegaLiteRenderException` that names the limit they enforce (`MaxTicks` for the ticks of an axis or a legend,
`MaxScaleDomainSize` elsewhere), so the exception never reaches a public caller. An invalid time unit or `DateTime`
object is not an exception but a false result with an `error` text (and, for `DateTimeObject`, the node the error is
about); `SpecParser` turns it into a located `VegaLiteSpecException`. A null argument is an
`ArgumentNullException`. The unit performs no I/O.

**Documented deviations from JavaScript, d3, and Vega** (each pinned by a test):

1. **Range and NaN** (P8-D2): years 1 to 9999 only, NaN instead of `Invalid Date` and instead of the range of
   8.64e15 milliseconds.
2. **No host zone, clock, or culture** (P8-D3): the zone is the render option; the gap and overlap rule is
   ECMAScript's (ES2023) and assumes one transition within 48 hours.
3. **Bounded filter intervals**: steps clamped to the field range, a week step above 64 is null, and an offset of
   more than 64 steps of a filter interval is NaN; nested `every` is null.
4. **`EveryEpochDay`** exists because d3's UTC tick table counts two-day ticks from the epoch.
5. **No ISO-week unit**: Vega-Lite has none (`isoweek` exists in the specifier helper only).
6. **`DurationMilliseconds` in UTC**, where Vega-Lite measures it in the local zone.
7. **`DateTimeObject.ToMilliseconds` has no unit argument**, because Vega-Lite's `dateTimeToExpr` ignores it.
8. **A non-finite extent of `maxbins`** resolves to one year (Vega would produce NaN steps); callers skip an empty
   table.

#### Dependencies

- **`DataValue`** - `MinDateMilliseconds` and `MaxDateMilliseconds`, the range of a date (the `DataTable` unit)
- **`JsNumber`** - invariant JavaScript number text for field names and expression source (the `DataTable` unit)
- **`SpecNode`** - the object and text forms that `TimeUnitSpec.TryParse` and `DateTimeObject.TryRead` read (the
  `SpecParser` unit)
- **.NET base class library** - `System.TimeZoneInfo` (offsets only), `System.Text`, `System.Linq`, and
  `System.Collections.Generic`; `System.Globalization` is not used for any decision

#### Callers

`VegaLiteDocument` creates one calendar per `Measure` or `Render` call from `VegaLiteRenderOptions.TimeZone` and gives
it to the `DataLoader`, which hands it to `ParseDirectives` for dates without an offset and for `date:'%fmt'`
formats (see _DelimitedTextParser_ and _DataLoader_), and to the `ExpressionFunctionContext`, which the date and time
functions of `ExpressionFunctions` read. `TimeFormat` and `JsDateParser` (see _TimeFormat_) format and parse in the
calendar. `SpecParser` (`SpecParser.Time.cs`) validates time units, date-time objects, and tick and nice intervals with
`TimeUnitSpec.TryParse` and `DateTimeObject.TryRead`, and `PredicateSource` writes time-aware predicates with
`DateTimeObject.ToExpression` and `TimeUnitSpec`. `TimeUnitTransform`, `EncodingPlan`, and `TemporalFields` (see
_TimeUnitTransform_) compute and plan the units. `ScaleModel` (`CreateTime`), `Ticks` (`Ticks.Time.cs`), and
`ScaleResolver` (`ScaleResolver.Time.cs`) build the time scales and their ticks and `nice` boundaries from the
intervals and read the domains with `DateTimeObject` and the unit rule. `AxisBuilder`, `LegendBuilder`, and
`TimeLabels` (see _TimeLabels_) label and place the ticks, and `MarkBuilder` reads the band of a unit. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Data/` (`TimeCalendarTests.cs`, `TimeCalendarZoneTests.cs`,
`TimeCalendarIntervalTests.cs`, `TimeCalendarEpochDayTests.cs`, `TimeUnitSpecTests.cs`, and `DateTimeObjectTests.cs`,
with the zone helpers in `TimeTestSupport.cs`) call the types directly through `InternalsVisibleTo`, with zones they
construct themselves, so they do not depend on the time zone database of the operating system.
