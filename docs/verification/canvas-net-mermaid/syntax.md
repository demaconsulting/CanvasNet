## Syntax Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Syntax` subsystem (internal namespace
`DemaConsulting.CanvasNet.Mermaid.Syntax`), containing five units: `Preprocessor` (Mermaid's preparation of a diagram
text with the location map back to the original text), `DiagramDetector` (the diagram type with Mermaid's detectors),
`YamlSubsetParser` (the YAML of frontmatter and shape data as js-yaml reads it), `SourceScanner` (the lexer
infrastructure of the diagram parsers), and `HtmlSanitizer` (the clean-up of label HTML with the `HtmlEntities`
table).

### Verification Approach

The `Syntax` subsystem is verified at two tiers. The unit tests (see the unit verification designs under `syntax/`)
verify each unit in isolation against the results of the pinned Mermaid release and the js-yaml it bundles: the
expected values of the preparation, detection, and YAML tests were produced by running Mermaid's own expressions and
js-yaml on the same inputs, and the expected texts of the sanitizer tests are what Mermaid's `sanitizeText` gives in
a browser. The subsystem-level scenarios in `SyntaxTests` verify the units working together on a whole diagram text:
the `Preprocessor` reads the frontmatter through the `YamlSubsetParser`, finds the directives, and removes the
comments; the `DiagramDetector` detects the type of the resulting code; a small grammar built on the `SourceScanner`
lexes the parser text that the `Preprocessor` prepares; the `HtmlSanitizer` cleans the label HTML of a prepared text;
and positions of the parser text, of its tokens and lexical and grammar errors, of the detected keyword, and of a YAML
error map back to the lines and columns of the original text.

The expectations are exact: the title, the configuration values, the directive arguments, the detector id, and the
one-based locations are stated as literals derived by hand from the input text (for example, the `B` of the tenth
line of a text with Windows line endings is at line 10, column 24), and the cleaned label is the text that DOMPurify's
rules give for it. The small grammar (spaces skipped, line feeds and words as tokens) stands in for a diagram parser,
so the scanner is exercised on the real parser text without the `Flowchart` subsystem. The subsystem uses no mocking or
stubbing; it is internal, so its tests reach it through the test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test prepares its own text in memory; no files are read

### Acceptance Criteria

The `Syntax` subsystem's verification passes when every subsystem-level scenario below and every unit-level scenario
in the five unit verification documents passes without error or unexpected exception.

### Test Scenarios

#### CanvasNetMermaid-Syntax-Preprocessing: A Text Is Prepared As Mermaid Prepares It

**Tests**: `SyntaxTests.Syntax_PreprocessAndDetect_FullText_KeepsOriginalLocations`

Prepares a text with Windows line endings, a frontmatter with a title and a nested configuration, an init directive
with single-quoted JSON, a comment line, and a flowchart, and asserts that the title is `Pipeline`, that the
configuration value `flowchart.curve` is `linear`, and that the directive is `init` with its quotes replaced by
double quotes.

#### CanvasNetMermaid-Syntax-ParserInput: The Parser Text Maps Back To The Original Text

**Tests**: `SyntaxTests.Syntax_PreprocessAndDetect_FullText_KeepsOriginalLocations`,
`SyntaxTests.Syntax_ScanParserText_AfterFrontmatter_LocatesTokensAndLexicalError`,
`SyntaxTests.Syntax_ParseParserText_UnexpectedToken_ListsExpectedTokensAtOriginalLocation`

Asserts that the last node of the parser text, which follows an encoded `#quot;` entity on a line after the removed
frontmatter, directive, and comment, is located at line 10, column 24 of the original text. Lexes the parser text of a
text with a frontmatter and a comment line with a grammar of words and line feeds and asserts the tokens `flowchart`,
`LR`, a line feed, `a`, `b`, a line feed, and `c`, that the keyword is located at line 5, column 1 and `b` at line 6,
column 5 of the original text, and that the `?` that no rule matches is a `mermaid-syntax-error` at line 7, column 5
that names the character. Reads a second word where the grammar accepts only a line feed, after a frontmatter, and
asserts a `mermaid-syntax-error` at line 4, column 14 whose expected tokens are the grammar's description `a new line`
and whose message names the word found. The full lexing semantics, including the letters outside ASCII of the
`UNICODE_TEXT` rule that the flowchart and class grammars share (the `SourceScanner-UnicodeLetters` child), are verified
by the `SourceScanner` unit scenarios and, end to end, by the `Flowchart`, `Sequence`, and `Class` subsystems.

#### CanvasNetMermaid-Syntax-Detection: The Type Of The Prepared Code Is Detected At Its Keyword

**Tests**: `SyntaxTests.Syntax_PreprocessAndDetect_FullText_KeepsOriginalLocations`

Detects the type of the prepared code and asserts that it is Mermaid's `flowchart-v2` located at line 9, column 1 of
the original text, after the frontmatter, the directive, and the comment line.

#### CanvasNetMermaid-Syntax-Yaml: The Frontmatter YAML Has Mermaid's Values

**Tests**: `SyntaxTests.Syntax_PreprocessAndDetect_FullText_KeepsOriginalLocations`

Asserts that the frontmatter configuration is a mapping whose `flowchart` key holds a mapping whose `curve` is the
string `linear`, as js-yaml reads it.

#### CanvasNetMermaid-Syntax-UntrustedInput: Refused YAML Stops The Text, Label HTML Is Cleaned, Links Are Checked

**Tests**: `SyntaxTests.Syntax_Preprocess_FrontmatterAlias_IsRefusedAtLocation`,
`SyntaxTests.Syntax_PrepareAndSanitizeLabel_UnsafeHtml_IsCleaned`,
`SyntaxTests.Syntax_PrepareAndCheckLinks_EncodedUnsafeLinks_AreRefused`

Prepares a text whose frontmatter defines an anchor and uses it as an alias, and asserts that preparation fails with
`mermaid-yaml-unsupported` at line 2, column 7 of the original text (the anchor). Prepares a flowchart whose quoted
node label holds a link with a `javascript:` address and an `onclick` handler in double-quoted attributes, a script,
the named reference `&eacute;`, and a bold element; asserts that the preparation turns the attribute quotes into
single quotes so the label stays inside its quoted string, and that the label cleaned by the `HtmlSanitizer` is
`<a>go</a> café <b>ok</b>` (the link without its address and handler, no script, and the reference decoded through
the `HtmlEntities` table). Prepares a class diagram whose links hold a numeric reference, a tab, a percent escape, and
`&Tab;` with `&colon;` inside a `javascript` scheme, and a safe `https` link, and asserts that the link check gives
`about:blank` for each encoded scheme that reaches it, keeps the numeric reference in Mermaid's entity encoding (so it
never decodes to a scheme), and writes the safe link as sanitize-url writes it. The text size limits, the YAML depth
limit, the bound of the clean-up, and every form of the link check are verified by the `Preprocessor`,
`YamlSubsetParser`, `HtmlSanitizer`, and `SourceScanner` unit scenarios.
