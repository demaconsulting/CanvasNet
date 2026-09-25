### Canvas Unit Design

The `Canvas` class is the sole stateful unit in the `Rendering` subsystem. It wraps a
`Canvas.Surface` and owns an affine transform stack that is baked into every fill or stroke.

#### Responsibilities

- Own the current 2D affine transform as a `System.Numerics.Matrix3x2` field `_current`,
  initialized to the identity.
- Own a `Stack<Matrix3x2>` for `Save` / `Restore` bookkeeping.
- Expose `Translate(float x, float y)` and `RotateDegrees(float)` mutators that prepend the operation
  to `_current` (`_current = newOp * _current`) matching HTML5 canvas / Skia semantics.
- Expose `CurrentTransform` as a read-only property.
- Expose `FillPath(Path, Rgba32)` and `StrokePath(Path, StrokeStyle, Rgba32)` that first bake
  `CurrentTransform` into the path (short-circuited when it is the identity), then dispatch to
  `Drawing.PathFiller.Fill` / `Drawing.PathStroker.Stroke` on the wrapped `Surface`.
- Expose `FillPath(Path, Gradient, FillRule)`, which bakes `CurrentTransform` into the path the
  same way, and additionally folds `CurrentTransform` into the `Gradient` paint's own coordinate
  mapping via `Gradient.WithTransform` (short-circuited when `CurrentTransform` is the identity,
  matching the path's own short-circuit) before dispatching to `Drawing.PathFiller.Fill(Surface,
  Path, Gradient, FillRule, float)`. Composing the transform into both the path and the gradient
  is what keeps a gradient visually anchored to the shape it paints under a translated or rotated
  Canvas, instead of the gradient remaining fixed in the shape's old, untransformed local frame.

#### Byte-identical no-transform guarantee

When `CurrentTransform` is the identity, `FillPath` and `StrokePath` skip the `Path.Transform`
call entirely and pass the original `Path` reference straight to the drawing pipeline; the
gradient-paint `FillPath` overload likewise skips `Gradient.WithTransform` and passes the original
`Gradient` reference straight through. This preserves the exact bit pattern of pre-existing
renders when callers adopt the wrapper.

#### Validation

- The constructor throws `ArgumentNullException` on a null `Surface`.
- `FillPath` / `StrokePath` throw `ArgumentNullException` on a null `Path`, `Gradient`, or
  `StrokeStyle`.
- `FillPath` throws `ArgumentOutOfRangeException` on an undefined `FillRule`.
- `Restore` throws `InvalidOperationException` when the stack is empty.
