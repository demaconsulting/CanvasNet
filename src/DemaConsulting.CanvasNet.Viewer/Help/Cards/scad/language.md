---
title: The OpenSCAD language subset
summary: Primitives, transforms, booleans, extrusions, modifiers, and what is left out.
tags:
  - scad
  - authoring
see-also:
  - scad/style
  - scad/diagnostics
  - scad/differences
examples:
  - name: plate
    kind: source
    language: openscad
    file: plate.scad
    description: A plate with a round hole (difference of a cube and a cylinder).
  - name: posts
    kind: source
    language: openscad
    file: posts.scad
    description: A row of posts built from a module and a for loop; -D width=30 changes the spacing.
  - name: torus
    kind: source
    language: openscad
    file: torus.scad
    description: A torus made by rotate_extrude of a circle.
  - name: hull-ghost
    kind: source
    language: openscad
    file: hull-ghost.scad
    description: A colored hull of two spheres and a translucent ghost.
---

Write ordinary OpenSCAD. Everything in Supported is evaluated and drawn; a
construct in Unsupported is left out and reported, or fails with `--strict`.

## Supported

- 3D primitives: cube, sphere, cylinder (cones too), polyhedron.
- 2D shapes: square, circle, polygon, and text (flat glyph outlines; extrude or
  subtract them like any 2D shape).
- Transforms: translate, rotate, scale, mirror, multmatrix, resize.
- color(): CSS names, #RGB, #RGBA, #RRGGBB, #RRGGBBAA, [r, g, b] and [r, g, b,
  a]. An alpha below 1 makes the surface translucent.
- Booleans: union, difference, intersection, render, intersection_for.
- linear_extrude (twist, slices, scale), rotate_extrude, hull of 3D or 2D
  children, minkowski of convex 3D children.
- projection(): the shadow of 3D children on the XY plane (cut = false, the
  default) or their cross-section at z = 0 (cut = true), as a flat shape that 2D
  modules and extrusions use like any other. A model that is entirely 2D, for
  example a projection() drawing sheet, is drawn as a flat top-down view
  (orthographic) unless a view is chosen; its shapes are flat gray unless
  colored. For a stronger drawing use --default-color black, or color the shape
  in the model outside the projection (color("black") projection() ...; colors
  inside projection() are dropped), and --background to choose the paper (white
  by default, transparent keeps alpha).
- Modifiers: ! (show only), * (disable), # (highlight, a translucent copy), %
  (background, a translucent ghost).
- The language: variables, functions, modules with children(), for, if, let,
  comprehensions, echo, assert, include and use, and the built-in functions.
- Curve resolution from $fn, $fa, and $fs.
- -D name=value overrides a variable of the model with a number, a boolean,
  text, or a vector as a JSON array (-D "$vpt=[0,0,20]"); in an SVG link,
  def.name=value in the fragment does the same, and so does the fragment of an
  input path of the command line: canvasnet render "part.scad#def.width=20" -o
  part.png (see [[svg]]).
- text() uses the bundled Liberation Sans unless the model names another font; a
  character the font lacks (such as U+221D) is taken from the bundled Noto
  fonts, and one that no font covers draws the missing-glyph box and gives one
  warning.

## Unsupported

- Modules offset, surface, import, and roof: they are left out and reported.
- Functions parent_module, textmetrics, and fontmetrics (they evaluate to undef
  and are reported), and some argument forms of search().
- minkowski of 2D children, of mixed 2D and 3D children, or of non-convex
  children (a difference, an intersection, a hollow mesh).
- hull with a difference or an intersection among its children, or with mixed 2D
  and 3D children.
- multmatrix with a bottom row other than [0, 0, 0, 1].
- Animation: one still picture per command; --time only sets $t. Run the command
  once per view.
- -D values that are expressions (a number, a boolean, text, or a vector of them
  is accepted).
