## xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements
for this OTS item are defined in the xUnit OTS Software Requirements document.

### Required Functionality

xUnit v3 (xunit.v3 and xunit.runner.visualstudio) is the unit-testing framework used by the
project. It discovers and runs all test methods and writes TRX result files that feed into coverage
reporting and requirements traceability. Passing tests confirm the framework is functioning
correctly.

### Verification Approach

xUnit is verified by self-validation evidence from the CI pipeline. Each scenario names a specific
test method that xUnit must discover, execute, and record in a TRX result file. A passing pipeline
run for all scenarios constitutes evidence that both requirements are satisfied.

### Test Scenarios

#### Surface_Constructor_ValidDimensions_SetsWidthAndHeight

**Scenario**: xUnit discovers and runs this test; the test verifies that constructing a `Surface`
with valid dimensions sets the `Width` and `Height` properties correctly.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_Constructor_ValidDimensions_BufferIsAllZero

**Scenario**: xUnit discovers and runs this test; the test verifies that a newly constructed
`Surface` is fully transparent (zero-initialized).

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_Constructor_ZeroWidth_ThrowsArgumentOutOfRangeException

**Scenario**: xUnit discovers and runs this test; the test verifies that constructing a `Surface`
with zero width throws `ArgumentOutOfRangeException`.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_Constructor_NegativeWidth_ThrowsArgumentOutOfRangeException

**Scenario**: xUnit discovers and runs this test; the test verifies that constructing a `Surface`
with negative width throws `ArgumentOutOfRangeException`.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_Constructor_ZeroHeight_ThrowsArgumentOutOfRangeException

**Scenario**: xUnit discovers and runs this test; the test verifies that constructing a `Surface`
with zero height throws `ArgumentOutOfRangeException`.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException

**Scenario**: xUnit discovers and runs this test; the test verifies that constructing a `Surface`
with negative height throws `ArgumentOutOfRangeException`.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_Indexer_SetThenGet_ReturnsStoredPixel

**Scenario**: xUnit discovers and runs this test; the test verifies that a pixel set through the
`Surface` indexer is returned unchanged by a subsequent get.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_GetRowSpanBytes_WriteToSpan_IndexerReflectsChange

**Scenario**: xUnit discovers and runs this test; the test verifies that writing through the
`Span<byte>` returned by `GetRowSpanBytes` is reflected by the indexer.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

#### Surface_GetRowSpan_WriteToSpan_IndexerReflectsChange

**Scenario**: xUnit discovers and runs this test; the test verifies that writing through the
`Span<Rgba32>` returned by `GetRowSpan` is reflected by the indexer.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `CanvasNet-OTS-xUnit-Execute`, `CanvasNet-OTS-xUnit-Report`.

### Requirements Coverage

- **`CanvasNet-OTS-xUnit-Execute`**: Surface_Constructor_ValidDimensions_SetsWidthAndHeight,
  Surface_Constructor_ValidDimensions_BufferIsAllZero,
  Surface_Constructor_ZeroWidth_ThrowsArgumentOutOfRangeException,
  Surface_Constructor_NegativeWidth_ThrowsArgumentOutOfRangeException,
  Surface_Constructor_ZeroHeight_ThrowsArgumentOutOfRangeException,
  Surface_Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException,
  Surface_Indexer_SetThenGet_ReturnsStoredPixel,
  Surface_GetRowSpanBytes_WriteToSpan_IndexerReflectsChange,
  Surface_GetRowSpan_WriteToSpan_IndexerReflectsChange
- **`CanvasNet-OTS-xUnit-Report`**: Surface_Constructor_ValidDimensions_SetsWidthAndHeight,
  Surface_Constructor_ValidDimensions_BufferIsAllZero,
  Surface_Constructor_ZeroWidth_ThrowsArgumentOutOfRangeException,
  Surface_Constructor_NegativeWidth_ThrowsArgumentOutOfRangeException,
  Surface_Constructor_ZeroHeight_ThrowsArgumentOutOfRangeException,
  Surface_Constructor_NegativeHeight_ThrowsArgumentOutOfRangeException,
  Surface_Indexer_SetThenGet_ReturnsStoredPixel,
  Surface_GetRowSpanBytes_WriteToSpan_IndexerReflectsChange,
  Surface_GetRowSpan_WriteToSpan_IndexerReflectsChange
