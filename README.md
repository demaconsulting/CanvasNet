# CanvasNet

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

- 🖼️ **Pixel Buffer** - Mutable 32-bit RGBA `Surface` with span-based row access
- ✂️ **Cropping** - Independent-copy cropping for load/crop/save workflows
- 🌈 **Compositing** - Vectorized alpha premultiply/unpremultiply and Porter-Duff "over"
  compositing (surface-over-surface and surface-over-constant-color)
- 📀 **BMP Codec** - Load and save 24-bit and 32-bit uncompressed Windows BMP files
- 🎨 **PNG Codec** - Load and save 8-bit Truecolor (RGB) and Truecolor-with-alpha (RGBA) PNG files
- 🖨️ **TIFF Codec** - Load and save 8-bit RGB/RGBA/Grayscale TIFF files with PackBits/LZW/Deflate
- 🗜️ **JPEG Codec** - Load baseline/progressive JPEG and save baseline 4:2:0 JPEG with quality control
- 🔍 **Header-Only Probing** - `GetInfo` reads only image headers (dimensions/channels/alpha) without
  decoding pixel data, letting callers triage untrusted files before calling `Load`
- 🖌️ **Path Filling** - Antialiased nonzero/even-odd fill of closed vector paths onto a `Surface`
- 🖊️ **Stroke-to-Fill** - Convert stroked vector paths (caps, joins, dashes, miter limits) into
  fillable outline geometry and render them through the same antialiased fill pipeline
- ⚡ **Span-Based** - Fast, allocation-conscious row and pixel access
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

var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(255, 0, 0, 255); // set a red, opaque pixel
var cropped = surface.Crop(0, 0, 2, 2);     // independent 2x2 copy

BmpCodec.Save(surface, "surface.bmp");        // save as a 32-bit BMP file
var reloaded = BmpCodec.Load("surface.bmp"); // load it back

PngCodec.Save(surface, "surface.png");        // save as an RGBA PNG file
var reloadedPng = PngCodec.Load("surface.png"); // load it back

TiffCodec.Save(surface, "surface.tiff");        // save as an RGBA TIFF file
var reloadedTiff = TiffCodec.Load("surface.tiff"); // load it back

JpegCodec.Save(surface, "surface.jpg", 90);        // save as a baseline JPEG file
var reloadedJpeg = JpegCodec.Load("surface.jpg"); // load it back

// Triage an untrusted file's header before decoding pixel data:
var info = PngCodec.GetInfo("untrusted.png"); // reads only the header, never decodes IDAT
if (info.Width > Surface.MaxDimension
    || info.Height > Surface.MaxDimension
    || (long)info.Width * info.Height > (long)Surface.MaxDimension * Surface.MaxDimension)
{
    throw new InvalidDataException("Image dimensions exceed the supported maximum.");
}

var safeSurface = PngCodec.Load("untrusted.png"); // safe to decode fully
```

Filling a vector path onto a surface:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

var canvas = new Surface(64, 64);
var triangle = new PathBuilder()
    .MoveTo(new Vector2(8, 56))
    .LineTo(new Vector2(56, 56))
    .LineTo(new Vector2(32, 8))
    .Close()
    .Build();

PathFiller.Fill(canvas, triangle, new Rgba32(0, 128, 255, 255)); // antialiased solid fill
```

Stroking a vector path onto a surface:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using System.Numerics;

var canvas = new Surface(64, 64);
var polyline = new PathBuilder()
    .MoveTo(new Vector2(8, 48))
    .LineTo(new Vector2(32, 16))
    .LineTo(new Vector2(56, 48))
    .Build();

var style = new StrokeStyle(
    width: 6f,
    cap: LineCap.Round,
    join: LineJoin.Round,
    dashArray: [10f, 6f]);

var strokedOutline = PathStroker.Stroke(polyline, style);
PathFiller.Fill(canvas, strokedOutline, new Rgba32(255, 128, 0, 255));
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
