### MathMlReader

![Syntax Structure](MathMlSyntaxView.svg)

The `MathMlReader` unit is a software unit of the `Syntax` subsystem. Its source file is `MathMlReader.cs`, which
defines the internal static partial class `MathMlReader` and the record `MathReadResult`.

#### Purpose

`MathMlReader` turns MathML text into the equation tree with the location of every element, attribute, and text in the
original text, through the reference pre-pass and the hardened XML reader of the core, bounded and without recursion.
The unit's behavior is specified by the `CanvasNetMathMl-Syntax-MathMlReader-*` requirements in
`docs/reqstream/canvas-net-math-ml/syntax/math-ml-reader.yaml`.

#### Data Model

- `MathReadResult(Root, ElementCount, Reports, Annotations)` - the `math` element, the number of MathML elements read
  (annotation content and skipped elements excluded), the pending reports in text order, and the annotations in text
  order.
- `MathMlNamespace` - `http://www.w3.org/1998/Math/MathML`; the XLink namespace marks link attributes.
- Per call (private): `ReadState` with the `LocationMap`, the limits, the stack of open `Frame`s (name, attributes,
  location, children, the raw text of a token or of a run of text, and its location), the root, the element count, the
  reports, and the annotations.

#### Key Methods

- `Read(text, sourceName, limits)` - rewrites the references (`CharacterReferences.Rewrite`), refuses an unpaired
  surrogate at its location, encodes the text as strict UTF-8, creates the reader with `SafeXml.CreateReader(stream,
  cap)` where the cap is the length of the rewritten text plus one (at most 9/4 of the original, which was already
  checked against `MaxTextSize`), and runs the state over every node. An `XmlException` is mapped to a
  `mathml-syntax-error` at its position mapped back (or at the document type declaration the pre-pass recorded, when the
  reader refused it without a line), with the position suffix of its message removed and a hint (for a document type:
  remove the declaration, because the W3C named references are built in; otherwise: close every element and escape a
  literal `&` or `<`).
- Start of an element - the first element must be `math` in the MathML namespace or in no namespace, else a located
  error with a hint. An element of another namespace, or any element inside a token, is reported as
  `mathml-element-unsupported` and skipped with its subtree. `annotation` and `annotation-xml` are read as raw content
  (the text, or the inner markup) with their `encoding`, listed in `Annotations`, and never laid out or reported. Every
  other element is pushed, checked against `MaxNestingDepth` and `MaxElements`, with its attributes read with their own
  locations: `href` and the `xlink` attributes are reported as `mathml-attribute-unsupported` ("not followed") and not
  kept; other attributes of other namespaces are ignored.
- Text - inside a token the raw text is accumulated (checked against `MaxTokenLength` on the raw text) and, at the end
  of the token, trimmed and collapsed (`CollapseWhitespace`: every run of XML whitespace becomes one space). Elsewhere,
  whitespace-only text is dropped and other text is kept as a `MathText` child (drawn like an `mtext`) and reported as
  `mathml-element-unsupported`, because text belongs in a token.
- End of an element - completes the `MathElement` with its attributes and children and adds it to its parent, or makes
  it the root.

#### Error Handling

Every refusal is a `MathMlSyntaxException` located in the original text: `mathml-syntax-error` for malformed XML, a
document type, another root, an unknown reference or a foreign encoding (from the pre-pass), and an unpaired surrogate;
`mathml-limit-exceeded` for the element, nesting, and token-length limits, naming the `MathMlLimits` property. Foreign
content, links, and stray text are not errors: they are kept as pending reports for every measure and render call. The
walk is iterative, so deep nesting cannot overflow the stack.

#### Dependencies

The `CharacterReferences` and `MathTree` units, the core `Packaging` subsystem (`SafeXml`), and the root namespace's
`MathMlLimits`, `MathMlLocation`, `MathMlAnnotation`, `MathMlSyntaxException`, `MathMlDiagnosticCodes`, and
`MathMlPendingReport`.

#### Callers

`MathMlDocument` reads every text it parses or opens with it.
