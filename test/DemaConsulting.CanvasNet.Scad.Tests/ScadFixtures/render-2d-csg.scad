// Original CanvasNet test fixture: 2D boolean operations distributed into extrusions.
$fn = 64;

// A ring: a disc of radius 10 minus a disc of radius 6, 4 tall, centered at the origin
linear_extrude(height = 4)
  difference() {
    circle(r = 10);
    circle(r = 6);
  }

// A plate centered at x = 40: a 24 x 16 rectangle trimmed by a disc of radius 14, with a round hole at x = -6
// and a square hole at x = +6 (relative to the plate center), 3 tall
translate([40, 0, 0])
  linear_extrude(height = 3)
    difference() {
      intersection() {
        square([24, 16], center = true);
        circle(r = 14);
      }
      translate([-6, 0]) circle(r = 3, $fn = 32);
      translate([6, 0]) square([4, 4], center = true);
    }
