// Original CanvasNet test fixture: a square block trimmed by a round cutter.
// The block covers x 0..20, y 0..20, z 0..10; the cylinder is centered at (10, 10) with radius 12, so it removes
// the four corners of the block (their distance from the center is about 14.1) but not the middle of the sides.
intersection() {
  cube([20, 20, 10]);
  translate([10, 10, -1]) cylinder(r = 12, h = 12, $fn = 64);
}
