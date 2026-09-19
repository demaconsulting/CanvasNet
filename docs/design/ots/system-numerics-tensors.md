## System.Numerics.Tensors

### Purpose

`System.Numerics.Tensors` provides vectorized/hardware-accelerated numeric primitives
(`TensorPrimitives`) used by `Surface`'s bulk pixel operations. It was chosen so that the
per-channel floating-point arithmetic required for alpha premultiplication and Porter-Duff
compositing runs as whole-row SIMD operations, without hand-rolled vector code or `#if`
platform-specific gating.

### Features Used

- `TensorPrimitives.ConvertSaturating<byte, float>` — widens a row's byte channel values to
  `float` for arithmetic
- `TensorPrimitives.ConvertSaturating<float, byte>` — narrows a row's computed `float` channel
  values back to `byte`, saturating to `[0, 255]`
- `TensorPrimitives.Round` (with `MidpointRounding.AwayFromZero`) — rounds intermediate `float`
  results to the nearest whole channel value before narrowing back to `byte`
- `TensorPrimitives.Multiply` — elementwise multiplication (for example, color times alpha)
- `TensorPrimitives.Divide` — elementwise division (for example, un-premultiplying by alpha)
- `TensorPrimitives.Add` — elementwise addition (for example, combining Porter-Duff terms)
- `TensorPrimitives.Subtract` — elementwise subtraction (for example, computing `1 - alpha`)

### Integration Pattern

Unlike every other OTS item in this repository, `System.Numerics.Tensors` is referenced as a
standard `PackageReference` (not `PrivateAssets="All"`) in
`src/DemaConsulting.CanvasNet/DemaConsulting.CanvasNet.csproj`, and is therefore **shipped as a
transitive dependency of the compiled NuGet package**, not merely used at build time. It is
consumed directly, and exclusively, by `Surface`'s `PremultiplyAlpha`, `UnpremultiplyAlpha`, and
`CompositeOver` implementations via private planar-buffer helper methods; no other member of
`Surface`, or any other unit in CanvasNet, depends on it. `TensorPrimitives` is in-box on
`net9.0`/`net10.0` and supplied via this package on `net8.0`; the package version is pinned to one
value across all three target frameworks so the same code path is exercised everywhere.
