### WordWrap Unit Design

`WordWrap` is a stateless static class that packs a paragraph's tokens into lines with a greedy
word wrap that never splits a word group. Its entry points are the two `Pack` overloads (single and
double precision); the supporting enum `WordWrapTokenKind` (`Word`, `Whitespace`, `LineBreak`) is
covered inline in this unit.

#### Purpose

The Pptx and Vsdx text layout engines each carried a copy of the same packing loop: accumulate
consecutive word tokens into one word group, treat whitespace as the only wrap point, close the
line when a unit would overflow it, and place a unit wider than the line on a line of its own.
`WordWrap` is that loop once. It is deliberately independent of text, fonts and units: the caller
classifies each token and measures it in whatever unit it lays out in (EMU as `float`, inches as
`double`), and the packer only decides which tokens share a line. The two layout engines themselves
(bullets, tab stops, autofit, text boxes, glyph placement) stay in their own packages.

#### Data Model

`WordWrap` has no instance state. `WordWrapTokenKind` has three values: `Word` (visible,
non-breakable content), `Whitespace` (a collapsible gap and the only legal wrap point) and
`LineBreak` (an explicit break). The caller's token type is a generic parameter, so no token type is
imposed. The packing algorithm is written once, generically over the number type (`INumber<T>`), and
the `float` and `double` overloads are thin wrappers around it, so that a caller laying out in single
precision gets exactly the single-precision sums it would get from writing the loop itself.

#### Key Methods

##### Pack(tokens, availableWidth, classify, measure)

Walks the tokens in order, keeping the current line, its width, whether it holds a word, and a
pending word group with its width:

- A `Word` token joins the pending group; its width, `measure(token, currentLineWidth)`, is added to
  the group's.
- A `Whitespace` token first places the pending group, then is itself placed as a unit of width
  `measure(token, currentLineWidth)`, measured after the group so a width that depends on the
  position (a tab stop) sees the true width of the line it joins.
- A `LineBreak` token places the pending group, closes the current line (even an empty one), and
  starts a new line. It is never added to a line and `measure` is not called for it.
- Placing a unit closes the current line first only when the line already holds a word and the
  line width plus the unit's width exceeds `availableWidth` (strictly greater, so an exact fit
  stays). A line holding only whitespace is never closed, so whitespace stranded at a wrap point is
  carried onto the next line instead of leaving a blank line behind. A unit wider than the line is
  placed whole on a line of its own.
- After the last token the pending group is placed and, if there was at least one token, the final
  line is kept even when empty (so a trailing line break yields a trailing empty line). No tokens at
  all yields no lines; the caller decides what an empty paragraph looks like.

The result is a new `List<List<T>>` the caller owns; the input list is only read.

#### Error Handling

`tokens`, `classify` and `measure` are validated up front (`ArgumentNullException` with the
parameter name) and an `availableWidth` of NaN throws `ArgumentOutOfRangeException` (parameter
`availableWidth`). A zero or negative width is valid (every unit then gets its own line, apart
from carried whitespace) and positive infinity never wraps. Exceptions thrown by the caller's
delegates propagate unchanged.

#### Dependencies

`WordWrap` depends only on the base class library (`System.Numerics.INumber<T>`); it uses no other
CanvasNet type.

#### Callers

- `PptxDocument` (text layout) packs DrawingML tokens, classifying explicit `<a:br>` breaks and
  supplying the position-dependent width of a tab token from the running line width.
- `VsdxDocument` (text layout) packs VisioML tokens, which are only words and whitespace of fixed
  width.

#### Thread Safety

The class is stateless, so concurrent calls are safe provided the supplied delegates and token list
are.
