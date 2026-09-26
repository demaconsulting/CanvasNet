# CanvasNet

<!-- cspell:ignore SFNT codepoints -->
<!-- IMPORTANT: All links in this file must be absolute URLs.
     This file is distributed in packages and relative links will not resolve. -->

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

.NET canvas library for loading, saving, and cropping images

## Overview

CanvasNet is a .NET library providing a mutable, span-based 32-bit RGBA pixel buffer along with
codecs for loading and saving images in common file formats. It's designed for fast, allocation-conscious
image operations using `Span<T>`, and supports independent-copy cropping for load/crop/save workflows.

## Features

- 🖼️ **Pixel Buffer** - Mutable 32-bit RGBA surface with span access
- ✂️ **Cropping** - Independent-copy crop for load/crop/save workflows
- 🌈 **Compositing** - Alpha premultiply and Porter-Duff "over" compositing
- 📀 **BMP Codec** - Load/save 24-bit/32-bit uncompressed BMP files
- 🎨 **PNG Codec** - Load non-interlaced PNGs; save 8-bit RGB/RGBA
- 🖨️ **TIFF Codec** - Load/save 8-bit RGB/RGBA/Grayscale TIFF files
- 🗜️ **JPEG Codec** - Load baseline/progressive; save baseline JPEG
- 🎞️ **GIF Codec** - Decode-only load of first GIF frame; `GetInfo` reports the true frame count
- 📐 **SVG Codec** - Rasterize a common SVG subset to a surface
- 🔍 **Header-Only Probing** - `GetInfo` reads headers without decoding pixels (GIF excepted)
- 🖌️ **Path Filling** - Antialiased nonzero/even-odd fill of vector paths
- 🖊️ **Stroke-to-Fill** - Convert stroked paths into fillable outlines
- 🌅 **Gradient Paint** - Linear or radial gradient fills with spread
- 🔤 **TrueType Fonts** - Load fonts, map codepoints, extract glyph outlines
- 🎬 **Rendering** - Transform-aware canvas with text and shape drawing
- ⚡ **Span-Based** - Fast, allocation-conscious pixel and row access
- 🔄 **Multi-Target** - Supports .NET 8, 9, and 10
- 📦 **NuGet Ready** - Easy integration via NuGet package

## Installation

```bash
dotnet add package DemaConsulting.CanvasNet
```

Or via Package Manager Console:

```powershell
Install-Package DemaConsulting.CanvasNet
```

## Usage

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using System.IO;

// Create a surface, set a pixel, and crop an independent copy
using var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(255, 0, 0, 255);
using var cropped = surface.Crop(0, 0, 2, 2);

// Save as BMP and load it back
BmpCodec.Save(surface, "surface.bmp");
using var reloaded = BmpCodec.Load("surface.bmp");

// Save as PNG and load it back
PngCodec.Save(surface, "surface.png");
using var reloadedPng = PngCodec.Load("surface.png");

// Save as TIFF and load it back
TiffCodec.Save(surface, "surface.tiff");
using var reloadedTiff = TiffCodec.Load("surface.tiff");

// Save as JPEG and load it back
JpegCodec.Save(surface, "surface.jpg", 90);
using var reloadedJpeg = JpegCodec.Load("surface.jpg");

// Decode-only: load the first frame of a GIF
using var reloadedGif = GifCodec.Load("surface.gif");

// GetInfo also reports a GIF's true total frame count; it decodes the first frame's
// LZW-compressed pixel data to validate CanDecode, but never resolves those pixels into a
// rendered Surface
var gifInfo = GifCodec.GetInfo("surface.gif");
Console.WriteLine($"Frames: {gifInfo.FrameCount}");

// Decode/rasterize an SVG into a 256x256 surface
using var rasterized = SvgCodec.Load("icon.svg", 256, 256);

// Triage an untrusted file's header before decoding pixel data
var info = PngCodec.GetInfo("untrusted.png");
if (info.Width > Surface.MaxDimension
    || info.Height > Surface.MaxDimension
    || (long)info.Width * info.Height > (long)Surface.MaxDimension * Surface.MaxDimension)
{
    throw new InvalidDataException("Image dimensions exceed the supported maximum.");
}

// Reject files that declare a feature the codec cannot decode
if (!info.CanDecode)
{
    throw new UnsupportedImageFeatureException(
        "png-adam7-interlace",
        "File is well-formed but declares an unsupported feature.");
}

// Now safe to decode fully
using var safeSurface = PngCodec.Load("untrusted.png");
```

Filling a vector path onto a surface:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

using var canvas = new Surface(64, 64);

// Build a triangular path
var triangle = new PathBuilder()
    .MoveTo(new Vector2(8, 56))
    .LineTo(new Vector2(56, 56))
    .LineTo(new Vector2(32, 8))
    .Close()
    .Build();

// Fill the triangle with an antialiased solid color
PathFiller.Fill(canvas, triangle, new Rgba32(0, 128, 255, 255));
```

Stroking a vector path onto a surface:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

using var canvas = new Surface(64, 64);

// Build a zig-zag polyline
var polyline = new PathBuilder()
    .MoveTo(new Vector2(8, 48))
    .LineTo(new Vector2(32, 16))
    .LineTo(new Vector2(56, 48))
    .Build();

// Define a round-capped, round-joined, dashed stroke style
var style = new StrokeStyle(
    width: 6f,
    cap: LineCap.Round,
    join: LineJoin.Round,
    dashArray: [10f, 6f]);

// Convert the stroke to fillable outline geometry and fill it
var strokedOutline = PathStroker.Stroke(polyline, style);
PathFiller.Fill(canvas, strokedOutline, new Rgba32(255, 128, 0, 255));
```

Filling a vector path with a linear gradient:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

using var canvas = new Surface(64, 64);

// Build a square path
var rectangle = new PathBuilder()
    .MoveTo(new Vector2(4, 4))
    .LineTo(new Vector2(60, 4))
    .LineTo(new Vector2(60, 60))
    .LineTo(new Vector2(4, 60))
    .Close()
    .Build();

// Define a red-to-blue horizontal gradient
var gradient = new LinearGradient(
    start: new Vector2(4, 0),
    end: new Vector2(60, 0),
    stops:
    [
        new GradientStop(0f, new Rgba32(255, 0, 0, 255)),
        new GradientStop(1f, new Rgba32(0, 0, 255, 255))
    ]);

// Fill the square with the gradient
PathFiller.Fill(canvas, rectangle, gradient, FillRule.NonZero, 1f);
```

Loading a TrueType font and filling a glyph outline:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

// Convert a glyph outline from font units (Y up) to canvas space (Y down)
static Path TransformGlyph(Path glyph, float scale, float baselineY)
{
    var builder = new PathBuilder();

    Vector2 ToCanvas(Vector2 point) => new(point.X * scale, baselineY - point.Y * scale);

    foreach (var subpath in glyph.Subpaths)
    {
        builder.MoveTo(ToCanvas(subpath.Start));
        foreach (var command in subpath.Commands)
        {
            switch (command.Type)
            {
                case PathCommandType.LineTo:
                    builder.LineTo(ToCanvas(command.EndPoint));
                    break;
                case PathCommandType.QuadraticBezierTo:
                    builder.QuadraticBezierTo(
                        ToCanvas(command.Control1),
                        ToCanvas(command.EndPoint));
                    break;
                case PathCommandType.Close:
                    builder.Close();
                    break;
            }
        }
    }

    return builder.Build();
}

// Load the font and get glyph 'A' scaled to a 48px em size
var font = TrueTypeFont.Load("font.ttf");
var glyphIndex = font.GetGlyphIndex('A');
var glyphOutline = font.GetGlyphOutline(glyphIndex);
var scale = 48f / font.UnitsPerEm;
var canvasOutline = TransformGlyph(glyphOutline, scale, baselineY: 56f);

// Fill the transformed glyph outline
using var surface = new Surface(64, 64);
PathFiller.Fill(surface, canvasOutline, new Rgba32(20, 120, 255, 255));
```

## Building

```pwsh
pwsh ./build.ps1
```

## API Documentation

Detailed API documentation for all public types and members is distributed in the `api/` folder
of the NuGet package.

## User Guide

The CanvasNet User Guide is available on the
[CanvasNet releases page][link-releases].

## Contributing

Contributions are welcome. See [CONTRIBUTING.md][link-contributing] for development setup, coding
standards, and the pull request process.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

## Support

- [Report a bug or request a feature][link-issues]
- [Ask a question or start a discussion][link-discussions]

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/CanvasNet?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/CanvasNet?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/CanvasNet?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/CanvasNet?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/CanvasNet/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_CanvasNet&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_CanvasNet&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.CanvasNet?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/CanvasNet/network/members
[link-stars]: https://github.com/demaconsulting/CanvasNet/stargazers
[link-contributors]: https://github.com/demaconsulting/CanvasNet/graphs/contributors
[link-license]: https://github.com/demaconsulting/CanvasNet/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/CanvasNet/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_CanvasNet
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_CanvasNet
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.CanvasNet
[link-contributing]: https://github.com/demaconsulting/CanvasNet/blob/main/CONTRIBUTING.md
[link-releases]: https://github.com/demaconsulting/CanvasNet/releases
[link-issues]: https://github.com/demaconsulting/CanvasNet/issues
[link-discussions]: https://github.com/demaconsulting/CanvasNet/discussions
