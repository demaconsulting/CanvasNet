### SpecParser

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `SpecParser` unit is the first software unit in the `SpecModel` subsystem. It comprises eight source files in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace, all `internal`:

- `SpecParser.cs` - `SpecParser` (sealed partial class): the entry points, the single-pass reader with an explicit
  frame stack, the limits, the duplicate-key policy, and the translation of reader errors
- `SpecParser.Validation.cs` - the second half of `SpecParser`: the structural validation rules and the sets of known
  property names, and the detection of unknown properties
- `SpecParser.Transforms.cs` - the third part of `SpecParser`: the compilation of expressions, the semantic validation of
  variable parameters, transforms, and predicates, the helpers that the catalog and the transforms share, and
  `ParameterGraph` (the dependency order of parameters)
- `SpecParser.Aggregates.cs` - the fourth part of `SpecParser`: the validation of the aggregating transforms and of the
  `aggregate`, `bin`, `stack`, and `sort` properties of a channel definition, and the sort helpers that the catalog shares
- `SpecParser.Time.cs` - the fifth part of `SpecParser`: the validation of the time constructs (time units, date-time
  objects, the interval forms of `tickCount` and `nice`, the `timeUnit` transform, and the time values of predicates) and
  the compilation of the time predicates
- `SpecExpressions.cs` - `SpecExpressions`: the compiled expressions of a tree, by the string node that holds the text
- `SpecNode.cs` - `SpecNode`, `SpecNodeKind`, `NodePosition`, and `SpecTree`: the immutable tree
- `SpecMetadata.cs` - `SpecMetadata` and `SpecMarkEntry`: the descriptive facts read from a tree

#### Purpose

`SpecParser` converts the UTF-8 JSON text of a Vega-Lite specification into an immutable `SpecNode` tree with precise
locations, under the resource limits of `VegaLiteLimits`, and checks the shape of the properties that the renderer
depends on, or throws a `VegaLiteSpecException` at the first problem. It exists so that every later stage reads one
trustworthy model of the specification and points its diagnostics at the exact construct. It also **compiles the
expressions** of the supported sites (once, when the document is opened) and validates the semantics of the variable
parameters and the supported transforms. `SpecMetadata` reads from the
tree the facts that the public `VegaLiteDocument` properties expose. The unit is original code; the names of the known
properties were taken from the public Vega-Lite 6 JSON schema.

The unit's behavior is specified by the `CanvasNetVegaLite-SpecModel-SpecParser-*` requirements in
`docs/reqstream/canvas-net-vega-lite/spec-model/spec-parser.yaml`.

#### Data Model

**`SpecNode`** (sealed class, immutable after the parse) has a `Kind` (`SpecNodeKind`: `Object`, `Array`, `String`,
`Number`, `Boolean`, `Null`), a `Parent`, the property `Name` (for an object member) or array `Index` (else -1), the
one-based `Line` and `Column` of the first character of its value (columns count UTF-16 code units, a tab is one
column), the `KeyLine` and `KeyColumn` of its property key (zero for array items and the root), the lazily computed
RFC 6901 `Pointer`, and the value: `StringValue`, `NumberValue` (always finite), `BoolValue`, `Items` (array elements),
and `Properties` (object member values in document order, each with its `Name`). `this[name]` returns a property or
null; `AsString`, `AsNumber`, and `AsBool` return typed values or null; `GetLocation` and `GetKeyLocation` build a
`VegaLiteLocation` with the source name; `ChildPointer` and `EscapePointerSegment` build and escape pointers (`~` becomes
`~0`, `/` becomes `~1`).

**`SpecTree`** holds the `Root`, the `SourceName`, the `Warnings` (the parser's `vl-duplicate-key` and
`vl-unknown-property` reports), the `Expressions` (a `SpecExpressions`: the `CompiledExpression` of every supported site,
found by the string node that holds the text, or by the `{"expr": ...}` object that wraps it), `Locate` and `LocateKey`,
and `EnumerateViews()`, which lists the root and the nested
`layer`, `concat`, `hconcat`, `vconcat`, and `spec` views, parents first, using an explicit work stack.

**`SpecMetadata`** (record) holds `SchemaUrl`, `SchemaMajorVersion`, `Title`, `Marks` (a `SpecMarkEntry` of type and
location per mark, in text position order), `DataReferences`, and `ParameterNames`, produced by `SpecMetadata.Read`.

**Validation rules.** A rule table describes, for each checked property of a view, title, padding, autosize, mark
definition, channel definition, scale, and axis, the accepted JSON kinds (string, number, boolean, object, array, null)
and, where it applies, the allowed values. The sets of known property names (views, mark definitions, channels, channel
definitions) are the generous union of every variant in the Vega-Lite 6 schema, so a valid key never warns; the legacy
top-level `selection` and the channel key `band` are accepted too. Validation never walks inline data rows: a `values`
array is checked for being an array and no more.

**Scale options.** After the kind rules of a scale, `ValidateScaleOptions` checks the options that need more than a kind
letter: a `scheme` object must have a string `name`, a positive whole `count`, and an `extent` of two numbers; an
`interpolate` must be one of the eight color-space names (`rgb`, `hsl`, `hsl-long`, `lab`, `hcl`, `hcl-long`,
`cubehelix`, `cubehelix-long`) or an object with that name as `type` and a numeric `gamma`; each failure is a
`VegaLiteSpecException` at the offending value, and an unknown key of a `scheme` or `interpolate` object is a
`vl-unknown-property` warning. An expression reference in place of either is left to the catalog. `ValidateTransformOptions`
checks the parameters of the transform scale types by the type of the scale only: the `base` of a `log` scale must be a
number above zero other than 1, the `exponent` of a `pow` scale finite, and the `constant` of a `symlog` scale
above zero (the
parameters of another type are ignored, as in Vega-Lite). `domainMid`,
`rangeMin`, and `rangeMax` (numbers or expression references) and `bins` (array or object) have
kind rules only; `domainMin` and `domainMax` are a number, an expression reference, or a date-time object (see the time
rules below).

**Legend options.** After the kind rules of a legend, `ValidateLegendOptions` checks what they cannot express: the counts
and lengths `columns`, `symbolLimit`, `tickCount`, `clipHeight`, `gradientLength`, and `gradientThickness` must not be
negative (a `VegaLiteSpecException` at the value), and a key that Vega-Lite does not define for a legend is a
`vl-unknown-property` warning. The kind rules carry the enumerations (`orient`, `direction`, `titleOrient`, and the
alignments) and the kinds of the paint, font, and layout properties; `legend: null` disables the legend, and `legend:
false` is a specification error.

**Data format rules (phase 4).** The `format` object of a `data` definition is validated for the parts that the data stage
reads: `delimiter` must be exactly one character that can separate fields (not empty, not longer, not a quote or a line
break, not half of a surrogate pair, and a string); `parse` must be `"auto"`, `null`, or an object; each entry of a
`parse` object must be a string that is `number`, `boolean`, `date`, `string`, or a custom date format
(`date:'%fmt'` or `utc:'%fmt'`, with the d3 time format in matching single or double quotes; the data stage applies it).
An entry that starts with `date:` or `utc:` but has no quoted format is a `VegaLiteSpecException` at the entry with a
dedicated message, and any other unknown entry value gets the general one;
`property` must be a string; a `sequence` needs numeric `start` and `stop`, a numeric `step` (zero is **not** rejected
here, because the metadata reports an unknown length for it and the data stage fails the render with a located error
instead), and a non-empty `as`. A top-level `datasets` object may hold arrays, text, and objects (the catalog reports
the last two when a `data.name` refers to them). Both rules use the static helpers `DelimitedTextParser.IsValidDelimiter`
and `ParseDirectives.TryClassify` of the `Data` subsystem, so that validation, the catalog, and the loader agree.

**Expression, parameter, and transform rules (phase 5).** The same fail-fast rule applies to a construct whose shape cannot
be what its author meant, and well-formed constructs that the renderer does not execute are left to the catalog:

- **Compiled sites.** The `expr` of every variable parameter of a view, the `calculate` and the string `filter` of every
  transform entry (also inside a predicate composition, and in every view, nested ones included), and the
  `{"expr": ...}` values of the paint properties `color`, `fill`, `stroke`, `opacity`, and
  `strokeWidth` (and `size` for the
  `point`, `circle`, and `square` marks) of the mark definition and the constant `value` of the `color`, `fill`, `stroke`,
  `opacity`, and symbol `size` channels, **of the drawn view only**, are compiled with
  `ExpressionParser.Parse` (limits from
  `VegaLiteLimits`, location of the string value) and kept in `SpecTree.Expressions`. Any other expression is not compiled.
- **References.** An identifier of a compiled expression that is not a function or a constant
  is a parameter reference, and
  it must name a parameter declared in the view that contains the expression or an ancestor view (a selection parameter
  counts); the view signals `width`, `height`, `padding`, `autosize`, `background`, and `cursor` are also accepted (and
  reported as view state when no parameter declares them). Any other name is a located error that names the offset.
- **Parameters.** A parameter needs a non-empty `name`; names are unique within one view and may not be `datum`, `event`,
  `item`, or `parent`; the references between the expressions of the variable parameters of one view must not form a cycle
  (the error is located at the `expr` of the lowest-numbered parameter that is on or depends on the cycle, found by
  `ParameterGraph.Sort`, a deterministic topological order that takes the earliest-declared ready parameter first).
- **Transforms.** Every supported transform is checked for its shape: `calculate` needs a text `as`; `filter` is an
  expression text or a predicate (an `and` or `or` array, a `not`, a `param` that names a declared parameter, a `selection`
  text, or a `field` with exactly one of `equal`, `lt`, `lte`, `gt`, `gte`, `range` (two values), `oneOf` (an array), and
  `valid` (a boolean)); `fold` and `flatten` are arrays of field paths with an `as` of the right length (one or two names
  for a fold, at most as many as the fields for a flatten); `sample` is a non-negative
  number; `pivot` has a field path, a
  `value` field, an optional `groupby` array, a non-negative `limit`, and a text `op`; `lookup` has a field path and a `from`
  with either a `param` text or a `data` object (validated like any `data`), a `key` field
  path, optional `fields`, and an
  `as` and `default` that fit them (a `default` is a scalar). Predicate nesting is bounded by `MaxJsonDepth`, so the
  recursive check cannot exhaust the stack. A transform that the renderer does not compute (`regression`,
  `loess`, `density`, `quantile`, `extent`) is accepted without a shape check; `timeUnit` is computed and checked (see the
  time rules below).

**Aggregation rules (phase 6, `SpecParser.Aggregates.cs`).** The same fail-fast rule applies to the six aggregating
transforms and to the encoding properties that aggregate, bin, stack, and sort; each error is located at the offending
value:

- **`aggregate` and `joinaggregate`** are arrays of `{op, field, as}` objects with an `op` of the operation registry of
  `AggregateMeasures` (an unknown operation is a schema violation, not an unsupported-feature report), a `field`
  that is a
  valid field path (required by every operation but `count`), a non-empty text `as`, and, for `exponential` and
  `exponentialb`, a `param` greater than 0 and at most 1; `groupby` is an array of field paths.
- **`window`** is the same with the window-only operations added, a `param` that is an integer of at least 1 for `ntile`,
  `lag`, `lead`, and `nth_value` (required for `ntile` and `nth_value`), a `sort` array of `{field, order}`, a `frame` of
  two entries that are each an integer or null, and a boolean `ignorePeers`.
- **`bin`** is true or an object of parameters and needs a `field`; `as` is a non-empty text or two of them. The parameters
  (`ValidateBinParameters`, shared with the `bin` of a channel definition) are `maxbins` of at least 2, `step` greater
  than 0, `steps` (1 to 1,024 numbers greater than 0), `minstep` not negative, `divide` (1 to 16 numbers greater than 0),
  `base` greater than 1, a numeric `anchor`, boolean `nice` and `binned`, and an `extent` that is two
  ascending numbers or
  an object with a `param` (recognized and reported by the catalog).
- **`stack`** names the field and has an optional `groupby`, an `offset` of `zero`, `center`, or `normalize`, a `sort`
  array, and an `as` of one or two names. **`impute`** needs a `key` and has an optional `groupby`, a `method` of `value`,
  `mean`, `median`, `max`, or `min`, a scalar `value`, a `frame`, and `keyvals` that is an array of scalars or a sequence
  `{start, stop, step}` with a numeric `stop` and a `step` other than 0.
- **Channel definitions** (`ValidateAggregationProperties`, called from `ValidateChannelDefinition`, so also for the `x2`,
  `y2`, and `order` definitions): `aggregate` is an operation name, or an object with exactly one of `argmax`, `argmin`
  (a field path), `exponential`, or `exponentialb` (a number greater than 0 and at most 1; a bare `argmax` or `argmin`
  text is an error because it needs its field); `bin` is true, false, null, `"binned"`, or an object of parameters;
  `stack` is `zero`, `center`, `normalize`, true, false, or null; `sort` is `ascending`, `descending`, null, an array of
  values, a channel shorthand (`"y"` or `"-y"` with a channel of `x y color fill stroke strokeWidth size shape fillOpacity
  strokeOpacity opacity text`), or an object `{field, op, order}` (the `op` any aggregate operation but `argmin` and
  `argmax`) or `{encoding, order}` (the two forms cannot be mixed); the `sort` of an `order` channel is `ascending` or
  `descending` only. Every item of a `sort` array (`ValidateSortArray`) is a string, a number, a boolean, or null: an
  object or an array item is an error at the item, because it would otherwise read as the null category and silently
  mis-sort; an object is accepted only as a date-time on a field of type `temporal`, and is then checked by
  `DateTimeObject.TryRead` like every other date-time object (an unknown key, a wrong kind of value, or an invalid month,
  quarter, or day is an error at its member; see the time rules below). An array that
  mixes kinds of values is accepted (values are matched by kind, as Vega-Lite's strict comparison does) and is one
  `vl-encoding-sort-mixed-types` warning at the array, in `SpecTree.Warnings`; a key of a sort object other than `field`,
  `op`, `order`, and `encoding` is a `vl-unknown-property` warning at the key.

**Time rules (phase 8, `SpecParser.Time.cs`).** The same fail-fast rule applies to the time constructs: a value whose shape
cannot be what its author meant is a `VegaLiteSpecException` at that exact value, and a member that Vega-Lite does not
define is a `vl-unknown-property` warning; what the renderer does with a valid construct it cannot apply is left to the
catalog (for example a binned unit).

- **Time units** (`ValidateTimeUnit`) are checked in a channel definition, in a condition, in a predicate, and in the
  `timeUnit` transform. The value is null, a unit name that `TimeUnitSpec.TryParse` accepts (the 40 local names and
  their `utc` forms, and the `binned` forms; an unknown name is `Unrecognized time unit 'foo'` at the value), or an
  object whose members are `unit` (a unit name), `maxbins` and `step` (positive numbers), and `utc` and `binned`
  (booleans), each located at its member; the object needs a `unit` or a `maxbins`, and any other member is
  `vl-unknown-property`. The mark definition rule table also gains `timeUnitBandPosition` and `timeUnitBandSize`
  (numbers or expression references).
- **Date-time objects** are checked by `DateTimeObject.TryRead` of the `Data` subsystem, at every site that accepts one,
  so every site reports the same errors at the same member: the items of `scale.domain`, `domainMin` and `domainMax`, the
  `values` of an axis or legend, the values of a predicate (`equal`, `lt`, `lte`, `gt`, `gte`, `range`, `oneOf`), the
  items of a `sort` array of a temporal field, and a `datum` that has a date-time key (`IsDateTimeShaped`). An object
  that holds an `expr` string is an expression reference and is left alone; an empty object is an error at the object;
  otherwise only the keys `year`, `quarter`, `month`, `date`, `day`, `hours`, `minutes`, `seconds`, `milliseconds`, and
  `utc` are allowed (anything else is an error at that member), a month, quarter, or day that is not one is an error at
  its member, and a value of the wrong kind is an error at its member.
- **Tick and nice intervals** (`ValidateTimeInterval`): an axis or legend `tickCount` and a scale `nice` that are a text
  must be one of the eight intervals `millisecond`, `second`, `minute`, `hour`, `day`, `week`, `month`, and `year`
  (`SpecParser.IsTimeInterval`); an object needs a valid `interval` and, when present, a positive
  `step` (another key is `vl-unknown-property`); an expression reference is left alone. The rule is checked for every
  field type because the type of the scale is not known to the parser; the catalog reports an interval on a scale that
  has none.
- **Time format limits** (`ValidateGuideTimeFormat`, called from `ValidateGuideTime`, and `ValidateFormat` for a data
  `parse` entry): the `format` text, or each text member of a `format` object, of an axis or a legend that the guide reads
  as a d3 time format - the `formatType` is `time` or `utc`, or there is no `formatType` and the channel definition (the
  parent of the guide) is `temporal` or has a `timeUnit`; a number format (`formatType: "number"`, or a field of another
  type) and a custom format type are not read as time formats here - is compiled with `TimeFormat.Compile`, and a
  `TimeFormatSpecifierException` (more than 1,024 characters, or more than 4,096 tokens after the locale directives
  expand) is an error at that format. The format of a `date:'%fmt'` or `utc:'%fmt'` parse entry is limited to 1,024
  characters (it is only scanned by the parser, never compiled). The `timeFormat` of the configuration is not read by
  the parser; `TimeLabels.Create` finds it when a legend is built.
- **The `timeUnit` transform** is supported (`IsSupportedTransform("timeUnit")`): `ValidateTimeUnitTransform` requires
  a unit, a `field` that is a valid field path, and a non-empty text `as`.
- **Predicate time values** (`ValidatePredicateTime`, called from `ValidatePredicate` for a field predicate): the
  `timeUnit` of the predicate and its date-time values are validated, and a time-aware `oneOf` list (one that
  `PredicateSource.IsTimeAware` says is written as an expression) that holds more than `MaxScaleDomainSize` values is an
  error at the array. `CompileTimePredicate` then writes every time-aware field predicate **leaf** (also inside `and`,
  `or`, and `not`) as expression source with `PredicateSource.TryBuild` and compiles it once under the key of the
  predicate object, so the 4,096-character `MaxExpressionLength` bounds a long list of date-time values and an over-long
  one fails at the predicate when the document is opened.
- **Parse directives** (`ValidateFormat`): a `date:` or `utc:` entry of `format.parse` without a format in matching
  quotes is an error at the entry (see the data format rules above).

**Limits and invariants.** The `MaxJsonDepth` bound is both the reader's depth option and the size of the frame array;
a string longer than `MaxStringLength` and a number outside the double range are errors; a root that is not an object is
an error; a duplicate key keeps the last value at the position of the first occurrence.

#### Key Methods

- `SpecParser.Parse(string text, string? sourceName, VegaLiteLimits? limits)` - requires non-null text, uses the default
  limits when none are given, ignores a leading U+FEFF, rejects text longer than `MaxSourceLength` before it is encoded
  (`InvalidDataException`), encodes it as strict UTF-8 (text with an unpaired surrogate is a located
  `VegaLiteSpecException`, never U+FFFD), and runs a single-use parser.
- `SpecParser.Parse(byte[] utf8, ...)` - the same for bytes: a UTF-8 byte-order mark is skipped, and the character count
  is checked only when the byte count already exceeds the limit (every character takes at least one byte). Invalid UTF-8
  (a stray or truncated byte, an overlong encoding, an encoded surrogate, a code point above U+10FFFF) is a malformed-JSON
  error at its position, never U+FFFD; `Open(Stream)` and `Open(path)` pass their raw bytes here.
- `Run()` / `Read()` - read with `Utf8JsonReader` (depth option set from the limits) one token at a time, push and pop
  frames on an explicit array indexed by depth (frames are reused), create nodes with their positions through an
  incremental line and column locator (a forward scan that only advances), and apply the duplicate-key policy.
  A `JsonException` is translated into a `VegaLiteSpecException` with the position the reader stopped at.
- The validation pass (in `SpecParser.Validation.cs`) visits the root view and each nested view with an explicit work
  stack in document order, so the first problem reported is the first one in the text, applies the rule tables to the
  known properties, and adds a `vl-unknown-property` warning for each unknown key of a view, mark definition,
  encoding, or channel definition. It rejects a `$schema` naming plain Vega with a clear message.
- `ParseSchemaMajorVersion(string url)` - extracts the digits (at most four) after `/vega-lite/v`, or null.
- `IsChannelName(string)` and `FindTransformOperation(SpecNode)` - small helpers shared with the catalog.
- `CompileExpression(SpecNode text)` (in `SpecParser.Transforms.cs`) - parses the string with `ExpressionParser.Parse`,
  checks the parameter references against `FindParameter` (the nearest view that declares the name), and stores the result
  in the tree's expression map. `ValidateParameterSemantics`, `ValidateTransformEntry`, `ValidateFilter`, `ValidatePredicate`,
  `ValidatePivot`, `ValidateLookup`, `CompileMarkExpressions`, and `CompileChannelExpression`
  apply the rules above from the
  validation pass; `ValidateAggregateTransform`, `ValidateWindowTransform`, `ValidateBinTransform`,
  `ValidateStackTransform`, `ValidateImputeTransform`, and `ValidateAggregationProperties` (`SpecParser.Aggregates.cs`)
  apply the phase 6 rules; `ValidateTimeUnit`, `ValidateTimeUnitObject`, `ValidateDateTimeValue`,
  `ValidateDateTimeItems`, `ValidateTimeInterval`, `ValidateScaleTime`, `ValidateGuideTime`, `ValidateGuideTimeFormat`,
  `CheckTimeFormat`, `ValidateDatum`,
  `ValidatePredicateTime`, `CompileTimePredicate`, and `ValidateTimeUnitTransform` (`SpecParser.Time.cs`) apply the
  time rules. The static helpers `IsSupportedTransform(name)` (true for the six aggregating transforms and for
  `timeUnit` too), `IsTimeInterval(name)`, `IsDateTimeShaped(node)` (also used by the catalog), `IsDrawnView(root, view)`,
  `IsViewSignal(name)`,
  `IsParameterDeclared(node, name)`, and `FindParameter(node, name)` are shared with the catalog and the transforms so that
  validation, reports, and execution agree on what is supported; the parameter index of a `params` array is built once per
  array, so a reference costs one lookup. `IsSupportedSort(sort, definition)`, `TryParseSortShorthand(text, ...)`,
  `TryGetSortChannel(sort, out channel)` (the channel a shorthand or an `encoding` refers to), and
  `IsSortChannel(name)` are shared with the catalog and the scale resolver in the same way.
- `ValidateConditions(definition)` (in `SpecParser.Transforms.cs`, called from `ValidateChannelDefinition`) checks the
  `condition` of a channel definition: an object, or an array of objects, each with a `test` or a `param` (an object
  that has neither is an error at the condition) and the properties of a value or a field definition (the scale, axis,
  and legend of a field condition are validated like those of a channel). A `test` is checked by `ValidatePredicate`
  (an expression text is compiled there, as in a filter). On a channel that accepts a condition of the drawn view
  (`ChannelDefinitions.CanCondition`, the `IsConditionSite` check) a predicate **object** is also written as expression
  source by `PredicateSource.TryBuild` and compiled once under the key of the predicate node, so the marks evaluate
  it with the expression engine under the step budget; a predicate that no expression can say (a selection, a parameter
  that is not a variable, a date-time object that is not valid) is not written. A predicate with a `timeUnit` or a
  date-time value **is** written, with the date and time functions (see `PredicateSource` below), so it is evaluated in
  the calendar of the chart. The `value.expr` of a condition is compiled like
  the one of a channel. `CompileSource(key, source, location)` is the common part of `CompileExpression`.
  `ChannelDefinitions` is the shared, pure helper that tells the conditional channels, lists the conditions, and finds
  the **field definition** of a channel (its own definition when it names a field, a datum, or an aggregate, else the
  first condition on a test that does, as Vega-Lite's `getFieldOrDatumDef`) and the constant values a legend shows
  (the first condition value, then the channel's own value).
- `PredicateSource.TryBuild(predicate, out source)` writes the predicates of Vega-Lite as expression text with the
  meaning that `isValid`, strict equality, and the comparison operators give: `equal` as `===`, `lt`, `lte`, `gt`, and
  `gte` as the comparisons, `range` as an inclusive pair (the endpoints in ascending order when both are numbers, a null
  endpoint leaving that side open), `oneOf` as an `or` of strict equalities, `valid` as `isValid` (negated for false),
  `and`, `or`, and `not` as the logical operators (an empty `and` is true, an empty `or` is false), and a variable
  parameter as its name. A field path is written as one bracket per segment (`datum["a"]["b"]`) and a text as a
  double-quoted literal with the JavaScript escapes, so no part of the specification is ever pasted into the source
  unquoted.
  **Time predicates** (`IsTimeAware`, `ReadUnit`, `AppendTime`) follow Vega-Lite's `fieldFilterExpression`: a field
  predicate that has a `timeUnit` keeping at least one part, or that compares with a date-time object, is written
  with the instant of the field on the left, `time(datetime(<parts of datum["f"]>))`, where the parts are the
  unit's (`year(datum["d"])`, `month(...)`, ...; a part the unit lacks defaults as a date-time object does: the year 2012,
  January, the first, zero for the times; a quarter is written as the month `3 * (quarter - 1)`), and the instant of
  the value on the right, `time(datetime(...))`, so that both sides are numbers. Without a unit but with a date-time
  value the left side is `time(datum["f"])`. For a UTC unit the `utc...` getters and `utc(...)` are written (Vega-Lite
  ignores the UTC flag here, marking it a TODO; this renderer honors it). The `week` and `dayofyear` parts are not
  written, as in Vega-Lite (and the catalog reports `vl-predicate-timeunit-part`). A date-time object value is its
  `DateTimeObject.ToExpression()`; a number compared with a single local unit is that unit's part when it is below
  10,000 and milliseconds since the epoch otherwise, and a text that is not a date is a unit part name (a month or
  weekday name) while one that is a date is read as a date; `{"expr": ...}`
  values are written as `(expr)`, or `time(expr)` with a unit; a text that is neither a date nor a part is `time(null)`
  (it never matches). A `range` is written as `(L >= min(a, b) && L <= max(a, b))`, which also accepts reversed ends
  (the function table has no range test), and a `oneOf` as `(indexof([...], L) !== -1)`, so that the left side is evaluated
  once. The `valid` test and comparisons with plain values stay native. `IsUnsupported` says when `TryBuild` would fail.
- `ParameterGraph.Sort(dependencies)` - orders parameters so that each follows the parameters its expression reads (the
  earliest-declared ready parameter first), and reports the lowest index that could not be
  ordered (a cycle), or -1; the same
  function gives the evaluation order of `ParameterEnvironment`.
- `SpecMetadata.Read(SpecTree)` - reads the schema, title (a string, a title object's text, or an array of lines joined
  by a line feed; null for an expression), marks, data references, and parameter names. For a data object the source is
  chosen by precedence (inline `values`, then `url`, then `sequence`, then `name`), and a `sequence` with numeric
  `start` and `stop` yields a row count.

**Preconditions and postconditions**: the text is non-null; on return the tree is complete and valid, or an exception
was thrown and nothing is returned. The parser is single-use and not thread-safe; the tree is immutable.

#### Error Handling

The parser is fail-fast: the first problem aborts the parse with one exception and no recovery is attempted. Text
longer than `MaxSourceLength` is an `InvalidDataException` (no location is meaningful). Everything else is a
`VegaLiteSpecException` with the source name, pointer, line, and column of the offending construct: malformed JSON (the
first problem), invalid UTF-8, nesting deeper than `MaxJsonDepth` (at the crossing), a string longer than
`MaxStringLength`, a number outside the double range, a root that is not an object, any content after the root object other
than whitespace (a second value is rejected at its own position), a `$schema` naming plain Vega, and a
known property of the wrong type or value (the first in document order). An expression that cannot be compiled is a
`VegaLiteSpecException` located at the string value of the expression (its pointer, line, and column) whose message ends
with the zero-based offset inside the expression text (a syntax error, a text longer than `MaxExpressionLength`, nesting
beyond `MaxExpressionDepth`, an invalid regular expression literal, or a reference to an undeclared parameter); a parameter
cycle, a repeated or reserved parameter name, a `param` of a predicate that names no parameter, and a transform or predicate
of the wrong shape are located at the offending value. A time unit that does not exist, a date-time object with an
unknown key or a month, quarter, or day that is not one, a `tickCount` or `nice` interval that is not one of the eight
(or a `step` that is not positive), a time format of more than 1,024 characters or 4,096 pieces, a `parse` entry `date:` or
`utc:` without a quoted format or with a format of more than 1,024 characters, and a time-aware `oneOf`
of more
than `MaxScaleDomainSize` values are located at the offending value or member. A null argument is a programming error and
throws `ArgumentNullException`, which is not converted. Unknown and duplicate keys are warnings, returned with the tree.

#### Dependencies

- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteSpecException`** - the public types of the
  `VegaLiteDocument` unit
- **`FeatureReport`** - the report record of the `FeatureCatalog` unit, used for the parser's own warnings
- **`DelimitedTextParser.IsValidDelimiter`** and **`ParseDirectives.TryClassify`** - the delimiter and `parse` entry
  rules of the `DelimitedTextParser` unit of the `Data` subsystem, used by the data format validation
- **`TimeUnitSpec`**, **`DateTimeObject`**, and **`JsDateParser`** - the time unit grammar, the date-time object reader
  and its expression form, and the date text reader of the `Data` subsystem (see _Time Calendar Design_ and
  _Time Format Design_), used by the time rules and by `PredicateSource`
- **`ExpressionParser`**, **`CompiledExpression`**, and **`ExpressionConstruct`** - the compilation of the text of an
  expression and the constructs it collects (the `Expressions` subsystem), and **`FieldPath`** of the `Data` subsystem
  (the validation of field names)
- **`AggregateMeasures`**, **`AggregateOp`**, and **`WindowOp`** - the registry of the operation names, which operation
  needs a field or a parameter, and which can order categories (the `Transforms` subsystem), so that validation, reports,
  and execution agree on what an operation is
- **.NET base class library** - `System.Text.Json` (`Utf8JsonReader`), `System.Text` (`UTF8Encoding`), and
  `System.Globalization` (invariant-culture parsing of the schema version)

The unit does not depend on the other two units for behavior; `FeatureCatalog` and `ConfigResolver` depend on the tree.
The dependency on the `Expressions` subsystem is one-directional: `Expressions` never reads the tree.
The dependency on the
operation registry of the `Transforms` subsystem is a read of a static table of names, not of the transforms themselves.

#### Callers

`VegaLiteDocument` is the production caller of `Parse` and `SpecMetadata.Read`. The `Data`, `Scales`, `Guides`,
`Layout`, `Marks`, and `Transforms` subsystems and the other two `SpecModel` units consume the resulting `SpecTree` and
`SpecNode` types (the `Transforms` and `Marks` subsystems also read the compiled expressions and the parameter helpers).
The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/SpecParserTests.cs` call the entry points
directly through `InternalsVisibleTo`.
