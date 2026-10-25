// Original CanvasNet test fixture: every kind of transform on its own colored part.
// The comments give the analytic extent of each part; together the parts span x -20..10, y -10..25, z 0..5.

// A bar turned a quarter turn about Z and moved right: x 8..10, y 0..4, z 0..1
color("red") translate([10, 0, 0]) rotate([0, 0, 90]) cube([4, 2, 1]);

// A cube mirrored across the YZ plane: x -13..-10, y 0..3, z 0..3
color("green") mirror([1, 0, 0]) translate([10, 0, 0]) cube([3, 3, 3]);

// A block stretched by a non-uniform scale: x 0..6, y 20..24, z 0..3
color("blue") translate([0, 20, 0]) scale([2, 1, 0.5]) cube([3, 4, 6]);

// A block resized to a 5 unit cube: x -20..-15, y 20..25, z 0..5
color("orange") translate([-20, 20, 0]) resize([5, 5, 5]) cube([1, 2, 3]);

// A block sheared so that x grows by half of z: x 0..4, y -10..-8, z 0..4
color("purple") translate([0, -10, 0])
  multmatrix([[1, 0, 0.5, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]) cube([2, 2, 4]);
