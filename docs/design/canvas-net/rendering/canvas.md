### Canvas Unit Design

The `Canvas` class is the sole stateful unit in the `Rendering` subsystem. It wraps a
`Canvas.Surface` and owns an affine transform stack that is baked into every fill or stroke.

#### Responsibilities

- Own the current 2D affine transform as a `System.Numerics.Matrix3x2` field `_current`,
  initialized to the identity.
- Own a `Stack<Matrix3x2>` for `Save` / `Restore` bookkeeping.
- Expose `Translate(Vector2)` and `RotateDegrees(float)` mutators that prepend the operation
  to `_current` (`_current = newOp * _current`) matching HTML5 canvas / Skia semantics.
- Expose `CurrentTransform` as a read-only property.
- Expose `FillPath(Path, Rgba32)` and `StrokePath(Path, StrokeStyle, Rgba32)` that first bake
  `CurrentTransform` into the path (fast-pathed when it is the identity), then dispatch to
  `Drawing.PathFiller.Fill` / `Drawing.PathStroker.Stroke` on the wrapped `Surface`.

#### Byte-identical no-transform guarantee

When `CurrentTransform` is the identity, `FillPath` and `StrokePath` skip the `Path.Transform`
call entirely and pass the original `Path` reference straight to the drawing pipeline. This
preserves the exact bit pattern of pre-existing renders when callers adopt the wrapper.

#### Validation

- The constructor throws `ArgumentNullException` on a null `Surface`.
- `FillPath` / `StrokePath` throw `ArgumentNullException` on a null `Path` or null
  `StrokeStyle`.
- `FillPath` throws `ArgumentOutOfRangeException` on an undefined `FillRule`.
- `Restore` throws `InvalidOperationException` when the stack is empty.
