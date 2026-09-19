## System.Numerics.Tensors Verification

This document provides the verification evidence for the `System.Numerics.Tensors` OTS software
item. Requirements for this OTS item are defined in the System.Numerics.Tensors OTS Software
Requirements document.

### Required Functionality

`System.Numerics.Tensors` (`TensorPrimitives`) is CanvasNet's only shipped runtime NuGet
dependency, used by `Surface`'s `PremultiplyAlpha`, `UnpremultiplyAlpha`, and `CompositeOver`
implementations for vectorized per-channel floating-point arithmetic
(`ConvertSaturating`, `Round`, `Multiply`, `Divide`, `Add`, `Subtract`). Correct results from these
operations are required for numerically correct alpha premultiplication and Porter-Duff
compositing.

### Verification Approach

Unlike the build-time/quality-pipeline OTS items, `System.Numerics.Tensors` has no CLI or
self-validation mode and no injectable seam to mock in isolation, so it is verified indirectly,
end-to-end, through its observable effect on pixel bytes: the existing `Surface` unit tests
(in `SurfaceTests.cs`) that exercise `PremultiplyAlpha`, `UnpremultiplyAlpha`, and
`CompositeOver(Surface)`/`CompositeOver(Rgba32)` assert exact expected pixel values, computed
independently of the implementation via a separate float32 simulation. A passing test run for
each named scenario below is direct evidence that every `TensorPrimitives` method `Surface` calls
computed the correct result.

### Test Scenarios

#### Surface_PremultiplyAlpha_VariousValues_ComputesExpectedPixels

**Scenario**: `PremultiplyAlpha` calls `TensorPrimitives.ConvertSaturating`, `Multiply`, `Divide`,
`Round`, and the reverse `ConvertSaturating` across a table of color/alpha combinations.

**Expected**: Each resulting pixel exactly matches the independently computed premultiplied value.

**Requirement coverage**: `CanvasNet-OTS-SystemNumericsTensors`.

#### Surface_UnpremultiplyAlpha_VariousValues_ComputesExpectedPixels

**Scenario**: `UnpremultiplyAlpha` calls the same `TensorPrimitives` operations in the reverse
direction across a table of premultiplied-color/alpha combinations.

**Expected**: Each resulting pixel exactly matches the independently computed straight-alpha value.

**Requirement coverage**: `CanvasNet-OTS-SystemNumericsTensors`.

#### Surface_CompositeOverSurface_PartialAlpha_MatchesIndependentlyComputedResult

**Scenario**: `CompositeOver(Surface)` calls `TensorPrimitives.Divide`, `Subtract`, `Multiply`, and
`Add` to compute the Porter-Duff "over" formula for two partially transparent pixels.

**Expected**: The resulting pixel exactly matches a value independently hand-computed via the
Porter-Duff "over" formula in a separate float32 simulation.

**Requirement coverage**: `CanvasNet-OTS-SystemNumericsTensors`.

#### Surface_CompositeOverColor_PartialAlpha_MatchesIndependentlyComputedResult

**Scenario**: `CompositeOver(Rgba32)` calls the same `TensorPrimitives` operations to composite a
partially transparent constant color over a background pixel.

**Expected**: The resulting pixel exactly matches an independently hand-computed value.

**Requirement coverage**: `CanvasNet-OTS-SystemNumericsTensors`.

### Requirements Coverage

- **`CanvasNet-OTS-SystemNumericsTensors`**: Surface_PremultiplyAlpha_VariousValues_ComputesExpectedPixels,
  Surface_UnpremultiplyAlpha_VariousValues_ComputesExpectedPixels,
  Surface_CompositeOverSurface_PartialAlpha_MatchesIndependentlyComputedResult,
  Surface_CompositeOverColor_PartialAlpha_MatchesIndependentlyComputedResult
