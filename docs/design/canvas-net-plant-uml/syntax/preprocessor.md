### Preprocessor

![Syntax Structure](PlantUmlSyntaxView.svg)

The `Preprocessor` unit is the first software unit in the `Syntax` subsystem. It is three source files in the
`DemaConsulting.CanvasNet.PlantUml.Syntax` namespace: `Preprocessor.cs`, which defines the internal static class
`Preprocessor` and the records `DiagramBlock` and `PreprocessedSource`, `Preprocessor.Includes.cs`, which defines the
include directives of the preprocessor, the record `IncludedFile`, and the class `IncludeResolver`, and
`Preprocessor.MappedText.cs`, which defines the location map: the line table `SourceLines` and the statement
`SourceStatement` with its nested `Builder`.

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
- `IncludedFile(Lines, File)` - an included file: its line table (whose source name is the display name of the file,
  so its statements are located in it) and the `FileReference` the core engine returned; `Identity` is its real path.
- `IncludeResolver` - the include files of one open call: the core `FileReferenceResolver` (base directory, referrer,
  policy, and a byte limit of three bytes per character of the text limit), the files read so far, and the identity of
  the diagram's own file.

#### Key Methods

- `Run(text, sourceName, limits, includes)` - refuses a text longer than `SourceLengthFactor` (four) times
  `MaxTextSize`; builds the line table; frames the block; reads its lines into statements (with the include resolver
  of the call, or none); returns the result.
- Framing - the first line that starts (after white space) with `@start` opens the block and its tag is the letters
  after it; the first later line that starts with `@end` closes it; without an `@start` line the whole text is the
  block. A further `@start` line after the end is reported once as `plantuml-multiple-diagrams`. The framing follows
  the comments with the same scanner (`ScanComments`) that reads the lines, so a tag line counts only outside a block
  comment and a line comment; a block comment opened on the `@start` line carries into the block's first line. An
  included file is framed the same way (`IncludedRange`). When the block has no end because a block comment is still
  open, the hint names the line that opened it.
- Reading a line - a line whose first character is `'` is dropped; the characters between `/'` and `'/` are dropped,
  across lines; a line that ends with a single backslash is joined with the next one (the backslash removed); the
  statement is trimmed.
- Directives - a statement that starts with `!` and a letter or `$` is a directive: `!$name = value` and `?=` (also
  after `!global` or `!local`) assign a quoted text, a number, or another variable; `!define NAME [value]` defines a
  macro (a name followed by `(` has parameters and is reported); `!undef NAME` removes it; `!startsub` and `!endsub` are
  dropped silently; `!include`, `!include_once`, and `!include_many` read a file (below); the other include forms,
  `!theme`, `!pragma`, and every other directive are reported and dropped, and the block
  that `!if`, `!ifdef`, `!ifndef`, `!while`, `!foreach`, `!function`, `!procedure`, or `!definelong` opens is dropped
  up to its closing directive, nested blocks counted.
- Substitution - in every other statement, a `$name` that is a variable and a whole-word `NAME` that is a macro are
  replaced once (the inserted text is not scanned again), and the first built-in call `%name(` is reported and kept.
- Includes - the path after the directive (variables substituted, one pair of quotes removed) is never followed when it
  is a URL (`scheme://`), the standard library (`<name>`), a bracketed name, or a part of a file (`file!part`); those
  and `!includeurl`, `!includesub`, `!includedef`, and `!import` are reported as `plantuml-include-unsupported`. A local
  path is resolved by `IncludeResolver.Load` through the core `FileReferenceResolver` (the folder of the including
  file, then the base directory; the policy judged on the real path of the opened file), decoded as strict UTF-8 without
  a byte-order mark, and named by its path relative to the base directory (forward slashes) or its file name. As in
  PlantUML's preprocessor (`TContext.executeInclude`), a file with an `@start` line contributes the content of its
  first block and another file its whole text; a second `!include` of a file (or of the diagram's own file) is skipped,
  `!include_many` includes it again, and a second `!include_once` is an error. The lines of the file are read in place
  with its own line table, folder, and depth, sharing the variables, macros, and statements of the text.
- Limits - the total length of the statements may not pass `MaxTextSize`; the open dropped blocks may not pass
  `MaxNestingDepth`; an include at a depth of `MaxIncludeDepth` and the include directive after `MaxIncludes` followed
  ones are refused.

#### Error Handling

A text or statements over the size limit throw `InvalidDataException` with a message that names
`PlantUmlLimits.MaxTextSize`. A block without an end tag throws a `PlantUmlSyntaxException` with the code
`plantuml-syntax-error` at the `@start` line, a hint, and the expected end tag. Dropped blocks nested beyond the limit
throw a `PlantUmlSyntaxException` with the code `plantuml-limit-exceeded` at the opening directive, as do the include
limits at the include directive. A missing included file, a file the engine refuses (whose `FileReferenceException`
is the inner exception), a file that is not valid UTF-8, a second `!include_once`, and an include without a file
source throw `plantuml-include-failed` at the directive with a hint; an included file over the size limit throws
`InvalidDataException`. Every unsupported directive is a pending report, never an exception; reading never touches the
network, and touches a local file only through the include resolver.

#### Dependencies

The unit uses the root namespace's `PlantUmlLimits`, `PlantUmlLocation`, `PlantUmlSyntaxException`,
`PlantUmlDiagnosticCodes`, and `PlantUmlPendingReport`, the core `Files` subsystem (`FileReferenceResolver`,
`FileReferenceOptions`, `FileReference`, `FileReferenceException`, `FileAccessPolicy`), and the .NET base class library.

#### Callers

`PlantUmlDocument` runs the preprocessor on every text it opens, with an `IncludeResolver` built from its open options
(the folder of an opened file by default); `DiagramDetector` reads its result; the diagram
parsers read its statements and locate their errors with `SourceStatement`.
