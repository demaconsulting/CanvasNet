---
title: Raster images (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000)
summary: 'Raster images: PNG, JPEG, BMP, TIFF, GIF, JPEG 2000.'
tags:
  - raster
  - hosting
  - diagnostics
aliases:
  - png
  - jpeg
  - jpg
  - bmp
  - tiff
  - tif
  - gif
  - jpeg2000
  - jp2
see-also:
  - svg/fragments
  - limits
---

Pixel images CanvasNet reads, converts, and writes. There is no diagnostics
channel.

## Supported

- PNG input: non-interlaced, every color type, bit depths 1 to 16, tRNS
  transparency. Output: 8-bit RGBA.
- JPEG input: baseline and progressive, gray and YCbCr, 4:4:4, 4:2:2, and 4:2:0.
  The EXIF orientation is applied unless `SourceOptions.AutoOrient` is false.
  Output: baseline, no alpha (flattened over the background), quality
  {{qualityMin}} to {{qualityMax}}, default {{qualityDefault}}.
- BMP input: uncompressed 24-bit and 32-bit. Output: 32-bit with alpha.
- TIFF input: 8-bit RGB, RGBA, and gray in strips, little- and big-endian, with
  no compression, PackBits, LZW, or Deflate. Output: uncompressed, with alpha.
- GIF input: GIF87a and GIF89a, the first frame only, with transparency and
  interlacing.
- JPEG 2000 input: JP2 files and raw codestreams of Part 1 (.jp2, .j2k, .j2c).
- Output format: from the output extension, or `WriteOptions.Format` (Png, Jpeg,
  Bmp, or Tiff).

## Unsupported

- Adam7-interlaced PNG.
- CMYK and arithmetic-coded JPEG.
- Palette-based and RLE BMP.
- Tiled, palette, CMYK, and YCbCr TIFF, and other bit depths.
- Animated GIF: only the first frame is read (a probe reports the frame count,
  `SourceInfo.FrameCount`).
- JPEG 2000 Part 2 and High Throughput (HTJ2K) codestreams.
- Writing GIF or JPEG 2000: they are input formats only.
- Images with a side above {{maxDimension}} pixels: size-limit.

## Options

The operations of the Imaging namespace read a raster with a `SourceOptions` and
write it with a `WriteOptions`; `CanvasNetViewer.Render` fits a raster inside a
box of both a `Width` and a `Height`, keeping its aspect ratio.

- `Scale`: Multiply the size by a factor; a `Width` and a `Height` resample to a
  size.
- `Filter`: Resampling filter (`ResamplingMode`): Auto (default), Nearest,
  Bilinear, Bicubic, Lanczos3, or Area.
- `Background`: The color transparency is flattened over, for example for a JPEG
  output (`WriteOptions.FlattenBackground` when it is written).
- `AutoOrient`: false does not turn a JPEG upright by its EXIF orientation.
- `Format`: The output format of `WriteOptions`; it overrides the output
  extension.
- `Quality`: JPEG quality of `WriteOptions` (default {{qualityDefault}}).
- `NoClobber`: With `WriteOptions.NoClobber`, writing fails with output-exists
  instead of replacing an existing output.
- The pipeline operations (crop, resize, fit, rotate, flip, and pad) and the
  photo operations are the `Operations` of the render.

## Diagnostics

- None: the diagnostics of a raster image are always empty and strict mode has
  no effect.
- `unsupported-feature`: Error code: a valid file that uses a feature the
  decoder lacks, for example an interlaced PNG.
- `corrupt-input`: Error code: a damaged or truncated file.
- `unsupported-format`: Error code: the file is not a format CanvasNet reads.

## Known differences

- JPEG output is lossy, so decoded pixels differ from the source.
- ICC profiles are not applied; an embedded JPEG 2000 profile is reported by the
  library, not applied.
- GIF and JPEG 2000 can be read but not written.
