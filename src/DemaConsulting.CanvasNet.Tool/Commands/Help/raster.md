---
extends: raster
examples:
  - name: convert
    kind: command
    language: shell
    content: 'canvasnet convert scan.tif -o scan.png --no-clobber'
    description: Convert a TIFF scan to PNG; fail instead of replacing an existing file.
  - name: to-jpeg
    kind: command
    language: shell
    content: 'canvasnet render shot.png -o shot.jpg --quality 85 --json'
    description: Write a PNG as a JPEG at quality 85 and print the JSON result.
  - name: fit
    kind: command
    language: shell
    content: 'canvasnet render photo.jpg --fit 1600x1600 -o photo.png'
    description: Shrink a photo to fit inside 1600x1600 pixels.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info photo.jpg --json'
    description: Report the size, channels, and EXIF orientation without decoding the pixels.
---

## Command line

- `--scale <factor>`: Multiply the size by a factor; --width, --height, and
  --size resample to a size.
- `--filter <name>`: Resampling filter: auto (default), nearest, bilinear,
  bicubic, lanczos3, or area.
- `--background <color>`: The color transparency is flattened over, for example
  for a JPEG output.
- `--no-auto-orient`: Do not turn a JPEG upright by its EXIF orientation.
- `--format <format>`: Output format (png, jpeg, jpg, bmp, tiff, or tif);
  overrides the output extension.
- `--quality <1-100>`: JPEG quality (default {{qualityDefault}}).
- `--no-clobber`: Fail with output-exists instead of replacing an existing
  output.
- The pipeline operations --crop, --resize, --fit, --rotate, --flip, and --pad,
  and the photo operations, are described by 'canvasnet help render'; 'canvasnet
  info' reports the frame count of a GIF. --strict has no effect on a raster
  image.
