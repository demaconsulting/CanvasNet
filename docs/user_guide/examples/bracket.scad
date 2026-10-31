// L-shaped mounting bracket, the model drawn in the user guide by the Pandoc filter.
// Original to this repository (MIT). The variables can be overridden with define-NAME attributes.
$fn = 48;

base_length = 60;   // length of the bracket along X
base_width = 30;    // width of the bracket along Y
thickness = 5;      // thickness of both legs
wall_height = 34;   // height of the upright leg, from the floor
hole_radius = 3.2;  // radius of the mounting holes

module bracket() {
    difference() {
        union() {
            // The foot lies on the floor, the upright leg rises from its back edge.
            cube([base_length, base_width, thickness]);
            translate([0, base_width - thickness, 0]) cube([base_length, thickness, wall_height]);
        }
        // Two mounting holes through the foot.
        for (x = [base_length * 0.25, base_length * 0.75])
            translate([x, base_width * 0.35, -1]) cylinder(r = hole_radius, h = thickness + 2);
        // One hole through the upright leg.
        translate([base_length / 2, base_width + 1, wall_height * 0.65])
            rotate([90, 0, 0]) cylinder(r = hole_radius, h = thickness + 2);
    }
}

bracket();
