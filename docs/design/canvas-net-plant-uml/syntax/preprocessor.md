### Preprocessor

![Syntax Structure](PlantUmlSyntaxView.svg)

The `Preprocessor` unit is the first software unit in the `Syntax` subsystem. It is two source files in the
`DemaConsulting.CanvasNet.PlantUml.Syntax` namespace: `Preprocessor.cs`, which defines the internal static class
`Preprocessor` and the records `DiagramBlock` and `PreprocessedSource`, and `Preprocessor.MappedText.cs`, which defines
the location map: the line table `SourceLines` and the statement `SourceStatement` with its nested `Builder`.

#### Purpose

`Preprocessor` prepares a text for detection and parsing as PlantUML does before it reads a diagram: it finds the
diagram block, removes the comments, joins continued lines, and applies the supported directives. Because every step
rewrites the text, the unit keeps, for every character it produces, the offset of the original character it came
from, so that every later diagnostic points into the text the author wrote. It enforces the text size limits before
and after the preparation. The unit's behavior is specified by the `CanvasNetPlantUml-Syntax-Preprocessor-*`
requirements in `docs/reqstream/canvas-net-plant-uml/syntax/preprocessor.yaml`.

#### Data Model

- `PreprocessedSource(Lines, Block, Reports)` - the line table of the original text, the first diagram block, and the
  pending reports in text order.
- `DiagramBlock(Tag, Location, Statements)` - the tag in lowercase (`uml`, `mindmap`, ...; null for a text without a
  tag), the location of the `@start` line (or of the start of the text), and the statements in text order, empty
  lines included (a multi-line note keeps them).
- `SourceLines` - the original text, the source name, and the offsets at which the lines start (a line ends at `\n`,
  `\r\n`, or a lone `\r`); `Locate(offset)` gives a one-based location, columns counting UTF-16 code units.
- `SourceStatement` - the statement text and the original offset of every character plus one for the end (invariant:
  one more offset than characters); `Locate(index)` and `OriginOf(index)` clamp the index to the statement.
- `Builder` - the text and offsets being built: `Append`, `AppendInserted` (every character of an inserted text mapped
  to the offset of what it replaces), `AppendFrom`, `TrimEnd`, `RemoveLast`, `EndsWithContinuation`, and `Build`,
  which trims the statement and keeps the offsets of the characters that remain.

#### Key Methods

- `Run(text, sourceName, limits)` - refuses a text longer than `SourceLengthFactor` (four) times `MaxTextSize`; builds
  the line table; frames the block; reads its lines into statements; returns the result.
- Framing - the first line that starts (after white space) with `@start` opens the block and its tag is the letters
  after it; the first later line that starts with `@end` closes it; without an `@start` line the whole text is the
  block. A further `@start` line after the end is reported once as `plantuml-multiple-diagrams`.
- Reading a line - a line whose first character is `'` is dropped; the characters between `/'` and `'/` are dropped,
  across lines; a line that ends with a single backslash is joined with the next one (the backslash removed); the
  statement is trimmed.
- Directives - a statement that starts with `!` and a letter or `$` is a directive: `!$name = value` and `?=` (also
  after `!global` or `!local`) assign a quoted text, a number, or another variable; `!define NAME [value]` defines a
  macro (a name followed by `(` has parameters and is reported); `!undef NAME` removes it; `!startsub` and `!endsub` are
  dropped silently; includes, `!theme`, `!pragma`, and every other directive are reported and dropped, and the block
  that `!if`, `!ifdef`, `!ifndef`, `!while`, `!foreach`, `!function`, `!procedure`, or `!definelong` opens is dropped
  up to its closing directive, nested blocks counted.
- Substitution - in every other statement, a `$name` that is a variable and a whole-word `NAME` that is a macro are
  replaced once (the inserted text is not scanned again), and the first built-in call `%name(` is reported and kept.
- Limits - the total length of the statements may not pass `MaxTextSize`; the open dropped blocks may not pass
  `MaxNestingDepth`.

#### Error Handling

A text or statements over the size limit throw `InvalidDataException` with a message that names
`PlantUmlLimits.MaxTextSize`. A block without an end tag throws a `PlantUmlSyntaxException` with the code
`plantuml-syntax-error` at the `@start` line, a hint, and the expected end tag. Dropped blocks nested beyond the limit
throw a `PlantUmlSyntaxException` with the code `plantuml-limit-exceeded` at the opening directive. Every unsupported
directive is a pending report, never an exception; reading never touches a file or the network.

#### Dependencies

The unit uses the root namespace's `PlantUmlLimits`, `PlantUmlLocation`, `PlantUmlSyntaxException`,
`PlantUmlDiagnosticCodes`, and `PlantUmlPendingReport`, and the .NET base class library.

#### Callers

`PlantUmlDocument` runs the preprocessor on every text it opens; `DiagramDetector` reads its result; the diagram
parsers read its statements and locate their errors with `SourceStatement`.
