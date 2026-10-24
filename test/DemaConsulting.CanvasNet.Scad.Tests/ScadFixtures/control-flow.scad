// Original CanvasNet test fixture: conditionals, loops and diagnostics statements.
mode = 2;
count = 5;

if (mode == 1) {
  cube(1);
} else if (mode == 2) {
  sphere(1);
} else {
  cylinder(h = 1, r = 1);
}

if (count > 3) cube(2); else sphere(2);

for (i = [0 : 2], j = [10, 20, 30],) translate([i * 5, j, 0]) cube(1);
for (step = [0 : 2 : 8]) translate([step, 0, 0]) cube(1);
for (p = [[0, 0], [4, 0], [4, 4]]) {
  translate([p[0], p[1], 0]) sphere(0.5);
}

intersection_for (k = [1 : 3]) rotate([0, 0, k * 30]) cube([10, 2, 2], center = true);

let (a = 3, b = a * 2) translate([a, b, 0]) cube(1);

echo("count is", count);
echo(mode) cube(1);
assert(count > 0, "count must be positive");
assert(mode < 10) sphere(1);
