## Syntax

![Syntax Structure](MermaidSyntaxView.svg)

### Overview

The `Syntax` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Syntax`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Syntax/`) is the first stage of the pipeline: it turns a diagram text into
what the diagram parsers read, exactly as Mermaid does before it parses a diagram, with every position mapped back to
the text the author wrote. It is **internal**: its types are visible only to the other subsystems of the system and,
through `InternalsVisibleTo`, to the test assembly. It contains five units:

- **Preprocessor** - the internal static `Preprocessor` and the records `PreprocessedSource`, `FrontmatterData`,
  `DirectiveText`, `FrontmatterMatch`, and `DirectiveMatch` (`Preprocessor.cs`), and the location map `SourceLines`
  and `MappedText` (`Preprocessor.MappedText.cs`): line endings, the quotes of HTML attributes, the YAML frontmatter,
  the directives, the comment lines, the entity encoding, and the text size limits; see _Preprocessor Unit Design_
  (`syntax/preprocessor.md`)
- **DiagramDetector** - the internal static `DiagramDetector` with `DetectorSettings`, `DetectedDiagram`, and
  `DiagramSupport`: the diagram type of a text with Mermaid's detectors in Mermaid's order, and the errors and hints
  for a text of no known type; see _DiagramDetector Unit Design_ (`syntax/diagram-detector.md`)
- **YamlSubsetParser** - the internal `YamlSubsetParser` (a partial class in `YamlSubsetParser.cs` and
  `YamlSubsetParser.Values.cs`) and the value tree `YamlNode`, `YamlScalar`, `YamlSequence`, `YamlMapping`, and
  `YamlEntry`: the reader of the YAML of a frontmatter and of shape data; see _YamlSubsetParser Unit Design_
  (`syntax/yaml-subset-parser.md`)
- **SourceScanner** - the internal `SourceScanner` with `ScanRule`, `ScanToken`, and the `ScanMatcher` delegate
  (`SourceScanner.cs`), the matcher building blocks `ScanPatterns` (`SourceScanner.Patterns.cs`), and the lookahead
  `TokenCursor` (`SourceScanner.Tokens.cs`): the lexer infrastructure that the diagram parsers build on, with the
  link check `UrlSanitizer` (`SourceScanner.Urls.cs`) that every diagram applies to the links it keeps; see
  _SourceScanner Unit Design_ (`syntax/source-scanner.md`)
- **HtmlSanitizer** - the internal static `HtmlSanitizer` (`HtmlSanitizer.cs`) and the grouped table of named
  character references `HtmlEntities` (`HtmlEntities.cs`): the clean-up that Mermaid applies to every label it stores;
  see _HtmlSanitizer Unit Design_ (`syntax/html-sanitizer.md`)

The subsystem's namespace documentation is in `Syntax/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Syntax-*` requirements in `docs/reqstream/canvas-net-mermaid/syntax.yaml`. The SysML2 model names
the subsystem `MermaidSyntax` (see _CanvasNetMermaid System Design_).

### Interfaces

**Exposed** (internal only): `Preprocessor.Run(string, string?, MermaidLimits)` with its result
`PreprocessedSource(Lines, Code, ParserText, Frontmatter, Directives)`, `Preprocessor.SourceLengthFactor`, and the
JavaScript string helper `Preprocessor.TrimJs`; `MappedText` (`Text`, `Locate`, `OriginOf`, `Slice`, and its
`Builder`) and `SourceLines.Locate`; `DiagramDetector.Detect(MappedText, DetectorSettings)`,
`DiagramDetector.UnsupportedHint`, `DiagramDetector.SupportedDiagramKeywords`, `DiagramDetector.DetectorIds`, and
`DiagramDetector.EditDistance`; `YamlSubsetParser.Parse(string, Func<int, MermaidLocation>, int)` and the `YamlNode`
tree with `IsTruthy`, `ToJsString`, and `YamlMapping.Get`; `SourceScanner` (`Next`, `Begin`, `PopState`, `TopState`,
`Less`, `Locate`, `LexicalError`), `ScanRule`, `ScanToken`, `ScanPatterns`, and `TokenCursor` (`Current`, `Is`,
`Advance`, `Expect`, `Error`, `JoinAlternatives`); `UrlSanitizer.Sanitize` and `UrlSanitizer.FormatUrl`; and
`HtmlSanitizer.Sanitize` and `HtmlSanitizer.DecodeReferences`.

**Consumed**: the public `MermaidLimits` (`MaxTextSize`, `MaxYamlDepth`), `MermaidLocation`,
`MermaidSyntaxException`, and `MermaidDiagnosticCodes` of the root namespace, and the .NET base class library. The
subsystem uses no other subsystem of the system and no part of the core `CanvasNet` library.

**Consumers**: `MermaidDocument` preprocesses and detects every text it opens and reads the unsupported-type hint;
the `Configuration` subsystem (`MermaidConfigResolver`) reads the frontmatter configuration and the directives,
detects again with the resolved renderer settings, and uses the edit distance for its nearest-key hints; the
`Flowchart` subsystem lexes the parser text with `SourceScanner` and `TokenCursor`, reads shape data with
`YamlSubsetParser`, and sanitizes labels with `HtmlSanitizer`; the `Sequence` subsystem lexes the parser text with
`SourceScanner` and `TokenCursor`, reads participant metadata with `YamlSubsetParser`, sanitizes texts with
`HtmlSanitizer`, checks the link texts of participants with `UrlSanitizer`, and uses the edit distance for the nearest
participant type; the `Class` subsystem lexes the parser text with `SourceScanner` and `TokenCursor` (its names in
every script through `ScanPatterns.IsUnicodeLetter`, as the flowchart's do), sanitizes texts with `HtmlSanitizer`,
and checks the links of classes with `UrlSanitizer.FormatUrl`; the `Rendering` subsystem (`LabelBuilder`) sanitizes
label HTML and decodes character references with `HtmlSanitizer`, and `ShapeNames` uses the edit distance.

### Design

**Mermaid's preparation, step by step.** `Preprocessor.Run` performs the steps of Mermaid's `preprocessDiagram`
(`cleanupText`, `extractFrontMatter`, `processDirectives`, `cleanupComments`) in Mermaid's order, followed by the
`encodeEntities` of `Diagram.fromText`. The result holds the code (what the detector reads and what Mermaid's text
size limit measures), the parser text (the encoded code with a final line feed), the frontmatter (its title, display
mode, and configuration as YAML), and the directives in text order. Interpreting the configuration and the
directives is the business of the `Configuration` subsystem; the `Syntax` subsystem only finds them.

**Every position keeps its origin.** Each transformation builds a new `MappedText` from slices of the previous one,
whose characters keep their original offsets, and inserted text, which takes the offset of what it replaces. A
position of the parser text, a frontmatter key, a token, or a YAML value is therefore always one lookup away from its
one-based line and column in the original text, and every diagnostic of every later stage points into the text the
author wrote.

**No regular expression runs on diagram text.** Every Mermaid expression that the subsystem reproduces (the
frontmatter, directive, and comment expressions, the detector expressions, and the lexer rules of the parsers) is a
hand-written scanner with the same matches, including the backtracking cases, so the time is linear in the text and
no input can make a step backtrack catastrophically. The `ScanPatterns` building blocks and the JavaScript character
classes let the ported lexer rules state the original expressions part by part.

**Fidelity to the pinned Mermaid release.** The detector knows every diagram type that Mermaid registers, in
Mermaid's registration order, with the quirks of Mermaid's expressions kept on purpose. The YAML reader is a port of
the loader of the js-yaml that Mermaid bundles, with the implicit types of its JSON schema, its errors, and its
messages. The lexer has the semantics of the lexers that jison generates for Mermaid's grammars (first matching rule,
start-condition stack, the end-of-input rule run once, "expected ..., found ..." errors). The sanitizer reproduces
the results of DOMPurify, the sanitizer Mermaid's `sanitizeText` runs, for the inline HTML of labels.

**Untrusted input is bounded.** The original text may be at most four times `MermaidLimits.MaxTextSize` long and the
code at most `MaxTextSize`; YAML collections may nest at most `MermaidLimits.MaxYamlDepth` deep, a limit reported at
open time as a `MermaidSyntaxException` with `mermaid-limit-exceeded`; anchors, aliases, tags, explicit keys, and
directives are refused in YAML, so a small text cannot expand into a huge value; the sanitizer removes scripts,
event handlers, and unsafe links from label HTML in one pass without recursion; and every link a diagram keeps is
checked by `UrlSanitizer` exactly as the sanitize-url that Mermaid bundles checks it, so a `javascript`, `data`, or
`vbscript` link hidden by character references, control characters, escaped white space, or percent escapes is kept
only as `about:blank`.

**Errors are located and hinted.** Invalid YAML is `mermaid-yaml-invalid` with js-yaml's description, a refused
construct is `mermaid-yaml-unsupported`, an unknown diagram type is `mermaid-unknown-diagram` with the nearest known
keyword, a lexical error or an unexpected token is `mermaid-syntax-error` with the expected tokens, and a text beyond
the size limits is an `InvalidDataException`. Every type is immutable or used by one call on one thread, so the
subsystem is safe for concurrent use.

**Attribution.** The preparation, the detector, and the lexer semantics are derived from Mermaid (MIT), and the YAML
reader from js-yaml as bundled by Mermaid (MIT), and the link check from sanitize-url (MIT) and Mermaid's `formatUrl`;
the source files carry the derivation header and the provenance of
each unit is listed in `third-party/README.md`. The `HtmlSanitizer` and its `HtmlEntities` table are original work
that reproduces the results of DOMPurify with the standard HTML names it allows by default.

**Known differences from Mermaid.** The text size is checked twice (the code against `MaxTextSize`, as Mermaid does,
and the original text against four times it); the YAML depth limit, the refused YAML constructs, and the sanitizer's
simplified tree building and reduced set of named references are documented in the unit designs.
