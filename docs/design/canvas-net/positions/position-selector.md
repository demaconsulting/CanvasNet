### PositionSelector

![Positions Structure](PositionsView.svg)

The `PositionSelector` unit is the grammar that picks features from the positions of a render. It comprises these
source files, both `public`, in the namespace `DemaConsulting.CanvasNet.Positions`:

- `PositionSelector.cs` - `PositionSelector` (sealed class): one parsed selector, such as `all`, `node:Send`,
  `commit:*fix*`, `node:"my*odd id"`, `mark:x=Nov+color=Americas`, or `edge:@from=A+@to=B`
- `PositionSelection.cs` - `PositionSelection` (sealed class): the union of several selectors

#### Purpose

A caller asks for a few features of a picture that may have thousands. The unit gives one small, bounded, predictable
grammar for that question, shared by the command line, the library, and every renderer, so a selector means the same
everywhere: by kind, by id (a glob or an exact quoted id), or by typed predicates on the channel values, the data
fields, and the facts of a position.

The unit's behavior is specified by the `CanvasNet-Positions-PositionSelector-*` requirements (`Grammar`, `Matching`,
`DataFields`, `Limits`, and `Union`) in `docs/reqstream/canvas-net/positions/position-selector.yaml`.

#### Data Model

**Grammar** (from the XML documentation of `PositionSelector`):

```text
selector    := "all" | kind [ ":" target ]
kind        := ALPHA *( ALPHA / DIGIT / "-" )
target      := json-string        ; when the target starts with '"' (it must then be the whole target)
             | predicates         ; else, when it has '='
             | id-pattern         ; otherwise
id-pattern  := 1*( any char except '"' and '=' )  ; '*' is any run (also empty), '?' exactly one char
json-string := '"' JSON string body '"'           ; an exact id; '*' and '?' are literal
predicates  := predicate *( "+" predicate )       ; '+' separates only outside JSON strings
predicate   := name "=" value                     ; the first '=' separates; a name once per selector
name        := "@" meta | word
meta        := "id" / "view" / "from" / "to" / "index"
word        := ALPHA *( ALPHA / DIGIT / "_" )   ; a channel or a data field
value       := json-string | bare
bare        := 1*( any char except '+' and '"' )
```

**Case.** The keywords are case-insensitive: `all`, the kind, and the `@` names (the kind and the `@` names are stored
in lowercase). Ids, channel and field names, and text values are case-sensitive.

**Values.** A bare channel or field value is read like a `--param` value, with `ImageParameterValues.TryParseParameter`
of the `ImageSources` subsystem: `true`, `false`, `null`, and a finite invariant number (`3`, `-1.5`, `1e3`) are typed,
and anything else is text with one pair of surrounding single quotes removed. A JSON string is always text, so `x=3`
matches the number 3 (and 3.0) and `x="3"` matches the text `3`. A bare channel or field value that starts with `[` or
`{` is refused. A bare value of `@id`, `@view`, `@from`, or `@to` is the text as written (they hold ids and paths);
`@index` takes a whole number that is not negative.

**`PositionSelector`** fields: `Text` (as given), `Kind` (the lowercase kind, or `null` for `all`, so `IsAll` is
`Kind is null`), and the private `_idPattern` (a glob), `_exactId` (a quoted id), and `_predicates` (an array of the
private `Predicate(Name, Value)` record: an `@` name in lowercase or a channel or field name as written, and a
`PositionValue`). At most one of the id pattern, the exact id, and the predicates is set.

**`PositionSelection`** fields: `Selectors` (the parsed selectors in their first order, identical texts kept once) and
the static `All` (the selection of the single selector `all`).

**Limits**: `PositionSelector.MaxSelectorLength` = 256 UTF-16 characters, `PositionSelector.MaxPredicates` = 16, and
`PositionSelection.MaxSelectors` = 32 selector texts (counted before duplicates are removed). Both classes are immutable
and thread-safe.

#### Key Methods

- **`PositionSelector.TryParse(text, out selector, out problem)`** - bounds the work first (an empty text or one longer
  than `MaxSelectorLength` is refused before anything is read), splits the kind from the target at the first `:`,
  checks the kind word, and reads `all` (which takes no target) or a kind alone. A target is then read as an exact id
  when it starts with `"` (the closing quote is found by skipping escaped characters, the literal is decoded with
  `Utf8JsonReader`, and no text may follow it), as predicates when it contains `=`, and otherwise as an id pattern (in
  which a `"` is refused). On failure `problem` says why.
- **`PositionSelector.Parse(text)`** - `TryParse`, throwing `FormatException("The position selector '<text>' is not
  valid: <problem>.")` on failure.
- **`TryParsePredicates`** (private) - reads each predicate: the name up to the first `=` (a `+` before it means an
  empty predicate or one without `=`), checked and normalized by `TryReadName` and refused when it occurs twice; the
  value as a JSON string up to its closing quote (which must be followed by `+` or the end) or as bare text up to the
  next `+`, read by `TryReadBareValue`; and stops with a problem as soon as there are more than `MaxPredicates`.
- **`PositionSelector.Matches(position)`** - the kind gate first (the cheapest test; `all` passes every kind), then the
  exact id (ordinal equality) or the id glob, then every predicate (ANDed). A word name is a channel when it is in
  `ImagePosition.ChannelNames` or is a name `ImagePosition.Values` holds, and then compares with
  `ImagePosition.TryGetValue`; any other word is a data field and compares with `ImagePosition.TryGetData`. So a data
  field whose name is a channel is matched only through that channel (`x=1` never reads a field `x`), and a renderer
  that names no channels (a Mermaid pie slice's `value`) still compares its values. Both use typed equality, so a
  missing name never matches and `x=null` matches a name that is present and holds null; `@id` compares exactly (no
  globs); `@view`, `@from`, `@to`, and `@index` never match a position that does not have them.
- **`PositionSelector.Glob(pattern, id)`** (internal) - matches the whole id against the whole pattern, `*` being any
  run of characters (also empty) and `?` exactly one: an iterative two-pointer walk that keeps only the last `*` as a
  backtrack point (on a mismatch the star absorbs one more character and the walk resumes after it), followed by
  skipping trailing stars. It takes at most (pattern length) x (id length) steps and never recurses.
- **`PositionSelector.IsKindWord(text)`** (internal) - an ASCII letter followed by ASCII letters, digits, and `-`; also
  used by `ImagePosition` for its `Kind`.
- **`PositionSelection.Parse(texts)`** - counts every text and refuses the 33rd before parsing more, parses each
  distinct text once (ordinal comparison, first order kept), and refuses an empty list.
- **`PositionSelection.IncludesKind(kind)`** - `true` when a selector is `all` or names the kind, so that a renderer can
  skip the kinds nobody asked for; **`PositionSelection.Matches(position)`** - `true` when any selector matches.

#### Error Handling

- A text that does not follow the grammar or exceeds a limit is a `FormatException` from `Parse` (or `false` with a
  `problem` from `TryParse`) whose message names the selector and the problem: an empty selector (the hint is to use
  `all`), a selector that is too long, a kind that is not a kind word, `all` with a target, an empty target, an invalid
  or unterminated quoted id or value, text after a closing quote, a `"` in an id pattern or a bare value, an empty or
  doubled predicate, a predicate without `=`, an unknown `@` name or an invalid channel or field name, a name that
  occurs twice, an empty bare value (the hint is to write `""`), an array or object value, a bare value that the
  `--param` grammar rejects, an `@index` that is not a whole number that is not negative, and more than `MaxPredicates`
  predicates.
- `PositionSelection.Parse` throws `FormatException` for no selector or more than `MaxSelectors`, and passes on the
  `FormatException` of a selector.
- `ArgumentNullException` for a null text, list, list entry, kind, or position.

Nothing is handled locally; the caller (the `RenderCommand` unit of the tool) turns the `FormatException` into a usage
error.

#### Dependencies

`ImageParameterValues.TryParseParameter` of the `ImageSources` subsystem (the bare values); `ImagePosition` and
`PositionValue` of the `ImagePosition` unit (the matching and the typed values); and the .NET base class library
(`System.Text.Json.Utf8JsonReader`, `System.Globalization`).

#### Callers

`PositionCollector` (it evaluates every selector of its selection against every candidate), `ImagePosition` (it uses
`IsKindWord`), the `RenderCommand` unit of the `CanvasNetTool` system (`PositionSelection.Parse` of the `--positions`
texts), the `HelpTopics` unit of the tool (the `MaxSelectors`, `MaxSelectorLength`, and `MaxPredicates` tokens of its
help cards), the `CanvasNetViewer` system (the `PositionSelection` of its options), and applications.
