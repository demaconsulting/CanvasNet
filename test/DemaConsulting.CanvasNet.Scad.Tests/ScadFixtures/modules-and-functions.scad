// Original CanvasNet test fixture: module and function definitions.
function area(w, h = 1) = w * h;
function clamp01(v) = v < 0 ? 0 : (v > 1 ? 1 : v);
function total(list, i = 0) = i >= len(list) ? 0 : list[i] + total(list, i + 1);
function scaled(v, k) = let(n = len(v)) [for (i = [0 : n - 1]) v[i] * k];

module peg(radius = 2, height = 10, $fn = 24) {
  cylinder(r = radius, h = height);
}

module ring_of(count = 6, radius = 20) {
  for (i = [0 : count - 1])
    rotate([0, 0, i * 360 / count])
      translate([radius, 0, 0])
        children();
}

module nested_demo(depth = 3) {
  module leaf(s) { cube(s); }
  function half(v) = v / 2;
  if (depth > 0) {
    leaf(half(depth));
    nested_demo(depth - 1);
  }
}

module wrapper() children(0);

module nothing();

ring_of(count = 8, radius = 15) peg(radius = 1.5);
nested_demo();
