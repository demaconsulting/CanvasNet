### TimeFormat Unit Verification Design

This document describes the unit-level verification strategy for the `TimeFormat` unit in the
`DemaConsulting.CanvasNet.VegaLite.Data` namespace: the internal static `TimeFormat` class (`Compile`, `Pad`, `Multi`,
and `TryParse`, in `TimeFormat.cs`, `TimeFormat.Format.cs`, `TimeFormat.Parse.cs`, and `TimeFormat.Multi.cs`) with the
immutable `TimeFormatter` and `TimeFormatTooLongException`, which are the d3-time-format formatter and parser with the
English locale and Vega's multi-format (the default labels of a time axis); and the internal static `JsDateParser`
class (`TryParse`, in `JsDateParser.cs` and `JsDateParser.Legacy.cs`), the `Date.parse` rules that Vega's date parsing
relies on: the ECMAScript date-time string format and a documented subset of the legacy forms. The unit never uses the
platform's date formatting or `DateTime.Parse`, so its text and its numbers are the same on every machine and under
every culture.

#### Verification Approach

The unit is verified by calling `Compile`, `Format`, `Multi`, `TryParse`, and `JsDateParser.TryParse` directly with the
real `TimeCalendar` (UTC, and the United States like, fixed-offset, and +05:30 zones that `TimeTestSupport` builds with
`TimeZoneInfo.CreateCustomTimeZone`, never `TimeZoneInfo.Local`). No dependency is mocked or stubbed, and the internal
types are reached through the test assembly's `InternalsVisibleTo` access. The expected texts and instants are derived
by hand from the definition of each d3-time-format directive and from the ECMAScript and Vega rules (or, for plain
calendar dates, computed with `DateTimeOffset`), and not by running d3 and pasting its output. The derivations:

- **The formatting anchor.** 2024-03-05T13:04:09.007Z: 2024-03-05 is day 19,787 (2024-01-01 is day 19,723 = 54 x 365 +
  13 leap days, plus 31 + 29 + 4 days), so the day starts at 19,787 x 86,400,000 = 1,709,596,800,000, and 13:04:09.007
  is 46,800,000 + 240,000 + 9,000 + 7 = 47,049,007 ms; the instant is **1,709,643,849,007**. The day is a Tuesday
  (19,787 mod 7 = 5 days after the Thursday of the epoch) and the 65th day of the year (31 + 29 + 5). The same day at
  03:04:09.007 is 1,709,596,800,000 + 11,049,007 = 1,709,607,849,007, which makes the hour a single digit.
- **Every directive for the anchor, in UTC.** The text of each directive is derived from the definition:

| Directive | Text                   | Directive | Text           | Directive | Text         |
|-----------|------------------------|-----------|----------------|-----------|--------------|
| `%a`      | `Tue`                  | `%L`      | `007`          | `%u`      | `2`          |
| `%A`      | `Tuesday`              | `%m`      | `03`           | `%U`      | `09`         |
| `%b`      | `Mar`                  | `%M`      | `04`           | `%V`      | `10`         |
| `%B`      | `March`                | `%p`      | `PM`           | `%w`      | `2`          |
| `%c`      | `3/5/2024, 1:04:09 PM` | `%q`      | `1`            | `%W`      | `10`         |
| `%d`      | `05`                   | `%Q`      | `1709643849007`| `%x`      | `3/5/2024`   |
| `%e`      | space, `5`             | `%s`      | `1709643849`   | `%X`      | `1:04:09 PM` |
| `%f`      | `007000`               | `%S`      | `09`           | `%y`      | `24`         |
| `%g`      | `24`                   | `%H`      | `13`           | `%Y`      | `2024`       |
| `%G`      | `2024`                 | `%I`      | `01`           | `%Z`      | `+0000`      |
| `%j`      | `065`                  |           |                | `%%`      | `%`          |

- **Week numbers of the anchor.** The Sunday-based `%U` counts the Sundays since January 1 up to the date: 2024-01-01
  is a Monday, so the Sundays are January 7, 14, 21, 28, February 4, 11, 18, 25, and March 3, which is 9. The
  Monday-based `%W` counts the Mondays: January 1, 8, 15, 22, 29, February 5, 12, 19, 26, and March 4, which is 10. The
  ISO week `%V`: the week of the first Thursday is week 1 and January 1, 2024 (a Monday) starts week 1, so March 5, the
  65th day, is in week (65 - 1) / 7 + 1 = 10; the weekday `%u` (Monday is 1) is 2 and `%w` (Sunday is 0) is 2.
- **Padding flags** (the instant 03:04:09.007Z): `%-d` is `5`, `%_d` is a space and `5`, `%0e` is `05`, `%-e` is `5`,
  `%-m` is `3`, `%_m`, `%_H`, and `%_I` are a space and `3`, `%-H` and `%-I` are `3`, `%-j` is `65`, `%_j` is a space
  and `65`, `%-L` is `7`, `%-f` is `7000`, `%-M` is `4`, `%-S` is `9`, `%-U` is `9`, `%_Y` is `2024` (a four-digit
  year has nothing to pad), `%-y` is `24` (a two-digit field of 24 has nothing to remove), `%-a` is `Tue`, `%-Q` keeps
  all digits, and `%-x` is `3/5/2024` (the locale expansion already has no padding). The function `Pad` writes the sign
  first and then the fill: `Pad(-5, '0', 2)` is `-05`, `Pad(-123, ' ', 5)` is the sign, two spaces, and `123`,
  `Pad(1234, '0', 2)` is `1234` (wider than the width), `Pad(5, none, 3)` is `5`, and the smallest `long` keeps all
  its digits.
- **Twelve-hour clock.** 2024-03-05T00:00Z is 1,709,596,800,000; hour 0 shows as 12 AM, 12:00 as 12 PM, and 23:00 as
  11 PM.
- **Week numbers at the new year** (`%U %W`): 2012-01-01 (a Sunday) is `01 00` (the first Sunday is January 1 and the
  first Monday is January 2); 2012-01-02 is `01 01`; 2012-12-30 (a Sunday, the 53rd, because 52 weeks after January 1)
  is `53 52` and 2012-12-31 (the 53rd Monday: January 2 plus 52 weeks) is `53 53`; 2013-01-01 (a Tuesday) is `00 00`;
  2013-01-06 (a Sunday) is `01 00` and 2013-01-07 is `01 01`.
- **ISO week and week-based year** (`%V %G %g`): the week belongs to the year of its Thursday. 2021-01-03 (a Sunday) and
  2020-12-31 are in week 53 of 2020 (2020 starts on a Wednesday and is a leap year, so it has 53 weeks); 2024-12-30 (a
  Monday) is in week 1 of 2025 (January 1, 2025 is a Wednesday, so the week of Monday December 30 holds it); 2018-12-31
  (a Monday) is week 1 of 2019 (January 1, 2019 is a Tuesday); 2015-01-01 (a Thursday) is week 1 of 2015; 2016-01-03 (a
  Sunday) is week 53 of 2015 (2015 starts on a Thursday and has 53 weeks); 2024-01-01 is week 1 of 2024 and 2024-03-05
  is week 10.
- **The ends of the range.** The years are padded to four digits (`0005`, `0001`); a wall time in a zone that is five
  hours behind at the first instant of year 1 is `0000-12-31 19:00`; the last instant 253,402,300,799,999 is
  `9999-12-31T23:59:59.999`; `%C` is not a d3 directive, so it is copied as the letter `C` (see the unknown
  directives below).
- **The zone directive.** `%Z` writes the offset of the instant as `+hhmm`: `+0530` at +05:30, `-0330` at -03:30, and
  in the United States like zone `-0500` on 2024-01-15 and `-0400` on 2024-07-15. The local fields follow the offset:
  2024-03-05T20:00Z at +05:30 is 2024-03-06 01:30:00.000 (a Wednesday); the United States like change is at
  2024-03-10T07:00Z = 1,710,054,000,000 (19,792 days x 86,400,000 + 7 h), so 1,710,053,999,999 is
  `2024-03-10 01:59:59.999 Sun` and 1,710,054,000,000 is `2024-03-10 03:00:00.000 Sun`. `%Q` and `%s` do not depend on
  the zone, and `%s` floors a negative instant: -1,500 ms is `-1500` and `-2`.
- **Unknown directives and edge cases** follow d3: the percent sign is dropped and the letter is copied (`%k` is `k`,
  `%E` is `E`, `%5` is `5`, `%-k` is `k`), a trailing percent sign gives nothing (`100%` is `100`), a flag with no
  letter gives nothing (`%-` and `%0`), `%%` is a percent sign, and `%_%` is a percent sign. An instant that is NaN,
  infinite, or outside the years 1 to 9999 formats as `Invalid Date` whatever the specifier, a fraction of a
  millisecond is truncated toward zero (0.9 ms is dropped, -0.5 ms is the epoch), and a text longer than the limit
  throws `TimeFormatTooLongException`: `%c%c%c%c` is 4 x 20 = 80 characters, so it fits a limit of 80 and not 79, a
  literal of 5,000 characters fails a limit of 100 without being built, and even `Invalid Date` is refused by a limit
  of 5.
- **Vega's multi-format** (the default labels of a time axis, in UTC): the label is that of the coarsest boundary the
  instant sits on. With 2024-03-05T00:00Z = 1,709,596,800,000 and a day of 86,400,000 ms: the instant itself is a day
  that is not a week start, `Tue 05`; two days earlier is Sunday March 3, a week start that is not a month start,
  `Mar 03`; four days earlier is March 1, `March`; 27 days later is April 1, `April`; 13 hours after March 5 is
  `01 PM`; 13:45 is `01:45`; 13:45:30 is `:30`; 13:45:30.250 is `.250`; one day later is `Wed 06`; 33 days earlier is
  February 1, `February`; 64 days earlier is January 1, `2024`; 118 days later is July 1, `July` (a quarter start is
  a month label); five days earlier is Thursday February 29, `Thu 29`. Before the epoch, -1 ms is `.999` and 0 is
  `1970`. The overrides replace the label of their own level and nothing else (the keys `milliseconds`, `seconds`,
  `minutes`, `hours`, `date`, `week`, `month`, `quarter`, and `year`: for the same instants `ms:250`, `s:30`, `m:45`,
  `h:13`, `d:05`, `w:09` (the `%U` of March 3), `mo:03`, `q:2` (April 1 is in quarter 2), and `y:2024`); `day` stands
  in for `date` and `date` wins over `day`; an empty override keeps the default. At +05:30, local midnight of March 5
  is 18:30Z on March 4 (`Tue 05`), 00:00Z on March 5 is 05:30 local (`05:30`), 13:30Z is 19:00 local (`07 PM`), and
  2023-12-31T18:30Z is the local new year (`2024`); the formatter is bound to the calendar it was made with and
  ignores the calendar passed to `Format`.
- **Parsing round trip.** The parser is d3's, and the expected instants are derived from the same anchors: the formats
  that the formatter writes parse back to the instant as far as the specifier shows it (the anchor with milliseconds,
  the anchor less 7 ms without them, midnight of 2024-03-05 for a date). A quarter alone gives its first month
  (`2024-Q3` is July 1) and an explicit month wins.
- **Parsing weeks and names.** The week directives convert to days. With 2024-01-01 a Monday, Sunday-week 9 with the
  ISO weekday 7 is January 63 = Sunday March 3 (March 5 less 2 days), ISO week 10 with no weekday is the Monday of
  that week, March 4 (March 5 less 1 day), the Monday-based week 10 is the same Monday, and the Sunday-based week 9
  is Sunday March 3; an ISO week outside 1 to 53 fails. Names and the day period are matched ignoring case:
  `jan 02, 2006 01:45 pm` is 2006-01-02T13:45Z.
- **Parsing defaults and the pivot.** With no date the defaults are 1900-01-01, which is 25,567 days before the epoch
  (70 x 365 + 17 leap days from 1904 to 1968); 25,567 x 86,400,000 = 2,208,988,800,000, so 1900-01-01T00:00Z is
  -2,208,988,800,000 and `01:45 PM` (13:45 = 49,500,000 ms) is **-2,208,939,300,000**; `12:30 AM` is 00:30 and
  `12:30 PM` is 12:30. The two-digit year pivot is 68: 69 to 99 are 1969 to 1999 and 00 to 68 are 2000 to 2068.
- **Parsing zones and timestamps.** A zone directive makes the fields UTC and subtracts the offset in any calendar:
  `2024-03-05T10:00:00+0200`, `+02:00`, and `+02` are 08:00Z, `-0530` is 15:30Z, and `Z` is 10:00Z, while a lower-case
  `z` fails. d3's unanchored zone expression is reproduced: `+0200` alone is 1900-01-01T00:00Z less 2 hours
  (1899-12-31T22:00Z) and `x+0200` fails. The timestamps are `%Q` 1,709,643,849,007 and `%s` 1,709,643,849 (and
  1,709,643,849,250 with `%s.%L` and 250); a negative number or 40 digits fail.
- **Parsing ranges and windows.** Without range validation (as in d3) the month 13 of 2024 rolls to 2025-01-01,
  February 30 to March 1 (1,709,251,200,000), hour 25 to 01:00 the next day, minute 61 to the next hour, and date 0 to
  February 29. The windows of the numbers: `5/03/2024` with a leading space or a leading no-break space is March 5
  with `%e`; two spaces and `5` fail with `%d` (the window of two characters has no digit); a space and `2024` fail
  with `%Y` while a space and `202` is the year 202; `09.5` with `%L` is 5 ms; `09.123456` with `%f` is 123 ms; and
  `09.1234` with `%L` fails.
- **Parsing small years and local times.** The years below 100 follow d3 (the date is built in the year -1 and the year
  is set afterward): `0005-03-05` is as written, `0004-02-29` is March 1 of the year 4, `0000-01-01` fails, and
  `0099-12-31` is as written. A local parse reads the wall time in the zone: `2024-01-15 12:00` is 17:00Z at -5 h,
  `2024-07-15 12:00` is 16:00Z at -4 h, `2024-03-10 02:30` (a gap) is 07:30Z, and `2024-11-03 01:30` (an overlap) is
  05:30Z. Text longer than 256 characters fails at once.
- **`Date.parse` ISO forms.** A date without a time is UTC in every calendar: `2024-03-05` is 1,709,596,800,000 (day
  19,787), and `2024-03` and `2024` are the first of the month and the first of the year; a date-time without an
  offset is in the zone of the calendar (the mixed rule of ECMAScript): `2024-03-05T10:00` is 15:00Z at -5 h,
  `2024-07-15T10:00:00` is 14:00Z at -4 h, and `2024-03-05 10:00:00` at +05:30 is 04:30Z; an offset is honored in
  every calendar (`Z` 10:00Z, `+02:00` 08:00Z, `-05:30` 15:30Z, `+0200`, and `+02`). Fraction digits are truncated to
  milliseconds (`5` is 500, `05` is 50, `123456789` is 123, `0059` is 5).
- **`Date.parse` ranges.** The expanded years `+002024-03-05` parse, and the year 0 (either sign), negative years, and
  years above 9999 do not. The ends: `0001-01-01` is -62,135,596,800,000 (719,162 days before the epoch) and
  `9999-12-31T23:59:59.999Z` is 253,402,300,799,999 (2,932,897 days x 86,400,000 less one millisecond); one
  millisecond beyond either (`-01:00` on the last millisecond, `+01:00` on the first) is not a date; the year 50 is
  taken literally.
- **`Date.parse` days and times.** The leap day exists in 2024 and 2000 and not in 2023 and 2100, April has 30 days,
  and the month 0 or 13 and the day 0 are rejected. The time `24:00` (also `24:00:00` and `24:00:00.000`) is the end
  of the day, March 6; any other time with the hour 24, the hour 25, the minute or second 60, and the offsets
  `+24:00` and `+02:60` are rejected. Text that resembles the ISO form but is not it (`2024-3-5`, `2024-03-05T`,
  `2024-03-05T10`, `2024-03-05T10:00:`, `2024-03-05T10:00:00.`, `2024-03-05Z`, `20240305`, a one-digit offset, a space
  before the offset, and non-ASCII digits) is not a date.
- **`Date.parse` legacy forms.** The legacy forms with a month name are local: `Jan 2, 2006` is 2006-01-02T00:00Z (and
  22:00Z on January 1 at +02:00), `January 2, 2006 10:00` is 10:00Z, and `2 Jan 2006`, `Jan. 2, 2006`, and
  `Jan 02 2006` are the same day. The long forms skip the weekday without checking it (2006-01-02 was a Monday and
  `Tue,` is accepted), `GMT` and `UTC` are zero offsets, `GMT+0200` is two hours ahead
  (`Mon Jan 02 2006 15:04:05 GMT+0200` is 13:04:05Z), a parenthesized comment is ignored, and
  `Jan 2, 2006 10:00 +0530` is 04:30Z. The numeric forms are `2006/01/02` and `1/2/2006` (month first), with times and
  `AM` and `PM` (`12/31/1999 11:59:59 PM` is 23:59:59, `12:00 AM` is 00:00, `12:00 PM` is 12:00, `3:30pm` is 15:30)
  and a fraction (`10:00:00.250` is 250 ms). Month names match in any case, in full or with at least three letters
  (`Sept`).
- **`Date.parse` rejections and the length limit.** The rejected text is the empty and the white-space text, `abc`,
  `Sunday`, an unknown or two-letter month, `Feb 30`, a two-digit year, the hour 25 or the minute 61, a bare hour,
  trailing junk or an unterminated comment, `13:00 PM`, `0:30 AM`, the month 13, an offset beyond +24 hours or with
  three digits, a 13-digit token, and the lone `12`, `+`, and `-`. Text of 128 characters is examined and 129 are
  rejected before any scan (a one-megabyte text costs nothing).
- **`Date.parse` derived instants.** 2020-04-01 is day 18,353 (50 x 365 + 12 leap days is 18,262 for 2020-01-01, plus
  31 + 29 + 31), so the instants are 1,585,699,200,000 for `APRIL 1, 2020`, 1,585,778,400,000 for
  `April 1, 2020 10:00 PM` (22:00 is 79,200,000 ms later), and 1,585,735,200,000 for `2020-04-01T10:00:00Z`
  (36,000,000 ms later).
- **Culture.** Every parse and every format is repeated under `tr-TR` (the Turkish dotted capital I) and `de-DE` and
  compared with the invariant culture: the names and the digits are the English ASCII ones, `FRIDAY` and `APRIL`
  match, and 2024-04-05 (a Friday, day 19,818 = 19,787 + 31) is 1,712,275,200,000, so `apr  5 2024 pm` is noon of
  that day, 1,712,318,400,000.

- **Specifier limits.** A specifier of 1,024 characters compiles (and formats itself back as literal text) and one of
  1,025 throws `TimeFormatSpecifierException`, with a message that contains `the limit of 1024 characters`. One `%c`
  expands to 13 tokens (`%x, %X` is `%-m/%-d/%Y` with 5 tokens, the literal `,`, and `%-I:%M:%S %p` with 7), so 315
  copies are 4,095 tokens and compile (to 315 x 20 = 6,300 characters of `3/5/2024, 1:04:09 PM`), 316 copies are 4,108
  tokens and 512 copies (1,024 characters) throw with a message that contains `the limit of 4096 pieces`. A
  multi-format with an override of 1,025 characters throws the same exception, and one of `Y%Y` gives `Y2024` for
  January 1, 2024.

Unit tests reside in `TimeFormatTests.cs` (the formatter), `TimeFormatParseTests.cs` (the parser),
`TimeFormatMultiTests.cs` (Vega's multi-format), and `JsDateParserTests.cs` (the `Date.parse` rules), with the shared
helper `TimeTestSupport.cs`, all within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Data` folder.
The label specifiers of the time units (`TimeUnitSpec.Specifier` and `TimeUnitSpec.TryGetUnitSpecifier`) are verified
with the calendar unit (see the _TimeCalendar Unit Verification Design_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None
- **Isolation**: Pure functions of numbers and text. Every zone is a custom zone created by the test; no test reads the
  host's zone, clock, or culture. The culture scenarios set the current culture to `tr-TR` and `de-DE` and restore it
  afterwards

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any formatted text that differs from the hand-derived expectation (compared character by character), any
specifier or text accepted or rejected differently from the rules above, any parse that throws, any text that exceeds a
limit without the documented exception or the documented early rejection, and any output that depends on the current
culture or the host's zone constitutes a failure.

#### Unit-Level Test Scenarios

##### Every Directive Formats The Hand-Derived Text

**Tests**: `TimeFormatTests.TimeFormat_Format_EveryDirective_Utc`, `TimeFormatTests.TimeFormat_Format_PaddingFlags`,
`TimeFormatTests.TimeFormat_Pad_NegativeWideAndNoFillValues`,
`TimeFormatTests.TimeFormat_Format_TwelveHourClock_MidnightAndNoon`

Asserts the text of the 32 directives, the combined specifier `%Y-%m-%dT%H:%M:%S.%L`, plain text, and the empty
specifier for the anchor in UTC (the table above); the 22 padding-flag vectors for the early-morning instant; the `Pad`
vectors; and the twelve-hour clock at midnight, noon, and 23:00.

##### Week Numbers And ISO Week Boundaries

**Tests**: `TimeFormatTests.TimeFormat_Format_WeekNumbers_SundayAndMonday`,
`TimeFormatTests.TimeFormat_Format_IsoWeek_BoundaryYears`

Asserts the seven `%U %W` vectors around the start and the end of 2012 and 2013, and the eight `%V %G %g` vectors at
the boundary years 2015 to 2025 (week 53 of 2020 and 2015, week 1 of 2025 and 2019).

##### Zones, Timestamps, And The Ends Of The Range

**Tests**: `TimeFormatTests.TimeFormat_Format_ShortYearsAndTheYearZero`,
`TimeFormatTests.TimeFormat_Format_Zone_Z_ShowsTheOffsetOfTheInstant`,
`TimeFormatTests.TimeFormat_Format_Zone_LocalFieldsFollowTheOffset`,
`TimeFormatTests.TimeFormat_Format_Timestamps_AreZoneIndependentAndFloorNegatives`

Asserts the zero-padded years `0005`, `0001`, the wall time `0000-12-31 19:00`, the last instant, and `%C` as the letter
`C`; `%Z` as `+0530`, `-0330`, `-0500`, and `-0400`; the local fields at +05:30 and on both sides of the daylight
saving change (`01:59:59.999` and `03:00:00.000`); and that `%Q` and `%s` ignore the zone and floor a negative instant.

##### Unknown Directives, Invalid Dates, And Limits

**Tests**: `TimeFormatTests.TimeFormat_Format_UnknownDirective_FollowsD3`,
`TimeFormatTests.TimeFormat_Format_InvalidDateIsInvalidDateText`, `TimeFormatTests.TimeFormat_Format_TooLong_Throws`,
`TimeFormatTests.TimeFormat_Format_FractionIsTruncatedAndFormatterIsReusable`,
`TimeFormatTests.TimeFormat_Format_IgnoresTheCurrentCulture`

Asserts the ten pinned unknown-directive texts; that NaN, both infinities, the instants one millisecond beyond the range
and 1e300 format as `Invalid Date`; that a text of exactly the limit is returned, one character more throws with the
limit, a huge literal throws early, and `Invalid Date` throws under a limit of 5; that a fraction is truncated and one
formatter works with several calendars; and that the text of the specifier
`%A %B %a %b %p %I %d %Y %j %c` is `Tuesday March Tue Mar PM 01 05 2024 065 3/5/2024, 1:04:09 PM` under the invariant,
`tr-TR`, and `de-DE` cultures.

##### Specifier Length And Expansion Are Limited

**Tests**: `TimeFormatTests.TimeFormat_Compile_SpecifierLength_IsLimited`,
`TimeFormatTests.TimeFormat_Compile_LocaleDirectiveExpansion_IsLimited`,
`TimeFormatTests.TimeFormat_Multi_LongOverride_IsLimited`

Asserts that 1,024 characters of literal text compile and format to themselves, 1,025 throw
`TimeFormatSpecifierException` naming `the limit of 1024 characters`, and a null specifier is an
`ArgumentNullException`; that 315 `%c` directives (4,095 tokens) compile and format to 6,300 characters, while 316 and
512 throw naming `the limit of 4096 pieces`; and that a multi-format override of 1,025 characters throws while the
override `Y%Y` gives `Y2024`.

##### Vega Multi-Format Chooses The Coarsest Boundary

**Tests**: `TimeFormatMultiTests.TimeFormatMulti_Default_PicksTheCoarsestExactUnit`,
`TimeFormatMultiTests.TimeFormatMulti_Default_BeforeTheEpoch`

Asserts the thirteen default labels derived above (`Tue 05`, `Mar 03`, `March`, `April`, `01 PM`, `01:45`, `:30`,
`.250`, `Wed 06`, `February`, `2024`, `July`, and `Thu 29`) and the labels `.999` and `1970` around the epoch.

##### Multi-Format Overrides And Zones

**Tests**: `TimeFormatMultiTests.TimeFormatMulti_Overrides_ReplaceEachLevel`,
`TimeFormatMultiTests.TimeFormatMulti_Overrides_DayKeyAndEmptyValues`,
`TimeFormatMultiTests.TimeFormatMulti_Zone_UsesLocalBoundaries`,
`TimeFormatMultiTests.TimeFormatMulti_IsBoundToItsCalendar`,
`TimeFormatMultiTests.TimeFormatMulti_InvalidDateAndLimit`

Asserts that each of the nine overrides replaces its own level (`ms:250` to `y:2024`); that `day` stands in for `date`
(`D05`), `date` wins over `day` (`d05`), and empty overrides and unknown keys keep the defaults (`Tue 05` and `2024`);
that the boundaries are local at +05:30 (`Tue 05`, `05:30`, `07 PM`, and `2024`); that the formatter ignores the
calendar passed to `Format`; and that NaN is `Invalid Date` and `Tue 05` needs a limit of 6.

##### Parsing Round-Trips What The Formatter Writes

**Tests**: `TimeFormatParseTests.TimeFormatParse_RoundTripsEveryDirectiveItFormats`,
`TimeFormatParseTests.TimeFormatParse_Quarter_GivesTheFirstMonthUnlessAMonthIsRead`,
`TimeFormatParseTests.TimeFormatParse_WeekDirectives_ConvertToDays`,
`TimeFormatParseTests.TimeFormatParse_IsoWeekOutOfRange_Fails`, `TimeFormatParseTests.TimeFormatParse_DayMonthYear`

Asserts that the sixteen specifiers (milliseconds, microseconds, `%c`, `%x %X`, `%Q`, `%s`, a zone, names, a two-digit
year, the three week numbering schemes with weekdays, and fields without padding) format the anchor and parse the text back
to the derived instant; the quarter rules; the three week conversions; that the ISO weeks `W00`, `W54`, and `W99` fail;
and that `05/03/2024` is March 5.

##### Names, Defaults, Pivot, And Zones In Parsing

**Tests**: `TimeFormatParseTests.TimeFormatParse_MonthNamesAndPeriods_CaseInsensitive`,
`TimeFormatParseTests.TimeFormatParse_TwelveHourWithoutDate_UsesTheDefaultYear1900`,
`TimeFormatParseTests.TimeFormatParse_TwoDigitYearPivot`, `TimeFormatParseTests.TimeFormatParse_ZoneDirective`,
`TimeFormatParseTests.TimeFormatParse_ZoneDirective_ReproducesTheUnanchoredMatch`,
`TimeFormatParseTests.TimeFormatParse_EpochDirectives`,
`TimeFormatParseTests.TimeFormatParse_LocalParseUsesTheCalendarZone`

Asserts that lower-case, upper-case, and mixed-case names and periods give 2006-01-02T13:45Z; that `01:45 PM` is
-2,208,939,300,000 (and equals the base class library's 1900-01-01T13:45Z) and 12 AM and 12 PM map to 0 and 12; the
pivot at 68 and 69; the zone forms in a calendar that must not matter; the unanchored zone match; the timestamps and the
failure of a negative number and 40 digits; and the wall times of a zone in winter, summer, a gap, and an overlap.

##### Parsing Is Strict About Text And Lenient About Ranges Like d3

**Tests**: `TimeFormatParseTests.TimeFormatParse_TrailingOrMissingTextFails`,
`TimeFormatParseTests.TimeFormatParse_EmptySpecifier_MatchesEmptyTextOnly`,
`TimeFormatParseTests.TimeFormatParse_NoRangeValidation_FollowsD3`,
`TimeFormatParseTests.TimeFormatParse_NumberWindows_FollowD3`,
`TimeFormatParseTests.TimeFormatParse_PaddingFlagsAreIgnored`,
`TimeFormatParseTests.TimeFormatParse_YearsBelow100_FollowD3`

Asserts that the 15 mismatching texts (trailing text, missing text, wrong literal case, non-ASCII digits, a partial
name, an unknown directive, and a lone percent sign) give no result; that the empty specifier matches only empty text and
gives 1900-01-01; the five roll-over results; the nine window results; that the flags `-`, `_`, and `0` are ignored; and
the four small-year results.

##### Parsing Is Bounded And Total

**Tests**: `TimeFormatParseTests.TimeFormatParse_LongInputFailsFast`,
`TimeFormatParseTests.TimeFormatParse_NeverThrows_ForArbitrarySpecifiersAndText`,
`TimeFormatParseTests.TimeFormatParse_IgnoresTheCurrentCulture`

Asserts that a literal text of exactly 256 characters is parsed, 257 fail at once, and 1,000 digits fail for `%Q`; that
every combination of 18 specifiers (including a NUL, lone percent signs, and an empty one), 20 texts (including a
surrogate pair, a lone surrogate, NUL, and digit runs), and three calendars returns, with the success flag agreeing with
the number and every success a valid instant; and that `FRIDAY, APRIL 5, 2024` and `apr  5 2024 pm` give
`1712275200000|1712318400000` under all three cultures.

##### ISO Date Strings Follow The ECMAScript Format

**Tests**: `JsDateParserTests.JsDateParser_Iso_DateOnly_IsUtc`,
`JsDateParserTests.JsDateParser_Iso_DateTimeWithoutOffset_UsesTheZone`,
`JsDateParserTests.JsDateParser_Iso_Offsets_AreHonored`,
`JsDateParserTests.JsDateParser_Iso_FractionDigits_AreTruncatedToMilliseconds`,
`JsDateParserTests.JsDateParser_Iso_ExpandedYears`, `JsDateParserTests.JsDateParser_Iso_RangeBoundaries`,
`JsDateParserTests.JsDateParser_Iso_DaysOfTheMonth`, `JsDateParserTests.JsDateParser_Iso_TimeOfDayRules`,
`JsDateParserTests.JsDateParser_Iso_Malformed_IsRejected`

Asserts the mixed rule (a date alone is UTC, a date-time without an offset is in the zone, an offset is honored), the
seven fraction vectors, the expanded years, the range ends, the ten day-of-month vectors, the time-of-day rules and the
end of the day, and the eleven malformed forms derived above.

##### Legacy Date Forms Are A Documented Subset

**Tests**: `JsDateParserTests.JsDateParser_Legacy_MonthNameForms_AreLocal`,
`JsDateParserTests.JsDateParser_Legacy_WeekdayAndZoneForms`,
`JsDateParserTests.JsDateParser_Legacy_NumericFormsAndTimes`,
`JsDateParserTests.JsDateParser_Legacy_NamesAreCaseInsensitive`

Asserts the month-name forms (local, so 22:00Z on the day before at +02:00), the long forms with a skipped weekday and a
zone (the 13:04:05Z, 15:04:05Z, 10:00Z, 04:30Z, and 15:00Z vectors), the numeric forms and the day periods, and the
case-insensitive full and abbreviated month names.

##### Non-Dates, Long Text, And The Culture Are Handled

**Tests**: `JsDateParserTests.JsDateParser_Rejects_NonDates`, `JsDateParserTests.JsDateParser_Rejects_OverLongText`,
`JsDateParserTests.JsDateParser_NeverThrows_ForArbitraryText`, `JsDateParserTests.JsDateParser_IgnoresTheCurrentCulture`

Asserts that the 25 rejected texts are not dates; that 128 characters are examined, 129 and a megabyte are rejected;
that 200 texts built deterministically from 32 fragments (including surrogates, NUL, and a 12-digit run) in three
calendars never throw and every success is a valid instant; and that the three `APRIL` texts give
`1585699200000|1585778400000|1585735200000` under the invariant, `tr-TR`, and `de-DE` cultures.
