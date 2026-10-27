// Original CanvasNet test fixture: the four statement modifiers on four cubes along X.
// The first cube is drawn normally and the second is disabled (nothing is drawn). The third (#) is drawn normally
// and also as a translucent highlight in front of it. The fourth (%) is not part of the model: only a translucent
// ghost of it is drawn.
cube(10);
*translate([20, 0, 0]) cube(10);
#translate([40, 0, 0]) cube(10);
%translate([60, 0, 0]) cube(10);
