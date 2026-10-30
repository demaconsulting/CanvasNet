### SpecParser

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `SpecParser` unit is the first software unit in the `SpecModel` subsystem. It comprises seven source files in the
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
`vl-unknown-property` warning. An expression reference in place of either is left to the catalog. `domainMid`,
`domainMin`, `domainMax`, `rangeMin`, `rangeMax` (numbers or expression references) and `bins` (array or object) have
kind rules only.

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
(`date:'%fmt'` or `utc:'%fmt'`, which is valid here and reported by the feature catalog because it is not applied);
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
  recursive check cannot exhaust the stack. A transform that the renderer does not compute (`timeUnit`, `regression`,
  `loess`, `density`, `quantile`, `extent`) is accepted without a shape check.

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
  mis-sort; an object is accepted only as a date-time (a non-empty object of the keys `year quarter month date day hours
  minutes seconds milliseconds utc` with string, number, or boolean values) on a field of type `temporal`. An array that
  mixes kinds of values is accepted (values are matched by kind, as Vega-Lite's strict comparison does) and is one
  `vl-encoding-sort-mixed-types` warning at the array, in `SpecTree.Warnings`; a key of a sort object other than `field`,
  `op`, `order`, and `encoding` is a `vl-unknown-property` warning at the key.

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
  apply the phase 6 rules. The static helpers `IsSupportedTransform(name)` (now true for the six aggregating transforms
  too), `IsDrawnView(root, view)`, `IsViewSignal(name)`,
  `IsParameterDeclared(node, name)`, and `FindParameter(node, name)` are shared with the catalog and the transforms so that
  validation, reports, and execution agree on what is supported; the parameter index of a `params` array is built once per
  array, so a reference costs one lookup. `IsSupportedSort(sort, definition)`, `TryParseSortShorthand(text, ...)`,
  `TryGetSortChannel(sort, out channel)` (the channel a shorthand or an `encoding` refers to), and
  `IsSortChannel(name)` are shared with the catalog and the scale resolver in the same way.
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
of the wrong shape are located at the offending value. A null argument is a programming error and
throws `ArgumentNullException`, which is not converted. Unknown and duplicate keys are warnings, returned with the tree.

#### Dependencies

- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteSpecException`** - the public types of the
  `VegaLiteDocument` unit
- **`FeatureReport`** - the report record of the `FeatureCatalog` unit, used for the parser's own warnings
- **`DelimitedTextParser.IsValidDelimiter`** and **`ParseDirectives.TryClassify`** - the delimiter and `parse` entry
  rules of the `DelimitedTextParser` unit of the `Data` subsystem, used by the data format validation
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
