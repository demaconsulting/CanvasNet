# TIFF Test Fixtures

These TIFF files were generated with [ImageMagick](https://imagemagick.org/) 7.1.2 from two files
in the `PngSuite` conformance corpus (`basn2c08.png`, an RGB image, and `basn6a08.png`, an RGBA
image), exercising the byte-order, compression, and photometric combinations `TiffCodec` supports:

| File | Source | Byte Order | Compression | Photometric |
| ------ | -------- | ------------ | ------------- | ------------- |
| `rgb_none_le.tiff` | `basn2c08.png` | Little-endian (II) | None | RGB |
| `rgb_none_be.tiff` | `basn2c08.png` | Big-endian (MM) | None | RGB |
| `rgb_lzw.tiff` | `basn2c08.png` | Little-endian (II) | LZW | RGB |
| `rgb_packbits.tiff` | `basn2c08.png` | Little-endian (II) | PackBits | RGB |
| `rgb_deflate.tiff` | `basn2c08.png` | Little-endian (II) | Deflate | RGB |
| `rgba_none.tiff` | `basn6a08.png` | Little-endian (II) | None | RGBA |
| `rgba_lzw.tiff` | `basn6a08.png` | Little-endian (II) | LZW | RGBA |
| `gray_none.tiff` | `basn2c08.png` (converted to grayscale) | Little-endian (II) | None | Grayscale |
| `gray_lzw.tiff` | `basn2c08.png` (converted to grayscale) | Little-endian (II) | LZW | Grayscale |

Generated with, for example:

```pwsh
magick basn2c08.png -endian LSB -compress None rgb_none_le.tiff
magick basn2c08.png -compress LZW rgb_lzw.tiff
magick basn2c08.png -colorspace Gray -compress None gray_none.tiff
```

Since TIFF loading/saving is lossless, tests compare decoded pixels for exact equality against
the known source pixel data.
