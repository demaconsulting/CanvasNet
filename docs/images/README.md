# Documentation Images

This folder holds the images used by the project README and the tool user guide.

## composition.png

`composition.png` (1440x1080) shows composition across formats: one SVG page, `showcase/composition.svg` (960x720,
drawn at 1.5 times its size), whose pictures are other files that CanvasNet draws at the size of their boxes. It is
referenced from the *Composition* section of the README by an absolute URL, like `showcase.png`.

- The top panel embeds the Mermaid flowchart `showcase/pipeline.mmd` (`<image href="pipeline.mmd"/>`), whose picture
  nodes show the OpenSCAD model `showcase/hull.scad` and the Vega-Lite chart `showcase/output.vl.json`
  (`part@{ img: "hull.scad", ... }`): three levels of nesting, SVG, then Mermaid, then OpenSCAD and Vega-Lite. Every
  picture node has its `w` and `h`, so the layout does not depend on the pictures.
- The bottom left panel links the OpenSCAD assembly `showcase/explode.scad` twice, at the explode levels 0 and 1,
  chosen by the fragment of each link (in the SVG file,
  `explode.scad#def.explode=1&amp;def.$vpd=330&amp;def.$vpt=[0,0,46]`). The
  fixed camera (`def.$vpd`, `def.$vpt`) gives both pictures one scale.
- The bottom right panel links the Mermaid sequence diagram `showcase/release.mmd` with the dark theme chosen by its
  link (`release.mmd#theme=dark`).

The text of the page uses only the generic font families `sans-serif` and `monospace`, which are always the bundled
Liberation fonts, and Mermaid and Vega-Lite always use the bundled fonts, so the picture is the same on every machine.
`composition.svg`, `pipeline.mmd`, `output.vl.json`, and `release.mmd` are **original to this repository** (MIT), and
so are the reused `hull.scad` and `explode.scad`. `render-images.ps1` draws it with `--strict`, so a link that stops
working fails the script, and checks it like the other images.

## mermaid-gallery.png

`mermaid-gallery.png` (1248x680) is a montage of six Mermaid diagram types (a sequence diagram, a class diagram, a
state diagram, a gantt chart, a mindmap, and a git graph), each drawn by `canvasnet render --strict` at twice its size
and shown at 400x300. It is referenced from the README by an absolute URL, like `showcase.png`. The diagrams are the
user guide examples `mermaid-sequence-arrows.mmd`, `mermaid-class-relations.mmd`, `mermaid-state-notes.mmd`,
`mermaid-gantt.mmd`, `mermaid-mindmap.mmd`, and `mermaid-gitgraph.mmd` in `docs/user_guide/examples`, which are
**original to this repository** (MIT). `render-images.ps1` draws and checks the montage like the other images.

## montage-explode.png

`montage-explode.png` (888x366) is the picture of the `canvasnet montage` example of the tool user guide: the
OpenSCAD assembly `showcase/explode.scad` (original, authored for this repository, MIT) three times, at the explode
levels 0, 0.5 and 1, chosen by the fragment of each input path (`explode.scad#def.explode=0.5`). A fixed camera
(`-D '$vpd=360' -D '$vpt=[0,0,50]'`) gives every cell one scale, so the plate is as large in all three cells; without
it each cell would be fitted to its own geometry. `render-images.ps1` draws and checks it like `showcase.png`.

## vegalite-gallery.png

`vegalite-gallery.png` (1248x680) is a montage of six charts of the Vega-Lite gallery (a line chart, stacked bars, a
binned heat map, a donut, a density estimate, and a themed chart), each drawn by `canvasnet render --strict` at twice
its size and shown at 400x300. It is referenced from the README by an absolute URL, like `showcase.png`. The gallery has
18 charts in all; they are in `docs/user_guide/examples/gallery`, are drawn in the user guide by the Pandoc filter,
and are rendered by a test (`VegaLiteGalleryTests`) so that none of them can stop working. `render-images.ps1` draws
and checks the montage like the other images.

Every gallery specification, and the two small SVG pictures (`gallery-badge.svg` and `gallery-flag.svg`) that its image
chart shows, is **original to this repository** (MIT): the data is invented (a few inline rows, or a `sequence` and
`calculate` transforms), and no example of the Vega-Lite project and no Vega dataset is copied.

## showcase.png

`showcase.png` (1340x560) is a montage of eight panels in a 4x2 grid, each rendered by the `canvasnet` command-line
tool: SVG, OpenSCAD 3D, Vega-Lite, Mermaid, PDF, PowerPoint, Visio, and a composition of several formats on one SVG
page. It is referenced from the README by an absolute URL, because the README is also shipped inside the
NuGet packages.

### Provenance and Licensing

Every input is either original to this repository (licensed under the repository's MIT license) or a
permissively licensed third-party file that is credited below.

- **SVG**: `showcase/dashboard.svg` - original, authored for this repository (MIT)
- **OpenSCAD 3D**: `showcase/hull.scad` - original, authored for this repository (MIT)
- **Vega-Lite**: `showcase/sales.vl.json` - original, authored for this repository (MIT)
- **Mermaid**: `showcase/flowchart.mmd` - original, authored for this repository (MIT)
- **PDF**: `showcase/mesh-shading.pdf` - copy of the hand-authored test fixture
  `shading-mesh-types-4-5-6-7.pdf` (MIT)
- **PowerPoint**: `showcase/slide.pptx` - original, generated by `showcase/make-slide.py` with python-pptx (MIT)
- **Visio**: `davehoward-test4-connectors.vsdx` (not copied here) - third-party, BSD 3-Clause; see the notice below
- **Composition**: `showcase/composition.svg` and the files it links (see *composition.png* above) - original,
  authored for this repository (MIT)

The Visio panel renders `test4_connectors.vsdx` from the
[dave-howard/vsdx](https://github.com/dave-howard/vsdx) project, which is kept unmodified as
`test/DemaConsulting.CanvasNet.Vsdx.Tests/VsdxFixtures/davehoward-test4-connectors.vsdx`. That project is
distributed under the BSD 3-Clause License:

```text
BSD 3-Clause License

Copyright (c) 2020, Dave Howard
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

### Regenerating

`render-images.ps1` in this folder is the one place that knows how each image is drawn. It needs the `canvasnet`
tool (the command `dotnet canvasnet`, from a tool manifest or installed with
`dotnet tool install DemaConsulting.CanvasNet.Tool`), and runs from any folder:

```powershell
# Draw every image again and replace the committed files; commit the images that changed
pwsh docs/images/render-images.ps1

# Draw every image again and compare it with the committed file, without changing anything
pwsh docs/images/render-images.ps1 -Check
```

To use a freshly built tool instead of an installed one, name its command with `-ToolCommand` or the
`CANVASNET_TOOL` environment variable (a program and optional leading arguments), for example
`dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll`.

For `showcase.png`, each panel is rendered at 800x600 and shown at 320x240. The `--labels` option of
`canvasnet montage` prints the input file name, so the script copies the panels to extensionless files named after
their labels before it builds the montage. To rebuild the PowerPoint input, run `python make-slide.py` in
`docs/images/showcase` (it needs `pip install python-pptx`).

### Checking That the Images Are Current

`-Check` fails (exit code 1) when a committed image is missing, has a different size, or differs from a fresh
drawing by more than a tolerance. It runs in the documents job of the build workflow, so a change that alters how
a panel looks cannot be merged with a stale image. It compares with
`canvasnet compare <committed> <fresh> --threshold rmse=1 --threshold diff-ratio=0.001 --json` rather than
byte for byte, because a drawing is not bit-for-bit the same on every operating system: a last-bit difference in
floating point moves a few anti-aliased pixels, and installed fonts can differ.

- `rmse=1` is the root-mean-square difference of the color levels (0-255). A one-degree change of the 3D view
  gives about 3.8, a one-level change of a color about 0.08, and a different resampling filter about 0.2.
- `diff-ratio=0.001` allows 0.1% of the pixels to differ by more than 8 levels in any color. A one-degree change
  of the 3D view alters 0.35% of them.

When the check fails it prints the metrics and keeps the fresh image and a side-by-side picture in a temporary
folder. If the change is intended, run the script without `-Check` and commit the new image.

Notes:

- The chart colors bars by region on the x axis, which already names each bar, so its legend is switched off with
  `legend: null`.
- The Vega-Lite, Mermaid, PDF, PowerPoint, and Visio panels are padded with white to exactly 4:3 so that every cell
  has the same shape; the SVG, OpenSCAD, and Composition panels are drawn at 800x600 directly.
