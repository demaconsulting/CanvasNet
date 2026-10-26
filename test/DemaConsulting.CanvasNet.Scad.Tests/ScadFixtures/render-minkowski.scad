// Original CanvasNet test fixture: minkowski() of convex parts.
$fn = 32;

// A rounded box: a 10 x 6 x 4 block summed with a ball of radius 1 covers x -1..11, y -1..7 with rounded corners
minkowski() { cube([10, 6, 4]); sphere(r = 1); }

// A union of two blocks summed with a 2 x 2 x 2 cube: the sum distributes over the union, giving two 6 x 6 x 6
// blocks at x 20..26 and x 28..34 (y 0..6) with a gap of 2 between them
translate([20, 0, 0]) minkowski() { union() { cube(4); translate([8, 0, 0]) cube(4); } cube(2); }

// A hull as a child: the hull of two 2 x 2 x 2 cubes at opposite corners of an 8 x 8 square, summed with a unit
// cube; its top view is the hexagon (0,0) (3,0) (9,6) (9,9) (6,9) (0,3) at x 40..49, 3 tall
translate([40, 0, 0]) minkowski() { hull() { cube(2); translate([6, 6, 0]) cube(2); } cube(1); }
