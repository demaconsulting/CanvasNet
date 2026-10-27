// Original CanvasNet test fixture: transparency. Every solid is a 10 x 10 x 10 cube at y 0..10 unless noted, and
// the picture is meant to be viewed from above (the top face of most cubes is at z = 10).
//
// Row y 0..10, left to right:
//   x   0..10  a translucent red cube alone (alpha 0.5)
//   x  20..30  a translucent lime cube at z 4..14 floating over an opaque blue plate at z 0..2
//   x  40..50  a translucent red cube with a blind square cavity cut from the top (z 4..10)
//   x  60..70  an orange cube with a ghost (%) passing through its top, which is NOT cut by the ghost
//   x  75..85  a ghost (%) cube beside it
//   x  90..100 a cube with a round through hole, the cutter highlighted with #
// Row y 20..28:
//   x   0..4   color given as a vector with alpha
//   x  10..14  color given as a hex string with alpha
//   x  20..24  color given by name with the alpha argument
//   x  40..48  a ghost (%) of an extruded circle
color([1, 0, 0, 0.5]) cube(10);

color("blue") translate([20, 0, 0]) cube([10, 10, 2]);
color("lime", 0.5) translate([20, 0, 4]) cube(10);

difference() {
  color("red", alpha = 0.5) translate([40, 0, 0]) cube(10);
  translate([43, 3, 4]) cube([4, 4, 7]);
}

color("orange") difference() {
  translate([60, 0, 0]) cube(10);
  %translate([62, 2, 2]) cube([6, 6, 12]);
}
%translate([75, 0, 0]) cube(10);

difference() {
  translate([90, 0, 0]) cube(10);
  #translate([95, 5, -1]) cylinder(h = 12, r = 3, $fn = 32);
}

color([0, 0, 1, 0.5]) translate([0, 20, 0]) cube(4);
color("#ff000080") translate([10, 20, 0]) cube(4);
color("lime", alpha = 0.5) translate([20, 20, 0]) cube(4);
%translate([44, 24, 0]) linear_extrude(height = 4) circle(r = 4, $fn = 24);
