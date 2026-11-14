---
title: Composing figures
summary: Combine diagrams, charts, models, and pages in one picture.
see-also:
  - svg/composition
  - svg/framed-figure
  - mermaid/composition
  - vega-lite/composition
  - scad/composition
  - style
  - files
examples:
  - name: themes
    kind: command
    language: shell
    content: 'canvasnet montage "flow.mmd#theme=default" "flow.mmd#theme=forest" "flow.mmd#theme=neutral" --labels --cell 480x240 -o themes.png'
    description: Three themes of one diagram side by side, each chosen with the fragment of its input.
  - name: publish
    kind: command
    language: shell
    content: 'canvasnet flatten figure.svg -o figure.flat.svg'
    description: Write a copy of a figure whose diagrams, charts, models, and pages are PNG pictures, for any SVG reader.
---

Every picture the tool draws can be placed in another: a chart in a diagram
node, a model in a chart, any of them in an SVG figure. Choose the outer
picture by what the figure has to say, size each placement from its natural
size, then render, look, and fix.

## Choose the outer picture

- A page with a title, captions, and several pictures: an SVG figure
  (`canvasnet help svg composition`; `--example report` is a complete one).
- One picture presented in a designed frame - a title band, accents, a
  caption, and arrows that point at its features: a framed SVG figure
  (`canvasnet help svg framed-figure`).
- A process whose steps show the thing itself: Mermaid nodes with pictures
  (`canvasnet help mermaid composition`).
- Several charts of one data set: one Vega-Lite specification with `layer`,
  `concat`, or `facet` (`canvasnet help vega-lite composition`).
- Labels and leader lines on a model: anchors and an SVG overlay
  (`canvasnet help scad composition`).
- Variants or pages side by side: `canvasnet montage`, one cell per input.

## Size each placement

- An embedded diagram, chart, model, SVG file, or page is drawn at the size of
  its placement, so it stays sharp; its text scales with the placement.
- Read the natural size of a diagram, chart, or SVG file with
  `canvasnet info file --json` (`image.width`, `image.height`), keep that
  aspect ratio, and stay near that size so the text of every picture matches.
  A model has no natural aspect: it is fitted to the box it gets.

## Framed figures and callouts

- A framed figure layers a container, a title band, decoration, the picture
  slot, a callout layer, and a caption; arrows use `<marker>` (see
  `canvasnet help svg framed-figure`).
- To point at a feature, find its position in the picture at the size of its
  box: a model reports its anchors (`anchors[].pixel` of `render --json`; see
  `canvasnet help scad anchors`). A diagram or chart reports no positions, but
  rendered alone with `--size` equal to the box it is the picture the figure
  embeds: read the feature's x,y from
  `canvasnet render file --size WxH --grid 50` and confirm it with
  `canvasnet inspect pixel file x,y --size WxH`. Add the image `x` and `y`.

## Options per placement

- A fragment after the file name sets the options of one placement:
  `flow.mmd#theme=dark`, `part.scad#view=front&amp;def.width=20` (`&amp;` in
  XML), `chart.vl.json#param.t=5`, `report.pdf#page=2`, `deck.pptx#slide=3`.
  The same fragment works on a command-line input and in a montage (see
  `canvasnet help svg fragments`).

## Limits and file access

- At most 64 files are embedded in one render and SVG files nest 4 levels
  deep; a Mermaid diagram loads at most {{mmdMaxImages}} pictures and a chart at
  most {{vlMaxImages}} (see `canvasnet help limits`).
- A picture that is not drawn is left out with a warning
  (`svg-image-unresolved`, `mermaid-image-unresolved`); `--strict` makes it an
  error. Links read any local file unless `--file-access` confines them (see
  `canvasnet help files`).

## Render, look, fix

1. `canvasnet render figure.svg --width 1200 -o figure.png --strict --json`
2. Look at the PNG: sizes, alignment, contrast, and anything cut off.
3. Fix and render again; `canvasnet compare` measures what changed.
4. Publish the PNG, or a flattened SVG for a browser, WeasyPrint, or Pandoc
   HTML (`canvasnet help svg flatten`).
