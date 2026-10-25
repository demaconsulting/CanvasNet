// Original CanvasNet test fixture: a flat plate with a round through hole and a rectangular slot.
// The plate covers x 0..30, y 0..20 and is 4 thick; the hole is centered at (8, 10) and the slot spans
// x 16..26, y 8..12. Both cutters stick out of the plate on both sides, so the holes go all the way through.
plate_width = 30;
plate_depth = 20;
plate_thickness = 4;

difference() {
  cube([plate_width, plate_depth, plate_thickness]);
  translate([8, 10, -1]) cylinder(r = 3, h = plate_thickness + 2, $fn = 48);
  translate([16, 8, -1]) cube([10, 4, plate_thickness + 2]);
}
