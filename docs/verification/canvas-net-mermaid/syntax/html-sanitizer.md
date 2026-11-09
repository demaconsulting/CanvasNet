### HtmlSanitizer Unit Verification Design

This document describes the unit-level verification strategy for the `HtmlSanitizer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: the internal static `HtmlSanitizer` class (`Sanitize` and
`DecodeReferences`) and the grouped `HtmlEntities` table of named character references.

#### Verification Approach

The unit is verified by sanitizing labels and comparing the result with the text that Mermaid's `sanitizeText`
(DOMPurify in a browser) gives for the same label: the expected texts were recorded from Mermaid in Chrome. The 21
cases cover the fast path, references, removed and kept elements and attributes, unsafe URIs, the link-target hook,
comments, cut-off tags, and formatting elements closed out of order. A hostile label of 2,000 unclosed tags checks
that the clean-up completes in one pass and closes every element. The `HtmlEntities` table is exercised through the
named references of the labels. No mocking or stubbing is required; the internal types are reached through the test
assembly's `InternalsVisibleTo` access.

Unit tests reside in `HtmlSanitizerTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Syntax` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of in-memory text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception. Any cleaned label that
differs from Mermaid's by a single character constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Syntax-HtmlSanitizer-FastPath: A Label Without Markup Is Kept As Written

**Tests**: `HtmlSanitizerTests.HtmlSanitizer_Sanitize_Label_MatchesMermaid`

Asserts that `a & b` and `fish &amp; chips` are returned unchanged.

##### CanvasNetMermaid-Syntax-HtmlSanitizer-Removal: What DOMPurify Removes Is Removed

**Tests**: `HtmlSanitizerTests.HtmlSanitizer_Sanitize_Label_MatchesMermaid`

Asserts that a `script` element is removed with its content, an unknown element is removed with its text kept, an
`onerror` attribute and a `javascript:` link are removed, a comment is removed, and a tag cut off by the end of the
text (`a<b`) is removed, while `b`, `i`, `span` with `style` and `title`, `a` with `href`, and `img` with `src` and
`width` are kept.

##### CanvasNetMermaid-Syntax-HtmlSanitizer-Links: Link Targets Follow Mermaid's Hook

**Tests**: `HtmlSanitizerTests.HtmlSanitizer_Sanitize_Label_MatchesMermaid`

Asserts that a link with the target `_blank` is written with `href`, then `rel="noopener"`, then `target`.

##### CanvasNetMermaid-Syntax-HtmlSanitizer-Serialization: The Clean Label Is Serialized As A Browser Does

**Tests**: `HtmlSanitizerTests.HtmlSanitizer_Sanitize_Label_MatchesMermaid`,
`HtmlSanitizerTests.HtmlSanitizer_Sanitize_ManyUnclosedTags_ClosesThemAll`

Asserts lower-case names (`<B>` becomes `<b>`), double-quoted attributes with `>` escaped inside them, `&amp;` and
`&lt;` in text, `<br/>` and `</br>` written as `<br>`, an unclosed `<b>` closed at the end, `<i>a<b>b</i>c` written
as `<i>a<b>b</b></i><b>c</b>` (the formatting element reopened), and that 2,000 unclosed `<b>` elements are all
closed.

##### CanvasNetMermaid-Syntax-HtmlSanitizer-References: Known References Decode And Others Stay

**Tests**: `HtmlSanitizerTests.HtmlSanitizer_Sanitize_Label_MatchesMermaid`

Asserts that `&lt;b&gt;` stays escaped text, that `&copy;` decodes to the copyright sign, and that the unknown
`&unknown;` stays as written, its ampersand escaped.

##### CanvasNetMermaid-Syntax-HtmlSanitizer-Bounded: A Hostile Label Is Cleaned In One Pass

**Tests**: `HtmlSanitizerTests.HtmlSanitizer_Sanitize_ManyUnclosedTags_ClosesThemAll`

Asserts that a label of 2,000 nested unclosed `<b>` elements is cleaned without failing, with exactly 2,000 start
tags and 2,000 end tags at the end.
