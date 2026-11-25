### MathTree

![Syntax Structure](MathMlSyntaxView.svg)

The `MathTree` unit is a data-model unit of the `Syntax` subsystem. Its source file is `MathTree.cs`, which defines the
internal records `MathNode`, `MathElement`, `MathText`, and `MathAttribute`.

#### Purpose

`MathTree` is the single internal model of an equation: the reader builds it from MathML, the layout reads it, and a
later front end (for example LaTeX math) can produce it without any XML. It holds no `System.Xml` type, only names,
attribute values, children, text, and locations. The unit's behavior is specified by the
`CanvasNetMathMl-Syntax-MathTree-*` requirements in `docs/reqstream/canvas-net-math-ml/syntax/math-tree.yaml`.

#### Data Model

- `MathNode(Location)` - the abstract base of a node, with its `MathMlLocation` in the original text.
- `MathElement(Name, Attributes, Children, Location)` - the local name of a MathML element as written (MathML names are
  case-sensitive), the attributes keyed by local name (ordinal, case-sensitive; attributes of other namespaces are not
  kept), and the children in document order: elements and, for a token, its collapsed text.
- `MathText(Text, Location)` - a run of text: the content of a token, or text written directly inside a layout element.
- `MathAttribute(Value, Location)` - the value of an attribute with the location of the attribute, so that a report
  about an invalid value points at it.
- The records are immutable and compare by value (text runs and attribute values); a tree is safe for concurrent use.

#### Key Methods

- `ElementChildren` - the child elements in document order, the text children skipped.
- `TextContent` - the text children concatenated (for a token, its collapsed text); empty when there is none.
- `GetAttribute(name)` - the value of an attribute, or null.
- `IsTextTokenName(name)` - true for the token elements whose content is text: `mi`, `mn`, `mo`, `mtext`, and `ms`
  (`mspace` has no text).

#### Error Handling

N/A - the records hold values that the reader has already validated; they perform no parsing and raise no errors of
their own.

#### Dependencies

The root namespace's `MathMlLocation`.

#### Callers

`MathMlReader` builds the tree; `MathMlDocument` keeps it; the Layout subsystem (`LayoutEngine`, `StyleResolver`,
`TokenLayout`, `RowLayout`, and `MathBox`) reads it; `MathMlPainter` reads the locations of its elements.
