---
title: Writing an equation file
summary: The math element, its namespace, display mode, tokens, entities, and file rules.
tags:
  - mathml
  - authoring
see-also:
  - mathml/pitfalls
  - mathml/symbols
  - mathml/elements
examples:
  - name: ohm
    kind: source
    language: xml
    file: ohm.mml
    description: Ohm's law and the power of a resistor in display style, with a text alternative.
  - name: inline-sum
    kind: source
    language: xml
    file: inline-sum.mml
    description: A sum in inline style (no display attribute) - its limits become scripts and its fraction is small.
---

A file is one `math` element and nothing else. Write each quantity as a token
and let the operators space themselves:

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block" alttext="a squared plus b squared">
  <msup><mi>a</mi><mn>2</mn></msup>
  <mo>+</mo>
  <msup><mi>b</mi><mn>2</mn></msup>
</math>
```

## The math element

- Namespace: `xmlns="http://www.w3.org/1998/Math/MathML"`; a `math` element in
  no namespace (as HTML writes it) is accepted too.
- Display: `display="block"` lays the equation out as a stand-alone formula
  (display style: limits under and over a sum, large operators, full-size
  fractions); without it the equation is inline (compact, as in a line of text).
  A link or an input can change it with `#display=block` or `#display=inline`.
- Text alternative: `alttext="..."` describes the equation in words; it is not
  drawn, and a probe reports it for documents that need alt text.
- Source: wrap the equation in `semantics` with an `<annotation
  encoding="application/x-tex">` to keep the LaTeX it came from; annotations are
  never drawn, and a probe lists their encodings.

## Tokens and rows

- `mi` is an identifier (italic when it is one letter, upright when longer:
  `<mi>sin</mi>`), `mn` a number, `mo` an operator (spaced by the operator
  dictionary), `mtext` text, `mspace` a space, and `ms` a string literal.
- `mrow` groups children, so an element that takes exactly two children (a
  fraction, a script) gets one argument each.
- White space inside a token is trimmed and runs of it collapse to one space;
  white space between elements is ignored.

## Characters and files

- Named references of the full W3C HTML and MathML set work without a document
  type (`&alpha;`, `&minus;`, `&InvisibleTimes;`), and so do numeric references
  (`&#x2212;`); a document type declaration is refused.
- The file is UTF-8 (a byte-order mark is allowed; an XML declaration that
  names another encoding, US-ASCII included, is refused), its extension is .mml
  (the only one; the media type is application/mathml+xml), and it reads no
  other file.
