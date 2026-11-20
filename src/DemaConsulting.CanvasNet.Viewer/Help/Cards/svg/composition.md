---
title: SVG as the layout of a figure
summary: Place diagrams, charts, models, and pages with image href, add titles and captions, and publish.
tags:
  - svg
  - composition
see-also:
  - svg/bindings
  - composition
  - svg/style
  - svg/framed-figure
  - svg/embedding
  - svg/fragments
  - svg/flatten
  - scad/composition
examples:
  - name: report
    kind: snippet
    language: xml
    file: report.svg
    description: A finished figure - a card with a title and a lead line, a styled Mermaid flowchart and a styled Vega-Lite chart, each with a caption.
---

Make one SVG file the page, and place each picture with `<image href>`: the
SVG draws the frame, the titles, and the captions, and each linked file draws
itself sharply at its placement. Print a complete figure with
[[svg/composition#report]]; it links the files of
[[mermaid/style#styled]] (save it as `styled-flow.mmd`)
and [[vega-lite/style#styled]] (`styled-chart.vl.json`).

## Placing pictures

- `<image href="styled-flow.mmd" x="64" y="164" width="872" height="252"/>`: a
  Mermaid or PlantUML diagram, a chart, a model, another SVG file, or a document
  page is drawn at the size of the placement, so it stays sharp at any output
  size.
- Size each placement from the picture's natural size (the size it is drawn at
  when no size is asked for): keep its aspect ratio, and stay near its natural
  size so its text matches the text of the page; a placement twice as large
  draws its text twice as large. A diagram, chart, or model keeps its own aspect
  ratio in any box and is centered in it.
- An href is relative to the folder of the SVG file; write `&amp;` between the
  parameters of a fragment (`part.scad#view=front&amp;def.width=20`).

## One placement, its own options

- A fragment after the file name sets the options of that placement only:
  `flow.mmd#theme=neutral`, `part.scad#view=top`, `chart.vl.json#param.t=5`,
  `report.pdf#page=2`, `deck.pptx#slide=3`, `drawing.vsdx#page=2` (see
  [[svg/fragments]]). The options of the render do not reach an
  embedded file.

## Titles, captions, and backgrounds

- Write titles and captions as SVG `<text>` in the layout. A picture keeps its
  own title (the frontmatter `title` of a diagram, the `title` of a chart), so
  let the caption add what the title does not say: the finding or the source.
- A diagram and a model stay transparent in the figure, so a card shows through
  them; a chart keeps its own background unless the chart sets
  `"background": "transparent"`.

## Framed figures and callouts

- To present one picture in a designed frame (a title band, accents, a
  caption) with arrows that point at its features, see
  [[svg/framed-figure]]: the layers, `<marker>` arrows, and how
  to find where a feature is.
- On a model, the arrows end on its anchors: render the model alone at the size
  of its image, and add the image `x` and `y` to the pixel of each anchor
  (`ViewerImage.Anchors`; see [[scad/composition]]).

## Check and publish

- Render in strict mode: a picture that is not drawn (a missing file, a page
  that does not exist) is then an error instead of an empty frame. Look at the
  picture.
- At most 64 files are embedded in one render and SVG files nest 4 levels deep
  (see [[limits]]); links may read any local file unless the
  file-access policy says otherwise (see [[files]]).
- For a browser, WeasyPrint, or any other SVG reader, write a flattened copy
  whose links are PNG pictures (see [[svg/flatten]]).
