### TimeLabels

![Guides Structure](VegaLiteGuidesView.svg)

The `TimeLabels` unit is the sixth software unit in the `Guides` subsystem. It is one source file, `TimeLabels.cs`, in
the `DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines one `internal` static class, `TimeLabels`, with its
nested `Options` record. The file carries the attribution header comment for the d3-time, d3-time-format, d3-scale, Vega,
and Vega-Lite behavior it follows. The unit is shared by `AxisBuilder` and by the temporal part of `LegendBuilder`
(`LegendBuilder.Time.cs`, described below), which is a partial file of the `LegendBuilder` unit and not a unit of its own.

#### Purpose

`TimeLabels` holds what the axes and the legends (and, since phase 9, the text mark) of temporal fields share: which
**format** a label of a date gets (plan
decision P8-D7), how the **`tickCount`** of a time scale is read when it names a calendar interval, the Vega-Lite rule
for the **default tick count** of a time axis, and how an explicit list of dates (`axis.values`, `legend.values`) is
read as instants. It exists so that an axis, a legend, and a text label format the same date the same way, and so that
the rules that
Vega-Lite gives to time guides are written once, apart from the geometry of either guide.

The unit's behavior is specified by the `CanvasNetVegaLite-Guides-TimeLabels-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/time-labels.yaml`. The rules are those of Vega-Lite and Vega, re-implemented
from their public description (see _CanvasNetVegaLite System Design_, Third-Party Provenance and Attribution).

#### Data Model

The class is static and has no state. **`TimeLabels.Options`** (sealed record) carries what decides the label format of
one guide: `Format` (the `format` of the guide, a text or an object, or null), `FormatType` (`number`, `time`, `utc`, or
null), `Unit` (the `TimeUnitSpec` of the field, or null), `DefaultSpecifier` (the d3 time format the guide uses when it
has no other, a legend's `config.timeFormat`, or null for Vega's multi-format), `Calendar` (the calendar of the scale),
`MaxLength` (the longest label allowed), `Owner` (`axis` or `legend`, for messages), `Location` (the location of the
guide), and `FormatLocation` (the location of the `format` property).

**Label format resolution** (P8-D7). The label of an instant is made by the first rule that applies:

1. `formatType: "number"` formats the **milliseconds as a number** with the d3 number format given by `format` (the
   JavaScript text of the number when there is none).
2. A `format` that is a text is a **d3 time format** in the calendar of the scale (`formatType: "utc"` forces UTC, and
   `"time"` forces a time format on any axis, also a quantitative one; a field that is not temporal has no calendar of
   its own, so `AxisBuilder` gives `Options.Calendar` the calendar of the chart, the time zone of the render options,
   for `"time"`).
3. A `format` that is an object is **Vega's multi-format** with its members as the overrides of the levels
   (`milliseconds`, `seconds`, `minutes`, `hours`, `date` or `day`, `week`, `month`, `quarter`, `year`; a member that is
   not a text is ignored).
4. A field with a time unit uses the **specifier of the unit** (`%b %Y` for `yearmonth`; see _TimeCalendar_).
5. Otherwise the guide's default: `DefaultSpecifier` when it has one, which is how a legend uses `config.timeFormat`
   (`%b %d, %Y` by default), and Vega's multi-format otherwise, which is what an axis uses (axes ignore
   `config.timeFormat`).

A `formatType` that the renderer does not evaluate (any name but `number`, `time`, and `utc`; `IsKnownFormatType` is
`FeatureCatalog.IsEvaluatedFormatType`) is a custom format type: it is not passed in, and the feature catalog reports
`vl-format-type-custom`. All names are the English d3 locale and all digits are invariant ASCII; an invalid date
formats as `Invalid Date`.

**The interval `tickCount` request.** `IntervalRequest(tickCount)` reads the `tickCount` of a time axis or legend when
it names a calendar interval, as Vega's three forms allow: a text that is an interval name (`day`, `week`, `utcmonth`,
`quarter`, ...) or an object `{interval, step}`, through `TimeTickRequest.TryFromInterval` (see _Ticks_). It returns null
when the property is absent, a number (then the count is read as a number by the caller), or not an interval, and the
caller falls back to the default count.

**The default tick count rule.** `UsesVegaDefaultCount(unit)` is Vega-Lite's rule that leaves the default tick count of
a time axis to Vega: true for a field whose time unit keeps exactly one part and that part is a month, an hour
(`hours`), a day, or a quarter, so the default is Vega's **10**; otherwise the default is Vega-Lite's one tick for every
40 pixels
(`ceil(extent / 40)`, `Ticks.DefaultAxisTickCount`). `AxisBuilder` then takes `tickCount` when it is a number, else this
default (or the number of `values` when there are some), and limits the count to the number of minimum steps that fit in
the domain plus one, where the minimum step is `tickMinStep` or, by default, the length of one smallest part of the time
unit (`TimeUnitSpec.DurationMilliseconds`, which makes `yearmonth` bars show at most one tick per month).

**The `values` instant reader.** `ReadInstant(node, calendar)` reads an entry of an explicit list of dates: a number is
milliseconds since the epoch (truncated), a text is a date text read like `Date.parse` (`JsDateParser`, in the calendar
of the scale), and an object is a Vega-Lite date-time object read by `DateTimeObject` (in the calendar unless it says
`utc`). The result is a valid instant or NaN. A discrete scale over the dates of a time unit reads a number or a text
by the unit rule (`DateTimeObject.FromScalar`: the text `Mar` against `month` is March 2012) before this reader is used.

**Bounded labels** (`CreateBounded`, phase 9). The text mark labels its rows with the same format rules but owns the
budget of the whole mark (it limits every label and all the labels together by sixteen times `MaxStringLength`; see
_MarkBuilder_), so `CreateBounded(Options)` returns a function of an instant **and a limit** that does **not keep a
running total**: the caller passes what is left of its own budget as the limit of each label, and the function returns the
label, or **null when the label would be longer than that limit** (a time format stops as soon as it would write more, so
a huge specifier cannot allocate more; a number label is checked after it is made; a negative limit counts as 0). It never
throws for the length, and a refused label changes nothing for the next call. `Options.MaxLength` is not used. The format
rules above are the same, and the format errors are the located specification errors of `Create`, thrown when the function
is created. `Create` is unchanged; the compile of the d3 number format of `formatType: "number"` is shared by the two
through a private helper.

**Use by the legend** (`LegendBuilder.Time.cs`). A legend of a temporal field is drawn from the resolved scale
(`ResolvedScale.IsTemporalField`, `Calendar`, `TimeUnit`): a gradient legend of a temporal color, whose sequential scale
runs over milliseconds, takes the **time ticks** of its domain (`Ticks.TimeTicks`, the calendar interval nearest to the
span over the tick count, or the interval named by `tickCount`) and the gradient samples its colors at the time ticks
for a count of 15; a size or an opacity on a time scale is a symbol legend over time ticks; and the dates of a field
with a time unit are the symbol labels of a discrete legend. Its labels come from `TimeLabels.Create` with the
`config.timeFormat` default, `TimeLabels.IntervalRequest` reads its `tickCount`, and `TimeLabels.ReadInstant` reads its
`values` (a discrete scale gives a date, a continuous one milliseconds). The length of every label is charged to the
evaluation steps of the build (`MaxEvaluationSteps`) as it is made. `AxisBuilder` uses the unit the same way for the
ticks of a time or UTC scale and for the labels of the dates of a time unit, with the multi-format default.

**Limits** (every loop or allocation of the unit; the callers poll and charge):

- _One label_: at most `MaxStringLength` characters (the formatter stops as soon as it would write more; a number label
  is checked after it is made); a longer one is a located `VegaLiteRenderException` that contains `the limit of`.
- _All the labels of a guide_: the function that `Create` returns keeps a running total of the characters of the labels
  it has returned and gives the formatter what is left of `MaxStringLength` as its limit, so the labels of one axis or
  one legend are together at most `MaxStringLength` characters, charged before the text is built, measured, or kept
  (a thousand ticks of a hostile format cannot add up to a gigabyte). The overflow is a located
  `VegaLiteRenderException` whose message is "The labels of the axis are together longer than the limit of N characters
  (VegaLiteLimits.MaxStringLength)"; a refused label is not counted.
- _A specifier_: at most 1,024 characters and 4,096 tokens (see _TimeFormat_); a longer one fails when `Create` compiles
  it, with a located `VegaLiteSpecException` at the `format`.
- _Ticks of a guide_: at most `MaxTicks`, checked by the caller before any label is made; `TimeInterval.Range` polls the
  token every 1,024 items.
- _`IntervalRequest` and `ReadInstant`_: O(1) per entry, with date text of at most 128 characters (the `JsDateParser`
  bound); the caller bounds the `values` list by `MaxScaleDomainSize`.
- _Label text of a legend_: its length is also charged through `ExpressionFunctionContext.ChargeSize` (one step per 64
  characters).
- _`CreateBounded`_: one label is at most the limit that the caller passes (the formatter stops at it and the function
  gives null), and the unit keeps no total: the caller's budget bounds all the labels.

**Determinism.** Nothing here reads the clock, the host time zone, or the host culture: the zone is the calendar of the
chart and the names are the English d3 locale. The same specification and data give the same labels on every call and
machine, and under any culture.

#### Key Methods

- `TimeLabels.Create(Options)` - builds the function that turns an instant into its label by the resolution above; the
  function throws a located `VegaLiteRenderException` when a label is longer than the limit or the labels of the guide
  are together longer than it.
- `TimeLabels.CreateBounded(Options)` - builds the function that turns an instant and a limit into its label by the same
  resolution, without a running total: it returns null when the label is longer than the limit it is given (see
  _Bounded labels_); it throws the same located `VegaLiteSpecException` as `Create` for a format that is not valid.
- `TimeLabels.IntervalRequest(SpecNode?)` - the interval request of a `tickCount`, or null.
- `TimeLabels.UsesVegaDefaultCount(TimeUnitSpec?)` - whether the default tick count is Vega's ten.
- `TimeLabels.ReadInstant(SpecNode, TimeCalendar)` - the instant of an entry of `values`, or NaN.
- `TimeLabels.IsKnownFormatType(string?)` - whether a `formatType` is evaluated.

**Preconditions and postconditions**: `Options`, the node (for `ReadInstant`), and the calendar are non-null; `Create`
captures its formatter once (a compiled `TimeFormatter`, or the d3 number format), so the returned function is cheap per
label and deterministic; the function is not thread safe (it holds the running total of one guide, which is built on one
thread).

#### Error Handling

A label that is too long is a `VegaLiteRenderException` located at the guide (message: "A label of the axis is longer
than the limit of N characters (VegaLiteLimits.MaxStringLength)"), and so are labels that are too long together (message
above). A `format` of `formatType: "number"` that is not a d3
number format specifier is a `VegaLiteSpecException` located at the `format` value, as for any number format; a time
format fails only for a specifier that is longer than 1,024 characters or expands to more than 4,096 tokens
(`TimeFormatSpecifierException` of `TimeFormat.Compile`, mapped to a `VegaLiteSpecException` at the `format` with the
message "The axis time format is not valid: ..."), because d3 copies an unknown directive. A `tickCount` that is not an
interval, and a `values` entry that is not a date, are not errors of this unit (the interval request is null and the
instant is NaN; `SpecParser` rejected malformed intervals and date-time objects when the document was opened).
A null argument is an `ArgumentNullException`. The unit performs no I/O.

**Documented deviations from Vega-Lite** (each pinned by a test):

1. **`formatType: "utc"`** is accepted as well as `number` and `time`.
2. **A `time` format type on a non-temporal axis** uses the calendar of the chart (the time zone of the render options),
   and `utc` uses UTC; the numbers are read as milliseconds since the epoch.
3. **`labelExpr` and `labelBound`** are not evaluated for any axis or legend (the feature catalog reports
   `vl-expr-label`), and a custom format type is reported, not evaluated.

#### Dependencies

- **`TimeFormat`**, **`TimeFormatter`**, and **`TimeFormatTooLongException`** - the compiled formats and the multi-format
  (the `TimeFormat` unit)
- **`TimeCalendar`**, **`TimeUnitSpec`**, and **`DateTimeObject`** - the calendar, the unit specifier, and the date-time
  objects (the `TimeCalendar` unit), and **`JsDateParser`** for date text
- **`TimeTickRequest`**, **`NumberFormat`** - the interval request of the ticks (the `Ticks` unit) and the number
  labels of `formatType: "number"` (the `NumberFormat` unit)
- **`FeatureCatalog`** - `IsEvaluatedFormatType` (the `FeatureCatalog` unit); **`SpecNode`** and **`ConfigNode`** - the
  specification and configuration values read
- **`JsNumber`**, **`VegaLiteLimits`**, **`VegaLiteLocation`**, **`VegaLiteSpecException`**, and
  **`VegaLiteRenderException`** - the number text and the public types of the `VegaLiteDocument` unit

#### Callers

`AxisBuilder` calls `Create` for the labels of a time or UTC scale, of the dates of a time unit, and of an axis with a
`formatType` of `time` or `utc`, `IntervalRequest` and `UsesVegaDefaultCount` for the ticks, and `ReadInstant` for
`values`. `LegendBuilder` (`LegendBuilder.Time.cs`) calls `Create` through its `TimeLabelFunction`, `IntervalRequest`
through `TimeTickSet`, and `ReadInstant` through `TemporalValue`. The text mark of `MarkBuilder` (`MarkBuilder.Text.cs`)
calls `CreateBounded` for the label of a temporal value (the time formatter of its labels). The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/` (`TimeLabelsTests.cs`, including
`TimeLabels_CreateBounded_ReturnsNullWhenTheLabelIsLongerThanTheLimit`, with `AxisBuilderTimeTests.cs` and
`LegendBuilderTimeTests.cs` for the guides that use it) call the types directly through `InternalsVisibleTo`, including
under non-English cultures.
