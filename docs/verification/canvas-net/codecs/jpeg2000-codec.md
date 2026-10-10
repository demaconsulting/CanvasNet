### Jpeg2000Codec Unit Verification Design

This document describes the unit-level verification strategy for the `Jpeg2000Codec` class.

#### Verification Approach

The `Jpeg2000Codec` unit is verified by `Jpeg2000CodecTests.cs`, which feeds the decoder streams
produced by the test-only `Jpeg2000TestEncoder` (`Jpeg2000TestEncoder.cs`,
`Jpeg2000TestEncoder.Coding.cs`, `Jpeg2000TestEncoder.Markers.cs`). The encoder generates JPEG 2000
codestreams and JP2 files with a chosen combination of features, so each decoder feature is
covered by a round trip: reversible streams must reconstruct exactly and irreversible streams
must fall within a stated tolerance. The MQ coder is additionally verified against the ITU-T T.88
test sequence. No mocking is required; the unit's dependencies (`Surface`, `ImageInfo`) are
in-house types.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Every test builds its own in-memory stream; file-path tests use a temporary file

#### Unit-Level Test Scenarios

##### CanvasNet-Codecs-Jpeg2000Codec-Load: Load Produces RGBA Pixels

**Tests**: `Jpeg2000Codec_Load_Stream_ReturnsPixels`, `Jpeg2000Codec_Load_Grey_ExpandsToRgb`,
`Jpeg2000Codec_Load_Alpha_PopulatesAlpha`, `Jpeg2000Codec_Load_Path_ReturnsPixels`

Loads encoded images from a stream and a file path and checks the pixels, grey-to-RGB expansion
and alpha.

##### CanvasNet-Codecs-Jpeg2000Codec-Decode: Decode Reconstructs Component Samples

**Tests**: the `Jpeg2000Codec_Decode_Reversible*`, `_FourComponents`, `_CodeBlockSizes`,
`_DepthAndSign`, `_SignedRct`, `_Irreversible*` and `_SubsampledComponents` tests.

Round-trips reversible and irreversible streams across decomposition levels, odd sizes, color
transforms, bit depths, signedness and subsampling.

##### CanvasNet-Codecs-Jpeg2000Codec-EntropyCoding: Tier-1 and MQ Decoding

**Tests**: `Jpeg2000Codec_MqEncoder_T88TestSequence_MatchesReferenceCodeStream`,
`Jpeg2000Codec_MqDecoder_T88ReferenceCodeStream_RecoversTestSequence`, the
`Jpeg2000Codec_Decode_CodeBlockStyle*` tests, `_IrreversibleBypassTermall` and the `_Roi*` tests.

Verifies the MQ decoder against the T.88 sequence, every code-block style and the region of
interest max-shift.

##### CanvasNet-Codecs-Jpeg2000Codec-PacketStructure: Tier-2 Packets

**Tests**: the `_Tiles`, `_TileAndImageOffsets`, `_Precincts`, `_PerResolutionPrecincts`,
`_Layers`, `_ProgressionOrders`, `_PositionProgressionWithOffsets` and `_PerComponentLevels*`
tests.

Round-trips streams using every progression order, layers, precinct sizes, tiles and offsets.

##### CanvasNet-Codecs-Jpeg2000Codec-Codestream: Codestream Headers

**Tests**: the `_TileHeaderOverrides`, `_Poc*`, `_SopEph`, `_Ppm`, `_Ppt`, `_TileParts`,
`_ExtraMarkers_AreSkipped` and `_ZeroPsotMissingEoc` tests.

Verifies tile-part headers, progression order changes, packed packet headers, SOP/EPH markers and
tolerated marker variants.

##### CanvasNet-Codecs-Jpeg2000Codec-Jp2Container: JP2 Boxes

**Tests**: the `Jpeg2000Codec_Decode_Jp2*` tests.

Checks enumerated color spaces, sYCC conversion, ICC profile reporting, box length forms,
palettes, and channel definitions (alpha and reordering).

##### CanvasNet-Codecs-Jpeg2000Codec-GetInfo: Header-Only Information

**Tests**: `Jpeg2000Codec_GetInfo_RawCodestream_ReportsDimensionsAndChannels`,
`Jpeg2000Codec_GetInfo_Jp2WithAlpha_CountsAlphaChannel`,
`Jpeg2000Codec_GetInfo_ImageOffset_ReportsWidthMinusOffset`

Checks dimensions, channel count, alpha presence and image-offset handling.

##### CanvasNet-Codecs-Jpeg2000Codec-ArgumentValidation: Argument Validation

**Tests**: `Jpeg2000Codec_NullOrEmptyArguments_Throw`,
`Jpeg2000Codec_Load_MissingFile_ThrowsFileNotFound`

Checks the null, empty-path and missing-file exceptions for every public method.

##### CanvasNet-Codecs-Jpeg2000Codec-Malformed: Malformed Data

**Tests**: `Jpeg2000Codec_Load_NotJpeg2000_ThrowsInvalidData`,
`Jpeg2000Codec_Load_ExceedsMaxDimension_ThrowsInvalidData`, and the `_EveryTruncation`,
`_RandomBitFlips`, `_HeaderByteSubstitutions`, `_HugeCounts`, `_BadMarkerLengths`,
`_GarbageTileData`, `_HostileBoxes` and `_HostilePoc` tests.

Checks that corrupt, truncated and hostile data fail with `InvalidDataException` (or an
unsupported-feature exception) quickly and without unhandled exceptions.

##### CanvasNet-Codecs-Jpeg2000Codec-Unsupported: Unsupported Features

**Tests**: `Jpeg2000Codec_Decode_Part2Capabilities_ThrowsUnsupported`,
`_HighThroughput_ThrowsUnsupported`, `_UnknownTransforms_ThrowsUnsupported`,
`_UnknownRoiStyle_ThrowsUnsupported`, `_FiveComponentsWithoutCdef_ThrowsUnsupported`,
`_DepthAbove16_ThrowsUnsupported`

Checks that valid but unsupported features raise `UnsupportedImageFeatureException`.

#### Requirements Coverage

| Requirement                                         | Covered by                               |
| --------------------------------------------------- | ---------------------------------------- |
| `CanvasNet-Codecs-Jpeg2000Codec-Load`               | Load tests                               |
| `CanvasNet-Codecs-Jpeg2000Codec-Decode`             | Decode round-trip tests                  |
| `CanvasNet-Codecs-Jpeg2000Codec-EntropyCoding`      | MQ, code-block style and ROI tests       |
| `CanvasNet-Codecs-Jpeg2000Codec-PacketStructure`    | Tile, precinct, layer, progression tests |
| `CanvasNet-Codecs-Jpeg2000Codec-Codestream`         | Header override, POC, PPM/PPT, tile-part |
| `CanvasNet-Codecs-Jpeg2000Codec-Jp2Container`       | JP2 box tests                            |
| `CanvasNet-Codecs-Jpeg2000Codec-GetInfo`            | GetInfo tests                            |
| `CanvasNet-Codecs-Jpeg2000Codec-ArgumentValidation` | Argument validation tests                |
| `CanvasNet-Codecs-Jpeg2000Codec-Malformed`          | Malformed and robustness tests           |
| `CanvasNet-Codecs-Jpeg2000Codec-Unsupported`        | Unsupported-feature tests                |
