// A rotary control knob: the model of the chapter "Embedding Pictures in Markdown" (original, MIT).
// The variables can be overridden with define-NAME attributes.
$fn = 64;

diameter = 30;  // outer diameter of the knob
height = 16;    // height of the knob
ridges = 18;    // number of grip ridges around the side
shaft = 6;      // diameter of the shaft hole

module knob() {
    difference() {
        union() {
            // The body, with a rounded-off top edge
            cylinder(h = height - 2, d = diameter);
            translate([0, 0, height - 2]) cylinder(h = 2, d1 = diameter, d2 = diameter - 4);
            // The grip ridges around the side
            for (i = [0 : ridges - 1])
                rotate([0, 0, i * 360 / ridges])
                    translate([diameter / 2, 0, 0]) cylinder(h = height - 3, d = 2.4, $fn = 16);
        }
        // The pointer line on the top, and the shaft hole underneath
        translate([0, -0.8, height - 1.2]) cube([diameter / 2 - 3, 1.6, 2]);
        translate([0, 0, -1]) cylinder(h = height * 0.7, d = shaft);
    }
}

knob();
