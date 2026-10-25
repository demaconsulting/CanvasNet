// Original CanvasNet test fixture: the four statement modifiers on four cubes along X.
// Only the first and the third cube are drawn; the second is disabled and the fourth is a background object.
cube(10);
*translate([20, 0, 0]) cube(10);
#translate([40, 0, 0]) cube(10);
%translate([60, 0, 0]) cube(10);
