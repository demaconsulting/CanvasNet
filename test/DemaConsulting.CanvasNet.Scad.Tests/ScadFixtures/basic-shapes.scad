// Original CanvasNet test fixture: variables, primitives and transform chains.
wall = 2;
$fn = 48;
size = [40, 25, 12];

cube(size);
translate([50, 0, 0]) sphere(r = 8);
translate([0, 40, 0]) rotate([0, 0, 45]) cylinder(h = 15, r1 = 6, r2 = 3, center = true);
scale([1, 2, 0.5]) cube(10, center = true);

{
  translate([-20, 0, 0]) cube([wall, wall, wall]);
  translate([-20, 5, 0]) cube(wall);
}

polyhedron(
  points = [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]],
  faces = [[0, 1, 2], [0, 1, 3], [0, 2, 3], [1, 2, 3]]
);
