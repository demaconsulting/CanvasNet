// Original CanvasNet test fixture: hull() of 3D parts, of 2D shapes and as a boolean operand.
$fn = 32;

// A six-sided prism: the hull of two 4 x 4 x 4 cubes at opposite corners of a 14 x 14 square, 4 tall.
// Its outline is (0,0) (4,0) (14,10) (14,14) (10,14) (0,4).
hull() { cube(4); translate([10, 10, 0]) cube(4); }

// A 2D hull extruded 3 tall: the hull of three 2 x 2 squares, outline (0,0) (12,0) (12,2) (7,10) (5,10) (0,2),
// moved to x 30..42.
translate([30, 0, 0]) linear_extrude(height = 3)
  hull() { square(2); translate([10, 0]) square(2); translate([5, 8]) square(2); }

// A capsule: the hull of two spheres of radius 2 with their centers 10 apart along X, centered at (65, 5)
translate([60, 5, 2]) hull() { sphere(r = 2); translate([10, 0, 0]) sphere(r = 2); }

// A block with a diagonal slot cut by a hull: the slot is the hull of two 2 x 2 pillars, cut through the 6 tall
// block (20 x 20 at x 0..20, y 30..50); its outline is (4,4) (6,4) (16,14) (16,16) (14,16) (4,6) in block coordinates
translate([0, 30, 0])
  difference() {
    cube([20, 20, 6]);
    translate([4, 4, -1]) hull() { cube([2, 2, 8]); translate([10, 10, 0]) cube([2, 2, 8]); }
  }
