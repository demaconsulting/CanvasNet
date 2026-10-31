### SpecParser Unit Verification Design

This document describes the unit-level verification strategy for the `SpecParser` unit in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: the internal static `SpecParser` class (the `Parse` entry
points over text and UTF-8 bytes, the structural validation in the partial `SpecParser.Validation.cs`, the compilation of
expressions and the validation of transforms and parameters in `SpecParser.Transforms.cs`, the validation of the
aggregating transforms and of the aggregate, bin, stack, and sort properties of an encoding in `SpecParser.Aggregates.cs`,
the validation of the time constructs and the compilation of the time predicates in `SpecParser.Time.cs`,
and `ParseSchemaMajorVersion`), the `PredicateSource` class that writes a predicate as expression source (the plain
comparisons and, since Phase 8, the time predicates), and the types of the sibling files that carry its result —
`SpecNode`, `SpecNodeKind`,
`NodePosition`, `SpecTree` (with `EnumerateViews`), `SpecMetadata` (the description of the document read from the
tree), `SpecExpressions` (the compiled expressions of the tree, found by their string node), and `ParameterGraph` (the
dependency order of the parameters).

#### Verification Approach

The unit is verified by parsing small, original specification strings and byte arrays and asserting on the returned
tree: node kinds and values, properties and elements in document order, the RFC 6901 JSON pointer of each node, and
the one-based line and column of the first character of each value (and of each property key). Every error path is
asserted as a `VegaLiteSpecException` (or, for oversized source, a plain `InvalidDataException`) with an exact
location, pointer, or message fragment. The unit depends only on the BCL `Utf8JsonReader`, `VegaLiteLimits`,
`VegaLiteLocation`, `VegaLiteSpecException`, and the real `ExpressionParser` unit (which compiles the expression
texts), so no mocking or stubbing is required; the internal types are
reached through the test assembly's `InternalsVisibleTo` access. The unit's interaction with the feature catalog and
the configuration resolver is verified at the subsystem tier (see _SpecModel Subsystem Verification Design_).

Expected positions are derived by hand from the source text. For example, in a specification whose second line (after
a CRLF line break) is a tab followed by `"note": "😀", "mark": "bar"`, the value of `mark` is on line 2 at column
1 + 6 + 2 + 4 + 2 + 6 + 2 + 1: a tab is one column, `"note"` is six, the colon and the space after it are two, a
string holding one astral character is four UTF-16 code units (quote, two surrogates, quote), the comma and the space
are two, `"mark"` is six, the colon and the space are two, and the value starts on the next column. The test asserts
that sum, which pins the rule that columns count UTF-16 code units and not code points or bytes. Likewise the
duplicate-key, depth-limit, and unknown-property expectations are small texts whose lines, columns, and pointers can
be counted by eye.

Unit tests reside in `SpecParserTests.cs` (which also holds the tests of `SpecTree`, `SpecMetadata`, and `SpecNode`,
as they are the parser's result types) and `SpecParserAggregatesTests.cs` (the aggregating transforms and encoding
properties) within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`SpecModel` folder, together with `SpecParserConditionTests.cs` (the conditions of a channel), `PredicateSourceTests.cs`
(the predicates written as expressions), `SpecParserTimeTests.cs` (the validation of the time constructs),
`PredicateSourceTimeTests.cs` (the time predicates written as expressions), and `ChannelDefinitionsTests.cs`; the
large-inline-data test is in `VegaLiteDocumentSpecModelTests.cs` in the same folder. The time scenarios use the
rules of the `Data` subsystem that the parser calls (`TimeUnitSpec`, `DateTimeObject`; see _Time Calendar Verification_
and _Time Format Verification_), so a unit name, a date-time key, or an interval is checked here only as far as the
parser locates the error.

Expected values of the time scenarios are counted by hand. The local time unit names of Vega-Lite are 11 single units
(`year`, `quarter`, `month`, `week`, `day`, `dayofyear`, `date`, `hours`, `minutes`, `seconds`, `milliseconds`) and 29
multi units, 40 in all, and each also exists with the `utc` prefix; the test lists them by name and asserts the count of
40 before it checks that all 80 spellings are accepted. The predicate texts follow Vega-Lite's rule that the instant of
the unit's parts of the field is compared with the instant of the value: the reference date of the missing parts is the
year 2012, January, the first, and zero for the times, because 2012-01-01 is a Sunday, so a weekday name is the
weekday number plus one in January 2012 (Monday is the 2nd, Friday the 6th), and a date-time object month counts
from 0 (month 2 is index 1, `Mar` is index 2, and the second quarter is month index 3 = 3 x (2 - 1)).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own source strings or byte arrays; no files are read. The stack-safety scenario
  parses a source nested five thousand levels deep on the test thread and asserts a typed exception

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong exception type (in particular `InvalidDataException` where a located
`VegaLiteSpecException` is documented, or the reverse), wrong pointer, line, or column, or wrong warning identifier
constitutes a failure.

#### Unit-Level Test Scenarios

##### Valid Specification Becomes A Tree With Pointers And Positions

**Tests**: `SpecParserTests.SpecParser_Parse_ValidInlineSpecRecordsJsonPointerAndLineColumn`,
`SpecParserTests.SpecParser_Parse_AllValueKindsAndBomAreHandled`,
`SpecParserTests.SpecParser_Parse_LocationsCountUtf16CodeUnits`,
`SpecParserTests.SpecNode_ToString_DescribesKindPointerAndPosition`

Asserts that an inline specification becomes a tree whose nodes carry kind, value, pointer, and one-based line and
column in document order (with array indexes in pointers, and `~` and `/` escaped as `~0` and `~1`); that a property
also remembers the position of its key and a location carries the source name; that every value kind (object,
array, string, number, boolean, null) is read, and that a byte-order mark does not shift columns whether the input is a
string or bytes; that columns count UTF-16 code units; and that a node describes itself with its kind, pointer, and
position.

##### Malformed JSON And Non-Specifications Fail At A Location

**Tests**: `SpecParserTests.SpecParser_Parse_MalformedJsonReportsFirstLocation`,
`SpecParserTests.SpecParser_Parse_NotASpecificationThrowsLocatedSpecException`,
`SpecParserTests.SpecParser_Parse_InvalidUtf8BytesThrowSpecException`,
`SpecParserTests.SpecParser_Parse_MalformedUtf8InStringIsRejectedNotReplaced`,
`SpecParserTests.SpecParser_Parse_MalformedUtf8InNameOrStructureIsRejected`,
`SpecParserTests.SpecParser_Parse_ValidMultiByteUtf8IsAccepted`,
`SpecParserTests.SpecParser_Parse_UnpairedSurrogateInStringIsRejected`

Asserts that malformed JSON (a bare word at line 2, column 11 followed by a second problem on line 3) is reported at the
first problem as `bad.json:2:11: Malformed JSON: ...` with the reader's exception as inner exception and no internal
property names in the message; that empty text, whitespace, a non-object root, trailing content, a trailing comma, a
comment, and an unterminated string each throw a located `VegaLiteSpecException` with a line and column of at least 1;
and that bytes that are not valid UTF-8 throw a `VegaLiteSpecException` rather than another type. Every kind of
malformed UTF-8 is rejected, never replaced by U+FFFD: an overlong NUL (`C0 80`), overlong three- and four-byte forms
and `C1 BF`, an encoded high and low surrogate (`ED A0 80`, `ED BF BF`), a code point above U+10FFFF (`F4 90 80 80`), a
stray continuation byte, truncated two-, three-, and four-byte sequences and `FE`, in a string value, in a property
name, and outside a string; well-formed two-, three-, and four-byte text is read unchanged with and without a byte-order
mark; and a string with an unpaired high or low surrogate is rejected at its line and column (line 2, column 10 after an
astral character that counts as two columns), while a valid surrogate pair is accepted.

##### Wrong Types And Values Are Reported At The First Offending Pointer

**Tests**: `SpecParserTests.SpecParser_Parse_WrongTypeReportsPointer`,
`SpecParserTests.SpecParser_Parse_FirstWrongValueInDocumentOrderIsReported`,
`SpecParserTests.SpecParser_Parse_ValidFormsOfCheckedPropertiesAreAccepted`,
`SpecParserTests.SpecParser_Parse_Phase4DataFormsAreAccepted`

Asserts that a known property with the wrong type or value (a theory of about one hundred and twenty rows over `$schema`,
`title`, `description`, `name`, `width`, `height`, `padding`, `autosize`, `background`, `config`, `datasets`, `mark`,
`layer`, `vconcat`, `spec`, `data`, `encoding`, `params`, and `transform`, including nested properties such as
`/encoding/y/axis/orient`, `/data/sequence/start`, `/data/sequence/step`, `/data/sequence/as`, `/data/format/delimiter`
(empty, longer than one character, a quote, a line break, or not text), `/data/format/parse` (an unknown word or a
number), `/data/format/parse/a` (an unknown type, a number, null, and, since Phase 8, a `date:` or `utc:` entry without
a format in matching quotes: `date:%Y`, `utc:%Y`, `date:'%Y`, `date:'%Y"`, `date:'`, and `date:`), and
`/data/format/property`) throws a
`VegaLiteSpecException` whose pointer names the offending value and whose location has a text position and the file;
that the same holds for the shape of every transform of the supported subset and of its predicates: a `calculate` that
is not text or has no `as` or an empty one, a filter with no field or no test, a `range` of one value, a `oneOf` that
is not an array, an `equal` that is an array, an unparsable field path, an `and`, `or`, or `not` of the wrong kind,
a `param` that names no parameter, a `fold` or `flatten` with a bad field list or too many `as` names, a negative or
non-numeric `sample`, a `pivot` without `value` or with a bad `limit`, `groupby`, `op`, or field, and a `lookup` without
`from`, with a `from` that has no `key` or `data`, with more `fields` than `as` names, with an object `default`, or with
a bad data `url`; that an expression with a syntax error, a reference to a name that is not a parameter, a cycle of
parameter expressions, a self reference, a repeated parameter name, and a reserved parameter name (`event`) fail at the
expression or name, and that the expression of a row-bound mark property or channel value with a syntax error or an
unknown name fails at its `expr`; that when a nested view comes before a bad top-level mark in the text, the first
problem in document order is
the one reported; and that an original specification that touches every checked property with a valid but unusual form
(including expression references) is accepted with no warnings, and that the data forms of the data stage - DSV
delimiters (a pipe and a tab), `parse` as `"auto"`, `null`, or a map of `number`, `boolean`, `date`, `string`, and custom
date formats (`date:'...'`, `utc:'...'`), a `property` path, sequences with a negative or a zero step and a field name,
a named dataset, and top-level datasets given as an array, as text, and as an object - are accepted without a warning
(a custom date format is valid and is applied by the data stage, not reported).

##### Duplicate Keys: Last Wins, First Position, One Warning

**Tests**: `SpecParserTests.SpecParser_Parse_DuplicateKeyLastWinsAndWarns`

Asserts that when the key `mark` is written twice (lines 2 and 4) and a nested object repeats a key, the last value
wins, the property keeps the position of the first occurrence, one `vl-duplicate-key` warning is produced per
duplicate at the later key, and a large object (which uses a name index) follows the same policy.

##### Unknown Properties Warn Once, Valid Keys Never Warn

**Tests**: `SpecParserTests.SpecParser_Parse_UnknownPropertiesWarnOnceAndValidKeysNeverWarn`,
`SpecParserTests.SpecParser_Parse_ValidTopLevelNamesDoNotWarn`

Asserts that unknown properties at the view, mark, encoding, and channel-definition levels produce one
`vl-unknown-property` warning each, at the key and not the value, that valid but unusual keys elsewhere do not warn,
and, as a regression guard for the conservative policy, that every name of the Vega-Lite 6 sets used for the check is
accepted silently (a theory over the names).

##### Limits Fail Closed

**Tests**: `SpecParserTests.SpecParser_Parse_DepthAndSourceLimitsFailClosed`,
`VegaLiteDocumentSpecModelTests.SpecParser_Parse_LargeInlineDataIsReadInOnePass`

Asserts with a depth limit of 3 that `{"a":[[1]]}` is accepted and one more level throws a located
`VegaLiteSpecException` ("deeper than the limit of 3"); that 5,000 nested `{"layer":[` levels fail closed under the
default and under the ceiling limit without a stack overflow; that source over `MaxSourceLength` (from a string, from
bytes, and the default 4,194,304 limit) throws `InvalidDataException`, while a source of exactly the limit (and with a
byte-order mark) is accepted; that an overlong string value, key, or array element is a located error naming
`MaxStringLength` at its pointer; that a number outside the double range (`1e999`) is rejected at its pointer; and
that 50,000 inline rows are read in one pass with their pointers and row count intact.

##### Expressions Are Compiled At The Supported Sites And Fail At Their Text

**Tests**: `SpecParserTests.SpecParser_Parse_ExpressionErrorsCarryTheLocationOfTheTextAndTheOffset`,
`SpecParserTests.SpecParser_Parse_CompilesTheExpressionsOfSupportedSitesOnly`

Asserts that, in a document of several lines, a calculate expression with a syntax error on line 4 throws a
`VegaLiteSpecException` located at `e.vl.json:4:20 /transform/0/calculate` (the position of the string value) whose
message starts with that location and names "expression offset 10", and that a parameter expression that reads an
undeclared name on line 8 is located at `/params/1/expr`, names `'missing'`, and gives the offset 4; and that the tree
holds the compiled expression of exactly the supported sites — a parameter `expr`, a `calculate`, a string `filter`, a
string inside a predicate composition, a row-bound mark `fill`, and a color channel `value` (six in all, found by their
string node, with the source `datum.a + p` of the calculate) — and none for a title, a `width`, a bar's `size`, or a
position `value`, and null for a null node.

##### Phase 5 Transforms And Parameters Are Accepted Without A Warning

**Tests**: `SpecParserTests.SpecParser_Parse_Phase5TransformsAndParametersAreAccepted`,
`SpecParserTests.ParameterGraph_Sort_OrdersDependenciesFirstThenDeclarationOrder`

Asserts that a specification with a bound value parameter, an expression parameter that reads it, a selection parameter,
a parameter without a value, and sixteen transforms (calculate with the viewing signal `width`, string, field, `range`,
`oneOf` with a time unit, date-object, `param`, `empty`, legacy `selection`, and composed filters, a fold, a flatten with
`as`, a sample, a pivot with a limit and `op`, two lookups (a data source and a parameter), and an aggregate) has no
warning, lists the four parameter names in order, and compiles six expressions (the calculate, the string filter, the
string inside the composition, the variable-parameter filter, and, since Phase 8, the two time predicates, the `oneOf`
with a time unit and the date-time `equal`, which are written as expressions with the time functions and compiled
under their predicate object); and that the
parameter order puts dependencies first and then declaration order (`[1, 2, 0, 3]` for the dependencies
`0 -> 2`, `2 -> 1`, and `3 -> 0` listed twice, with none unresolved), leaves a cycle and a self reference unresolved
with the index of the first unresolved parameter, and is empty for no parameters, with a null list refused.

##### Aggregating Transforms And Encoding Properties Are Validated

**Tests**: `SpecParserAggregatesTests.SpecParser_Parse_AggregateTransformShapesArePinned`,
`SpecParserAggregatesTests.SpecParser_Parse_WindowTransformShapesArePinned`,
`SpecParserAggregatesTests.SpecParser_Parse_BinParamsAndBinTransformShapesArePinned`,
`SpecParserAggregatesTests.SpecParser_Parse_StackAndImputeTransformShapesArePinned`,
`SpecParserAggregatesTests.SpecParser_Parse_EncodingAggregateBinStackSortShapesArePinned`,
`SpecParserAggregatesTests.SpecParser_Parse_ValidAggregatingTransformsAreAccepted`,
`SpecParserAggregatesTests.SpecParser_Parse_ValidEncodingAggregateBinStackSortFormsAreAccepted`

Asserts, as theories of about two hundred cases, that a wrongly shaped `aggregate`, `joinaggregate`, `window`, `bin`,
`stack`, or `impute` transform, wrong bin parameters, and a wrongly shaped `aggregate`, `bin`, `stack`, or `sort` of a
channel definition are located `VegaLiteSpecException` errors at the offending value, with the file, a text position, and
a message that starts with the file name: for example, an `aggregate` that is not an array (`/transform/0/aggregate`), an
entry that is not an object or has no `op` or no `field` where one is required, an `op` that is not text or not an
aggregate operation (`nope`, the upper-case `SUM`, and the window-only `rank`), an `as` that is empty or not text, an
unparsable `field` or `groupby` path, an exponential `param` of 0, 1.5, or text, and a `groupby` that is not an array of
paths; a window `ntile` without a `param` or with 0, 1.5, or `"4"`; and, in the same way, the wrong shapes of the other
transforms and of the encoding properties. Asserts that every valid form, including every operation name, a `sort` of
a channel shorthand, and the secondary channels, is accepted without a warning; that the sort shorthand helper accepts a
channel with an optional minus sign and nothing else; that `IsSupportedSort` accepts only the values the renderer applies
and refuses null arguments; and that `IsSupportedTransform` accepts the six aggregating transform names and `timeUnit`
(a computed transform since Phase 8) and refuses the names of later phases (`regression`, `loess`, `density`,
`quantile`, `extent`), the lower-case `timeunit`, and the empty name.

##### Sort Array Items Are Validated And Mixed Or Unknown Parts Are Warned About

**Tests**: `SpecParserAggregatesTests.SpecParser_Parse_EncodingAggregateBinStackSortShapesArePinned`,
`SpecParserAggregatesTests.SpecParser_Parse_SortArrayAndObjectWarningsAreLocated`,
`SpecParserAggregatesTests.SpecParser_TryGetSortChannel_ReadsTheShorthandAndTheEncoding`

Asserts, in the rows of the shapes theory, that an object or an array item of a `sort` array is a located error at the
item (`/encoding/x/sort/0`, `/encoding/x/sort/1`, `/encoding/x/sort/2`), that a date-time object is an error on a field
that is not of type `temporal` and on a temporal field when it is empty (at the item), has a key that is not a date-time
key (`yr`, located at the member, `/encoding/x/sort/0/yr`), or has a value that is not a string, a number, or a boolean
(located at the member, `/encoding/x/sort/0/year`), since every date-time object is now checked by the one reader that
the other sites use; that a sort array that mixes kinds of values (`[1, "b"]`,
`[null, "a"]`, `["a", true, "b"]`) is exactly one warning `vl-encoding-sort-mixed-types` at `/encoding/x/sort`, with the
file, line, and column, and is still accepted with every item kept, while arrays of strings, numbers, booleans, only
nulls, an empty array, and date-time objects on a temporal field are silent; that a key of a sort object that
Vega-Lite does not define (`bogus`) is one `vl-unknown-property` warning at `/encoding/x/sort/bogus`; and that
the channel a sort refers to is read from the shorthand (`"y"`, `"-y"`) and from `encoding`, and from no other form
(`ascending`, an operation, an array, null), with a null argument refused.

##### Scale Options Are Validated And Unknown Keys Are Warned About

**Tests**: `SpecParserTests.SpecParser_Parse_WrongTypeReportsPointer`,
`SpecParserTests.SpecParser_Parse_ColorScaleOptionsAreAcceptedAndUnknownKeysWarn`,
`SpecParserTests.SpecParser_Parse_TransformScaleParametersAreCheckedOnlyForTheirType`

Asserts, in the rows of the pointer theory, that a `domainMid`, `domainMin`, `domainMax`, `rangeMin`, or `rangeMax` that
is not a number or an expression reference (for `domainMin` and `domainMax` a date-time object is also accepted since
Phase 8, so the rows use `true` and the text `"x"`; see the date-time object scenario below), an `interpolate` that is a
number, an unknown name, an object without a
string `type` or with an unknown one or a non-numeric `gamma`, a `scheme` that is not a string or an object, an object
without a string `name` (pointer at the scheme or at the name), a `count` that is 0, -1, 2.5, or a string, an `extent`
that has one entry, a non-number, or is not an array, and `bins` that is a number are each a located error at the
offending value, that the `base` of a log scale that is 1, 0, -10, or a string, the `constant` of a symlog scale
that is 0
or -1, and the `exponent` of a pow scale that is a string are located errors at the value, and that a base of 2.5, an
exponent of -0.5, a constant of 0.01, an expression reference, and invalid parameters on a scale of another type (a linear
scale with base 1, constant 0, and exponent 3, a sqrt scale with base 1 and constant -5) are silent; and,
in the accepted form, that numbers and
expression references for the limits, the eight interpolate
names, an interpolate object with a gamma or an expression reference, a scheme name, object with count and extent, or
expression reference, and `bins` as an array or an object are silent, while an unknown key of a scheme or interpolate
object is one `vl-unknown-property` warning at the key.

##### Legend Options Are Validated And Unknown Keys Are Warned About

**Tests**: `SpecParserTests.SpecParser_Parse_WrongTypeReportsPointer`,
`SpecParserTests.SpecParser_Parse_LegendPropertiesAreAcceptedAndUnknownKeysWarn`

Asserts, in the rows of the pointer theory, that a legend `columns`, `symbolLimit`, `gradientLength`, or
`gradientThickness` that is negative, an `orient` or `titleOrient` that is not one of the defined names, and a
`direction` that is not `horizontal` or `vertical` are each a located error at the offending value; and, in the accepted
form, that a legend with a title, an inner orient, a direction, counts, a symbol type and size, a list of values, an
offset and padding, label and title limits, a format, paint, tick count, label and title font properties, a `null`
legend, and the orient `none` with its `legendX` and `legendY` is silent, while an unknown key is one
`vl-unknown-property` warning at the key.

##### Schema Version Parsing

**Tests**: `SpecParserTests.SpecParser_ParseSchemaMajorVersion_ParsesTheMajorVersion`

Asserts that the major version is read from schema URLs in the documented forms (`v5.json`, `v6.json`, `v5.17.0.json`,
`v12.json`, and an upper-case `V4.json`) and that a URL without a Vega-Lite version, with an empty version number, or
with an implausibly large one (`v99999`) has none.

##### Views, Metadata, And Data References

**Tests**: `SpecParserTests.SpecTree_EnumerateViews_ListsNestedViewsParentsFirst`,
`SpecParserTests.SpecMetadata_Read_DescribesSchemaTitleMarksDataAndParameters`,
`SpecParserTests.SpecMetadata_Read_TitleIsPlainText`,
`SpecParserTests.SpecMetadata_Read_SequenceComputesLength`,
`SpecParserTests.SpecMetadata_Read_InlineValuesTakePrecedenceOverName`

Asserts that the views of a composition are enumerated parents before children, with the encoding's `layer` property
not mistaken for a view composition; that the metadata lists the schema, title, marks in the order written (with
pointers and positions), data references by kind with row counts, URLs, and locations (views without a source add
none), and distinct parameter names in order; that the title text is plain text for each form; that a sequence's
length is computed, including degenerate sequences; and that inline values win over the other source keys of a data
object.

##### Conditional Encodings

**Tests**: `SpecParserConditionTests.SpecParser_Parse_MalformedConditionReportsThePointer`,
`SpecParserConditionTests.SpecParser_Parse_ConditionTestsAreCompiled`,
`SpecParserConditionTests.SpecParser_Parse_ConditionsThatAreNotEvaluatedAreNotCompiled`,
`SpecParserConditionTests.SpecParser_Parse_FieldConditionScaleIsValidated`,
`PredicateSourceTests.PredicateSource_TryBuild_WritesTheExpressionThatMeansTheSame`,
`PredicateSourceTests.PredicateSource_TryBuild_QuotesPathsAndStrings`,
`PredicateSourceTests.PredicateSource_TryBuild_RefusesWhatNoExpressionCanSay`,
`PredicateSourceTests.PredicateSource_TryBuild_NullPredicateThrows`,
`ChannelDefinitionsTests.ChannelDefinitions_CanCondition_ListsTheConditionalChannels`,
`ChannelDefinitionsTests.ChannelDefinitions_FieldDefinitionOf_FindsTheObjectThatCarriesTheField`,
`ChannelDefinitionsTests.ChannelDefinitions_Conditions_ListsConditionsInOrder`,
`ChannelDefinitionsTests.ChannelDefinitions_Values_ListsTheConstantsOfAChannelThatNoFieldDrives`

Assert, as a theory of ten specifications, that a condition that is not an object, has neither a `test` nor a `param`,
has a test that does not parse, refers to an undeclared parameter, or is not an expression or a predicate, a predicate
composition that is not an array, and a `value.expr` that does not compile, are each a `VegaLiteSpecException` at the
offending value (the pointer of the condition, of the item of an array, of the `test`, of the `and`, or of the `expr`)
with the file, line, and column; that the tests of the three conditions of a color are compiled to the source
`datum.q > t` (an expression text as written), `(datum["q"] >= 2 && datum["q"] <= 3)` (the range `[3, 2]` with its
endpoints put in ascending order) and `(t)` (a variable parameter), and that the `value.expr` is compiled; that the
predicate objects of a position channel, of a selection, and of a view that is not drawn are not compiled; and that a
scale option of a field
condition is validated like that of a channel (`padding: "wide"` is an error at the scale). The predicate theory
writes out, for twenty-three predicates, the exact expression text of each comparison, range (with open ends), set, validity
check, negation, composition (an empty `and` is true and an empty `or` false), expression text, and parameter; a
second test checks the bracket path of a dotted field and an escaped dot, and the escapes of the quote, the newline, and
the backslash in a text; the third lists the predicates that no expression can say (a parameter predicate, and a
composition that holds one inside an `and` or a `not`) as refused with an empty source, while the former refusals of a
time unit and of a date object are gone because those predicates are now written (see _Time Predicates As
Expressions_); and a null predicate is refused. The helper tests list the eleven channels that take a
condition (and the position, secondary, order, and tooltip channels that do not), find the field definition of a channel
(its own, the first field condition on a test, none for a parameter condition), list the conditions in order with the
parameter conditions left out of the tests, and list the constant values a legend shows (the conditions first, then the
value; none for a channel that a field drives).

##### Time Units Are Validated

**Tests**: `SpecParserTimeTests.SpecParser_TimeUnit_AcceptsEveryName`,
`SpecParserTimeTests.SpecParser_TimeUnit_UnknownUnitIsLocated`,
`SpecParserTimeTests.SpecParser_TimeUnit_ObjectForm_ValidatesMembers`,
`SpecParserTimeTests.SpecParser_TimeUnit_UnknownMemberIsAWarning`,
`SpecParserTimeTests.SpecParser_TimeUnit_Binned_ParsesAndIsReported`

Asserts that each of the 40 local time unit names of Vega-Lite and its `utc` form (80 spellings) is accepted in the
encoding of a temporal field without a warning; that an unknown unit is a located `VegaLiteSpecException` whose message
says `Unrecognized time unit 'foo'` (and `'year-week'`, and the wrongly cased `'Month'`), at the `timeUnit` of an
encoding (`/encoding/x/timeUnit`), of a condition, of a predicate (`/transform/0/filter/timeUnit`), and of the
`timeUnit` transform (`/transform/0/timeUnit`); that, as a theory of eighteen values, the object form accepts a unit
with a step, a `utc` unit, `utc: true`, a bare `maxbins`, and `binned: true`, while a step of 0, -1, or a text, a
`maxbins` of 0 or a text, a `utc` or `binned` that is not a boolean, and a `unit` that is a number or unknown are each
an error at their own member (`/step`, `/maxbins`, `/utc`, `/binned`, `/unit`), and an object with neither a `unit` nor
a `maxbins` (`{"step":2}`, `{}`), a number, and a boolean are an error at the `timeUnit` itself; that a member that
the object does not define (`bogus`) is one `vl-unknown-property` warning at its key (`/encoding/x/timeUnit/bogus`)
and the unit still applies; and that a binned unit, by the name `binnedyearmonth` or by `binned: true`, parses without
error, is reported once by the feature catalog as `vl-timeunit-binned` at `/encoding/x/timeUnit`, and gives the
encoding plan no time unit (it is ignored).

##### Date-Time Objects Are Validated Wherever They Stand

**Tests**: `SpecParserTimeTests.SpecParser_DateTimeObject_InDomainPredicateAndValues_Validated`,
`SpecParserTimeTests.SpecParser_DateTimeObject_ExpressionReferencesAndRepeatDatumsAreLeftAlone`

Asserts, as a theory of twenty specifications, that a date-time object is checked by one reader at every site that
accepts it, with the error at the offending member, so that every site reports the same error in the same way: valid
objects are accepted in the items of `scale.domain`, in `domainMin` and `domainMax`, in the `values` of an axis and of a
legend, in the `oneOf` of a predicate, and as the `datum` of a channel (a month by number or name, a quarter, `utc`);
and a month that is not one (`Foo`), a quarter that is a text, an unknown key (`yr`), a text year or hour, a day name
that is not one (`Someday`), a `utc` that is not a boolean, and an empty object are errors at the member (for example
`/encoding/x/scale/domain/0/month`, `/encoding/x/scale/domainMin/year`, `/encoding/x/axis/values/0/hours`,
`/encoding/color/legend/values/0/utc`, `/transform/0/filter/range/1/year`, `/transform/0/filter/oneOf/1/quarter`,
`/transform/0/filter/gt/quarter`, `/encoding/x/datum/month`, and `/encoding/color/condition/datum/month`) or, for the
empty object, at the object (`/encoding/x/scale/domain/0`). It also asserts that an object that holds an `expr` is an
expression reference and is not read as a date-time object, that the repeat reference `{"repeat":"column"}` as a
`datum` is not checked and is not date-time shaped, and that `{"hours":3}` is.

##### Tick And Nice Intervals

**Tests**: `SpecParserTimeTests.SpecParser_TickCountInterval_Validated`,
`SpecParserTimeTests.SpecParser_IsTimeInterval_NamesTheEightIntervals`

Asserts, as a theory of thirteen specifications, that a `tickCount` of an axis or a legend and a `scale.nice` that are
one of the eight interval names (`month`, `week`, `year`), an object with a valid `interval` and a positive `step`
(`{"interval":"day","step":2}`), a number, `true`, or an expression reference are accepted without a warning, and that
an interval that is not one (`fortnight`, `Month`, and the object with `interval: "foo"`), an object without an
`interval`, and a step that is 0, -2, or a text are located errors at the value or at the `step` or `interval` member
(for example `/encoding/x/axis/tickCount/step` and `/encoding/x/scale/nice`); and that `IsTimeInterval` accepts exactly
`millisecond`, `second`, `minute`, `hour`, `day`, `week`, `month`, and `year` (the plural `milliseconds`, `date`,
`quarter`, the `utc`-prefixed `utcmonth`, the wrongly cased `Month`, and the empty text are refused). The rule is
checked for every field type, because the parser does not know the type of the scale; whether the interval is used on a
channel without a time scale is the catalog's concern (see _FeatureCatalog Unit Verification Design_).

##### Mark Time Unit Band Settings

**Tests**: `SpecParserTimeTests.SpecParser_MarkTimeUnitBand_NonNumberIsAnErrorInTheSpecAndIgnoredInTheCallerConfig`

Asserts that a `timeUnitBandPosition` that is the text `middle` and a `timeUnitBandSize` that is `true` in a mark
definition are located errors at the value (`/mark/timeUnitBandPosition`, naming the property) while the numbers 0.25
and 0.5 are accepted without a warning, and that the caller's configuration reads the same setting leniently: on a
temporal field with the unit `yearmonth`, a bar has a band end (`HasBandEnd` of the channel of the encoding plan) by
default, it has none when the configuration sets `timeUnitBandPosition` to a text or to `true` (the value is ignored),
and has one for the number 0.25; a point has none by default, one for `config.mark.timeUnitBandPosition` of 0.25, none
for the text `"x"`, and one when its own mark definition says 0.5.

##### The Time Unit Transform

**Tests**: `SpecParserTimeTests.SpecParser_TimeUnitTransform_ValidatesFieldAndAs`

Asserts, as a theory of twelve entries, that a `timeUnit` transform with a unit name, with an object unit
(`{"unit":"month","step":3}`, `{"maxbins":10}`), and with a nested field path (`a.b`) is accepted without a warning,
and that a missing `field` or `as` is an error at the transform (`/transform/0`), an unparsable (`a[`) or numeric
`field` is an error at `/transform/0/field`, an empty or numeric `as` is an error at `/transform/0/as`, and a unit that
is null, a number, or unknown is an error at `/transform/0/timeUnit`.

##### Time Predicates Are Compiled

**Tests**: `SpecParserTimeTests.SpecParser_TimePredicates_AreCompiledOnceUnderThePredicate`,
`SpecParserTimeTests.SpecParser_TimePredicates_OneOfIsBoundedByTheDomainSizeAndTheExpressionLength`

Asserts that the predicates that are written with the time functions are compiled when the specification is parsed,
once, under the key of the predicate object, also inside a composition: of four filters, the month predicate
(`timeUnit: "month"`, `equal: 3`), the date-time leaf of an `and`, and the predicate whose value is an expression
reference (`{"expr": "1 + 1"}`) have a compiled expression, while the plain comparison `q > 1`, whether alone or next to
the date-time leaf in the `and`, has none (it is evaluated natively); and that an expression value that reads an
undeclared name (`nope + 1`) fails at the specification. It also asserts the bounds: with a `MaxScaleDomainSize` of 2, a
`oneOf` of three dates is a located error at the list (`/transform/0/filter/oneOf`, naming `MaxScaleDomainSize`) while
two are accepted without a warning, and with a `MaxExpressionLength` of 40 the expression of the two-date list is too
long and fails at the predicate (`/transform/0/filter`), so that a long list of dates cannot build an unbounded
expression.

##### Time Predicates As Expressions

**Tests**: `PredicateSourceTimeTests.PredicateSource_TimeUnit_Equal_Month`,
`PredicateSourceTimeTests.PredicateSource_TimeUnit_Range_YearMonth`,
`PredicateSourceTimeTests.PredicateSource_TimeUnit_Range_OpenEnds`,
`PredicateSourceTimeTests.PredicateSource_TimeUnit_OneOf_Day`,
`PredicateSourceTimeTests.PredicateSource_TimeUnit_Utc_Uses_utc_functions`,
`PredicateSourceTimeTests.PredicateSource_TimeUnit_Quarter_UsesMonthTimesThree`,
`PredicateSourceTimeTests.PredicateSource_DatetimeWithoutUnit_WrapsLeftInTime`,
`PredicateSourceTimeTests.PredicateSource_WeekPart_IsOmittedAndWarned`,
`PredicateSourceTimeTests.PredicateSource_StringValueWithUnit_UsesDatetimeLiteral`,
`PredicateSourceTimeTests.PredicateSource_NumberBelow10000WithSingleUnit_UsesThePart`,
`PredicateSourceTimeTests.PredicateSource_UnitsThatApplyNothingAndExpressionValuesStayPlain`,
`PredicateSourceTimeTests.PredicateSource_IsTimeAware_AndReadUnit`,
`PredicateSourceTimeTests.PredicateSource_TimePredicates_AreCultureIndependent`,
`PredicateSourceTimeTests.PredicateSource_TimeMembers_RejectNull`

Asserts the exact expression text of each time predicate, written by hand from Vega-Lite's rule (the instant of the
unit's parts of the field on the left, the instant of the value on the right, both as numbers). An equality on the
`month` unit with the value `{"month": "Mar"}` compares the instant `time(datetime(2012, month(datum["d"]), 1, 0, 0, 0,
0))` with `time(datetime(2012, 2, 1, 0, 0, 0, 0))`: the left side keeps the field's month and takes the reference
values for the other parts, and the name `Mar` is month index 2. A range on `yearmonth` from `{year: 2024, month: 2}`
to `{year: 2024, month: 4}` compares
the year and month of the field with `min` and `max` of the two ends (indexes 1 and 3), so that reversed ends also
work; an open end compares with the other end only (`time(datum["d"]) >= time(datetime(2024, 0, 1, 0, 0, 0, 0))`) and
a range with two open ends is `(true)`. A `oneOf` on `day` of `Mon` and `Fri` is `indexof([...], ...) !== -1` with the
dates January 2 and January 6 of 2012 (2012-01-01 is a Sunday), and the left side is the weekday plus one. A UTC unit
uses `utcmonth(...)` and `utc(...)`. A quarter is `(quarter(datum["d"])-1)*3` against 3 for the second quarter, and a
year with a quarter keeps the year. With no unit, a date-time value compares `time(datum["d"])` (and a dotted field
path is the bracket path `datum["a"]["b"]`, with a value that has hours, minutes, seconds, and milliseconds written in
order). The `week` and `dayofyear` parts are left out, as in Vega-Lite, so the year is kept and the rest takes the
reference values (the catalog reports the omission as `vl-predicate-timeunit-part`). A text value with a unit is a
`datetime` call when it is a date (`"2024-03-05"`) and `time(null)` when it is not (`soon`, which never matches); a
number below 10,000 with a single local unit is that unit's part (3 is month index 2, 5 is the hour 5) and any other
number is milliseconds written as it is (`1709596800000`, which is 2024-03-05T00:00Z: 19,787 days x 86,400,000 ms),
while a number with a multi-part unit is written as it is. A `valid` test with a unit tests the field itself, a
`maxbins` unit and a binned unit are ignored (`datum["d"] === 3`), and an expression value is written in parentheses.
`IsTimeAware` is true for a unit with parts, a date-time value, and a list that holds one, and false for a plain
number, a `valid` test, a `maxbins` unit, and an expression text; `ReadUnit` gives the parts `year` and `month` for
`yearmonth` and null for no unit and for a binned unit. The text is the same under the invariant, `tr-TR`, and `de-DE`
cultures (an upper-case `FRIDAY` and `SEPTEMBER` are matched without the culture, Friday is the 6th, and 1.5 and 0.5
keep their point), and a null node is an `ArgumentNullException` for `IsTimeAware` and `ReadUnit`.
