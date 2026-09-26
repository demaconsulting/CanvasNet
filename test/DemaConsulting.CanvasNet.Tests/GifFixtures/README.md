# GIF Test Fixtures

These 8 real-world GIF files were downloaded from
[samplelib.com's sample GIF page](https://samplelib.com/sample-gif.html), exercising
`GifCodec.Load` against files produced by a real, independent GIF encoder rather than hand-built
byte streams.

| File | Dimensions | Frames | Purpose |
| ------ | ------------ | -------- | --------- |
| `sample-red-400x300.gif` | 400x300 | 1 | Solid-color fixture; no GCE (only a Comment extension) |
| `sample-red-200x200.gif` | 200x200 | 1 | Solid-color fixture |
| `sample-green-400x300.gif` | 400x300 | 1 | Solid-color fixture |
| `sample-green-200x200.gif` | 200x200 | 1 | Solid-color fixture |
| `sample-blue-400x300.gif` | 400x300 | 1 | Solid-color fixture |
| `sample-animated-400x300.gif` | 400x300 | 3 | Multi-frame (animated) fixture |
| `sample-animated-200x200.gif` | 200x200 | 3 | Multi-frame (animated) fixture |
| `sample-animated-100x75.gif` | 100x75 | 3 | Multi-frame (animated) fixture |

Per samplelib.com's stated license: these sample files are completely free to use, with no
licensing restrictions - you are allowed to do whatever you want with them, for commercial or
non-commercial purposes, and no attribution is required.

Each solid-color fixture's Global Color Table entries are read directly from the file's own bytes
by the tests that use them (rather than assumed to be pure primaries), since real-world GIF
encoders commonly quantize colors slightly - for example `sample-red-400x300.gif`'s non-background
color table entry is `(254, 0, 0)`, not `(255, 0, 0)`.

None of these 8 fixtures has its Image Descriptor's interlace bit set, so GIF interlacing is
verified separately with a hand-built byte-array fixture in `GifCodecTests.cs`.
