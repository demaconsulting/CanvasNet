// Original CanvasNet test fixture: one colored primitive of each solid kind, side by side along X.
// A cube, a sphere and a cylinder, each 12 units wide, with a 4 unit gap between them.
$fn = 64;

color("red") cube([12, 12, 12]);
color("green") translate([22, 6, 6]) sphere(r = 6);
color("blue") translate([38, 6, 0]) cylinder(r = 6, h = 12);
