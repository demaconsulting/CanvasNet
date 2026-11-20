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
