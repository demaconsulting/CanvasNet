// A plate with a round hole
difference() {
    cube([30, 20, 5]);
    translate([15, 10, -1]) cylinder(h = 7, r = 4, $fn = 48);
}
