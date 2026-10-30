// Convex hull showcase: rounded enclosure, lobed cam and capsule.
$fn = 48;

box_w = 52;   // enclosure outer size along X
box_d = 34;   // enclosure outer size along Y
box_h = 18;   // enclosure outer height
corner_r = 6; // corner radius
wall = 2.5;   // wall thickness

// Rounded box: hull of four vertical cylinders placed at the corners
module rounded_block(w, d, h, r) {
    hull() {
        for (x = [r, w - r], y = [r, d - r])
            translate([x, y, 0]) cylinder(r = r, h = h);
    }
}

// Enclosure base with a hollow cavity, open at the top
module enclosure() {
    difference() {
        rounded_block(box_w, box_d, box_h, corner_r);
        translate([wall, wall, wall])
            rounded_block(box_w - 2 * wall, box_d - 2 * wall, box_h, corner_r - wall);
    }
}

// Two-lobed cam: 2D hull of two circles, drilled, then extruded
module cam() {
    linear_extrude(height = 8)
        difference() {
            hull() {
                circle(r = 11);
                translate([34, 0]) circle(r = 5);
            }
            circle(r = 4);
        }
}

// Capsule: hull of two spheres
module capsule() {
    hull() {
        sphere(r = 8);
        translate([0, 0, 36]) sphere(r = 8);
    }
}

enclosure();
translate([14, -34, 0]) cam();
translate([76, 12, 8]) capsule();
