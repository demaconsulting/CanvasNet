---
title: Views and the camera
summary: Named views, azimuth and elevation, projection, picture size, and the viewport variables.
aliases:
  - camera
see-also:
  - scad/style
  - scad/composition
  - scad/options
  - scad/anchors
examples:
  - name: render-top
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --view top --size 400x400 -o plate-top.png --json'
    description: Draw plate.scad from above at 400x400 and print the JSON result.
  - name: free-view
    kind: command
    language: shell
    content: 'canvasnet render plate.scad --azimuth 30 --elevation 20 --projection perspective -o plate.png'
    description: Draw plate.scad from a free viewpoint with a perspective camera.
---

Pick a named view with `--view`, or turn it with `--azimuth` and `--elevation`;
set the size with `--size`. Fix the camera with the viewport variables when
several pictures must share one scale.

## Options

- `--view <name>`: The viewpoint: iso (default; a model that is entirely 2D
  defaults to top), front, back, left, right, top, or bottom.
- `--azimuth <degrees>`: Replace the azimuth of the view: degrees about the Z
  axis, 0 from the front.
- `--elevation <degrees>`: Replace the elevation of the view: degrees above the
  XY plane, -90 to 90.
- `--projection <type>`: ortho (default) or perspective.
- `--size <WxH>`: The picture is 800x800 pixels unless a size option is given;
  --width, --height, and --scale also size it. The camera fits the picture;
  nothing is stretched.

## Supported

- The viewport variables $vpt, $vpr, $vpd, and $vpf hold the camera of the
  picture, so a model can place geometry relative to it: translate($vpt)
  rotate($vpr) faces the camera. Assigning them at the top level of the model
  sets the view; --view, --azimuth, and --elevation win over the model.
- A fixed camera, so that several pictures of one model share a scale: set the
  viewport variables instead of letting the camera fit each picture. -D
  '$vpd=300' (or def.$vpd=300 in a fragment) sets the distance of the camera,
  which sets the scale of an orthographic picture (the visible height is about
  0.4 times the distance); add $vpt=[x,y,z] to center the picture (the target is
  the origin by default). Then canvasnet montage "part.scad#def.explode=0"
  "part.scad#def.explode=1" -D '$vpd=300' -D '$vpt=[0,0,50]' --labels -o m.png
  shows the explode levels at one scale; without them each cell is fitted to its
  own geometry.
- View angles in a link or input fragment: azimuth=<degrees> and
  elevation=<degrees>, like --azimuth and --elevation
  ('part.scad#azimuth=45&elevation=20'). The viewport variable works too:
  def.$vpr=[90-elevation,0,azimuth]; for example
  'part.scad#def.$vpr=[70,0,45]' is --azimuth 45 --elevation 20 (the default
  iso view is azimuth 45, elevation 35.26). The framing stays automatic, and
  render --json reports the azimuth and elevation used. Use single quotes in a
  shell, where $ expands inside double quotes, or write the $ as %24.

## Known differences

- The viewport variables follow the documented meaning of the OpenSCAD ones
  ($vpr is the rotation of the camera, so rotate($vpr) turns an object to face
  it). The model sees the default $vpt = [0, 0, 0], $vpd = 140, and $vpf = 22.5
  and the $vpr of the requested view, because the camera is fitted after the
  model is built; only an assignment of $vpt, $vpd, or $vpf turns off the
  automatic framing.
- The result is a static picture from a named view (iso by default, top for a
  model that is entirely 2D; orthographic by default), not an interactive
  camera.
