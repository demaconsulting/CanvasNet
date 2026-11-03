// A color, a hull, and a translucent ghost (the ghost is not part of the model)
color("steelblue") hull() { sphere(r = 4, $fn = 32); translate([15, 0, 0]) sphere(r = 2, $fn = 32); }
%translate([0, 0, 6]) cube(3);
