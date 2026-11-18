// Planetary gearbox: one model, several pictures.
// gearbox-sheet.svg links this file four times; the fragment of each link chooses the variant and the view:
//   gearbox.scad                      the assembled gearbox
//   gearbox.scad#def.explode=1        the parts moved apart along the axis
//   gearbox.scad#def.cutaway=1        the front quarter cut away, with the faces of the cut in red
//   gearbox.scad#view=top             the gears meshing, seen from above
// The echo("anchor", ...) lines at the end name the points the callouts of the sheet point at.
// Original to this repository (MIT).

explode = 0;      // 0 = assembled, 1 = fully exploded
cutaway = 0;      // 1 = cut the front quarter away (housing, bearing, carrier, shaft, screws); the gears stay whole

// Gear geometry (module m, tooth counts); (zs + zr) / planets must be a whole number
m = 2;
zs = 15;          // sun
zp = 12;          // planets
zr = zs + 2 * zp; // ring (39)
planets = 3;
fw = 16;          // face width of the ring
helix = 25;       // helix angle of the herringbone teeth (degrees)
gz = 22;          // bottom of the gear zone
ca = m * (zs + zp) / 2; // carrier radius: center distance of sun and planet
pa = 60;          // angle of the first planet; pa * (zs + zr) / 360 must be a whole number too

$fn = 48;

// Colors
c_housing = "#7d8ea3";
c_sun = "#f5a524";
c_planet = "#2f80ed";
c_carrier = "#3b4a5f";
c_race = "#c7d0db";
c_ball = "#eef2f7";
c_bolt = "#2a313c";
c_shaft = "#a9b6c5";
c_section = "#e5484d";

// ---------------------------------------------------------------- involute teeth
function polar(r, a) = [r * cos(a), r * sin(a)];
function inv_rad(r, rb) = let(t = sqrt(max(0, (r / rb) * (r / rb) - 1))) t - atan(t) * PI / 180;

// Half the angular thickness (degrees) of a tooth at radius r
function half_deg(r, rp, rb, z, bl) =
    let(ia = tan(20) - 20 * PI / 180,
        h = PI / (2 * z) - bl / rp + ia - inv_rad(max(r, rb), rb))
    max(h, 0.004) * 180 / PI;

// One tooth centered on angle 0: right flank from root to tip, then left flank back to the root
function tooth(z, add, ded, bl, n = 5) =
    let(rp = m * z / 2, rb = rp * cos(20), ra = rp + add * m, rr = rp - ded * m,
        r0 = max(rr, rb),
        rs = [for (i = [0:n]) r0 + (ra - r0) * i / n],
        root = rr < rb ? [polar(rr, -half_deg(rb, rp, rb, z, bl))] : [])
    concat(root,
           [for (r = rs) polar(r, -half_deg(r, rp, rb, z, bl))],
           [for (i = [n:-1:0]) polar(rs[i], half_deg(rs[i], rp, rb, z, bl))],
           rr < rb ? [polar(rr, half_deg(rb, rp, rb, z, bl))] : []);

// A whole gear outline as one polygon built by a list comprehension
module gear2d(z, add = 1, ded = 1.25, bl = 0.08) {
    polygon([for (i = [0:z - 1]) for (p = tooth(z, add, ded, bl)) let(a = i * 360 / z)
        [p[0] * cos(a) - p[1] * sin(a), p[0] * sin(a) + p[1] * cos(a)]]);
}

// Herringbone extrusion: two helical halves, the upper one mirrored; tpm is the twist per mm.
// Each half is turned so the outline is unturned at the apex (mid-height), where meshing gears are phased.
module herringbone(h, tpm, slices = 6) {
    rotate([0, 0, tpm * h / 2])
        linear_extrude(height = h / 2 + 0.01, twist = tpm * h / 2, slices = slices) children();
    translate([0, 0, h]) mirror([0, 0, 1]) rotate([0, 0, tpm * h / 2])
        linear_extrude(height = h / 2, twist = tpm * h / 2, slices = slices) children();
}

function tpm(rp) = tan(helix) / rp * 180 / PI;

// A gear with chamfered tooth ends: intersection with a double cone
module gear(z, h, hand, bore) {
    rp = m * z / 2;
    ra = rp + m;
    difference() {
        intersection() {
            herringbone(h, hand * tpm(rp)) gear2d(z);
            union() {
                cylinder(h = h / 2, r1 = ra - 0.9, r2 = ra - 0.9 + h / 2);
                translate([0, 0, h / 2]) cylinder(h = h / 2, r1 = ra - 0.9 + h / 2, r2 = ra - 0.9);
            }
        }
        if (bore > 0) translate([0, 0, -1]) cylinder(h = h + 2, r = bore);
    }
}

// ---------------------------------------------------------------- parts
// With cutaway = 1, a difference() removes the front quarter, and an intersection() with two thin
// slabs paints the faces of the cut in the section color
module cut() {
    if (cutaway == 1) {
        difference() {
            children();
            translate([0, -75, -30]) cube([75, 75, 110]);
        }
        color(c_section) intersection() {
            children();
            union() {
                translate([0, -75, -30]) cube([0.4, 75, 110]);
                translate([0, -0.4, -30]) cube([75, 0.4, 110]);
            }
        }
    } else {
        children();
    }
}

// Rounded flange: minkowski of a box and a cylinder, with four counterbored bolt holes
module flange() {
    difference() {
        minkowski() {
            translate([-50, -50, 0]) cube([100, 100, 4]);
            cylinder(r = 10, h = 4);
        }
        translate([0, 0, -1]) cylinder(h = 10, r = 36);
        for (i = [0:3]) rotate([0, 0, 45 + 90 * i]) translate([70, 0, -1]) {
            cylinder(h = 10, r = 4);
            translate([0, 0, 5.5]) cylinder(h = 10, r = 6.5);
        }
    }
}

// Housing drum: rotate_extrude of a stepped profile, with the ring teeth cut inside it
module housing() {
    difference() {
        union() {
            flange();
            // The profile: a fillet into the flange (a list comprehension of arc points) and a rim at the top
            rotate_extrude($fn = 96) polygon(concat(
                [[18.3, 0], [52, 0]],
                [for (a = [270:-15:180]) [52 + 4 * cos(a), 12 + 4 * sin(a)]],
                [[48, gz + fw - 7], [50, gz + fw - 5], [50, gz + fw - 1.5], [48.5, gz + fw],
                 [36, gz + fw], [36, 12], [18.3, 12]]));
        }
        // Engraved lettering on the flange
        translate([0, -56.5, 7.2]) linear_extrude(height = 2)
            text("CANVASNET", size = 5, font = "sans:style=Bold", halign = "center", valign = "center");
        translate([56.5, 0, 7.2]) rotate([0, 0, 90]) linear_extrude(height = 2)
            text("RATIO 3.6 : 1", size = 4.4, font = "sans:style=Bold", halign = "center", valign = "center");
        translate([0, 0, gz - 1]) rotate([0, 0, 180 / zr])
            herringbone(fw + 2, -tpm(m * zr / 2), slices = 6) gear2d(zr, add = 1.25, ded = 1.0, bl = -0.12);
        for (i = [0:7]) rotate([0, 0, 22.5 + 45 * i]) translate([44.2, 0, gz + fw - 8]) cylinder(h = 10, r = 1.4, $fn = 16);
    }
}


// Socket-head cap screw with a hexagon socket
module screw(r, len) {
    difference() {
        union() {
            cylinder(h = r * 1.4, r = r * 1.6, $fn = 32);
            translate([0, 0, -len]) cylinder(h = len + 0.1, r = r, $fn = 24);
        }
        translate([0, 0, r * 0.6]) cylinder(h = r * 2, r = r * 0.8, $fn = 6);
    }
}

module top_screws() {
    for (i = [0:7]) rotate([0, 0, 22.5 + 45 * i]) translate([44.2, 0, gz + fw + 0.05]) screw(1.4, 6);

}

// The engraved lettering, filled with a colored inlay just below the surface
module lettering() {
    translate([0, -56.5, 7.2]) linear_extrude(height = 0.6)
        text("CANVASNET", size = 5, font = "sans:style=Bold", halign = "center", valign = "center");
    translate([56.5, 0, 7.2]) rotate([0, 0, 90]) linear_extrude(height = 0.6)
        text("RATIO 3.6 : 1", size = 4.4, font = "sans:style=Bold", halign = "center", valign = "center");
}

module flange_screws() {
    for (i = [0:3]) rotate([0, 0, 45 + 90 * i]) translate([70, 0, 5.55]) screw(3.5, 10);
}

// Ball bearing: races from rotate_extrude of grooved profiles, and a ring of balls
module bearing() {
    rotate_extrude($fn = 64) difference() {
        union() {
            translate([8.3, 0]) square([3.2, 8]);
            translate([13.5, 0]) square([4.6, 8]);
        }
        translate([12.5, 4]) circle(r = 2.15, $fn = 24);
    }

}

module balls() {
    for (i = [0:13]) rotate([0, 0, i * 360 / 14]) translate([12.5, 0, 4]) sphere(r = 2, $fn = 16);
}

// Planet carrier: hull of three bosses, the pins, and the output shaft with a key way
module carrier() {
    translate([0, 0, gz - 7]) {
        hull() for (i = [0:planets - 1]) rotate([0, 0, pa + i * 360 / planets]) translate([ca, 0, 0]) cylinder(h = 6, r = 8);
        cylinder(h = 6, r = 14);
    }

}

module pins() {
    for (i = [0:planets - 1]) rotate([0, 0, pa + i * 360 / planets]) translate([ca, 0, gz - 1.1]) {
        cylinder(h = fw + 1.6, r = 4, $fn = 32);
        translate([0, 0, fw + 1.5]) cylinder(h = 1.6, r = 6, $fn = 32);
    }
}

module output_shaft() {
    difference() {
        translate([0, 0, -14]) cylinder(h = gz - 6.9 + 14, r = 8.2, $fn = 48);
        translate([5.6, -2, -15]) cube([4, 4, 12]);
    }
}

module input_shaft() {
    difference() {
        union() {
            translate([0, 0, gz + fw - 0.6]) cylinder(h = 26, r = 6, $fn = 48);
            translate([0, 0, gz + fw + 25.4]) cylinder(h = 1.2, r1 = 6, r2 = 4.8, $fn = 48);
        }
        translate([3.8, -1.5, gz + fw + 12]) cube([4, 3, 20]);
    }
}

// ---------------------------------------------------------------- assembly
e = explode;

color(c_housing) cut() housing();
color(c_sun) cut() lettering();
color(c_bolt) translate([0, 0, e * 14]) cut() flange_screws();
color(c_bolt) translate([0, 0, e * 14]) cut() top_screws();
translate([0, 0, e * 40]) {
    color(c_race) cut() bearing();
    color(c_ball) translate([0, 0, e * 12]) balls();
}
translate([0, 0, e * 78]) {
    color(c_carrier) cut() carrier();
    color(c_shaft) cut() output_shaft();
    color(c_shaft) cut() pins();
}

// Planets: each turned so its teeth fall between the teeth of the sun and the ring
translate([0, 0, e * 104]) for (i = [0:planets - 1]) let(t = pa + i * 360 / planets)
    rotate([0, 0, t]) translate([ca, 0, gz + 0.5])
        rotate([0, 0, 180 - 180 / zp + t * zs / zp]) color(c_planet) gear(zp, fw - 1, -1, 4.4);

translate([0, 0, e * 140]) {
    color(c_sun) translate([0, 0, gz + 0.5]) gear(zs, fw - 1, 1, 0);
    color(c_shaft) input_shaft();
}

// Anchors: named points for the callouts of gearbox-sheet.svg
function at(a, r, z) = [r * cos(a), r * sin(a), z];
echo("anchor", "planet", at(300, ca, gz + fw + e * 104) + [0, 0, 0]);
echo("anchor", "planet-teeth", at(180, ca, 0) + at(300, m * zp / 2 + m * 0.4, gz + 11 + e * 104));
echo("anchor", "sun-teeth", at(170, m * zs / 2 + m * 0.5, gz + fw - 0.5 + e * 140));
echo("anchor", "ring-teeth", at(100, m * zr / 2 - m, gz + fw));
echo("anchor", "drum", at(-20, 49, 12));
echo("anchor", "flange-corner", [-50 - 7.07, -50 - 7.07, 8]);
echo("anchor", "lettering", [-12, -56.5, 8]);
echo("anchor", "ratio", [56.5, 9, 8]);
echo("anchor", "bolt", at(22.5, 44.2, gz + fw + 2 + e * 14));
echo("anchor", "shaft", [0, -6, gz + fw + 18 + e * 140]);
echo("anchor", "carrier", at(180, ca + 8, gz - 1 + e * 78));
echo("anchor", "bearing", at(-90, 16, 8 + e * 40));
echo("anchor", "balls", at(-90 + 360 / 28, 12.5, 4 + e * 52));
echo("anchor", "section", [0.4, -43, 27]);
echo("anchor", "cut-edge", [62, -0.4, 8]);