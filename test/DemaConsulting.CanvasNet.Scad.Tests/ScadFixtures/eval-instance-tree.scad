// Evaluation fixture: the instantiation tree of a parametric part, with modifiers, fragment settings and
// the group nodes created by user modules and loops. The tree is compared with eval-instance-tree.tree.txt.

width = 40;
height = 25;
hole_count = 3;
hole_r = 2;

function hole_x(i) = 8 + i * (width - 16) / (hole_count - 1);

module plate() {
    cube([width, 10, 3]);
}

module holes() {
    for (i = [0 : hole_count - 1])
        translate([hole_x(i), 5, -1])
            cylinder(h = 5, r = hole_r, $fn = 16);
}

module rib() {
    rotate([90, 0, 0])
        linear_extrude(height = 3)
            polygon([[0, 0], [width / 2, 0], [0, height]]);
}

module bracket() {
    difference() {
        union() {
            plate();
            rib();
        }
        holes();
    }
}

$fn = 32;
bracket();
#translate([0, 20, 0]) sphere(r = 4);
%color("red") cube(1);
*cube(1000);
echo("instances built", hole_count);
