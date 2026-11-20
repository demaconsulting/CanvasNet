---
title: Views and the camera
summary: Named views, azimuth and elevation, projection, picture size, and the viewport variables.
tags:
  - scad
  - hosting
  - design
aliases:
  - camera
see-also:
  - scad/style
  - scad/composition
  - scad/options
  - scad/anchors
---

Pick a named view, or turn it with an azimuth and an elevation; set the size of
the picture. Fix the camera with the viewport variables when several pictures
must share one scale.

## Options

The host sets the camera in a `ScadViewOptions` (`ViewerRenderOptions.Scad` or
`SourceOptions.Scad`), and the size in the render options.

- `View`: The viewpoint: iso (default; a model that is entirely 2D defaults to
  top), front, back, left, right, top, or bottom.
- `Azimuth`: Replace the azimuth of the view: degrees about the Z axis, 0 from
  the front.
- `Elevation`: Replace the elevation of the view: degrees above the XY plane,
  -90 to 90.
- `Perspective`: false for an orthographic projection (the default), true for
  perspective.
- `Width`, `Height`: The picture is 800x800 pixels unless a size is given; a
  scale factor also sizes it. The camera fits the picture; nothing is stretched.

## Supported

- The camera fits the result of the model: what a difference() removes or an
  intersection() cuts away does not count, so a cutter larger than the part
  does not shrink it; ghosts (%) and highlights (#) stay in the picture.
- The viewport variables $vpt, $vpr, $vpd, and $vpf hold the camera of the
  picture, so a model can place geometry relative to it: translate($vpt)
  rotate($vpr) faces the camera. Assigning them at the top level of the model
  sets the view; the `View`, `Azimuth`, and `Elevation` options (or view=,
  azimuth=, and elevation= in a fragment) win over the model and over def.$vpr,
  and the named view (iso without one) supplies only the angles that were not
  given.
- A fixed camera, so that several pictures of one model share a scale: set the
  viewport variables instead of letting the camera fit each picture. Defining
  $vpd as 300 (in `Defines`, or def.$vpd=300 in a fragment) sets the distance of
  the camera, which sets the scale of an orthographic picture (the visible
  height is about 0.4 times the distance); add $vpt=[x,y,z] to center the
  picture (the target is the origin by default). Then pictures of
  part.scad#def.explode=0 and part.scad#def.explode=1 with $vpd=300 and
  $vpt=[0,0,50] show the explode levels at one scale; without them each picture
  is fitted to its own geometry.
- View angles in a link or input fragment: azimuth=<degrees> and
  elevation=<degrees>, like `Azimuth` and `Elevation`
  (part.scad#azimuth=45&elevation=20). The viewport variable works too:
  def.$vpr=[90-elevation,0,azimuth]; for example part.scad#def.$vpr=[70,0,45] is
  azimuth 45, elevation 20 (the default iso view is azimuth 45, elevation
  35.26). The framing stays automatic. In an XML attribute, write the & as
  &amp;; in a URL the $ may be written as %24.
- The projection in a link or input fragment: projection=ortho, orthographic, or
  perspective, like `Perspective` (part.scad#projection=perspective); in an
  input fragment it replaces the projection of the options.

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
