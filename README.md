# CanvasNet

<!-- cspell:ignore SFNT codepoints Noto Zapf Visio -->
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
image operations using `Span<T>`, and supports independent-copy cropping and high-quality resizing for
load/crop/resize/save workflows.

## Features

- 🖼️ **Pixel Buffer** - Mutable 32-bit RGBA surface with span access
- ✂️ **Cropping** - Independent-copy crop for load/crop/save workflows
- 📐 **Resizing** - `Resize`/`ResizeToFit` with Nearest, Bilinear, Bicubic, Lanczos3 and Area filters
- 🌈 **Compositing** - Alpha premultiply and Porter-Duff "over" compositing
- 📀 **BMP Codec** - Load/save 24-bit/32-bit uncompressed BMP files
- 🎨 **PNG Codec** - Load non-interlaced PNGs; save 8-bit RGB/RGBA
- 🖨️ **TIFF Codec** - Load/save 8-bit RGB/RGBA/Grayscale TIFF files
- 🗜️ **JPEG Codec** - Load baseline/progressive; save baseline JPEG
- 🎞️ **GIF Codec** - Decode-only load of first GIF frame; `GetInfo` reports the true frame count
- 🖼️ **JPEG 2000 Codec** - Decode-only load of JP2 files and raw codestreams
- 📐 **SVG Codec** - Rasterize a common SVG subset to a surface
- 📄 **PDF Document** - Open, inspect, and rasterize PDF pages with font substitution
- 📽️ **PowerPoint Rendering** - Rasterize `.pptx` slides to a surface
- 📊 **Chart Rendering** - Build and paint Bar/Column/Line/Area/Pie/Doughnut charts
- 📈 **OOXML Chart Parsing** - Parse ECMA-376 DrawingML charts, including from `.pptx`
- 🔷 **Visio Rendering** - Rasterize `.vsdx` diagrams; unsupported constructs are reported
- 🧊 **3D Rendering** - Rasterize meshes and scenes with camera, lights, antialiasing, and CSG
  (union/difference/intersection), plus polygon extrusion (linear/rotational)
- 🧩 **OpenSCAD-Language Rendering** - Parse, evaluate, and render `.scad` models (primitives, transforms,
  booleans, extrusions, `-D` variables) straight to a surface; unsupported constructs are reported
- 🔍 **Header-Only Probing** - `GetInfo` reads headers without decoding pixels (GIF excepted)
- 🖌️ **Path Filling** - Antialiased nonzero/even-odd fill of vector paths
- 🖊️ **Stroke-to-Fill** - Convert stroked paths into fillable outlines
- 🌅 **Gradient Paint** - Linear or radial gradient fills with spread
- 🧱 **Tile Paint** - Fill a path by repeating a pre-rendered tile bitmap at a configurable pitch
- 🔤 **TrueType/CFF Fonts** - Load fonts and collections, extract glyph outlines, query metadata
- 🗂️ **System Font Discovery** - Match a family/style against installed fonts, with a bundled fallback
- 🎬 **Rendering** - Transform-aware canvas with text and shape drawing
- ⚡ **Span-Based** - Fast, allocation-conscious pixel and row access
- 🔄 **Multi-Target** - Supports .NET 8, 9, and 10
- 📦 **NuGet Ready** - Easy integration via NuGet package

## Installation

Install the core package with the .NET CLI (or the equivalent `Install-Package` command in the
Package Manager Console):

```bash
dotnet add package DemaConsulting.CanvasNet
```

Each optional file-format/rendering feature ships as its own additional package - install only
the ones you need, the same way as above:

| Package                             | Adds                                             |
|-------------------------------------|--------------------------------------------------|
| `DemaConsulting.CanvasNet.Svg`      | SVG rasterization                                |
| `DemaConsulting.CanvasNet.Pdf`      | PDF document rendering                           |
| `DemaConsulting.CanvasNet.Charts`   | Chart building/rendering and OOXML chart parsing |
| `DemaConsulting.CanvasNet.Pptx`     | PowerPoint (`.pptx`) document rendering          |
| `DemaConsulting.CanvasNet.Vsdx`     | Visio (`.vsdx`) diagram rendering                |
| `DemaConsulting.CanvasNet.Render3D` | 3D mesh/scene rasterization                      |
| `DemaConsulting.CanvasNet.Scad`     | OpenSCAD-language model rendering                |

## Usage

Create a surface, crop it, and round-trip it through a codec:

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

// Create a surface, set a pixel, and crop an independent copy
using var surface = new Surface(4, 4);
surface[1, 1] = new Rgba32(255, 0, 0, 255);
using var cropped = surface.Crop(0, 0, 2, 2);

// Save as PNG and load it back
PngCodec.Save(surface, "surface.png");
using var reloaded = PngCodec.Load("surface.png");

// Load, crop, resize to fit a 64x64 box, and save
using var photo = PngCodec.Load("photo.png");
using var region = photo.Crop(10, 10, 200, 100);
using var thumbnail = region.ResizeToFit(64, 64, ResamplingMode.Lanczos3);
PngCodec.Save(thumbnail, "thumbnail.png");
```

Open a PDF and render a page (BMP/TIFF/JPEG/GIF/JPEG 2000/SVG codecs, header-only probing, and
feature-detection all follow the same pattern):

```csharp
using DemaConsulting.CanvasNet.Pdf;

// Render paints the page's real content-stream geometry: path fills/strokes, shadings and tiling
// patterns, placed image XObjects, and text (using the page's embedded font, or an
// automatically substituted system/bundled fallback font when none is embedded). Device,
// CalGray/CalRGB/Lab, ICCBased, Indexed, Separation and DeviceN colors are converted to sRGB.
using var pdfDoc = PdfDocument.Open("document.pdf");
var pageInfo = pdfDoc.GetPageInfo(0);
using var pdfSurface = pdfDoc.Render(0, pageInfo.Width, pageInfo.Height);
```

Render a PowerPoint slide and a Visio page (both report constructs they cannot draw rather than
skipping them silently):

```csharp
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Vsdx;

// EMF/WMF pictures on a slide are skipped; the callback reports each one
using var deck = PptxDocument.Open("slides.pptx");
var pptxOptions = new PptxRenderOptions
{
    OnUnsupportedPictureSkipped = ex => Console.WriteLine($"Skipped picture: {ex.Message}"),
};
using var slide = deck.Render(0, 96f, pptxOptions);

// Each recognized-but-unsupported Visio construct is reported with a stable feature token
using var diagram = VsdxDocument.Open("diagram.vsdx");
var vsdxOptions = new VsdxRenderOptions
{
    UnsupportedFeatureHandler = f => Console.WriteLine($"{f.Feature}: {f.Message}"),
};
using var page = diagram.Render(0, 150, vsdxOptions);
```

Fill a vector path onto a surface (stroking, gradients, tile paint, fonts, and charts use the same
`Drawing`/`Geometry` API):

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

Render a 3D scene (a pure-managed software rasterizer with camera, lights, antialiasing, and
parallel tile rendering):

```csharp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

// A gray plate with a red sphere on it; node colors are optional and default to neutral gray
var plate = new MeshNode(PrimitiveTessellator.Box(new Vector3D(10, 10, 1), center: true))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, -0.5)),
};
var ball = new MeshNode(PrimitiveTessellator.Sphere(2, 48))
{
    Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 2)),
    Appearance = new NodeAppearance(new Rgba32(200, 40, 40, 255)),
};
var scene = new Scene(new GroupNode([plate, ball]));

// Render an automatically framed isometric view and save it
using var image = SceneRenderer.RenderFitted(scene, 640, 480);
PngCodec.Save(image, "scene.png");
```

Render an OpenSCAD-language model (the `DemaConsulting.CanvasNet.Scad` package parses and evaluates the
source, builds the solids with `CanvasNetRender3D`, and draws them; it is an independent, clean-room
implementation of the language, not affiliated with the OpenSCAD project):

```csharp
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Scad;

// Parse a model (or ScadDocument.Open("part.scad") to read a file) and render a fitted isometric view
var document = ScadDocument.Parse("""
    size = 20;
    difference() {
        cube([size, size, 6], center = true);
        cylinder(h = 8, r = size / 5, center = true, $fn = 48);
    }
    """);
using var image = document.Render(640, 480);
PngCodec.Save(image, "part.png");

// Override variables, and be told about anything that could not be built instead of losing it silently
var options = new ScadRenderOptions
{
    Evaluation = new ScadEvaluationOptions
    {
        Variables = new Dictionary<string, object?> { ["size"] = 30.0 },
        UnsupportedFeatureHandler = feature => Console.WriteLine(feature),
    },
};
using var variant = document.Render(640, 480, options);
```

See the [User Guide][link-releases] for the full API reference and additional worked examples,
including stroking, gradients, tile paint, font loading/metadata, chart building, OOXML chart
parsing, PDF, PowerPoint and Visio rendering, 3D scene rendering, OpenSCAD-language model rendering,
and SVG and JPEG 2000 decoding.

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

The `DemaConsulting.CanvasNet` package bundles the Liberation Sans, Liberation Serif, and
Liberation Mono TrueType fonts (12 files total) as embedded resources, used as a last-resort
fallback font by `PdfDocument`'s automatic font-substitution feature. These fonts are Copyright
(c) 2012 Red Hat, Inc., licensed under the SIL Open Font License, Version 1.1; see
`src/DemaConsulting.CanvasNet/Fonts/BundledFonts/OFL.txt` and
`src/DemaConsulting.CanvasNet/Fonts/BundledFonts/README.md` for the full license text and
sourcing/provenance details.

The `DemaConsulting.CanvasNet` package also bundles the Noto Sans, Noto Sans Math, and Noto Sans
Symbols 2 TrueType fonts (`NotoSans-Regular.ttf`, `NotoSansMath-Regular.ttf`,
`NotoSansSymbols2-Regular.ttf`) as embedded resources, used as the dedicated substitute font for
`Symbol`/`ZapfDingbats` text by `PdfDocument`'s automatic font-substitution feature. These fonts
are part of the Noto Project, licensed under the SIL Open Font License, Version 1.1; see
`src/DemaConsulting.CanvasNet/Fonts/BundledFonts/NotoFonts-OFL.txt` and the "Noto Substitute
Fonts" section of `src/DemaConsulting.CanvasNet/Fonts/BundledFonts/README.md` for the full license
text and sourcing/provenance details.

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
