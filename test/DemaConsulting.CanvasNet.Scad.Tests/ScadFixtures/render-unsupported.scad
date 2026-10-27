// Original CanvasNet test fixture: constructs that are recognized but not drawn, one per line so that each report
// has a known location (lines 10 and 11: a hull of mixed dimensions, a Minkowski sum with a boolean operand; line
// 12: a % on a 2D shape that is not extruded). Only the red cube and the small cube at x 8..9 are drawn.
surface(file = "logo.dat");
offset(r = 2) square(10);
projection() cube(10);
surface(file = "heights.dat");
import("part.stl");
roof() square(10);
hull() { cube(5); square(3); }
minkowski() { difference() { cube(5); cube(2); } sphere(1); }
%square(3);
color("red") cube(4);
translate([8, 0, 0]) cube(1);
