# JPEG Test Fixtures

These JPEG files were generated with [ImageMagick](https://imagemagick.org/) 7.1.2 from
`basn2c08.png` in the `PngSuite` conformance corpus, exercising the entropy coding and chroma
subsampling combinations `JpegCodec` supports:

| File | SOF Marker | Chroma Subsampling | Notes |
| ------ | ----------- | --------------------- | ------- |
| `baseline_420.jpg` | SOF0 (baseline) | 4:2:0 | Most common combination in the wild |
| `baseline_422.jpg` | SOF0 (baseline) | 4:2:2 | |
| `baseline_444.jpg` | SOF0 (baseline) | 4:4:4 (no subsampling) | |
| `progressive_420.jpg` | SOF2 (progressive) | 4:2:0 | Common for web-optimized images |
| `grayscale_baseline.jpg` | SOF0 (baseline) | N/A (single component) | |

Generated with, for example:

```pwsh
magick basn2c08.png -sampling-factor 4:2:0 -interlace none -quality 90 baseline_420.jpg
magick basn2c08.png -sampling-factor 4:2:0 -interlace plane -quality 90 progressive_420.jpg
magick basn2c08.png -colorspace Gray -interlace none -quality 90 grayscale_baseline.jpg
```

Verified directly against each file's raw bytes (not assumed from ImageMagick's options): the SOF
marker (`0xFFC0` for baseline, `0xFFC2` for progressive) and `identify -format
"%[jpeg:sampling-factor]"` output were checked before committing these fixtures.

Since JPEG is a lossy format, tests compare decoded pixels against the known source pixel data
using a similarity tolerance, not exact equality.
