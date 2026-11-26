# Documentation Images

This folder holds the images used by the project README and the tool user guide.

## architecture.png

`architecture.png` (1800x1260) is the package architecture diagram: the core package, the nine format packages and the
three shared building blocks they use, the Viewer, and the `canvasnet` tool, with arrows from each package to the
packages it references. Its source is `architecture.svg` (original to this repository, MIT), which the user guide and
the design document embed directly; the README shows the PNG by an absolute URL, like `showcase.png`, because the
README is also shipped inside the NuGet packages. `render-images.ps1` draws it with `--strict` on a transparent
background, so its white card reads on light and dark pages, and checks it like the other images. Update the SVG,
and run the script, whenever a package or a project reference is added or removed.

## composition-explained.png

`composition-explained.png` (1600x900) explains composition: an exploded stack of three layers (the SVG page
`composition-explained/page.svg`, the Mermaid diagram `flow.mmd` it embeds, and the PlantUML state diagram
`release.puml`, OpenSCAD model `part.scad`, and Vega-Lite chart `chart.vl.json` that the diagram's nodes embed),
numbered cards with the markup of each link, and the finished page. It is referenced from the *Composition* section of
the README by an absolute URL. The diagram layer and the bottom layer (the PlantUML diagram, the model, and the chart)
are live drawings of their files under an SVG transform; the top layer is a manual copy of the content of `page.svg`,
so a change to the page must be repeated there (the figure's header comment explains how its coordinates follow
`page.svg`). The slots of the picture nodes on the lower layers, and the end of the page's "Goal met" arrow, are taken
from `canvasnet render "flow.mmd#theme=dark" --size 608x234 --positions "node:*" --json`, moved by the corner of the
diagram in the page (16, 96). `render-images.ps1` draws it with `--strict` on a transparent background, with file
access confined to its folder.

Every file in `composition-explained/` is **original to this repository** (MIT): `composition-explained.svg`,
`page.svg`, and `release.puml` were authored for it, `part.scad` is a copy of `showcase/explode.scad`, and `flow.mmd`
and `chart.vl.json` are adapted from `showcase/pipeline.mmd` and `showcase/output.vl.json`.

## showcase/composition.svg

`showcase/composition.svg` (960x720) is the *Composition* panel of `showcase.png`: one SVG page whose pictures are
other files that CanvasNet draws at the size of their boxes.

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
so are the reused `hull.scad` and `explode.scad`. `render-images.ps1` draws it with `--strict` as a panel of
`showcase.png`, so a link that stops working fails the script.

## gearbox.png

`gearbox.png` (1600x1000) is a product sheet for a planetary gearbox, `gears/gearbox-sheet.svg`, that links one
OpenSCAD model, `gears/gearbox.scad`, four times: assembled, exploded (`#def.explode=1`), cut away
(`#def.cutaway=1`), and from the top (`#view=top`). Its callouts name the OpenSCAD constructs behind each feature
(`linear_extrude` with `twist`, `polygon` with a list comprehension, `difference`, `for`, `hull`, `minkowski`, `text`,
`rotate_extrude`, `intersection`, `sphere`); each ends on a named point of the model through a `cn:x2y2` binding, so
it stays on its feature at any size. Every link fixes the camera (`def.$vpd`, `def.$vpt`). It is referenced from the
README by an absolute URL. `gearbox.scad` and `gearbox-sheet.svg` are **original to this repository** (MIT).

## hero.png

`hero.png` (1600x640) is the README banner, `hero/hero.svg`: the input cards and output tiles are live drawings of
the files beside it, so the banner is itself one composite SVG drawn by CanvasNet. It is referenced from the top of the
README by an absolute URL. Every file in `hero/` is licensed under this repository's MIT license, and none comes from a
third party:

- authored for the banner: `hero.svg`, `figure.svg`, `flow.mmd`, `sequence.puml`, `bearing.scad`, `chart.vl.json`, and
  `vector.svg`;
- `dc-logo-dark.svg`: the DEMA Consulting "DC" monogram of the repository's `Icon.png`, redrawn as vector paths in
  colors for a dark background;
- generated: `drawing.vsdx`, by `make-drawing.py` (Python standard library only; run `python make-drawing.py` in
  `docs/images/hero`);
- copied from `showcase/` (themselves original to this repository): `explode.scad`, `slide.pptx`, and
  `mesh-shading.pdf`.

`drawing.vsdx` is also the Visio example of the user guide chapter *Embedding Pictures in Markdown*, which links it as
`hero/drawing.vsdx` through the guide's resource path.

## mermaid-gallery.png

`mermaid-gallery.png` (1248x680) is a montage of six Mermaid diagram types (a sequence diagram, a class diagram, a
state diagram, a gantt chart, a mindmap, and a git graph), each drawn by `canvasnet render --strict` at twice its size
and shown at 400x300. It is referenced from the README by an absolute URL, like `showcase.png`. The diagrams are the
user guide examples `mermaid-sequence-arrows.mmd`, `mermaid-class-relations.mmd`, `mermaid-state-notes.mmd`,
`mermaid-gantt.mmd`, `mermaid-mindmap.mmd`, and `mermaid-gitgraph.mmd` in `docs/user_guide/examples`, which are
**original to this repository** (MIT). `render-images.ps1` draws and checks the montage like the other images.

## plantuml-gallery.png

`plantuml-gallery.png` (2496x1352) shows six PlantUML diagram types (a sequence diagram, an activity diagram, a class
diagram, a state diagram, a use case diagram, and a component diagram) in a 3x2 grid laid out like
`mermaid-gallery.png`, at twice its size so that the text stays sharp in the README. It is one SVG page,
`plantuml/gallery.svg`, that links each diagram with `<image href>`, so every diagram is drawn as vector at the size of
its 800x600 cell rather than scaled from a PNG. It is referenced from the README by an absolute URL. The diagrams are
`sequence.puml`, `activity.puml`, `class.puml`, `state.puml`, `usecase.puml`, and `component.puml` in
`docs/images/plantuml`, authored for the gallery with landscape layouts and larger fonts so that each fills its cell
(the user guide examples are mostly tall). Every file in `docs/images/plantuml` is **original to this repository**
(MIT). `render-images.ps1` draws it with `--strict` and checks it like the other images.

## mathml-gallery.png

`mathml-gallery.png` (2496x1576) shows six equations (the quadratic formula, the binomial theorem, the Fourier
transform, a transfer function, Maxwell's equations, and a state-space model) in a 2x3 grid at the width of
`plantuml-gallery.png`. It is one SVG page, `mathml/gallery.svg`, that links each equation with `<image href>`, so every
equation is drawn as vector rather than scaled from a PNG. Each image box is four times the natural size of its
equation (the size that `canvasnet info x.mml --json` reports), centered in its cell, so all six share one 64 pixel
font; the rows are as tall as their equations need. Update the box of an equation whenever its natural size changes.
It is referenced from the README by an absolute URL. The equations are `quadratic.mml`, `binomial-theorem.mml`,
`fourier-transform.mml`, `transfer-function.mml`, `maxwell.mml`, and `state-space.mml` in `docs/images/mathml`, written
with the advice of `canvasnet help mathml pitfalls`; a test (`MathMlGalleryTests`) renders each of them in strict mode
and fails on any report. Every file in `docs/images/mathml` is **original to this repository** (MIT).
`render-images.ps1` draws it with `--strict` and checks it like the other images.

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
tool: SVG, OpenSCAD 3D, Vega-Lite, Mermaid, PDF, PowerPoint, PlantUML, and a composition of several formats on one SVG
page. It is referenced from the README by an absolute URL, because the README is also shipped inside the
NuGet packages.

### Provenance and Licensing

Every input is original to this repository and licensed under the repository's MIT license.

- **SVG**: `showcase/dashboard.svg` - original, authored for this repository (MIT)
- **OpenSCAD 3D**: `showcase/hull.scad` - original, authored for this repository (MIT)
- **Vega-Lite**: `showcase/sales.vl.json` - original, authored for this repository (MIT)
- **Mermaid**: `showcase/flowchart.mmd` - original, authored for this repository (MIT)
- **PDF**: `showcase/mesh-shading.pdf` - copy of the hand-authored test fixture
  `shading-mesh-types-4-5-6-7.pdf` (MIT)
- **PowerPoint**: `showcase/slide.pptx` - original, generated by `showcase/make-slide.py` with python-pptx (MIT)
- **PlantUML**: `showcase/checkout.puml` - original, authored for this repository (MIT): a checkout sequence diagram
  with an actor, a database, an entity, an activation, a group, and a note, restyled with `skinparam` colors
- **Composition**: `showcase/composition.svg` and the files it links (see *showcase/composition.svg* above) - original,
  authored for this repository (MIT)

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
- The Vega-Lite, Mermaid, PDF, PowerPoint, and PlantUML panels are padded with white to exactly 4:3 so that every cell
  has the same shape; the SVG, OpenSCAD, and Composition panels are drawn at 800x600 directly.
