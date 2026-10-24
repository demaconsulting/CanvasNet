// Original CanvasNet test fixture: a parametric shelf bracket that combines most language features.
plate_w = 40;
plate_t = 4;
arm_len = 60;
hole_d = 5;
hole_count = 3;
$fn = 32;

function rib_height(len, ratio = 0.6) = len * ratio;
function hole_pitch(width, count) = count > 1 ? (width - 10) / (count - 1) : 0;
function hole_positions(width, count) =
  [for (i = [0 : count - 1]) [5 + i * hole_pitch(width, count), 0]];

module bracket_plate(w = plate_w, t = plate_t, l = arm_len) {
  difference() {
    cube([w, l, t]);
    mounting_holes(w, t);
  }
}

module bracket_rib(len = arm_len, t = plate_t) {
  h = rib_height(len);
  translate([plate_w / 2 - t / 2, 0, t])
    rotate([90, 0, 90])
      linear_extrude(height = t)
        polygon([[0, 0], [len, 0], [0, h]]);
}

module mounting_holes(w, t) {
  assert(hole_count >= 1, "need at least one hole");
  for (p = hole_positions(w, hole_count))
    translate([p[0], 10, -1]) cylinder(d = hole_d, h = t + 2);
}

module shelf_bracket() {
  union() {
    bracket_plate();
    bracket_rib();
    #translate([0, arm_len, 0]) cube([plate_w, plate_t, 20]);
  }
}

shelf_bracket();
