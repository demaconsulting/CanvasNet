// The OpenSCAD card of hero.svg: a flanged bearing housing on a shaft.
$fn = 72;
$vpd = 185;      // a close camera, so the part fills its card
$vpt = [0, 3, 14];

// Two-hole flange: the hull of a center disk and two ears, drilled
module flange() {
    color("#f59e0b") difference() {
        hull() {
            cylinder(r = 20, h = 6);
            for (x = [-28, 28]) translate([x, 0, 0]) cylinder(r = 9, h = 6);
        }
        for (x = [-28, 28]) translate([x, 0, -1]) cylinder(r = 4, h = 8);
        translate([0, 0, -1]) cylinder(r = 9, h = 8);
    }
}

// Raised boss around the bore
module boss() {
    color("#fbbf24") difference() {
        cylinder(r = 15, h = 20);
        translate([0, 0, -1]) cylinder(r = 9, h = 22);
    }
}

// The shaft through the bore
module shaft() {
    color("#38bdf8") translate([0, 0, -12]) cylinder(r = 7, h = 48);
}

flange();
boss();
shaft();
