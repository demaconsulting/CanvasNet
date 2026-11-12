### YamlSubsetParser

![Syntax Structure](MermaidSyntaxView.svg)

The `YamlSubsetParser` unit is the third software unit in the `Syntax` subsystem. It is two source files in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace that define the internal sealed partial class `YamlSubsetParser`:
`YamlSubsetParser.cs` holds the loader, and `YamlSubsetParser.Values.cs` holds the value tree (the abstract record
`YamlNode` and the records `YamlScalar`, `YamlSequence`, `YamlEntry`, and `YamlMapping`), the implicit scalar types of
the JSON schema, and JavaScript's number formatting.

#### Purpose

`YamlSubsetParser` reads the YAML that Mermaid reads with js-yaml's `load(text, { schema: JSON_SCHEMA })`: the
frontmatter of a diagram and the `@{ ... }` shape data of nodes and edges. It is a port of the js-yaml loader that
Mermaid bundles, so documents give the values Mermaid sees and invalid YAML fails with js-yaml's descriptions. It
refuses the constructs that no diagram needs and that could expand a small text into a huge value, and it bounds
the nesting of collections so that untrusted YAML cannot exhaust the stack.

The unit's behavior is specified by the `CanvasNetMermaid-Syntax-YamlSubsetParser-*` requirements in
`docs/reqstream/canvas-net-mermaid/syntax/yaml-subset-parser.yaml`.

#### Data Model

- `YamlNode(Location)` - a value with the location of its first character in the original text, its JavaScript
  truthiness (`IsTruthy`), and its JavaScript `String(value)` conversion (`ToJsString`).
- `YamlScalar(Value, Location)` - `null`, a `bool`, a `double`, or a `string`; null, false, zero, not a number, and
  the empty string are falsy; numbers convert to text in JavaScript's shortest form.
- `YamlSequence(Items, Location)` - an array; truthy; converts to its items joined with commas (null items empty).
- `YamlMapping(Entries, Location)` and `YamlEntry(Key, Value, KeyLocation)` - an object with unique string keys in
  document order; `Get(key)` returns a value or null; truthy; converts to `[object Object]`.
- The reader's state - the prepared input (a final line break added, a byte-order mark removed, and a terminating NUL
  appended, as js-yaml's `loadDocuments` does), the position, line, line start, indentation, the number of open
  collections, and the scalar being read. An instance reads one text on one thread; the nodes are immutable.

#### Key Methods

- `Parse(string text, Func<int, MermaidLocation> locate, int maxDepth)` - reads a text that holds at most one
  document and returns its value, or null for no document or an empty one; `locate` maps a position of the text to
  the original text (the frontmatter body and the shape data are passed with their own location maps).
- The loader functions, ported one to one from js-yaml's `loader.js`: `ReadDocuments` and `ReadDocument` (an explicit
  `---` start and a `...` end are allowed), `ComposeNode`, `ReadBlockSequence`, `ReadBlockMapping`,
  `ReadFlowCollection`, `ReadBlockScalar` (literal and folded, with chomping and indentation indicators),
  `ReadPlainScalar`, `ReadSingleQuotedScalar`, `ReadDoubleQuotedScalar` with `ReadEscape`, `SkipSeparationSpace`
  (comments), the line folding, and `StoreEntry` (duplicate keys); keys are converted to strings as JavaScript object
  keys, and a sequence key that contains a sequence is refused as js-yaml refuses it.
- `ResolvePlain(string)` - the implicit types of the JSON schema in js-yaml's order: null (`~`, `null`, `Null`, and
  `NULL`), boolean (`true`/`True`/`TRUE`, `false`/`False`/`FALSE`), integer (decimal digits, or `0b`, `0o`, and `0x`
  digits, with an optional sign; a leading zero is decimal), and float (a decimal number with an optional fraction
  and exponent, and `.inf` and `.nan` in their three spellings); a number that overflows to infinity, and any other
  text, stays a string. An empty value is null.
- `FormatJsNumber(double)` - ECMAScript's `Number::toString`: the shortest round-trip digits, positional for decimal
  exponents from -6 to 21 and exponential otherwise, `NaN`, `Infinity`, and `-Infinity`.

**Preconditions and postconditions**: `text` and `locate` must not be null. The result has the shape of the
JavaScript value js-yaml gives Mermaid; every node and key carries a location in the original text.

#### Error Handling

All errors are `MermaidSyntaxException`s located through `locate`:

- **Invalid YAML** - `mermaid-yaml-invalid`, with the message "the YAML is not valid: " followed by js-yaml's own
  description (for example "duplicated mapping key", "bad indentation of a mapping entry", "tab characters must not
  be used in indentation", "unknown escape sequence", "null byte is not allowed in input", or "expected a single
  document in the stream, but found more") and a hint about indentation and quoting.
- **Refused constructs** - `mermaid-yaml-unsupported` for anchors (`&a`), aliases (`*a`), tags (`!tag`), explicit
  keys (`? key`), and directives (`%YAML`, `%TAG`), at the construct, with the hint to write the value out in full.
- **Depth** - `mermaid-limit-exceeded` when a collection opens deeper than `maxDepth` (`MermaidLimits.MaxYamlDepth`,
  32 by default and at most 256), at the collection, with a message that names `MermaidLimits.MaxYamlDepth`. The
  recursion of the loader is bounded by this limit, so no input can overflow the stack.

Every step of the loader consumes input, so the time is linear in the text. The unit performs no I/O.

#### Dependencies

- **Root namespace** - `MermaidLocation`, `MermaidSyntaxException`, and `MermaidDiagnosticCodes`
- **.NET base class library** - `System.Text.StringBuilder`, `System.Numerics`, and `System.Globalization`

The unit has no dependency on another unit of the system.

#### Callers

`Preprocessor` reads the frontmatter body; `FlowchartModel` of the `Flowchart` subsystem reads the shape data of
nodes and edges; the `Configuration` subsystem and the `Rendering` subsystem (`LayoutData`) read the resulting
`YamlNode` values. The tests in `test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/YamlSubsetParserTests.cs` call the
unit directly through `InternalsVisibleTo`; their expected values were produced with the js-yaml that Mermaid bundles.

#### Known Differences From Mermaid

- **Refused constructs.** js-yaml accepts anchors, aliases, tags, explicit keys, and directives; the unit refuses
  them with `mermaid-yaml-unsupported`, because aliases can expand a small text into a huge value and no diagram
  needs any of them. A merge key `<<` is an ordinary key, which is faithful to the JSON schema, where merge is not a
  type; merging is therefore impossible because aliases are refused.
- **Several documents** are an error with js-yaml's own message, as in js-yaml's `load`, reported as
  `mermaid-yaml-invalid`.
- **Depth limit.** Collections nested deeper than `MaxYamlDepth` are a `MermaidSyntaxException` with
  `mermaid-limit-exceeded`, raised when the document is opened (not a render-time error); js-yaml has no such limit.
