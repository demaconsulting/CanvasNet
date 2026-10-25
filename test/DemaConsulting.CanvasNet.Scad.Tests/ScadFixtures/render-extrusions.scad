// Original CanvasNet test fixture: five extruded shapes in a row along X, centered at x = -40, 0, 40, 80 and 120.
$fn = 64;

// A twisted bar centered on its own middle: footprint 8 x 4, 20 tall (z -10..10), a quarter turn over the height
translate([-40, 0, 0])
  linear_extrude(height = 20, twist = 90, slices = 20, center = true) square([8, 4], center = true);

// A five-pointed star (tip radius 10, notch radius 4) with a round hole in the middle, 6 tall
star_points = [for (i = [0 : 9])
  let(r = i % 2 == 0 ? 10 : 4, a = 90 + i * 36) [r * cos(a), r * sin(a)]];
linear_extrude(height = 6)
  difference() {
    polygon(star_points);
    circle(r = 2, $fn = 24);
  }

// A square frustum: 10 x 10 at the bottom, 5 x 5 at the top, 10 tall
translate([40, 0, 0]) linear_extrude(height = 10, scale = 0.5) square(10, center = true);

// A vase turned from a profile: radius 8 at the foot, 5 at the waist (z = 10), 7 at the rim (z = 20)
translate([80, 0, 0]) rotate_extrude() polygon([[0, 0], [8, 0], [8, 2], [5, 10], [7, 20], [0, 20]]);

// A flat ring: inner radius 10, outer radius 13, 2 tall
translate([120, 0, 0]) rotate_extrude() translate([10, 0]) square([3, 2]);
