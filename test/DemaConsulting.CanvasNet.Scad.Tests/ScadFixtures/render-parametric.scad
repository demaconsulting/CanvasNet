// Original CanvasNet test fixture: a parametric peg board whose size is set by variables that a caller can
// override. A row of `pegs` round pegs, `pitch` apart, stands on a base strip that is as long as the row.
pegs = 3;
pitch = 10;
peg_radius = 3;
peg_height = 6;
base_thickness = 2;
$fn = 32;

module peg(i) {
  translate([i * pitch, 0, 0]) cylinder(r = peg_radius, h = peg_height);
}

// The base spans x -5 .. pegs * pitch - 5 and y -5 .. 5, below z = 0
color("steelblue")
  translate([-pitch / 2, -5, -base_thickness]) cube([pegs * pitch, 10, base_thickness]);

color("orange")
  for (i = [0 : pegs - 1]) peg(i);
