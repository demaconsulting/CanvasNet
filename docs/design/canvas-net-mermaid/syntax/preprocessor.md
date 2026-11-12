### Preprocessor

![Syntax Structure](MermaidSyntaxView.svg)

The `Preprocessor` unit is the first software unit in the `Syntax` subsystem. It is two source files in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: `Preprocessor.cs`, which defines the internal static class
`Preprocessor` and the records `FrontmatterData`, `DirectiveText`, `PreprocessedSource`, `FrontmatterMatch`, and
`DirectiveMatch`, and `Preprocessor.MappedText.cs`, which defines the location map: the line table `SourceLines` and
the mapped text `MappedText` with its nested `Builder`.

#### Purpose

`Preprocessor` prepares a diagram text for detection and parsing exactly as Mermaid does before it parses a diagram:
it normalizes the line endings and the quotes of HTML attributes, reads and removes the YAML frontmatter, finds and
removes the `%%{...}%%` directives, removes the `%%` comment lines, and applies Mermaid's entity encoding. Because
every one of these steps rewrites the text, the unit also keeps, for every character it produces, the offset of the
original character it came from, so that every later diagnostic points into the text the author wrote. It enforces
the text size limits before and after the preparation.

The unit's behavior is specified by the `CanvasNetMermaid-Syntax-Preprocessor-*` requirements in
`docs/reqstream/canvas-net-mermaid/syntax/preprocessor.yaml`.

#### Data Model

- `PreprocessedSource(Lines, Code, ParserText, Frontmatter, Directives)` - the result: the line table of the original
  text; the code (Mermaid's `processed.code`: the text without the frontmatter, the directives, and the comment
  lines, with its start trimmed), which the detector reads and `MaxTextSize` measures; the parser text (the code with
  the entity encoding and a final line feed); the frontmatter or null; and the directives in text order.
- `FrontmatterData(Title, DisplayMode, Config, Location)` - what Mermaid's `extractFrontMatter` keeps: the title and
  display mode converted to text with JavaScript's `String(value)` (null when missing or falsy), the `config` value as
  a `YamlNode` (null when missing or falsy), and the location of the opening `---`.
- `DirectiveText(Type, Arguments, ArgumentsAreJson, Location)` - a directive as Mermaid's `detectDirective` finds it:
  the type as written (for example `init`, `initialize`, or `wrap`), the trimmed arguments after Mermaid's replacement
  of every `'` by `"` (null when there are none), whether they are a JSON text or a single word, and the location of
  the opening `%%{`.
- `FrontmatterMatch(Indent, BodyStart, BodyEnd, End)` and `DirectiveMatch(End, Type, Arguments, ArgumentsAreJson)` -
  the positions and parts of a match of the frontmatter and directive expressions.
- `SourceLines` - the offsets at which the lines of the original text start (a line ends at `\n`, `\r\n`, or a lone
  `\r`) and the source name; `Locate(offset)` gives a one-based `MermaidLocation`, columns counting UTF-16 code units.
- `MappedText` - a text with the original offset of every character plus one for the end; `Locate(index)`,
  `OriginOf(index)`, and `Slice` keep the origins. A `Builder` appends slices of a source mapped text (with their
  origins) and inserted text (mapped to the origin of what it replaces). The memory is a small multiple of the text.

Every type is immutable (the builder is used by one transformation on one thread), so the unit is safe for
concurrent use.

#### Key Methods

- `Run(string source, string? sourceName, MermaidLimits limits)` - the whole preparation: the check of the original
  length against `SourceLengthFactor` (4) times `MaxTextSize`, `CleanupText`, `MatchFrontmatter` and the reading of
  the frontmatter, `DetectDirectives`, `RemoveDirectives`, `CleanupComments`, the trim of the start, the check of the
  code length against `MaxTextSize`, and `EncodeEntities` followed by the final line feed.
- `CleanupText(MappedText)` - Mermaid's `cleanupText`: `\r\n` and `\r` become `\n` (the inserted `\n` keeps the
  origin of the `\r`), and in every tag `<name ...>` the attribute values `="..."` become `='...'`.
- `MatchFrontmatter(string)` - Mermaid's `frontMatterRegex` as a scanner: an indent of white space other than line
  breaks, exactly `---`, white space ending in a line break, the lazy body, and a closing line with the same indent.
  The backtracking cases of the expression are reproduced: the first closing line wins, an empty body closes only by
  backtracking to an earlier line break of the opening run, and a block without its closing line is not frontmatter.
- `DetectDirectives(MappedText)` - Mermaid's `detectDirective`: after removing every line rest that starts with
  `%%}%%`, every match of the directive expression gives a `DirectiveText`.
- `TryMatchDirective(string, int, out DirectiveMatch)` - Mermaid's `directiveRegex` at a position: `%%{`, a type with
  or without a colon, then a word argument or free-form arguments up to `}%%`, the closing `}%%` being optional.
- `RemoveDirectives(MappedText)` and `CleanupComments(MappedText)` - Mermaid's `removeDirectives` and
  `cleanupComments` (a comment line starts, after white space, with `%%` that is not followed by `{` and has at least
  one more character; it is removed with its line feed and the blank lines before it, and a bare `%%` stays).
- `EncodeEntities(MappedText)` - Mermaid's `encodeEntities`: the last `;` of a `style` or `classDef` line with a
  color (`:` followed by `#` without white space) is dropped, then `#name;` becomes `ﬂ°name¶ß` and `#123;` becomes
  `ﬂ°°123¶ß` with Mermaid's private markers.
- `TrimStart(MappedText)` and `TrimJs(string)` - JavaScript's `trimStart` and `trim` with JavaScript's white space;
  `TrimJs` is also used by the parsers and the model of later subsystems.

**Preconditions and postconditions**: `source` and `limits` must not be null. The code and the parser text only
remove or encode characters of the cleaned text, every position of them maps to a valid location of the original
text, and the parser text ends with a line feed. The frontmatter is read with `YamlSubsetParser` and
`MaxYamlDepth`; only a mapping keeps anything.

#### Error Handling

- A null argument is an `ArgumentNullException`.
- An original text longer than four times `MaxTextSize`, or code longer than `MaxTextSize`, is an
  `InvalidDataException` whose message names `MermaidLimits.MaxTextSize`.
- A frontmatter that is not valid YAML, uses a refused construct, or nests too deeply is the located
  `MermaidSyntaxException` of `YamlSubsetParser` (`mermaid-yaml-invalid`, `mermaid-yaml-unsupported`, or
  `mermaid-limit-exceeded`), located in the original text because the body is passed with its own location map.
- A frontmatter that is not closed is not frontmatter; the code then starts with `---`, which the detector reports
  with Mermaid's own message.

The unit performs no I/O.

#### Dependencies

- **YamlSubsetParser** (same subsystem) - reads the frontmatter body
- **SourceScanner** (same subsystem) - the JavaScript character classes of `ScanPatterns`
- **Root namespace** - `MermaidLimits`, `MermaidLocation`, and `MermaidSyntaxException`
- **.NET base class library** - `System.Text.StringBuilder` and `System.Globalization`

#### Callers

`MermaidDocument` runs `Preprocessor.Run` on every text it opens and uses `SourceLengthFactor` to bound the bytes it
reads from a stream; `DiagramDetector` reuses `MatchFrontmatter`, `RemoveDirectives`, and `TrimJs`; the
`Configuration` subsystem reads the frontmatter and the directives of the `PreprocessedSource`; the `Flowchart`
subsystem lexes `ParserText` and uses `TrimJs`; the `Rendering` subsystem uses `TrimJs`. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/PreprocessorTests.cs` call the unit directly through
`InternalsVisibleTo`.

#### Known Differences From Mermaid

- **Two size checks.** Mermaid checks only `maxTextSize` against the code. The unit also refuses an original text
  longer than four times `MaxTextSize` before any work, so that a text of huge frontmatter, directives, or comments
  cannot make the preparation itself expensive. Both checks are an `InvalidDataException` rather than Mermaid's
  error diagram.
- **Scanners instead of regular expressions.** The expressions of Mermaid's preprocessing are hand-written scanners
  with the same matches, linear in the text; the quadratic worst cases of the `%%}%%` line removal and of the comment
  expressions do not occur.
