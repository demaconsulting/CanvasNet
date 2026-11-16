### ImagePosition

![Positions Structure](PositionsView.svg)

The `ImagePosition` unit is the model of one feature of a rendered picture. It comprises these source files, all
`public`, in the namespace `DemaConsulting.CanvasNet.Positions`:

- `ImagePosition.cs` - `ImagePosition` (sealed record): one feature with its facts, box, and attachment pixel
- `PositionBox.cs` - `PositionBox` (readonly record struct): the geometry box of a feature
- `PositionPoint.cs` - `PositionPoint` (readonly record struct): a point in the pixels of the picture
- `PositionValue.cs` - `PositionValue` (readonly struct) and `PositionValueKind` (enum): a typed scalar value
- `NamespaceDoc.cs` - documentation of the namespace

#### Purpose

A caller that draws a callout on a rendered diagram or chart needs to know where a feature is without probing the
pixels. The unit is that answer in a form every renderer can produce and every consumer can read: a validated, immutable
record of the kind and id of a feature, the facts that tell it apart from its neighbors, its geometry box, and its
attachment pixel, in the pixels of the picture.

The unit's behavior is specified by the `CanvasNet-Positions-ImagePosition-*` requirements (`Validation`, `Values`,
`Data`, `TypedEquality`, and `Geometry`) in `docs/reqstream/canvas-net/positions/image-position.yaml`.

#### Data Model

**`ImagePosition`** - created with `ImagePosition(kind, id)`; every other property is an `init` property:

- `Kind` (string) - a lowercase word `[a-z][a-z0-9-]*` such as `node`, `edge`, `mark`, or `axis`.
- `Id` (string) - the source id of the feature, or a synthetic id that is deterministic for the same input; may be
  empty, never null.
- `Text` (string?) - a label or a title.
- `Line` (int?) - the 1-based source line that declares the feature.
- `View` (string?) and `ViewName` (string?) - the canonical path of the chart view that holds the feature (`""` for a
  single view, `hconcat[1]`, `vconcat[0].layer[1]`) and the `name` the specification gives that view.
- `Mark` (string?) - the mark type of a chart mark (`bar`, `line`, `point`).
- `From`, `To` (string?) and `Index` (int?) - the ends of an edge or message and its 0-based index among the features of
  the same kind with the same ends, in drawing order.
- `Values` (`IReadOnlyList<KeyValuePair<string, PositionValue>>`) - the named scalar values of the feature (the
  normalized channel values of a chart mark, the `value` of a pie slice); empty by default.
- `Data` (`IReadOnlyList<KeyValuePair<string, PositionValue>>`) - the named scalar data fields of the source row the
  feature was drawn from (a chart mark's row, as the data gives it); empty by default; at most `MaxDataFields` = 32.
- `ChannelNames` (`FrozenSet<string>?`) - the names a selector predicate compares with `Values` (the channels of the
  renderer); `null` by default. It is kept as given (immutable by type), so many positions share one set.
- `Box` (`PositionBox?`) and `Pixel` (`PositionPoint?`) - the geometry box and the attachment point; `null` only when
  a later image step could not follow them.

**`PositionBox(X, Y, Width, Height)`** - the bounds of the geometry of a feature, the way SVG `getBBox` measures it
(the stroke is not included); a vertex of a line has a box of zero size. `Center` is the middle of the box.

**`PositionPoint(X, Y)`** - continuous pixel coordinates with the origin at the top-left corner of the picture: pixel
`i` spans `[i, i + 1)`, x grows to the right and y downward.

**`PositionValue`** - a `PositionValueKind` (`Null`, `Boolean`, `Number`, or `String`) with `BooleanValue`,
`NumberValue`, and `StringValue` accessors that return `false`, 0, or `null` for another kind; `default` is `Null`. A
boolean is stored as the number 1 or 0, and a number is stored with negative zero turned into zero.

**Invariants**: `Kind` is a lowercase kind word; `Id` is not null; `Line` is at least 1; `Index` is not negative;
`Values` and `Data` are sorted copies (ordinal by name) whose names are not empty and occur once; `Data` has at most
`MaxDataFields` fields; `Box` is finite with a size that
is not negative; `Pixel` is finite; a `Number` value is finite. Every type is immutable and thread-safe. Record equality
of `ImagePosition` compares `Values`, `Data`, and `ChannelNames` by reference.

#### Key Methods

- **`ImagePosition` `init` setters** - check their value before storing it (see *Error Handling*). The `Values` setter
  copies the list with `ToArray`, sorts the copy by name, and then checks the names, so a list the caller
  changes later does not change the position; an empty list becomes the shared empty array. The `Data` setter refuses
  more than `MaxDataFields` fields and then copies, sorts, and checks the list the same way.
- **`ImagePosition.TryGetValue(name, out value)`** - finds a value by its name (case-sensitive); it returns `true`
  when the name is present (even when the value is null) and otherwise gives `PositionValue.Null` and `false`.
  **`ImagePosition.TryGetData(name, out value)`** does the same for a data field; both share the private `TryFind`.
- **`PositionValue.FromBoolean`, `FromNumber`, `FromString`, and `Null`** - create a value.
- **`PositionValue.TryFromObject(value, out result)`** - converts a plain scalar as a parser produces it (the scalars of
  `ImageParameterValues.TryParseParameter` of the `ImageSources` subsystem): `null`, a `bool`, a `string`, a finite
  `double` or `float`, or an integer type or `decimal`; anything else (a list, a dictionary, a number that is not
  finite) gives `false`.
- **`PositionValue.Equals`, `==`, `!=`, and `GetHashCode`** - typed equality: two values are equal only when they have
  the same kind and the same value, so the number 3 equals the number 3.0 but not the text `"3"`; text is compared
  case-sensitively, and numbers are compared by their bits (they are finite and never negative zero, so
  equal bits are equal numbers and the hash code agrees with the equality).
- **`PositionValue.ToString()`** - invariant text: `null`, `true`, `false`, the round-trip number, or the text.
- **`PositionBox.IsValid()` and `PositionPoint.IsValid()`** - every coordinate finite (and, for a box, a size that is
  not negative).
- **`PositionBox.Corners()`** - the four corners clockwise from the top-left; **`PositionBox.Enclose(points)`** - the
  smallest box that encloses at least one point. A transform that is not a pure translation and scale (a rotation) is
  applied to the corners, and the transformed corners are enclosed.

#### Error Handling

All errors are argument errors detected in the setters and factories and thrown to the caller; nothing is handled
locally:

- `ArgumentNullException` for a null `Kind`, `Id`, `Values` or `Data` list, or `FromString` text, and for a null list
  given to `Enclose`.
- `ArgumentException` for a `Kind` that is not a lowercase kind word, a value or data field name that is empty or occurs
  twice, more than `MaxDataFields` data fields, a `Box` that is not valid, a `Pixel` that is not finite, and an empty
  list given to `Enclose`.
- `ArgumentOutOfRangeException` for a `Line` below 1, a negative `Index`, and a `FromNumber` argument that is NaN or
  infinite.

`TryGetValue`, `TryGetData`, and `TryFromObject` never throw.

#### Dependencies

`PositionSelector.IsKindWord` of the `PositionSelector` unit (the kind check), and the .NET base class library
(`System.Globalization` for invariant formatting, `BitConverter`, and `HashCode`).

#### Callers

The `ScenePositions` units of the `CanvasNetMermaid` and `CanvasNetVegaLite` systems create positions (the `MarkBuilder`
unit of `CanvasNetVegaLite` creates their values and data fields); `PositionSelector` and `PositionCollector` read them,
and `PositionSelector` creates the values of its predicates with `TryFromObject`; the `ImagePipeline` unit of the
`CanvasNetViewer` system maps them with `Corners`, `Enclose`, and a `with` expression; and the `RenderCommand` unit of
the `CanvasNetTool` system writes them as JSON.
