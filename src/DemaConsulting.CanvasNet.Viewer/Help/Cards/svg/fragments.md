---
title: 'Link fragments: pages, views, variables, params, and themes'
summary: '#page=, #view=, #azimuth=, #elevation=, #projection=, #theme=, def., param. in a link or input.'
tags:
  - svg
  - composition
see-also:
  - svg/embedding
  - limits
examples:
  - name: embed-pages
    kind: snippet
    language: xml
    file: pages.svg
    description: 'A diagram that places pages of a PDF, PPTX, and VSDX document side by side, chosen with #page=N and #slide=N (a snippet: it needs those files, so it is not rendered alone).'
---

- **page fragment**: A fragment after the file name is a list of name=value
  parameters separated by & (RFC 8118 style: report.pdf#page=3). Names are not
  case sensitive, the first = of a parameter splits name from value, and %XX
  escapes are decoded after the split. Documents: pdf and vsdx take page=N, pptx
  takes page=N or slide=N (the same slide); N counts from 1 (digits only,
  leading zeros allowed), and with no parameter page 1 is drawn. A model takes
  view=iso|front|back|left|right|top|bottom instead of its automatic view, and
  azimuth=<degrees> and elevation=<degrees> (-90 to 90) turn it like
  `ScadViewOptions.Azimuth` and `Elevation` (they replace the angles of the
  named view, iso without one), and projection=ortho|orthographic|perspective
  chooses the projection like `ScadViewOptions.Perspective`. A Mermaid diagram
  takes theme=default|base|dark|forest|neutral (flow.mmd#theme=dark), like
  `DiagramOptions.Theme`. A MathML equation takes display=block|inline
  (eq.mml#display=inline), like `EquationOptions.Display`. A raster, an SVG
  file, and a PlantUML diagram take no parameter. A model and a chart also take
  the prefixed parameters of variable fragment below. If a name is given twice
  with different values the last one wins, with a warning. A fragment is at most
  {{fragmentMaxLength}} characters and {{fragmentMaxParameters}} parameters.
- **variable fragment**: A model and a chart take parameters whose name starts
  with a prefix, so they never collide with the names above. A model:
  def.<name>=<value> sets the variable <name> of the model exactly like an entry
  of `ScadViewOptions.Defines` (a number, true, false, or text; one pair of
  quotes is removed; a vector is a JSON array such as def.$vpt=[0,0,20]; the
  assignment of the file is replaced), for example
  part.scad#view=front&amp;def.width=20&amp;def.label=%22A%22 (%22 is a double
  quote). The viewport variables work too: def.$vpd=300 fixes the camera
  distance (and with it the scale), def.$vpt=[x,y,z] the target,
  def.$vpr=[90-elevation,0,azimuth] the view angles (def.$vpr=[70,0,45] is
  azimuth 45, elevation 20; see [[scad/views]]), and def.$vpf the
  field of view, so several links of one model can share a scale. A chart:
  param.<name>=<value> sets the param <name> of the chart like
  `ChartOptions.Parameters`, with the value true, false, null, a number, text,
  or JSON (a selection takes its initial state, for example
  chart.vl.json#param.threshold=5&amp;param.brush={%22x%22:[2,6]}). The prefix
  is not case sensitive, the name after it is (as written), the value is decoded
  after the fragment is split, and the last value of a name wins. A value that
  is not usable (a def. name that is not an identifier, malformed JSON, an
  azimuth or elevation that is not a number, an elevation outside -90 to 90, a
  projection that is not ortho, orthographic, or perspective) is
  image-fragment-invalid and the image is not drawn; a param. name the chart
  does not declare is the warning vl-param-unknown and the chart is drawn
  without it. def. on anything but a model, and param. on anything but a chart,
  is image-fragment-unknown-parameter. A def. name the model does not assign
  creates the variable, as `Defines` does.
- **input fragment**: The path of an input takes the same fragment when it is
  named with `InputReference.Parse` for the operations of the Imaging namespace
  (report.pdf#page=3, chart.vl.json#param.t=5, flow.mmd#theme=dark,
  part.scad#def.explode=0, eq.mml#display=inline), with the same parameters
  and values as a link; it overrides the page, the view, the angles, the
  projection, the theme, the display mode, the variables, and the parameters
  of the options for that input (a variable or a
  param by name). The # starts a fragment only when no file has that exact name
  (a file called chart#2.png keeps working); when the name before the # is a
  file, the longest such name is used. A value that cannot be used (the same
  cases as image-fragment-invalid above) is an error with the code
  image-fragment-invalid, and a page the file does not have is
  page-out-of-range; an unknown or repeated parameter name
  (image-fragment-unknown-parameter, image-fragment-duplicate-parameter,
  vl-param-unknown) is a warning, also in strict mode, as for a link.
- **page example**: `<image href="report.pdf#page=2" .../>` draws the second
  page of the PDF file; `<image href="deck.pptx#slide=3" .../>` draws the third
  slide; `<image href="drawing.vsdx#page=2" .../>` the second page;
  `<image href="part.scad#view=front" .../>` the front view of a model. Inside
  an XML attribute a second parameter is written with &amp;, for example
  report.pdf#page=2&amp;page=3. See a whole diagram in
  [[svg#embed-pages]].
