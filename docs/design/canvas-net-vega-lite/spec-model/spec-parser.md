### SpecParser

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `SpecParser` unit is the first software unit in the `SpecModel` subsystem. It comprises four source files in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace, all `internal`:

- `SpecParser.cs` - `SpecParser` (sealed partial class): the entry points, the single-pass reader with an explicit
  frame stack, the limits, the duplicate-key policy, and the translation of reader errors
- `SpecParser.Validation.cs` - the second half of `SpecParser`: the structural validation rules and the sets of known
  property names, and the detection of unknown properties
- `SpecNode.cs` - `SpecNode`, `SpecNodeKind`, `NodePosition`, and `SpecTree`: the immutable tree
- `SpecMetadata.cs` - `SpecMetadata` and `SpecMarkEntry`: the descriptive facts read from a tree

#### Purpose

`SpecParser` converts the UTF-8 JSON text of a Vega-Lite specification into an immutable `SpecNode` tree with precise
locations, under the resource limits of `VegaLiteLimits`, and checks the shape of the properties that the renderer
depends on, or throws a `VegaLiteSpecException` at the first problem. It exists so that every later stage reads one
trustworthy model of the specification and points its diagnostics at the exact construct. `SpecMetadata` reads from the
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
`vl-unknown-property` reports), `Locate` and `LocateKey`, and `EnumerateViews()`, which lists the root and the nested
`layer`, `concat`, `hconcat`, `vconcat`, and `spec` views, parents first, using an explicit work stack.

**`SpecMetadata`** (record) holds `SchemaUrl`, `SchemaMajorVersion`, `Title`, `Marks` (a `SpecMarkEntry` of type and
location per mark, in text position order), `DataReferences`, and `ParameterNames`, produced by `SpecMetadata.Read`.

**Validation rules.** A rule table describes, for each checked property of a view, title, padding, autosize, mark
definition, channel definition, scale, and axis, the accepted JSON kinds (string, number, boolean, object, array, null)
and, where it applies, the allowed values. The sets of known property names (views, mark definitions, channels, channel
definitions) are the generous union of every variant in the Vega-Lite 6 schema, so a valid key never warns; the legacy
top-level `selection` and the channel key `band` are accepted too. Validation never walks inline data rows: a `values`
array is checked for being an array and no more.

**Limits and invariants.** The `MaxJsonDepth` bound is both the reader's depth option and the size of the frame array;
a string longer than `MaxStringLength` and a number outside the double range are errors; a root that is not an object is
an error; a duplicate key keeps the last value at the position of the first occurrence.

#### Key Methods

- `SpecParser.Parse(string text, string? sourceName, VegaLiteLimits? limits)` - requires non-null text, uses the default
  limits when none are given, ignores a leading U+FEFF, rejects text longer than `MaxSourceLength` before it is encoded
  (`InvalidDataException`), encodes it as UTF-8 (unpaired surrogates become U+FFFD, deterministically), and runs a
  single-use parser.
- `SpecParser.Parse(byte[] utf8, ...)` - the same for bytes: a UTF-8 byte-order mark is skipped, and the character count
  is checked only when the byte count already exceeds the limit (every character takes at least one byte). Invalid UTF-8
  is a malformed-JSON error at its position.
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
known property of the wrong type or value (the first in document order). A null argument is a programming error and
throws `ArgumentNullException`, which is not converted. Unknown and duplicate keys are warnings, returned with the tree.

#### Dependencies

- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteSpecException`** - the public types of the
  `VegaLiteDocument` unit
- **`FeatureReport`** - the report record of the `FeatureCatalog` unit, used for the parser's own warnings
- **.NET base class library** - `System.Text.Json` (`Utf8JsonReader`), `System.Text` (`UTF8Encoding`), and
  `System.Globalization` (invariant-culture parsing of the schema version)

The unit does not depend on the other two units for behavior; `FeatureCatalog` and `ConfigResolver` depend on the tree.

#### Callers

`VegaLiteDocument` is the production caller of `Parse` and `SpecMetadata.Read`. The `Data`, `Scales`, `Guides`,
`Layout`, and `Marks` subsystems and the other two `SpecModel` units consume the resulting `SpecTree` and `SpecNode`
types. The tests in `test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/SpecParserTests.cs` call the entry points
directly through `InternalsVisibleTo`.
