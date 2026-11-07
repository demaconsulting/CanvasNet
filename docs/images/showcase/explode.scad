// A three-part assembly that moves apart as `explode` grows (0 = together, 1 = fully apart).
// docs/images/montage-explode.png draws it three times with one fixed camera.
explode = 0;

module plate() { color("steelblue") cube([40, 40, 8], center = true); }
module post() { color("orange") cylinder(h = 20, r = 8, center = true, $fn = 48); }
module cap() { color("seagreen") sphere(r = 11, $fn = 48); }

plate();
translate([0, 0, 14 + explode * 30]) post();
translate([0, 0, 34 + explode * 60]) cap();
