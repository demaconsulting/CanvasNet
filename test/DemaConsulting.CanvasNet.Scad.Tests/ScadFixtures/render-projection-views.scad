// Original CanvasNet test fixture: a flanged bushing (a base plate with a central bore, two mounting holes and a boss)
// drawn as a sheet of four views made with projection(): top, front and side shadows and a front section. The views are
// placed at known positions so that the tests can check the shapes by sampling pixels; the part and the sheet were
// designed for these tests.
module part() {
    difference() {
        union() {
            translate([-25, -15, 0]) cube([50, 30, 6]);
            translate([0, 0, 6]) cylinder(h = 14, r = 10, $fn = 48);
        }
        translate([0, 0, -1]) cylinder(h = 30, r = 5, $fn = 48);
        for (x = [-18, 18]) translate([x, 0, -1]) cylinder(h = 8, r = 2.5, $fn = 32);
    }
}

translate([45, 75]) projection(cut = false) part();
translate([45, 25]) projection(cut = false) rotate([-90, 0, 0]) part();
translate([112, 25]) projection(cut = false) rotate([-90, 0, 0]) rotate([0, 0, -90]) part();
translate([112, 75]) projection(cut = true) rotate([-90, 0, 0]) part();
difference() { square([160, 110]); translate([2, 2]) square([156, 106]); }
